using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Página a la que Identity envía a quien está autenticado pero no tiene permiso para una sección.
    /// Sustituye a la página por defecto (en inglés). Solo informa: no cambia ninguna regla de autorización.
    /// </summary>
    public class AccessDeniedModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
