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

namespace SAPS.Web.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<IdentityUser> signInManager, ApplicationDbContext db, ILogger<LoginModel> logger)
        {
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

        private const string MensajeColaboradorInactivo =
            "Tu perfil de colaborador está inactivo. Comunicate con Recursos Humanos o el administrador.";

        public class InputModel
        {
            [Required]
            [Display(Name = "Correo de empleado")]
            public string CodigoEmpleado { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
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
                ModelState.AddModelError(string.Empty, "Tu acceso fue desactivado. Comunicate con el administrador.");
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
                        ModelState.AddModelError(string.Empty, MensajeColaboradorInactivo);
                        return Page();
                    }

                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    // F-02 (H9): el mensaje nombra el campo real e indica qué hacer
                    ModelState.AddModelError(string.Empty, "El correo de empleado o la contraseña son incorrectos. Revisa los datos e inténtalo de nuevo. Si olvidaste tu contraseña, comunícate con el administrador.");
                    return Page();
                }
            }

            return Page();
        }
    }
}
