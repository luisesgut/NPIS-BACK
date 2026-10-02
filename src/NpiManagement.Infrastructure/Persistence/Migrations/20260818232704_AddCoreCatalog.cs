using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración de datos (no de esquema) — agrega el catálogo "CORE" (3"/6") usado por el campo
    /// "Core (Pulg)" del paso "Empaque y Embalaje", que antes era texto libre. Mismo patrón que las
    /// migraciones de catálogo anteriores.
    /// </summary>
    /// <inheritdoc />
    public partial class AddCoreCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CoreId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CORE');
IF @CoreId IS NULL
BEGIN
    SET @CoreId = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@CoreId, N'CORE', N'Core (Pulg)');
END
DELETE FROM CatalogOptions WHERE CatalogId = @CoreId;
INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @CoreId, N'3""', 0, 1),
    (NEWID(), @CoreId, N'6""', 1, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CoreId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CORE');
IF @CoreId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @CoreId;
    DELETE FROM Catalogs WHERE Id = @CoreId;
END");
        }
    }
}
