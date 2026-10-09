using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;

namespace SAPS.Web.Areas.Identity.Pages.Account;

/// <summary>
/// HU-001, criterio 6: cambio de contraseña opcional tras iniciar sesión (criterio 7: también
/// se usa en modo obligatorio cuando la contraseña venció, ver VencimientoContrasenaMiddleware).
/// </summary>
[Authorize]
public class CambiarContrasenaModel(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ApplicationDbContext context,
    TimeProvider reloj,
    ILogger<CambiarContrasenaModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Query string (no TempData): debe sobrevivir a un return Page() tras un error de validación.
    [BindProperty(SupportsGet = true)]
    public bool Obligatorio { get; set; }

    [TempData]
    public string? Mensaje { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Ingresa tu contraseña actual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string ContrasenaActual { get; set; } = "";

        [Required(ErrorMessage = "Ingresa tu contraseña nueva.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña nueva")]
        public string ContrasenaNueva { get; set; } = "";

        [Required(ErrorMessage = "Confirma tu contraseña nueva.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña nueva")]
        [Compare(nameof(ContrasenaNueva), ErrorMessage = "Las contraseñas no coinciden.")]
        public string ConfirmarContrasenaNueva { get; set; } = "";
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var resultado = await userManager.ChangePasswordAsync(user, Input.ContrasenaActual, Input.ContrasenaNueva);
        if (!resultado.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                "La contraseña actual no es correcta, o la nueva no cumple la política de seguridad (alfanumérica, mínimo 6 caracteres).");
            return Page();
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var estado = await context.EstadosContrasena.FindAsync(user.Id);
        if (estado is null)
        {
            context.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = user.Id, FechaUltimoCambio = ahora });
        }
        else
        {
            estado.FechaUltimoCambio = ahora;
        }
        await context.SaveChangesAsync();

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation("User changed their password.");

        if (Obligatorio) return LocalRedirect("~/");

        Mensaje = "Contraseña actualizada correctamente.";
        return RedirectToPage();
    }
}
