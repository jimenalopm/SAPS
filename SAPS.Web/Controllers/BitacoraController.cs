using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Web.Controllers;

/// <summary>
/// HU-004 / HU-015: consulta de la bitácora (soda.tb_Bitacora).
/// Criterio 5: solo el rol Administrador. Es un controlador propio (no una acción de AdministracionController,
/// que también admite RecursosHumanos). Cualquier otro rol que escriba /Bitacora en la barra de direcciones
/// recibe la página de acceso no autorizado de Identity.
/// Criterio 6: solo lectura. Únicamente acciones GET, consultas AsNoTracking y nunca SaveChanges; además la
/// tabla rechaza UPDATE y DELETE en la base de datos (tr_BitacoraSoloLectura).
/// </summary>
[Authorize(Roles = "Administrador")]
public class BitacoraController(ApplicationDbContext db) : Controller
{
    /// <summary>Filas por página. La tabla crece con cada inicio de sesión, así que nunca se carga completa.</summary>
    public const int TamanoPagina = 50;

    /// <summary>Listado con filtros (task 12). Los filtros llegan por la URL: /Bitacora?tipoEvento=CambioPrecio&amp;pagina=2</summary>
    [HttpGet]
    public async Task<IActionResult> Index(BitacoraFiltros filtros, int pagina = 1, CancellationToken ct = default)
    {
        filtros.Normalizar();
        var consulta = filtros.Aplicar(db.Bitacoras.AsNoTracking());

        var total = await consulta.CountAsync(ct);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanoPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        // Lo más reciente primero (IX_Bitacora_FechaHoraUtc). Skip/Take se traduce a OFFSET/FETCH,
        // que SQL Server 2012 sí soporta. El id desempata eventos del mismo instante.
        var filas = await consulta
            .OrderByDescending(b => b.FechaHoraUtc)
            .ThenByDescending(b => b.IdBitacora)
            .Skip((pagina - 1) * TamanoPagina)
            .Take(TamanoPagina)
            .ToListAsync(ct);

        var (tiposPorModulo, modulos) = await CargarOpcionesAsync(filtros, ct);

        var modelo = new BitacoraListadoViewModel
        {
            Filas = filas.Select(BitacoraFilaViewModel.DesdeEntidad).ToList(),
            Filtros = filtros,
            TiposPorModulo = tiposPorModulo,
            Modulos = modulos,
            Pagina = pagina,
            TotalPaginas = totalPaginas,
            TotalRegistros = total,
            TamanoPagina = TamanoPagina,
        };
        return View(modelo);
    }

    /// <summary>Detalle de un evento (task 12, HU-015 criterio 6). Recibe los filtros solo para el enlace de regreso.</summary>
    [HttpGet]
    public async Task<IActionResult> Detalle(int id, BitacoraFiltros filtros, int pagina = 1, CancellationToken ct = default)
    {
        var registro = await db.Bitacoras.AsNoTracking().FirstOrDefaultAsync(b => b.IdBitacora == id, ct);
        if (registro is null) return NotFound();

        filtros.Normalizar();
        return View(BitacoraDetalleViewModel.DesdeEntidad(registro, filtros, Math.Max(1, pagina)));
    }

    /// <summary>
    /// Opciones de los filtros: los eventos conocidos más los que haya en la tabla (SELECT DISTINCT Modulo, TipoEvento).
    /// Un evento nuevo, como la anulación de órdenes de HU-010, aparece en el filtro sin cambiar código (HU-004 criterio 8).
    /// </summary>
    private async Task<(List<GrupoOpcionesFiltro> TiposPorModulo, List<OpcionFiltro> Modulos)> CargarOpcionesAsync(
        BitacoraFiltros filtros, CancellationToken ct)
    {
        var enTabla = await db.Bitacoras.AsNoTracking()
            .Select(b => new { b.Modulo, b.TipoEvento })
            .Distinct()
            .ToListAsync(ct);

        // Los eventos conocidos siempre aparecen (aunque aún no haya filas de ese tipo), más cualquier
        // evento nuevo que ya esté en la tabla.
        var pares = PresentacionBitacora.EventosConocidos
            .Select(e => (e.Modulo, e.TipoEvento))
            .Concat(enTabla.Select(p => (p.Modulo, p.TipoEvento)))
            .Distinct()
            .ToList();

        var nombresModulo = pares.Select(p => p.Modulo)
            .Distinct()
            .OrderBy(m => PresentacionBitacora.EtiquetaModulo(m))
            .ToList();
        if (filtros.Modulo is string moduloElegido && !nombresModulo.Contains(moduloElegido))
            nombresModulo.Add(moduloElegido);

        var modulos = nombresModulo
            .Select(m => new OpcionFiltro(m, PresentacionBitacora.EtiquetaModulo(m)))
            .ToList();

        var tiposPorModulo = pares
            .GroupBy(p => p.Modulo)
            .OrderBy(g => PresentacionBitacora.EtiquetaModulo(g.Key))
            .Select(g => new GrupoOpcionesFiltro(
                PresentacionBitacora.EtiquetaModulo(g.Key),
                g.Select(p => p.TipoEvento).Distinct()
                 .Select(t => new OpcionFiltro(t, PresentacionBitacora.EtiquetaEvento(t)))
                 .OrderBy(o => o.Texto)
                 .ToList()))
            .ToList();

        // Si la URL trae un tipo desconocido, se conserva como opción para que el filtro se vea aplicado.
        if (filtros.TipoEvento is string tipoElegido && !pares.Any(p => p.TipoEvento == tipoElegido))
            tiposPorModulo.Add(new GrupoOpcionesFiltro("Otros",
                [new OpcionFiltro(tipoElegido, PresentacionBitacora.EtiquetaEvento(tipoElegido))]));

        return (tiposPorModulo, modulos);
    }
}
