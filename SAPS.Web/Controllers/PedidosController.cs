using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Auditoria;
using SAPS.Web.Services.Pedidos;
using SAPS.Web.Services.Seguridad;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "Soda,Administrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting(LimitesPeticiones.PoliticaPedidos)]
public class PedidosController(ServicioPedidos pedidos, EntornoPrueba entorno, ILogger<PedidosController> logger,
    IServicioBitacora? bitacora = null) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["EsPrueba"] = entorno.Activo;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Colaborador(string? codigo, CancellationToken ct) =>
        await EjecutarAsync(async () => Ok(await pedidos.BuscarColaboradorAsync(codigo, ct)));

    [HttpGet]
    public async Task<IActionResult> Catalogo(CancellationToken ct) =>
        await EjecutarAsync(async () => Ok(await pedidos.CatalogoAsync(ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar([FromBody] RegistrarPedidoRequest? solicitud, CancellationToken ct)
    {
        if (solicitud is null || !ModelState.IsValid)
            return BadRequest(new { mensaje = "Revise el código, las cantidades enteras y las observaciones (máximo 500 caracteres)." });
        return await EjecutarAsync(async () =>
        {
            var resultado = await pedidos.RegistrarAsync(solicitud, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", ct);
            return Ok(new { resultado.IdPedido, Total = resultado.Total.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }, solicitud.CodigoColaborador);
    }

    // Solo el registro de pedidos deja rechazos en la bitácora; las consultas (GET) no.
    private async Task RegistrarRechazoAsync(string? codigoColaborador, string motivo)
    {
        if (bitacora is null || codigoColaborador is null) return;
        await bitacora.RegistrarAsync(new EventoBitacora(ModulosBitacora.Pedidos, TiposEventoBitacora.PedidoRechazado,
            RecursosBitacora.Pedido, false)
        {
            Descripcion = $"Pedido para {codigoColaborador.Trim()} rechazado: {motivo}",
        });
    }

    private async Task<IActionResult> EjecutarAsync(Func<Task<IActionResult>> accion, string? codigoColaborador = null)
    {
        try { return await accion(); }
        catch (PedidoInvalidoException ex)
        {
            await RegistrarRechazoAsync(codigoColaborador, ex.Message);
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (OverflowException)
        {
            const string mensaje = "El monto supera la capacidad numérica del sistema. Revise las cantidades.";
            await RegistrarRechazoAsync(codigoColaborador, mensaje);
            return BadRequest(new { mensaje });
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            logger.LogError(ex, "Fallo de base de datos al procesar un pedido.");
            await RegistrarRechazoAsync(codigoColaborador, "Fallo de base de datos al registrar el pedido");
            return StatusCode(503, new { mensaje = "No se pudo completar la operación. Mantenga esta pantalla e intente nuevamente; un reintento no duplicará el pedido." });
        }
    }
}
