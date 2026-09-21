-- ============================================================================
-- seed_catalogo.sql
-- Datos iniciales de EJEMPLO para el catálogo de SAPS, tomados del
-- "Listado de Precios del Servicio de Soda" (vigente desde el 23/09/2025).
--
-- IMPORTANTE: a propósito NO se sembraron los ~150 artículos del listado
-- completo aquí. El líder técnico pidió que los precios no queden
-- hardcodeados en el código, y escribir a mano un INSERT por cada uno de los
-- artículos del PDF sin poder probarlo contra la base de datos real es
-- arriesgado (un error de tipeo en un precio real es delicado). Este script
-- deja precargada una muestra representativa de cada categoría para que
-- puedan ver el CRUD funcionando con datos reales de una vez; el resto del
-- listado se carga cómodamente desde la propia pantalla de Administración
-- > Catálogo que ya quedó implementada (para eso se construyó).
--
-- Cómo ejecutarlo (con el contenedor de crear_db.bat ya corriendo):
--   sqlcmd -S localhost,1433 -U sa -P "GarfieldPapuPro1234!" -C -d SAPS_Db -i seed_catalogo.sql
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ---------- Categorías ----------
IF NOT EXISTS (SELECT 1 FROM tb_Categoria WHERE nombreCategoria = 'Desayuno')
    INSERT INTO tb_Categoria (nombreCategoria, activo) VALUES ('Desayuno', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Categoria WHERE nombreCategoria = 'Almuerzo')
    INSERT INTO tb_Categoria (nombreCategoria, activo) VALUES ('Almuerzo', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Categoria WHERE nombreCategoria = 'Café/Repostería')
    INSERT INTO tb_Categoria (nombreCategoria, activo) VALUES ('Café/Repostería', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Categoria WHERE nombreCategoria = 'Fresco')
    INSERT INTO tb_Categoria (nombreCategoria, activo) VALUES ('Fresco', 1);

-- ---------- Tamaños ----------
IF NOT EXISTS (SELECT 1 FROM tb_Tamano WHERE nombreTamano = 'Pequeño')
    INSERT INTO tb_Tamano (nombreTamano, activo) VALUES ('Pequeño', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Tamano WHERE nombreTamano = 'Mediano')
    INSERT INTO tb_Tamano (nombreTamano, activo) VALUES ('Mediano', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Tamano WHERE nombreTamano = 'Grande')
    INSERT INTO tb_Tamano (nombreTamano, activo) VALUES ('Grande', 1);
IF NOT EXISTS (SELECT 1 FROM tb_Tamano WHERE nombreTamano = '600ml')
    INSERT INTO tb_Tamano (nombreTamano, activo) VALUES ('600ml', 1);

DECLARE @hoy date = CAST(GETDATE() AS date);

-- ---------- Productos con precio único (Desayuno) ----------
IF NOT EXISTS (SELECT 1 FROM tb_Producto WHERE nombreProducto = 'Pinto con 1 acomp. c/s Tortilla')
BEGIN
    DECLARE @idDesayuno INT = (SELECT idCategoria FROM tb_Categoria WHERE nombreCategoria = 'Desayuno');
    INSERT INTO tb_Producto (nombreProducto, idCategoria, esEspecial, requiereTamano, activo)
        VALUES ('Pinto con 1 acomp. c/s Tortilla', @idDesayuno, 0, 0, 1);
    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (SCOPE_IDENTITY(), NULL, 1100, @hoy, 1);
END

-- ---------- Productos con precio único (Almuerzo) ----------
IF NOT EXISTS (SELECT 1 FROM tb_Producto WHERE nombreProducto = 'Rice and Beans')
BEGIN
    DECLARE @idAlmuerzo INT = (SELECT idCategoria FROM tb_Categoria WHERE nombreCategoria = 'Almuerzo');
    INSERT INTO tb_Producto (nombreProducto, idCategoria, esEspecial, requiereTamano, activo)
        VALUES ('Rice and Beans', @idAlmuerzo, 1, 0, 1);
    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (SCOPE_IDENTITY(), NULL, 1550, @hoy, 1);
END

-- ---------- Producto CON tamaños (ejemplo de RequiereTamano = 1) ----------
IF NOT EXISTS (SELECT 1 FROM tb_Producto WHERE nombreProducto = 'Fresco natural')
BEGIN
    DECLARE @idFresco INT = (SELECT idCategoria FROM tb_Categoria WHERE nombreCategoria = 'Fresco');
    DECLARE @idProdFresco INT;
    INSERT INTO tb_Producto (nombreProducto, idCategoria, esEspecial, requiereTamano, activo)
        VALUES ('Fresco natural', @idFresco, 0, 1, 1);
    SET @idProdFresco = SCOPE_IDENTITY();

    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (@idProdFresco, (SELECT idTamano FROM tb_Tamano WHERE nombreTamano = 'Pequeño'), 250, @hoy, 1);
    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (@idProdFresco, (SELECT idTamano FROM tb_Tamano WHERE nombreTamano = 'Mediano'), 300, @hoy, 1);
    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (@idProdFresco, (SELECT idTamano FROM tb_Tamano WHERE nombreTamano = 'Grande'), 350, @hoy, 1);
END

-- ---------- Café/Repostería ----------
IF NOT EXISTS (SELECT 1 FROM tb_Producto WHERE nombreProducto = 'Empanadas')
BEGIN
    DECLARE @idCafe INT = (SELECT idCategoria FROM tb_Categoria WHERE nombreCategoria = 'Café/Repostería');
    INSERT INTO tb_Producto (nombreProducto, idCategoria, esEspecial, requiereTamano, activo)
        VALUES ('Empanadas', @idCafe, 0, 0, 1);
    INSERT INTO tb_Precio (idProducto, idTamano, precio, fechaVigenciaDesde, activo)
        VALUES (SCOPE_IDENTITY(), NULL, 500, @hoy, 1);
END

-- ---------- Bebidas ----------
IF NOT EXISTS (SELECT 1 FROM tb_Bebida WHERE nombreBebida = 'Pepsi Cola 600ml')
    INSERT INTO tb_Bebida (nombreBebida, tipoBebida, idTamano, precio, activo)
    VALUES ('Pepsi Cola 600ml', 'Gaseosa', (SELECT idTamano FROM tb_Tamano WHERE nombreTamano = '600ml'), 1000, 1);

PRINT 'Datos de ejemplo del catálogo cargados (o ya existían).';

