using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Migración de datos (no de esquema) para el paso "Producto" del wizard de captura:
    /// - "TERMINADO" pasa a usarse solo para Película de Impresión, con opciones nuevas.
    /// - "TERMINADO LAMINACION" es un catálogo nuevo para Película 1ª y 2ª Laminación (comparten
    ///   la misma lista).
    /// - "TIPO DE PELICULA" es un catálogo nuevo — antes "Tipo de Película" (PI/PPL/PSL) era texto
    ///   libre, ahora es un desplegable, mismo catálogo para las 3 capas.
    /// Mismo patrón que UpdateProductCatalogsAndAddUnidadVentaCatalog: el CatalogId se resuelve en
    /// tiempo de ejecución por Key, nunca un GUID fijo.
    /// </summary>
    /// <inheritdoc />
    public partial class UpdateTerminadoAndAddTipoPeliculaCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @TerminadoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TERMINADO');
IF @TerminadoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TerminadoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @TerminadoId, N'TRANSPARENTE', 0, 1),
        (NEWID(), @TerminadoId, N'METALIZADO', 1, 1),
        (NEWID(), @TerminadoId, N'BLANCO', 2, 1),
        (NEWID(), @TerminadoId, N'MATE', 3, 1),
        (NEWID(), @TerminadoId, N'OTRO', 4, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @TerminadoLamId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TERMINADO LAMINACION');
IF @TerminadoLamId IS NULL
BEGIN
    SET @TerminadoLamId = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@TerminadoLamId, N'TERMINADO LAMINACION', N'Terminado (Laminación)');
END
DELETE FROM CatalogOptions WHERE CatalogId = @TerminadoLamId;
INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @TerminadoLamId, N'N/A', 0, 1),
    (NEWID(), @TerminadoLamId, N'BAJO COF', 1, 1),
    (NEWID(), @TerminadoLamId, N'OXODEGRADABLE', 2, 1),
    (NEWID(), @TerminadoLamId, N'BAJO TIS', 3, 1),
    (NEWID(), @TerminadoLamId, N'CAVITADO', 4, 1),
    (NEWID(), @TerminadoLamId, N'ALTA BARRERA', 5, 1),
    (NEWID(), @TerminadoLamId, N'RETORTABLE', 6, 1),
    (NEWID(), @TerminadoLamId, N'ANTIFOG', 7, 1),
    (NEWID(), @TerminadoLamId, N'PELABLE', 8, 1),
    (NEWID(), @TerminadoLamId, N'ALOX', 9, 1),
    (NEWID(), @TerminadoLamId, N'SELLABLE', 10, 1);");

            migrationBuilder.Sql(@"
DECLARE @TipoPeliculaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TIPO DE PELICULA');
IF @TipoPeliculaId IS NULL
BEGIN
    SET @TipoPeliculaId = NEWID();
    INSERT INTO Catalogs (Id, [Key], DisplayName) VALUES (@TipoPeliculaId, N'TIPO DE PELICULA', N'Tipo de Película');
END
DELETE FROM CatalogOptions WHERE CatalogId = @TipoPeliculaId;
INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
    (NEWID(), @TipoPeliculaId, N'BOPP', 0, 1),
    (NEWID(), @TipoPeliculaId, N'CPP', 1, 1),
    (NEWID(), @TipoPeliculaId, N'PET', 2, 1),
    (NEWID(), @TipoPeliculaId, N'BOPA', 3, 1),
    (NEWID(), @TipoPeliculaId, N'FOIL', 4, 1),
    (NEWID(), @TipoPeliculaId, N'MDO', 5, 1),
    (NEWID(), @TipoPeliculaId, N'PE', 6, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @TerminadoId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TERMINADO');
IF @TerminadoId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TerminadoId;
    INSERT INTO CatalogOptions (Id, CatalogId, Value, DisplayOrder, IsActive) VALUES
        (NEWID(), @TerminadoId, N'NATURAL', 0, 1),
        (NEWID(), @TerminadoId, N'BLANCO', 1, 1),
        (NEWID(), @TerminadoId, N'PIGMENTADO', 2, 1),
        (NEWID(), @TerminadoId, N'METALIZADO', 3, 1),
        (NEWID(), @TerminadoId, N'MATE', 4, 1);
END");

            migrationBuilder.Sql(@"
DECLARE @TerminadoLamId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TERMINADO LAMINACION');
IF @TerminadoLamId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TerminadoLamId;
    DELETE FROM Catalogs WHERE Id = @TerminadoLamId;
END");

            migrationBuilder.Sql(@"
DECLARE @TipoPeliculaId UNIQUEIDENTIFIER = (SELECT Id FROM Catalogs WHERE [Key] = N'TIPO DE PELICULA');
IF @TipoPeliculaId IS NOT NULL
BEGIN
    DELETE FROM CatalogOptions WHERE CatalogId = @TipoPeliculaId;
    DELETE FROM Catalogs WHERE Id = @TipoPeliculaId;
END");
        }
    }
}
