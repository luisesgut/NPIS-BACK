-- 2026-11-04 · Ubicación de la solapa (Arriba / Abajo) en la captura del NPI.
-- Idempotente: se puede correr más de una vez. Los NPIs existentes quedan en vacío = arriba,
-- que es como el dibujo los mostraba hasta hoy.
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104195444_AddUbicacionSolapa'
)
BEGIN
    ALTER TABLE [Npis] ADD [Capture_UbicacionSolapa] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104195444_AddUbicacionSolapa'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104195444_AddUbicacionSolapa', N'10.0.11');
END;

COMMIT;
GO
