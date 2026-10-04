using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models;

namespace SAPS.Web.Controllers;

// Sigue siendo público: sin sesión se muestra la bienvenida; con sesión, el Dashboard.
public class HomeController(ApplicationDbContext db) : Controller
{
    private const int PedidosRecientesMax = 5;

    // El Dashboard muestra datos del negocio: solo para quienes tienen uno de los roles del sistema.
    // (Privacy y Error siguen siendo públicos.)
    [Authorize(Roles = "Administrador,RecursosHumanos,Soda,Usuario")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(new DashboardViewModel());

        // Solo lectura: conteos y los últimos pedidos. No se modifica ninguna entidad.
        var modelo = new DashboardViewModel
        {
            ProductosTotal = await db.Productos.AsNoTracking().CountAsync(ct),
            ProductosActivos = await db.Productos.AsNoTracking().CountAsync(p => p.Activo, ct),
            ProductosEspeciales = await db.Productos.AsNoTracking().CountAsync(p => p.Activo && p.EsEspecial, ct),
            BebidasTotal = await db.Bebidas.AsNoTracking().CountAsync(ct),
            BebidasActivas = await db.Bebidas.AsNoTracking().CountAsync(b => b.Activo, ct),
            CategoriasTotal = await db.Categorias.AsNoTracking().CountAsync(ct),
            CategoriasActivas = await db.Categorias.AsNoTracking().CountAsync(c => c.Activo, ct),
            TamanosTotal = await db.Tamanos.AsNoTracking().CountAsync(ct),
            TamanosActivos = await db.Tamanos.AsNoTracking().CountAsync(t => t.Activo, ct),
            PreciosActivos = await db.Precios.AsNoTracking().CountAsync(p => p.Activo, ct),
            PedidosTotal = await db.Pedidos.AsNoTracking().CountAsync(ct),
            PedidosRecientes = await db.Pedidos.AsNoTracking()
                .OrderByDescending(p => p.FechaRegistroUtc).ThenByDescending(p => p.IdPedido)
                .Take(PedidosRecientesMax)
                .Select(p => new PedidoRecienteItem
                {
                    IdPedido = p.IdPedido,
                    NombreColaborador = p.NombreColaborador,
                    CodigoColaborador = p.CodigoColaborador,
                    TipoComida = p.TipoComida,
                    Total = p.Total,
                    FechaRegistroUtc = p.FechaRegistroUtc,
                    EsPrueba = p.EsPrueba
                })
                .ToListAsync(ct)
        };
        return View(modelo);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
