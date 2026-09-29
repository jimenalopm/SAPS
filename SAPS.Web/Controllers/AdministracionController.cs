// Controllers/AdministracionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "Administrador,RecursosHumanos")]
public class AdministracionController(ApplicationDbContext db) : Controller
{
    public IActionResult Index() => View();

    // ==================================================================
    //  Pantalla principal del catálogo (con pestañas)
    // ==================================================================
    public async Task<IActionResult> Catalogo(
        string tab = "categorias",
        string? catQ = null, bool catInc = false,
        string? tamQ = null, bool tamInc = false,
        string? prodQ = null, bool prodInc = false,
        string? bebQ = null, bool bebInc = false)
    {
        var vm = new CatalogoIndexViewModel
        {
            TabActiva = tab,
            CategoriaBuscar = catQ,
            CategoriaIncluirInactivos = catInc,
            TamanoBuscar = tamQ,
            TamanoIncluirInactivos = tamInc,
            ProductoBuscar = prodQ,
            ProductoIncluirInactivos = prodInc,
            BebidaBuscar = bebQ,
            BebidaIncluirInactivos = bebInc,
        };

        var categorias = db.Categorias.AsQueryable();
        if (!catInc) categorias = categorias.Where(c => c.Activo);
        if (!string.IsNullOrWhiteSpace(catQ)) categorias = categorias.Where(c => c.NombreCategoria.Contains(catQ));
        vm.Categorias = await categorias.OrderBy(c => c.NombreCategoria).ToListAsync();

        var tamanos = db.Tamanos.AsQueryable();
        if (!tamInc) tamanos = tamanos.Where(t => t.Activo);
        if (!string.IsNullOrWhiteSpace(tamQ)) tamanos = tamanos.Where(t => t.NombreTamano.Contains(tamQ));
        vm.Tamanos = await tamanos.OrderBy(t => t.NombreTamano).ToListAsync();

        var productos = db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Precios.Where(pr => pr.Activo))
                .ThenInclude(pr => pr.Tamano)
            .AsQueryable();
        if (!prodInc) productos = productos.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(prodQ)) productos = productos.Where(p => p.NombreProducto.Contains(prodQ));
        vm.Productos = await productos.OrderBy(p => p.NombreProducto).ToListAsync();

        var bebidas = db.Bebidas.Include(b => b.Tamano).AsQueryable();
        if (!bebInc) bebidas = bebidas.Where(b => b.Activo);
        if (!string.IsNullOrWhiteSpace(bebQ)) bebidas = bebidas.Where(b => b.NombreBebida.Contains(bebQ));
        vm.Bebidas = await bebidas.OrderBy(b => b.NombreBebida).ToListAsync();

        return View(vm);
    }

    // ==================================================================
    //  CATEGORÍAS
    // ==================================================================
    public IActionResult CrearCategoria() => View(new CategoriaFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCategoria(CategoriaFormViewModel modelo)
    {
        if (await db.Categorias.AnyAsync(c => c.NombreCategoria == modelo.NombreCategoria))
        {
            ModelState.AddModelError(nameof(modelo.NombreCategoria), "Ya existe una categoría con ese nombre.");
        }
        if (!ModelState.IsValid) return View(modelo);

        db.Categorias.Add(new Categoria { NombreCategoria = modelo.NombreCategoria });
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Categoría \"{modelo.NombreCategoria}\" creada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
    }

    public async Task<IActionResult> EditarCategoria(int id)
    {
        var categoria = await db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();
        return View(new CategoriaFormViewModel { IdCategoria = categoria.IdCategoria, NombreCategoria = categoria.NombreCategoria });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCategoria(CategoriaFormViewModel modelo)
    {
        if (await db.Categorias.AnyAsync(c => c.NombreCategoria == modelo.NombreCategoria && c.IdCategoria != modelo.IdCategoria))
        {
            ModelState.AddModelError(nameof(modelo.NombreCategoria), "Ya existe otra categoría con ese nombre.");
        }
        if (!ModelState.IsValid) return View(modelo);

        var categoria = await db.Categorias.FindAsync(modelo.IdCategoria);
        if (categoria == null) return NotFound();

        categoria.NombreCategoria = modelo.NombreCategoria;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = "Categoría actualizada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoCategoria(int id)
    {
        var categoria = await db.Categorias.FindAsync(id);
        if (categoria == null) return NotFound();
        categoria.Activo = !categoria.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = categoria.Activo ? "Categoría activada." : "Categoría desactivada.";
        return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
    }

    // ==================================================================
    //  TAMAÑOS
    // ==================================================================
    public IActionResult CrearTamano() => View(new TamanoFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearTamano(TamanoFormViewModel modelo)
    {
        if (await db.Tamanos.AnyAsync(t => t.NombreTamano == modelo.NombreTamano))
        {
            ModelState.AddModelError(nameof(modelo.NombreTamano), "Ya existe un tamaño con ese nombre.");
        }
        if (!ModelState.IsValid) return View(modelo);

        db.Tamanos.Add(new Tamano { NombreTamano = modelo.NombreTamano });
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Tamaño \"{modelo.NombreTamano}\" creado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "tamanos" });
    }

    public async Task<IActionResult> EditarTamano(int id)
    {
        var tamano = await db.Tamanos.FindAsync(id);
        if (tamano == null) return NotFound();
        return View(new TamanoFormViewModel { IdTamano = tamano.IdTamano, NombreTamano = tamano.NombreTamano });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarTamano(TamanoFormViewModel modelo)
    {
        if (await db.Tamanos.AnyAsync(t => t.NombreTamano == modelo.NombreTamano && t.IdTamano != modelo.IdTamano))
        {
            ModelState.AddModelError(nameof(modelo.NombreTamano), "Ya existe otro tamaño con ese nombre.");
        }
        if (!ModelState.IsValid) return View(modelo);

        var tamano = await db.Tamanos.FindAsync(modelo.IdTamano);
        if (tamano == null) return NotFound();

        tamano.NombreTamano = modelo.NombreTamano;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = "Tamaño actualizado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "tamanos" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoTamano(int id)
    {
        var tamano = await db.Tamanos.FindAsync(id);
        if (tamano == null) return NotFound();
        tamano.Activo = !tamano.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = tamano.Activo ? "Tamaño activado." : "Tamaño desactivado.";
        return RedirectToAction(nameof(Catalogo), new { tab = "tamanos" });
    }

    // ==================================================================
    //  PRODUCTOS  (con precios por tamaño, cargados en una sola operación)
    // ==================================================================
    public async Task<IActionResult> CrearProducto()
    {
        var modelo = new ProductoFormViewModel();
        await RecargarListasProducto(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearProducto(ProductoFormViewModel modelo)
    {
        await RecargarListasProducto(modelo);
        ValidarPreciosProducto(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var producto = new Producto
        {
            NombreProducto = modelo.NombreProducto,
            IdCategoria = modelo.IdCategoria,
            EsEspecial = modelo.EsEspecial,
            RequiereTamano = modelo.RequiereTamano,
        };
        foreach (var precio in PreciosSeleccionados(modelo))
        {
            producto.Precios.Add(new Precio { IdTamano = precio.IdTamano, MontoPrecio = precio.Monto });
        }
        // EF guarda el producto y sus precios juntos, en una sola transacción.
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Producto \"{producto.NombreProducto}\" creado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "productos" });
    }

    public async Task<IActionResult> EditarProducto(int id)
    {
        var producto = await db.Productos
            .Include(p => p.Precios.Where(pr => pr.Activo))
            .FirstOrDefaultAsync(p => p.IdProducto == id);
        if (producto == null) return NotFound();

        var modelo = new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto,
            NombreProducto = producto.NombreProducto,
            IdCategoria = producto.IdCategoria,
            EsEspecial = producto.EsEspecial,
            RequiereTamano = producto.RequiereTamano,
            PrecioUnico = producto.Precios.FirstOrDefault(p => p.IdTamano == null)?.MontoPrecio,
        };
        // También carga tamaños si actualmente usa precio único.
        await RecargarListasProducto(modelo, producto, cargarPreciosActuales: true);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarProducto(ProductoFormViewModel modelo)
    {
        var producto = await db.Productos
            .Include(p => p.Precios.Where(pr => pr.Activo))
            .FirstOrDefaultAsync(p => p.IdProducto == modelo.IdProducto);
        if (producto == null) return NotFound();

        await RecargarListasProducto(modelo, producto);
        ValidarPreciosProducto(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var seleccionados = PreciosSeleccionados(modelo);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var nuevos = seleccionados.Where(n => !producto.Precios.Any(p =>
            p.Activo && p.IdTamano == n.IdTamano && p.MontoPrecio == n.Monto)).ToList();

        // Cierra tanto precios reemplazados como los del modo que se deja de usar.
        foreach (var precio in producto.Precios.Where(p => p.Activo))
        {
            if (!seleccionados.Any(n => n.IdTamano == precio.IdTamano && n.Monto == precio.MontoPrecio))
            {
                precio.Activo = false;
                precio.FechaVigenciaHasta = hoy;
            }
        }
        producto.NombreProducto = modelo.NombreProducto;
        producto.IdCategoria = modelo.IdCategoria;
        producto.EsEspecial = modelo.EsEspecial;
        producto.RequiereTamano = modelo.RequiereTamano;

        // Primero libera el índice de precio activo por producto/tamaño.
        // Si falla cualquiera de los dos guardados, se revierte toda la edición.
        await using var transaccion = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        foreach (var precio in nuevos)
        {
            db.Precios.Add(new Precio
            {
                IdProducto = producto.IdProducto,
                IdTamano = precio.IdTamano,
                MontoPrecio = precio.Monto,
                FechaVigenciaDesde = hoy,
            });
        }
        await db.SaveChangesAsync();
        await transaccion.CommitAsync();
        TempData["Mensaje"] = "Producto actualizado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "productos" });
    }

    private static List<(int? IdTamano, int Monto)> PreciosSeleccionados(ProductoFormViewModel modelo)
    {
        return modelo.RequiereTamano
            ? modelo.PreciosPorTamano.Where(p => p.Incluir)
                .Select(p => ((int?)p.IdTamano, p.Monto!.Value)).ToList()
            : new List<(int?, int)> { (null, modelo.PrecioUnico!.Value) };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoProducto(int id)
    {
        var producto = await db.Productos.FindAsync(id);
        if (producto == null) return NotFound();
        producto.Activo = !producto.Activo;
        // Nota: no se tocan los precios. Se desactiva solo el producto; sus precios
        // quedan tal cual estaban (según se acordó para este sprint).
        await db.SaveChangesAsync();
        TempData["Mensaje"] = producto.Activo ? "Producto activado." : "Producto desactivado.";
        return RedirectToAction(nameof(Catalogo), new { tab = "productos" });
    }

    private async Task RecargarListasProducto(ProductoFormViewModel modelo, Producto? producto = null,
        bool cargarPreciosActuales = false)
    {
        var categoriaActual = producto?.IdCategoria;
        modelo.CategoriasDisponibles = await db.Categorias
            .Where(c => c.Activo || c.IdCategoria == categoriaActual)
            .OrderBy(c => c.NombreCategoria).ToListAsync();
        if (!modelo.CategoriasDisponibles.Any(c => c.IdCategoria == modelo.IdCategoria) && HttpContext.Request.Method == "POST")
            ModelState.AddModelError(nameof(modelo.IdCategoria), "Seleccione una categoría activa o conserve la categoría actual.");

        var tamanosAsignados = producto?.Precios.Where(p => p.Activo && p.IdTamano.HasValue)
            .Select(p => p.IdTamano!.Value).ToList() ?? new List<int>();
        var tamanos = await db.Tamanos
            .Where(t => t.Activo || tamanosAsignados.Contains(t.IdTamano))
            .OrderBy(t => t.NombreTamano).ToListAsync();

        // Conserva los índices de los campos enviados cuando hay errores de validación.
        foreach (var entrada in modelo.PreciosPorTamano)
        {
            var tamano = tamanos.FirstOrDefault(t => t.IdTamano == entrada.IdTamano);
            entrada.NombreTamano = tamano?.NombreTamano ?? "Tamaño no disponible";
            entrada.TamanoActivo = tamano?.Activo ?? false;
            if (modelo.RequiereTamano && entrada.Incluir && tamano == null)
                ModelState.AddModelError(string.Empty, "Uno de los tamaños seleccionados ya no está disponible.");
        }
        foreach (var tamano in tamanos)
        {
            if (modelo.PreciosPorTamano.Any(p => p.IdTamano == tamano.IdTamano)) continue;
            var precioActual = cargarPreciosActuales
                ? producto?.Precios.FirstOrDefault(p => p.Activo && p.IdTamano == tamano.IdTamano)
                : null;
            modelo.PreciosPorTamano.Add(new PrecioPorTamanoInput
            {
                IdTamano = tamano.IdTamano,
                NombreTamano = tamano.NombreTamano,
                TamanoActivo = tamano.Activo,
                Incluir = precioActual != null,
                Monto = precioActual?.MontoPrecio,
            });
        }
    }

    private void ValidarPreciosProducto(ProductoFormViewModel modelo)
    {
        if (modelo.RequiereTamano)
        {
            var seleccionados = modelo.PreciosPorTamano.Where(p => p.Incluir).ToList();
            if (seleccionados.Count == 0)
                ModelState.AddModelError(string.Empty, "Debe indicar el precio de al menos un tamaño.");
            if (seleccionados.Any(p => p.Monto is null or <= 0))
                ModelState.AddModelError(string.Empty, "Cada tamaño seleccionado debe tener un precio entero mayor a cero.");
            if (seleccionados.GroupBy(p => p.IdTamano).Any(g => g.Count() > 1))
                ModelState.AddModelError(string.Empty, "No se puede repetir un tamaño en el producto.");
        }
        else if (modelo.PrecioUnico is null or <= 0)
        {
            ModelState.AddModelError(nameof(modelo.PrecioUnico), "Debe indicar un precio mayor a cero.");
        }
    }

    // ==================================================================
    //  BEBIDAS
    // ==================================================================
    public async Task<IActionResult> CrearBebida()
    {
        var vm = new BebidaFormViewModel
        {
            TamanosDisponibles = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearBebida(BebidaFormViewModel modelo)
    {
        modelo.TamanosDisponibles = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano).ToListAsync();
        ValidarTamanoBebida(modelo);
        if (!ModelState.IsValid) return View(modelo);

        db.Bebidas.Add(new Bebida
        {
            NombreBebida = modelo.NombreBebida,
            TipoBebida = modelo.TipoBebida,
            IdTamano = modelo.IdTamano,
            Precio = modelo.Precio,
        });
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Bebida \"{modelo.NombreBebida}\" creada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
    }

    public async Task<IActionResult> EditarBebida(int id)
    {
        var bebida = await db.Bebidas.FindAsync(id);
        if (bebida == null) return NotFound();
        return View(new BebidaFormViewModel
        {
            IdBebida = bebida.IdBebida,
            NombreBebida = bebida.NombreBebida,
            TipoBebida = bebida.TipoBebida,
            IdTamano = bebida.IdTamano,
            Precio = bebida.Precio,
            TamanosDisponibles = await db.Tamanos.Where(t => t.Activo || t.IdTamano == bebida.IdTamano)
                .OrderBy(t => t.NombreTamano).ToListAsync(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarBebida(BebidaFormViewModel modelo)
    {
        var bebida = await db.Bebidas.FindAsync(modelo.IdBebida);
        if (bebida == null) return NotFound();
        modelo.TamanosDisponibles = await db.Tamanos.Where(t => t.Activo || t.IdTamano == bebida.IdTamano)
            .OrderBy(t => t.NombreTamano).ToListAsync();
        ValidarTamanoBebida(modelo);
        if (!ModelState.IsValid) return View(modelo);

        bebida.NombreBebida = modelo.NombreBebida;
        bebida.TipoBebida = modelo.TipoBebida;
        bebida.IdTamano = modelo.IdTamano;
        bebida.Precio = modelo.Precio;

        await db.SaveChangesAsync();
        TempData["Mensaje"] = "Bebida actualizada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
    }

    private void ValidarTamanoBebida(BebidaFormViewModel modelo)
    {
        if (modelo.IdTamano.HasValue && !modelo.TamanosDisponibles.Any(t => t.IdTamano == modelo.IdTamano))
            ModelState.AddModelError(nameof(modelo.IdTamano), "Seleccione un tamaño activo o conserve el tamaño actual.");
        if (!new[] { "Gaseosa", "Embotellada", "Energizante", "Jugo" }.Contains(modelo.TipoBebida))
            ModelState.AddModelError(nameof(modelo.TipoBebida), "Seleccione un tipo de bebida válido.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiarEstadoBebida(int id)
    {
        var bebida = await db.Bebidas.FindAsync(id);
        if (bebida == null) return NotFound();
        bebida.Activo = !bebida.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = bebida.Activo ? "Bebida activada." : "Bebida desactivada.";
        return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
    }
}
