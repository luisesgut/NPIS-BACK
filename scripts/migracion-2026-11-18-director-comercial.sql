-- 2026-11-18 · "Mi equipo" de Comercial: cada vendedor puede tener un director (AspNetUsers.DirectorUserId).
-- Columna nueva, vacía para todos los usuarios; no toca datos. Los equipos se arman después en Usuarios.
-- Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261118172944_AddCommercialDirector')
BEGIN
    IF COL_LENGTH(N'AspNetUsers', N'DirectorUserId') IS NULL
        ALTER TABLE [AspNetUsers] ADD [DirectorUserId] uniqueidentifier NULL;

    -- En otro lote (EXEC): la columna recién creada no existe todavía al compilar este.
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = N'IX_AspNetUsers_DirectorUserId' AND [object_id] = OBJECT_ID(N'[AspNetUsers]'))
        EXEC(N'CREATE INDEX [IX_AspNetUsers_DirectorUserId] ON [AspNetUsers] ([DirectorUserId]);');

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261118172944_AddCommercialDirector', N'10.0.11');
END;

COMMIT;
GO
