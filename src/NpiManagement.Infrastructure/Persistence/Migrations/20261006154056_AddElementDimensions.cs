using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElementDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_CierreDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_ColgaderaDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_MuescaDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_OtrosElementosDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_ParcheRefuerzoDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_PatronPerforadoDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_PrecorteDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_SuajeDimensiones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capture_CierreDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_ColgaderaDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_MuescaDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_OtrosElementosDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_ParcheRefuerzoDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_PatronPerforadoDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_PrecorteDimensiones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_SuajeDimensiones",
                table: "Npis");
        }
    }
}
