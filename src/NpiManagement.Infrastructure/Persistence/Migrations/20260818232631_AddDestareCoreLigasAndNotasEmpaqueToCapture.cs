using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDestareCoreLigasAndNotasEmpaqueToCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Capture_DestareCore",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_Ligas",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Capture_NotasEmpaqueEmbalaje",
                table: "Npis",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Capture_DestareCore",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_Ligas",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "Capture_NotasEmpaqueEmbalaje",
                table: "Npis");
        }
    }
}
