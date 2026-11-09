using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalculoBobina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_Repeticion",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CoresBobina",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DiametroExteriorM = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    KgPorCm = table.Column<decimal>(type: "decimal(10,5)", precision: 10, scale: 5, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoresBobina", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialesBobina",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DensidadKgM3 = table.Column<decimal>(type: "decimal(12,4)", precision: 12, scale: 4, nullable: false),
                    TipoPelicula = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Terminado = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FormulaLdpe = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EsEstimado = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialesBobina", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParametrosBobina",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AdhesivoGm2 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TintaGm2 = table.Column<decimal>(type: "decimal(10,4)", precision: 10, scale: 4, nullable: false),
                    TarimaAnchoCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TarimaLargoCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TarimaAltoMaxCm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ToleranciaAlto = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrosBobina", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NpiMaterialesExactos",
                columns: table => new
                {
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Capa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaterialBobinaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SetByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpiMaterialesExactos", x => new { x.NpiId, x.Capa });
                    table.ForeignKey(
                        name: "FK_NpiMaterialesExactos_MaterialesBobina_MaterialBobinaId",
                        column: x => x.MaterialBobinaId,
                        principalTable: "MaterialesBobina",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpiMaterialesExactos_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoresBobina_Nombre",
                table: "CoresBobina",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialesBobina_Codigo",
                table: "MaterialesBobina",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpiMaterialesExactos_MaterialBobinaId",
                table: "NpiMaterialesExactos",
                column: "MaterialBobinaId");
            // Semilla (spec "Cálculos de bobina", Excel convertidor). Densidades tal cual el Excel; BOPA (2700,
            // igual que FOIL), ME-10 y ME-22 (densidad 0) quedan inactivos hasta que Ingeniería las confirme.
            // Los códigos de PE (CE-xx, M-xx, ME-xx…) no se ligan a un tipo de película: solo los elige
            // Ingeniería como material exacto. El PE capturado por Comercial usa los estimados (920 kg/m³).
            migrationBuilder.Sql(@"
INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive) VALUES
(NEWID(), N'PET', 1400, N'PET', NULL, NULL, 0, 1),
(NEWID(), N'PET MET', 1400, N'PET', N'METALIZADO', NULL, 0, 1),
(NEWID(), N'PET ALOX', 1400, N'PET', N'ALOX', NULL, 0, 1),
(NEWID(), N'PET MATE', 1400, N'PET', N'MATE', NULL, 0, 1),
(NEWID(), N'FOIL', 2700, N'FOIL', NULL, NULL, 0, 1),
(NEWID(), N'BOPA', 2700, N'BOPA', NULL, NULL, 0, 0),
(NEWID(), N'BOPP', 910, N'BOPP', NULL, NULL, 0, 1),
(NEWID(), N'BOPP ANTIFOG', 910, N'BOPP', N'ANTIFOG', NULL, 0, 1),
(NEWID(), N'BOPP CAVITADO', 680, N'BOPP', N'CAVITADO', NULL, 0, 1),
(NEWID(), N'BOPP MATE', 910, N'BOPP', N'MATE', NULL, 0, 1),
(NEWID(), N'BOPP MET', 910, N'BOPP', N'METALIZADO', NULL, 0, 1),
(NEWID(), N'BOPP OXO', 910, N'BOPP', N'OXODEGRADABLE', NULL, 0, 1),
(NEWID(), N'CPP', 900, N'CPP', NULL, NULL, 0, 1),
(NEWID(), N'CPP OXO', 910, N'CPP', N'OXODEGRADABLE', NULL, 0, 1),
(NEWID(), N'C-02', 990, NULL, NULL, NULL, 0, 1),
(NEWID(), N'C-03', 900, NULL, NULL, NULL, 0, 1),
(NEWID(), N'C-04', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-01', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-02', 961.367, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-03', 918.1325, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-04', 932.7974999999999, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-05', 919.9359999999999, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-06', 1082.319, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-07', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-08', 920.176, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-09', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-10', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-11', 930.329, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-12', 919.065, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-13', 927.2504, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-15', 1068.372, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-14', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-16', 1036.53, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-17', 919.651, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-18', 990.51, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-20', 978.564, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-21', 919.314, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-22', 931.562, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-23', 1049.865, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-24', 927, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-25', 919.32, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-26', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-27', 971.785, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-01', 921.025, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-02', 938.8199999999999, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-03', 921.5550000000001, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-04', 972.09, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-05', 921.31, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-06', 918.9000000000001, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-08', 971.82, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-09', 966.855, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-11', 1237.7800000000002, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-12', 1242.4859999999999, NULL, NULL, NULL, 0, 1),
(NEWID(), N'M-13', 1189.5, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-01', 920.2, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-02', 922.356, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-03', 926.886, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-04', 927.466, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-07', 1265, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-08', 923.345, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-09', 919.6, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-10', 0, NULL, NULL, NULL, 0, 0),
(NEWID(), N'ME-14', 985.12, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-15', 925.925, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-16', 920.75, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-17', 926.3000000000001, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-18', 971.785, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-19', 992.2800000000001, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-20', 926.175, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-21', 923.67, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-22', 0, NULL, NULL, NULL, 0, 0),
(NEWID(), N'SIGLO XX', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-32', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-18', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'ME-24', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-14', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-12', 1610, NULL, NULL, NULL, 0, 1),
(NEWID(), N'PCR', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-19', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MDO PE', 940, N'MDO', NULL, NULL, 0, 1),
(NEWID(), N'CE-31', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-36', 950, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CE-35', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-24', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'Shannon w/eva', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-27', 950, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-25', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-26', 950, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-21', 990, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-08', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-28', 950, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-29', 950, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-11', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'CPP MET', 900, N'CPP', N'METALIZADO', NULL, 0, 1),
(NEWID(), N'MCE-17', 920, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-30', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-32', 930, NULL, NULL, NULL, 0, 1),
(NEWID(), N'MCE-31', 900, NULL, NULL, NULL, 0, 1);

INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive)
VALUES (NEWID(), N'PE ESTIMADO', 920, N'PE', NULL, NULL, 1, 1);

-- Un estimado por cada fórmula LDPE que exista en el catálogo, para poder ajustarlos por separado.
INSERT INTO MaterialesBobina (Id, Codigo, DensidadKgM3, TipoPelicula, Terminado, FormulaLdpe, EsEstimado, IsActive)
SELECT NEWID(), LEFT(N'PE ESTIMADO ' + o.Value, 60), 920, N'PE', NULL, o.Value, 1, 1
FROM CatalogOptions o JOIN Catalogs c ON c.Id = o.CatalogId
WHERE c.[Key] = N'FORMULA LDPE' AND o.Value NOT IN (N'N/A', N'NA')
  AND NOT EXISTS (SELECT 1 FROM MaterialesBobina m WHERE m.Codigo = LEFT(N'PE ESTIMADO ' + o.Value, 60));

INSERT INTO CoresBobina (Id, Nombre, DiametroExteriorM, KgPorCm, IsActive) VALUES
(NEWID(), N'3""', 0.095, 0.0188, 1),
(NEWID(), N'6""', 0.1712, 0.02, 1);

INSERT INTO ParametrosBobina (Id, AdhesivoGm2, TintaGm2, TarimaAnchoCm, TarimaLargoCm, TarimaAltoMaxCm, ToleranciaAlto)
VALUES (1, 1.7, 2.0, 100, 120, 150, 0);
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoresBobina");

            migrationBuilder.DropTable(
                name: "NpiMaterialesExactos");

            migrationBuilder.DropTable(
                name: "ParametrosBobina");

            migrationBuilder.DropTable(
                name: "MaterialesBobina");

            migrationBuilder.DropColumn(
                name: "Capture_Repeticion",
                table: "Npis");
        }
    }
}
