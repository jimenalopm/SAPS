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

    // HU-001: distingue "primer ingreso" (contraseña temporal del Administrador) de "venció a los 10 meses".
    [BindProperty(SupportsGet = true)]
    public string? Motivo { get; set; }

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

        // La nueva debe ser distinta de la actual; en el primer ingreso, la "actual" es la temporal del Administrador.
        if (Input.ContrasenaNueva == Input.ContrasenaActual)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.ContrasenaNueva)}",
                "La contraseña nueva debe ser distinta de la actual.");
            return Page();
        }

        var resultado = await userManager.ChangePasswordAsync(user, Input.ContrasenaActual, Input.ContrasenaNueva);
        if (!resultado.Succeeded)
        {
            // La actual incorrecta se muestra en su campo; las reglas de la nueva (mínimo, minúscula, número)
            // en el suyo. Los textos salen en español (IdentityErrorDescriberEs).
            foreach (var error in resultado.Errors)
            {
                var campo = error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)
                    ? nameof(Input.ContrasenaActual)
                    : nameof(Input.ContrasenaNueva);
                ModelState.AddModelError($"{nameof(Input)}.{campo}", error.Description);
            }
            return Page();
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var estado = await context.EstadosContrasena.FindAsync(user.Id);
        if (estado is null)
        {
            context.EstadosContrasena.Add(new EstadoContrasenaUsuario { IdUsuario = user.Id, FechaUltimoCambio = ahora, RequiereCambioInicial = false });
        }
        else
        {
            estado.FechaUltimoCambio = ahora;
            estado.RequiereCambioInicial = false;
        }
        await context.SaveChangesAsync();

        logger.LogInformation("User changed their password.");

        if (Obligatorio)
        {
            // Cambio obligatorio: se cierra la sesión (y con ella cualquier cookie "recordada" abierta
            // con la contraseña temporal) y se pide iniciar sesión con la contraseña nueva.
            await signInManager.SignOutAsync();
            return RedirectToPage("./Login", new { contrasenaActualizada = true });
        }

        await signInManager.RefreshSignInAsync(user);
        Mensaje = "Contraseña actualizada correctamente.";
        return RedirectToPage();
    }
}
