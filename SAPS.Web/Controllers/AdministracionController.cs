// Controllers/AdministracionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "Administrador")]
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
        var vm = new ProductoFormViewModel();
        await RecargarListasProducto(vm, forzarTamanos: true);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CrearProducto(ProductoFormViewModel modelo)
    {
        await RecargarListasProducto(modelo, forzarTamanos: false);
        ValidarPreciosProducto(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var producto = new Producto
        {
            NombreProducto = modelo.NombreProducto,
            IdCategoria = modelo.IdCategoria,
            EsEspecial = modelo.EsEspecial,
            RequiereTamano = modelo.RequiereTamano,
        };
        db.Productos.Add(producto);
        await db.SaveChangesAsync(); // necesitamos el IdProducto antes de crear los precios

        if (modelo.RequiereTamano)
        {
            foreach (var entrada in modelo.PreciosPorTamano.Where(p => p.Incluir && p.Monto is > 0))
            {
                db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = entrada.IdTamano, MontoPrecio = entrada.Monto!.Value });
            }
        }
        else if (modelo.PrecioUnico is > 0)
        {
            db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = null, MontoPrecio = modelo.PrecioUnico.Value });
        }

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

        var vm = new ProductoFormViewModel
        {
            IdProducto = producto.IdProducto,
            NombreProducto = producto.NombreProducto,
            IdCategoria = producto.IdCategoria,
            EsEspecial = producto.EsEspecial,
            RequiereTamano = producto.RequiereTamano,
        };

        await RecargarListasProducto(vm, forzarTamanos: false, incluirCategoriaActual: producto.IdCategoria);

        if (producto.RequiereTamano)
        {
            var tamanos = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano).ToListAsync();
            vm.PreciosPorTamano = tamanos.Select(t =>
            {
                var precioActual = producto.Precios.FirstOrDefault(pr => pr.IdTamano == t.IdTamano);
                return new PrecioPorTamanoInput
                {
                    IdTamano = t.IdTamano,
                    NombreTamano = t.NombreTamano,
                    Incluir = precioActual != null,
                    Monto = precioActual?.MontoPrecio,
                };
            }).ToList();
        }
        else
        {
            vm.PrecioUnico = producto.Precios.FirstOrDefault(pr => pr.IdTamano == null)?.MontoPrecio;
        }

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarProducto(ProductoFormViewModel modelo)
    {
        await RecargarListasProducto(modelo, forzarTamanos: false, incluirCategoriaActual: modelo.IdCategoria);
        ValidarPreciosProducto(modelo);
        if (!ModelState.IsValid) return View(modelo);

        var producto = await db.Productos
            .Include(p => p.Precios.Where(pr => pr.Activo))
            .FirstOrDefaultAsync(p => p.IdProducto == modelo.IdProducto);
        if (producto == null) return NotFound();

        producto.NombreProducto = modelo.NombreProducto;
        producto.IdCategoria = modelo.IdCategoria;
        producto.EsEspecial = modelo.EsEspecial;
        producto.RequiereTamano = modelo.RequiereTamano;

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        if (modelo.RequiereTamano)
        {
            foreach (var entrada in modelo.PreciosPorTamano)
            {
                var precioActual = producto.Precios.FirstOrDefault(pr => pr.IdTamano == entrada.IdTamano);

                if (!entrada.Incluir || entrada.Monto is not > 0)
                {
                    // Este tamaño se quitó del producto: cerramos su precio activo, si tenía uno.
                    if (precioActual != null)
                    {
                        precioActual.Activo = false;
                        precioActual.FechaVigenciaHasta = hoy;
                    }
                    continue;
                }

                if (precioActual == null)
                {
                    db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = entrada.IdTamano, MontoPrecio = entrada.Monto!.Value });
                }
                else if (precioActual.MontoPrecio != entrada.Monto!.Value)
                {
                    // Cambió el precio: cerramos la fila anterior y abrimos una nueva (historial).
                    precioActual.Activo = false;
                    precioActual.FechaVigenciaHasta = hoy;
                    db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = entrada.IdTamano, MontoPrecio = entrada.Monto.Value });
                }
            }
        }
        else
        {
            var precioActual = producto.Precios.FirstOrDefault(pr => pr.IdTamano == null);
            if (modelo.PrecioUnico is > 0)
            {
                if (precioActual == null)
                {
                    db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = null, MontoPrecio = modelo.PrecioUnico.Value });
                }
                else if (precioActual.MontoPrecio != modelo.PrecioUnico.Value)
                {
                    precioActual.Activo = false;
                    precioActual.FechaVigenciaHasta = hoy;
                    db.Precios.Add(new Precio { IdProducto = producto.IdProducto, IdTamano = null, MontoPrecio = modelo.PrecioUnico.Value });
                }
            }
        }

        await db.SaveChangesAsync();
        TempData["Mensaje"] = "Producto actualizado correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "productos" });
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

    private async Task RecargarListasProducto(ProductoFormViewModel modelo, bool forzarTamanos, int? incluirCategoriaActual = null)
    {
        var categorias = db.Categorias.Where(c => c.Activo || c.IdCategoria == incluirCategoriaActual);
        modelo.CategoriasDisponibles = await categorias.OrderBy(c => c.NombreCategoria).ToListAsync();

        if (modelo.RequiereTamano && (forzarTamanos || modelo.PreciosPorTamano.Count == 0))
        {
            modelo.PreciosPorTamano = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano)
                .Select(t => new PrecioPorTamanoInput { IdTamano = t.IdTamano, NombreTamano = t.NombreTamano })
                .ToListAsync();
        }
    }

    private void ValidarPreciosProducto(ProductoFormViewModel modelo)
    {
        if (modelo.RequiereTamano)
        {
            if (!modelo.PreciosPorTamano.Any(p => p.Incluir && p.Monto is > 0))
            {
                ModelState.AddModelError(string.Empty, "Debe indicar el precio de al menos un tamaño.");
            }
            if (modelo.PreciosPorTamano.Any(p => p.Incluir && p.Monto is <= 0))
            {
                ModelState.AddModelError(string.Empty, "Los precios deben ser mayores a cero.");
            }
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
            TamanosDisponibles = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano).ToListAsync(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarBebida(BebidaFormViewModel modelo)
    {
        modelo.TamanosDisponibles = await db.Tamanos.Where(t => t.Activo).OrderBy(t => t.NombreTamano).ToListAsync();
        if (!ModelState.IsValid) return View(modelo);

        var bebida = await db.Bebidas.FindAsync(modelo.IdBebida);
        if (bebida == null) return NotFound();

        bebida.NombreBebida = modelo.NombreBebida;
        bebida.TipoBebida = modelo.TipoBebida;
        bebida.IdTamano = modelo.IdTamano;
        bebida.Precio = modelo.Precio;

        await db.SaveChangesAsync();
        TempData["Mensaje"] = "Bebida actualizada correctamente.";
        return RedirectToAction(nameof(Catalogo), new { tab = "bebidas" });
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
