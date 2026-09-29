// Data/IdentityConfig.cs
using Microsoft.AspNetCore.Identity;

namespace SAPS.Web.Data;

/// <summary>
/// Reglas de autenticación de HU-001 / RNF-001. Se separan de Program.cs
/// para poder reutilizarlas (y probarlas) sin levantar la aplicación.
/// </summary>
public static class IdentityConfig
{
    public const int MaxIntentosFallidos = 5;
    public static readonly TimeSpan TiempoBloqueo = TimeSpan.FromMinutes(15);

    public static void Configurar(IdentityOptions options)
    {
        options.SignIn.RequireConfirmedAccount = false;

        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;

        options.Lockout.MaxFailedAccessAttempts = MaxIntentosFallidos;
        options.Lockout.DefaultLockoutTimeSpan = TiempoBloqueo;
    }
}
