﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AgregarOrdenTamano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Orden",
                schema: "soda",
                table: "tb_Tamano",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // HU-007: los tamaños que ya existen reciben su posición de menor a mayor.
            // Pequeño = 1, Mediano = 2, Grande = 3; el resto (600ml, 1L...) queda después, por orden de creación.
            migrationBuilder.Sql(@"
UPDATE soda.tb_Tamano
SET Orden = CASE
        WHEN Nombre LIKE 'Peque%' THEN 1
        WHEN Nombre = 'Mediano'   THEN 2
        WHEN Nombre = 'Grande'    THEN 3
        ELSE 3 + idTamano
    END
WHERE Orden = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Orden",
                schema: "soda",
                table: "tb_Tamano");
        }
    }
}
