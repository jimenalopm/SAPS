using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class CrearBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_Bitacora",
                schema: "soda",
                columns: table => new
                {
                    idBitacora = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FechaHoraUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Modulo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TipoEvento = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Recurso = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    IdRegistro = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    EsExitoso = table.Column<bool>(type: "bit", nullable: false),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NombreUsuario = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    Rol = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    UsuarioBd = table.Column<string>(type: "varchar(128)", unicode: false, maxLength: 128, nullable: false, defaultValueSql: "SUSER_SNAME()"),
                    DireccionIp = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true),
                    Dispositivo = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: true),
                    ValorAnterior = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    ValorNuevo = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Descripcion = table.Column<string>(type: "varchar(300)", unicode: false, maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora", x => x.idBitacora);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_FechaHoraUtc",
                schema: "soda",
                table: "tb_Bitacora",
                column: "FechaHoraUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_idUsuario",
                schema: "soda",
                table: "tb_Bitacora",
                column: "idUsuario");

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_TipoEvento_FechaHoraUtc",
                schema: "soda",
                table: "tb_Bitacora",
                columns: new[] { "TipoEvento", "FechaHoraUtc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_Bitacora",
                schema: "soda");
        }
    }
}
