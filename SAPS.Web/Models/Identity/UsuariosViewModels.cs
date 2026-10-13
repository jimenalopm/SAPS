using System.ComponentModel.DataAnnotations;
using SAPS.Web.Services.Identity;

namespace SAPS.Web.Models.Identity;

// ---------- Listado ----------
public class UsuarioListadoViewModel
{
    public string Id { get; set; } = "";
    public string Correo { get; set; } = "";
    public string Rol { get; set; } = "";
    public string? ColaboradorNombre { get; set; }

    /// <summary>"Activa" | "Desactivada" | "Bloqueada temporalmente" (ver UsuariosController.CalcularEstado).</summary>
    public string Estado { get; set; } = "";
}

// ---------- Alta ----------
public class CrearUsuarioViewModel
{
    public static readonly string[] RolesDisponibles = ["Administrador", "RecursosHumanos", "Soda"];

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingresá un correo válido.")]
    [Display(Name = "Correo de empleado")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string ContrasenaInicial { get; set; } = "";

    [Required(ErrorMessage = "Seleccioná un rol.")]
    public string Rol { get; set; } = "";

    // Obligatorio en los 3 roles, incluido Administrador: toda cuenta nueva declara a qué
    // colaborador de RRHH pertenece (criterio de aceptación 2, RNF-006).
    [Required(ErrorMessage = "Seleccioná el colaborador de RRHH al que pertenece esta cuenta.")]
    [Display(Name = "Colaborador de RRHH")]
    public int? IdColaborador { get; set; }

    public List<ColaboradorCandidato> ColaboradoresDisponibles { get; set; } = [];
}
