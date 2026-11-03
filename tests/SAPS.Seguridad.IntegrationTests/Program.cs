using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Web.Data;
using SAPS.Web.Models.Catalogo;

// Pruebas de extremo a extremo del endurecimiento de seguridad (encabezados, antiforgery, mensaje del login,
// límite de peticiones, concurrencia y bitácora de pedidos). Levanta la aplicación REAL contra una base propia en SQL Server
// local, la maneja por HTTP como lo haría un navegador y al final elimina la base. No toca SAPS_DB.
//
// Uso (con SAPS.Web ya compilado en Debug):
//   dotnet run --project tests/SAPS.Seguridad.IntegrationTests -- SAPS.Web/appsettings.json
// La contraseña de sa se toma de la cadena de conexión: appsettings.json o, si es un marcador, user-secrets del proyecto web.
if (args.Length != 1) throw new ArgumentException("Indique la ruta de appsettings.json del entorno local.");
var webDir = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
var cadenaBase = await LeerCadenaAsync(args[0], webDir);
var conexion = new SqlConnectionStringBuilder(cadenaBase);
if (conexion.DataSource is not ("localhost,1433" or "127.0.0.1,1433"))
    throw new InvalidOperationException("Estas pruebas solo admiten SQL Server local en el puerto 1433.");
conexion.InitialCatalog = "SAPS_Seguridad_Pruebas_" + Guid.NewGuid().ToString("N");

var dll = Path.Combine(webDir, "bin", "Debug", "net10.0", "SAPS.Web.dll");
if (!File.Exists(dll)) throw new FileNotFoundException("Compile SAPS.Web en Debug antes de correr estas pruebas.", dll);

var verificaciones = 0;
void Verificar(bool condicion, string caso)
{
    if (!condicion) throw new Exception("FALLO: " + caso);
    verificaciones++;
    Console.WriteLine("OK: " + caso);
}

// Mismo registro que Program.cs (Identity + compatibilidad 110): si no, EF ve un modelo distinto al de las migraciones.
var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(conexion.ConnectionString, sql => sql.UseCompatibilityLevel(110)));
servicios.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
await using var proveedor = servicios.BuildServiceProvider();
ApplicationDbContext NuevoContexto() => proveedor.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
Process? app = null;
try
{
    await using (var db = NuevoContexto())
    {
        await db.Database.MigrateAsync();
        var almuerzo = new Categoria { NombreCategoria = "Almuerzo" };
        db.Precios.Add(new Precio { Producto = new Producto { NombreProducto = "Casado", Categoria = almuerzo }, MontoPrecio = 2800, FechaVigenciaDesde = new DateOnly(2026, 1, 1) });
        db.Bebidas.Add(new Bebida { NombreBebida = "Naranjada", TipoBebida = "Frescos", Precio = 800 });
        await db.SaveChangesAsync();
    }

    var puerto = PuertoLibre();
    var url = $"http://127.0.0.1:{puerto}";
    app = Process.Start(new ProcessStartInfo("dotnet", $"\"{dll}\"")
    {
        WorkingDirectory = webDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        Environment =
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_URLS"] = url,
            ["ConnectionStrings__DefaultConnection"] = conexion.ConnectionString,
        },
    })!;
    var salida = new StringBuilder();
    app.OutputDataReceived += (_, e) => salida.AppendLine(e.Data);
    app.ErrorDataReceived += (_, e) => salida.AppendLine(e.Data);
    app.BeginOutputReadLine();
    app.BeginErrorReadLine();

    HttpClient Cliente(out CookieContainer cookies)
    {
        cookies = new CookieContainer();
        return new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { BaseAddress = new Uri(url) };
    }

    var admin = Cliente(out _);
    var anonimo = Cliente(out _);

    var listo = false;
    for (var i = 0; i < 60 && !listo && !app.HasExited; i++)
    {
        try { listo = (await anonimo.GetAsync("/Identity/Account/Login")).IsSuccessStatusCode; }
        catch (HttpRequestException) { await Task.Delay(1000); }
    }
    if (!listo) throw new Exception("La aplicación no arrancó:\n" + salida);

    // ---------- 1. Encabezados de seguridad y nonce de la CSP ----------
    var paginaLogin = await anonimo.GetAsync("/Identity/Account/Login");
    var htmlLogin = await paginaLogin.Content.ReadAsStringAsync();
    var csp = paginaLogin.Headers.GetValues("Content-Security-Policy").Single();
    var nonce = Regex.Match(csp, "script-src 'self' 'nonce-([^']+)'").Groups[1].Value;
    Verificar(nonce.Length > 10, "la CSP trae script-src propio con nonce");
    Verificar(!csp.Contains("script-src 'self' 'unsafe-inline'") && csp.Contains("frame-ancestors 'none'"), "la CSP no permite scripts en línea sin nonce y bloquea marcos");
    var scriptsEnLinea = Regex.Matches(htmlLogin, @"<script(?![^>]*\bsrc=)[^>]*>").Select(m => m.Value).ToList();
    Verificar(scriptsEnLinea.Count > 0 && scriptsEnLinea.All(s => s.Contains($"nonce=\"{nonce}\"")), "todos los scripts en línea del login llevan el nonce de la solicitud");
    Verificar(paginaLogin.Headers.GetValues("X-Frame-Options").Single() == "DENY"
              && paginaLogin.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff"
              && paginaLogin.Headers.Contains("Referrer-Policy"), "X-Frame-Options, nosniff y Referrer-Policy presentes");
    // El servidor solo emite la cookie antiforgery a quien aún no la tiene: se pide con un cliente nuevo.
    var primeraVisita = await Cliente(out _).GetAsync("/Identity/Account/Login");
    var cookieAntiforgery = primeraVisita.Headers.GetValues("Set-Cookie").First(c => c.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
    Verificar(cookieAntiforgery.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) && cookieAntiforgery.Contains("httponly", StringComparison.OrdinalIgnoreCase),
        "la cookie antiforgery es HttpOnly y SameSite=Strict");
    var nonce2 = WebUtility.HtmlDecode(Regex.Match((await (await anonimo.GetAsync("/Identity/Account/Login")).Content.ReadAsStringAsync()), @"<script[^>]*nonce=""([^""]+)""").Groups[1].Value);
    Verificar(nonce2.Length > 10 && nonce2 != nonce, "el nonce cambia en cada solicitud");

    // ---------- 2. Inicio de sesión del administrador (intento de login 1) ----------
    async Task<HttpResponseMessage> Login(HttpClient c, string usuario, string clave)
    {
        var token = Token(await (await c.GetAsync("/Identity/Account/Login")).Content.ReadAsStringAsync());
        return await c.PostAsync("/Identity/Account/Login", Formulario(("Input.CodigoEmpleado", usuario), ("Input.Password", clave), ("__RequestVerificationToken", token)));
    }
    var entrada = await Login(admin, "ADM001", "Prueba123");
    Verificar(entrada.StatusCode == HttpStatusCode.Redirect, "ADM001 inicia sesión con la contraseña de prueba");

    // ---------- 3. Antiforgery global: un POST sin token se rechaza ----------
    var sinToken = await admin.PostAsync("/Administracion/CambiarEstadoBebida/1", Formulario());
    Verificar(sinToken.StatusCode == HttpStatusCode.BadRequest, "un POST de controlador sin token antiforgery responde 400");

    // ---------- 4. Concurrencia: la segunda edición con versión vieja se rechaza ----------
    int idBebida;
    await using (var db = NuevoContexto()) idBebida = await db.Bebidas.Select(b => b.IdBebida).FirstAsync();
    async Task<Dictionary<string, string>> AbrirEdicion() =>
        Campos(await (await admin.GetAsync($"/Administracion/EditarBebida/{idBebida}")).Content.ReadAsStringAsync(), "EditarBebida");
    var a = await AbrirEdicion();
    var b = await AbrirEdicion();
    Verificar(a.TryGetValue("RowVersion", out var rv) && rv.Length > 4, "el formulario de edición lleva la versión de la fila");
    a["Precio"] = "900";
    b["Precio"] = "950";
    var guardaA = await admin.PostAsync($"/Administracion/EditarBebida/{idBebida}", FormularioDe(a));
    var guardaB = await admin.PostAsync($"/Administracion/EditarBebida/{idBebida}", FormularioDe(b));
    Verificar(guardaA.StatusCode == HttpStatusCode.Redirect && guardaB.StatusCode == HttpStatusCode.Redirect, "ambas ediciones responden con redirección al catálogo");
    var avisoB = WebUtility.HtmlDecode(await (await admin.GetAsync(guardaB.Headers.Location!.ToString())).Content.ReadAsStringAsync());
    Verificar(avisoB.Contains("Otra persona modificó"), "la edición con versión vieja muestra el aviso de conflicto");
    await using (var db = NuevoContexto())
    {
        Verificar(await db.Bebidas.AsNoTracking().Where(x => x.IdBebida == idBebida).Select(x => x.Precio).SingleAsync() == 900, "en la base quedó el precio de la primera edición (900), no el de la vieja (950)");
        Verificar(await db.Bitacoras.AnyAsync(x => x.TipoEvento == "CambioPrecio" && x.ValorNuevo == "900"), "el cambio de precio quedó en la bitácora");
    }

    // ---------- 5. Pedido registrado y rechazado quedan en la bitácora ----------
    var paginaPedidos = await (await admin.GetAsync("/Pedidos")).Content.ReadAsStringAsync();
    var tokenPedidos = Token(paginaPedidos);
    var catalogo = JsonDocument.Parse(await (await admin.GetAsync("/Pedidos/Catalogo")).Content.ReadAsStringAsync()).RootElement;
    var casado = catalogo.EnumerateArray().First(x => x.GetProperty("nombre").GetString() == "Casado");
    async Task<HttpResponseMessage> Registrar(string codigo, int precio)
    {
        var cuerpo = JsonSerializer.Serialize(new
        {
            tokenRegistro = Guid.NewGuid(), codigoColaborador = codigo, tipoComida = "Almuerzo", observaciones = (string?)null,
            lineas = new[] { new { clave = casado.GetProperty("clave").GetString(), cantidad = 1, precioMostrado = precio } },
        });
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/Pedidos/Registrar") { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        peticion.Headers.Add("RequestVerificationToken", tokenPedidos);
        return await admin.SendAsync(peticion);
    }
    Verificar((await Registrar("DEMO001", 2800)).StatusCode == HttpStatusCode.OK, "el pedido de DEMO001 se registra");
    Verificar((await Registrar("DEMO001", 2000)).StatusCode == HttpStatusCode.BadRequest, "un pedido con el precio cambiado se rechaza");
    await using (var db = NuevoContexto())
    {
        var eventos = await db.Bitacoras.AsNoTracking().Where(x => x.Modulo == "Pedidos").ToListAsync();
        Verificar(eventos.Count(x => x.TipoEvento == "PedidoRegistrado" && x.EsExitoso && x.Descripcion!.Contains("2800 colones")) == 1, "la bitácora tiene el pedido registrado, con el monto");
        Verificar(eventos.Count(x => x.TipoEvento == "PedidoRechazado" && !x.EsExitoso) == 1, "la bitácora tiene el pedido rechazado");
    }

    // ---------- 6. El login no delata cuentas desactivadas (intentos de login 2, 3 y 4) ----------
    string idEmp;
    await using (var db = NuevoContexto()) idEmp = await db.Users.Where(u => u.UserName == "EMP001").Select(u => u.Id).SingleAsync();
    var tokenUsuarios = Token(await (await admin.GetAsync("/Usuarios")).Content.ReadAsStringAsync());
    var desactivar = await admin.PostAsync("/Usuarios/Desactivar", Formulario(("id", idEmp), ("__RequestVerificationToken", tokenUsuarios)));
    Verificar(desactivar.StatusCode == HttpStatusCode.Redirect, "el administrador desactiva la cuenta EMP001");
    const string Generico = "El correo de empleado o la contraseña son incorrectos";
    var visitante = Cliente(out _);
    var htmlDesactivada = WebUtility.HtmlDecode(await (await Login(visitante, "EMP001", "ClaveEquivocada1")).Content.ReadAsStringAsync());
    var htmlInexistente = WebUtility.HtmlDecode(await (await Login(visitante, "NOEXISTE", "ClaveEquivocada1")).Content.ReadAsStringAsync());
    Verificar(htmlDesactivada.Contains(Generico) && htmlInexistente.Contains(Generico), "cuenta desactivada y cuenta inexistente con contraseña incorrecta dan el mismo mensaje");
    Verificar(!htmlDesactivada.Contains("fue desactivada"), "la cuenta desactivada no se delata sin la contraseña correcta");
    var htmlCorrecta = WebUtility.HtmlDecode(await (await Login(visitante, "EMP001", "Prueba123")).Content.ReadAsStringAsync());
    Verificar(htmlCorrecta.Contains("fue desactivada"), "con la contraseña correcta sí se explica que la cuenta fue desactivada");

    // ---------- 7. Límite de peticiones del login: 10 por minuto por IP ----------
    // Ya se enviaron 4 envíos: ADM001, EMP001 con clave incorrecta, NOEXISTE y EMP001 con clave correcta.
    HttpStatusCode? primerRechazo = null;
    var enviados = 4;
    for (var i = 0; i < 12 && primerRechazo is null; i++)
    {
        var token = Token(await (await visitante.GetAsync("/Identity/Account/Login")).Content.ReadAsStringAsync());
        var r = await visitante.PostAsync("/Identity/Account/Login", Formulario(("Input.CodigoEmpleado", "NOEXISTE"), ("Input.Password", "ClaveEquivocada1"), ("__RequestVerificationToken", token)));
        enviados++;
        if (r.StatusCode == HttpStatusCode.TooManyRequests) primerRechazo = r.StatusCode;
    }
    Verificar(primerRechazo is not null && enviados is >= 10 and <= 12, $"el envío del login se rechaza con 429 al superar 10 por minuto (rechazo en el envío {enviados})");
    Verificar((await visitante.GetAsync("/Identity/Account/Login")).IsSuccessStatusCode, "abrir la página de login sigue funcionando tras el límite");

    Console.WriteLine($"\n{verificaciones} verificaciones correctas.");
}
finally
{
    try { if (app is { HasExited: false }) { app.Kill(entireProcessTree: true); await app.WaitForExitAsync(); } } catch { /* ya terminó */ }
    await using var db = NuevoContexto();
    await db.Database.EnsureDeletedAsync();
}

static int PuertoLibre()
{
    var l = new TcpListener(IPAddress.Loopback, 0);
    l.Start();
    var puerto = ((IPEndPoint)l.LocalEndpoint).Port;
    l.Stop();
    return puerto;
}

static async Task<string> LeerCadenaAsync(string appsettings, string webDir)
{
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync(appsettings));
    var cadena = config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()!;
    if (!cadena.Contains("CONFIGURAR_EN_USER_SECRETS")) return cadena;
    var psi = new ProcessStartInfo("dotnet", "user-secrets list") { WorkingDirectory = webDir, RedirectStandardOutput = true };
    using var p = Process.Start(psi)!;
    var texto = await p.StandardOutput.ReadToEndAsync();
    await p.WaitForExitAsync();
    var m = Regex.Match(texto, @"ConnectionStrings:DefaultConnection = (.+)");
    return m.Success ? m.Groups[1].Value.Trim() : throw new InvalidOperationException("No hay cadena de conexión en user-secrets.");
}

static string Token(string html) =>
    WebUtility.HtmlDecode(Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""").Groups[1].Value);

static FormUrlEncodedContent Formulario(params (string, string)[] campos) =>
    new(campos.Select(c => new KeyValuePair<string, string>(c.Item1, c.Item2)));

static FormUrlEncodedContent FormularioDe(Dictionary<string, string> campos) => new(campos);

// Lee los campos del formulario cuyo action contiene la acción indicada: inputs, selects y checkboxes marcados.
static Dictionary<string, string> Campos(string html, string accion)
{
    var form = Regex.Match(html, $@"<form[^>]*action=""[^""]*{accion}[^""]*""[^>]*>(.*?)</form>", RegexOptions.Singleline).Groups[1].Value;
    var campos = new Dictionary<string, string>();
    foreach (Match m in Regex.Matches(form, @"<input[^>]*>"))
    {
        string Atributo(string n) => WebUtility.HtmlDecode(Regex.Match(m.Value, $@"\b{n}=""([^""]*)""").Groups[1].Value);
        var nombre = Atributo("name");
        var tipo = Atributo("type");
        if (nombre.Length == 0 || tipo is "submit" or "button") continue;
        if (tipo is "checkbox" or "radio" && !Regex.IsMatch(m.Value, @"\bchecked\b")) continue;
        campos[nombre] = Atributo("value");
    }
    foreach (Match s in Regex.Matches(form, @"<select[^>]*name=""([^""]+)""[^>]*>(.*?)</select>", RegexOptions.Singleline))
    {
        var opcion = Regex.Match(s.Groups[2].Value, @"<option[^>]*\bselected\b[^>]*>") is { Success: true } sel ? sel
            : Regex.Match(s.Groups[2].Value, @"<option[^>]*>");
        campos[s.Groups[1].Value] = WebUtility.HtmlDecode(Regex.Match(opcion.Value, @"value=""([^""]*)""").Groups[1].Value);
    }
    return campos;
}
