using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;

namespace SAPS.Web.Services.Identity;

/// <summary>Un colaborador activo y sin cuenta vinculada todavía (candidato para la pantalla de alta).</summary>
public record ColaboradorCandidato(int IdColaborador, string Codigo, string NombreCompleto);

/// <summary>
/// HU-001: búsqueda de colaboradores para vincularlos a una cuenta nueva desde la pantalla de
/// gestión de usuarios. No se reutiliza IColaboradores (Services/Pedidos/Colaboradores.cs, HU-008)
/// porque ese busca por código exacto para resolver pedidos; esta lista es para elegir de a uno
/// entre los colaboradores activos que todavía no tienen cuenta.
/// </summary>
public static class BusquedaColaboradores
{
    public static async Task<List<ColaboradorCandidato>> BuscarDisponiblesAsync(ApplicationDbContext db, CancellationToken ct = default) =>
        await db.Colaboradores.AsNoTracking()
            .Where(c => c.EstaActivo && !db.VinculosUsuarioColaborador.Any(v => v.IdColaborador == c.IdColaborador))
            .OrderBy(c => c.NombreCompleto)
            .Select(c => new ColaboradorCandidato(c.IdColaborador, c.Codigo, c.NombreCompleto))
            .ToListAsync(ct);
}
