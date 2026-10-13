namespace SAPS.Web.Models.Identity;

/// <summary>
/// HU-001 (RNF-006): a qué colaborador de rrhh.tb_Colaborador pertenece una cuenta de SAPS.
/// Vínculo 1 a 1 (ver índice único en VinculoUsuarioColaboradorConfiguration): un colaborador
/// puede estar vinculado a como máximo una cuenta.
/// </summary>
public class VinculoUsuarioColaborador
{
    public string IdUsuario { get; set; } = "";
    public int IdColaborador { get; set; }
    public DateTime FechaVinculacion { get; set; }
}
