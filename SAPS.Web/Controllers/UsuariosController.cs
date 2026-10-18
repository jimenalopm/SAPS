using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;
using SAPS.Web.Services.Auditoria;
using SAPS.Web.Services.Identity;

namespace SAPS.Web.Controllers;

/// <summary>
/// HU-001: gestión de usuarios, solo Administrador (a diferencia de AdministracionController y
/// RecursosHumanosController, que también permiten el rol RecursosHumanos). "Eliminar" una cuenta
/// es siempre baja lógica (LockoutEnd), nunca borrado físico: tb_Pedido.idUsuario tiene FK a
/// AspNetUsers.Id con ON DELETE NO ACTION (PedidoConfiguration.cs), así que un borrado físico
/// rompería el historial de pedidos. HU-004 (criterio 3): las altas, bajas, reactivaciones y cambios
/// de rol quedan en la bitácora con la cuenta afectada; quien hizo el cambio lo toma el servicio del request.
/// </summary>
[Authorize(Roles = "Administrador")]
public class UsuariosController(ApplicationDbContext db, UserManager<IdentityUser> userManager,
    IServicioBitacora? bitacora = null) : Controller
{
    public async Task<IActionResult> Index()
    {
        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Users.AsNoTracking()
            .Select(u => new
            {
                u.Id,
                u.UserName,
                u.LockoutEnabled,
                u.LockoutEnd,
                Rol = (from ur in db.UserRoles
                       join r in db.Roles on ur.RoleId equals r.Id
                       where ur.UserId == u.Id
                       select r.Name).FirstOrDefault(),
                ColaboradorNombre = (from v in db.VinculosUsuarioColaborador
                                     join c in db.Colaboradores on v.IdColaborador equals c.IdColaborador
                                     where v.IdUsuario == u.Id
                                     select c.NombreCompleto).FirstOrDefault(),
            })
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var vm = filas.Select(u => new UsuarioListadoViewModel
        {
            Id = u.Id,
            Correo = u.UserName ?? "",
            Rol = u.Rol ?? "Sin rol asignado",
            ColaboradorNombre = u.ColaboradorNombre,
            Estado = CalcularEstado(u.LockoutEnabled, u.LockoutEnd, ahora),
        }).ToList();

        return View(vm);
    }

    public async Task<IActionResult> Crear()
    {
        var modelo = new CrearUsuarioViewModel();
        await CargarColaboradoresDisponibles(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearUsuarioViewModel modelo)
    {
        await CargarColaboradoresDisponibles(modelo);

        if (!CrearUsuarioViewModel.RolesDisponibles.Contains(modelo.Rol))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Seleccione un rol válido.");
        }

        ColaboradorRrhh? colaborador = null;
        if (modelo.IdColaborador is int idColaborador)
        {
            colaborador = await db.Colaboradores.FirstOrDefaultAsync(c => c.IdColaborador == idColaborador);
            if (colaborador is null || !colaborador.EstaActivo)
            {
                ModelState.AddModelError(nameof(modelo.IdColaborador), "Seleccione un colaborador activo.");
            }
            else if (await db.VinculosUsuarioColaborador.AnyAsync(v => v.IdColaborador == idColaborador))
            {
                ModelState.AddModelError(nameof(modelo.IdColaborador), "Ese colaborador ya está vinculado a otra cuenta.");
            }
        }

        if (!string.IsNullOrWhiteSpace(modelo.Correo) && await userManager.FindByNameAsync(modelo.Correo) is not null)
        {
            ModelState.AddModelError(nameof(modelo.Correo), "Ya existe una cuenta con ese correo.");
        }

        if (!ModelState.IsValid) return View(modelo);

        var nuevo = new IdentityUser { UserName = modelo.Correo, Email = modelo.Correo };
        var resultadoCrear = await userManager.CreateAsync(nuevo, modelo.ContrasenaInicial);
        if (!resultadoCrear.Succeeded)
        {
            foreach (var error in resultadoCrear.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(modelo);
        }

        try
        {
            var resultadoRol = await userManager.AddToRoleAsync(nuevo, modelo.Rol);
            if (!resultadoRol.Succeeded)
                throw new InvalidOperationException(string.Join(" ", resultadoRol.Errors.Select(e => e.Description)));

            var ahora = DateTime.UtcNow;
            db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador
            {
                IdUsuario = nuevo.Id,
                IdColaborador = colaborador!.IdColaborador,
                FechaVinculacion = ahora,
            });
            // Contraseña temporal: el primer ingreso obliga a cambiarla antes de usar cualquier
            // otra página (VencimientoContrasenaMiddleware), y desde ahí arranca la vigencia de 10 meses.
            db.EstadosContrasena.Add(new EstadoContrasenaUsuario
            {
                IdUsuario = nuevo.Id,
                FechaUltimoCambio = ahora,
                RequiereCambioInicial = true,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Identity no ofrece una transacción ambiente entre CreateAsync y los SaveChanges de
            // ApplicationDbContext: si un paso posterior falla, se compensa eliminando la cuenta
            // recién creada para no dejarla huérfana (sin rol, sin vínculo o sin estado de contraseña).
            await userManager.DeleteAsync(nuevo);
            await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaCreada, false, nuevo.Id, null, modelo.Rol,
                $"No se pudo completar el alta de la cuenta {modelo.Correo}; se revirtió");
            ModelState.AddModelError(string.Empty, "No se pudo completar el alta. Verifique los datos e intente de nuevo.");
            return View(modelo);
        }

        await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaCreada, true, nuevo.Id, null, modelo.Rol,
            $"Cuenta {modelo.Correo} creada con rol {CrearUsuarioViewModel.NombreVisible(modelo.Rol)} (colaborador {colaborador!.Codigo})");
        TempData["Mensaje"] = $"Cuenta \"{modelo.Correo}\" creada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Editar(string id)
    {
        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        var modelo = new EditarUsuarioViewModel
        {
            Id = usuario.Id,
            Correo = usuario.UserName ?? "",
            Rol = (await userManager.GetRolesAsync(usuario)).FirstOrDefault() ?? "",
            IdColaborador = await db.VinculosUsuarioColaborador
                .Where(v => v.IdUsuario == usuario.Id)
                .Select(v => (int?)v.IdColaborador)
                .FirstOrDefaultAsync(),
            EsCuentaPropia = usuario.Id == User.FindFirstValue(ClaimTypes.NameIdentifier),
        };
        await CargarColaboradoresParaEditar(modelo);
        return View(modelo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(EditarUsuarioViewModel modelo)
    {
        var usuario = await userManager.FindByIdAsync(modelo.Id);
        if (usuario is null) return NotFound();

        modelo.EsCuentaPropia = usuario.Id == User.FindFirstValue(ClaimTypes.NameIdentifier);
        await CargarColaboradoresParaEditar(modelo);
        modelo.Correo = (modelo.Correo ?? "").Trim();

        var rolActual = (await userManager.GetRolesAsync(usuario)).FirstOrDefault();
        var cambiaRol = !string.Equals(rolActual, modelo.Rol, StringComparison.Ordinal);
        var idColaboradorActual = await db.VinculosUsuarioColaborador
            .Where(v => v.IdUsuario == usuario.Id)
            .Select(v => (int?)v.IdColaborador)
            .FirstOrDefaultAsync();

        if (!CrearUsuarioViewModel.RolesDisponibles.Contains(modelo.Rol))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Seleccione un rol válido.");
        }
        else if (cambiaRol && modelo.EsCuentaPropia)
        {
            ModelState.AddModelError(nameof(modelo.Rol), "No puede cambiar el rol de su propia cuenta.");
        }
        else if (cambiaRol && rolActual == "Administrador" && !await QuedaOtroAdministradorActivoAsync(usuario.Id))
        {
            ModelState.AddModelError(nameof(modelo.Rol), "Debe quedar al menos un Administrador activo.");
        }

        if (!string.IsNullOrWhiteSpace(modelo.Correo))
        {
            var existente = await userManager.FindByNameAsync(modelo.Correo);
            if (existente is not null && existente.Id != usuario.Id)
            {
                ModelState.AddModelError(nameof(modelo.Correo), "Ya existe una cuenta con ese correo.");
            }
        }

        // Solo se exige un colaborador activo y libre si se cambia: conservar el que ya tiene siempre se permite.
        if (modelo.IdColaborador is int idColaborador && idColaborador != idColaboradorActual)
        {
            var colaborador = await db.Colaboradores.FirstOrDefaultAsync(c => c.IdColaborador == idColaborador);
            if (colaborador is null || !colaborador.EstaActivo)
            {
                ModelState.AddModelError(nameof(modelo.IdColaborador), "Seleccione un colaborador activo.");
            }
            else if (await db.VinculosUsuarioColaborador.AnyAsync(v => v.IdColaborador == idColaborador))
            {
                ModelState.AddModelError(nameof(modelo.IdColaborador), "Ese colaborador ya está vinculado a otra cuenta.");
            }
        }

        if (!ModelState.IsValid) return View(modelo);

        // HU-004 criterio 3: el cambio de correo o de colaborador también es una modificación de la cuenta.
        var correoAnterior = usuario.UserName ?? "";
        try
        {
            if (!string.Equals(usuario.UserName, modelo.Correo, StringComparison.Ordinal))
            {
                var resultadoNombre = await userManager.SetUserNameAsync(usuario, modelo.Correo);
                if (resultadoNombre.Succeeded) resultadoNombre = await userManager.SetEmailAsync(usuario, modelo.Correo);
                await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaModificada, resultadoNombre.Succeeded, usuario.Id,
                    correoAnterior, modelo.Correo, $"Cuenta {modelo.Correo}: correo {correoAnterior} -> {modelo.Correo}");
                if (!resultadoNombre.Succeeded) return ErrorDeEdicion(modelo, resultadoNombre);
            }

            if (cambiaRol)
            {
                var rolAnterior = rolActual ?? "Sin rol";
                var descripcionRol = $"Cuenta {modelo.Correo}: rol {CrearUsuarioViewModel.NombreVisible(rolActual)} -> {CrearUsuarioViewModel.NombreVisible(modelo.Rol)}";
                if (rolActual is not null)
                {
                    var quitar = await userManager.RemoveFromRoleAsync(usuario, rolActual);
                    if (!quitar.Succeeded)
                    {
                        await RegistrarEnBitacoraAsync(TiposEventoBitacora.RolCambiado, false, usuario.Id, rolAnterior, modelo.Rol, descripcionRol);
                        return ErrorDeEdicion(modelo, quitar);
                    }
                }
                var agregar = await userManager.AddToRoleAsync(usuario, modelo.Rol);
                if (!agregar.Succeeded)
                {
                    await RegistrarEnBitacoraAsync(TiposEventoBitacora.RolCambiado, false, usuario.Id, rolAnterior, modelo.Rol, descripcionRol);
                    return ErrorDeEdicion(modelo, agregar);
                }
                // Fuerza a que la sesión abierta de la persona se revalide con el rol nuevo.
                await userManager.UpdateSecurityStampAsync(usuario);
                await RegistrarEnBitacoraAsync(TiposEventoBitacora.RolCambiado, true, usuario.Id, rolAnterior, modelo.Rol, descripcionRol);
            }

            if (modelo.IdColaborador is int nuevoColaborador && nuevoColaborador != idColaboradorActual)
            {
                var vinculo = await db.VinculosUsuarioColaborador.FirstOrDefaultAsync(v => v.IdUsuario == usuario.Id);
                if (vinculo is null)
                {
                    db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador
                    {
                        IdUsuario = usuario.Id,
                        IdColaborador = nuevoColaborador,
                        FechaVinculacion = DateTime.UtcNow,
                    });
                }
                else
                {
                    vinculo.IdColaborador = nuevoColaborador;
                    vinculo.FechaVinculacion = DateTime.UtcNow;
                }
                await db.SaveChangesAsync();

                var codigos = await db.Colaboradores.AsNoTracking()
                    .Where(c => c.IdColaborador == nuevoColaborador || c.IdColaborador == idColaboradorActual)
                    .ToDictionaryAsync(c => c.IdColaborador, c => c.Codigo);
                var codigoAnterior = idColaboradorActual is int ida && codigos.TryGetValue(ida, out var ca) ? ca : "Sin colaborador";
                var codigoNuevo = codigos.GetValueOrDefault(nuevoColaborador, nuevoColaborador.ToString());
                await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaModificada, true, usuario.Id, codigoAnterior, codigoNuevo,
                    $"Cuenta {modelo.Correo}: colaborador {codigoAnterior} -> {codigoNuevo}");
            }
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "No se pudo guardar el cambio. Verifique los datos e intente de nuevo.");
            return View(modelo);
        }

        TempData["Mensaje"] = $"Cuenta \"{modelo.Correo}\" actualizada.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ErrorDeEdicion(EditarUsuarioViewModel modelo, IdentityResult resultado)
    {
        foreach (var error in resultado.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(modelo);
    }

    private async Task CargarColaboradoresParaEditar(EditarUsuarioViewModel modelo) =>
        modelo.ColaboradoresDisponibles = await BusquedaColaboradores.BuscarParaEditarAsync(db, modelo.Id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desactivar(string id)
    {
        var idPropio = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == idPropio)
        {
            TempData["Error"] = "No puede desactivar su propia cuenta.";
            return RedirectToAction(nameof(Index));
        }

        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        if (await userManager.IsInRoleAsync(usuario, "Administrador") && !await QuedaOtroAdministradorActivoAsync(id))
        {
            TempData["Error"] = "Debe quedar al menos un Administrador activo.";
            return RedirectToAction(nameof(Index));
        }

        var estadoAnterior = CalcularEstado(usuario.LockoutEnabled, usuario.LockoutEnd, DateTimeOffset.UtcNow);
        var resultado = await userManager.SetLockoutEnabledAsync(usuario, true);
        if (resultado.Succeeded) resultado = await userManager.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
        await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaDesactivada, resultado.Succeeded, usuario.Id, estadoAnterior, "Desactivada",
            $"Cuenta {usuario.UserName} desactivada");
        if (!resultado.Succeeded)
        {
            TempData["Error"] = $"No se pudo desactivar la cuenta \"{usuario.UserName}\". Intente de nuevo.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Mensaje"] = $"Cuenta \"{usuario.UserName}\" desactivada.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivar(string id)
    {
        var usuario = await userManager.FindByIdAsync(id);
        if (usuario is null) return NotFound();

        var estadoAnterior = CalcularEstado(usuario.LockoutEnabled, usuario.LockoutEnd, DateTimeOffset.UtcNow);
        var resultado = await userManager.SetLockoutEndDateAsync(usuario, null);
        // Limpia también cualquier contador de intentos fallidos viejo (RNF-001), para que la
        // cuenta reactivada no vuelva a caer en un bloqueo transitorio por un conteo previo.
        if (resultado.Succeeded) resultado = await userManager.ResetAccessFailedCountAsync(usuario);
        await RegistrarEnBitacoraAsync(TiposEventoBitacora.CuentaReactivada, resultado.Succeeded, usuario.Id, estadoAnterior, "Activa",
            $"Cuenta {usuario.UserName} reactivada");
        if (!resultado.Succeeded)
        {
            TempData["Error"] = $"No se pudo reactivar la cuenta \"{usuario.UserName}\". Intente de nuevo.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Mensaje"] = $"Cuenta \"{usuario.UserName}\" reactivada.";
        return RedirectToAction(nameof(Index));
    }

    // HU-004 criterio 3: la cuenta afectada va en IdRegistro y en la descripción; el usuario, rol, IP y
    // dispositivo de quien hizo el cambio los completa el servicio desde el request. El servicio nunca lanza.
    private async Task RegistrarEnBitacoraAsync(string tipoEvento, bool exitoso, string idCuenta,
        string? valorAnterior, string? valorNuevo, string descripcion)
    {
        if (bitacora is null) return;
        await bitacora.RegistrarAsync(new EventoBitacora(ModulosBitacora.Seguridad, tipoEvento, RecursosBitacora.Cuentas, exitoso)
        {
            IdRegistro = idCuenta,
            ValorAnterior = valorAnterior,
            ValorNuevo = valorNuevo,
            Descripcion = descripcion,
        });
    }

    private async Task CargarColaboradoresDisponibles(CrearUsuarioViewModel modelo) =>
        modelo.ColaboradoresDisponibles = await BusquedaColaboradores.BuscarDisponiblesAsync(db);

    private async Task<bool> QuedaOtroAdministradorActivoAsync(string idExcluido)
    {
        var ahora = DateTimeOffset.UtcNow;
        var administradores = await userManager.GetUsersInRoleAsync("Administrador");
        return administradores.Any(a => a.Id != idExcluido && CalcularEstado(a.LockoutEnabled, a.LockoutEnd, ahora) == "Activa");
    }

    public static string CalcularEstado(bool lockoutEnabled, DateTimeOffset? lockoutEnd, DateTimeOffset ahora)
    {
        if (lockoutEnd == DateTimeOffset.MaxValue) return "Desactivada";
        if (lockoutEnabled && lockoutEnd is { } fin && fin > ahora) return "Bloqueada temporalmente";
        return "Activa";
    }
}
