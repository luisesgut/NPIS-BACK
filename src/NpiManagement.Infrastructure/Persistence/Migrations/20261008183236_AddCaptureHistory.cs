using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptureHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstSubmittedAt",
                table: "Npis",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstSubmittedByUserId",
                table: "Npis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialCaptureJson",
                table: "Npis",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NpiCaptureChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Before = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    After = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WasRequested = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpiCaptureChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpiCaptureChanges_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpiCaptureChanges_NpiId_ChangedAt",
                table: "NpiCaptureChanges",
                columns: new[] { "NpiId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpiCaptureChanges");

            migrationBuilder.DropColumn(
                name: "FirstSubmittedAt",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "FirstSubmittedByUserId",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "InitialCaptureJson",
                table: "Npis");
        }
    }
}
