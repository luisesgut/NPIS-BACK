using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración de datos (no de esquema) — reemplaza las opciones de los catálogos "TIPO DE
    /// PRODUCTO", "FORMATO" y "CATEGORÍA SAP" por las listas nuevas del negocio, y agrega el
    /// catálogo "UNIDAD DE VENTA". El seeder de Development (DevelopmentDataSeeder.cs) solo siembra
    /// una vez (guardado por "if (!await dbContext.Catalogs.AnyAsync())"), así que en cualquier
    /// base ya sembrada esta es la única forma de que los datos nuevos realmente lleguen. El
    /// CatalogId de cada catálogo se resuelve en tiempo de ejecución por su Key — nunca un GUID
    /// fijo, porque el seeder los genera con Guid.NewGuid().
    /// </summary>
    /// <inheritdoc />
    public partial class UpdateProductCatalogsAndAddUnidadVentaCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @TipoProductoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TIPO DE PRODUCTO');
IF @TipoProductoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TipoProductoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @TipoProductoId, N'BOBINA', 0, 1),
        (NEWID(), @TipoProductoId, N'BOLSA', 1, 1),
        (NEWID(), @TipoProductoId, N'POUCH', 2, 1),
        (NEWID(), @TipoProductoId, N'OTRO', 3, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @FormatoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'FORMATO');
IF @FormatoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @FormatoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @FormatoId, N'BOBINA PLANA', 0, 1),
        (NEWID(), @FormatoId, N'BOBINA TUBULAR', 1, 1),
        (NEWID(), @FormatoId, N'BOBINA LAMINADA', 2, 1),
        (NEWID(), @FormatoId, N'BOLSA SELLO LATERAL', 3, 1),
        (NEWID(), @FormatoId, N'BOLSA ZIPPER', 4, 1),
        (NEWID(), @FormatoId, N'BOLSA WICKET', 5, 1),
        (NEWID(), @FormatoId, N'STANDUP POUCH (SELLO K)', 6, 1),
        (NEWID(), @FormatoId, N'DOYPACK (SELLO ""U""/SONRISA)', 7, 1),
        (NEWID(), @FormatoId, N'POUCH 2 SELLOS', 8, 1),
        (NEWID(), @FormatoId, N'POUCH 3 SELLOS', 9, 1),
        (NEWID(), @FormatoId, N'POUCH 4 SELLOS', 10, 1),
        (NEWID(), @FormatoId, N'POUCH SELLO TRASLAPE', 11, 1),
        (NEWID(), @FormatoId, N'POUCH SELLO ALETA', 12, 1),
        (NEWID(), @FormatoId, N'OTRO', 13, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @CategoriaSapId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CATEGORÍA SAP');
IF @CategoriaSapId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @CategoriaSapId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @CategoriaSapId, N'BOPP MAP', 0, 1),
        (NEWID(), @CategoriaSapId, N'BOPP NATURAL / IMPRESO', 1, 1),
        (NEWID(), @CategoriaSapId, N'CINTA ACRILICA', 2, 1),
        (NEWID(), @CategoriaSapId, N'CPP ZIPPER', 3, 1),
        (NEWID(), @CategoriaSapId, N'STRECH', 4, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOBINA BARRERA / VACIO (TAPA)', 5, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOBINA ESTRUCTURAS', 6, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOBINA ESTRUCTURAS DIGITAL', 7, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOBINA ESTRUCTURAS DIGITAL TOP SEAL', 8, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOBINA PE / PE', 9, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOLSA STAND UP POUCH', 10, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS BOLSA STAND UP POUCH DIGITAL', 11, 1),
        (NEWID(), @CategoriaSapId, N'PE BARRERA / VACIO', 12, 1),
        (NEWID(), @CategoriaSapId, N'PE BOBINA PLANA', 13, 1),
        (NEWID(), @CategoriaSapId, N'PE BOLSA SELLO LATERAL', 14, 1),
        (NEWID(), @CategoriaSapId, N'PE BOLSA WICKET', 15, 1),
        (NEWID(), @CategoriaSapId, N'PE BOLSA ZIPPER', 16, 1),
        (NEWID(), @CategoriaSapId, N'PE REYMA BOBINA Y BOLSA', 17, 1),
        (NEWID(), @CategoriaSapId, N'BOBINA PE / PE', 18, 1),
        (NEWID(), @CategoriaSapId, N'PE TERMOENCOGIBLE', 19, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @UnidadVentaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'UNIDAD DE VENTA');
IF @UnidadVentaId IS NULL
BEGIN
    SET @UnidadVentaId = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@UnidadVentaId, N'UNIDAD DE VENTA', N'Unidad de venta');
END
DELETE FROM CatalogOptions WHERE CatalogId = @UnidadVentaId;
INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @UnidadVentaId, N'MILL', 0, 1),
    (NEWID(), @UnidadVentaId, N'PZAS', 1, 1),
    (NEWID(), @UnidadVentaId, N'KG', 2, 1),
    (NEWID(), @UnidadVentaId, N'OTRO', 3, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @TipoProductoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TIPO DE PRODUCTO');
IF @TipoProductoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TipoProductoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @TipoProductoId, N'POUCH', 0, 1),
        (NEWID(), @TipoProductoId, N'BOLSA', 1, 1),
        (NEWID(), @TipoProductoId, N'BOBINA', 2, 1),
        (NEWID(), @TipoProductoId, N'BOLSA EN ROLLO', 3, 1),
        (NEWID(), @TipoProductoId, N'BOLSA DE CAMISETA', 4, 1),
        (NEWID(), @TipoProductoId, N'BOLSA BOUTIQUE', 5, 1),
        (NEWID(), @TipoProductoId, N'LAMINA', 6, 1),
        (NEWID(), @TipoProductoId, N'OTRO', 7, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @FormatoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'FORMATO');
IF @FormatoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @FormatoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @FormatoId, N'STANDUP POUCH (SELLO K)', 0, 1),
        (NEWID(), @FormatoId, N'DOYPACK (SELLO ""U""/SONRISA)', 1, 1),
        (NEWID(), @FormatoId, N'POUCH 3 SELLOS', 2, 1),
        (NEWID(), @FormatoId, N'POUCH 2 SELLOS', 3, 1),
        (NEWID(), @FormatoId, N'BOLSA SELLO LATERAL', 4, 1),
        (NEWID(), @FormatoId, N'BOLSA SELLO DE FONDO', 5, 1),
        (NEWID(), @FormatoId, N'BOBINA PLANA', 6, 1),
        (NEWID(), @FormatoId, N'BOBINA TUBULAR', 7, 1),
        (NEWID(), @FormatoId, N'OTRO', 8, 1),
        (NEWID(), @FormatoId, N'N/A', 9, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @CategoriaSapId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'CATEGORÍA SAP');
IF @CategoriaSapId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @CategoriaSapId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @CategoriaSapId, N'BOPP NATURAL / IMPRESO', 0, 1),
        (NEWID(), @CategoriaSapId, N'CINTA ACRÍLICA', 1, 1),
        (NEWID(), @CategoriaSapId, N'COMPLEMENTOS Y OTROS', 2, 1),
        (NEWID(), @CategoriaSapId, N'STRETCH', 3, 1),
        (NEWID(), @CategoriaSapId, N'LAMINADOS', 4, 1),
        (NEWID(), @CategoriaSapId, N'BOLSA STAND UP POUCH', 5, 1),
        (NEWID(), @CategoriaSapId, N'BOLSA STAND UP POUCH DIGITAL', 6, 1),
        (NEWID(), @CategoriaSapId, N'BOBINA ESTRUCTURAS', 7, 1),
        (NEWID(), @CategoriaSapId, N'BOBINA ESTRUCTURAS DIGITAL', 8, 1),
        (NEWID(), @CategoriaSapId, N'BOBINA PE / PE', 9, 1),
        (NEWID(), @CategoriaSapId, N'REYMA BOBINA Y BOLSA', 10, 1),
        (NEWID(), @CategoriaSapId, N'BOBINA PLANA', 11, 1),
        (NEWID(), @CategoriaSapId, N'BOLSA SELLO LATERAL', 12, 1),
        (NEWID(), @CategoriaSapId, N'BOLSA ZIPPER', 13, 1),
        (NEWID(), @CategoriaSapId, N'BOLSA WICKET', 14, 1),
        (NEWID(), @CategoriaSapId, N'BARRERA / VACIO', 15, 1),
        (NEWID(), @CategoriaSapId, N'TERMOENCOGIBLE', 16, 1),
        (NEWID(), @CategoriaSapId, N'VASO IMPRESO', 17, 1),
        (NEWID(), @CategoriaSapId, N'COMPUESTOS', 18, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @UnidadVentaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'UNIDAD DE VENTA');
IF @UnidadVentaId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @UnidadVentaId;
    DELETE FROM Catalogs WHERE Id = @UnidadVentaId;
END");
        }
    }
}
