using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración de datos (no de esquema) para el paso "Elementos y Configuración" del wizard:
    /// reemplaza las opciones de "CIERRE" y "SELLO", y agrega el catálogo nuevo "PERFORACIONES"
    /// (MACRO/MICRO). "OTROS ELEMENTOS" no cambia — ya tenía exactamente los valores pedidos
    /// (SPOUT/WICKET/OTRO/N/A) desde el seed original. Mismo patrón de las migraciones anteriores:
    /// CatalogId resuelto por Key en tiempo de ejecución, nunca un GUID fijo.
    /// </summary>
    /// <inheritdoc />
    public partial class UpdateCierreSelloAndAddPerforacionesCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CierreId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CIERRE');
IF @CierreId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @CierreId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @CierreId, N'ZIPPER', 0, 1),
        (NEWID(), @CierreId, N'ZIPPER MARIPOSA', 1, 1),
        (NEWID(), @CierreId, N'ZIPPER VELCRO', 2, 1),
        (NEWID(), @CierreId, N'N/A', 3, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @SelloId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'SELLO');
IF @SelloId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @SelloId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @SelloId, N'ALETA', 0, 1),
        (NEWID(), @SelloId, N'TRASLAPE', 1, 1),
        (NEWID(), @SelloId, N'LATERAL', 2, 1),
        (NEWID(), @SelloId, N'FONDO', 3, 1),
        (NEWID(), @SelloId, N'SELLO ""K""', 4, 1),
        (NEWID(), @SelloId, N'SELLO ""U""', 5, 1),
        (NEWID(), @SelloId, N'2 SELLOS', 6, 1),
        (NEWID(), @SelloId, N'3 SELLOS', 7, 1),
        (NEWID(), @SelloId, N'4 SELLOS', 8, 1),
        (NEWID(), @SelloId, N'N/A', 9, 1),
        (NEWID(), @SelloId, N'TOV', 10, 1),
        (NEWID(), @SelloId, N'OTRO', 11, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @PerforacionesId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'PERFORACIONES');
IF @PerforacionesId IS NULL
BEGIN
    SET @PerforacionesId = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@PerforacionesId, N'PERFORACIONES', N'Perforaciones');
END
DELETE FROM CatalogOptions WHERE CatalogId = @PerforacionesId;
INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @PerforacionesId, N'MACRO', 0, 1),
    (NEWID(), @PerforacionesId, N'MICRO', 1, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @CierreId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CIERRE');
IF @CierreId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @CierreId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @CierreId, N'ZIPPER', 0, 1),
        (NEWID(), @CierreId, N'CINTA', 1, 1),
        (NEWID(), @CierreId, N'ULTRASONICO', 2, 1),
        (NEWID(), @CierreId, N'N/A', 3, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @SelloId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'SELLO');
IF @SelloId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @SelloId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @SelloId, N'ALETA', 0, 1),
        (NEWID(), @SelloId, N'TRASLAPE', 1, 1),
        (NEWID(), @SelloId, N'LATERAL', 2, 1),
        (NEWID(), @SelloId, N'FONDO', 3, 1),
        (NEWID(), @SelloId, N'SELLO ""K""', 4, 1),
        (NEWID(), @SelloId, N'3 SELLOS', 5, 1),
        (NEWID(), @SelloId, N'SELLO ""U""', 6, 1),
        (NEWID(), @SelloId, N'OTRO', 7, 1),
        (NEWID(), @SelloId, N'N/A', 8, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @PerforacionesId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'PERFORACIONES');
IF @PerforacionesId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @PerforacionesId;
    DELETE FROM Catalogs WHERE Id = @PerforacionesId;
END");
        }
    }
}
