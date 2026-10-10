using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Sustituye a la página de registro que trae Identity por defecto.
    /// SAPS no permite que las personas se registren solas: las cuentas y sus roles las administra
    /// la empresa. Esta página existe únicamente para que /Identity/Account/Register responda 404
    /// y no se pueda crear ninguna cuenta (ni por GET ni por POST).
    /// </summary>
    public class RegisterModel : PageModel
    {
        public IActionResult OnGet() => NotFound();

        public IActionResult OnPost() => NotFound();
    }
}
