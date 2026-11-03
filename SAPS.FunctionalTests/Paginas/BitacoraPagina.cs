using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SAPS.FunctionalTests.Infraestructura;

namespace SAPS.FunctionalTests.Paginas;

/// <summary>Fila de la bitácora tal como se ve en la tabla.</summary>
public record FilaBitacora(string Evento, string Usuario, string Detalle, string Resultado, IWebElement Elemento);

/// <summary>
/// Pantalla de bitácora (/Bitacora). Solo consulta: la pantalla no tiene ninguna acción que escriba.
/// Los filtros se llenan en el formulario como lo haría el Administrador y se envían con «Filtrar».
/// </summary>
public sealed class BitacoraPagina(IWebDriver driver, WebDriverWait espera)
{
    public void Abrir()
    {
        driver.Navigate().GoToUrl(Configuracion.UrlBase + "/Bitacora");
        EsperarTabla();
    }

    /// <summary>
    /// Llena los filtros del formulario y pulsa «Filtrar». Los parámetros nulos se dejan como estén.
    /// <paramref name="tipoEvento"/> y <paramref name="resultado"/> usan el texto visible ("Cambio de precio", "Fallido").
    /// </summary>
    public void Filtrar(string? tipoEvento = null, string? resultado = null, string? usuario = null, string? texto = null)
    {
        if (tipoEvento is not null) new SelectElement(driver.FindElement(By.Id("f-tipo"))).SelectByText(tipoEvento);
        if (resultado is not null) new SelectElement(driver.FindElement(By.Id("f-resultado"))).SelectByText(resultado);
        if (usuario is not null) Escribir("f-usuario", usuario);
        if (texto is not null) Escribir("f-texto", texto);
        driver.FindElement(By.CssSelector(".filtros-acciones button[type='submit']")).ClicSeguro();
        espera.Until(d => d.Url.Contains('?'));
        EsperarTabla();
    }

    /// <summary>Filas de la página actual (sin la fila de «no hay eventos»).</summary>
    public List<FilaBitacora> Filas() =>
        driver.FindElements(By.CssSelector("table.table-recyplast tbody tr:not(.fila-vacia)"))
            .Select(tr => new FilaBitacora(
                Celda(tr, "Evento"), Celda(tr, "Usuario"), Celda(tr, "Detalle"), Celda(tr, "Resultado"), tr))
            .ToList();

    /// <summary>Etiquetas de los filtros aplicados («Evento: Cambio de precio»…).</summary>
    public List<string> FiltrosActivos() =>
        driver.FindElements(By.CssSelector(".chip-filtro")).Select(e => e.Text.Trim()).ToList();

    public bool MuestraSoloLectura => driver.FindElements(By.CssSelector(".badge-solo-lectura")).Count > 0;

    /// <summary>Abre el detalle de la fila y espera el título del evento.</summary>
    public void VerDetalle(FilaBitacora fila)
    {
        fila.Elemento.FindElement(By.PartialLinkText("Ver detalle")).ClicSeguro();
        espera.Until(d => d.Url.Contains("/Bitacora/Detalle", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Valor de un dato del detalle (dt → dd), p. ej. «Usuario» o «Producto».</summary>
    public string? DatoDelDetalle(string etiqueta) =>
        driver.FindElements(By.XPath($"//dt[normalize-space()='{etiqueta}']/following-sibling::dd[1]"))
            .FirstOrDefault()?.Text.Trim();

    /// <summary>Valores «anterior» y «nuevo» del recuadro «Cambio registrado».</summary>
    public (string Anterior, string Nuevo) CambioDelDetalle()
    {
        var valores = driver.FindElements(By.CssSelector(".comparacion-valor")).Select(e => e.Text.Trim()).ToList();
        return (valores[0], valores[1]);
    }

    public string? DiferenciaDelDetalle() =>
        driver.FindElements(By.CssSelector(".comparacion-diferencia")).FirstOrDefault()?.Text.Trim();

    private void Escribir(string id, string valor)
    {
        var campo = driver.FindElement(By.Id(id));
        campo.Clear();
        campo.SendKeys(valor);
    }

    private void EsperarTabla() =>
        espera.Until(d => d.FindElements(By.CssSelector("table.table-recyplast tbody")).Count > 0);

    private static string Celda(IWebElement fila, string etiqueta) =>
        fila.FindElements(By.CssSelector($"td[data-label='{etiqueta}']")).FirstOrDefault()?.Text.Trim() ?? "";
}
