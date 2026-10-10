using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Pedidos;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "Soda,Administrador")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PedidosController(ServicioPedidos pedidos, EntornoPrueba entorno, ILogger<PedidosController> logger) : Controller
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
        });
    }

    private async Task<IActionResult> EjecutarAsync(Func<Task<IActionResult>> accion)
    {
        try { return await accion(); }
        catch (PedidoInvalidoException ex) { return BadRequest(new { mensaje = ex.Message }); }
        catch (OverflowException) { return BadRequest(new { mensaje = "El monto supera la capacidad numérica del sistema. Revise las cantidades." }); }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            logger.LogError(ex, "Fallo de base de datos al procesar un pedido.");
            return StatusCode(503, new { mensaje = "No se pudo completar la operación. Mantenga esta pantalla e intente nuevamente; un reintento no duplicará el pedido." });
        }
    }
}
