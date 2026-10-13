using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;
using SAPS.Web.Services.Identity;

namespace SAPS.Tests;

/// <summary>
/// HU-001 | Pantalla de gestión de usuarios (solo Administrador): alta con colaborador
/// obligatorio, baja lógica (desactivar/reactivar) y las reglas de "último Administrador".
/// </summary>
public class HU001_GestionUsuariosTests
{
    private static async Task<(ApplicationDbContext db, UserManager<IdentityUser> um, RoleManager<IdentityRole> rm)> CrearEntornoAsync(bool seedearRoles = true)
    {
        var sp = TestServices.Crear();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
        if (seedearRoles)
        {
            foreach (var rol in CrearUsuarioViewModel.RolesDisponibles)
                await rm.CreateAsync(new IdentityRole(rol));
        }
        return (db, um, rm);
    }

    private static async Task<int> CrearColaboradorAsync(ApplicationDbContext db, bool activo = true, string codigo = "C001")
    {
        var colaborador = new ColaboradorRrhh { Codigo = codigo, NombreCompleto = "Colaborador de prueba", EstaActivo = activo, FechaRegistro = DateTime.UtcNow };
        db.Colaboradores.Add(colaborador);
        await db.SaveChangesAsync();
        return colaborador.IdColaborador;
    }

    private static UsuariosController CrearControlador(ApplicationDbContext db, UserManager<IdentityUser> um, string? idUsuarioActual = null)
    {
        var controlador = TestServices.ConContexto(new UsuariosController(db, um), "POST");
        controlador.ControllerContext.HttpContext.User = idUsuarioActual is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, idUsuarioActual)], "prueba"));
        return controlador;
    }

    // ---------- Alta ----------

    [Fact]
    public async Task Crear_ConDatosValidos_CreaUsuarioRolVinculoYEstadoDeContrasenaTemporal()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var idColaborador = await CrearColaboradorAsync(db);
        var controlador = CrearControlador(db, um);

        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "nuevo@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Soda",
            IdColaborador = idColaborador,
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var usuario = await um.FindByNameAsync("nuevo@recyplast.cr");
        Assert.NotNull(usuario);
        Assert.True(await um.IsInRoleAsync(usuario!, "Soda"));
        var vinculo = await db.VinculosUsuarioColaborador.SingleAsync(v => v.IdUsuario == usuario!.Id);
        Assert.Equal(idColaborador, vinculo.IdColaborador);
        var estado = await db.EstadosContrasena.SingleAsync(e => e.IdUsuario == usuario!.Id);
        Assert.True(estado.RequiereCambioInicial);
    }

    [Fact]
    public async Task Crear_SinColaboradorSeleccionado_NoCreaLaCuenta()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var controlador = CrearControlador(db, um);

        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "sin-colaborador@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Administrador",
            IdColaborador = null,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Null(await um.FindByNameAsync("sin-colaborador@recyplast.cr"));
    }

    [Fact]
    public async Task Crear_ConColaboradorInactivo_NoCreaLaCuenta()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var idColaborador = await CrearColaboradorAsync(db, activo: false);
        var controlador = CrearControlador(db, um);

        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "con-inactivo@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "RecursosHumanos",
            IdColaborador = idColaborador,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Null(await um.FindByNameAsync("con-inactivo@recyplast.cr"));
    }

    [Fact]
    public async Task Crear_ConColaboradorYaVinculado_NoCreaLaCuenta()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var idColaborador = await CrearColaboradorAsync(db);
        var primero = await CrearControlador(db, um).Crear(new CrearUsuarioViewModel
        {
            Correo = "primero@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Soda",
            IdColaborador = idColaborador,
        });
        Assert.IsType<RedirectToActionResult>(primero);

        var controlador = CrearControlador(db, um);
        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "segundo@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Soda",
            IdColaborador = idColaborador,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Null(await um.FindByNameAsync("segundo@recyplast.cr"));
    }

    [Fact]
    public async Task Crear_ConCorreoYaExistente_NoCreaOtraCuenta()
    {
        var (db, um, _) = await CrearEntornoAsync();
        await um.CreateAsync(new IdentityUser { UserName = "repetido@recyplast.cr" }, "Prueba123");
        var idColaborador = await CrearColaboradorAsync(db);
        var controlador = CrearControlador(db, um);

        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "repetido@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Soda",
            IdColaborador = idColaborador,
        });

        Assert.IsType<ViewResult>(resultado);
        var vinculo = await db.VinculosUsuarioColaborador.AnyAsync(v => v.IdColaborador == idColaborador);
        Assert.False(vinculo); // no quedó vinculado a nadie
    }

    [Fact]
    public async Task Crear_SiFallaUnPasoPosteriorACrearElUsuario_LoElimina()
    {
        // Sin sembrar roles: AddToRoleAsync falla porque el rol "Soda" no existe en la base.
        var (db, um, _) = await CrearEntornoAsync(seedearRoles: false);
        var idColaborador = await CrearColaboradorAsync(db);
        var controlador = CrearControlador(db, um);

        var resultado = await controlador.Crear(new CrearUsuarioViewModel
        {
            Correo = "huerfano@recyplast.cr",
            ContrasenaInicial = "Temporal123",
            Rol = "Soda",
            IdColaborador = idColaborador,
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Null(await um.FindByNameAsync("huerfano@recyplast.cr"));
        Assert.False(await db.VinculosUsuarioColaborador.AnyAsync(v => v.IdColaborador == idColaborador));
    }

    // ---------- Búsqueda de colaboradores disponibles ----------

    [Fact]
    public async Task BuscarDisponibles_SoloDevuelveActivosYSinVinculo()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var activoLibre = await CrearColaboradorAsync(db, activo: true, codigo: "C010");
        var inactivo = await CrearColaboradorAsync(db, activo: false, codigo: "C011");
        var activoVinculado = await CrearColaboradorAsync(db, activo: true, codigo: "C012");
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = "otro-usuario", IdColaborador = activoVinculado, FechaVinculacion = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var disponibles = await BusquedaColaboradores.BuscarDisponiblesAsync(db);

        Assert.Single(disponibles);
        Assert.Equal(activoLibre, disponibles[0].IdColaborador);
    }

    // ---------- Baja lógica / reactivación ----------

    [Fact]
    public async Task Desactivar_CuentaPropia_SeRechaza()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var admin = new IdentityUser { UserName = "admin@recyplast.cr" };
        await um.CreateAsync(admin, "Prueba123");
        await um.AddToRoleAsync(admin, "Administrador");
        var controlador = CrearControlador(db, um, idUsuarioActual: admin.Id);

        await controlador.Desactivar(admin.Id);

        var recargado = await um.FindByIdAsync(admin.Id);
        Assert.Null(recargado!.LockoutEnd);
    }

    [Fact]
    public async Task Desactivar_UltimoAdministradorActivo_SeRechaza()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var admin = new IdentityUser { UserName = "unico-admin@recyplast.cr" };
        await um.CreateAsync(admin, "Prueba123");
        await um.AddToRoleAsync(admin, "Administrador");
        var otroUsuario = new IdentityUser { UserName = "otro@recyplast.cr" };
        await um.CreateAsync(otroUsuario, "Prueba123");
        var controlador = CrearControlador(db, um, idUsuarioActual: otroUsuario.Id);

        await controlador.Desactivar(admin.Id);

        var recargado = await um.FindByIdAsync(admin.Id);
        Assert.Null(recargado!.LockoutEnd);
    }

    [Fact]
    public async Task Desactivar_AdministradorConOtroAdministradorActivo_SePermite()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var admin1 = new IdentityUser { UserName = "admin1@recyplast.cr" };
        await um.CreateAsync(admin1, "Prueba123");
        await um.AddToRoleAsync(admin1, "Administrador");
        var admin2 = new IdentityUser { UserName = "admin2@recyplast.cr" };
        await um.CreateAsync(admin2, "Prueba123");
        await um.AddToRoleAsync(admin2, "Administrador");
        var controlador = CrearControlador(db, um, idUsuarioActual: admin2.Id);

        await controlador.Desactivar(admin1.Id);

        var recargado = await um.FindByIdAsync(admin1.Id);
        Assert.Equal(DateTimeOffset.MaxValue, recargado!.LockoutEnd);
    }

    [Fact]
    public async Task Desactivar_CuentaNoAdministrador_SePermiteSiempre()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var sodaUser = new IdentityUser { UserName = "soda1@recyplast.cr" };
        await um.CreateAsync(sodaUser, "Prueba123");
        await um.AddToRoleAsync(sodaUser, "Soda");
        var admin = new IdentityUser { UserName = "admin@recyplast.cr" };
        await um.CreateAsync(admin, "Prueba123");
        await um.AddToRoleAsync(admin, "Administrador");
        var controlador = CrearControlador(db, um, idUsuarioActual: admin.Id);

        await controlador.Desactivar(sodaUser.Id);

        var recargado = await um.FindByIdAsync(sodaUser.Id);
        Assert.Equal(DateTimeOffset.MaxValue, recargado!.LockoutEnd);
        Assert.True(recargado.LockoutEnabled);
    }

    [Fact]
    public async Task Reactivar_QuitaElLockoutYReseteaIntentosFallidos()
    {
        var (db, um, _) = await CrearEntornoAsync();
        var usuario = new IdentityUser { UserName = "reactivar@recyplast.cr" };
        await um.CreateAsync(usuario, "Prueba123");
        await um.SetLockoutEnabledAsync(usuario, true);
        await um.SetLockoutEndDateAsync(usuario, DateTimeOffset.MaxValue);
        await um.AccessFailedAsync(usuario);
        var controlador = CrearControlador(db, um, idUsuarioActual: "admin-x");

        await controlador.Reactivar(usuario.Id);

        var recargado = await um.FindByIdAsync(usuario.Id);
        Assert.Null(recargado!.LockoutEnd);
        Assert.Equal(0, await um.GetAccessFailedCountAsync(recargado));
    }

    // ---------- Estado calculado para el listado ----------

    [Fact]
    public void CalcularEstado_LockoutEndMaxValue_EsDesactivada()
    {
        var ahora = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("Desactivada", UsuariosController.CalcularEstado(true, DateTimeOffset.MaxValue, ahora));
    }

    [Fact]
    public void CalcularEstado_LockoutEndCercanoEnElFuturo_EsBloqueadaTemporalmente()
    {
        var ahora = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("Bloqueada temporalmente", UsuariosController.CalcularEstado(true, ahora.AddMinutes(10), ahora));
    }

    [Fact]
    public void CalcularEstado_LockoutEnabledFalso_EsActivaAunqueHayaUnaFechaFutura()
    {
        var ahora = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("Activa", UsuariosController.CalcularEstado(false, ahora.AddMinutes(10), ahora));
    }

    [Fact]
    public void CalcularEstado_SinLockout_EsActiva()
    {
        var ahora = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal("Activa", UsuariosController.CalcularEstado(false, null, ahora));
    }
}
