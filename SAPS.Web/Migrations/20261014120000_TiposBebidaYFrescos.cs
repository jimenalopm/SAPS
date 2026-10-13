using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAPS.Web.Migrations
{
    /// <summary>
    /// Reorganiza los tipos de bebida y saca el "Fresco" de la comida:
    ///  - Embotellada y Jugo se unen en "Tés y refrescos".
    ///  - Nace el tipo "Frescos"; el fresco con tamaños que estaba como producto de la categoría
    ///    "Fresco" pasa a ser la bebida "Frescos naturales" (tamaños de bebida "Vaso pequeño/mediano/grande").
    ///  - El producto y la categoría "Fresco" se eliminan si ningún pedido los usó; si alguno los usó
    ///    solo se desactivan, para no perder el historial.
    /// No cambia la estructura de las tablas, solo datos.
    /// </summary>
    public partial class TiposBebidaYFrescos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ScriptDatos);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Solo devuelve los nombres de los tipos. Lo que se eliminó (producto y categoría "Fresco")
            // y la separación original entre Embotellada y Jugo no se pueden reconstruir.
            migrationBuilder.Sql(@"
UPDATE soda.tb_Bebida SET Tipo = 'Embotellada' WHERE Tipo = N'Tés y refrescos';
UPDATE soda.tb_Bebida SET Tipo = 'Jugo' WHERE Tipo = N'Frescos';
UPDATE soda.tb_TipoBebida SET Nombre = 'Embotellada' WHERE idTipoBebida = 2 AND Nombre = N'Tés y refrescos';
UPDATE soda.tb_TipoBebida SET Nombre = 'Jugo' WHERE idTipoBebida = 4 AND Nombre = N'Frescos';");
        }

        // Se usa también para generar db/SAPS_DB_servidor.sql. Todo es idempotente: se puede repetir sin duplicar.
        internal const string ScriptDatos = @"
-- 1) Tipos de bebida: ""Frescos"" (era Jugo) y ""Tés y refrescos"" (era Embotellada, ahora también con los Jugo).
IF NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Frescos')
BEGIN
    IF EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Jugo')
        UPDATE soda.tb_TipoBebida SET Nombre = N'Frescos', EstaActivo = 1 WHERE Nombre = N'Jugo';
    ELSE
        INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo) VALUES (N'Frescos', 1);
END;

IF NOT EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Tés y refrescos')
BEGIN
    IF EXISTS (SELECT 1 FROM soda.tb_TipoBebida WHERE Nombre = N'Embotellada')
        UPDATE soda.tb_TipoBebida SET Nombre = N'Tés y refrescos', EstaActivo = 1 WHERE Nombre = N'Embotellada';
    ELSE
        INSERT INTO soda.tb_TipoBebida (Nombre, EstaActivo) VALUES (N'Tés y refrescos', 1);
END;

UPDATE soda.tb_Bebida SET Tipo = N'Tés y refrescos' WHERE Tipo IN (N'Embotellada', N'Jugo');
DELETE FROM soda.tb_TipoBebida
WHERE Nombre IN (N'Embotellada', N'Jugo')
  AND NOT EXISTS (SELECT 1 FROM soda.tb_Bebida b WHERE b.Tipo = soda.tb_TipoBebida.Nombre);

-- 2) Tamaños de bebida para el fresco.
INSERT INTO soda.tb_Tamano (Nombre, EsParaBebida, EstaActivo)
SELECT v.Nombre, 1, 1
FROM (VALUES (N'Vaso pequeño'), (N'Vaso mediano'), (N'Vaso grande')) AS v(Nombre)
WHERE NOT EXISTS (SELECT 1 FROM soda.tb_Tamano t WHERE t.Nombre = v.Nombre);

-- 3) El fresco que estaba como producto de la categoría ""Fresco"" pasa a ser la bebida ""Frescos naturales"".
DECLARE @idCategoriaFresco int = (SELECT idCategoria FROM soda.tb_Categoria WHERE Nombre = N'Fresco');
IF @idCategoriaFresco IS NOT NULL
BEGIN
    INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
    SELECT N'Frescos naturales', N'Frescos', tb.idTamano, MAX(pr.Monto), 1
    FROM soda.tb_Producto p
    JOIN soda.tb_Precio pr ON pr.idProducto = p.idProducto AND pr.EstaActivo = 1 AND pr.idTamano IS NOT NULL
    JOIN soda.tb_Tamano ta ON ta.idTamano = pr.idTamano
    JOIN soda.tb_Tamano tb ON tb.Nombre = N'Vaso ' + LOWER(ta.Nombre)
    WHERE p.idCategoria = @idCategoriaFresco
      AND p.Nombre IN (N'Fresco', N'Fresco natural')
      AND NOT EXISTS (SELECT 1 FROM soda.tb_Bebida x WHERE x.Nombre = N'Frescos naturales' AND x.idTamano = tb.idTamano)
    GROUP BY tb.idTamano;

    -- 4) Se elimina el fresco de comida si ningún pedido lo usó; si alguno lo usó, solo se desactiva.
    DECLARE @frescos TABLE (idProducto int NOT NULL PRIMARY KEY);
    INSERT INTO @frescos (idProducto)
    SELECT idProducto FROM soda.tb_Producto
    WHERE idCategoria = @idCategoriaFresco AND Nombre IN (N'Fresco', N'Fresco natural');

    DELETE FROM soda.tb_Precio
    WHERE idProducto IN (SELECT idProducto FROM @frescos)
      AND NOT EXISTS (SELECT 1 FROM soda.tb_DetallePedido d WHERE d.idPrecio = soda.tb_Precio.idPrecio);

    UPDATE soda.tb_Precio SET EstaActivo = 0 WHERE idProducto IN (SELECT idProducto FROM @frescos);

    DELETE FROM soda.tb_Producto
    WHERE idProducto IN (SELECT idProducto FROM @frescos)
      AND NOT EXISTS (SELECT 1 FROM soda.tb_Precio x WHERE x.idProducto = soda.tb_Producto.idProducto);

    UPDATE soda.tb_Producto SET EstaActivo = 0 WHERE idProducto IN (SELECT idProducto FROM @frescos);

    -- 5) La categoría ""Fresco"" se elimina si quedó vacía, o se desactiva si solo le quedan productos inactivos.
    IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE idCategoria = @idCategoriaFresco)
    BEGIN
        DELETE FROM soda.tb_CategoriaTamano WHERE idCategoria = @idCategoriaFresco;
        DELETE FROM soda.tb_Categoria WHERE idCategoria = @idCategoriaFresco;
    END
    ELSE IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE idCategoria = @idCategoriaFresco AND EstaActivo = 1)
        UPDATE soda.tb_Categoria SET EstaActivo = 0 WHERE idCategoria = @idCategoriaFresco;
END;";
    }
}
