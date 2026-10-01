using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Services.Pedidos;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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
// Los datos ficticios nunca se habilitan automáticamente fuera de Development.
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IColaboradores, ColaboradoresDePrueba>();
else
    builder.Services.AddSingleton<IColaboradores, ColaboradoresSinConexion>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Los roles deben existir ANTES de poder asignárselos a un usuario de prueba.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.SeedRolesAsync();
}

if (app.Environment.IsDevelopment())
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