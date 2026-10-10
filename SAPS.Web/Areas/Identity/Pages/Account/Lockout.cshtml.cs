using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Pantalla que ve la persona cuya cuenta se bloqueó por intentos fallidos (la decisión de bloquear
    /// la toma Login.cshtml.cs / IdentityConfig; aquí solo se informa). Sustituye a la página por defecto.
    /// </summary>
    [AllowAnonymous]
    public class LockoutModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
