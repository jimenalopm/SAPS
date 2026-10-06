-- =====================================================================================
-- SAPS.FunctionalTests - Consulta de SOLO LECTURA: qué productos ZZ_SELENIUM_ dejaron las pruebas,
-- si siguen ACTIVOS (visibles para la usuaria de Soda) y con qué precio quedaron.
-- Sirve para revisar, por ejemplo, el caso "1500,5" de HU-007: si se creó, aquí se ve su precio.
-- =====================================================================================
SET NOCOUNT ON;

SELECT p.idProducto, p.Nombre, p.EstaActivo AS ProductoActivo, p.EsEspecial, p.TieneTamano,
       t.Nombre AS Tamano, pr.Monto, pr.FechaVigenciaDesde, pr.FechaVigenciaHasta, pr.EstaActivo AS PrecioActivo
FROM soda.tb_Producto p
LEFT JOIN soda.tb_Precio pr ON pr.idProducto = p.idProducto
LEFT JOIN soda.tb_Tamano t ON t.idTamano = pr.idTamano
WHERE p.Nombre LIKE 'ZZ[_]SELENIUM[_]%'
ORDER BY p.idProducto, pr.idPrecio;

-- Productos de prueba con un precio distinto de los que usan las pruebas (1500, 1800, 2500 y 1000+500*n por tamaño):
SELECT p.Nombre, pr.Monto
FROM soda.tb_Producto p
JOIN soda.tb_Precio pr ON pr.idProducto = p.idProducto
WHERE p.Nombre LIKE 'ZZ[_]SELENIUM[_]%'
  AND pr.Monto NOT IN (1500, 1800, 2500, 1000, 1500, 2000, 2500, 3000, 3500);
