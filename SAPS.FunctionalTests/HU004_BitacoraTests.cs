using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using SAPS.FunctionalTests.Paginas;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-004 — Bitácora de actividad y accesos (Sprint 2).
/// Corre contra la BD REAL. Los inicios y cierres de sesión de las pruebas quedan en la bitácora, como pasaría con
/// cualquier persona (la tabla es de solo lectura y no se puede limpiar). Las altas, bajas y cambios de rol se hacen
/// SOLO sobre una cuenta de prueba zz-selenium-...@recyplast.cr vinculada al colaborador ficticio DEMO001; al terminar
/// queda desactivada y Sql/limpieza_pruebas.sql la borra.
/// </summary>
public class HU004_BitacoraTests(ITestOutputHelper salida) : PruebaBase("HU004", salida)
{
    private BitacoraPagina Bitacora => new(Driver, Espera);
    private UsuariosPagina Usuarios => new(Driver, Espera);

    // ===================================================================== Criterio 5: solo el Administrador

    [Fact(DisplayName = "HU-004 Acceso: el Administrador ve «Bitácora» en el menú y la abre en modo solo lectura")]
    public void Administrador_abreLaBitacoraEnSoloLectura()
    {
        IniciarSesion(Rol.Administrador);
        Assert.Contains("Bitácora", MenuLateral());

        Bitacora.Abrir();
        Capturar("bitacora_administrador");

        Assert.False(EstaEnAccesoDenegado);
        Assert.Contains("Bitácora", Driver.FindElement(By.CssSelector("h1")).Text);
        Assert.True(Bitacora.MuestraSoloLectura);
        Assert.NotEmpty(Bitacora.Filas());

        // Criterio 6: ningún botón para modificar o borrar eventos; solo «Ver detalle».
        var botones = Driver.FindElements(By.CssSelector("table.table-recyplast button, table.table-recyplast form"));
        Assert.Empty(botones);
        Assert.DoesNotContain(Driver.FindElements(By.CssSelector("table.table-recyplast a")),
            a => a.Text.Contains("Editar") || a.Text.Contains("Eliminar"));
    }

    [Theory(DisplayName = "HU-004 Acceso: Recursos Humanos y Soda no ven la bitácora ni la abren por URL")]
    [InlineData(Rol.RecursosHumanos)]
    [InlineData(Rol.Soda)]
    public void OtrosRoles_noAccedenALaBitacora(Rol rol)
    {
        IniciarSesion(rol);
        Assert.DoesNotContain("Bitácora", MenuLateral());

        Ir("/Bitacora");
        Capturar("acceso_denegado_bitacora");

        Assert.True(EstaEnAccesoDenegado, $"{rol} no debería abrir /Bitacora, pero quedó en {Driver.Url}");
    }

    [Fact(DisplayName = "HU-004 Acceso: sin sesión, la bitácora redirige al inicio de sesión")]
    public void SinSesion_bitacoraRedirigeAlLogin()
    {
        Ir("/Bitacora");
        Espera.Until(d => d.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
        Capturar("redirige_al_login");

        Assert.True(EstaEnLogin);
    }

    // ===================================================================== Criterios 1 y 2: inicios y cierres de sesión

    [Fact(DisplayName = "HU-004 Accesos: el inicio y el cierre de sesión de la usuaria de Soda quedan registrados")]
    public void InicioYCierreDeSesion_quedanRegistrados()
    {
        var (correoSoda, _) = Configuracion.Credenciales(Rol.Soda);
        IniciarSesion(Rol.Soda);
        CerrarSesion();

        CambiarSesion(Rol.Administrador);
        Bitacora.Abrir();
        Bitacora.Filtrar(usuario: correoSoda);
        Capturar("eventos_de_la_usuaria_soda");

        var filas = Bitacora.Filas();
        Assert.True(filas.Count >= 2, "Se esperaban al menos el inicio y el cierre de sesión de la usuaria de Soda.");
        // Lo más reciente va primero: el cierre de sesión y, antes, el inicio de sesión exitoso.
        Assert.Contains("Cierre de sesión", filas[0].Evento);
        Assert.Contains(filas.Skip(1), f => f.Evento.Contains("Inicio de sesión") && f.Resultado.Contains("Exitoso"));
        Assert.All(filas, f => Assert.Contains(correoSoda, f.Usuario, StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "HU-004 Accesos: un intento fallido queda registrado con lo digitado y sin guardar la contraseña")]
    public void InicioDeSesionFallido_quedaRegistradoSinLaContrasena()
    {
        const string claveDigitada = "ClaveIncorrectaSelenium9";
        IntentarLogin(DatosDePrueba.CorreoInexistente, claveDigitada);
        Espera.Until(d => d.FindElements(By.CssSelector(".validation-summary-errors, .alerta-error-recyplast, .text-danger"))
            .Any(e => e.Text.Trim().Length > 0));
        Capturar("login_fallido");

        CambiarSesion(Rol.Administrador);
        Bitacora.Abrir();
        Bitacora.Filtrar(tipoEvento: "Inicio de sesión", resultado: "Fallido", usuario: DatosDePrueba.CorreoInexistente);
        Capturar("intento_fallido_en_bitacora");

        var filas = Bitacora.Filas();
        Assert.NotEmpty(filas);
        Assert.Contains("Fallido", filas[0].Resultado);

        Bitacora.VerDetalle(filas[0]);
        Capturar("detalle_intento_fallido");
        Assert.Equal(DatosDePrueba.CorreoInexistente, Bitacora.DatoDelDetalle("Usuario")?.Split('\n')[0].Trim());
        Assert.DoesNotContain(claveDigitada, Driver.PageSource);
    }

    // ===================================================================== Criterio 3: altas, bajas y cambios de rol

    [SkippableFact(DisplayName = "HU-004 Cuentas: el alta, el cambio de rol, la baja y la reactivación quedan registrados")]
    public void GestionDeCuentas_quedaRegistradaEnLaBitacora()
    {
        var correo = $"zz-selenium-{DateTime.Now:yyyyMMddHHmmss}@recyplast.cr";
        IniciarSesion(Rol.Administrador);
        Skip.IfNot(Usuarios.ColaboradorDemoDisponible(),
            "DEMO001 no existe o ya tiene cuenta. Corra Sql/crear_colaborador_demo.sql (y la limpieza si quedó una cuenta zz-selenium).");

        try
        {
            Usuarios.Crear(correo, "prueba" + Guid.NewGuid().ToString("N")[..6] + "1", "Soda");
            Usuarios.CambiarRol(correo, "RecursosHumanos");
            Usuarios.Desactivar(correo);
            Usuarios.Reactivar(correo);
            Usuarios.Desactivar(correo);
            Capturar("cuenta_de_prueba_desactivada");
            Assert.Equal("Desactivada", Usuarios.EstadoDe(correo));

            Bitacora.Abrir();
            Bitacora.Filtrar(texto: correo);
            Capturar("eventos_de_la_cuenta_de_prueba");

            // Lo más reciente primero: baja, reactivación, baja, cambio de rol y alta.
            var eventos = Bitacora.Filas().Select(f => f.Evento.Split('\n')[0].Trim()).ToList();
            Assert.Equal(["Cuenta desactivada", "Cuenta reactivada", "Cuenta desactivada", "Cambio de rol", "Cuenta creada"], eventos);
            Assert.All(Bitacora.Filas(), f => Assert.Contains("Exitoso", f.Resultado));

            // El cambio de rol guarda el rol anterior y el nuevo, y quién lo hizo.
            var (correoAdmin, _) = Configuracion.Credenciales(Rol.Administrador);
            Bitacora.VerDetalle(Bitacora.Filas()[3]);
            Capturar("detalle_cambio_de_rol");
            var (anterior, nuevo) = Bitacora.CambioDelDetalle();
            Assert.Equal("Soda", anterior);
            Assert.Equal("Recursos Humanos", nuevo);
            Assert.Contains(correoAdmin, Bitacora.DatoDelDetalle("Usuario"), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Administrador", Bitacora.DatoDelDetalle("Rol"));
        }
        finally { Usuarios.DesactivarSiActiva(correo); }
    }

    // ===================================================================== Consulta con filtros

    [Fact(DisplayName = "HU-004 Consulta: filtrar por tipo de evento muestra solo ese evento y la etiqueta del filtro")]
    public void FiltroPorTipoDeEvento_muestraSoloEseEvento()
    {
        IniciarSesion(Rol.Administrador);
        Bitacora.Abrir();
        Bitacora.Filtrar(tipoEvento: "Inicio de sesión", resultado: "Exitoso");
        Capturar("filtro_inicio_de_sesion");

        var filas = Bitacora.Filas();
        Assert.NotEmpty(filas);   // el propio inicio de sesión de esta prueba ya está registrado
        Assert.All(filas, f =>
        {
            Assert.Contains("Inicio de sesión", f.Evento);
            Assert.Contains("Exitoso", f.Resultado);
        });
        Assert.Contains(Bitacora.FiltrosActivos(), c => c.Contains("Inicio de sesión"));
    }
}
