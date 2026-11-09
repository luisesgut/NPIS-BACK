using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Campo "Desarrollo de manga (mm)" de la captura + catálogo DESARROLLO MANGA. Opciones: todos los
    /// desarrollos del "Inventario de mangas por prensa" (Bobst 1, Bobst 2/3, Vision 1/2, 3 y 4, Schiavi).
    /// Solo inserta si el catálogo no existe, para no pisar cambios hechos desde Catálogos.
    /// </summary>
    /// <inheritdoc />
    public partial class AddDesarrolloManga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_DesarrolloManga",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA')
BEGIN
    DECLARE @Id UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@Id, N'DESARROLLO MANGA', N'Desarrollo de manga (mm)');
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @Id, N'370', 0, 1),
    (NEWID(), @Id, N'390', 1, 1),
    (NEWID(), @Id, N'400', 2, 1),
    (NEWID(), @Id, N'450', 3, 1),
    (NEWID(), @Id, N'460', 4, 1),
    (NEWID(), @Id, N'470', 5, 1),
    (NEWID(), @Id, N'480', 6, 1),
    (NEWID(), @Id, N'490', 7, 1),
    (NEWID(), @Id, N'500', 8, 1),
    (NEWID(), @Id, N'520', 9, 1),
    (NEWID(), @Id, N'540', 10, 1),
    (NEWID(), @Id, N'550', 11, 1),
    (NEWID(), @Id, N'560', 12, 1),
    (NEWID(), @Id, N'570', 13, 1),
    (NEWID(), @Id, N'580', 14, 1),
    (NEWID(), @Id, N'590', 15, 1),
    (NEWID(), @Id, N'600', 16, 1),
    (NEWID(), @Id, N'610', 17, 1),
    (NEWID(), @Id, N'620', 18, 1),
    (NEWID(), @Id, N'630', 19, 1),
    (NEWID(), @Id, N'650', 20, 1),
    (NEWID(), @Id, N'660', 21, 1),
    (NEWID(), @Id, N'670', 22, 1),
    (NEWID(), @Id, N'680', 23, 1),
    (NEWID(), @Id, N'700', 24, 1),
    (NEWID(), @Id, N'720', 25, 1),
    (NEWID(), @Id, N'732', 26, 1),
    (NEWID(), @Id, N'740', 27, 1),
    (NEWID(), @Id, N'750', 28, 1),
    (NEWID(), @Id, N'800', 29, 1),
    (NEWID(), @Id, N'N/A', 30, 1);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @Id UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'DESARROLLO MANGA');
IF @Id IS NOT NULL
BEGIN
    DELETE FROM CatalogChangeLogs WHERE CatalogId = @Id;
    DELETE FROM CatalogOptions WHERE CatalogId = @Id;
    DELETE FROM Catalogs WHERE Id = @Id;
END");

            migrationBuilder.DropColumn(
                name: "Capture_DesarrolloManga",
                table: "Npis");
        }
    }
}
