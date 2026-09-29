using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRegistroPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_Pedido",
                columns: table => new
                {
                    IdPedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenRegistro = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoColaborador = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NombreColaborador = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IdUsuarioRegistro = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaRegistroUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoComida = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    EsPrueba = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Pedido", x => x.IdPedido);
                    table.CheckConstraint("CK_Pedido_TipoComida", "[TipoComida] IN ('Desayuno','Almuerzo','Merienda')");
                    table.CheckConstraint("CK_Pedido_Total", "[Total] > 0");
                    table.ForeignKey(
                        name: "FK_tb_Pedido_AspNetUsers_IdUsuarioRegistro",
                        column: x => x.IdUsuarioRegistro,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_DetallePedido",
                columns: table => new
                {
                    IdDetallePedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdPedido = table.Column<int>(type: "int", nullable: false),
                    IdPrecio = table.Column<int>(type: "int", nullable: true),
                    IdBebida = table.Column<int>(type: "int", nullable: true),
                    NombreArticulo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NombreTamano = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_DetallePedido", x => x.IdDetallePedido);
                    table.CheckConstraint("CK_DetallePedido_Articulo", "([IdPrecio] IS NOT NULL AND [IdBebida] IS NULL) OR ([IdPrecio] IS NULL AND [IdBebida] IS NOT NULL)");
                    table.CheckConstraint("CK_DetallePedido_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallePedido_Precio", "[PrecioUnitario] > 0");
                    table.CheckConstraint("CK_DetallePedido_Subtotal", "[Subtotal] = CAST([Cantidad] AS bigint) * [PrecioUnitario]");
                    table.ForeignKey(
                        name: "FK_tb_DetallePedido_tb_Bebida_IdBebida",
                        column: x => x.IdBebida,
                        principalTable: "tb_Bebida",
                        principalColumn: "idBebida",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_DetallePedido_tb_Pedido_IdPedido",
                        column: x => x.IdPedido,
                        principalTable: "tb_Pedido",
                        principalColumn: "IdPedido",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tb_DetallePedido_tb_Precio_IdPrecio",
                        column: x => x.IdPrecio,
                        principalTable: "tb_Precio",
                        principalColumn: "idPrecio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tb_DetallePedido_IdBebida",
                table: "tb_DetallePedido",
                column: "IdBebida");

            migrationBuilder.CreateIndex(
                name: "IX_tb_DetallePedido_IdPedido",
                table: "tb_DetallePedido",
                column: "IdPedido");

            migrationBuilder.CreateIndex(
                name: "IX_tb_DetallePedido_IdPrecio",
                table: "tb_DetallePedido",
                column: "IdPrecio");

            migrationBuilder.CreateIndex(
                name: "IX_tb_Pedido_CodigoColaborador_FechaRegistroUtc",
                table: "tb_Pedido",
                columns: new[] { "CodigoColaborador", "FechaRegistroUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_tb_Pedido_IdUsuarioRegistro",
                table: "tb_Pedido",
                column: "IdUsuarioRegistro");

            migrationBuilder.CreateIndex(
                name: "IX_tb_Pedido_TokenRegistro",
                table: "tb_Pedido",
                column: "TokenRegistro",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_DetallePedido");

            migrationBuilder.DropTable(
                name: "tb_Pedido");
        }
    }
}
