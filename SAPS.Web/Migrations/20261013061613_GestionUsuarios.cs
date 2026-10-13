using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class GestionUsuarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiereCambioInicial",
                table: "tb_EstadoContrasena",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "tb_UsuarioColaborador",
                columns: table => new
                {
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    idColaborador = table.Column<int>(type: "int", nullable: false),
                    FechaVinculacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioColaborador", x => x.idUsuario);
                    table.ForeignKey(
                        name: "FK_UsuarioColaborador_Colaborador",
                        column: x => x.idColaborador,
                        principalSchema: "rrhh",
                        principalTable: "tb_Colaborador",
                        principalColumn: "idColaborador",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioColaborador_Usuario",
                        column: x => x.idUsuario,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_UsuarioColaborador_idColaborador",
                table: "tb_UsuarioColaborador",
                column: "idColaborador",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_UsuarioColaborador");

            migrationBuilder.DropColumn(
                name: "RequiereCambioInicial",
                table: "tb_EstadoContrasena");
        }
    }
}
