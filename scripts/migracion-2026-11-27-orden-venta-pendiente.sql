-- 2026-11-27 · "Ponle orden de venta antes de crear otro NPI": Comercial puede indicar que el cliente aún no
-- hace pedido (hasta qué fecha y por qué). 4 columnas nuevas en Npis, vacías en los NPIs que ya existen; no toca datos.
-- Idempotente: se puede correr más de una vez. Correr ANTES de publicar back y front (v1.10.0).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261127174145_AddSapOrderDeferral')
BEGIN
    IF COL_LENGTH(N'Npis', N'SapOrderDeferralReason') IS NULL
        ALTER TABLE [Npis] ADD [SapOrderDeferralReason] nvarchar(1000) NULL;
    IF COL_LENGTH(N'Npis', N'SapOrderDeferredAt') IS NULL
        ALTER TABLE [Npis] ADD [SapOrderDeferredAt] datetimeoffset NULL;
    IF COL_LENGTH(N'Npis', N'SapOrderDeferredByName') IS NULL
        ALTER TABLE [Npis] ADD [SapOrderDeferredByName] nvarchar(200) NULL;
    IF COL_LENGTH(N'Npis', N'SapOrderDeferredUntil') IS NULL
        ALTER TABLE [Npis] ADD [SapOrderDeferredUntil] date NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261127174145_AddSapOrderDeferral', N'10.0.11');
END;

COMMIT;
GO
