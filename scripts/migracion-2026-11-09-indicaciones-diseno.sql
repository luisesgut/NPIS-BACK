-- 2026-11-09 · Indicaciones del admin de Diseño para el diseñador (columna DesignInstructions en Npis).
-- Idempotente: se puede correr más de una vez. Los NPIs existentes quedan sin indicaciones (NULL).
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261109163554_AddDesignInstructions')
BEGIN
    IF COL_LENGTH('Npis', 'DesignInstructions') IS NULL
        ALTER TABLE [Npis] ADD [DesignInstructions] nvarchar(2000) NULL;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109163554_AddDesignInstructions', N'10.0.11');
END;

COMMIT;
GO
