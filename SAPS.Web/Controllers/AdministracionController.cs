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
        string? prodQ = null, bool prodInc = false, int? prodCat = null,
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
            ProductoCategoria = prodCat,
            ProductoIncluirInactivos = prodInc,
            BebidaBuscar = bebQ,
            BebidaIncluirInactivos = bebInc,
        };

        var categorias = db.Categorias.Include(c => c.TamanosPermitidos).ThenInclude(x => x.Tamano).AsQueryable();
        categorias = categorias.Where(c => c.Activo == !catInc);
        if (!string.IsNullOrWhiteSpace(catQ)) categorias = categorias.Where(c => c.NombreCategoria.Contains(catQ));
        vm.Categorias = await categorias.OrderBy(c => c.NombreCategoria).ToListAsync();

        var tamanos = db.Tamanos.AsQueryable();
        tamanos = tamanos.Where(t => t.Activo == !tamInc);
        if (!string.IsNullOrWhiteSpace(tamQ)) tamanos = tamanos.Where(t => t.NombreTamano.Contains(tamQ));
        vm.Tamanos = OrdenarTamanos(await tamanos.ToListAsync());

        var productos = db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Precios.Where(pr => pr.Activo))
                .ThenInclude(pr => pr.Tamano)
            .AsQueryable();
        productos = productos.Where(p => p.Activo == !prodInc);
        if (!string.IsNullOrWhiteSpace(prodQ)) productos = productos.Where(p => p.NombreProducto.Contains(prodQ));
        if (prodCat.HasValue) productos = productos.Where(p => p.IdCategoria == prodCat.Value);
        vm.CategoriasFiltro = await db.Categorias.AsNoTracking().OrderBy(c => c.NombreCategoria).ToListAsync();
        vm.Productos = await productos.OrderBy(p => p.NombreProducto).ToListAsync();

        var bebidas = db.Bebidas.Include(b => b.Tamano).AsQueryable();
        bebidas = bebidas.Where(b => b.Activo == !bebInc);
        if (!string.IsNullOrWhiteSpace(bebQ)) bebidas = bebidas.Where(b => b.NombreBebida.Contains(bebQ));
        vm.Bebidas = await bebidas.OrderBy(b => b.NombreBebida).ToListAsync();

        return View(vm);
    }

    // ==================================================================
    //  CATEGORÍAS
    // ==================================================================
    public async Task<IActionResult> CrearCategoria()
    {
        var modelo = new CategoriaFormViewModel();
        await CargarTamanosCategoria(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearCategoria(CategoriaFormViewModel modelo)
    {
        await CargarTamanosCategoria(modelo);
        modelo.NombreCategoria = NombreCatalogoAttribute.Normalizar(modelo.NombreCategoria);
        if (await db.Categorias.AnyAsync(c => c.NombreCategoria == modelo.NombreCategoria))
        {
            ModelState.AddModelError(nameof(modelo.NombreCategoria), "Ya existe una categoría con ese nombre.");
        }
        if (!ModelState.IsValid) return View(modelo);

        var nueva = new Categoria { NombreCategoria = modelo.NombreCategoria };
        foreach (var idTamano in modelo.TamanosPermitidos)
            nueva.TamanosPermitidos.Add(new CategoriaTamano { IdTamano = idTamano });
        db.Categorias.Add(nueva);
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Categoría \"{modelo.NombreCategoria}\" creada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
    }

    public async Task<IActionResult> EditarCategoria(int id)
    {
        var categoria = await db.Categorias.Include(c => c.TamanosPermitidos).FirstOrDefaultAsync(c => c.IdCategoria == id);
        if (categoria == null) return NotFound();
        var modelo = new CategoriaFormViewModel
        {
            IdCategoria = categoria.IdCategoria,
            NombreCategoria = categoria.NombreCategoria,
            TamanosPermitidos = categoria.TamanosPermitidos.Select(x => x.IdTamano).ToList(),
        };
        await CargarTamanosCategoria(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarCategoria(CategoriaFormViewModel modelo)
    {
        await CargarTamanosCategoria(modelo);
        modelo.NombreCategoria = NombreCatalogoAttribute.Normalizar(modelo.NombreCategoria);
        if (await db.Categorias.AnyAsync(c => c.NombreCategoria == modelo.NombreCategoria && c.IdCategoria != modelo.IdCategoria))
        {
            ModelState.AddModelError(nameof(modelo.NombreCategoria), "Ya existe otra categoría con ese nombre.");
        }
        await ValidarTamanosEnUsoCategoria(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var categoria = await db.Categorias.Include(c => c.TamanosPermitidos).FirstOrDefaultAsync(c => c.IdCategoria == modelo.IdCategoria);
        if (categoria == null) return NotFound();

        categoria.NombreCategoria = modelo.NombreCategoria;
        foreach (var enlace in categoria.TamanosPermitidos.Where(e => !modelo.TamanosPermitidos.Contains(e.IdTamano)).ToList())
            db.CategoriasTamanos.Remove(enlace);
        foreach (var idTamano in modelo.TamanosPermitidos.Where(id => categoria.TamanosPermitidos.All(e => e.IdTamano != id)))
            categoria.TamanosPermitidos.Add(new CategoriaTamano { IdTamano = idTamano });
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
        if (categoria.Activo)
        {
            var activos = await db.Productos.CountAsync(p => p.IdCategoria == id && p.Activo);
            if (activos > 0)
            {
                TempData["Error"] = $"No se puede desactivar la categoría «{categoria.NombreCategoria}»: tiene {activos} producto(s) activo(s). Desactive primero esos productos.";
                return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
            }
        }
        categoria.Activo = !categoria.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = categoria.Activo ? $"Categoría «{categoria.NombreCategoria}» activada." : $"Categoría «{categoria.NombreCategoria}» desactivada.";
        return RedirectToAction(nameof(Catalogo), new { tab = "categorias" });
    }

    // HU-007: los tamaños se muestran de menor a mayor (el orden se deduce del nombre).
    private static List<Tamano> OrdenarTamanos(IEnumerable<Tamano> tamanos) =>
        tamanos.OrderBy(t => OrdenTamano.Calcular(t.NombreTamano) ?? int.MaxValue).ThenBy(t => t.NombreTamano).ToList();

    // HU-007: tamaños que se pueden asignar a una categoría: activos y de comida (más los que ya tiene asignados).
    // Los ids enviados que no estén en esa lista se descartan.
    private async Task CargarTamanosCategoria(CategoriaFormViewModel modelo)
    {
        var vinculados = modelo.IdCategoria > 0
            ? await db.CategoriasTamanos.Where(ct => ct.IdCategoria == modelo.IdCategoria).Select(ct => ct.IdTamano).ToListAsync()
            : new List<int>();
        modelo.TamanosDisponibles = OrdenarTamanos(await db.Tamanos
            .Where(t => (t.Activo && !t.EsParaBebida) || vinculados.Contains(t.IdTamano))
            .ToListAsync());
        modelo.TamanosPermitidos = modelo.TamanosPermitidos.Distinct()
            .Where(id => modelo.TamanosDisponibles.Any(t => t.IdTamano == id)).ToList();
    }

    // HU-007: no se puede quitar de una categoría un tamaño que sus productos activos todavía usan.
    private async Task ValidarTamanosEnUsoCategoria(CategoriaFormViewModel modelo)
    {
        var permitidos = modelo.TamanosPermitidos;
        var enUso = await db.Precios
            .Where(p => p.Activo && p.IdTamano != null && p.Producto!.Activo
                        && p.Producto.IdCategoria == modelo.IdCategoria
                        && !permitidos.Contains(p.IdTamano.Value))
            .Select(p => p.Tamano!.NombreTamano)
            .Distinct().ToListAsync();
        if (enUso.Count > 0)
            ModelState.AddModelError(nameof(modelo.TamanosPermitidos),
                $"No se puede quitar {string.Join(", ", enUso)}: hay productos activos de esta categoría que lo usan.");
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

        db.Tamanos.Add(new Tamano { NombreTamano = modelo.NombreTamano, EsParaBebida = modelo.EsParaBebida });
        await db.SaveChangesAsync();
        TempData["Mensaje"] = $"Tamaño \"{modelo.NombreTamano}\" creado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "tamanos" });
    }

    public async Task<IActionResult> EditarTamano(int id)
    {
        var tamano = await db.Tamanos.FindAsync(id);
        if (tamano == null) return NotFound();
        return View(new TamanoFormViewModel
        {
            IdTamano = tamano.IdTamano,
            NombreTamano = tamano.NombreTamano,
            EsParaBebida = tamano.EsParaBebida,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarTamano(TamanoFormViewModel modelo)
    {
        if (await db.Tamanos.AnyAsync(t => t.NombreTamano == modelo.NombreTamano && t.IdTamano != modelo.IdTamano))
        {
            ModelState.AddModelError(nameof(modelo.NombreTamano), "Ya existe otro tamaño con ese nombre.");
        }
        await ValidarUsoTamano(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var tamano = await db.Tamanos.FindAsync(modelo.IdTamano);
        if (tamano == null) return NotFound();

        tamano.NombreTamano = modelo.NombreTamano;
        tamano.EsParaBebida = modelo.EsParaBebida;
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
        if (tamano.Activo)
        {
            var productos = await db.Precios.CountAsync(p => p.IdTamano == id && p.Activo && p.Producto!.Activo);
            var bebidas = await db.Bebidas.CountAsync(b => b.IdTamano == id && b.Activo);
            if (productos + bebidas > 0)
            {
                TempData["Error"] = $"No se puede desactivar el tamaño «{tamano.NombreTamano}»: lo usan {productos} producto(s) y {bebidas} bebida(s) activos. Desactívelos o cámbieles el tamaño primero.";
                return RedirectToAction(nameof(Catalogo), new { tab = "tamanos" });
            }
        }
        tamano.Activo = !tamano.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = tamano.Activo ? $"Tamaño «{tamano.NombreTamano}» activado." : $"Tamaño «{tamano.NombreTamano}» desactivado.";
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
        modelo.NombreProducto = NombreCatalogoAttribute.Normalizar(modelo.NombreProducto);
        await ValidarNombreProductoUnico(modelo);
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
        modelo.NombreProducto = NombreCatalogoAttribute.Normalizar(modelo.NombreProducto);
        await ValidarNombreProductoUnico(modelo);
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
        if (!producto.Activo)
        {
            var categoria = await db.Categorias.FindAsync(producto.IdCategoria);
            if (categoria is { Activo: false })
            {
                TempData["Error"] = $"No se puede activar el producto «{producto.NombreProducto}»: su categoría «{categoria.NombreCategoria}» está inactiva. Active primero la categoría.";
                return RedirectToAction(nameof(Catalogo), new { tab = "productos" });
            }
        }
        producto.Activo = !producto.Activo;
        // Nota: no se tocan los precios. Se desactiva solo el producto; sus precios
        // quedan tal cual estaban (según se acordó para este sprint).
        await db.SaveChangesAsync();
        TempData["Mensaje"] = producto.Activo ? $"Producto «{producto.NombreProducto}» activado." : $"Producto «{producto.NombreProducto}» desactivado.";
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
        var tamanos = OrdenarTamanos(await db.Tamanos
            .Where(t => (t.Activo && !t.EsParaBebida) || tamanosAsignados.Contains(t.IdTamano))
            .ToListAsync());

        // HU-007: tamaños permitidos por categoría.
        var enlaces = await db.CategoriasTamanos.AsNoTracking().ToListAsync();
        var categoriasPorTamano = enlaces.GroupBy(e => e.IdTamano)
            .ToDictionary(g => g.Key, g => g.Select(e => e.IdCategoria).ToHashSet());
        string CategoriasDe(int idTamano) =>
            categoriasPorTamano.TryGetValue(idTamano, out var cats) ? string.Join(",", cats) : "";
        var esPost = HttpContext.Request.Method == "POST";

        // Conserva los índices de los campos enviados cuando hay errores de validación.
        for (int i = 0; i < modelo.PreciosPorTamano.Count; i++)
        {
            var entrada = modelo.PreciosPorTamano[i];
            var tamano = tamanos.FirstOrDefault(t => t.IdTamano == entrada.IdTamano);
            entrada.NombreTamano = tamano?.NombreTamano ?? "Tamaño no disponible";
            entrada.TamanoActivo = tamano?.Activo ?? false;
            entrada.Orden = OrdenTamano.Calcular(entrada.NombreTamano);
            entrada.CategoriasPermitidas = CategoriasDe(entrada.IdTamano);
            if (modelo.RequiereTamano && entrada.Incluir && tamano == null)
                ModelState.AddModelError(string.Empty, "Uno de los tamaños seleccionados ya no está disponible.");
            else if (esPost && modelo.RequiereTamano && entrada.Incluir && modelo.IdCategoria > 0
                     && !(categoriasPorTamano.TryGetValue(entrada.IdTamano, out var permitidas) && permitidas.Contains(modelo.IdCategoria)))
                ModelState.AddModelError($"PreciosPorTamano[{i}].Monto",
                    $"El tamaño {entrada.NombreTamano} no está disponible para la categoría seleccionada. Asígnelo a la categoría desde el catálogo.");
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
                Orden = OrdenTamano.Calcular(tamano.NombreTamano),
                CategoriasPermitidas = CategoriasDe(tamano.IdTamano),
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
                ModelState.AddModelError(nameof(modelo.RequiereTamano), "Seleccione al menos un tamaño e indique su precio.");
            for (int i = 0; i < modelo.PreciosPorTamano.Count; i++)
            {
                var precio = modelo.PreciosPorTamano[i];
                if (precio.Incluir && precio.Monto is null or <= 0)
                    ModelState.AddModelError($"PreciosPorTamano[{i}].Monto", precio.Monto is null
                        ? "Indique el precio de este tamaño."
                        : "No se pueden colocar valores negativos o iguales a cero.");
                else if (precio.Incluir && precio.Monto > ReglasCatalogo.PrecioMaximo)
                    ModelState.AddModelError($"PreciosPorTamano[{i}].Monto", ReglasCatalogo.MensajePrecioMaximo);
            }
            ValidarOrdenDePrecios(modelo);
            if (seleccionados.GroupBy(p => p.IdTamano).Any(g => g.Count() > 1))
                ModelState.AddModelError(string.Empty, "No se puede repetir un tamaño en el producto.");
        }
        else if (modelo.PrecioUnico is null or <= 0)
        {
            ModelState.AddModelError(nameof(modelo.PrecioUnico), modelo.PrecioUnico is null ? "Indique el precio." : "No se pueden colocar valores negativos o iguales a cero.");
        }
        else if (modelo.PrecioUnico > ReglasCatalogo.PrecioMaximo)
        {
            ModelState.AddModelError(nameof(modelo.PrecioUnico), ReglasCatalogo.MensajePrecioMaximo);
        }
    }

    // HU-007: un tamaño mayor no puede costar menos que uno menor (ej. Mediano ₡1.500 y Grande ₡500 no tiene sentido).
    // Igual precio entre tamaños sí se permite. Solo se comparan precios ya válidos.
    private void ValidarOrdenDePrecios(ProductoFormViewModel modelo)
    {
        var ordenados = modelo.PreciosPorTamano
            .Select((p, i) => (Entrada: p, Indice: i))
            .Where(x => x.Entrada.Incluir && x.Entrada.Orden != null && x.Entrada.Monto is > 0 && x.Entrada.Monto <= ReglasCatalogo.PrecioMaximo)
            .OrderBy(x => x.Entrada.Orden)
            .ToList();

        for (int k = 1; k < ordenados.Count; k++)
        {
            var menor = ordenados[k - 1].Entrada;
            var mayor = ordenados[k].Entrada;
            if (mayor.Orden > menor.Orden && mayor.Monto < menor.Monto)
            {
                ModelState.AddModelError($"PreciosPorTamano[{ordenados[k].Indice}].Monto",
                    $"El precio de {mayor.NombreTamano} (₡{mayor.Monto:N0}) no puede ser menor que el de {menor.NombreTamano} (₡{menor.Monto:N0}).");
            }
        }
    }

    // HU-007: un tamaño en uso no cambia de tipo (comida o bebida) mientras lo usen productos, categorías o bebidas.
    private async Task ValidarUsoTamano(TamanoFormViewModel modelo)
    {
        if (modelo.IdTamano <= 0) return;
        var actual = await db.Tamanos.AsNoTracking().FirstOrDefaultAsync(t => t.IdTamano == modelo.IdTamano);
        if (actual == null || actual.EsParaBebida == modelo.EsParaBebida) return;

        if (modelo.EsParaBebida)
        {
            var enProductos = await db.Precios.AnyAsync(p => p.IdTamano == modelo.IdTamano)
                              || await db.CategoriasTamanos.AnyAsync(ct => ct.IdTamano == modelo.IdTamano);
            if (enProductos)
                ModelState.AddModelError(nameof(modelo.EsParaBebida),
                    "No se puede marcar como tamaño de bebida: ya lo usan productos o categorías. Quítelo de ellos primero.");
        }
        else if (await db.Bebidas.AnyAsync(b => b.IdTamano == modelo.IdTamano))
        {
            ModelState.AddModelError(nameof(modelo.EsParaBebida),
                "No se puede cambiar a tamaño de comida: ya lo usan bebidas.");
        }
    }

    // ==================================================================
    //  BEBIDAS
    // ==================================================================
    public async Task<IActionResult> CrearBebida()
    {
        var vm = new BebidaFormViewModel
        {
            TamanosDisponibles = await db.Tamanos.Where(t => t.Activo && t.EsParaBebida).OrderBy(t => t.NombreTamano).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearBebida(BebidaFormViewModel modelo)
    {
        modelo.TamanosDisponibles = await db.Tamanos.Where(t => t.Activo && t.EsParaBebida).OrderBy(t => t.NombreTamano).ToListAsync();
        modelo.NombreBebida = NombreCatalogoAttribute.Normalizar(modelo.NombreBebida);
        await ValidarNombreBebidaUnico(modelo);
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
            TamanosDisponibles = await db.Tamanos.Where(t => (t.Activo && t.EsParaBebida) || t.IdTamano == bebida.IdTamano)
                .OrderBy(t => t.NombreTamano).ToListAsync(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarBebida(BebidaFormViewModel modelo)
    {
        var bebida = await db.Bebidas.FindAsync(modelo.IdBebida);
        if (bebida == null) return NotFound();
        modelo.TamanosDisponibles = await db.Tamanos.Where(t => (t.Activo && t.EsParaBebida) || t.IdTamano == bebida.IdTamano)
            .OrderBy(t => t.NombreTamano).ToListAsync();
        modelo.NombreBebida = NombreCatalogoAttribute.Normalizar(modelo.NombreBebida);
        await ValidarNombreBebidaUnico(modelo);
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

    // HU-007: no se repite un producto (mismo nombre) dentro de la misma categoría, esté activo o no.
    private async Task ValidarNombreProductoUnico(ProductoFormViewModel modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo.NombreProducto) || modelo.IdCategoria <= 0) return;
        var existente = await db.Productos.FirstOrDefaultAsync(p =>
            p.NombreProducto == modelo.NombreProducto
            && p.IdCategoria == modelo.IdCategoria
            && p.IdProducto != modelo.IdProducto);
        if (existente == null) return;
        ModelState.AddModelError(nameof(modelo.NombreProducto), existente.Activo
            ? "Ya existe un producto con ese nombre en esta categoría."
            : "Ya existe un producto con ese nombre en esta categoría, pero está desactivado. Actívelo desde el catálogo en lugar de crearlo de nuevo.");
    }

    // HU-007: no se repite una bebida con el mismo nombre y tamaño (la misma bebida puede existir en otro tamaño).
    private async Task ValidarNombreBebidaUnico(BebidaFormViewModel modelo)
    {
        if (string.IsNullOrWhiteSpace(modelo.NombreBebida)) return;
        var existente = await db.Bebidas.FirstOrDefaultAsync(b =>
            b.NombreBebida == modelo.NombreBebida
            && b.IdTamano == modelo.IdTamano
            && b.IdBebida != modelo.IdBebida);
        if (existente == null) return;
        ModelState.AddModelError(nameof(modelo.NombreBebida), existente.Activo
            ? "Ya existe una bebida con ese nombre y tamaño."
            : "Ya existe una bebida con ese nombre y tamaño, pero está desactivada. Actívela desde el catálogo en lugar de crearla de nuevo.");
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
        if (!bebida.Activo && bebida.IdTamano.HasValue)
        {
            var tamano = await db.Tamanos.FindAsync(bebida.IdTamano.Value);
            if (tamano is { Activo: false })
            {
                TempData["Error"] = $"No se puede activar la bebida «{bebida.NombreBebida}»: su tamaño «{tamano.NombreTamano}» está inactivo. Active primero el tamaño.";
                return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
            }
        }
        bebida.Activo = !bebida.Activo;
        await db.SaveChangesAsync();
        TempData["Mensaje"] = bebida.Activo ? $"Bebida «{bebida.NombreBebida}» activada." : $"Bebida «{bebida.NombreBebida}» desactivada.";
        return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
    }
}
