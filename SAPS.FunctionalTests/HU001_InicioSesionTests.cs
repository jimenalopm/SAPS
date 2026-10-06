using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-001 — Inicio de sesión por correo de empleado.
/// Cuentas reales (Administrador, Recursos Humanos, Soda) leídas de variables de entorno.
/// NO se prueba el bloqueo por 5 intentos (lo cubren las pruebas unitarias de SAPS.Tests) y NUNCA se
/// falla un login contra una cuenta real: las credenciales inválidas usan un correo que no existe.
/// </summary>
public class HU001_InicioSesionTests(ITestOutputHelper salida) : PruebaBase("HU001", salida)
{
    private const string NombreCookieSesion = ".AspNetCore.Identity.Application";

    // ===================================================================== Acceso exitoso

    [Theory(DisplayName = "HU-001 Acceso exitoso: la usuaria con cuenta activa entra y ve su rol")]
    [InlineData(Rol.Administrador)]
    [InlineData(Rol.RecursosHumanos)]
    [InlineData(Rol.Soda)]
    public void AccesoExitoso_conCredencialesCorrectas_concedeAccesoSegunRol(Rol rol)
    {
        // Paso 1: abrir la pantalla de login y dejar evidencia del formulario vacío.
        Ir("/Identity/Account/Login");
        Espera.Until(d => d.FindElements(By.Id("Input_CodigoEmpleado")).Count > 0);
        Capturar("pantalla_de_login");

        // Paso 2: iniciar sesión con las credenciales reales del rol (variables de entorno).
        IniciarSesion(rol);
        Capturar("sesion_iniciada");

        // Paso 3: el sistema concede acceso: ya no estamos en el login y aparece el encabezado de la usuaria.
        Assert.False(EstaEnLogin, "Con credenciales correctas no debe quedarse en la pantalla de login.");
        Assert.True(Driver.FindElements(By.CssSelector(".app-usuario")).Count > 0, "Debe verse el encabezado con la usuaria autenticada.");

        // Paso 4: el rol mostrado y el menú corresponden al rol de la cuenta (funcionalidades según rol).
        Assert.Equal(RolEsperado(rol), RolMostrado);
        Assert.Equal(MenuEsperado(rol), MenuLateral());
    }

    // ===================================================================== Criterio 1: estado laboral

    [Fact(Skip = "No implementado en el código del Sprint 1: el login solo valida Identity y no consulta el estado laboral " +
                 "(RNF-006) de quien inicia sesión. Además requeriría una cuenta real de una persona inactiva. Confirmar alcance con el equipo.")]
    public void EstadoLaboral_correoDePersonaInactiva_noConcedeAcceso() { }

    // ===================================================================== Criterio 2: credenciales incorrectas

    [Fact(DisplayName = "HU-001 Credenciales incorrectas: un correo inexistente muestra error y no concede acceso")]
    public void CredencialesIncorrectas_correoInexistente_muestraErrorYNoConcedeAcceso()
    {
        // Se usa un correo que NO existe para no registrar intentos fallidos sobre cuentas reales.
        IntentarLogin(DatosDePrueba.CorreoInexistente, "ClaveIncorrecta123");

        // El sistema muestra el mensaje de error del formulario...
        var alerta = Espera.Until(d => d.FindElements(By.CssSelector(".auth-alerta")).FirstOrDefault(a => a.Text.Trim().Length > 0));
        Capturar("mensaje_de_error");
        Assert.Contains("inválidos", alerta.Text, StringComparison.OrdinalIgnoreCase);

        // ...y no concede el acceso: sigue en el login y no hay menú lateral ni usuaria autenticada.
        Assert.True(EstaEnLogin);
        Assert.Empty(Driver.FindElements(By.CssSelector(".sidebar-link")));
        Assert.Empty(Driver.FindElements(By.CssSelector(".app-usuario")));
    }

    [Fact(DisplayName = "HU-001 Credenciales incorrectas: campos vacíos no permiten ingresar")]
    public void CredencialesIncorrectas_camposVacios_muestraValidacionYNoConcedeAcceso()
    {
        Ir("/Identity/Account/Login");
        Espera.Until(d => d.FindElements(By.Id("login-submit")).Count > 0);

        // Se envía el formulario sin escribir nada.
        Driver.FindElement(By.Id("login-submit")).Click();

        // Aparecen mensajes de validación junto a los campos y la usuaria sigue sin sesión.
        Espera.Until(d => d.FindElements(By.CssSelector("form#account .text-danger")).Any(e => e.Text.Trim().Length > 0));
        Capturar("validacion_campos_vacios");
        Assert.True(EstaEnLogin);
        Assert.Empty(Driver.FindElements(By.CssSelector(".sidebar-link")));
    }

    // ===================================================================== Criterios 3 y 4: bloqueo

    [Fact(Skip = "Excluida por instrucción del equipo: el bloqueo (5 intentos / 15 minutos, RNF-001) ya lo cubren las pruebas " +
                 "unitarias de HU-001 en SAPS.Tests; en funcionales bloquearía una cuenta real de la BD de RecyPlast.")]
    public void BloqueoAutomatico_cincoIntentosFallidos_bloqueaLaCuenta() { }

    [Fact(Skip = "Excluida por instrucción del equipo: ver HU-001 criterio 3 (cubierto por pruebas unitarias en SAPS.Tests).")]
    public void CuentaBloqueada_noConcedeAccesoHastaQuePasenLos15Minutos() { }

    // ===================================================================== Criterios 5, 6 y 8: no implementados

    [Fact(Skip = "No implementado en el código del Sprint 1: no existe la pantalla de primer ingreso para que la usuaria " +
                 "establezca su contraseña. Confirmar con el equipo si es del Sprint 1.")]
    public void PrimerIngreso_usuariaPermiteEstablecerSuPropiaContrasena() { }

    [Fact(Skip = "No implementado en el código del Sprint 1: no hay control de vigencia (10 meses) de la contraseña ni " +
                 "solicitud de cambio al iniciar sesión. Confirmar con el equipo si es del Sprint 1.")]
    public void ContrasenaVencida_solicitaCambiarlaAntesDeDarAcceso() { }

    [Fact(Skip = "No implementado en el código del Sprint 1: no existe bitácora (RNF-002) de inicios de sesión ni pantalla " +
                 "para consultarla. Confirmar con el equipo si es del Sprint 1.")]
    public void Bitacora_inicioDeSesionRegistraUsuariaRolFechaDispositivoIpYResultado() { }

    // ===================================================================== Criterio 7: colaboradores que compran

    [Fact(DisplayName = "HU-001 Colaboradores que compran: su código no sirve para iniciar sesión")]
    public void ColaboradoresQueCompran_sinCuenta_elCodigoNoConcedeAcceso()
    {
        // Un colaborador solo se identifica al registrar una compra: no tiene cuenta ni contraseña.
        // Se usa el colaborador de prueba DEMO001 (ficticio) como "código de colaborador".
        IntentarLogin(DatosDePrueba.ColaboradorDemo, "Cualquier123");

        var alerta = Espera.Until(d => d.FindElements(By.CssSelector(".auth-alerta")).FirstOrDefault(a => a.Text.Trim().Length > 0));
        Capturar("codigo_de_colaborador_rechazado");
        Assert.Contains("inválidos", alerta.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(EstaEnLogin);
        Assert.Empty(Driver.FindElements(By.CssSelector(".app-usuario")));
    }

    // ===================================================================== Criterio 9: cierre de sesión

    [Fact(DisplayName = "HU-001 Cierre de sesión: al cerrar manualmente ya no se accede a pantallas protegidas")]
    public void CierreDeSesion_manual_noPermiteVolverAEntrarSinCredenciales()
    {
        IniciarSesion(Rol.Soda);
        Capturar("sesion_iniciada");
        Ir("/Pedidos");
        Assert.False(EstaEnLogin, "Con sesión activa, la usuaria Soda debe poder abrir /Pedidos.");

        // La usuaria pulsa "Cerrar sesión" en el encabezado.
        CerrarSesion();
        Capturar("sesion_cerrada");

        // Al intentar abrir de nuevo una pantalla protegida, el sistema la manda al login.
        Ir("/Pedidos");
        Espera.Until(d => d.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
        Capturar("pantalla_protegida_redirige_al_login");
        Assert.True(EstaEnLogin);
    }

    [Fact(DisplayName = "HU-001 Cierre de sesión: sin 'Recordar mi sesión' la cookie dura solo mientras el navegador esté abierto")]
    public void CierreDeSesion_sinRecordar_laCookieEsDeSesionDelNavegador()
    {
        IniciarSesion(Rol.Soda, recordar: false);
        Capturar("sesion_sin_recordar");

        // Una cookie "de sesión" no tiene fecha de vencimiento: desaparece al cerrar el navegador.
        var cookie = Driver.Manage().Cookies.GetCookieNamed(NombreCookieSesion);
        Assert.NotNull(cookie);
        Assert.Null(cookie.Expiry);
    }

    [Fact(DisplayName = "HU-001 Cierre de sesión: con 'Recordar mi sesión' la cookie persiste al cerrar el navegador")]
    public void CierreDeSesion_conRecordar_laCookieEsPersistente()
    {
        IniciarSesion(Rol.Soda, recordar: true);
        Capturar("sesion_con_recordar");

        // Una cookie persistente tiene fecha de vencimiento: sobrevive al cierre del navegador
        // hasta que la usuaria cierre sesión manualmente.
        var cookie = Driver.Manage().Cookies.GetCookieNamed(NombreCookieSesion);
        Assert.NotNull(cookie);
        Assert.NotNull(cookie.Expiry);
        Assert.True(cookie.Expiry > DateTime.Now.AddDays(1), "La sesión recordada debe durar más de un día.");
    }

    [Fact(Skip = "No automatizable de forma práctica: comprobar que la sesión no vence por inactividad exigiría dejar el " +
                 "navegador inactivo durante horas. Confirmar con el equipo cómo se evidenciará.")]
    public void CierreDeSesion_laSesionNoVencePorInactividad() { }
}
