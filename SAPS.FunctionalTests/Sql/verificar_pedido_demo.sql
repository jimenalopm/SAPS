-- =====================================================================================
-- SAPS.FunctionalTests - Consulta de SOLO LECTURA para evidenciar HU-008 (criterio de trazabilidad):
-- muestra los pedidos de DEMO001, la usuaria que los registró y el detalle con el precio cobrado.
-- Adjunte el resultado como evidencia en el TASK de HU-008.
-- =====================================================================================
SET NOCOUNT ON;

SELECT p.idPedido, p.CodigoColaborador, p.NombreColaborador, u.UserName AS UsuariaQueRegistro,
       p.FechaRegistroUtc, p.TipoComida, p.Total, p.EsPrueba
FROM soda.tb_Pedido p
JOIN AspNetUsers u ON u.Id = p.idUsuario
WHERE p.CodigoColaborador = 'DEMO001'
ORDER BY p.idPedido;

SELECT d.idPedido, d.NombreArticulo, d.NombreTamano, d.Cantidad, d.PrecioUnitario, d.Subtotal
FROM soda.tb_DetallePedido d
JOIN soda.tb_Pedido p ON p.idPedido = d.idPedido
WHERE p.CodigoColaborador = 'DEMO001'
ORDER BY d.idPedido, d.idDetallePedido;
