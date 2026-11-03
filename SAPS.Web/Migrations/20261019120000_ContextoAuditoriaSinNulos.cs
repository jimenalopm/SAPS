using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class ContextoAuditoriaSinNulos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HU-015 (hallazgo de las pruebas funcionales del Sprint 2): CONTEXT_INFO siempre mide 128 bytes y se rellena
            // con 0x00. Con una intercalación de Windows (p. ej. Modern_Spanish_CI_AS) REPLACE ignora CHAR(0) y no quita el
            // relleno, así que el rol llegaba con ~100 bytes y no cabía en tb_Bitacora.Rol varchar(50): todo cambio de
            // precio hecho desde la pantalla fallaba con "String or binary data would be truncated".
            // Con una intercalación binaria REPLACE sí reconoce CHAR(0).
            migrationBuilder.Sql(FuncionUsuarioContexto("ALTER", " COLLATE Latin1_General_BIN"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(FuncionUsuarioContexto("ALTER", ""));
        }

        private static string FuncionUsuarioContexto(string accion, string intercalacion) => $@"
EXEC(N'
{accion} FUNCTION [soda].[fn_UsuarioContexto]()
RETURNS TABLE
AS
RETURN
(
    SELECT
        CASE WHEN y.p1 > 0 THEN SUBSTRING(y.c, 1, y.p1 - 1) END AS idUsuario,
        CASE WHEN y.p1 > 0
             THEN SUBSTRING(y.c, y.p1 + 1, CASE WHEN y.p2 > 0 THEN y.p2 - y.p1 - 1 ELSE 128 END)
             ELSE SUSER_SNAME() END AS NombreUsuario,
        CASE WHEN y.p2 > 0 THEN SUBSTRING(y.c, y.p2 + 1, 128) END AS Rol
    FROM (
        SELECT x.c,
               CHARINDEX(''|'', x.c) AS p1,
               CHARINDEX(''|'', x.c, CHARINDEX(''|'', x.c) + 1) AS p2
        FROM (SELECT REPLACE(CAST(CONTEXT_INFO() AS varchar(128)){intercalacion}, CHAR(0), '''') AS c) x
    ) y
);');";
    }
}
