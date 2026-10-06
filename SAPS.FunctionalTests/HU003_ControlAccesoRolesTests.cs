using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-003 — Control de acceso por roles (Administrador, Recursos Humanos y Soda).
/// Solo se consulta y se navega: ninguna prueba de esta HU guarda datos.
/// Nota de nombres: en la app el rol "Personal de cocina" de la HU se llama "Soda" y "RH" se llama "RecursosHumanos".
/// </summary>
public class HU003_ControlAccesoRolesTests(ITestOutputHelper salida) : PruebaBase("HU003", salida)
{
    // ===================================================================== Criterios 1 y 5: validación de rol y menú

    [Theory(DisplayName = "HU-003 Menú según rol: el menú lateral muestra solo las opciones del rol")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    [InlineData(Rol.Soda)]
    public void MenuSegunRol_cadaRolVeSoloSusOpciones(Rol rol)
    {
        // Al iniciar sesión el sistema valida el rol y habilita solo las funcionalidades de su perfil.
        IniciarSesion(rol);
        Capturar("menu_del_rol");

        Assert.Equal(RolEsperado(rol), RolMostrado);

        // El menú debe ser EXACTAMENTE el del rol: ni una opción de más ni de menos.
        var menu = MenuLateral();
        Assert.Equal(MenuEsperado(rol), menu);

        // Criterio 2: la usuaria de Soda no ve reportes/usuarios/bitácora ni gestión de catálogo.
        // Criterio 3: RH no ve "Registrar pedido".
        if (rol == Rol.Soda)
        {
            Assert.DoesNotContain("Catálogo", menu);
            Assert.DoesNotContain("Recursos Humanos", menu);
        }
        if (rol == Rol.RecursosHumanos) Assert.DoesNotContain("Registrar pedido", menu);
    }

    // ===================================================================== Criterio 2: usuaria de Soda

    [Fact(DisplayName = "HU-003 Usuaria de Soda: puede abrir el registro de pedidos")]
    public void UsuariaSoda_puedeAccederAlRegistroDePedidos()
    {
        IniciarSesion(Rol.Soda);
        Ir("/Pedidos");
        Espera.Until(d => d.FindElements(By.Id("codigo-colaborador")).Count > 0);
        Capturar("soda_en_registro_de_pedidos");

        Assert.False(EstaEnAccesoDenegado);
        Assert.Contains("/Pedidos", Driver.Url);
        Assert.Contains("Registrar pedido", Driver.FindElement(By.CssSelector("h1")).Text);
    }

    // ===================================================================== Criterio 6: acceso fuera de alcance

    [Theory(DisplayName = "HU-003 Acceso fuera de alcance: Soda escribiendo la dirección directamente es rechazada")]
    [InlineData("/Administracion/Catalogo")]
    [InlineData("/Administracion/CrearProducto")]
    [InlineData("/Administracion/CrearBebida")]
    [InlineData("/RecursosHumanos")]
    public void AccesoFueraDeAlcance_sodaPorUrlDirecta_noEjecutaLaOperacion(string ruta)
    {
        IniciarSesion(Rol.Soda);

        // La usuaria escribe la dirección a mano en el navegador (no hay enlace en su menú).
        Ir(ruta);
        Capturar("acceso_denegado_" + ruta.Trim('/').Replace('/', '_'));

        // El sistema no muestra la pantalla: la redirige a "Acceso no autorizado".
        Assert.True(EstaEnAccesoDenegado, $"Soda no debería poder abrir {ruta}, pero quedó en {Driver.Url}");
        Assert.Contains("Acceso no autorizado", Driver.FindElement(By.CssSelector("h1")).Text);
    }

    // ===================================================================== Criterio 3: Recursos Humanos

    [Theory(DisplayName = "HU-003 Recursos Humanos: accede a catálogo y a su área")]
    [InlineData("/Administracion/Catalogo")]
    [InlineData("/RecursosHumanos")]
    public void RecursosHumanos_accedeALasPantallasDeSuPerfil(string ruta)
    {
        IniciarSesion(Rol.RecursosHumanos);
        Ir(ruta);
        Capturar("rh_en_" + ruta.Trim('/').Replace('/', '_'));

        Assert.False(EstaEnAccesoDenegado, $"RH debería poder abrir {ruta}");
        Assert.False(EstaEnLogin);
        Assert.Contains(ruta, Driver.Url);
    }

    [Fact(DisplayName = "HU-003 Recursos Humanos: no registra pedidos (URL directa rechazada)")]
    public void RecursosHumanos_noPuedeRegistrarPedidos()
    {
        IniciarSesion(Rol.RecursosHumanos);

        Ir("/Pedidos");
        Capturar("rh_pedidos_acceso_denegado");

        Assert.True(EstaEnAccesoDenegado, "RH no debe poder abrir el registro de pedidos.");
        Assert.Empty(Driver.FindElements(By.Id("codigo-colaborador")));
    }

    // ===================================================================== Criterio 4: Administrador

    [Theory(DisplayName = "HU-003 Administrador: acceso completo a las pantallas existentes")]
    [InlineData("/")]
    [InlineData("/Administracion/Catalogo")]
    [InlineData("/Pedidos")]
    [InlineData("/RecursosHumanos")]
    public void Administrador_accedeATodasLasPantallas(string ruta)
    {
        // Solo se verifican las pantallas que existen hoy; usuarios, bitácora y anulación no están en el sistema todavía.
        IniciarSesion(Rol.Administrador);
        Ir(ruta);
        Capturar("admin_en_" + (ruta.Trim('/').Replace('/', '_') is { Length: > 0 } n ? n : "inicio"));

        Assert.False(EstaEnAccesoDenegado, $"El Administrador debería poder abrir {ruta}");
        Assert.False(EstaEnLogin);
        Assert.Empty(Driver.FindElements(By.CssSelector(".pagina-estado")));
    }

    // ===================================================================== Sin sesión

    [Theory(DisplayName = "HU-003 Sin sesión: las pantallas protegidas redirigen al inicio de sesión")]
    [InlineData("/Pedidos")]
    [InlineData("/Administracion/Catalogo")]
    [InlineData("/RecursosHumanos")]
    public void SinSesion_pantallasProtegidas_redirigenAlLogin(string ruta)
    {
        Ir(ruta);
        Espera.Until(d => d.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
        Capturar("redirige_al_login_" + ruta.Trim('/').Replace('/', '_'));

        Assert.True(EstaEnLogin);
        Assert.Empty(Driver.FindElements(By.CssSelector(".sidebar-link")));
    }

    // ===================================================================== No implementados

    [Fact(Skip = "No implementado en el código del Sprint 1: no existe la gestión de usuarios (activar cuentas y asignar uno " +
                 "de los tres roles). Los roles se asignan fuera de la app. Confirmar con el equipo si es del Sprint 1.")]
    public void RolesDefinidos_administradorActivaCuentaYAsignaRol() { }

    [Fact(Skip = "No implementado en el código del Sprint 1: no existe bitácora de altas, bajas ni cambios de rol (RNF-002).")]
    public void BitacoraDeRoles_altaBajaOCambioDeRolQuedaRegistrado() { }

    [Fact(Skip = "No implementado en el código del Sprint 1: no existe la consulta de bitácora, por lo que no se puede probar " +
                 "que solo el Administrador la vea ni que sus registros sean inalterables.")]
    public void AccesoALaBitacora_soloAdministradorYRegistrosInalterables() { }

    [Fact(Skip = "No implementado: tampoco existen reportes (HU-013/HU-014), anulación de pedidos ni altas/bajas de usuarios " +
                 "que el Administrador deba poder usar. Se cubrirán cuando existan.")]
    public void Administrador_accesoCompletoAReportesUsuariosYAnulacion() { }
}
