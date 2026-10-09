using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Pedidos;
using SAPS.Web.Services.Pedidos;

// Crea y elimina una base independiente; nunca utiliza SAPS_Db para las pruebas.
if (args.Length is < 1 or > 2) throw new ArgumentException("Indique la ruta local de SAPS.Web/appsettings.json.");
var archivo = Path.GetFullPath(args[0]);
using var config = JsonDocument.Parse(await File.ReadAllTextAsync(archivo));
var conexion = new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
if (conexion.DataSource is not ("localhost,1433" or "127.0.0.1,1433")) throw new InvalidOperationException("Solo se permite SQL Server local.");
conexion.InitialCatalog = "SAPS_HU008_Pruebas_" + Guid.NewGuid().ToString("N");
var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(conexion.ConnectionString));
servicios.AddDefaultIdentity<IdentityUser>(o => o.Password.RequireNonAlphanumeric = false).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var proveedor = servicios.BuildServiceProvider();
await using var alcance = proveedor.CreateAsyncScope();
var db = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var empleados = new ColaboradoresDePrueba();
var reloj = new RelojPrueba();
var pedidos = new ServicioPedidos(db, empleados, reloj);
var verificaciones = 0;
Process? app = null;
void Verificar(bool valor, string caso)
{
    if (!valor) throw new Exception("FALLO: " + caso);
    Console.WriteLine("OK: " + caso); verificaciones++;
}
async Task Rechazar(Func<Task> accion, string caso)
{
    var antes = await db.Pedidos.CountAsync();
    var rechazo = false;
    try { await accion(); } catch (PedidoInvalidoException) { rechazo = true; }
    db.ChangeTracker.Clear();
    Verificar(rechazo && await db.Pedidos.CountAsync() == antes, caso);
}
try
{
    await db.Database.MigrateAsync();
    Verificar(!db.Database.HasPendingModelChanges(), "El modelo coincide con las migraciones");
    var usuario = new IdentityUser { UserName = "operadora-prueba" };
    db.Users.Add(usuario);
    var categoria = new Categoria { NombreCategoria = "Almuerzos" };
    var tamano = new Tamano { NombreTamano = "Grande" };
    var producto = new Producto { NombreProducto = "Casado", Categoria = categoria };
    var precio = new Precio { Producto = producto, MontoPrecio = 1500, FechaVigenciaDesde = new(2026, 1, 1) };
    var bebida = new Bebida { NombreBebida = "Jugo", TipoBebida = "Jugo", Tamano = tamano, Precio = 700 };
    var fresco = new Producto { NombreProducto = "Fresco natural", Categoria = categoria, RequiereTamano = true };
    var precioFresco = new Precio { Producto = fresco, Tamano = tamano, MontoPrecio = 500, FechaVigenciaDesde = new(2026, 1, 1) };
    db.AddRange(precio, bebida, precioFresco); await db.SaveChangesAsync();
    var usuarioId = usuario.Id; var precioId = precio.IdPrecio; var bebidaId = bebida.IdBebida;
    var categoriaId = categoria.IdCategoria; var tamanoId = tamano.IdTamano; var productoId = producto.IdProducto;
    RegistrarPedidoRequest Solicitud() => new()
    {
        TokenRegistro = Guid.NewGuid(), CodigoColaborador = "DEMO001", TipoComida = "Almuerzos",
        Lineas = [new() { Clave = $"P:{precioId}", Cantidad = 2, PrecioMostrado = 1500 }, new() { Clave = $"B:{bebidaId}", Cantidad = 1, PrecioMostrado = 700 }]
    };
    Verificar((await pedidos.BuscarColaboradorAsync(" demo001 ")).Codigo == "DEMO001", "Buscar código normalizando espacios y mayúsculas");
    Verificar((await pedidos.BuscarColaboradorAsync("DEMO002")).FotoUrl.EndsWith(".png"), "Colaborador devuelve nombre y fotografía de prueba");
    await Rechazar(async () => { await pedidos.BuscarColaboradorAsync(""); }, "Código vacío rechazado");
    await Rechazar(async () => { await pedidos.BuscarColaboradorAsync("NOEXISTE"); }, "Código inexistente rechazado");
    await Rechazar(async () => { await pedidos.BuscarColaboradorAsync("DEMO003"); }, "Colaborador inactivo rechazado");
    await Rechazar(async () => { await new ColaboradoresSinConexion().BuscarAsync("DEMO001"); }, "Fuera de desarrollo no se consultan colaboradores ficticios");
    Verificar((await pedidos.CatalogoAsync()).Count == 3, "Catálogo ofrece producto, bebida y variante de tamaño activos");
    var solicitud = Solicitud(); solicitud.Observaciones = "  Sin ensalada  ";
    var resultado = await pedidos.RegistrarAsync(solicitud, usuarioId);
    db.ChangeTracker.Clear();
    var guardado = await db.Pedidos.Include(p => p.Detalles).SingleAsync();
    Verificar(resultado.Total == 3700 && guardado.Detalles.Sum(d => d.Subtotal) == 3700, "Totales enteros sin impuesto calculados desde SQL Server");
    Verificar(guardado.CodigoColaborador == "DEMO001" && guardado.IdUsuarioRegistro == usuarioId && guardado.EsPrueba, "Comprador y operadora se guardan separados y se marca dato ficticio");
    Verificar(guardado.Observaciones == "Sin ensalada" && guardado.FechaRegistroUtc == reloj.GetUtcNow().UtcDateTime, "Fecha y observación guardadas");
    var repetido = await pedidos.RegistrarAsync(solicitud, usuarioId);
    Verificar(repetido.IdPedido == resultado.IdPedido && await db.Pedidos.CountAsync() == 1, "Reintentar el mismo token no duplica la compra");
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, "otra-persona"); }, "Otra operadora no puede recuperar el pedido por su token");
    solicitud = Solicitud(); solicitud.Lineas = [];
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Carrito vacío no crea cabecera");
    foreach (var cantidad in new[] { 0, -1 })
    {
        solicitud = Solicitud(); solicitud.Lineas[0].Cantidad = cantidad;
        await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, $"Cantidad {cantidad} rechazada");
    }
    solicitud = Solicitud(); solicitud.Lineas[0].PrecioMostrado = 1;
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Manipular el precio enviado no altera el cobro");
    solicitud = Solicitud(); solicitud.Lineas.Add(solicitud.Lineas[0]);
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Renglón repetido rechazado");
    solicitud = Solicitud(); solicitud.CodigoColaborador = "DEMO003";
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "El estado del colaborador se valida también al registrar");
    solicitud = Solicitud(); solicitud.TipoComida = "Otro";
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Tipo de comida inválido rechazado");
    solicitud = Solicitud(); solicitud.Observaciones = new string('x', 501);
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Límite de longitud de observaciones validado");
    solicitud = Solicitud(); solicitud.TokenRegistro = Guid.Empty;
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Token vacío rechazado");
    solicitud = Solicitud(); solicitud.Lineas[0].Clave = "P:999999";
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Artículo inexistente rechazado sin guardar parcialmente");
    await db.Productos.Where(p => p.IdProducto == productoId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, false));
    Verificar((await pedidos.CatalogoAsync()).All(a => a.Clave != $"P:{precioId}"), "Producto desactivado no se ofrece");
    await Rechazar(async () => { await pedidos.RegistrarAsync(Solicitud(), usuarioId); }, "Producto desactivado después de agregarlo se rechaza");
    await db.Productos.Where(p => p.IdProducto == productoId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, true));
    await db.Categorias.Where(c => c.IdCategoria == categoriaId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Activo, false));
    Verificar((await pedidos.CatalogoAsync()).Count == 1, "Categoría inactiva impide nuevas ventas de sus productos");
    await db.Categorias.Where(c => c.IdCategoria == categoriaId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Activo, true));
    await db.Tamanos.Where(t => t.IdTamano == tamanoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false));
    Verificar((await pedidos.CatalogoAsync()).Count == 1, "Tamaño inactivo excluye tanto fresco como bebida");
    await Rechazar(async () => { await pedidos.RegistrarAsync(Solicitud(), usuarioId); }, "Tamaño desactivado después de seleccionar bebida se rechaza");
    await db.Tamanos.Where(t => t.IdTamano == tamanoId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, true));
    await db.Precios.Where(p => p.IdPrecio == precioId).ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaVigenciaDesde, new DateOnly(2099, 1, 1)));
    Verificar((await pedidos.CatalogoAsync()).All(a => a.Clave != $"P:{precioId}"), "Precio futuro no se ofrece");
    await db.Precios.Where(p => p.IdPrecio == precioId).ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaVigenciaDesde, new DateOnly(2026, 1, 1)).SetProperty(p => p.FechaVigenciaHasta, new DateOnly(2026, 1, 2)));
    Verificar((await pedidos.CatalogoAsync()).All(a => a.Clave != $"P:{precioId}"), "Precio vencido no se ofrece");
    await db.Precios.Where(p => p.IdPrecio == precioId).ExecuteUpdateAsync(s => s.SetProperty(p => p.FechaVigenciaHasta, (DateOnly?)null));
    await db.Bebidas.Where(b => b.IdBebida == bebidaId).ExecuteUpdateAsync(s => s.SetProperty(b => b.Precio, 900));
    await Rechazar(async () => { await pedidos.RegistrarAsync(Solicitud(), usuarioId); }, "Precio de bebida cambiado exige revisar el nuevo monto");
    guardado = await db.Pedidos.AsNoTracking().Include(p => p.Detalles).SingleAsync();
    Verificar(guardado.Total == 3700 && guardado.Detalles.Single(d => d.IdBebida != null).PrecioUnitario == 700, "Cambio de catálogo conserva precio y total históricos");
    await db.Bebidas.Where(b => b.IdBebida == bebidaId).ExecuteUpdateAsync(s => s.SetProperty(b => b.Precio, 700));
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE tb_DetallePedido ADD CONSTRAINT CK_PruebaFallo CHECK (Cantidad <> 7)");
    solicitud = Solicitud(); solicitud.Lineas[0].Cantidad = 7;
    var antes = await db.Pedidos.CountAsync(); var fallo = false;
    try { await pedidos.RegistrarAsync(solicitud, usuarioId); } catch (DbUpdateException) { fallo = true; }
    db.ChangeTracker.Clear();
    Verificar(fallo && await db.Pedidos.CountAsync() == antes, "Fallo de detalle revierte cabecera y todos los renglones");
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE tb_DetallePedido DROP CONSTRAINT CK_PruebaFallo");
    solicitud = Solicitud(); solicitud.Lineas[0].Cantidad = 1000;
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Cantidad de más de 3 dígitos rechazada");
    solicitud = Solicitud(); solicitud.Lineas[0].Cantidad = 27;
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Pedido de más de ₡40.000 rechazado");
    solicitud = Solicitud(); solicitud.Lineas[0].Cantidad = 26;
    resultado = await pedidos.RegistrarAsync(solicitud, usuarioId);
    Verificar(resultado.Total == 39_700, "Pedido por encima de ₡25.000 pero dentro de ₡40.000 se registra");
    solicitud = Solicitud(); solicitud.TipoComida = "Desayuno";
    await Rechazar(async () => { await pedidos.RegistrarAsync(solicitud, usuarioId); }, "Tipo de comida sin artículos en el catálogo rechazado");
    reloj.Hora = new DateTimeOffset(2026, 9, 20, 9, 0, 0, TimeSpan.Zero);
    resultado = await pedidos.RegistrarAsync(Solicitud(), usuarioId);
    Verificar(resultado.Total == 3700, "Se puede registrar de madrugada sin restricción horaria");

    // Regresión: no debe existir un límite de dos tamaños por producto en pedidos.
    var pequenoTres = new Tamano { NombreTamano = "Pequeño" };
    var medianoTres = new Tamano { NombreTamano = "Mediano" };
    var productoTres = new Producto { NombreProducto = "Producto con tres tamaños", IdCategoria = categoriaId, RequiereTamano = true };
    var variantes = new[]
    {
        new Precio { Producto = productoTres, Tamano = pequenoTres, MontoPrecio = 350, FechaVigenciaDesde = new(2026, 1, 1) },
        new Precio { Producto = productoTres, Tamano = medianoTres, MontoPrecio = 500, FechaVigenciaDesde = new(2026, 1, 1) },
        new Precio { Producto = productoTres, IdTamano = tamanoId, MontoPrecio = 700, FechaVigenciaDesde = new(2026, 1, 1) }
    };
    db.Precios.AddRange(variantes); await db.SaveChangesAsync();
    var opcionesTres = (await pedidos.CatalogoAsync()).Where(a => a.Nombre == productoTres.NombreProducto).ToList();
    Verificar(opcionesTres.Count == 3 && opcionesTres.Select(a => a.Tamano).ToHashSet().SetEquals(["Pequeño", "Mediano", "Grande"]),
        "Los tres tamaños activos con precio vigente se ofrecen simultáneamente");
    var solicitudTres = new RegistrarPedidoRequest
    {
        TokenRegistro = Guid.NewGuid(), CodigoColaborador = "DEMO001", TipoComida = "Almuerzos",
        Lineas = opcionesTres.Select(a => new LineaPedidoRequest { Clave = a.Clave, Cantidad = 1, PrecioMostrado = a.Precio }).ToList()
    };
    var compraTres = await pedidos.RegistrarAsync(solicitudTres, usuarioId);
    Verificar(compraTres.Total == 1550 && await db.DetallesPedido.CountAsync(d => d.IdPedido == compraTres.IdPedido) == 3,
        "Se pueden registrar los tres tamaños en un mismo pedido con el tipo de comida tomado de la categoría");
    await db.Tamanos.Where(t => t.IdTamano == medianoTres.IdTamano).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, false));
    Verificar((await pedidos.CatalogoAsync()).Count(a => a.Nombre == productoTres.NombreProducto) == 2,
        "Solo se oculta el tamaño desactivado y se mantienen los otros dos");
    await db.Tamanos.Where(t => t.IdTamano == medianoTres.IdTamano).ExecuteUpdateAsync(s => s.SetProperty(t => t.Activo, true));

    // Pruebas HTTP reales: autenticación, permisos, antifalsificación y model binding.
    var puertoLibre = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
    puertoLibre.Start();
    var direccion = "http://127.0.0.1:" + ((IPEndPoint)puertoLibre.LocalEndpoint).Port;
    puertoLibre.Stop();
    var inicio = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
    {
        WorkingDirectory = Path.GetDirectoryName(archivo)!, RedirectStandardOutput = true, RedirectStandardError = true
    };
    inicio.ArgumentList.Add(Path.Combine(inicio.WorkingDirectory, "bin/Debug/net10.0/SAPS.Web.dll"));
    inicio.ArgumentList.Add("--urls"); inicio.ArgumentList.Add(direccion);
    inicio.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    inicio.Environment["DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE"] = "false";
    inicio.Environment["DOTNET_USE_POLLING_FILE_WATCHER"] = "1";
    inicio.Environment["ConnectionStrings__DefaultConnection"] = conexion.ConnectionString;
    app = Process.Start(inicio)!;
    var salida = new System.Text.StringBuilder();
    app.OutputDataReceived += (_, e) => { lock (salida) salida.AppendLine(e.Data); };
    app.ErrorDataReceived += (_, e) => { lock (salida) salida.AppendLine(e.Data); };
    app.BeginOutputReadLine(); app.BeginErrorReadLine();
    using var cliente = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(direccion) };
    var listo = false;
    for (var i = 0; i < 100; i++)
    {
        if (app.HasExited) throw new Exception("La aplicación de prueba no inició: " + salida);
        try { using var respuesta = await cliente.GetAsync("/"); listo = true; break; } catch (HttpRequestException) { await Task.Delay(200); }
    }
    if (!listo) throw new Exception("La aplicación de prueba no respondió: " + salida);
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    async Task Login(string nombre)
    {
        var pagina = await cliente.GetStringAsync("/Identity/Account/Login");
        using var respuesta = await cliente.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(pagina), ["Input.CodigoEmpleado"] = nombre,
            ["Input.Password"] = "Prueba123", ["Input.RememberMe"] = "false"
        }));
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect, "Inicio de sesión HTTP: " + nombre);
    }
    using (var respuesta = await cliente.GetAsync("/Pedidos"))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect, "Sin sesión no se accede a pedidos");
    await Login("SODA001");
    var pantalla = await cliente.GetStringAsync("/Pedidos");
    var anti = Token(pantalla);
    Verificar(pantalla.Contains("DEMO001") && pantalla.Contains("Registrar pedido"), "La vista se renderiza para operadora de soda");
    using (var respuesta = await cliente.GetAsync("/Administracion/Catalogo"))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect || respuesta.StatusCode == HttpStatusCode.Forbidden, "La operadora no puede modificar el catálogo");
    using (var respuesta = await cliente.PostAsJsonAsync("/Pedidos/Registrar", Solicitud()))
        Verificar(respuesta.StatusCode == HttpStatusCode.BadRequest, "POST sin token antifalsificación rechazado");
    async Task<HttpResponseMessage> Enviar(string json)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/Pedidos/Registrar") { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        peticion.Headers.Add("RequestVerificationToken", anti);
        return await cliente.SendAsync(peticion);
    }
    solicitud = Solicitud();
    var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var texto = JsonSerializer.Serialize(solicitud, opciones);
    using (var respuesta = await Enviar(texto.Replace("\"cantidad\":2", "\"cantidad\":1.5")))
        Verificar(respuesta.StatusCode == HttpStatusCode.BadRequest, "Cantidad decimal rechazada por HTTP");
    using (var respuesta = await Enviar(texto.Replace("\"cantidad\":2", "\"cantidad\":\"abc\"")))
        Verificar(respuesta.StatusCode == HttpStatusCode.BadRequest, "Cantidad no numérica rechazada por HTTP");
    using (var respuesta = await Enviar(texto))
        Verificar(respuesta.StatusCode == HttpStatusCode.OK, "Registro completo mediante HTTP autenticado");
    var filas = await db.Pedidos.CountAsync();
    using (var respuesta = await Enviar(texto))
        Verificar(respuesta.StatusCode == HttpStatusCode.OK && await db.Pedidos.CountAsync() == filas, "Reintento HTTP no duplica el pedido");
    filas = await db.Pedidos.CountAsync();
    var simultaneo = JsonSerializer.Serialize(Solicitud(), opciones);
    var respuestas = await Task.WhenAll(Enviar(simultaneo), Enviar(simultaneo));
    foreach (var respuesta in respuestas) respuesta.Dispose();
    using (var respuesta = await Enviar(simultaneo))
        Verificar(respuesta.StatusCode == HttpStatusCode.OK && await db.Pedidos.CountAsync() == filas + 1, "Solicitudes simultáneas y reintento producen una sola compra");
    // Un usuario sin rol y RH deben quedar fuera aunque invoquen la URL directamente.
    var um = alcance.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var rh = new IdentityUser { UserName = "RHPRUEBA" };
    var creacionRh = await um.CreateAsync(rh, "Prueba123");
    if (!creacionRh.Succeeded) throw new Exception(string.Join(", ", creacionRh.Errors.Select(e => e.Description)));
    await um.AddToRoleAsync(rh, "RecursosHumanos");
    await Login("RHPRUEBA");
    using (var respuesta = await cliente.GetAsync("/Pedidos"))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect || respuesta.StatusCode == HttpStatusCode.Forbidden, "Recursos Humanos no puede registrar pedidos");
    await Login("EMP001");
    using (var respuesta = await cliente.GetAsync("/Pedidos"))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect || respuesta.StatusCode == HttpStatusCode.Forbidden, "Usuario sin rol no puede registrar pedidos");
    await Login("ADM001");
    using (var respuesta = await cliente.GetAsync("/Pedidos"))
        Verificar(respuesta.StatusCode == HttpStatusCode.OK, "Administrador puede registrar pedidos");
    await Login("SODA001");
    pantalla = await cliente.GetStringAsync("/Pedidos"); anti = Token(pantalla);
    filas = await db.Pedidos.CountAsync();
    await cliente.GetStringAsync("/Pedidos/Colaborador?codigo=DEMO001");
    await cliente.GetStringAsync("/Pedidos/Catalogo");
    using (var respuesta = await cliente.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = anti })))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect, "Cerrar sesión funciona por HTTP");
    using (var respuesta = await Enviar(JsonSerializer.Serialize(Solicitud(), opciones)))
        Verificar(respuesta.StatusCode == HttpStatusCode.Redirect && await db.Pedidos.CountAsync() == filas, "Consultar y cerrar sesión sin registrar no guarda pedidos");
    Console.WriteLine($"RESULTADO: {verificaciones} verificaciones correctas.");
    if (args.Length == 2 && args[1] == "--browser")
    {
        Console.WriteLine("PRUEBA VISUAL: " + direccion + "/Pedidos");
        Console.WriteLine("Presione Enter al terminar para detener la aplicación y eliminar únicamente la base temporal.");
        Console.ReadLine();
    }
}
finally
{
    if (app is not null && !app.HasExited) { app.Kill(entireProcessTree: true); await app.WaitForExitAsync(); }
    var destinoLimpieza = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
    if (destinoLimpieza.InitialCatalog != conexion.InitialCatalog || !destinoLimpieza.InitialCatalog.StartsWith("SAPS_HU008_Pruebas_"))
        throw new InvalidOperationException("Limpieza cancelada: la base no es la temporal creada por esta ejecución.");
    await db.Database.EnsureDeletedAsync();
    Console.WriteLine("Base temporal de pruebas eliminada.");
}

sealed class RelojPrueba : TimeProvider
{
    public DateTimeOffset Hora { get; set; } = new(2026, 9, 19, 23, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Hora;
}
