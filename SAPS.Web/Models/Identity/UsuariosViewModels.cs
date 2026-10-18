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

    /// <summary>Nombre del rol para mostrar en pantalla; el valor interno (p. ej. "RecursosHumanos") no cambia.</summary>
    public static string NombreVisible(string? rol) => rol switch
    {
        "RecursosHumanos" => "Recursos Humanos",
        null or "" => "Sin rol asignado",
        _ => rol,
    };

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [Display(Name = "Correo de empleado")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "La contraseña inicial es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña inicial")]
    public string ContrasenaInicial { get; set; } = "";

    [Required(ErrorMessage = "Seleccione un rol.")]
    public string Rol { get; set; } = "";

    // Obligatorio en los 3 roles, incluido Administrador: toda cuenta nueva declara a qué
    // colaborador pertenece (criterio de aceptación 2, RNF-006).
    [Required(ErrorMessage = "Seleccione el colaborador que usará esta cuenta.")]
    [Display(Name = "Colaborador")]
    public int? IdColaborador { get; set; }

    public List<ColaboradorCandidato> ColaboradoresDisponibles { get; set; } = [];
}

// ---------- Edición ----------
public class EditarUsuarioViewModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [Display(Name = "Correo de empleado")]
    public string Correo { get; set; } = "";

    [Required(ErrorMessage = "Seleccione un rol.")]
    public string Rol { get; set; } = "";

    [Required(ErrorMessage = "Seleccione el colaborador que usará esta cuenta.")]
    [Display(Name = "Colaborador")]
    public int? IdColaborador { get; set; }

    /// <summary>Activos sin cuenta, más el colaborador que esta cuenta ya tiene vinculado.</summary>
    public List<ColaboradorCandidato> ColaboradoresDisponibles { get; set; } = [];

    /// <summary>Es la cuenta del Administrador que tiene la sesión abierta: no puede cambiarse su propio rol.</summary>
    public bool EsCuentaPropia { get; set; }
}
