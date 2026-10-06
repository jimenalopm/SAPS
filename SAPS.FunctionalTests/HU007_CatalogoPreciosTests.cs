using System.Globalization;
using System.Text;
using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using SAPS.FunctionalTests.Paginas;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-007 — Catálogo de productos y precios.
/// Esto corre contra la BD REAL: NUNCA se modifica un producto, bebida ni precio real. Todo lo que se escribe usa un
/// producto nuevo llamado ZZ_SELENIUM_&lt;fecha-hora&gt;, que cada prueba desactiva al terminar (bloque finally) para que
/// no aparezca en los pedidos reales. Las filas restantes se borran con Sql/limpieza_pruebas.sql.
/// </summary>
public class HU007_CatalogoPreciosTests(ITestOutputHelper salida) : PruebaBase("HU007", salida)
{
    private CatalogoPagina Catalogo => new(Driver, Espera);
    private PedidosPagina Pedidos => new(Driver, Espera);

    /// <summary>Entra como Administrador y desactiva el producto de prueba si quedó activo (red de seguridad en finally).</summary>
    private void LimpiarProducto(string nombre)
    {
        try
        {
            CambiarSesion(Rol.Administrador);
            Catalogo.DesactivarSiExiste(nombre);
        }
        catch { /* la limpieza nunca debe ocultar el resultado de la prueba */ }
    }

    /// <summary>Espera el mensaje verde de la app tras guardar y lo devuelve.</summary>
    private string EsperarConfirmacion() =>
        Espera.Until(d => d.FindElements(By.CssSelector(".alert-recyplast")).FirstOrDefault(a => a.Text.Trim().Length > 0))!.Text;

    private static string SinTildes(string texto) =>
        new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();

    // ===================================================================== Permisos (Admin / RH)

    [Theory(DisplayName = "HU-007 Permisos: Admin y RH ingresan a la gestión del catálogo y pueden agregar")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    public void Permisos_adminYRh_ingresanALaGestionDelCatalogo(Rol rol)
    {
        IniciarSesion(rol);
        Ir("/Administracion/Catalogo");
        Capturar("catalogo_visible");

        Assert.False(EstaEnAccesoDenegado);
        Assert.Contains("Catálogo", Driver.FindElement(By.CssSelector("h1")).Text);

        // Pueden agregar productos y bebidas (botones de alta presentes en sus pestañas).
        Catalogo.AbrirPestana("productos");
        Assert.NotEmpty(Driver.FindElements(By.LinkText("Nuevo producto")));
        Catalogo.AbrirPestana("bebidas");
        Capturar("pestana_bebidas");
        Assert.NotEmpty(Driver.FindElements(By.LinkText("Nueva bebida")));
    }

    // ===================================================================== Criterio 1: sin permisos

    [Theory(DisplayName = "HU-007 Sin permisos: la usuaria de Soda no accede a la gestión del catálogo")]
    [InlineData("/Administracion/Catalogo")]
    [InlineData("/Administracion/CrearProducto")]
    [InlineData("/Administracion/CrearBebida")]
    public void SinPermisos_soda_noAccedeALaGestionDelCatalogo(string ruta)
    {
        IniciarSesion(Rol.Soda);
        Assert.DoesNotContain("Catálogo", MenuLateral()); // no tiene la opción en el menú...

        Ir(ruta);                                          // ...ni por dirección directa.
        Capturar("acceso_denegado");

        Assert.True(EstaEnAccesoDenegado, $"Soda no debería abrir {ruta}, pero quedó en {Driver.Url}");
    }

    // ===================================================================== Catálogo real (solo lectura)

    [Fact(DisplayName = "HU-007 Catálogo real: el producto Sopa aparece con precio entero")]
    public void CatalogoReal_productoSopa_apareceConPrecioEntero()
    {
        // Solo consulta: se busca un producto real del catálogo cargado por la empresa.
        IniciarSesion(Rol.Administrador);
        Catalogo.AbrirProductos(DatosDePrueba.ProductoReal);
        Capturar("busqueda_producto_real");

        var fila = Catalogo.FilaProducto(DatosDePrueba.ProductoReal);
        Assert.NotNull(fila);
        var precios = CatalogoPagina.PreciosDeFila(fila);
        Assert.NotEmpty(precios);
        // Cada precio mostrado es un número entero (sin decimales), RNF-005.
        foreach (var precio in precios) Assert.True(MontoEntero(precio) > 0);
    }

    // ===================================================================== Criterios 2, 3 (registro) y 6

    [Theory(DisplayName = "HU-007 Registro de producto: Admin y RH lo agregan con precio entero y queda disponible para pedidos")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    public void RegistroDeProducto_conNombreCategoriaYPrecioEntero_quedaDisponibleParaPedidos(Rol rol)
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(rol);

            // Se registra un producto nuevo (de prueba) con precio entero. El precio vive en la BD: se guarda desde la
            // pantalla, sin tocar el código fuente ni redesplegar (RNF-003).
            Catalogo.CrearProducto(nombre, "1500");
            Assert.Contains("creado correctamente", EsperarConfirmacion());
            Capturar("producto_creado");

            // Aparece en el catálogo, activo y con su precio.
            Catalogo.AbrirProductos(nombre);
            var fila = Catalogo.FilaProducto(nombre);
            Assert.NotNull(fila);
            Assert.Equal([1500], CatalogoPagina.PreciosDeFila(fila).Select(MontoEntero));
            Assert.Equal("Activo", CatalogoPagina.EstadoDeFila(fila));
            Capturar("producto_en_catalogo");

            // Y queda disponible para registrar pedidos: la usuaria de Soda lo ve en la pantalla de pedidos con ese precio.
            CambiarSesion(Rol.Soda);
            var disponibles = Catalogo.ArticulosParaPedido().Where(a => a.Nombre == nombre).ToList();
            Capturar("disponible_para_pedidos");
            var articulo = Assert.Single(disponibles);
            Assert.Equal(1500, articulo.Precio);
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== Criterio 3: precios enteros

    [Theory(DisplayName = "HU-007 Precios enteros: un precio con decimales o no numérico no se acepta")]
    [InlineData("1500.5")]
    [InlineData("1500,5")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    public void PreciosEnteros_decimalONoNumerico_noSeAcepta(string precio)
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            Catalogo.CrearProducto(nombre, precio);

            // Se espera que la pantalla muestre un error y NO avance al catálogo.
            var campo = By.Id("PrecioUnico");
            var mensaje = By.CssSelector("span[data-valmsg-for='PrecioUnico']");
            Espera.Until(d => d.FindElements(mensaje).Any(m => m.Text.Trim().Length > 0) || !d.Url.Contains("CrearProducto"));
            Capturar("precio_rechazado_" + precio);

            Assert.Contains("CrearProducto", Driver.Url);
            var hayTexto = Driver.FindElements(mensaje).Any(m => m.Text.Trim().Length > 0);
            var campoInvalido = !(bool)((IJavaScriptExecutor)Driver).ExecuteScript("return arguments[0].validity.valid;", Driver.FindElement(campo))!;
            Assert.True(hayTexto || campoInvalido, "Debe mostrarse un mensaje de error para el precio.");

            // Y el producto no se creó.
            Catalogo.AbrirProductos(nombre, inactivos: true);
            Assert.Null(Catalogo.FilaProducto(nombre));
        }
        finally { LimpiarProducto(nombre); }
    }

    [Fact(DisplayName = "HU-007 Bebidas embotelladas: la bebida también exige precio entero")]
    public void Bebidas_precioConDecimales_noSeAcepta()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        IniciarSesion(Rol.Administrador);
        Ir("/Administracion/CrearBebida");
        Espera.Until(d => d.FindElements(By.Id("NombreBebida")).Count > 0);

        // Se llena el formulario de bebida (tb_Bebida) con un precio decimal.
        Driver.FindElement(By.Id("NombreBebida")).SendKeys(nombre);
        var precio = Driver.FindElement(By.Id("Precio"));
        precio.Clear();
        precio.SendKeys("1200.5");
        Driver.FindElement(By.CssSelector("form.form-card button[type='submit']")).Click();

        var mensaje = By.CssSelector("span[data-valmsg-for='Precio']");
        Espera.Until(d => d.FindElements(mensaje).Any(m => m.Text.Trim().Length > 0) || !d.Url.Contains("CrearBebida"));
        Capturar("bebida_precio_decimal_rechazado");

        // El sistema no la acepta: sigue en el formulario con el mensaje de error.
        Assert.Contains("CrearBebida", Driver.Url);
        Assert.Contains(Driver.FindElements(mensaje), m => m.Text.Trim().Length > 0);

        // Y no quedó creada en la lista de bebidas.
        Catalogo.AbrirPestana("bebidas", "&bebInc=true&bebQ=" + Uri.EscapeDataString(nombre));
        Assert.Empty(Catalogo.Filas());
    }

    // ===================================================================== Criterios 4 y 5: cambio de precio y vigencia

    [Theory(DisplayName = "HU-007 Cambio de precio: queda un solo precio vigente (el nuevo)")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    public void CambioDePrecio_cierraElAnteriorYQuedaUnSoloPrecioVigente(Rol rol)
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(rol);
            Catalogo.CrearProducto(nombre, "1500");
            EsperarConfirmacion();

            // Se modifica el precio del producto de prueba (nunca uno real).
            Catalogo.CambiarPrecioUnico(nombre, "1800");
            Assert.Contains("actualizado correctamente", EsperarConfirmacion());

            // En el catálogo solo se ve el precio nuevo: el anterior quedó cerrado.
            Catalogo.AbrirProductos(nombre);
            var fila = Catalogo.FilaProducto(nombre);
            Assert.NotNull(fila);
            Assert.Equal([1800], CatalogoPagina.PreciosDeFila(fila).Select(MontoEntero));
            Capturar("precio_nuevo_en_catalogo");

            // Y para vender solo existe UN precio vigente del producto (no dos al mismo tiempo).
            CambiarSesion(Rol.Soda);
            var vigentes = Catalogo.ArticulosParaPedido().Where(a => a.Nombre == nombre).ToList();
            Capturar("un_solo_precio_vigente");
            var unico = Assert.Single(vigentes);
            Assert.Equal(1800, unico.Precio);
        }
        finally { LimpiarProducto(nombre); }
    }

    [Fact(DisplayName = "HU-007 Cambio de precio: los pedidos ya registrados conservan el precio con el que se vendieron")]
    public void CambioDePrecio_noAlteraLosPedidosYaRegistrados()
    {
        // ESCRIBE en la BD real: un pedido a nombre del colaborador de prueba DEMO001 (limpiar con Sql/limpieza_pruebas.sql).
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            Catalogo.CrearProducto(nombre, "1500");
            EsperarConfirmacion();

            // Se registra un pedido de 1 unidad a ₡1.500 para DEMO001.
            Pedidos.Abrir();
            Pedidos.BuscarColaborador(DatosDePrueba.ColaboradorDemo);
            Assert.True(Pedidos.ColaboradorIdentificado, "DEMO001 debe existir en rrhh.tb_Colaborador (ver Sql/crear_colaborador_demo.sql).");
            Pedidos.ElegirTipoComida("Almuerzo");
            Pedidos.AgregarArticulo(nombre);
            var (numeroPedido, total) = Pedidos.RegistrarYConfirmar();
            Capturar("pedido_registrado_a_1500");
            Assert.Equal(1500, MontoEntero(total));

            // Después se cambia el precio del producto a ₡1.800.
            Catalogo.CambiarPrecioUnico(nombre, "1800");
            EsperarConfirmacion();

            // El pedido ya registrado sigue costando ₡1.500 (se ve en la actividad reciente del inicio).
            Ir("/");
            var pedido = Espera.Until(d => d.FindElements(By.CssSelector(".lista-actividad li"))
                .FirstOrDefault(li => li.Text.Contains("Pedido " + numeroPedido)));
            Assert.NotNull(pedido);
            Capturar("pedido_conserva_el_precio_original");
            Assert.Equal(1500, MontoEntero(pedido.FindElement(By.CssSelector(".lista-actividad-monto")).Text));

            // Criterio 10: al desactivar el producto, el pedido anterior se conserva en el historial.
            Catalogo.Desactivar(nombre);
            Ir("/");
            Assert.NotNull(Espera.Until(d => d.FindElements(By.CssSelector(".lista-actividad li"))
                .FirstOrDefault(li => li.Text.Contains("Pedido " + numeroPedido))));
            Capturar("pedido_conservado_tras_desactivar");
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== Criterio 7: platillos especiales

    [Fact(DisplayName = "HU-007 Platillos especiales: pueden tener un precio distinto al estándar")]
    public void PlatillosEspeciales_puedenTenerPrecioDistintoAlEstandar()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);
            Catalogo.CrearProducto(nombre, "2500", especial: true);
            EsperarConfirmacion();

            Catalogo.AbrirProductos(nombre);
            var fila = Catalogo.FilaProducto(nombre);
            Assert.NotNull(fila);
            Capturar("platillo_especial_en_catalogo");

            // Se identifica como especial y conserva su propio precio (distinto del de otros productos).
            Assert.Contains("Especial", fila.FindElement(By.CssSelector("td.celda-principal")).Text);
            Assert.Equal([2500], CatalogoPagina.PreciosDeFila(fila).Select(MontoEntero));
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== Criterio 8: tamaños

    [Fact(DisplayName = "HU-007 Tamaños: un producto con tamaños guarda un precio distinto por cada tamaño")]
    public void Tamanos_productoConPequenoMedianoGrande_guardaUnPrecioPorTamano()
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(Rol.Administrador);

            // Los tamaños reales que ofrece el sistema deben incluir pequeño, mediano y grande.
            var tamanos = Catalogo.TamanosEnFormulario();
            Capturar("tamanos_disponibles");
            foreach (var esperado in new[] { "pequeno", "mediano", "grande" })
                Assert.Contains(tamanos, t => SinTildes(t).Contains(esperado));

            // Se crea un producto de prueba con un precio diferente por cada tamaño.
            var precios = tamanos.Select((_, i) => 1000 + 500 * i).ToList();
            Catalogo.CrearProducto(nombre, preciosPorTamano: precios);
            EsperarConfirmacion();

            Catalogo.AbrirProductos(nombre);
            var fila = Catalogo.FilaProducto(nombre);
            Assert.NotNull(fila);
            Capturar("precios_por_tamano");
            var porTamano = CatalogoPagina.PreciosPorTamano(fila);
            Assert.Equal(tamanos.Count, porTamano.Count);
            for (var i = 0; i < tamanos.Count; i++)
                Assert.Equal(precios[i], MontoEntero(porTamano[tamanos[i]]));
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== Criterio 10: desactivar

    [Theory(DisplayName = "HU-007 Desactivar: el producto deja de estar disponible para nuevos pedidos")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    public void Desactivar_productoDejaDeEstarDisponibleParaNuevosPedidos(Rol rol)
    {
        var nombre = DatosDePrueba.NombreProductoPrueba();
        try
        {
            IniciarSesion(rol);
            Catalogo.CrearProducto(nombre, "1500");
            EsperarConfirmacion();

            Catalogo.Desactivar(nombre);
            Capturar("producto_desactivado");

            // Se conserva en el catálogo, marcado como inactivo (visible con "Mostrar inactivos").
            Catalogo.AbrirProductos(nombre, inactivos: true);
            var fila = Catalogo.FilaProducto(nombre);
            Assert.NotNull(fila);
            Assert.Equal("Inactivo", CatalogoPagina.EstadoDeFila(fila));

            // Pero la usuaria de Soda ya no lo encuentra para registrar pedidos.
            CambiarSesion(Rol.Soda);
            Assert.DoesNotContain(Catalogo.ArticulosParaPedido(), a => a.Nombre == nombre);
            Capturar("ya_no_disponible_para_pedidos");
        }
        finally { LimpiarProducto(nombre); }
    }

    // ===================================================================== No implementado

    [Fact(Skip = "No implementado en el código del Sprint 1: los cambios de precio no se registran en bitácora (valor anterior, " +
                 "valor nuevo, usuario y fecha; RNF-002/HU-015). Confirmar con el equipo si es del Sprint 1.")]
    public void Bitacora_cambioDePrecioQuedaRegistradoConValoresUsuarioYFecha() { }
}
