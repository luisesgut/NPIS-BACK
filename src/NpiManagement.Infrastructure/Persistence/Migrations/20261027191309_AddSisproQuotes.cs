using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Cotización en SISPRO (2026-10-27): equivalencias de materiales y cotizaciones ligadas al NPI.
    /// La tabla Quotations (cotizaciones locales de demo) sale del modelo pero NO se borra aquí: en
    /// producción podría tener datos que nadie ha revisado. Se puede borrar a mano después de
    /// confirmar que está vacía o que no sirve (ver scripts/migracion-2026-10-27-cotizacion-sispro.sql).
    /// </summary>
    public partial class AddSisproQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SisproMaterialEquivalences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterialSispro = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TipoPelicula = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Terminado = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FormulaLdpe = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SisproMaterialEquivalences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SisproQuoteLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    NumeroCotizacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EstadoSispro = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StructureWarnings = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    LinkedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RespondedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponseNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InvalidationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SisproQuoteLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SisproQuoteLinks_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SisproMaterialEquivalences_MaterialSispro",
                table: "SisproMaterialEquivalences",
                column: "MaterialSispro",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SisproQuoteLinks_NpiId",
                table: "SisproQuoteLinks",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_SisproQuoteLinks_NpiId_VersionNumber",
                table: "SisproQuoteLinks",
                columns: new[] { "NpiId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SisproQuoteLinks_NumeroCotizacion",
                table: "SisproQuoteLinks",
                column: "NumeroCotizacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SisproMaterialEquivalences");

            migrationBuilder.DropTable(
                name: "SisproQuoteLinks");
        }
    }
}
