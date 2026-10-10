using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Cierre de sesión. La lógica es la misma de la página por defecto de Identity
    /// (cerrar sesión y volver a returnUrl o a esta misma página); solo cambia la presentación.
    /// Es [AllowAnonymous] como la original: tras cerrar sesión se vuelve aquí ya sin sesión.
    /// El botón "Cerrar sesión" del menú lateral envía un POST directo a esta página.
    /// </summary>
    [AllowAnonymous]
    public class LogoutModel(SignInManager<IdentityUser> signInManager, ILogger<LogoutModel> logger) : PageModel
    {
        public async Task<IActionResult> OnPost(string? returnUrl = null)
        {
            await signInManager.SignOutAsync();
            logger.LogInformation("User logged out.");
            if (returnUrl != null)
            {
                return LocalRedirect(returnUrl);
            }
            else
            {
                // Se vuelve a esta misma página, que ya sin sesión confirma el cierre.
                return RedirectToPage();
            }
        }
    }
}
