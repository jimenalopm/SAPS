using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SAPS.FunctionalTests.Infraestructura;

namespace SAPS.FunctionalTests.Paginas;

/// <summary>
/// Gestión de usuarios (/Usuarios). Las pruebas solo tocan cuentas de prueba (zz-selenium-...@recyplast.cr)
/// vinculadas al colaborador ficticio DEMO001; nunca una cuenta real.
/// </summary>
public sealed class UsuariosPagina(IWebDriver driver, WebDriverWait espera)
{
    public void Abrir()
    {
        driver.Navigate().GoToUrl(Configuracion.UrlBase + "/Usuarios");
        espera.Until(d => d.FindElements(By.CssSelector("table.table-recyplast")).Count > 0);
    }

    /// <summary>¿El formulario «Nueva cuenta» ofrece al colaborador de prueba (activo y sin cuenta)?</summary>
    public bool ColaboradorDemoDisponible()
    {
        driver.Navigate().GoToUrl(Configuracion.UrlBase + "/Usuarios/Crear");
        espera.Until(d => d.FindElements(By.Id("IdColaborador")).Count > 0);
        return OpcionDemo() is not null;
    }

    /// <summary>Llena «Nueva cuenta» con el colaborador DEMO001 y la envía. Espera volver al listado.</summary>
    public void Crear(string correo, string contrasena, string rol)
    {
        driver.Navigate().GoToUrl(Configuracion.UrlBase + "/Usuarios/Crear");
        espera.Until(d => d.FindElements(By.Id("Correo")).Count > 0);
        driver.FindElement(By.Id("Correo")).SendKeys(correo);
        driver.FindElement(By.Id("ContrasenaInicial")).SendKeys(contrasena);
        new SelectElement(driver.FindElement(By.Id("Rol"))).SelectByValue(rol);
        var demo = OpcionDemo() ?? throw new NoSuchElementException("El colaborador DEMO001 no está disponible para vincular.");
        new SelectElement(driver.FindElement(By.Id("IdColaborador"))).SelectByValue(demo.GetAttribute("value")!);
        driver.FindElement(By.CssSelector("form.form-card button[type='submit']")).ClicSeguro();
        EsperarMensaje("creada");
    }

    /// <summary>Abre «Editar» de la cuenta, cambia el rol y guarda.</summary>
    public void CambiarRol(string correo, string rolNuevo)
    {
        Abrir();
        Fila(correo).FindElement(By.LinkText("Editar")).ClicSeguro();
        espera.Until(d => d.FindElements(By.Id("Rol")).Count > 0);
        new SelectElement(driver.FindElement(By.Id("Rol"))).SelectByValue(rolNuevo);
        driver.FindElement(By.CssSelector("form.form-card button[type='submit']")).ClicSeguro();
        EsperarMensaje("actualizada");
    }

    public void Desactivar(string correo) => BotonConConfirmacion(correo, "Desactivar", "desactivada");

    public void Reactivar(string correo) => BotonConConfirmacion(correo, "Reactivar", "reactivada");

    /// <summary>Desactiva la cuenta de prueba si quedó activa (limpieza). Nunca lanza excepción.</summary>
    public void DesactivarSiActiva(string correo)
    {
        try
        {
            Abrir();
            if (Fila(correo).FindElements(By.XPath(".//button[normalize-space()='Desactivar']")).Count > 0)
                Desactivar(correo);
        }
        catch { /* la limpieza no debe ocultar el resultado de la prueba */ }
    }

    public string EstadoDe(string correo)
    {
        Abrir();
        return Fila(correo).FindElement(By.CssSelector("td[data-label='Estado']")).Text.Trim();
    }

    private IWebElement Fila(string correo) =>
        driver.FindElements(By.CssSelector("table.table-recyplast tbody tr"))
            .FirstOrDefault(tr => tr.FindElements(By.CssSelector("td[data-label='Correo']"))
                .Any(td => td.Text.Trim().Equals(correo, StringComparison.OrdinalIgnoreCase)))
        ?? throw new NoSuchElementException($"No se encontró la cuenta {correo} en el listado.");

    private IWebElement? OpcionDemo() =>
        driver.FindElements(By.CssSelector("#IdColaborador option"))
            .FirstOrDefault(o => o.Text.Trim().StartsWith(DatosDePrueba.ColaboradorDemo, StringComparison.OrdinalIgnoreCase));

    /// <summary>Pulsa el botón de la fila y acepta la confirmación (ventana de la app o, si no la hay, la del navegador).</summary>
    private void BotonConConfirmacion(string correo, string boton, string mensaje)
    {
        Abrir();
        Fila(correo).FindElement(By.XPath($".//button[normalize-space()='{boton}']")).ClicSeguro();
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < limite)
        {
            try { driver.SwitchTo().Alert().Accept(); break; }
            catch (NoAlertPresentException) { }
            var aceptar = driver.FindElements(By.Id("modalConfirmarAceptar")).FirstOrDefault(b => b.Displayed && b.Enabled);
            if (aceptar is not null) { aceptar.ClicSeguro(); break; }
            Thread.Sleep(200);
        }
        EsperarMensaje(mensaje);
    }

    private void EsperarMensaje(string contiene) =>
        espera.Until(d => d.FindElements(By.CssSelector(".alert-recyplast")).Any(a => a.Text.Contains(contiene)));
}
