using System.Text.RegularExpressions;
using OpenQA.Selenium;
using SAPS.FunctionalTests.Infraestructura;
using SAPS.FunctionalTests.Paginas;
using Xunit.Abstractions;

namespace SAPS.FunctionalTests;

/// <summary>
/// HU-008 — Registrar el pedido de un colaborador (usuaria de Soda).
/// Reglas de seguridad de esta clase:
///  * Colaboradores REALES (SAPS_COLAB_ACTIVO / SAPS_COLAB_INACTIVO): solo se CONSULTAN. Nunca se registra un pedido a su nombre.
///  * La ÚNICA prueba que guarda datos es <see cref="RegistroDePedido_colaboradorDemo_guardaElPedidoConSuPrecioYTotal"/>, y usa DEMO001.
///  * El producto real "Sopa" solo se lee del catálogo.
/// </summary>
public class HU008_RegistroPedidoTests(ITestOutputHelper salida) : PruebaBase("HU008", salida)
{
    private PedidosPagina Pedidos => new(Driver, Espera);
    private CatalogoPagina Catalogo => new(Driver, Espera);

    /// <summary>Entra como Soda, abre el registro de pedidos e identifica al colaborador indicado.</summary>
    private void AbrirPedidoParaColaborador(string codigo)
    {
        IniciarSesion(Rol.Soda);
        Pedidos.Abrir();
        Pedidos.BuscarColaborador(codigo);
        Assert.True(Pedidos.ColaboradorIdentificado, $"El colaborador {codigo} debería identificarse. Mensaje: {(Pedidos.HayMensaje ? Pedidos.Mensaje : "(ninguno)")}");
    }

    // ===================================================================== Datos del colaborador

    [Fact(DisplayName = "HU-008 Datos del colaborador: un código real activo muestra nombre y foto")]
    public void DatosDelColaborador_codigoRealActivo_muestraNombreYFoto()
    {
        // Solo consulta: se usa un colaborador REAL de rrhh.tb_Colaborador (código en SAPS_COLAB_ACTIVO).
        var codigo = Configuracion.ColaboradorActivo;
        IniciarSesion(Rol.Soda);
        Pedidos.Abrir();
        Capturar("pantalla_de_pedidos");

        Pedidos.BuscarColaborador(codigo);
        Capturar("colaborador_identificado");

        // El sistema despliega su nombre y su foto (si no tiene foto cargada, la app muestra sus iniciales).
        Assert.True(Pedidos.ColaboradorIdentificado);
        Assert.False(string.IsNullOrWhiteSpace(Pedidos.NombreColaborador), "Debe mostrarse el nombre del colaborador.");
        Assert.True(Pedidos.FotoOInicialesVisibles, "Debe mostrarse la foto (o las iniciales) del colaborador.");
        // La app completa con ceros el código numérico (842 -> 0000000842): se compara sin ceros a la izquierda.
        Assert.Equal(codigo.Trim().TrimStart('0'), Pedidos.CodigoMostrado.TrimStart('0'), ignoreCase: true);
    }

    // ===================================================================== Criterio 1: inexistente / inactivo

    [Fact(DisplayName = "HU-008 Colaborador inexistente: no permite registrar el pedido")]
    public void ColaboradorInexistente_noPermiteRegistrarElPedido()
    {
        IniciarSesion(Rol.Soda);
        Pedidos.Abrir();

        Pedidos.BuscarColaborador("NOEXISTE-ZZ");
        Capturar("colaborador_inexistente");

        Assert.False(Pedidos.ColaboradorIdentificado);
        Assert.Contains("no encontrado", Pedidos.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.False(Pedidos.BotonRegistrarHabilitado, "Sin colaborador identificado no se debe poder registrar.");
    }

    [SkippableFact(DisplayName = "HU-008 Colaborador inactivo: no permite registrar el pedido")]
    public void ColaboradorInactivo_noPermiteRegistrarElPedido()
    {
        // Solo consulta. Si no se configuró un colaborador real inactivo, la prueba se omite (no se inventa ni se
        // marca a ningún colaborador real como inactivo).
        var codigo = Configuracion.ColaboradorInactivo;
        Skip.If(codigo is null, "Omitida: falta la variable SAPS_COLAB_INACTIVO (código de un colaborador real e inactivo).");

        IniciarSesion(Rol.Soda);
        Pedidos.Abrir();
        Pedidos.BuscarColaborador(codigo!);
        Capturar("colaborador_inactivo");

        Assert.False(Pedidos.ColaboradorIdentificado);
        Assert.Contains("inactivo", Pedidos.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.False(Pedidos.BotonRegistrarHabilitado);
    }

    // ===================================================================== Criterio 2: selección de productos

    [Fact(DisplayName = "HU-008 Selección de productos: tipo de comida y producto se agregan al detalle")]
    public void SeleccionDeProductos_tipoDeComidaYProducto_seAgreganAlDetalle()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);

        // La usuaria elige el tipo de comida y agrega el producto real "Sopa" (cantidad 2).
        Pedidos.ElegirTipoComida("Almuerzo");
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal, cantidad: 2);
        Capturar("detalle_con_sopa");

        var linea = Assert.Single(Pedidos.Lineas());
        Assert.StartsWith(DatosDePrueba.ProductoReal, linea.Articulo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, linea.Cantidad);
        Assert.True(Pedidos.BotonRegistrarHabilitado, "Con colaborador, tipo de comida y productos ya se puede registrar.");
        // (No se pulsa "Registrar": es un colaborador real.)
    }

    [SkippableFact(DisplayName = "HU-008 Selección de productos: una bebida con tamaño se agrega con su tamaño")]
    public void SeleccionDeProductos_bebidaConTamano_seAgregaConSuTamano()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        var bebidas = Pedidos.BebidasConTamano();
        Skip.If(bebidas.Count == 0, "Omitida: el catálogo real no tiene bebidas con tamaño activas.");

        Pedidos.ElegirTipoComida("Almuerzo");
        Pedidos.AgregarOpcion(bebidas[0]);
        Capturar("bebida_con_tamano_en_detalle");

        // El renglón muestra el nombre y el tamaño de la bebida (formato "Nombre · Tamaño").
        var linea = Assert.Single(Pedidos.Lineas());
        Assert.Contains(" · ", linea.Articulo);
        Assert.StartsWith(bebidas[0].Split(" · ")[0], linea.Articulo);
    }

    // ===================================================================== Criterio 3: precio vigente

    [Fact(DisplayName = "HU-008 Precio vigente: el detalle toma el precio vigente del catálogo")]
    public void PrecioVigente_alAgregarProducto_seTomaElPrecioDelCatalogo()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        Pedidos.ElegirTipoComida("Almuerzo");

        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal);
        Capturar("precio_en_el_detalle");

        // El precio del renglón coincide con el precio vigente que publica el catálogo de pedidos para ese producto.
        var linea = Assert.Single(Pedidos.Lineas());
        var vigente = Catalogo.ArticulosParaPedido().First(a => a.Nombre.Equals(DatosDePrueba.ProductoReal, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(vigente.Precio, MontoEntero(linea.Precio));
    }

    // ===================================================================== Criterio 4: monto total entero

    [Fact(DisplayName = "HU-008 Monto total: se calcula como número entero y se recalcula al cambiar el detalle")]
    public void MontoTotal_esEnteroYSeRecalculaAlCambiarElDetalle()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        Pedidos.ElegirTipoComida("Almuerzo");

        // Se agregan dos productos distintos: la Sopa (real) y la primera otra opción disponible del catálogo.
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal, cantidad: 2);
        var otra = Pedidos.OpcionesDeArticulo().FirstOrDefault(o => !o.StartsWith(DatosDePrueba.ProductoReal, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(otra);
        Pedidos.AgregarOpcion(otra);
        Capturar("total_con_dos_productos");

        // El total es un entero y es la suma de los subtotales (precio x cantidad), sin decimales.
        var lineas = Pedidos.Lineas();
        Assert.Equal(2, lineas.Count);
        Assert.Equal(lineas.Sum(l => MontoEntero(l.Precio) * l.Cantidad), MontoEntero(Pedidos.TotalTexto));
        Assert.DoesNotMatch(@"[.,]\d{1,2}$", Pedidos.TotalTexto);

        // Al cambiar el detalle (quitar un renglón) el total se recalcula.
        Pedidos.QuitarLinea(1);
        Capturar("total_tras_quitar_un_producto");
        var restante = Assert.Single(Pedidos.Lineas());
        Assert.Equal(MontoEntero(restante.Precio) * restante.Cantidad, MontoEntero(Pedidos.TotalTexto));
    }

    // ===================================================================== Criterio 5: rebajo de planilla

    [Fact(DisplayName = "HU-008 Cobro por rebajo de planilla: no se ofrece otra forma de pago")]
    public void CobroPorRebajoDePlanilla_noSeOfreceOtraFormaDePago()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal);
        Capturar("pantalla_sin_formas_de_pago");

        var texto = Pedidos.TextoDePantalla;
        Assert.Contains("rebajo de planilla", texto, StringComparison.OrdinalIgnoreCase);
        foreach (var otraForma in new[] { "efectivo", "tarjeta", "sinpe", "transferencia", "cheque" })
            Assert.DoesNotContain(otraForma, texto, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Driver.FindElements(By.CssSelector("input[type='radio']"))); // ningún selector de forma de pago
    }

    // ===================================================================== Criterio 6: sin restricciones

    [Fact(DisplayName = "HU-008 Sin restricciones: no hay límite de monto ni de consumo previo")]
    public void SinRestricciones_cantidadGrande_noSeLimitaElMonto()
    {
        // El colaborador real tiene consumo previo; aun así el sistema debe permitir otro pedido, sin tope.
        // (La hora no se puede forzar desde Selenium; la regla de horario no existe en el código, por eso solo se verifica el monto.)
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        Pedidos.ElegirTipoComida("Desayuno");
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal, cantidad: 500);
        Capturar("pedido_de_500_unidades");

        var linea = Assert.Single(Pedidos.Lineas());
        Assert.Equal(500, linea.Cantidad);
        Assert.Equal(MontoEntero(linea.Precio) * 500, MontoEntero(Pedidos.TotalTexto));
        Assert.True(Pedidos.BotonRegistrarHabilitado, "No debe haber límite de consumo que bloquee el registro.");
        Assert.False(Pedidos.HayMensaje);
    }

    // ===================================================================== Criterio 7: sesión cerrada

    [Fact(DisplayName = "HU-008 Sesión cerrada: un pedido sin confirmar no se guarda")]
    public void SesionCerrada_pedidoSinConfirmar_noSeGuarda()
    {
        AbrirPedidoParaColaborador(Configuracion.ColaboradorActivo);
        var pedidosAntes = TotalPedidosEnInicio();

        // Se arma un borrador (colaborador real, solo en pantalla) y se cierra la sesión SIN confirmar.
        Pedidos.Abrir();
        Pedidos.BuscarColaborador(Configuracion.ColaboradorActivo);
        Pedidos.ElegirTipoComida("Almuerzo");
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal);
        Capturar("borrador_sin_confirmar");
        CerrarSesion();
        Capturar("sesion_cerrada");

        // Al volver a entrar, el borrador no existe y el total de pedidos del sistema no cambió.
        IniciarSesion(Rol.Soda);
        Pedidos.Abrir();
        Capturar("pantalla_de_pedidos_vacia");
        Assert.Empty(Pedidos.Lineas());
        Assert.False(Pedidos.ColaboradorIdentificado);
        Assert.Equal(pedidosAntes, TotalPedidosEnInicio());
    }

    // ===================================================================== Registro completo (ESCRIBE: solo DEMO001)

    [Fact(DisplayName = "HU-008 Registro de pedido: DEMO001 con Sopa queda guardado con su precio y total")]
    public void RegistroDePedido_colaboradorDemo_guardaElPedidoConSuPrecioYTotal()
    {
        // ESCRIBE en la BD real: un pedido a nombre de DEMO001 (colaborador de prueba). Se borra con Sql/limpieza_pruebas.sql.
        IniciarSesion(Rol.Soda);
        var pedidosAntes = TotalPedidosEnInicio();

        Pedidos.Abrir();
        Pedidos.BuscarColaborador(DatosDePrueba.ColaboradorDemo);
        Assert.True(Pedidos.ColaboradorIdentificado,
            "DEMO001 debe existir y estar activo en rrhh.tb_Colaborador (ver Sql/crear_colaborador_demo.sql).");
        var nombreDemo = Pedidos.NombreColaborador;
        Capturar("demo001_identificado");

        Pedidos.ElegirTipoComida("Almuerzo");
        Pedidos.AgregarArticulo(DatosDePrueba.ProductoReal, cantidad: 2);
        var linea = Assert.Single(Pedidos.Lineas());
        var totalEsperado = MontoEntero(linea.Precio) * 2;
        Capturar("detalle_antes_de_registrar");

        // La usuaria confirma el registro: la app muestra el número de pedido y el total (entero).
        var (numero, total) = Pedidos.RegistrarYConfirmar();
        Capturar("pedido_registrado");
        Assert.Matches(@"^#\d+$", numero);
        Assert.Equal(totalEsperado, MontoEntero(total));

        // Trazabilidad: el pedido queda cargado al colaborador (aparece en la actividad reciente, marcado como prueba).
        Pedidos.CerrarMensajeExito();
        Assert.Equal(pedidosAntes + 1, TotalPedidosEnInicio());
        var pedido = Espera.Until(d => d.FindElements(By.CssSelector(".lista-actividad li"))
            .FirstOrDefault(li => li.Text.Contains("Pedido " + numero)));
        Assert.NotNull(pedido);
        Capturar("pedido_en_actividad_reciente");
        Assert.Contains(nombreDemo, pedido.Text);
        Assert.Contains(DatosDePrueba.ColaboradorDemo, pedido.Text);
        Assert.Contains("Prueba", pedido.Text);
        Assert.Equal(totalEsperado, MontoEntero(pedido.FindElement(By.CssSelector(".lista-actividad-monto")).Text));
    }

    // ===================================================================== Criterio 8 (parte): trazabilidad por usuaria

    [Fact(Skip = "No verificable desde la interfaz: ninguna pantalla muestra qué usuaria registró el pedido. La asociación " +
                 "pedido-usuaria (columna idUsuario) se comprueba con la consulta de solo lectura Sql/verificar_pedido_demo.sql.")]
    public void Trazabilidad_pedidoQuedaAsociadoALaUsuariaQueLoRegistro() { }

    // El criterio 9 (el colaborador no inicia sesión) se prueba en HU001_InicioSesionTests.ColaboradoresQueCompran_...
}
