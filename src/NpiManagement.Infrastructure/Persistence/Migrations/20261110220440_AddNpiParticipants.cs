using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNpiParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NpiParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NpiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpiParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpiParticipants_Npis_NpiId",
                        column: x => x.NpiId,
                        principalTable: "Npis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpiParticipants_NpiId_UserId_Area",
                table: "NpiParticipants",
                columns: new[] { "NpiId", "UserId", "Area" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpiParticipants_UserId",
                table: "NpiParticipants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpiParticipants");
        }
    }
}
