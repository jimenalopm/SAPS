-- =====================================================================================
-- SAPS.FunctionalTests - LIMPIEZA de lo que dejaron las pruebas funcionales en la BD real.
-- NO se ejecuta automáticamente. Córralo manualmente DESPUÉS de correr las pruebas por única vez.
--
-- Borra, y SOLO esto:
--   1. Pedidos (y su detalle) del colaborador de prueba DEMO001.
--   2. Precios y productos cuyo nombre empieza con ZZ-SELENIUM-
--   3. Bebidas cuyo nombre empieza con ZZ-SELENIUM-
--   4. Las cuentas de prueba zz-selenium-%@recyplast.cr (HU-004)
--   5. El colaborador DEMO001
--
-- SEGURIDAD: por defecto es un ENSAYO (@Confirmar = 0): muestra qué se borraría y hace ROLLBACK.
-- Revise la salida y, si es correcta, cambie @Confirmar a 1 y vuelva a ejecutar.
-- Compatible con SQL Server 2012.
-- =====================================================================================
SET NOCOUNT ON;
DECLARE @Confirmar bit = 0;   -- <<< 0 = ensayo (no borra nada) | 1 = borra de verdad

BEGIN TRY
    BEGIN TRANSACTION;

    -- ---- Qué se va a borrar --------------------------------------------------------
    SELECT 'Pedidos DEMO001' AS Tipo, idPedido, CodigoColaborador, Total, EsPrueba
    FROM soda.tb_Pedido WHERE CodigoColaborador = 'DEMO001';

    SELECT 'Productos ZZ-SELENIUM-' AS Tipo, idProducto, Nombre, EstaActivo
    FROM soda.tb_Producto WHERE Nombre LIKE 'ZZ-SELENIUM-%';

    SELECT 'Bebidas ZZ-SELENIUM-' AS Tipo, idBebida, Nombre, EstaActivo
    FROM soda.tb_Bebida WHERE Nombre LIKE 'ZZ-SELENIUM-%';

    SELECT 'Cuentas zz-selenium' AS Tipo, Id, Email
    FROM dbo.AspNetUsers WHERE Email LIKE 'zz-selenium-%@recyplast.cr';

    SELECT 'Colaborador DEMO001' AS Tipo, idColaborador, Codigo, NombreCompleto
    FROM rrhh.tb_Colaborador WHERE Codigo = 'DEMO001';

    -- ---- Protección: un pedido de OTRO colaborador que use un ZZ-SELENIUM- impediría borrarlo ----
    IF EXISTS (
        SELECT 1
        FROM soda.tb_DetallePedido d
        JOIN soda.tb_Pedido p ON p.idPedido = d.idPedido
        LEFT JOIN soda.tb_Precio pr ON pr.idPrecio = d.idPrecio
        LEFT JOIN soda.tb_Producto pd ON pd.idProducto = pr.idProducto
        LEFT JOIN soda.tb_Bebida b ON b.idBebida = d.idBebida
        WHERE p.CodigoColaborador <> 'DEMO001'
          AND (pd.Nombre LIKE 'ZZ-SELENIUM-%' OR b.Nombre LIKE 'ZZ-SELENIUM-%'))
        RAISERROR('Hay pedidos de un colaborador distinto de DEMO001 que usan productos ZZ-SELENIUM-. No se borra nada.', 16, 1);

    -- ---- Borrado en orden de dependencias ------------------------------------------
    DELETE d
    FROM soda.tb_DetallePedido d
    JOIN soda.tb_Pedido p ON p.idPedido = d.idPedido
    WHERE p.CodigoColaborador = 'DEMO001';

    DELETE FROM soda.tb_Pedido WHERE CodigoColaborador = 'DEMO001';

    DELETE pr
    FROM soda.tb_Precio pr
    JOIN soda.tb_Producto pd ON pd.idProducto = pr.idProducto
    WHERE pd.Nombre LIKE 'ZZ-SELENIUM-%';

    DELETE FROM soda.tb_Producto WHERE Nombre LIKE 'ZZ-SELENIUM-%';
    DELETE FROM soda.tb_Bebida   WHERE Nombre LIKE 'ZZ-SELENIUM-%';
    -- Cuentas de prueba de HU-004 (vinculadas a DEMO001). Sus roles, vínculo y estado de contraseña se borran en cascada.
    -- Sus eventos en soda.tb_Bitacora se conservan: la bitácora es de solo lectura.
    DELETE FROM dbo.AspNetUsers WHERE Email LIKE 'zz-selenium-%@recyplast.cr';
    DELETE FROM rrhh.tb_Colaborador WHERE Codigo = 'DEMO001';

    IF @Confirmar = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT 'Limpieza APLICADA.';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT 'ENSAYO: no se borró nada. Cambie @Confirmar a 1 para aplicar.';
    END
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR, se revirtió todo: ' + ERROR_MESSAGE();
END CATCH;
