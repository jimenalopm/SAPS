-- =====================================================================================
-- SAPS.FunctionalTests - Crea el colaborador de PRUEBA DEMO001 en rrhh.tb_Colaborador.
-- NO se ejecuta automáticamente. Córralo manualmente SOLO si DEMO001 no existe en la BD real.
-- Es idempotente: si ya existe, no hace nada.
-- Compatible con SQL Server 2012.
-- =====================================================================================
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM rrhh.tb_Colaborador WHERE Codigo = 'DEMO001')
    PRINT 'DEMO001 ya existe: no se hizo ningún cambio.';
ELSE
BEGIN
    INSERT INTO rrhh.tb_Colaborador (Codigo, NombreCompleto, EstaActivo, RutaFoto, FechaRegistro)
    VALUES ('DEMO001', 'Ana Solis (prueba)', 1, NULL, SYSUTCDATETIME());
    PRINT 'DEMO001 creado (activo, sin foto).';
END;

-- Verificación
SELECT idColaborador, Codigo, NombreCompleto, EstaActivo, RutaFoto, FechaRegistro
FROM rrhh.tb_Colaborador
WHERE Codigo = 'DEMO001';
