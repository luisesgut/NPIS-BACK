-- =====================================================================================
-- Migración 2026-10-27 — Cotización en SISPRO (migración EF 20261027191309_AddSisproQuotes)
--
-- Crea:
--   * SisproMaterialEquivalences: equivalencias material SISPRO -> película del NPI (Ingeniería).
--   * SisproQuoteLinks: cotizaciones de SISPRO ligadas a cada NPI y la respuesta del cliente.
-- No modifica ni borra datos existentes. Es idempotente: se puede correr dos veces.
--
-- Requisito: que ya estén aplicadas las migraciones anteriores (la última es
-- 20261023000804_AddColorCintaUniones). Para revisarlo:
--   SELECT TOP 3 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
--
-- La tabla Quotations (cotizaciones locales de demo) ya no la usa el sistema, pero NO se borra
-- aquí. Revísala y, si no sirve, bórrala a mano:
--   SELECT COUNT(*) FROM Quotations;
--   -- DROP TABLE Quotations;
-- =====================================================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE TABLE [SisproMaterialEquivalences] (
        [Id] uniqueidentifier NOT NULL,
        [MaterialSispro] nvarchar(150) NOT NULL,
        [TipoPelicula] nvarchar(150) NOT NULL,
        [Terminado] nvarchar(150) NULL,
        [FormulaLdpe] nvarchar(150) NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [UpdatedByName] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_SisproMaterialEquivalences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE TABLE [SisproQuoteLinks] (
        [Id] uniqueidentifier NOT NULL,
        [VersionNumber] int NOT NULL,
        [NumeroCotizacion] nvarchar(30) NOT NULL,
        [EstadoSispro] nvarchar(50) NOT NULL,
        [SnapshotJson] nvarchar(max) NOT NULL,
        [StructureWarnings] nvarchar(4000) NOT NULL,
        [LinkedByUserId] uniqueidentifier NOT NULL,
        [LinkedByName] nvarchar(200) NOT NULL,
        [LinkedAt] datetimeoffset NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [RespondedAt] datetimeoffset NULL,
        [RespondedByUserId] uniqueidentifier NULL,
        [RespondedByName] nvarchar(200) NULL,
        [ResponseNote] nvarchar(2000) NULL,
        [EvidenceAttachmentId] uniqueidentifier NULL,
        [InvalidatedAt] datetimeoffset NULL,
        [InvalidationReason] nvarchar(1000) NULL,
        [NpiId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_SisproQuoteLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SisproQuoteLinks_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SisproMaterialEquivalences_MaterialSispro] ON [SisproMaterialEquivalences] ([MaterialSispro]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE INDEX [IX_SisproQuoteLinks_NpiId] ON [SisproQuoteLinks] ([NpiId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SisproQuoteLinks_NpiId_VersionNumber] ON [SisproQuoteLinks] ([NpiId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    CREATE INDEX [IX_SisproQuoteLinks_NumeroCotizacion] ON [SisproQuoteLinks] ([NumeroCotizacion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261027191309_AddSisproQuotes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261027191309_AddSisproQuotes', N'10.0.11');
END;

COMMIT;
GO

