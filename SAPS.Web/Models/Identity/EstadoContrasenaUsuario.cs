namespace SAPS.Web.Models.Identity;

/// <summary>dbo.tb_EstadoContrasena: fecha del último cambio de contraseña de cada cuenta (HU-001, criterio 7).</summary>
public class EstadoContrasenaUsuario
{
    public string IdUsuario { get; set; } = "";
    public DateTime FechaUltimoCambio { get; set; }
}
