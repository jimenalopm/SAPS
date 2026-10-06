using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Pedidos;

namespace SAPS.Web.Services.Pedidos;

public sealed class ServicioPedidos(ApplicationDbContext db, IColaboradores colaboradores, TimeProvider reloj)
{
    public async Task<Colaborador> BuscarColaboradorAsync(string? codigo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo)) throw new PedidoInvalidoException("Ingrese el código del colaborador.");
        if (codigo.Trim().Length > 50) throw new PedidoInvalidoException("El código es demasiado largo.");
        var colaborador = await colaboradores.BuscarAsync(CodigoColaborador.Normalizar(codigo), ct)
            ?? throw new ColaboradorNoEncontradoException();
        if (!colaborador.Activo) throw new ColaboradorInactivoException();
        return colaborador;
    }

    public async Task<List<ArticuloPedido>> CatalogoAsync(CancellationToken ct = default)
    {
        var fecha = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById("America/Costa_Rica")).DateTime);
        var productos = await db.Precios.AsNoTracking()
            .Where(p => p.Activo && p.MontoPrecio > 0 && p.Producto!.Activo && p.Producto!.Categoria!.Activo
                && p.FechaVigenciaDesde <= fecha && (p.FechaVigenciaHasta == null || p.FechaVigenciaHasta >= fecha)
                && ((!p.Producto!.RequiereTamano && p.IdTamano == null)
                    || (p.Producto!.RequiereTamano && p.IdTamano != null && p.Tamano!.Activo)))
            .Select(p => new { p.IdPrecio, p.Producto!.NombreProducto, p.Producto!.Categoria!.NombreCategoria,
                Tamano = p.Tamano == null ? null : p.Tamano.NombreTamano, p.MontoPrecio }).ToListAsync(ct);
        var bebidas = await db.Bebidas.AsNoTracking()
            .Where(b => b.Activo && b.Precio > 0 && (b.IdTamano == null || b.Tamano!.Activo))
            .Select(b => new { b.IdBebida, b.NombreBebida, Tamano = b.Tamano == null ? null : b.Tamano.NombreTamano, b.Precio }).ToListAsync(ct);
        return productos.Select(p => new ArticuloPedido($"P:{p.IdPrecio}", p.NombreProducto, p.NombreCategoria, p.Tamano, p.MontoPrecio))
            .Concat(bebidas.Select(b => new ArticuloPedido($"B:{b.IdBebida}", b.NombreBebida, "Bebidas", b.Tamano, b.Precio)))
            .OrderBy(a => a.Categoria).ThenBy(a => a.Nombre).ThenBy(a => a.Tamano).ToList();
    }

    public async Task<PedidoRegistrado> RegistrarAsync(RegistrarPedidoRequest solicitud, string usuarioId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioId)) throw new PedidoInvalidoException("Inicie sesión nuevamente.");
        if (solicitud.TokenRegistro == Guid.Empty) throw new PedidoInvalidoException("Recargue la pantalla antes de registrar.");
        if (solicitud.TipoComida is not ("Desayuno" or "Almuerzo" or "Merienda")) throw new PedidoInvalidoException("Seleccione el tipo de comida.");
        if (solicitud.Lineas is null || solicitud.Lineas.Count == 0) throw new PedidoInvalidoException("Agregue al menos un artículo al pedido.");
        if (solicitud.Lineas.Any(l => l is null || string.IsNullOrWhiteSpace(l.Clave) || l.Cantidad < 1 || l.PrecioMostrado < 1))
            throw new PedidoInvalidoException("Las cantidades y los precios deben ser enteros positivos.");
        if (solicitud.Lineas.Select(l => l.Clave).Distinct().Count() != solicitud.Lineas.Count)
            throw new PedidoInvalidoException("Un artículo aparece repetido. Ajuste su cantidad en un solo renglón.");
        if (solicitud.Observaciones?.Length > 500) throw new PedidoInvalidoException("Las observaciones admiten hasta 500 caracteres.");

        var existente = await ExistenteAsync(solicitud.TokenRegistro, usuarioId, ct);
        if (existente is not null) return existente;
        var colaborador = await BuscarColaboradorAsync(solicitud.CodigoColaborador, ct);
        // Mantiene los precios y estados consultados estables hasta guardar todo el pedido.
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var catalogo = (await CatalogoAsync(ct)).ToDictionary(a => a.Clave);
        var pedido = new Pedido
        {
            TokenRegistro = solicitud.TokenRegistro, CodigoColaborador = colaborador.Codigo,
            NombreColaborador = colaborador.Nombre, IdUsuarioRegistro = usuarioId,
            FechaRegistroUtc = reloj.GetUtcNow().UtcDateTime, TipoComida = solicitud.TipoComida,
            Observaciones = string.IsNullOrWhiteSpace(solicitud.Observaciones) ? null : solicitud.Observaciones.Trim(),
            EsPrueba = colaborador.EsDemo
        };
        foreach (var linea in solicitud.Lineas)
        {
            if (!catalogo.TryGetValue(linea.Clave, out var articulo))
                throw new PedidoInvalidoException("Un artículo o tamaño dejó de estar disponible. Actualice el catálogo y revise el pedido.");
            if (articulo.Precio != linea.PrecioMostrado)
                throw new PedidoInvalidoException("Cambió un precio del catálogo. Actualice el catálogo y revise el nuevo monto antes de registrar.");
            var subtotal = checked((long)articulo.Precio * linea.Cantidad);
            pedido.Detalles.Add(new DetallePedido
            {
                IdPrecio = linea.Clave.StartsWith("P:") ? int.Parse(linea.Clave[2..]) : null,
                IdBebida = linea.Clave.StartsWith("B:") ? int.Parse(linea.Clave[2..]) : null,
                NombreArticulo = articulo.Nombre, NombreTamano = articulo.Tamano,
                Cantidad = linea.Cantidad, PrecioUnitario = articulo.Precio, Subtotal = subtotal
            });
            pedido.Total = checked(pedido.Total + subtotal);
        }
        db.Pedidos.Add(pedido);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Dos clics o un reintento tras perder conexión deben devolver la misma orden.
            await transaccion.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ExistenteAsync(solicitud.TokenRegistro, usuarioId, ct) ?? throw new PedidoInvalidoException("No se pudo registrar el pedido. Intente nuevamente.");
        }
        return new(pedido.IdPedido, pedido.Total);
    }

    private async Task<PedidoRegistrado?> ExistenteAsync(Guid token, string usuarioId, CancellationToken ct)
    {
        var existente = await db.Pedidos.AsNoTracking().SingleOrDefaultAsync(p => p.TokenRegistro == token, ct);
        if (existente is null) return null;
        if (existente.IdUsuarioRegistro != usuarioId) throw new PedidoInvalidoException("La solicitud pertenece a otra sesión. Recargue la página.");
        return new(existente.IdPedido, existente.Total);
    }
}
