using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Services.Pedidos;

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
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<DbInitializer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ServicioPedidos>();
// Los datos ficticios nunca se habilitan fuera de Development ni contra una base no local.
if (sembrarPrueba)
    builder.Services.AddSingleton<IColaboradores, ColaboradoresDePrueba>();
else
    builder.Services.AddSingleton<IColaboradores, ColaboradoresSinConexion>();
builder.Services.AddControllersWithViews(options =>
{
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

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();