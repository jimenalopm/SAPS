using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <inheritdoc />
    public partial class CrearTriggersAuditoriaPrecio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HU-015: registrar en la bitácora los cambios de precio, vengan de la pantalla o directo de la BD.
            // CREATE FUNCTION / CREATE TRIGGER deben ser la primera instrucción del lote, por eso van dentro de EXEC.

            // Usuario que hizo el cambio. La aplicación lo pasa con SET CONTEXT_INFO 'idUsuario|nombre|rol'
            // (CONTEXT_INFO existe en SQL Server 2012; SESSION_CONTEXT no). Sin contexto, devuelve el usuario de la BD.
            migrationBuilder.Sql(@"
EXEC(N'
CREATE FUNCTION [soda].[fn_UsuarioContexto]()
RETURNS TABLE
AS
RETURN
(
    SELECT
        CASE WHEN y.p1 > 0 THEN SUBSTRING(y.c, 1, y.p1 - 1) END AS idUsuario,
        CASE WHEN y.p1 > 0
             THEN SUBSTRING(y.c, y.p1 + 1, CASE WHEN y.p2 > 0 THEN y.p2 - y.p1 - 1 ELSE 128 END)
             ELSE SUSER_SNAME() END AS NombreUsuario,
        CASE WHEN y.p2 > 0 THEN SUBSTRING(y.c, y.p2 + 1, 128) END AS Rol
    FROM (
        SELECT x.c,
               CHARINDEX(''|'', x.c) AS p1,
               CHARINDEX(''|'', x.c, CHARINDEX(''|'', x.c) + 1) AS p2
        FROM (SELECT REPLACE(CAST(CONTEXT_INFO() AS varchar(128)), CHAR(0), '''') AS c) x
    ) y
);');");

            // tb_Precio: la pantalla cierra la fila anterior e inserta una nueva (INSERT); un UPDATE directo del monto también se registra.
            migrationBuilder.Sql(@"
EXEC(N'
CREATE TRIGGER [soda].[tr_AuditoriaPrecio]
ON [soda].[tb_Precio]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        -- Cambio directo del monto sobre la misma fila (p. ej. un UPDATE hecho en la BD).
        IF NOT UPDATE([Monto]) RETURN;

        INSERT INTO [soda].[tb_Bitacora] ([Modulo], [TipoEvento], [Recurso], [IdRegistro], [EsExitoso], [idUsuario], [NombreUsuario], [Rol], [ValorAnterior], [ValorNuevo], [Descripcion])
        SELECT ''Catalogo'', ''CambioPrecio'', ''tb_Precio'', CAST(i.[idPrecio] AS varchar(50)), 1,
               u.[idUsuario], u.[NombreUsuario], u.[Rol],
               CAST(d.[Monto] AS varchar(500)), CAST(i.[Monto] AS varchar(500)),
               LEFT(N''Producto: '' + p.[Nombre] + N'' | Tamaño: '' + ISNULL(t.[Nombre], N''Único''), 300)
        FROM inserted i
        INNER JOIN deleted d ON d.[idPrecio] = i.[idPrecio]
        INNER JOIN [soda].[tb_Producto] p ON p.[idProducto] = i.[idProducto]
        LEFT JOIN [soda].[tb_Tamano] t ON t.[idTamano] = i.[idTamano]
        CROSS APPLY [soda].[fn_UsuarioContexto]() u
        WHERE i.[Monto] <> d.[Monto];
    END
    ELSE
    BEGIN
        -- La pantalla cierra la fila anterior (EstaActivo = 0) e inserta una fila nueva activa.
        -- Si hay una fila cerrada del mismo producto y tamaño con otro monto, es un cambio de precio.
        -- Sin fila anterior es un alta (producto o tamaño nuevo) y no se registra.
        INSERT INTO [soda].[tb_Bitacora] ([Modulo], [TipoEvento], [Recurso], [IdRegistro], [EsExitoso], [idUsuario], [NombreUsuario], [Rol], [ValorAnterior], [ValorNuevo], [Descripcion])
        SELECT ''Catalogo'', ''CambioPrecio'', ''tb_Precio'', CAST(i.[idPrecio] AS varchar(50)), 1,
               u.[idUsuario], u.[NombreUsuario], u.[Rol],
               CAST(prev.[Monto] AS varchar(500)), CAST(i.[Monto] AS varchar(500)),
               LEFT(N''Producto: '' + p.[Nombre] + N'' | Tamaño: '' + ISNULL(t.[Nombre], N''Único''), 300)
        FROM inserted i
        INNER JOIN [soda].[tb_Producto] p ON p.[idProducto] = i.[idProducto]
        LEFT JOIN [soda].[tb_Tamano] t ON t.[idTamano] = i.[idTamano]
        CROSS APPLY (
            SELECT TOP (1) a.[Monto]
            FROM [soda].[tb_Precio] a
            WHERE a.[idProducto] = i.[idProducto]
              AND (a.[idTamano] = i.[idTamano] OR (a.[idTamano] IS NULL AND i.[idTamano] IS NULL))
              AND a.[idPrecio] <> i.[idPrecio]
              AND a.[EstaActivo] = 0
            ORDER BY a.[FechaVigenciaHasta] DESC, a.[idPrecio] DESC
        ) prev
        CROSS APPLY [soda].[fn_UsuarioContexto]() u
        WHERE i.[EstaActivo] = 1 AND prev.[Monto] <> i.[Monto];
    END
END');");

            // tb_Bebida: el precio se actualiza sobre la misma fila.
            migrationBuilder.Sql(@"
EXEC(N'
CREATE TRIGGER [soda].[tr_AuditoriaBebida]
ON [soda].[tb_Bebida]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE([Precio]) RETURN;

    INSERT INTO [soda].[tb_Bitacora] ([Modulo], [TipoEvento], [Recurso], [IdRegistro], [EsExitoso], [idUsuario], [NombreUsuario], [Rol], [ValorAnterior], [ValorNuevo], [Descripcion])
    SELECT ''Catalogo'', ''CambioPrecio'', ''tb_Bebida'', CAST(i.[idBebida] AS varchar(50)), 1,
           u.[idUsuario], u.[NombreUsuario], u.[Rol],
           CAST(d.[Precio] AS varchar(500)), CAST(i.[Precio] AS varchar(500)),
           LEFT(N''Bebida: '' + i.[Nombre] + N'' | Tamaño: '' + ISNULL(t.[Nombre], N''Sin tamaño''), 300)
    FROM inserted i
    INNER JOIN deleted d ON d.[idBebida] = i.[idBebida]
    LEFT JOIN [soda].[tb_Tamano] t ON t.[idTamano] = i.[idTamano]
    CROSS APPLY [soda].[fn_UsuarioContexto]() u
    WHERE i.[Precio] <> d.[Precio];
END');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER [soda].[tr_AuditoriaBebida];");
            migrationBuilder.Sql("DROP TRIGGER [soda].[tr_AuditoriaPrecio];");
            migrationBuilder.Sql("DROP FUNCTION [soda].[fn_UsuarioContexto];");
        }
    }
}
