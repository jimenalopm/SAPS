using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class InicialEstandares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "soda");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tb_Categoria",
                schema: "soda",
                columns: table => new
                {
                    idCategoria = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categoria", x => x.idCategoria);
                });

            migrationBuilder.CreateTable(
                name: "tb_Tamano",
                schema: "soda",
                columns: table => new
                {
                    idTamano = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tamano", x => x.idTamano);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tb_Pedido",
                schema: "soda",
                columns: table => new
                {
                    idPedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TokenRegistro = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoColaborador = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    NombreColaborador = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    idUsuario = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FechaRegistroUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoComida = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    EsPrueba = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedido", x => x.idPedido);
                    table.CheckConstraint("CK_Pedido_TipoComida", "[TipoComida] IN ('Desayuno','Almuerzo','Merienda')");
                    table.CheckConstraint("CK_Pedido_Total", "[Total] > 0");
                    table.ForeignKey(
                        name: "FK_Pedido_Usuario",
                        column: x => x.idUsuario,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_Producto",
                schema: "soda",
                columns: table => new
                {
                    idProducto = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    idCategoria = table.Column<int>(type: "int", nullable: false),
                    EsEspecial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TieneTamano = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Producto", x => x.idProducto);
                    table.ForeignKey(
                        name: "FK_Producto_Categoria",
                        column: x => x.idCategoria,
                        principalSchema: "soda",
                        principalTable: "tb_Categoria",
                        principalColumn: "idCategoria",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_Bebida",
                schema: "soda",
                columns: table => new
                {
                    idBebida = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    idTamano = table.Column<int>(type: "int", nullable: true),
                    Precio = table.Column<int>(type: "int", nullable: false),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bebida", x => x.idBebida);
                    table.CheckConstraint("CK_Bebida_Precio", "[Precio] > 0");
                    table.CheckConstraint("CK_Bebida_Tipo", "[Tipo] IN ('Gaseosa','Embotellada','Energizante','Jugo')");
                    table.ForeignKey(
                        name: "FK_Bebida_Tamano",
                        column: x => x.idTamano,
                        principalSchema: "soda",
                        principalTable: "tb_Tamano",
                        principalColumn: "idTamano",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_Precio",
                schema: "soda",
                columns: table => new
                {
                    idPrecio = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idProducto = table.Column<int>(type: "int", nullable: false),
                    idTamano = table.Column<int>(type: "int", nullable: true),
                    Monto = table.Column<int>(type: "int", nullable: false),
                    FechaVigenciaDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaVigenciaHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    EstaActivo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Precio", x => x.idPrecio);
                    table.CheckConstraint("CK_Precio_Monto", "[Monto] > 0");
                    table.CheckConstraint("CK_Precio_Vigencia", "[FechaVigenciaHasta] IS NULL OR [FechaVigenciaHasta] >= [FechaVigenciaDesde]");
                    table.ForeignKey(
                        name: "FK_Precio_Producto",
                        column: x => x.idProducto,
                        principalSchema: "soda",
                        principalTable: "tb_Producto",
                        principalColumn: "idProducto",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Precio_Tamano",
                        column: x => x.idTamano,
                        principalSchema: "soda",
                        principalTable: "tb_Tamano",
                        principalColumn: "idTamano",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tb_DetallePedido",
                schema: "soda",
                columns: table => new
                {
                    idDetallePedido = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    idPedido = table.Column<int>(type: "int", nullable: false),
                    idPrecio = table.Column<int>(type: "int", nullable: true),
                    idBebida = table.Column<int>(type: "int", nullable: true),
                    NombreArticulo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    NombreTamano = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    PrecioUnitario = table.Column<int>(type: "int", nullable: false),
                    Subtotal = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallePedido", x => x.idDetallePedido);
                    table.CheckConstraint("CK_DetallePedido_Articulo", "([idPrecio] IS NOT NULL AND [idBebida] IS NULL) OR ([idPrecio] IS NULL AND [idBebida] IS NOT NULL)");
                    table.CheckConstraint("CK_DetallePedido_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallePedido_PrecioUnitario", "[PrecioUnitario] > 0");
                    table.CheckConstraint("CK_DetallePedido_Subtotal", "[Subtotal] = CAST([Cantidad] AS bigint) * [PrecioUnitario]");
                    table.ForeignKey(
                        name: "FK_DetallePedido_Bebida",
                        column: x => x.idBebida,
                        principalSchema: "soda",
                        principalTable: "tb_Bebida",
                        principalColumn: "idBebida",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallePedido_Pedido",
                        column: x => x.idPedido,
                        principalSchema: "soda",
                        principalTable: "tb_Pedido",
                        principalColumn: "idPedido",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DetallePedido_Precio",
                        column: x => x.idPrecio,
                        principalSchema: "soda",
                        principalTable: "tb_Precio",
                        principalColumn: "idPrecio",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bebida_idTamano",
                schema: "soda",
                table: "tb_Bebida",
                column: "idTamano");

            migrationBuilder.CreateIndex(
                name: "UQ_Categoria_Nombre",
                schema: "soda",
                table: "tb_Categoria",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedido_idBebida",
                schema: "soda",
                table: "tb_DetallePedido",
                column: "idBebida");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedido_idPedido",
                schema: "soda",
                table: "tb_DetallePedido",
                column: "idPedido");

            migrationBuilder.CreateIndex(
                name: "IX_DetallePedido_idPrecio",
                schema: "soda",
                table: "tb_DetallePedido",
                column: "idPrecio");

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_CodigoColaborador_FechaRegistroUtc",
                schema: "soda",
                table: "tb_Pedido",
                columns: new[] { "CodigoColaborador", "FechaRegistroUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedido_idUsuario",
                schema: "soda",
                table: "tb_Pedido",
                column: "idUsuario");

            migrationBuilder.CreateIndex(
                name: "UQ_Pedido_TokenRegistro",
                schema: "soda",
                table: "tb_Pedido",
                column: "TokenRegistro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Precio_idProducto_idTamano",
                schema: "soda",
                table: "tb_Precio",
                columns: new[] { "idProducto", "idTamano" },
                unique: true,
                filter: "[EstaActivo] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Precio_idTamano",
                schema: "soda",
                table: "tb_Precio",
                column: "idTamano");

            migrationBuilder.CreateIndex(
                name: "IX_Producto_idCategoria",
                schema: "soda",
                table: "tb_Producto",
                column: "idCategoria");

            migrationBuilder.CreateIndex(
                name: "UQ_Tamano_Nombre",
                schema: "soda",
                table: "tb_Tamano",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "tb_DetallePedido",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "tb_Bebida",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "tb_Pedido",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "tb_Precio",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "tb_Producto",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "tb_Tamano",
                schema: "soda");

            migrationBuilder.DropTable(
                name: "tb_Categoria",
                schema: "soda");
        }
    }
}
