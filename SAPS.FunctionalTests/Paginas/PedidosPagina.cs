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

    // ------------------------------------------------------------------ comida / bebida y tipo
    // HU-008 (versión actual): la pantalla primero pregunta "Comida" o "Bebida". Luego el selector "tipo-comida" ofrece
    // las categorías del catálogo (Comida) o los tipos de bebida (Bebida), y el selector "articulo" solo muestra los
    // artículos de ese tipo. El tipo elegido es el "tipo de comida" con el que se registra el pedido.

    /// <summary>Elige "Comida" o "Bebida". El radio está oculto (estilo botón de Bootstrap): se pulsa su etiqueta.</summary>
    public void ElegirGrupo(string grupo)
    {
        var id = grupo.Equals("Bebida", StringComparison.OrdinalIgnoreCase) ? "grupo-bebida" : "grupo-comida";
        if (driver.FindElement(By.Id(id)).Selected) return;
        driver.FindElement(By.CssSelector($"label[for='{id}']")).ClicSeguro();
        espera.Until(d => d.FindElement(By.Id(id)).Selected);
    }

    /// <summary>Opciones del selector de tipo (sin la opción vacía "Seleccione…").</summary>
    public List<string> TiposDisponibles() =>
        new SelectElement(driver.FindElement(By.Id("tipo-comida"))).Options
            .Where(o => !string.IsNullOrEmpty(o.GetDomAttribute("value"))).Select(o => o.Text.Trim()).ToList();

    /// <summary>
    /// Elige el tipo de comida (grupo "Comida"). Las categorías vienen del catálogo real: si <paramref name="tipo"/> no
    /// existe en esta base, no se elige nada y <see cref="AgregarArticulo"/> elegirá la categoría del artículo.
    /// </summary>
    public void ElegirTipoComida(string tipo)
    {
        ElegirGrupo("Comida");
        if (TiposDisponibles().Contains(tipo, StringComparer.OrdinalIgnoreCase)) ElegirTipo(tipo);
    }

    private void ElegirTipo(string tipo)
    {
        var select = new SelectElement(driver.FindElement(By.Id("tipo-comida")));
        var opcion = select.Options.First(o => o.Text.Trim().Equals(tipo, StringComparison.OrdinalIgnoreCase));
        select.SelectByValue(opcion.GetDomAttribute("value")!);
    }

    /// <summary>Opciones del selector de artículos del tipo elegido ("Sopa — ₡1.500", "Fresco · Grande — ₡2.000"...).</summary>
    public List<string> OpcionesDeArticulo() =>
        new SelectElement(driver.FindElement(By.Id("articulo"))).Options.Select(o => o.Text.Trim()).Where(t => t.Contains('—')).ToList();

    /// <summary>
    /// Deja elegido el tipo (del grupo actual) cuyo selector de artículos tiene una opción que cumple
    /// <paramref name="condicion"/>. Primero mira el tipo ya elegido; si ahí no está, recorre los demás.
    /// Devuelve el texto de la opción encontrada.
    /// </summary>
    private string BuscarOpcion(Func<string, bool> condicion, string descripcion)
    {
        var enTipoActual = OpcionesDeArticulo().FirstOrDefault(condicion);
        if (enTipoActual is not null) return enTipoActual;
        foreach (var tipo in TiposDisponibles())
        {
            ElegirTipo(tipo);
            var encontrada = OpcionesDeArticulo().FirstOrDefault(condicion);
            if (encontrada is not null) return encontrada;
        }
        throw new NoSuchElementException($"{descripcion} no está en el catálogo de pedidos.");
    }

    /// <summary>
    /// Agrega el artículo cuyo nombre es EXACTAMENTE <paramref name="nombre"/> (la opción se ve "Nombre — ₡precio"
    /// o "Nombre · Tamaño — ₡precio"). Si no está en el tipo elegido, elige el tipo (categoría) donde sí está.
    /// </summary>
    public void AgregarArticulo(string nombre, int cantidad = 1)
    {
        var texto = BuscarOpcion(t =>
            t.StartsWith(nombre + " —", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith(nombre + " ·", StringComparison.OrdinalIgnoreCase), $"El artículo «{nombre}»");
        AgregarOpcion(texto, cantidad);
    }

    /// <summary>
    /// Agrega la opción cuyo texto completo es el indicado (p. ej. "Coca-Cola · 600ml — ₡1.200"), buscándola en los
    /// tipos del grupo actual si hace falta.
    /// </summary>
    public void AgregarOpcion(string textoOpcion, int cantidad = 1)
    {
        BuscarOpcion(t => t == textoOpcion, $"La opción «{textoOpcion}»");
        var antes = driver.FindElements(By.CssSelector("#lineas-pedido tr")).Count;
        new SelectElement(driver.FindElement(By.Id("articulo"))).SelectByText(textoOpcion);
        var campoCantidad = driver.FindElement(By.Id("cantidad"));
        campoCantidad.Clear();
        campoCantidad.SendKeys(cantidad.ToString());
        driver.FindElement(By.Id("agregar")).ClicSeguro();
        // Un artículo nuevo agrega un renglón; uno repetido solo suma cantidad. Se espera a que la tabla tenga algo.
        espera.Until(d => d.FindElements(By.CssSelector("#lineas-pedido tr")).Count >= Math.Max(antes, 1));
    }

    /// <summary>
    /// Primera opción del catálogo (grupo actual) que NO es el artículo indicado. Busca primero en el tipo elegido y,
    /// si ahí no hay otra, en los demás tipos (dejando elegido el tipo donde la encontró).
    /// </summary>
    public string? PrimeraOpcionDistintaDe(string nombre)
    {
        try { return BuscarOpcion(t => !t.StartsWith(nombre, StringComparison.OrdinalIgnoreCase), "Otra opción"); }
        catch (NoSuchElementException) { return null; }
    }

    /// <summary>
    /// Bebidas que tienen tamaño (el texto trae " · Tamaño"), de todos los tipos de bebida. Deja la pantalla en el
    /// grupo "Bebida".
    /// </summary>
    public List<string> BebidasConTamano()
    {
        ElegirGrupo("Bebida");
        var bebidas = new List<string>();
        foreach (var tipo in TiposDisponibles())
        {
            ElegirTipo(tipo);
            bebidas.AddRange(OpcionesDeArticulo().Where(t => t.Contains(" · ")));
        }
        return bebidas;
    }

    /// <summary>¿Se ve el aviso de total (monto alto o por encima del máximo permitido)?</summary>
    public bool HayAvisoDeTotal => driver.FindElement(By.Id("aviso-total")).Displayed;
    public string AvisoDeTotal => driver.FindElement(By.Id("aviso-total")).Text.Trim();

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
