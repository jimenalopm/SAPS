using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// SAPS no tiene recuperación de contraseña por correo: no hay servicio de envío de correos configurado
    /// y las cuentas son códigos de empleado. La página por defecto de Identity aparentaba enviar un mensaje
    /// que nunca salía. Esta la sustituye por un aviso de "no disponible"; el POST no hace nada.
    /// </summary>
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        public void OnGet()
        {
        }

        public IActionResult OnPost() => NotFound();
    }
}
