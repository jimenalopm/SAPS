using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SAPS.Web.Data.ApplicationDbContext))]
    [Migration("20261009120000_TiposDeBebida")]
    public partial class TiposDeBebida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HU-007: los tipos de bebida pasan de una restricción fija (CHECK) a una tabla administrable.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bebida_Tipo",
                schema: "soda",
                table: "tb_Bebida");

            migrationBuilder.CreateTable(
                name: "tb_TipoBebida",
                schema: "soda",
                columns: table => new
                {
                    idTipoBebida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoBebida", x => x.idTipoBebida);
                });

            migrationBuilder.InsertData(
                schema: "soda",
                table: "tb_TipoBebida",
                columns: new[] { "idTipoBebida", "Nombre" },
                values: new object[,]
                {
                    { 1, "Gaseosa" },
                    { 2, "Embotellada" },
                    { 3, "Energizante" },
                    { 4, "Jugo" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_TipoBebida_Nombre",
                schema: "soda",
                table: "tb_TipoBebida",
                column: "Nombre",
                unique: true);

            // Las bebidas que ya existen conservan su tipo aunque no sea uno de los cuatro iniciales.
            migrationBuilder.Sql(@"
INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo)
SELECT DISTINCT b.Tipo, 1
FROM soda.tb_Bebida b
WHERE NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida t WHERE t.Nombre = b.Tipo);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_TipoBebida",
                schema: "soda");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bebida_Tipo",
                schema: "soda",
                table: "tb_Bebida",
                sql: "[Tipo] IN ('Gaseosa','Embotellada','Energizante','Jugo')");
        }
    }
}
