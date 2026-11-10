-- 2026-11-10 · Chat usuario ↔ soporte en cada reporte (botón "Reportar").
-- Tabla nueva FeedbackMessages; no toca datos existentes.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261110175509_AddFeedbackMessages')
BEGIN
    IF OBJECT_ID(N'[FeedbackMessages]', N'U') IS NULL
    BEGIN
        CREATE TABLE [FeedbackMessages] (
            [Id] uniqueidentifier NOT NULL,
            [ReportId] uniqueidentifier NOT NULL,
            [AuthorUserId] uniqueidentifier NOT NULL,
            [AuthorName] nvarchar(200) NOT NULL,
            [FromSupport] bit NOT NULL,
            [Text] nvarchar(2000) NOT NULL,
            [CreatedAt] datetimeoffset NOT NULL,
            CONSTRAINT [PK_FeedbackMessages] PRIMARY KEY ([Id]),
            CONSTRAINT [FK_FeedbackMessages_FeedbackReports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [FeedbackReports] ([Id]) ON DELETE CASCADE
        );

        CREATE INDEX [IX_FeedbackMessages_ReportId_CreatedAt] ON [FeedbackMessages] ([ReportId], [CreatedAt]);
    END;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261110175509_AddFeedbackMessages', N'10.0.11');
END;

COMMIT;
GO
