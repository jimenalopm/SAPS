using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SAPS.Tests.Helpers;
using SAPS.Web.Controllers;
using SAPS.Web.Models.Auditoria;
using SAPS.Web.Models.Catalogo;
using SAPS.Web.Models.Identity;
using SAPS.Web.Services.Auditoria;

namespace SAPS.Tests;

/// <summary>
/// Mejoras posteriores al Sprint 2: formato del correo de las cuentas, lo digitado en un inicio de sesión
/// fallido sin cuenta (puede ser la contraseña) y los mensajes del catálogo con conteos.
/// </summary>
public class MejorasUsuariosCatalogoTests
{
    // ---------- Correo de las cuentas ----------

    [Theory]
    [InlineData("nombre@recyplast.cr")]
    [InlineData("nombre.apellido@recyplast.com")]
    [InlineData("soda-01@correo.recyplast.co.cr")]
    [InlineData("rrhh+planilla@gmail.com")]
    [InlineData("persona@empresa.online")]
    public void Correo_ConFormatoReal_EsValido(string correo)
    {
        Assert.Empty(ErroresDeCorreo(correo));
    }

    [Theory]
    [InlineData("ejemplo@recyplast.commmmmmmmmmmmmmmmmm")]   // terminación de más de 6 letras
    [InlineData("ejemplo@recyplast")]                       // sin terminación
    [InlineData("ejemplo@recyplast.c")]                     // terminación de 1 letra
    [InlineData("ejemplo@recyplast.c0m")]                   // terminación con números
    [InlineData("ejemplo@@recyplast.cr")]
    [InlineData("ejemplo@recyplast..cr")]
    [InlineData("ejemplo@-recyplast.cr")]
    [InlineData(".ejemplo@recyplast.cr")]
    [InlineData("ejemplo.@recyplast.cr")]
    [InlineData("ejem..plo@recyplast.cr")]
    [InlineData("ejemplo recyplast@recyplast.cr")]
    [InlineData("ejemplo")]
    public void Correo_SinFormatoReal_EsInvalido(string correo)
    {
        Assert.Contains(ReglasCorreo.Mensaje, ErroresDeCorreo(correo));
    }

    [Fact]
    public void Correo_EditarUsaLaMismaRegla()
    {
        var modelo = new EditarUsuarioViewModel { Id = "1", Correo = "ejemplo@recyplast.commmmmmmmmm", Rol = "Soda", IdColaborador = 1 };
        var errores = new List<ValidationResult>();

        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);

        Assert.Contains(errores, e => e.ErrorMessage == ReglasCorreo.Mensaje);
    }

    private static List<string?> ErroresDeCorreo(string correo)
    {
        var modelo = new CrearUsuarioViewModel { Correo = correo, ContrasenaInicial = "Temporal123", Rol = "Soda", IdColaborador = 1 };
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores.Where(e => e.MemberNames.Contains(nameof(CrearUsuarioViewModel.Correo))).Select(e => e.ErrorMessage).ToList();
    }

    // ---------- Inicio de sesión fallido sin cuenta ----------

    [Fact]
    public void UsuarioVisible_IntentoFallidoSinCuenta_NoMuestraLoDigitado()
    {
        Assert.Equal(PresentacionBitacora.UsuarioNoReconocido,
            PresentacionBitacora.UsuarioVisible(TiposEventoBitacora.InicioSesion, false, null, "MiClave123"));
    }

    [Theory]
    [InlineData(true, null)]      // inicio exitoso
    [InlineData(false, "u-1")]    // fallido de una cuenta que sí existe
    public void UsuarioVisible_OtrosCasos_MuestraElUsuario(bool exitoso, string? idUsuario)
    {
        Assert.Equal("ADM001", PresentacionBitacora.UsuarioVisible(TiposEventoBitacora.InicioSesion, exitoso, idUsuario, "ADM001"));
    }

    [Fact]
    public async Task Bitacora_RegistroViejoConLoDigitado_NoSeMuestraNiSeEncuentraAlBuscar()
    {
        await using var db = TestServices.CrearContextoInMemory();
        db.Bitacoras.Add(new Bitacora
        {
            FechaHoraUtc = DateTime.UtcNow, Modulo = ModulosBitacora.Seguridad, TipoEvento = TiposEventoBitacora.InicioSesion,
            Recurso = RecursosBitacora.Sesion, EsExitoso = false, NombreUsuario = "MiClave123", UsuarioBd = "sa",
            Descripcion = "Credenciales incorrectas",
        });
        await db.SaveChangesAsync();
        var controlador = TestServices.ConContexto(new BitacoraController(db));

        var todo = Assert.IsType<BitacoraListadoViewModel>(Assert.IsType<ViewResult>(await controlador.Index(new BitacoraFiltros())).Model);
        var buscado = Assert.IsType<BitacoraListadoViewModel>(Assert.IsType<ViewResult>(
            await controlador.Index(new BitacoraFiltros { Usuario = "MiClave" })).Model);

        Assert.Equal(PresentacionBitacora.UsuarioNoReconocido, Assert.Single(todo.Filas).NombreUsuario);
        Assert.Empty(buscado.Filas);
    }

    // ---------- Mensajes del catálogo ----------

    [Theory]
    [InlineData(2, 0, "lo usan 2 productos activos. Desactívelos o cámbieles el tamaño primero.")]
    [InlineData(1, 0, "lo usa 1 producto activo. Desactívelo o cámbiele el tamaño primero.")]
    [InlineData(0, 3, "lo usan 3 bebidas activas. Desactívelas o cámbieles el tamaño primero.")]
    [InlineData(0, 1, "lo usa 1 bebida activa. Desactívela o cámbiele el tamaño primero.")]
    [InlineData(2, 1, "lo usan 2 productos activos y 1 bebida activa. Desactívelos o cámbieles el tamaño primero.")]
    public void Tamano_SoloMencionaLoQueLoUsaConSingularOPlural(int productos, int bebidas, string final)
    {
        var mensaje = MensajesCatalogo.NoDesactivarTamano("Pequeño", productos, bebidas);

        Assert.Equal($"No se puede desactivar el tamaño «Pequeño»: {final}", mensaje);
        Assert.DoesNotContain("(s)", mensaje);
        Assert.DoesNotContain(" 0 ", mensaje);
    }

    [Theory]
    [InlineData(1, "tiene 1 producto activo. Desactive primero ese producto.")]
    [InlineData(4, "tiene 4 productos activos. Desactive primero esos productos.")]
    public void Categoria_UsaSingularOPlural(int productos, string final)
    {
        Assert.Equal($"No se puede desactivar la categoría «Almuerzos»: {final}",
            MensajesCatalogo.NoDesactivarCategoria("Almuerzos", productos));
    }

    [Theory]
    [InlineData(1, "lo usa 1 bebida activa. Desactívela o cámbiele el tipo primero.")]
    [InlineData(5, "lo usan 5 bebidas activas. Desactívelas o cámbieles el tipo primero.")]
    public void TipoBebida_UsaSingularOPlural(int bebidas, string final)
    {
        Assert.Equal($"No se puede desactivar el tipo «Frescos»: {final}",
            MensajesCatalogo.NoDesactivarTipoBebida("Frescos", bebidas));
    }
}
