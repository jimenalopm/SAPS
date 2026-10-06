using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SAPS.FunctionalTests.Infraestructura;

namespace SAPS.FunctionalTests.Paginas;

/// <summary>Renglón del detalle del pedido tal como se ve en pantalla.</summary>
public record LineaPedido(string Articulo, string Precio, int Cantidad, string Subtotal);

/// <summary>Acciones sobre la pantalla "Registrar pedido" (/Pedidos).</summary>
public sealed class PedidosPagina(IWebDriver driver, WebDriverWait espera)
{
    /// <summary>Abre la pantalla y espera a que el catálogo termine de cargar.</summary>
    public void Abrir()
    {
        driver.Navigate().GoToUrl(Configuracion.UrlBase + "/Pedidos");
        espera.Until(d => d.FindElements(By.Id("codigo-colaborador")).Count > 0);
        espera.Until(d => d.FindElement(By.Id("estado-catalogo")).Text.Length > 0);
    }

    /// <summary>Escribe el código, pulsa "Buscar colaborador" y espera la tarjeta con sus datos o un mensaje de error.</summary>
    public void BuscarColaborador(string codigo)
    {
        var campo = driver.FindElement(By.Id("codigo-colaborador"));
        campo.Clear();
        campo.SendKeys(codigo);
        driver.FindElement(By.CssSelector("#buscar-colaborador button[type='submit']")).ClicSeguro();
        espera.Until(d => ColaboradorIdentificado || HayMensaje);
    }

    public bool ColaboradorIdentificado => driver.FindElement(By.Id("datos-colaborador")).Displayed;
    public string NombreColaborador => driver.FindElement(By.Id("nombre-colaborador")).Text.Trim();
    public string CodigoMostrado => driver.FindElement(By.Id("codigo-encontrado")).Text.Trim();

    /// <summary>La foto (o, si el colaborador no tiene, sus iniciales) que sirve para verificar su identidad.</summary>
    public bool FotoOInicialesVisibles
    {
        get
        {
            var foto = driver.FindElement(By.Id("foto-colaborador"));
            if (!foto.Displayed) return false;
            return foto.GetCssValue("background-image").Contains("url(") || foto.Text.Trim().Length > 0;
        }
    }

    public bool HayMensaje => driver.FindElement(By.Id("mensaje-pedido")).Displayed;
    public string Mensaje => driver.FindElement(By.Id("mensaje-pedido")).Text.Trim();

    public void ElegirTipoComida(string tipo) =>
        new SelectElement(driver.FindElement(By.Id("tipo-comida"))).SelectByText(tipo);

    /// <summary>Opciones del selector de artículos ("Sopa — ₡1.500", "Fresco · Grande — ₡2.000"...).</summary>
    public List<string> OpcionesDeArticulo() =>
        new SelectElement(driver.FindElement(By.Id("articulo"))).Options.Select(o => o.Text.Trim()).Where(t => t.Contains('—')).ToList();

    /// <summary>
    /// Selecciona el artículo cuyo nombre es EXACTAMENTE <paramref name="nombre"/> (la opción se ve "Nombre — ₡precio"
    /// o "Nombre · Tamaño — ₡precio") y lo agrega con la cantidad indicada.
    /// </summary>
    public void AgregarArticulo(string nombre, int cantidad = 1)
    {
        var select = new SelectElement(driver.FindElement(By.Id("articulo")));
        var opcion = select.Options.FirstOrDefault(o =>
            o.Text.StartsWith(nombre + " —", StringComparison.OrdinalIgnoreCase) ||
            o.Text.StartsWith(nombre + " ·", StringComparison.OrdinalIgnoreCase))
            ?? throw new NoSuchElementException($"El artículo «{nombre}» no está en el catálogo de pedidos.");
        select.SelectByValue(opcion.GetDomAttribute("value")!);
        var campoCantidad = driver.FindElement(By.Id("cantidad"));
        campoCantidad.Clear();
        campoCantidad.SendKeys(cantidad.ToString());
        driver.FindElement(By.Id("agregar")).ClicSeguro();
        espera.Until(d => d.FindElements(By.CssSelector("#lineas-pedido tr")).Count > 0);
    }

    /// <summary>Agrega la opción cuyo texto completo es el indicado (p. ej. "Gaseosa · Grande — ₡1.200").</summary>
    public void AgregarOpcion(string textoOpcion, int cantidad = 1)
    {
        var select = new SelectElement(driver.FindElement(By.Id("articulo")));
        select.SelectByText(textoOpcion);
        var campoCantidad = driver.FindElement(By.Id("cantidad"));
        campoCantidad.Clear();
        campoCantidad.SendKeys(cantidad.ToString());
        driver.FindElement(By.Id("agregar")).ClicSeguro();
        espera.Until(d => d.FindElements(By.CssSelector("#lineas-pedido tr")).Count > 0);
    }

    /// <summary>Opciones de la categoría "Bebidas" que tienen tamaño (el texto trae " · Tamaño").</summary>
    public List<string> BebidasConTamano() =>
        driver.FindElements(By.CssSelector("#articulo optgroup[label='Bebidas'] option"))
            .Select(o => o.Text.Trim()).Where(t => t.Contains(" · ")).ToList();

    /// <summary>Quita el renglón indicado (0 = primero) con el botón "Quitar".</summary>
    public void QuitarLinea(int indice)
    {
        var antes = driver.FindElements(By.CssSelector("#lineas-pedido tr")).Count;
        driver.FindElements(By.CssSelector("#lineas-pedido tr"))[indice].FindElement(By.CssSelector("button")).ClicSeguro();
        espera.Until(d => d.FindElements(By.CssSelector("#lineas-pedido tr")).Count == antes - 1);
    }

    /// <summary>Texto visible de toda la pantalla (para verificar que no hay otras formas de pago).</summary>
    public string TextoDePantalla => driver.FindElement(By.TagName("body")).Text;

    public List<LineaPedido> Lineas() =>
        driver.FindElements(By.CssSelector("#lineas-pedido tr")).Select(tr =>
        {
            var celdas = tr.FindElements(By.TagName("td"));
            return new LineaPedido(celdas[0].Text.Trim(), celdas[1].Text.Trim(),
                int.Parse(celdas[2].FindElement(By.TagName("input")).GetDomProperty("value")!), celdas[3].Text.Trim());
        }).ToList();

    public string TotalTexto => driver.FindElement(By.Id("total-pedido")).Text.Trim();
    public bool BotonRegistrarHabilitado => driver.FindElement(By.Id("registrar-pedido")).Enabled;

    /// <summary>Pulsa "Registrar pedido", confirma en la ventana y devuelve el número y total que muestra la app.</summary>
    public (string NumeroPedido, string Total) RegistrarYConfirmar()
    {
        driver.FindElement(By.Id("registrar-pedido")).ClicSeguro();
        espera.Until(d => d.FindElement(By.Id("aceptar-registro-pedido")).Displayed);
        // La ventana de confirmación se anima: se espera a que el botón esté clicable.
        espera.Until(d => d.FindElement(By.Id("aceptar-registro-pedido")).Enabled);
        driver.FindElement(By.Id("aceptar-registro-pedido")).ClicSeguro();
        espera.Until(d => d.FindElement(By.Id("numero-pedido-registrado")).Text.Trim().Length > 0);
        return (driver.FindElement(By.Id("numero-pedido-registrado")).Text.Trim(),
                driver.FindElement(By.Id("total-pedido-registrado")).Text.Trim());
    }

    /// <summary>Cierra la ventana "Pedido registrado correctamente".</summary>
    public void CerrarMensajeExito()
    {
        driver.FindElement(By.CssSelector("#pedido-registrado .btn-recyplast")).ClicSeguro();
        espera.Until(d => !d.FindElement(By.Id("pedido-registrado")).Displayed);
    }
}
