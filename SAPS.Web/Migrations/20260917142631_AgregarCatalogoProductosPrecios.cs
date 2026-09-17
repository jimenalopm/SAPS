using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCatalogoProductosPrecios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tb_Categoria",
                columns: table => new
                {
                    idCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombreCategoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Categoria", x => x.idCategoria);
                });

            migrationBuilder.CreateTable(
                name: "tb_Tamano",
                columns: table => new
                {
                    idTamano = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombreTamano = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Tamano", x => x.idTamano);
                });

            migrationBuilder.CreateTable(
                name: "tb_Producto",
                columns: table => new
                {
                    idProducto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombreProducto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    idCategoria = table.Column<int>(type: "int", nullable: false),
                    esEspecial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    requiereTamano = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Producto", x => x.idProducto);
                    table.ForeignKey(
                        name: "FK_Producto_Categoria",
                        column: x => x.idCategoria,
                        principalTable: "tb_Categoria",
                        principalColumn: "idCategoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_Bebida",
                columns: table => new
                {
                    idBebida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombreBebida = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    tipoBebida = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    idTamano = table.Column<int>(type: "int", nullable: true),
                    precio = table.Column<int>(type: "int", nullable: false),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Bebida", x => x.idBebida);
                    table.CheckConstraint("CK_Bebida_PrecioPositivo", "[precio] > 0");
                    table.CheckConstraint("CK_Bebida_TipoBebida", "[tipoBebida] IN ('Gaseosa','Embotellada','Energizante','Jugo')");
                    table.ForeignKey(
                        name: "FK_Bebida_Tamano",
                        column: x => x.idTamano,
                        principalTable: "tb_Tamano",
                        principalColumn: "idTamano",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_Precio",
                columns: table => new
                {
                    idPrecio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    idTamano = table.Column<int>(type: "int", nullable: true),
                    precio = table.Column<int>(type: "int", nullable: false),
                    fechaVigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    fechaVigenciaHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_Precio", x => x.idPrecio);
                    table.CheckConstraint("CK_Precio_PrecioPositivo", "[precio] > 0");
                    table.ForeignKey(
                        name: "FK_Precio_Producto",
                        column: x => x.idProducto,
                        principalTable: "tb_Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Precio_Tamano",
                        column: x => x.idTamano,
                        principalTable: "tb_Tamano",
                        principalColumn: "idTamano",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bebida_Tamano",
                table: "tb_Bebida",
                column: "idTamano");

            migrationBuilder.CreateIndex(
                name: "UQ_Categoria_NombreCategoria",
                table: "tb_Categoria",
                column: "nombreCategoria",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Precio_Producto_Tamano",
                table: "tb_Precio",
                columns: new[] { "idProducto", "idTamano" },
                unique: true,
                filter: "[activo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_tb_Precio_idTamano",
                table: "tb_Precio",
                column: "idTamano");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_Categoria",
                table: "tb_Producto",
                column: "idCategoria");

            migrationBuilder.CreateIndex(
                name: "UQ_Tamano_NombreTamano",
                table: "tb_Tamano",
                column: "nombreTamano",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_Bebida");

            migrationBuilder.DropTable(
                name: "tb_Precio");

            migrationBuilder.DropTable(
                name: "tb_Producto");

            migrationBuilder.DropTable(
                name: "tb_Tamano");

            migrationBuilder.DropTable(
                name: "tb_Categoria");
        }
    }
}
