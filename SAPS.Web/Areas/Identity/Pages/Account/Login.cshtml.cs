// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SAPS.Web.Data;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<LoginModel> _logger;
        private readonly IServicioBitacora _bitacora;

        // HU-004: la bitácora es opcional para no romper quien construye la página sin ella (pruebas);
        // en la aplicación se registra en Program.cs y siempre llega.
        public LoginModel(SignInManager<IdentityUser> signInManager, ApplicationDbContext db, ILogger<LoginModel> logger,
            IServicioBitacora bitacora = null)
        {
            _bitacora = bitacora;
            _signInManager = signInManager;
            _db = db;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        // HU-001 (RNF-006): motivo por el que el middleware cerró la sesión y redirigió aquí.
        [BindProperty(SupportsGet = true)]
        public bool AccesoRevocado { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool ColaboradorInactivo { get; set; }

        // Se llega con ?bloqueoTemporal=true cuando el middleware cierra la sesión por 5 intentos fallidos.
        [BindProperty(SupportsGet = true)]
        public bool BloqueoTemporal { get; set; }

        // Se llega con ?contrasenaActualizada=true tras el cambio obligatorio de contraseña.
        [BindProperty(SupportsGet = true)]
        public bool ContrasenaActualizada { get; set; }

        private const string MensajeColaboradorInactivo =
            "Su perfil de colaborador está inactivo. Comuníquese con Recursos Humanos o con el administrador.";

        public const string MensajeCuentaDesactivada =
            "Su cuenta fue desactivada por el administrador. Comuníquese con el administrador del sistema.";

        public const string MensajeBloqueoTemporal =
            "Su cuenta está bloqueada temporalmente por demasiados intentos fallidos. Inténtelo de nuevo más tarde o comuníquese con el administrador.";

        public class InputModel
        {
            [Required]
            [Display(Name = "Correo de empleado")]
            public string CodigoEmpleado { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Recordar mi sesión en este equipo")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }
            if (AccesoRevocado)
            {
                ModelState.AddModelError(string.Empty, MensajeCuentaDesactivada);
            }
            if (BloqueoTemporal)
            {
                ModelState.AddModelError(string.Empty, MensajeBloqueoTemporal);
            }
            if (ColaboradorInactivo)
            {
                ModelState.AddModelError(string.Empty, MensajeColaboradorInactivo);
            }

            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // lockoutOnFailure: true -> requerido por RNF-001 (bloqueo tras 5 intentos fallidos)
                var result = await _signInManager.PasswordSignInAsync(Input.CodigoEmpleado, Input.Password, Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    // RNF-006: no se concede el acceso si el colaborador vinculado no está activo.
                    // La cuenta desactivada/bloqueada (LockoutEnd) ya la rechaza PasswordSignInAsync
                    // más abajo (result.IsLockedOut), así que ese caso no se repite aquí.
                    var user = await _signInManager.UserManager.FindByNameAsync(Input.CodigoEmpleado);
                    var colaboradorActivo = user is null
                        ? (bool?)null
                        : await (from v in _db.VinculosUsuarioColaborador
                                 join c in _db.Colaboradores on v.IdColaborador equals c.IdColaborador
                                 where v.IdUsuario == user.Id
                                 select (bool?)c.EstaActivo).FirstOrDefaultAsync();

                    if (colaboradorActivo == false)
                    {
                        await _signInManager.SignOutAsync();
                        await RegistrarInicioSesionAsync(false, "Colaborador inactivo", user);
                        ModelState.AddModelError(string.Empty, MensajeColaboradorInactivo);
                        return Page();
                    }

                    // Con la contraseña temporal no se deja una sesión recordada: solo sirve para
                    // llegar al cambio obligatorio (el middleware no permite nada más hasta hacerlo).
                    var debeCambiarla = user is not null
                        && await _db.EstadosContrasena.AnyAsync(e => e.IdUsuario == user.Id && e.RequiereCambioInicial);
                    if (debeCambiarla && Input.RememberMe)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                    }

                    await RegistrarInicioSesionAsync(true, "Inicio de sesión exitoso", user);
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    // Cuenta desactivada por el administrador (LockoutEnd = MaxValue) ≠ bloqueo temporal
                    // por 5 intentos fallidos: son motivos distintos y llevan mensajes distintos.
                    var bloqueada = await _signInManager.UserManager.FindByNameAsync(Input.CodigoEmpleado);
                    if (bloqueada is not null
                        && await _signInManager.UserManager.GetLockoutEndDateAsync(bloqueada) == DateTimeOffset.MaxValue)
                    {
                        _logger.LogWarning("Login rejected: account deactivated by an administrator.");
                        await RegistrarInicioSesionAsync(false, "Cuenta desactivada por el administrador", bloqueada);
                        ModelState.AddModelError(string.Empty, MensajeCuentaDesactivada);
                        return Page();
                    }

                    _logger.LogWarning("User account locked out.");
                    await RegistrarInicioSesionAsync(false, "Cuenta bloqueada temporalmente", bloqueada);
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // Si la cuenta existe se guarda su id; si no, solo lo digitado. Nunca la contraseña.
                    var cuentaFallida = _bitacora is null
                        ? null
                        : await _signInManager.UserManager.FindByNameAsync(Input.CodigoEmpleado);
                    await RegistrarInicioSesionAsync(false, "Credenciales incorrectas", cuentaFallida);

                    // F-02 (H9): el mensaje nombra el campo real e indica qué hacer
                    ModelState.AddModelError(string.Empty, "El correo de empleado o la contraseña son incorrectos. Revise los datos e inténtelo de nuevo. Si olvidó su contraseña, comuníquese con el administrador.");
                    return Page();
                }
            }

            return Page();
        }

        // HU-004 criterio 1: inicio de sesión exitoso o fallido. El usuario va explícito porque en este
        // punto el HttpContext aún no tiene la sesión. El servicio nunca lanza: el login no se rompe.
        private async Task RegistrarInicioSesionAsync(bool exitoso, string descripcion, IdentityUser cuenta)
        {
            if (_bitacora is null) return;
            var rol = cuenta is null ? null : (await _signInManager.UserManager.GetRolesAsync(cuenta)).FirstOrDefault();
            await _bitacora.RegistrarAsync(new EventoBitacora(
                ModulosBitacora.Seguridad, TiposEventoBitacora.InicioSesion, RecursosBitacora.Sesion, exitoso)
            {
                IdRegistro = cuenta?.Id,
                IdUsuario = cuenta?.Id,
                NombreUsuario = cuenta?.UserName ?? NombreSeguro(Input.CodigoEmpleado),
                Rol = rol,
                Descripcion = descripcion,
            });
        }

        private const string UsuarioNoReconocido = "(usuario no reconocido)";

        // Si la cuenta no existe, lo digitado puede ser una contraseña y la bitácora no se puede borrar:
        // solo se guarda si tiene forma de correo (no de código de empleado, que una contraseña igualaría).
        private static string NombreSeguro(string digitado) =>
            digitado is not null && System.Text.RegularExpressions.Regex.IsMatch(digitado.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$")
                ? digitado.Trim()
                : UsuarioNoReconocido;
    }
}
