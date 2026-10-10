using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class ColaboradoresRrhh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rrhh");

            migrationBuilder.CreateTable(
                name: "tb_Colaborador",
                schema: "rrhh",
                columns: table => new
                {
                    idColaborador = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    NombreCompleto = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RutaFoto = table.Column<string>(type: "varchar(260)", unicode: false, maxLength: 260, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colaborador", x => x.idColaborador);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_Colaborador_Codigo",
                schema: "rrhh",
                table: "tb_Colaborador",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_Colaborador",
                schema: "rrhh");
        }
    }
}
