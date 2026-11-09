-- =====================================================================================
-- Migración 2026-10-30 — Cierres con visto bueno del admin (migración EF 20261030181508_AddClosureProposals)
--
-- Crea:
--   * NpiClosureProposals: propuestas de cierre de usuarios normales (no factibilidad de
--     Ingeniería/Diseño, rechazo del cliente, rechazo total por insumos, rechazo por precio) y la
--     decisión del admin del área (confirmada / no autorizada / retirada / sin efecto).
-- No modifica ni borra datos existentes. Los NPIs ya cerrados se quedan como están.
-- Es idempotente: se puede correr dos veces.
--
-- Requisito: que ya esté aplicada 20261027191309_AddSisproQuotes. Para revisarlo:
--   SELECT TOP 3 MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
--
-- Publicar el backend nuevo DESPUÉS de correr este script: el detalle del NPI lee esta tabla.
-- =====================================================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030181508_AddClosureProposals'
)
BEGIN
    CREATE TABLE [NpiClosureProposals] (
        [Id] uniqueidentifier NOT NULL,
        [Reason] nvarchar(40) NOT NULL,
        [Area] nvarchar(20) NOT NULL,
        [ProposedByUserId] uniqueidentifier NOT NULL,
        [ProposedByName] nvarchar(200) NOT NULL,
        [ProposedAt] datetimeoffset NOT NULL,
        [Note] nvarchar(2000) NOT NULL,
        [EvidenceAttachmentId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [DecidedByUserId] uniqueidentifier NULL,
        [DecidedByName] nvarchar(200) NULL,
        [DecidedAt] datetimeoffset NULL,
        [DecisionNote] nvarchar(2000) NULL,
        [NpiId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_NpiClosureProposals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NpiClosureProposals_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030181508_AddClosureProposals'
)
BEGIN
    CREATE INDEX [IX_NpiClosureProposals_NpiId] ON [NpiClosureProposals] ([NpiId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030181508_AddClosureProposals'
)
BEGIN
    CREATE INDEX [IX_NpiClosureProposals_Status] ON [NpiClosureProposals] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030181508_AddClosureProposals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030181508_AddClosureProposals', N'10.0.11');
END;

COMMIT;
GO

