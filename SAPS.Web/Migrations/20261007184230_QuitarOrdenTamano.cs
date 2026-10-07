using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class QuitarOrdenTamano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                schema: "soda",
                table: "tb_Tamano");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                schema: "soda",
                table: "tb_Tamano",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
