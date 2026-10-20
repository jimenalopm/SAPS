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

    // El inicio (dashboard) muestra datos del negocio: solo para quienes tienen uno de los roles del sistema.
    // (Privacy y Error siguen siendo públicos.)
    [Authorize(Roles = "Administrador,RecursosHumanos,Soda")]
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
        await CargarPanelesAsync(modelo, ct);
        return View(modelo);
    }

    /// <summary>
    /// Datos de los paneles del inicio: lo que hay activo en el catálogo, en forma resumida, y los pedidos de hoy.
    /// Solo lectura. El catálogo de la soda es pequeño, así que se carga completo con la página.
    /// </summary>
    private async Task CargarPanelesAsync(DashboardViewModel modelo, CancellationToken ct)
    {
        var precios = await db.Precios.AsNoTracking()
            .Where(p => p.Activo && p.Producto!.Activo)
            .Select(p => new
            {
                p.IdProducto, Producto = p.Producto!.NombreProducto,
                Tamano = p.Tamano == null ? null : p.Tamano.NombreTamano, p.IdTamano, p.MontoPrecio,
            })
            .ToListAsync(ct);
        modelo.ListaPrecios = precios
            .Select(p => new ResumenPrecio(p.Producto, p.Tamano, p.MontoPrecio))
            .OrderBy(p => p.Producto).ThenBy(p => p.Monto).ToList();

        var productos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.IdProducto, p.NombreProducto, Categoria = p.Categoria!.NombreCategoria, p.EsEspecial })
            .OrderBy(p => p.NombreProducto)
            .ToListAsync(ct);
        modelo.ListaProductos = productos
            .Select(p => new ResumenProducto(p.NombreProducto, p.Categoria, p.EsEspecial,
                precios.Where(x => x.IdProducto == p.IdProducto).OrderBy(x => x.MontoPrecio)
                       .Select(x => new ResumenPrecio(x.Producto, x.Tamano, x.MontoPrecio)).ToList()))
            .ToList();

        modelo.ListaBebidas = (await db.Bebidas.AsNoTracking()
                .Where(b => b.Activo)
                .Select(b => new { b.NombreBebida, b.TipoBebida, Tamano = b.Tamano == null ? null : b.Tamano.NombreTamano, b.Precio })
                .ToListAsync(ct))
            .Select(b => new ResumenBebida(b.NombreBebida, b.TipoBebida, b.Tamano, b.Precio))
            .OrderBy(b => b.Tipo).ThenBy(b => b.Nombre).ToList();

        modelo.ListaCategorias = (await db.Categorias.AsNoTracking()
                .Where(c => c.Activo)
                .Select(c => new { c.NombreCategoria, Productos = db.Productos.Count(p => p.IdCategoria == c.IdCategoria && p.Activo) })
                .ToListAsync(ct))
            .Select(c => new ResumenCategoria(c.NombreCategoria, c.Productos))
            .OrderBy(c => c.Nombre).ToList();

        var bebidasPorTamano = await db.Bebidas.AsNoTracking()
            .Where(b => b.Activo && b.IdTamano != null)
            .GroupBy(b => b.IdTamano!.Value).Select(g => new { Id = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cantidad, ct);
        modelo.ListaTamanos = (await db.Tamanos.AsNoTracking()
                .Where(t => t.Activo)
                .Select(t => new { t.IdTamano, t.NombreTamano, t.EsParaBebida })
                .ToListAsync(ct))
            .Select(t => new ResumenTamano(t.NombreTamano, t.EsParaBebida,
                precios.Count(p => p.IdTamano == t.IdTamano) + bebidasPorTamano.GetValueOrDefault(t.IdTamano)))
            .OrderBy(t => t.EsParaBebida).ThenBy(t => t.Nombre).ToList();

        // "Hoy" en hora de Costa Rica (UTC-6, sin horario de verano), convertido a UTC para comparar con la BD.
        var ahoraCr = DateTime.UtcNow.AddHours(-6);
        var inicioHoyUtc = ahoraCr.Date.AddHours(6);
        modelo.PedidosHoy = await db.Pedidos.AsNoTracking()
            .Where(p => p.FechaRegistroUtc >= inicioHoyUtc)
            .OrderByDescending(p => p.FechaRegistroUtc).ThenByDescending(p => p.IdPedido)
            .Select(p => new PedidoRecienteItem
            {
                IdPedido = p.IdPedido,
                NombreColaborador = p.NombreColaborador,
                CodigoColaborador = p.CodigoColaborador,
                TipoComida = p.TipoComida,
                Total = p.Total,
                FechaRegistroUtc = p.FechaRegistroUtc,
                EsPrueba = p.EsPrueba,
            })
            .ToListAsync(ct);
        modelo.TotalHoy = modelo.PedidosHoy.Sum(p => p.Total);
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
