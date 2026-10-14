using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class BloquearBitacoraSoloLectura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HU-004 criterios 6 y 7 / HU-015 criterio 5: la bitácora es de solo lectura para todos.
            // Capa 1: trigger que rechaza UPDATE y DELETE sin importar quién los ejecute (EBD09).
            // Va dentro de EXEC porque CREATE TRIGGER debe ser la primera instrucción del lote
            // y el script idempotente de EF lo envuelve en un IF.
            migrationBuilder.Sql(@"
EXEC(N'
CREATE TRIGGER [soda].[tr_BitacoraSoloLectura]
ON [soda].[tb_Bitacora]
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, N''La bitácora es de solo lectura: no se pueden modificar ni eliminar sus registros.'', 1;
END');");

            // Capa 2: permisos. Las cuentas normales de la BD no pueden modificar ni borrar filas.
            migrationBuilder.Sql("DENY UPDATE, DELETE ON [soda].[tb_Bitacora] TO [public];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE UPDATE, DELETE ON [soda].[tb_Bitacora] FROM [public];");
            migrationBuilder.Sql("DROP TRIGGER [soda].[tr_BitacoraSoloLectura];");
        }
    }
}
