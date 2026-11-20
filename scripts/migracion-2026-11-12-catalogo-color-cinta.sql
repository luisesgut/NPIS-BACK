-- 2026-11-12 · Catálogo "COLOR CINTA UNIONES" para que Ingeniería administre los colores de la cinta
-- de uniones de la bobina (antes fijos en el formulario). Sólo agrega el catálogo con BLANCA y ROJA;
-- no toca los NPIs. Idempotente: se puede correr más de una vez.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261112152615_AddColorCintaCatalog')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'COLOR CINTA UNIONES')
    BEGIN
        DECLARE @Id UNIQUEIDENTIFIER = NEWID();
        INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'COLOR CINTA UNIONES', N'Color de cinta para uniones');
        INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
            (NEWID(), @Id, N'BLANCA', 0, 1),
            (NEWID(), @Id, N'ROJA', 1, 1);
    END;

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261112152615_AddColorCintaCatalog', N'10.0.11');
END;

COMMIT;
GO
