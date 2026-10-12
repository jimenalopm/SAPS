using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SAPS.Web.Data;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <summary>
    /// El tipo de comida de un pedido pasa a ser el nombre de una categoría del catálogo,
    /// por lo que deja de estar limitado a Desayuno/Almuerzo/Merienda.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261009230000_TipoComidaDesdeCatalogo")]
    public partial class TipoComidaDesdeCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Pedido_TipoComida",
                table: "tb_Pedido",
                schema: "soda");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComida",
                table: "tb_Pedido",
                schema: "soda",
                type: "varchar(50)",
                maxLength: 50,
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldUnicode: false,
                oldType: "varchar(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TipoComida",
                table: "tb_Pedido",
                schema: "soda",
                type: "varchar(20)",
                maxLength: 20,
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldUnicode: false,
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pedido_TipoComida",
                table: "tb_Pedido",
                schema: "soda",
                sql: "[TipoComida] IN ('Desayuno','Almuerzo','Merienda')");
        }
    }
}
