using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMangaPrensaLabioYPrecortes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_Labio",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_PrecorteWicket",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "NO");

            migrationBuilder.AddColumn<string>(
                name: "Capture_PrecorteZipper",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "NO");

            migrationBuilder.AddColumn<string>(
                name: "Capture_PrensaManga",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // El inventario de mangas por prensa ahora es el del evaluador de factibilidad (index.html de
            // Producción): se agrega 730 (VISION 4) y se desactivan 680 y 732, que no están en él. Desactivar
            // no borra: los NPIs que ya los tienen los siguen mostrando.
            migrationBuilder.Sql(@"
DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
IF @MangaId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM CatalogOptions WHERE CatalogId = @MangaId AND Value = N'730')
        INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive)
        VALUES (NEWID(), @MangaId, N'730', (SELECT DisplayOrder FROM CatalogOptions WHERE CatalogId = @MangaId AND Value = N'720'), 1);
    UPDATE CatalogOptions SET IsActive = 0 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
IF @MangaId IS NOT NULL
    UPDATE CatalogOptions SET IsActive = 1 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');");

            migrationBuilder.DropColumn(
                name: "Capture_Labio",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_PrecorteWicket",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_PrecorteZipper",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_PrensaManga",
                table: "Npis");
        }
    }
}
