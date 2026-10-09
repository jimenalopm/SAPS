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
                table: "tb_Pedido");

            migrationBuilder.AlterColumn<string>(
                name: "TipoComida",
                table: "tb_Pedido",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TipoComida",
                table: "tb_Pedido",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Pedido_TipoComida",
                table: "tb_Pedido",
                sql: "[TipoComida] IN ('Desayuno','Almuerzo','Merienda')");
        }
    }
}
