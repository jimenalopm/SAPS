// Data/DbInitializer.cs
using Microsoft.AspNetCore.Identity;

namespace SAPS.Web.Data;

public class DbInitializer(RoleManager<IdentityRole> roleManager)
{
    private static readonly string[] Roles = ["Administrador", "RecursosHumanos", "Soda", "Usuario"];

    public async Task SeedRolesAsync()
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}