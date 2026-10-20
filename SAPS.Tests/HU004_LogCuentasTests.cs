using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Data;
using SAPS.Web.Models.Identity;
using SAPS.Web.Models.Rrhh;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// HU-004 criterio 3 (tasks 7 y 9): las altas, bajas, reactivaciones y cambios de rol que hace el Administrador
/// en la gestión de usuarios quedan en la bitácora con la cuenta afectada y el valor anterior y nuevo.
/// Una operación rechazada por las reglas de negocio no deja registro.
/// </summary>
public class HU004_LogCuentasTests
{
    // ---------- Alta ----------

    [Fact]
    public async Task Crear_Exitoso_RegistraCuentaCreadaConLaCuentaYElRol()
    {
        var (db, um) = await CrearEntornoAsync();
        var idColaborador = await CrearColaboradorAsync(db, "C100");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora).Crear(new CrearUsuarioViewModel
        {
            Correo = "nuevo@recyplast.cr", ContrasenaInicial = "Temporal123", Rol = "Soda", IdColaborador = idColaborador,
        });

        var cuenta = await um.FindByNameAsync("nuevo@recyplast.cr");
        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaCreada, evento.TipoEvento);
        Assert.Equal(ModulosBitacora.Seguridad, evento.Modulo);
        Assert.Equal(RecursosBitacora.Cuentas, evento.Recurso);
        Assert.True(evento.EsExitoso);
        Assert.Equal(cuenta!.Id, evento.IdRegistro);
        Assert.Null(evento.ValorAnterior);
        Assert.Equal("Soda", evento.ValorNuevo);
        Assert.Contains("nuevo@recyplast.cr", evento.Descripcion);
        Assert.Contains("C100", evento.Descripcion);
    }

    [Fact]
    public async Task Crear_ConDatosInvalidos_NoRegistraNada()
    {
        var (db, um) = await CrearEntornoAsync();
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora).Crear(new CrearUsuarioViewModel
        {
            Correo = "nuevo@recyplast.cr", ContrasenaInicial = "Temporal123", Rol = "RolInventado", IdColaborador = null,
        });

        Assert.Empty(bitacora.Eventos);
    }

    [Fact]
    public async Task Crear_SiFallaUnPasoPosterior_RegistraElAltaComoFallida()
    {
        // Sin sembrar roles: AddToRoleAsync falla y el controlador revierte la cuenta recién creada.
        var (db, um) = await CrearEntornoAsync(seedearRoles: false);
        var idColaborador = await CrearColaboradorAsync(db, "C101");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora).Crear(new CrearUsuarioViewModel
        {
            Correo = "fallido@recyplast.cr", ContrasenaInicial = "Temporal123", Rol = "Soda", IdColaborador = idColaborador,
        });

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaCreada, evento.TipoEvento);
        Assert.False(evento.EsExitoso);
        Assert.Contains("fallido@recyplast.cr", evento.Descripcion);
        Assert.Null(await um.FindByNameAsync("fallido@recyplast.cr"));
    }

    // ---------- Cambio de rol ----------

    [Fact]
    public async Task Editar_CambioDeRol_RegistraRolAnteriorYNuevo()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        var resultado = await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = cuenta.Id, Correo = "soda@recyplast.cr", Rol = "RecursosHumanos",
        });

        Assert.IsType<RedirectToActionResult>(resultado);
        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.RolCambiado, evento.TipoEvento);
        Assert.True(evento.EsExitoso);
        Assert.Equal(cuenta.Id, evento.IdRegistro);
        Assert.Equal("Soda", evento.ValorAnterior);
        Assert.Equal("RecursosHumanos", evento.ValorNuevo);
        Assert.Contains("soda@recyplast.cr", evento.Descripcion);
    }

    [Fact]
    public async Task Editar_SinCambioDeRol_NoRegistraCambioDeRol()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = cuenta.Id, Correo = "soda2@recyplast.cr", Rol = "Soda",
        });

        Assert.DoesNotContain(bitacora.Eventos, e => e.TipoEvento == TiposEventoBitacora.RolCambiado);
    }

    [Fact]
    public async Task Editar_CambioDeRolDeLaCuentaPropia_SeRechazaYNoRegistra()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        await CrearCuentaAsync(um, "admin2@recyplast.cr", "Administrador");
        var bitacora = new BitacoraFalsa();

        var resultado = await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = admin.Id, Correo = "admin@recyplast.cr", Rol = "Soda",
        });

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(bitacora.Eventos);
        Assert.True(await um.IsInRoleAsync(admin, "Administrador"));
    }

    // ---------- Cambio de correo o colaborador ----------

    [Fact]
    public async Task Editar_CambioDeCorreo_RegistraCuentaModificadaConCorreoAnteriorYNuevo()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = cuenta.Id, Correo = "soda.nueva@recyplast.cr", Rol = "Soda",
        });

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaModificada, evento.TipoEvento);
        Assert.True(evento.EsExitoso);
        Assert.Equal(cuenta.Id, evento.IdRegistro);
        Assert.Equal("soda@recyplast.cr", evento.ValorAnterior);
        Assert.Equal("soda.nueva@recyplast.cr", evento.ValorNuevo);
    }

    [Fact]
    public async Task Editar_CambioDeColaborador_RegistraCuentaModificadaConLosCodigos()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var anterior = await CrearColaboradorAsync(db, "C200");
        var nuevo = await CrearColaboradorAsync(db, "C201");
        db.VinculosUsuarioColaborador.Add(new VinculoUsuarioColaborador { IdUsuario = cuenta.Id, IdColaborador = anterior, FechaVinculacion = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = cuenta.Id, Correo = "soda@recyplast.cr", Rol = "Soda", IdColaborador = nuevo,
        });

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaModificada, evento.TipoEvento);
        Assert.Equal("C200", evento.ValorAnterior);
        Assert.Equal("C201", evento.ValorNuevo);
        Assert.Contains("soda@recyplast.cr", evento.Descripcion);
    }

    [Fact]
    public async Task Editar_SinCambios_NoRegistraNada()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Editar(new EditarUsuarioViewModel
        {
            Id = cuenta.Id, Correo = "soda@recyplast.cr", Rol = "Soda",
        });

        Assert.Empty(bitacora.Eventos);
    }

    // ---------- Baja y reactivación ----------

    [Fact]
    public async Task Desactivar_RegistraCuentaDesactivadaConEstadoAnteriorYNuevo()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Desactivar(cuenta.Id);

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaDesactivada, evento.TipoEvento);
        Assert.True(evento.EsExitoso);
        Assert.Equal(cuenta.Id, evento.IdRegistro);
        Assert.Equal("Activa", evento.ValorAnterior);
        Assert.Equal("Desactivada", evento.ValorNuevo);
        Assert.Contains("soda@recyplast.cr", evento.Descripcion);
    }

    [Fact]
    public async Task Desactivar_UltimoAdministrador_SeRechazaYNoRegistra()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var otro = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, otro.Id).Desactivar(admin.Id);

        Assert.Empty(bitacora.Eventos);
        Assert.NotEqual(DateTimeOffset.MaxValue, await um.GetLockoutEndDateAsync(admin));
    }

    [Fact]
    public async Task Desactivar_CuentaPropia_SeRechazaYNoRegistra()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Desactivar(admin.Id);

        Assert.Empty(bitacora.Eventos);
    }

    [Fact]
    public async Task Reactivar_RegistraCuentaReactivadaConEstadoAnteriorYNuevo()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        await um.SetLockoutEnabledAsync(cuenta, true);
        await um.SetLockoutEndDateAsync(cuenta, DateTimeOffset.MaxValue);
        var bitacora = new BitacoraFalsa();

        await CrearControlador(db, um, bitacora, admin.Id).Reactivar(cuenta.Id);

        var evento = Assert.Single(bitacora.Eventos);
        Assert.Equal(TiposEventoBitacora.CuentaReactivada, evento.TipoEvento);
        Assert.True(evento.EsExitoso);
        Assert.Equal(cuenta.Id, evento.IdRegistro);
        Assert.Equal("Desactivada", evento.ValorAnterior);
        Assert.Equal("Activa", evento.ValorNuevo);
    }

    // ---------- Sin servicio de bitácora ----------

    [Fact]
    public async Task SinServicioDeBitacora_LaGestionDeUsuariosSigueFuncionando()
    {
        var (db, um) = await CrearEntornoAsync();
        var admin = await CrearCuentaAsync(um, "admin@recyplast.cr", "Administrador");
        var cuenta = await CrearCuentaAsync(um, "soda@recyplast.cr", "Soda");
        var controlador = TestServices.ConContexto(new UsuariosController(db, um), "POST");
        controlador.ControllerContext.HttpContext.User = Usuario(admin.Id);

        await controlador.Desactivar(cuenta.Id);

        Assert.Equal(DateTimeOffset.MaxValue, await um.GetLockoutEndDateAsync(cuenta));
    }

    // ---------- Apoyo ----------

    private static async Task<(ApplicationDbContext db, UserManager<IdentityUser> um)> CrearEntornoAsync(bool seedearRoles = true)
    {
        var sp = TestServices.Crear();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var um = sp.GetRequiredService<UserManager<IdentityUser>>();
        if (seedearRoles)
        {
            var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var rol in CrearUsuarioViewModel.RolesDisponibles)
                await rm.CreateAsync(new IdentityRole(rol));
        }
        return (db, um);
    }

    private static async Task<int> CrearColaboradorAsync(ApplicationDbContext db, string codigo)
    {
        var colaborador = new ColaboradorRrhh { Codigo = codigo, NombreCompleto = "Colaborador de prueba", EstaActivo = true, FechaRegistro = DateTime.UtcNow };
        db.Colaboradores.Add(colaborador);
        await db.SaveChangesAsync();
        return colaborador.IdColaborador;
    }

    private static async Task<IdentityUser> CrearCuentaAsync(UserManager<IdentityUser> um, string correo, string rol)
    {
        var cuenta = new IdentityUser { UserName = correo, Email = correo };
        await um.CreateAsync(cuenta, "Prueba123");
        await um.AddToRoleAsync(cuenta, rol);
        return cuenta;
    }

    private static ClaimsPrincipal Usuario(string? id) => id is null
        ? new ClaimsPrincipal(new ClaimsIdentity())
        : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "prueba"));

    private static UsuariosController CrearControlador(ApplicationDbContext db, UserManager<IdentityUser> um,
        IServicioBitacora bitacora, string? idUsuarioActual = null)
    {
        var controlador = TestServices.ConContexto(new UsuariosController(db, um, bitacora), "POST");
        controlador.ControllerContext.HttpContext.User = Usuario(idUsuarioActual);
        return controlador;
    }

    /// <summary>Guarda los eventos que recibiría el servicio real, sin escribir en la base.</summary>
    private sealed class BitacoraFalsa : IServicioBitacora
    {
        public List<EventoBitacora> Eventos { get; } = [];

        public Task<bool> RegistrarAsync(EventoBitacora evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.FromResult(true);
        }
    }
}
