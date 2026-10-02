using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

// Ejecutar contra SQL Server local. No modifica SAPS_DB: crea y elimina su propia base.
if (args.Length != 1) throw new ArgumentException("Indique la ruta de appsettings.json del entorno local.");
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(args[0]));
var connection = new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
if (connection.DataSource is not ("localhost,1433" or "127.0.0.1,1433"))
    throw new InvalidOperationException("Estas pruebas solo admiten SQL Server local en el puerto 1433.");
connection.InitialCatalog = "SAPS_HU007_Pruebas_" + Guid.NewGuid().ToString("N");
var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection.ConnectionString));
servicios.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var proveedor = servicios.BuildServiceProvider();
await using var alcance = proveedor.CreateAsyncScope();
var db = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var verificaciones = 0;
void Verificar(bool condicion, string caso)
{
    if (!condicion) throw new Exception("FALLO: " + caso);
    verificaciones++;
    Console.WriteLine("OK: " + caso);
}
AdministracionController Controlador(string method = "POST")
{
    var http = new DefaultHttpContext();
    http.Request.Method = method;
    return new AdministracionController(db)
    {
        ControllerContext = new ControllerContext { HttpContext = http },
        TempData = new TempDataDictionary(http, new MemoriaTempData()),
    };
}
async Task<ProductoFormViewModel> Editar(int id)
{
    db.ChangeTracker.Clear();
    return (ProductoFormViewModel)((ViewResult)await Controlador("GET").EditarProducto(id)).Model!;
}
async Task<List<Precio>> Activos(int id)
{
    db.ChangeTracker.Clear();
    return await db.Precios.Where(p => p.IdProducto == id && p.Activo).ToListAsync();
}
try
{
    await db.Database.MigrateAsync();
    var categoria = new Categoria { NombreCategoria = "Prueba" };
    var pequeno = new Tamano { NombreTamano = "Pequeño" };
    var grande = new Tamano { NombreTamano = "Grande" };
    db.AddRange(categoria, pequeno, grande);
    await db.SaveChangesAsync();
    var categoriaId = categoria.IdCategoria;
    var pequenoId = pequeno.IdTamano;
    var grandeId = grande.IdTamano;

    var crear = (ProductoFormViewModel)((ViewResult)await Controlador("GET").CrearProducto()).Model!;
    Verificar(crear.PreciosPorTamano.Count == 2, "Nuevo producto carga tamaños antes de seleccionar el modo");
    crear.NombreProducto = "Producto"; crear.IdCategoria = categoriaId; crear.PrecioUnico = 1500;
    Verificar(await Controlador().CrearProducto(crear) is RedirectToActionResult, "Crear producto con precio único");
    var id = await db.Productos.Select(p => p.IdProducto).SingleAsync();
    var modelo = await Editar(id);
    Verificar(modelo.PreciosPorTamano.Count == 2 && modelo.PrecioUnico == 1500, "Edición de precio único carga tamaños y precio actual");
    modelo.RequiereTamano = true;
    foreach (var entrada in modelo.PreciosPorTamano) { entrada.Incluir = true; entrada.Monto = entrada.IdTamano == pequenoId ? 1700 : 2300; }
    Verificar(await Controlador().EditarProducto(modelo) is RedirectToActionResult, "Cambiar de precio único a tamaños");
    var activos = await Activos(id);
    Verificar(activos.Count == 2 && activos.All(p => p.IdTamano != null), "Solo quedan activos los precios por tamaño");
    Verificar(await db.Precios.AnyAsync(p => p.IdProducto == id && p.IdTamano == null && !p.Activo && p.FechaVigenciaHasta != null), "Precio único anterior conserva historial cerrado");

    modelo = await Editar(id); modelo.RequiereTamano = false; modelo.PrecioUnico = 1000;
    Verificar(await Controlador().EditarProducto(modelo) is RedirectToActionResult, "Volver de tamaños a precio único");
    activos = await Activos(id);
    Verificar(activos.Count == 1 && activos[0].IdTamano == null && activos[0].MontoPrecio == 1000, "Solo queda vigente el nuevo precio único");
    modelo = await Editar(id); modelo.PrecioUnico = 1200;
    await Controlador().EditarProducto(modelo);
    activos = await Activos(id);
    Verificar(activos.Count == 1 && activos[0].MontoPrecio == 1200, "Actualizar precio único sin violar índice de unicidad");
    var totalPrecios = await db.Precios.CountAsync();
    modelo = await Editar(id); await Controlador().EditarProducto(modelo);
    Verificar(await db.Precios.CountAsync() == totalPrecios, "Guardar sin cambio de precio no duplica el historial");

    modelo = await Editar(id); modelo.RequiereTamano = true;
    foreach (var entrada in modelo.PreciosPorTamano) { entrada.Incluir = true; entrada.Monto = entrada.IdTamano == pequenoId ? 1500 : null; }
    Verificar(await Controlador().EditarProducto(modelo) is ViewResult, "Rechazar tamaño seleccionado sin precio aunque otro sea válido");
    activos = await Activos(id);
    Verificar(activos.Count == 1 && activos[0].MontoPrecio == 1200, "Validación fallida no modifica precios");
    modelo = await Editar(id); modelo.RequiereTamano = true;
    modelo.PreciosPorTamano = [new() { IdTamano = 999999, Incluir = true, Monto = 2000 }];
    Verificar(await Controlador().EditarProducto(modelo) is ViewResult, "Rechazar tamaño inexistente");

    modelo = await Editar(id); modelo.RequiereTamano = true;
    foreach (var entrada in modelo.PreciosPorTamano) { entrada.Incluir = true; entrada.Monto = 2000; }
    await Controlador().EditarProducto(modelo);
    modelo = await Editar(id); modelo.PreciosPorTamano.Single(p => p.IdTamano == grandeId).Monto = 2500;
    await Controlador().EditarProducto(modelo);
    activos = await Activos(id);
    Verificar(activos.Count == 2 && activos.Single(p => p.IdTamano == grandeId).MontoPrecio == 2500, "Modificar precio por tamaño conserva una sola versión activa");

    var bebida = new Bebida { NombreBebida = "Bebida", TipoBebida = "Gaseosa", IdTamano = grandeId, Precio = 900 };
    db.Bebidas.Add(bebida); await db.SaveChangesAsync(); var bebidaId = bebida.IdBebida;
    await Controlador().CambiarEstadoTamano(grandeId);
    modelo = await Editar(id);
    Verificar(modelo.PreciosPorTamano.Any(p => p.IdTamano == grandeId && !p.TamanoActivo && p.Incluir && p.Monto == 2500), "Edición conserva tamaño inactivo asignado y lo identifica");
    await Controlador().EditarProducto(modelo);
    activos = await Activos(id);
    Verificar(activos.Any(p => p.IdTamano == grandeId && p.MontoPrecio == 2500), "Editar sin cambios no elimina precio de tamaño inactivo");
    var bebidaVm = (BebidaFormViewModel)((ViewResult)await Controlador("GET").EditarBebida(bebidaId)).Model!;
    Verificar(bebidaVm.TamanosDisponibles.Any(t => t.IdTamano == grandeId && !t.Activo), "Bebida muestra su tamaño inactivo actual");
    bebidaVm.NombreBebida = "Bebida editada"; await Controlador().EditarBebida(bebidaVm);
    db.ChangeTracker.Clear();
    Verificar((await db.Bebidas.FindAsync(bebidaId))!.IdTamano == grandeId, "Editar bebida conserva su tamaño inactivo");
    var bebidaNueva = new BebidaFormViewModel { NombreBebida = "Otra", TipoBebida = "Gaseosa", IdTamano = grandeId, Precio = 1000 };
    Verificar(await Controlador().CrearBebida(bebidaNueva) is ViewResult, "Rechazar asignación nueva de un tamaño inactivo");

    await Controlador().CambiarEstadoCategoria(categoriaId);
    modelo = await Editar(id);
    Verificar(modelo.CategoriasDisponibles.Any(c => c.IdCategoria == categoriaId && !c.Activo), "Conservar categoría inactiva actualmente asignada");

    // Simula datos inconsistentes producidos por la versión anterior y los normaliza al guardar.
    db.Precios.Add(new Precio { IdProducto = id, IdTamano = null, MontoPrecio = 1000 });
    await db.SaveChangesAsync();
    modelo = await Editar(id); modelo.RequiereTamano = false; modelo.PrecioUnico = 1300;
    await Controlador().EditarProducto(modelo);
    activos = await Activos(id);
    Verificar(activos.Count == 1 && activos[0].IdTamano == null && activos[0].MontoPrecio == 1300, "Reparar mezcla anterior de precios al guardar producto");

    await db.Database.ExecuteSqlRawAsync("ALTER TABLE soda.tb_Precio ADD CONSTRAINT CK_PruebaRollback CHECK (Monto <> 7777)");
    modelo = await Editar(id); modelo.PrecioUnico = 7777;
    var falloEsperado = false;
    try { await Controlador().EditarProducto(modelo); }
    catch (DbUpdateException) { falloEsperado = true; }
    Verificar(falloEsperado, "Simular fallo al insertar el nuevo precio");
    activos = await Activos(id);
    Verificar(activos.Count == 1 && activos[0].MontoPrecio == 1300 && activos[0].FechaVigenciaHasta == null,
        "Fallo de guardado revierte el cierre del precio anterior");
    var roles = ((AuthorizeAttribute)Attribute.GetCustomAttribute(typeof(AdministracionController), typeof(AuthorizeAttribute))!).Roles!.Split(',');
    Verificar(roles.Contains("Administrador") && roles.Contains("RecursosHumanos") && !roles.Contains("Soda"), "Política del catálogo incluye Administrador y Recursos Humanos");
    Console.WriteLine($"RESULTADO: {verificaciones} verificaciones correctas.");
}
finally
{
    await db.Database.EnsureDeletedAsync();
    Console.WriteLine("Base temporal de pruebas eliminada.");
}

sealed class MemoriaTempData : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
