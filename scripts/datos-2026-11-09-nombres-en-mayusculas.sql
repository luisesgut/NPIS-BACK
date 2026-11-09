-- 2026-11-09 · Clientes, productos y contactos en MAYÚSCULAS en los NPIs que ya existen.
-- Sólo datos (no cambia la estructura): pasa a mayúsculas 7 campos de la captura, respetando acentos y Ñ.
-- Observaciones y notas NO se tocan. Seguro de correr más de una vez: la segunda vez no cambia nada.
-- Sólo actualiza los NPIs que tengan alguna minúscula en esos campos (los demás ni se tocan).
-- Recomendación: correrlo cuando haya poca gente usando el sistema (quien tenga uno de esos NPIs
-- abierto verá "otro usuario modificó este NPI" y sólo tiene que recargar).

SET NOCOUNT ON;
-- sqlcmd arranca con QUOTED_IDENTIFIER apagado y la tabla Npis tiene índices filtrados que lo exigen
-- (error 1934). Se fijan las opciones que pide SQL Server para actualizar tablas con esos índices.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
-- Si algo falla a medio camino, se deshace todo (no queda a medias).
SET XACT_ABORT ON;

-- Cuántos NPIs se van a actualizar (antes).
SELECT COUNT(*) AS NpisPorActualizar
FROM [Npis]
WHERE [Capture_Cliente] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_Cliente])
   OR [Capture_Descripcion] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_Descripcion])
   OR [Capture_DescripcionProducto] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_DescripcionProducto])
   OR [Capture_NombreExtranjeroProducto] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_NombreExtranjeroProducto])
   OR [Capture_CodigoProdCliente] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_CodigoProdCliente])
   OR [Capture_ContactoCompras] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_ContactoCompras])
   OR [Capture_ContactoClienteDiseno] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_ContactoClienteDiseno]);

BEGIN TRANSACTION;

UPDATE [Npis]
SET [Capture_Cliente] = UPPER([Capture_Cliente]),
    [Capture_Descripcion] = UPPER([Capture_Descripcion]),
    [Capture_DescripcionProducto] = UPPER([Capture_DescripcionProducto]),
    [Capture_NombreExtranjeroProducto] = UPPER([Capture_NombreExtranjeroProducto]),
    [Capture_CodigoProdCliente] = UPPER([Capture_CodigoProdCliente]),
    [Capture_ContactoCompras] = UPPER([Capture_ContactoCompras]),
    [Capture_ContactoClienteDiseno] = UPPER([Capture_ContactoClienteDiseno])
WHERE [Capture_Cliente] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_Cliente])
   OR [Capture_Descripcion] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_Descripcion])
   OR [Capture_DescripcionProducto] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_DescripcionProducto])
   OR [Capture_NombreExtranjeroProducto] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_NombreExtranjeroProducto])
   OR [Capture_CodigoProdCliente] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_CodigoProdCliente])
   OR [Capture_ContactoCompras] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_ContactoCompras])
   OR [Capture_ContactoClienteDiseno] COLLATE Latin1_General_100_CS_AS <> UPPER([Capture_ContactoClienteDiseno]);

SELECT @@ROWCOUNT AS NpisActualizados;

COMMIT;
GO
