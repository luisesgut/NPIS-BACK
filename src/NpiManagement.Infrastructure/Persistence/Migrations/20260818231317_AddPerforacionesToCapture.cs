using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerforacionesToCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_DiametroPerforaciones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_NumeroPerforaciones",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_Perforaciones",
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
                name: "Capture_DiametroPerforaciones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_NumeroPerforaciones",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_Perforaciones",
                table: "Npis");
        }
    }
}
