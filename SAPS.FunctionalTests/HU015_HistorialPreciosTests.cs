using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using SAPS.FunctionalTests.Paginas;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-015 — Historial de cambios de precio en la bitácora (Sprint 2).
/// Corre contra la BD REAL: NUNCA se cambia el precio de un producto real. Cada prueba crea un producto
/// ZZ-SELENIUM-&lt;fecha-hora&gt;, le cambia el precio y al final lo desactiva. Los eventos de bitácora de ese producto
/// quedan como evidencia (la tabla es de solo lectura); el producto se borra con Sql/limpieza_pruebas.sql.
/// </summary>
public class HU015_HistorialPreciosTests(ITestOutputHelper salida) : PruebaBase("HU015", salida)
{
    private CatalogoPagina Catalogo => new(Driver, Espera);
    private BitacoraPagina Bitacora => new(Driver, Espera);

    private void LimpiarProducto(string nombre)
    {
        try
        {
            CambiarSesion(Rol.Administrador);
            Catalogo.DesactivarSiExiste(nombre);
        }
        catch { /* la limpieza nunca debe ocultar el resultado de la prueba */ }
    }

    private void EsperarConfirmacion() =>
        Espera.Until(d => d.FindElements(By.CssSelector(".alert-recyplast")).Any(a => a.Text.Trim().Length > 0));

    /// <summary>Crea el producto de prueba a 1500 y le cambia el precio a cada valor de <paramref name="nuevos"/>.</summary>
    private void CrearYCambiarPrecios(string nombre, params string[] nuevos)
    {
        Catalogo.CrearProducto(nombre, "1500");
        EsperarConfirmacion();
        foreach (var precio in nuevos)
        {
            Catalogo.CambiarPrecioUnico(nombre, precio);
            EsperarConfirmacion();
        }
    }

    // ===================================================================== Criterios 1 a 4: registro del cambio

    [Theory(DisplayName = "HU-015 Cambio de precio: queda registrado con producto, precio anterior, nuevo, usuario y rol")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    public void CambioDePrecio_quedaRegistradoConValoresYUsuario(Rol rol)
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(rol);
            CrearYCambiarPrecios(nombre, "1800");
            Capturar("precio_cambiado");

            // Solo el Administrador consulta la bitácora.
            CambiarSesion(Rol.Administrador);
            Bitacora.Abrir();
            Bitacora.Filtrar(tipoEvento: "Cambio de precio", texto: nombre);
            Capturar("cambio_en_bitacora");

            var fila = Bitacora.Filas().First();
            Assert.Contains(nombre, fila.Detalle);
            Assert.Contains("Exitoso", fila.Resultado);
            Assert.DoesNotContain("Cambio directo en BD", fila.Usuario);

            Bitacora.VerDetalle(fila);
            Capturar("detalle_del_cambio");
            var (anterior, nuevo) = Bitacora.CambioDelDetalle();
            Assert.Equal(1500, MontoEntero(anterior));
            Assert.Equal(1800, MontoEntero(nuevo));
            Assert.StartsWith("Diferencia: +₡300", Bitacora.DiferenciaDelDetalle());
            Assert.StartsWith(nombre, Bitacora.DatoDelDetalle("Producto"));

            var (correo, _) = Configuracion.Credenciales(rol);
            Assert.Contains(correo, Bitacora.DatoDelDetalle("Usuario"), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(RolEsperado(rol), Bitacora.DatoDelDetalle("Rol"));
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== Criterio 6: historial por producto

    [Fact(DisplayName = "HU-015 Historial: varios cambios del mismo producto se ven en orden, del más reciente al más antiguo")]
    public void VariosCambios_seVenComoHistorialDelProducto()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            CrearYCambiarPrecios(nombre, "1800", "2000");

            Bitacora.Abrir();
            Bitacora.Filtrar(tipoEvento: "Cambio de precio", texto: nombre);
            Capturar("historial_del_producto");

            var cambios = Bitacora.Filas();
            Assert.True(cambios.Count >= 2, "Se esperaban al menos dos cambios de precio del producto de prueba.");
            Assert.All(cambios, f => Assert.Contains(nombre, f.Detalle));

            Bitacora.VerDetalle(cambios[0]);
            var (anterior, nuevo) = Bitacora.CambioDelDetalle();
            Assert.Equal((1800, 2000), (MontoEntero(anterior), MontoEntero(nuevo)));
        }
        finally { LimpiarProducto(nombre); }
    }

    [Fact(DisplayName = "HU-015 Historial: desde el detalle se llega a todos los cambios de precio del producto")]
    public void EnlaceDelDetalle_llevaAlHistorialDelProducto()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            CrearYCambiarPrecios(nombre, "1700");

            Bitacora.Abrir();
            Bitacora.Filtrar(tipoEvento: "Cambio de precio", texto: nombre);
            Bitacora.VerDetalle(Bitacora.Filas().First());

            Driver.FindElement(By.PartialLinkText("Ver todos los cambios de precio")).ClicSeguro();
            Espera.Until(d => d.Url.Contains("texto=", StringComparison.OrdinalIgnoreCase));
            Capturar("historial_desde_el_detalle");

            Assert.Contains(Bitacora.FiltrosActivos(), c => c.Contains("Cambio de precio"));
            Assert.Contains(Bitacora.FiltrosActivos(), c => c.Contains(nombre));
            Assert.All(Bitacora.Filas(), f => Assert.Contains(nombre, f.Detalle));
        }
        finally { LimpiarProducto(nombre); }
    }

    [Fact(DisplayName = "HU-015 Sin cambio: guardar el producto con el mismo precio no agrega un evento")]
    public void GuardarSinCambiarElPrecio_noRegistraEvento()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            CrearYCambiarPrecios(nombre, "1800");

            Bitacora.Abrir();
            Bitacora.Filtrar(tipoEvento: "Cambio de precio", texto: nombre);
            var antes = Bitacora.Filas().Count;

            Catalogo.CambiarPrecioUnico(nombre, "1800");   // mismo precio
            EsperarConfirmacion();

            Bitacora.Abrir();
            Bitacora.Filtrar(tipoEvento: "Cambio de precio", texto: nombre);
            Capturar("sin_evento_nuevo");
            Assert.Equal(antes, Bitacora.Filas().Count);
        }
        finally { LimpiarProducto(nombre); }
    }
}
