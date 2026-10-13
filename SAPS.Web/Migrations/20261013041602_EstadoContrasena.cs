using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class EstadoContrasena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_EstadoContrasena",
                columns: table => new
                {
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FechaUltimoCambio = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoContrasena", x => x.idUsuario);
                    table.ForeignKey(
                        name: "FK_EstadoContrasena_Usuario",
                        column: x => x.idUsuario,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_EstadoContrasena");
        }
    }
}
