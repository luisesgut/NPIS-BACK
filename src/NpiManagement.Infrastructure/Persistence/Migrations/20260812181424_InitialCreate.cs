using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence<int>(
                name: "NpiNumberSequence",
                schema: "dbo");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Catalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quotations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroCotizacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Cliente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoProducto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DescripcionProducto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EjecutivoVenta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ancho = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PiTipoPelicula = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PiCalibre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PplTipoPelicula = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PplCalibre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PslTipoPelicula = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PslCalibre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PesoBobina = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaponCore = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PiezasCajaBulto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Npis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NpiNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Capture_NumeroCotizacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Cliente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TipoProducto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_DescripcionProducto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_CodigoProdCliente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_ContactoCompras = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_ContactoClienteDiseno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_EjecutivoVenta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Formato = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Acabado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Mercado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_SubMercado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TipoEntrada = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_CategoriaSAP = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TipoImpresion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TipoTinta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TipoEmpaque = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Ancho = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Alto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_FuelleExtendido = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_SolapaPestana = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Fuelles = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PiTipoPelicula = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TerminadoPI = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PiCalibre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_FormulaLDPE_PI = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PplTipoPelicula = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TerminadoPPL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PplCalibre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_FormulaLDPE_PPL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PslTipoPelicula = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TerminadoPSL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PslCalibre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_FormulaLDPE_PSL = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Colgadera = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Cierre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_OtrosElementos = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Sello = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Muesca = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Precorte = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PatronPerforado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_ParcheRefuerzo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Suaje = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_AplicaPieDeCasa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_FiguraEmbob = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_DiametroBobina = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PesoBobina = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_BobinasPorTarima = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_AlturaTarimaMaxBobina = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_MaxUniones = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Core = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_TaponCore = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_DimTarimaBobina = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PiezasCajaBulto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_CajaBultoTarima = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_DimTarimaBolsa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_AlturaTarimaMaxBolsa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_BasePantone = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_BaseMuestra = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_VisitaCliente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_PruebaColor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_ArchivosEditables = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_MuestrasFisicas = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Especificaciones = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Otros = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_CostoPreprensa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Capture_Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TerminalReason = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EngineeringAreaStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DesignAreaStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AssignedEngineeringUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedDesignUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedCommercialUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GlobalTimerStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GlobalTimerEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GlobalTimerAccumulatedMs = table.Column<long>(type: "bigint", nullable: false),
                    GlobalTimerRunningSinceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommercialTimerStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommercialTimerEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CommercialTimerAccumulatedMs = table.Column<long>(type: "bigint", nullable: false),
                    CommercialTimerRunningSinceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EngineeringTimerStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EngineeringTimerEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EngineeringTimerAccumulatedMs = table.Column<long>(type: "bigint", nullable: false),
                    EngineeringTimerRunningSinceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DesignTimerStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DesignTimerEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DesignTimerAccumulatedMs = table.Column<long>(type: "bigint", nullable: false),
                    DesignTimerRunningSinceAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArtesValidadasAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArtesValidadasByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrintCardLoadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PrintCardLoadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrintCardLoadedVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrintCardSentToClientAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PrintCardSentToClientByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CodigoPT = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PtName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PtRegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PtRegisteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PedidoSapNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PedidoSapRegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PedidoSapRegisteredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EtCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EtCreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EtLoadedInSisproAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EtLoadedInSisproByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuppliesTemporaryRejection_At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SuppliesTemporaryRejection_ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuppliesTemporaryRejection_Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuppliesAgreement_Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SuppliesAgreement_ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SuppliesAgreement_ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuppliesAgreement_EvidenceAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientRejection_At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClientRejection_ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientRejection_Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClientRejection_EvidenceAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Npis_AspNetUsers_AssignedCommercialUserId",
                        column: x => x.AssignedCommercialUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Npis_AspNetUsers_AssignedDesignUserId",
                        column: x => x.AssignedDesignUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Npis_AspNetUsers_AssignedEngineeringUserId",
                        column: x => x.AssignedEngineeringUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Npis_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogOptions_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Context = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromArea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToArea = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FieldKeys = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeRequests_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeasibilityReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeasibilityReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeasibilityReviews_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InformationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformationRequests_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpiComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpiComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpiComments_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintCardVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintCardVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintCardVersions_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DurationMsAtEvent = table.Column<long>(type: "bigint", nullable: true),
                    FromStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimerEvents_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrintCardApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PrintCardVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EvidenceAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintCardApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintCardApprovals_PrintCardVersions_PrintCardVersionId",
                        column: x => x.PrintCardVersionId,
                        principalTable: "PrintCardVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Area",
                table: "AspNetUsers",
                column: "Area");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Context",
                table: "Attachments",
                column: "Context");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_NpiId",
                table: "Attachments",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StorageKey",
                table: "Attachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorUserId",
                table: "AuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_NpiId",
                table: "AuditEvents",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogOptions_CatalogId_Value",
                table: "CatalogOptions",
                columns: new[] { "CatalogId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Catalogs_Key",
                table: "Catalogs",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRequests_NpiId",
                table: "ChangeRequests",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_FeasibilityReviews_NpiId",
                table: "FeasibilityReviews",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_FeasibilityReviews_NpiId_Area",
                table: "FeasibilityReviews",
                columns: new[] { "NpiId", "Area" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InformationRequests_NpiId",
                table: "InformationRequests",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_InformationRequests_Status",
                table: "InformationRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NpiComments_CreatedAt",
                table: "NpiComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NpiComments_NpiId",
                table: "NpiComments",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_AssignedCommercialUserId",
                table: "Npis",
                column: "AssignedCommercialUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_AssignedDesignUserId",
                table: "Npis",
                column: "AssignedDesignUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_AssignedEngineeringUserId",
                table: "Npis",
                column: "AssignedEngineeringUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_Capture_Cliente",
                table: "Npis",
                column: "Capture_Cliente");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_Capture_NumeroCotizacion",
                table: "Npis",
                column: "Capture_NumeroCotizacion");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_CreatedAt",
                table: "Npis",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_CreatedByUserId",
                table: "Npis",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_DesignAreaStatus",
                table: "Npis",
                column: "DesignAreaStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_EngineeringAreaStatus",
                table: "Npis",
                column: "EngineeringAreaStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_LifecycleStatus",
                table: "Npis",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_NpiNumber",
                table: "Npis",
                column: "NpiNumber",
                unique: true,
                filter: "[NpiNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Npis_RequestDate",
                table: "Npis",
                column: "RequestDate");

            migrationBuilder.CreateIndex(
                name: "IX_PrintCardApprovals_PrintCardVersionId_Role",
                table: "PrintCardApprovals",
                columns: new[] { "PrintCardVersionId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintCardVersions_NpiId",
                table: "PrintCardVersions",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintCardVersions_NpiId_VersionNumber",
                table: "PrintCardVersions",
                columns: new[] { "NpiId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_NumeroCotizacion",
                table: "Quotations",
                column: "NumeroCotizacion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimerEvents_NpiId",
                table: "TimerEvents",
                column: "NpiId");

            migrationBuilder.CreateIndex(
                name: "IX_TimerEvents_OccurredAt",
                table: "TimerEvents",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "CatalogOptions");

            migrationBuilder.DropTable(
                name: "ChangeRequests");

            migrationBuilder.DropTable(
                name: "FeasibilityReviews");

            migrationBuilder.DropTable(
                name: "InformationRequests");

            migrationBuilder.DropTable(
                name: "NpiComments");

            migrationBuilder.DropTable(
                name: "PrintCardApprovals");

            migrationBuilder.DropTable(
                name: "Quotations");

            migrationBuilder.DropTable(
                name: "TimerEvents");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Catalogs");

            migrationBuilder.DropTable(
                name: "PrintCardVersions");

            migrationBuilder.DropTable(
                name: "Npis");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropSequence(
                name: "NpiNumberSequence",
                schema: "dbo");
        }
    }
}
