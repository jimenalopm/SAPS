using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<DbInitializer>();
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();