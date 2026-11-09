using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// El inventario vigente es la tabla "Inventario de mangas por prensa" (con Schiavi), no el
    /// evaluador de factibilidad: deshace el ajuste de AddMangaPrensaLabioYPrecortes — reactiva 680
    /// (BOBST 1) y 732 (VISION 4) y desactiva 730, que no está en la tabla. Desactivar no borra: un NPI
    /// que ya tenga 730 lo sigue mostrando.
    /// </summary>
    /// <inheritdoc />
    public partial class AlignMangaCatalogWithInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
IF @MangaId IS NOT NULL
BEGIN
    UPDATE CatalogOptions SET IsActive = 1 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');
    UPDATE CatalogOptions SET IsActive = 0 WHERE CatalogId = @MangaId AND Value = N'730';
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @MangaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
IF @MangaId IS NOT NULL
BEGIN
    UPDATE CatalogOptions SET IsActive = 0 WHERE CatalogId = @MangaId AND Value IN (N'680', N'732');
    UPDATE CatalogOptions SET IsActive = 1 WHERE CatalogId = @MangaId AND Value = N'730';
END");
        }
    }
}
