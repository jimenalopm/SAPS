using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class TamanosPorCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsParaBebida",
                schema: "soda",
                table: "tb_Tamano",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tb_CategoriaTamano",
                schema: "soda",
                columns: table => new
                {
                    idCategoria = table.Column<int>(type: "int", nullable: false),
                    idTamano = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriaTamano", x => new { x.idCategoria, x.idTamano });
                    table.ForeignKey(
                        name: "FK_CategoriaTamano_Categoria",
                        column: x => x.idCategoria,
                        principalSchema: "soda",
                        principalTable: "tb_Categoria",
                        principalColumn: "idCategoria",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CategoriaTamano_Tamano",
                        column: x => x.idTamano,
                        principalSchema: "soda",
                        principalTable: "tb_Tamano",
                        principalColumn: "idTamano",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriaTamano_idTamano",
                schema: "soda",
                table: "tb_CategoriaTamano",
                column: "idTamano");

            // HU-007: los tamaños que ningún producto usa (600ml, 1L...) pasan a ser tamaños de bebida.
            migrationBuilder.Sql(@"
UPDATE t
SET t.EsParaBebida = 1
FROM soda.tb_Tamano t
WHERE t.Nombre NOT LIKE 'Peque%'
  AND t.Nombre NOT IN ('Mediano', 'Grande')
  AND NOT EXISTS (SELECT 1 FROM soda.tb_Precio p WHERE p.idTamano = t.idTamano);");

            // HU-007: cada categoría conserva los tamaños que sus productos ya usan hoy.
            migrationBuilder.Sql(@"
INSERT INTO soda.tb_CategoriaTamano (idCategoria, idTamano)
SELECT DISTINCT pr.idCategoria, p.idTamano
FROM soda.tb_Precio p
JOIN soda.tb_Producto pr ON pr.idProducto = p.idProducto
WHERE p.idTamano IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_CategoriaTamano",
                schema: "soda");

            migrationBuilder.DropColumn(
                name: "EsParaBebida",
                schema: "soda",
                table: "tb_Tamano");
        }
    }
}
