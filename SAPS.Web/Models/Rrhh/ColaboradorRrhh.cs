namespace SAPS.Web.Models.Rrhh;

/// <summary>rrhh.tb_Colaborador: lista de colaboradores que administra RH desde SAPS (RNF-006).</summary>
public class ColaboradorRrhh
{
    public int IdColaborador { get; set; }
    public string Codigo { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public bool EstaActivo { get; set; } = true;
    public string? RutaFoto { get; set; }
    public DateTime FechaRegistro { get; set; }
}
