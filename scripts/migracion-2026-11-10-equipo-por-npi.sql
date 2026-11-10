-- 2026-11-10 · Equipo por NPI: personas que trabajan un NPI junto con el responsable de su área.
-- Tabla nueva NpiParticipants; no toca datos existentes.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261110220440_AddNpiParticipants')
BEGIN
    IF OBJECT_ID(N'[NpiParticipants]', N'U') IS NULL
    BEGIN
        CREATE TABLE [NpiParticipants] (
            [Id] uniqueidentifier NOT NULL,
            [UserId] uniqueidentifier NOT NULL,
            [Area] nvarchar(20) NOT NULL,
            [AddedByUserId] uniqueidentifier NOT NULL,
            [AddedByName] nvarchar(200) NOT NULL,
            [AddedAt] datetimeoffset NOT NULL,
            [NpiId] uniqueidentifier NOT NULL,
            CONSTRAINT [PK_NpiParticipants] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_NpiParticipants_Npis_NpiId] FOREIGN KEY ([NpiId]) REFERENCES [Npis] ([Id]) ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX [IX_NpiParticipants_NpiId_UserId_Area] ON [NpiParticipants] ([NpiId], [UserId], [Area]);
        CREATE INDEX [IX_NpiParticipants_UserId] ON [NpiParticipants] ([UserId]);
    END;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110220440_AddNpiParticipants', N'10.0.11');
END;

COMMIT;
GO
