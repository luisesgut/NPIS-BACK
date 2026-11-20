using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Catálogo "COLOR CINTA UNIONES" (2026-11-12): Ingeniería administra los colores de la cinta de
    /// uniones de la bobina; antes eran fijos en el formulario (Blanca, Roja). Sólo datos.
    /// </summary>
    public partial class AddColorCintaCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'COLOR CINTA UNIONES')
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'COLOR CINTA UNIONES', N'Color de cinta para uniones');
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @Id, N'BLANCA', 0, 1),
        (NEWID(), @Id, N'ROJA', 1, 1);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @Id UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'COLOR CINTA UNIONES');
IF @Id IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @Id;
    DELETE FROM Catalogs WHERE Id = @Id;
END");
        }
    }
}
