using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintCardRejectionAndClientChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RejectedAt",
                table: "PrintCardVersions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedByName",
                table: "PrintCardVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "PrintCardVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectionEvidenceAttachmentId",
                table: "PrintCardVersions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "PrintCardVersions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChecklistJson",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "PrintCardVersions");

            migrationBuilder.DropColumn(
                name: "RejectedByName",
                table: "PrintCardVersions");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "PrintCardVersions");

            migrationBuilder.DropColumn(
                name: "RejectionEvidenceAttachmentId",
                table: "PrintCardVersions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "PrintCardVersions");

            migrationBuilder.DropColumn(
                name: "ChecklistJson",
                table: "ChangeRequests");
        }
    }
}
