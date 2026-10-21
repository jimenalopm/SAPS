using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Middleware;
using SAPS.Web.Services.Auditoria;
using SAPS.Web.Services.Pedidos;
using SAPS.Web.Services.Seguridad;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
// Los datos de prueba (usuarios EMP001/SODA001/ADM001, colaboradores DEMO) solo se siembran en
// Development Y contra un servidor local. Cualquier otra base (p. ej. la de RecyPlast) se considera real.
var sembrarPrueba = SembradoPrueba.Permitido(builder.Environment, connectionString);
// El servidor de RecyPlast usa SQL Server 2012 (nivel de compatibilidad 110). Sin esto,
// EF Core 8+ traduce consultas como lista.Contains(x) con OPENJSON, que no existe en 2012.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.UseCompatibilityLevel(110)));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(IdentityConfig.Configurar)
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<IdentityErrorDescriberEs>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// HU-001 (criterio 9): la sesión no debe vencer por inactividad. El default de Identity
// (14 días deslizantes) es corto para este uso; se fija en 1 año deslizante.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(365);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // En Development se admite http://localhost:5120; fuera de él la cookie nunca viaja sin HTTPS.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
// Límite de peticiones: inicio de sesión por IP y pedidos por usuario (ver LimitesPeticiones).
builder.Services.AddRateLimiter(LimitesPeticiones.Configurar);

builder.Services.AddScoped<DbInitializer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ServicioPedidos>();
// HU-004: servicio central de bitácora (logs de actividad y accesos).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IServicioBitacora, ServicioBitacora>();
// HU-015: le pasa a los triggers de precios quién hace el cambio (CONTEXT_INFO).
builder.Services.AddSingleton<IContextoAuditoria, ContextoAuditoria>();
// Los colaboradores salen de rrhh.tb_Colaborador. Los DEMO solo se insertan si sembrarPrueba (ver abajo).
builder.Services.AddSingleton(new EntornoPrueba(sembrarPrueba));
builder.Services.AddScoped<IColaboradores, ColaboradoresTabla>();
builder.Services.AddControllersWithViews(options =>
{
    // Todo POST/PUT/DELETE de los controladores exige token antiforgery, aunque a una acción nueva se le olvide el atributo.
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    // [H9] Mensajes de conversión de datos en español (antes salían en inglés, por ejemplo
    // "The value '1500.5' is not valid for Precio."). ValueMustBeANumber también se usa
    // como mensaje de validación en el navegador (data-val-number).
    var m = options.ModelBindingMessageProvider;
    m.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"El valor «{valor}» no es válido para {campo}.");
    m.SetMissingBindRequiredValueAccessor(campo => $"Falta un valor para {campo}.");
    m.SetMissingKeyOrValueAccessor(() => "El valor es obligatorio.");
    m.SetMissingRequestBodyRequiredValueAccessor(() => "La solicitud no contiene datos.");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"El valor «{valor}» no es válido.");
    m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "El valor ingresado no es válido.");
    m.SetNonPropertyValueMustBeANumberAccessor(() => "El valor debe ser un número.");
    m.SetUnknownValueIsInvalidAccessor(campo => $"El valor ingresado no es válido para {campo}.");
    m.SetValueIsInvalidAccessor(valor => $"El valor «{valor}» no es válido.");
    m.SetValueMustBeANumberAccessor(campo => $"El campo {campo} debe ser un número entero.");
    m.SetValueMustNotBeNullAccessor(campo => $"El campo {campo} es obligatorio.");
});

// Si SAPS corre detrás de un proxy inverso, sus IP se declaran en Seguridad:ProxiesConfiables (appsettings). Así el esquema
// HTTPS y la IP real de cada persona llegan bien (cookie Secure, límite de peticiones y bitácora). Sin lista, no se confía en ninguno.
var proxiesConfiables = builder.Configuration.GetSection("Seguridad:ProxiesConfiables").Get<string[]>() ?? [];
if (proxiesConfiables.Length > 0)
{
    builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var ip in proxiesConfiables) options.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
    });
}

var app = builder.Build();

// Los roles deben existir ANTES de poder asignárselos a un usuario de prueba.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.SeedRolesAsync();
}

if (!sembrarPrueba)
{
    if (app.Environment.IsDevelopment())
        app.Logger.LogWarning("Sembrado de datos de prueba omitido: la base no es local.");
}
else
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    var testUser = await userManager.FindByNameAsync("EMP001");
    if (testUser == null)
    {
        var user = new IdentityUser { UserName = "EMP001" };
        await userManager.CreateAsync(user, "Prueba123");
    }

    // Operadora ficticia: no es el colaborador al que se carga el pedido.
    var sodaUser = await userManager.FindByNameAsync("SODA001");
    if (sodaUser == null)
    {
        sodaUser = new IdentityUser { UserName = "SODA001" };
        var resultado = await userManager.CreateAsync(sodaUser, "Prueba123");
        if (!resultado.Succeeded) throw new InvalidOperationException("No se pudo crear la operadora de prueba.");
    }
    if (!await userManager.IsInRoleAsync(sodaUser, "Soda"))
    {
        var resultado = await userManager.AddToRoleAsync(sodaUser, "Soda");
        if (!resultado.Succeeded) throw new InvalidOperationException("No se pudo asignar el rol de la operadora de prueba.");
    }

    // Usuario de prueba SOLO para desarrollo, con rol Administrador, para poder
    // probar el CRUD de catálogo (HU-007) desde /Administracion/Catalogo.
    var adminUser = await userManager.FindByNameAsync("ADM001");
    if (adminUser == null)
    {
        adminUser = new IdentityUser { UserName = "ADM001" };
        await userManager.CreateAsync(adminUser, "Prueba123");
    }
    if (!await userManager.IsInRoleAsync(adminUser, "Administrador"))
    {
        await userManager.AddToRoleAsync(adminUser, "Administrador");
    }

    // Colaboradores ficticios DEMO001-DEMO003: viven en tb_Colaborador, pero solo se insertan aquí.
    await SembradoPrueba.SembrarColaboradoresAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (proxiesConfiables.Length > 0) app.UseForwardedHeaders();
app.UseMiddleware<EncabezadosSeguridadMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// HU-001 (criterio 7): bloquea el acceso a toda la app si la contraseña venció.
app.UseMiddleware<VencimientoContrasenaMiddleware>();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();