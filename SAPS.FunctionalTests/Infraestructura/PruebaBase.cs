using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace SAPS.FunctionalTests.Infraestructura;

/// <summary>
/// Base de todas las pruebas funcionales. xUnit crea una instancia nueva por prueba, así que cada prueba
/// abre su propio Chrome (sin cookies de otra prueba) y lo cierra en <see cref="Dispose"/>.
/// </summary>
public abstract class PruebaBase : IDisposable
{
    protected IWebDriver Driver { get; }
    protected WebDriverWait Espera { get; }

    /// <summary>Carpeta de evidencias de la HU (Evidencias/HU001, etc.).</summary>
    private readonly string _carpetaEvidencias;
    private readonly string _nombrePrueba;
    private int _paso;

    /// <param name="hu">Código de la historia, p. ej. "HU001"; define la carpeta de capturas.</param>
    /// <param name="salida">Lo inyecta xUnit; de aquí se obtiene el nombre de la prueba en ejecución para nombrar las capturas.</param>
    protected PruebaBase(string hu, ITestOutputHelper salida)
    {
        _carpetaEvidencias = Path.Combine(RaizProyecto(), "Evidencias", hu);
        Directory.CreateDirectory(_carpetaEvidencias);
        _nombrePrueba = NombreDePrueba(salida);

        var opciones = new ChromeOptions();
        opciones.AddArgument("--window-size=1440,900");
        opciones.AddArgument("--lang=es-CR");
        opciones.AddArgument("--disable-notifications");
        // El certificado de desarrollo de ASP.NET (https://localhost) no es "oficial".
        opciones.AcceptInsecureCertificates = true;
        // Evita el aviso "guardar contraseña" de Chrome, que taparía la pantalla en las capturas.
        opciones.AddUserProfilePreference("credentials_enable_service", false);
        opciones.AddUserProfilePreference("profile.password_manager_enabled", false);
        if (Configuracion.SinVentana) opciones.AddArgument("--headless=new");

        Driver = new ChromeDriver(opciones);
        Espera = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
    }

    // ------------------------------------------------------------------ navegación

    /// <summary>Abre una ruta de la app (p. ej. "/Pedidos") usando SAPS_URL como base.</summary>
    protected void Ir(string ruta) => Driver.Navigate().GoToUrl(Configuracion.UrlBase + ruta);

    protected bool EstaEnLogin => Driver.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);
    protected bool EstaEnAccesoDenegado => Driver.Url.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ sesión

    /// <summary>Entra con las credenciales reales del rol (leídas de las variables de entorno).</summary>
    protected void IniciarSesion(Rol rol, bool recordar = false)
    {
        var (correo, clave) = Configuracion.Credenciales(rol);
        IntentarLogin(correo, clave, recordar);
        // Éxito = salimos de la pantalla de login.
        Espera.Until(d => !d.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Llena el formulario de login y lo envía. No espera ningún resultado: cada prueba decide qué verificar.</summary>
    protected void IntentarLogin(string correo, string clave, bool recordar = false)
    {
        Ir("/Identity/Account/Login");
        Espera.Until(d => d.FindElements(By.Id("Input_CodigoEmpleado")).Count > 0);
        Driver.FindElement(By.Id("Input_CodigoEmpleado")).SendKeys(correo);
        Driver.FindElement(By.Id("Input_Password")).SendKeys(clave);
        if (recordar) Driver.FindElement(By.Id("Input_RememberMe")).ClicSeguro();
        Driver.FindElement(By.Id("login-submit")).ClicSeguro();
    }

    /// <summary>Descarta la sesión actual (borra cookies) y entra con otro rol; sirve para verificar lo mismo desde otro perfil.</summary>
    protected void CambiarSesion(Rol rol, bool recordar = false)
    {
        Driver.Manage().Cookies.DeleteAllCookies();
        IniciarSesion(rol, recordar);
    }

    /// <summary>Usa el botón "Cerrar sesión" del encabezado, como lo haría la usuaria.</summary>
    protected void CerrarSesion()
    {
        Driver.FindElement(By.CssSelector(".btn-cerrar-sesion")).ClicSeguro();
        Espera.Until(d => d.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase)
                          || d.Url.Contains("/Account/Logout", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Etiqueta de rol que muestra el encabezado ("Administrador", "Recursos Humanos" o "Soda").</summary>
    protected string RolMostrado => Driver.FindElement(By.CssSelector(".app-usuario-rol")).Text.Trim();

    /// <summary>Opciones del menú lateral, en el orden en que se muestran.</summary>
    protected List<string> MenuLateral() =>
        Driver.FindElements(By.CssSelector(".sidebar-link")).Select(e => e.Text.Trim()).ToList();

    /// <summary>Menú que debe ver cada rol según el layout de la app (mismas reglas que los [Authorize]).</summary>
    protected static string[] MenuEsperado(Rol rol) => rol switch
    {
        Rol.Administrador => ["Inicio", "Catálogo", "Registrar pedido", "Recursos Humanos"],
        Rol.RecursosHumanos => ["Inicio", "Catálogo", "Recursos Humanos"],
        Rol.Soda => ["Inicio", "Registrar pedido"],
        _ => []
    };

    protected static string RolEsperado(Rol rol) => rol switch
    {
        Rol.Administrador => "Administrador",
        Rol.RecursosHumanos => "Recursos Humanos",
        _ => "Soda"
    };

    // ------------------------------------------------------------------ utilidades

    /// <summary>
    /// Quita separadores de miles y símbolo de colón de un monto mostrado ("₡1 500", "₡1.500") y devuelve el entero.
    /// Falla si el texto no es un entero (es justo lo que verifica RNF-005).
    /// </summary>
    protected static int MontoEntero(string texto)
    {
        var limpio = Regex.Replace(texto, @"[\s₡]", "");
        Assert.Matches(@"^\d{1,3}([.,]\d{3})*$|^\d+$", limpio);
        return int.Parse(Regex.Replace(limpio, @"[.,]", ""));
    }

    /// <summary>Cantidad total de pedidos que muestra el inicio (tarjeta "Pedidos"). Sirve para comprobar si se guardó o no uno.</summary>
    protected int TotalPedidosEnInicio()
    {
        Ir("/");
        var texto = Espera.Until(d => d.FindElements(By.XPath(
            "//p[contains(@class,'stat-etiqueta') and normalize-space()='Pedidos']/following-sibling::p[contains(@class,'stat-valor')]"))
            .FirstOrDefault())!.Text;
        return int.Parse(Regex.Replace(texto, @"\D", ""));
    }

    /// <summary>Texto del cuerpo de una respuesta JSON abierta directamente en el navegador (p. ej. /Pedidos/Catalogo).</summary>
    protected string CuerpoJson() => Driver.FindElement(By.TagName("body")).Text;

    /// <summary>Guarda una captura en Evidencias/HUxxx/ con un número de paso para ordenarlas.</summary>
    protected void Capturar(string descripcion)
    {
        _paso++;
        var limpio = Regex.Replace(descripcion, @"[^\w\-]+", "_").Trim('_');
        var archivo = $"{_nombrePrueba}__{_paso:00}_{limpio}.png";
        ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(Path.Combine(_carpetaEvidencias, archivo));
    }

    /// <summary>Nombre del método de prueba + sus parámetros (p. ej. "..._Administrador"), apto para nombre de archivo.</summary>
    private static string NombreDePrueba(ITestOutputHelper salida)
    {
        // xUnit 2 no expone la prueba actual; ITestOutputHelper la guarda en un campo privado "test".
        var prueba = salida.GetType().GetField("test", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(salida) as ITest;
        if (prueba is null) return "prueba";
        var nombre = prueba.TestCase.TestMethod.Method.Name;
        // Las pruebas con [InlineData] se ejecutan varias veces: se agregan los argumentos para no pisar las capturas.
        if ((prueba.TestCase as IXunitTestCase)?.TestMethodArguments is { Length: > 0 } argumentos)
            nombre += "__" + string.Join("_", argumentos.Select(a => a?.ToString()));
        nombre = Regex.Replace(nombre, @"[^\w\-]+", "_").Trim('_');
        return nombre.Length > 120 ? nombre[..120] : nombre;
    }

    /// <summary>Busca la carpeta del proyecto subiendo desde bin/Debug/...; así Evidencias queda junto al .csproj.</summary>
    private static string RaizProyecto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SAPS.FunctionalTests.csproj"))) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        try { Capturar("fin_de_la_prueba"); } catch { /* el navegador pudo cerrarse; no ocultar el error real de la prueba */ }
        Driver.Quit();
        Driver.Dispose();
        GC.SuppressFinalize(this);
    }
}
