// Controllers/AdministracionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "Administrador")]
public class AdministracionController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}