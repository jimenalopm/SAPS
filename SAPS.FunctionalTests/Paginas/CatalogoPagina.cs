using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SAPS.FunctionalTests.Infraestructura;

namespace SAPS.FunctionalTests.Paginas;

/// <summary>Artículo tal como lo ofrece la pantalla de pedidos (/Pedidos/Catalogo, JSON).</summary>
public record ArticuloPedido(string Clave, string Nombre, string Categoria, string? Tamano, int Precio);

/// <summary>
/// Acciones sobre la gestión del catálogo (/Administracion/Catalogo). Encapsula los selectores para que las
/// pruebas se lean como pasos de negocio ("crear producto", "editar precio") y no como CSS.
/// </summary>
public sealed class CatalogoPagina(IWebDriver driver, WebDriverWait espera)
{
    private string Base => Configuracion.UrlBase;

    /// <summary>Abre la pestaña Productos, opcionalmente filtrada por nombre y mostrando inactivos.</summary>
    public void AbrirProductos(string? buscar = null, bool inactivos = false)
    {
        var url = $"{Base}/Administracion/Catalogo?tab=productos";
        if (!string.IsNullOrEmpty(buscar)) url += "&prodQ=" + Uri.EscapeDataString(buscar);
        if (inactivos) url += "&prodInc=true";
        driver.Navigate().GoToUrl(url);
        espera.Until(d => d.FindElements(By.CssSelector(".tab-pane.active table")).Count > 0);
    }

    /// <summary>Abre una pestaña cualquiera: productos, categorias, tamanos o bebidas.</summary>
    public void AbrirPestana(string pestana, string? parametros = null)
    {
        driver.Navigate().GoToUrl($"{Base}/Administracion/Catalogo?tab={pestana}{parametros}");
        espera.Until(d => d.FindElements(By.CssSelector(".tab-pane.active table")).Count > 0);
    }

    /// <summary>Filas de la pestaña activa.</summary>
    public IReadOnlyList<IWebElement> Filas() =>
        driver.FindElements(By.CssSelector(".tab-pane.active tbody tr:not(.fila-vacia)"));

    /// <summary>Fila del producto cuyo nombre empieza con el texto dado (o null).</summary>
    public IWebElement? FilaProducto(string nombre) =>
        Filas().FirstOrDefault(f => f.FindElement(By.CssSelector("td.celda-principal")).Text.Trim()
            .StartsWith(nombre, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// ¿Existe un producto con ese nombre, ya sea activo o inactivo? La app separa ambas listas
    /// (sin filtro = solo activos; "Mostrar inactivos" = solo inactivos), así que hay que mirar las dos.
    /// </summary>
    public bool ExisteProducto(string nombre)
    {
        AbrirProductos(nombre);
        if (FilaProducto(nombre) is not null) return true;
        AbrirProductos(nombre, inactivos: true);
        return FilaProducto(nombre) is not null;
    }

    /// <summary>Precios mostrados en la fila: uno por línea (un precio único, o uno por tamaño).</summary>
    public static List<string> PreciosDeFila(IWebElement fila) =>
        fila.FindElements(By.CssSelector("td[data-label='Precio(s)'] strong")).Select(e => e.Text.Trim()).ToList();

    /// <summary>Texto completo de la celda de precios (incluye el nombre del tamaño, p. ej. "Grande: ₡2.000").</summary>
    public static string TextoPrecios(IWebElement fila) =>
        fila.FindElement(By.CssSelector("td[data-label='Precio(s)']")).Text;

    /// <summary>Precio por tamaño de una fila: "Grande: ₡2.000" -> { "Grande", "₡2.000" }.</summary>
    public static Dictionary<string, string> PreciosPorTamano(IWebElement fila) =>
        fila.FindElements(By.CssSelector("td[data-label='Precio(s)'] li")).Select(li => li.Text.Split(':', 2))
            .ToDictionary(partes => partes[0].Trim(), partes => partes[1].Trim());

    public static string EstadoDeFila(IWebElement fila) =>
        fila.FindElement(By.CssSelector("td[data-label='Estado']")).Text.Trim();

    /// <summary>Mensaje verde de confirmación que deja la app tras guardar (TempData).</summary>
    public string? MensajeConfirmacion()
    {
        var alertas = driver.FindElements(By.CssSelector(".alert-recyplast"));
        return alertas.Count > 0 ? alertas[0].Text : null;
    }

    // ------------------------------------------------------------------ crear

    /// <summary>
    /// Llena y envía el formulario "Nuevo producto". Con <paramref name="preciosPorTamano"/> marca "Se vende en distintos
    /// tamaños" y asigna, en orden, un precio a cada tamaño activo; si no, usa el precio único.
    /// No espera el resultado: la prueba decide qué verificar (éxito o rechazo). Con enviar = false deja el formulario lleno sin enviarlo.
    /// </summary>
    public void CrearProducto(string nombre, string? precioUnico = null, bool especial = false,
        IReadOnlyList<int>? preciosPorTamano = null, bool enviar = true)
    {
        driver.Navigate().GoToUrl($"{Base}/Administracion/CrearProducto");
        espera.Until(d => d.FindElements(By.Id("NombreProducto")).Count > 0);
        driver.FindElement(By.Id("NombreProducto")).SendKeys(nombre);

        // Categoría: la primera activa de la lista (no se modifica ninguna categoría, solo se asigna).
        var categoria = new SelectElement(driver.FindElement(By.Id("IdCategoria")));
        categoria.SelectByIndex(1);

        if (especial) driver.FindElement(By.Id("EsEspecial")).ClicSeguro();

        if (preciosPorTamano is null)
        {
            var campo = driver.FindElement(By.Id("PrecioUnico"));
            campo.Clear();
            campo.SendKeys(precioUnico ?? "");
        }
        else
        {
            driver.FindElement(By.Id("requiereTamano")).ClicSeguro();
            var filas = driver.FindElements(By.CssSelector(".precio-tamano-fila"));
            for (var i = 0; i < filas.Count && i < preciosPorTamano.Count; i++)
            {
                filas[i].FindElement(By.CssSelector("input[type='checkbox']")).ClicSeguro();
                var monto = filas[i].FindElement(By.CssSelector("input[name$='.Monto']"));
                monto.Clear();
                monto.SendKeys(preciosPorTamano[i].ToString());
            }
        }
        if (enviar) driver.FindElement(By.CssSelector("form.form-card button[type='submit']")).ClicSeguro();
    }

    /// <summary>Nombres de los tamaños activos que ofrece el formulario de producto.</summary>
    public List<string> TamanosEnFormulario()
    {
        driver.Navigate().GoToUrl($"{Base}/Administracion/CrearProducto");
        espera.Until(d => d.FindElements(By.Id("NombreProducto")).Count > 0);
        // El bloque de tamaños está OCULTO hasta marcar "Se vende en distintos tamaños"; Selenium devuelve "" para
        // el texto de lo oculto. Se marca la casilla para mostrarlo (no se guarda nada: no se envía el formulario).
        driver.FindElement(By.Id("requiereTamano")).ClicSeguro();
        espera.Until(d => d.FindElement(By.Id("bloquePreciosPorTamano")).Displayed);
        return driver.FindElements(By.CssSelector(".precio-tamano-fila .form-check-label")).Select(e => e.Text.Trim()).ToList();
    }

    // ------------------------------------------------------------------ editar precio / desactivar

    /// <summary>Abre el formulario de edición del producto y cambia su precio único.</summary>
    public void CambiarPrecioUnico(string nombre, string nuevoPrecio)
    {
        AbrirProductos(nombre);
        var fila = FilaProducto(nombre) ?? throw new NoSuchElementException($"No se encontró el producto {nombre} en el catálogo.");
        fila.FindElement(By.LinkText("Editar")).ClicSeguro();
        espera.Until(d => d.FindElements(By.Id("PrecioUnico")).Count > 0);
        var campo = driver.FindElement(By.Id("PrecioUnico"));
        campo.Clear();
        campo.SendKeys(nuevoPrecio);
        driver.FindElement(By.CssSelector("form.form-card button[type='submit']")).ClicSeguro();
    }

    /// <summary>
    /// Pulsa "Desactivar" en la fila del producto y acepta la ventana de confirmación.
    /// OJO: en la app, "Mostrar inactivos" (prodInc=true) muestra SOLO los inactivos, no activos + inactivos;
    /// por eso un producto activo se busca SIN ese filtro.
    /// </summary>
    public void Desactivar(string nombre)
    {
        AbrirProductos(nombre);
        var fila = FilaProducto(nombre) ?? throw new NoSuchElementException($"No se encontró el producto {nombre} en el catálogo.");
        fila.FindElement(By.XPath(".//button[normalize-space()='Desactivar']")).ClicSeguro();
        var aceptar = espera.Until(d =>
        {
            var b = d.FindElement(By.Id("modalConfirmarAceptar"));
            return b.Displayed && b.Enabled ? b : null;
        });
        aceptar.ClicSeguro();
        espera.Until(d => d.FindElements(By.CssSelector(".alert-recyplast")).Any(a => a.Text.Contains("desactivado")));
    }

    /// <summary>
    /// Limpieza de seguridad: si el producto de prueba quedó activo (por ejemplo porque la prueba falló a medias),
    /// lo desactiva para que NO aparezca en los pedidos reales. Nunca lanza excepción.
    /// </summary>
    public void DesactivarSiExiste(string nombre)
    {
        try
        {
            AbrirProductos(nombre);               // lista de ACTIVOS (ver nota en Desactivar)
            if (FilaProducto(nombre) is not null) Desactivar(nombre);
        }
        catch { /* mejor esfuerzo; Sql/limpieza_pruebas.sql borra lo que quede */ }
    }

    // ------------------------------------------------------------------ vista de pedidos

    /// <summary>Lo que la pantalla de pedidos ofrece hoy. Requiere sesión Soda o Administrador.</summary>
    public List<ArticuloPedido> ArticulosParaPedido()
    {
        driver.Navigate().GoToUrl($"{Base}/Pedidos/Catalogo");
        var json = driver.FindElement(By.TagName("body")).Text;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => new ArticuloPedido(
            e.GetProperty("clave").GetString()!, e.GetProperty("nombre").GetString()!,
            e.GetProperty("categoria").GetString()!,
            e.TryGetProperty("tamano", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
            e.GetProperty("precio").GetInt32())).ToList();
    }
}
