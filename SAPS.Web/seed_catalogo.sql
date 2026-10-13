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
--   sqlcmd -S localhost,1433 -U sa -P "TU_CLAVE" -C -d SAPS_DB -i seed_catalogo.sql
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ---------- Categorías ----------
IF NOT EXISTS (SELECT 1 FROM soda.tb_Categoria WHERE Nombre = 'Desayuno')
    INSERT INTO soda.tb_Categoria (Nombre, EstaActivo) VALUES ('Desayuno', 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Categoria WHERE Nombre = 'Almuerzo')
    INSERT INTO soda.tb_Categoria (Nombre, EstaActivo) VALUES ('Almuerzo', 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Categoria WHERE Nombre = 'Café/Repostería')
    INSERT INTO soda.tb_Categoria (Nombre, EstaActivo) VALUES ('Café/Repostería', 1);

-- ---------- Tamaños ----------
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Pequeño')
    INSERT INTO soda.tb_Tamano (Nombre, EstaActivo) VALUES ('Pequeño', 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Mediano')
    INSERT INTO soda.tb_Tamano (Nombre, EstaActivo) VALUES ('Mediano', 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Grande')
    INSERT INTO soda.tb_Tamano (Nombre, EstaActivo) VALUES ('Grande', 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = '600ml')
    INSERT INTO soda.tb_Tamano (Nombre, EstaActivo) VALUES ('600ml', 1);
-- Tamaños de bebida del fresco natural (las bebidas solo aceptan tamaños marcados EsParaBebida).
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Vaso pequeño')
    INSERT INTO soda.tb_Tamano (Nombre, EsParaBebida, EstaActivo) VALUES ('Vaso pequeño', 1, 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Vaso mediano')
    INSERT INTO soda.tb_Tamano (Nombre, EsParaBebida, EstaActivo) VALUES ('Vaso mediano', 1, 1);
IF NOT EXISTS (SELECT 1 FROM soda.tb_Tamano WHERE Nombre = 'Vaso grande')
    INSERT INTO soda.tb_Tamano (Nombre, EsParaBebida, EstaActivo) VALUES ('Vaso grande', 1, 1);

DECLARE @hoy date = CAST(GETDATE() AS date);

-- ---------- Productos con precio único (Desayuno) ----------
IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE Nombre = 'Pinto con 1 acomp. c/s Tortilla')
BEGIN
    DECLARE @idDesayuno INT = (SELECT idCategoria FROM soda.tb_Categoria WHERE Nombre = 'Desayuno');
    INSERT INTO soda.tb_Producto (Nombre, idCategoria, EsEspecial, TieneTamano, EstaActivo)
        VALUES ('Pinto con 1 acomp. c/s Tortilla', @idDesayuno, 0, 0, 1);
    INSERT INTO soda.tb_Precio (idProducto, idTamano, Monto, FechaVigenciaDesde, EstaActivo)
        VALUES (SCOPE_IDENTITY(), NULL, 1100, @hoy, 1);
END

-- ---------- Productos con precio único (Almuerzo) ----------
IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE Nombre = 'Rice and Beans')
BEGIN
    DECLARE @idAlmuerzo INT = (SELECT idCategoria FROM soda.tb_Categoria WHERE Nombre = 'Almuerzo');
    INSERT INTO soda.tb_Producto (Nombre, idCategoria, EsEspecial, TieneTamano, EstaActivo)
        VALUES ('Rice and Beans', @idAlmuerzo, 1, 0, 1);
    INSERT INTO soda.tb_Precio (idProducto, idTamano, Monto, FechaVigenciaDesde, EstaActivo)
        VALUES (SCOPE_IDENTITY(), NULL, 1550, @hoy, 1);
END

-- ---------- Café/Repostería ----------
IF NOT EXISTS (SELECT 1 FROM soda.tb_Producto WHERE Nombre = 'Empanadas')
BEGIN
    DECLARE @idCafe INT = (SELECT idCategoria FROM soda.tb_Categoria WHERE Nombre = 'Café/Repostería');
    INSERT INTO soda.tb_Producto (Nombre, idCategoria, EsEspecial, TieneTamano, EstaActivo)
        VALUES ('Empanadas', @idCafe, 0, 0, 1);
    INSERT INTO soda.tb_Precio (idProducto, idTamano, Monto, FechaVigenciaDesde, EstaActivo)
        VALUES (SCOPE_IDENTITY(), NULL, 500, @hoy, 1);
END

-- ---------- Bebidas ----------
-- El fresco natural es una bebida (tipo "Frescos") con un precio por tamaño.
IF NOT EXISTS (SELECT 1 FROM soda.tb_Bebida WHERE Nombre = 'Frescos naturales')
BEGIN
    INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
        VALUES ('Frescos naturales', 'Frescos', (SELECT idTamano FROM soda.tb_Tamano WHERE Nombre = 'Vaso pequeño'), 250, 1);
    INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
        VALUES ('Frescos naturales', 'Frescos', (SELECT idTamano FROM soda.tb_Tamano WHERE Nombre = 'Vaso mediano'), 300, 1);
    INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
        VALUES ('Frescos naturales', 'Frescos', (SELECT idTamano FROM soda.tb_Tamano WHERE Nombre = 'Vaso grande'), 350, 1);
END

IF NOT EXISTS (SELECT 1 FROM soda.tb_Bebida WHERE Nombre = 'Pepsi Cola 600ml')
    INSERT INTO soda.tb_Bebida (Nombre, Tipo, idTamano, Precio, EstaActivo)
    VALUES ('Pepsi Cola 600ml', 'Gaseosa', (SELECT idTamano FROM soda.tb_Tamano WHERE Nombre = '600ml'), 1000, 1);

PRINT 'Datos de ejemplo del catálogo cargados (o ya existían).';

