using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SAPS.Web.Controllers;

[Authorize(Roles = "RecursosHumanos,Administrador")]
public class RecursosHumanosController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}