using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Campo "Tipo de caja" de la captura (producto terminado bolsa / pouch) + catálogo TIPO DE CAJA.
    /// Solo inserta si el catálogo no existe, para no pisar cambios hechos desde Catálogos.
    /// </summary>
    /// <inheritdoc />
    public partial class AddTipoCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_TipoCaja",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'TIPO DE CAJA')
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'TIPO DE CAJA', N'Tipo de caja');
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @Id, N'CAJA KRAFT', 0, 1),
    (NEWID(), @Id, N'CAJA BLANCA', 1, 1),
    (NEWID(), @Id, N'CAJA CON LOGO', 2, 1),
    (NEWID(), @Id, N'N/A', 3, 1);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @Id UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TIPO DE CAJA');
IF @Id IS NOT NULL
BEGIN
    DELETE FROM CatalogChangeLogs WHERE CatalogId = @Id;
    DELETE FROM CatalogOptions WHERE CatalogId = @Id;
    DELETE FROM Catalogs WHERE Id = @Id;
END");

            migrationBuilder.DropColumn(
                name: "Capture_TipoCaja",
                table: "Npis");
        }
    }
}
