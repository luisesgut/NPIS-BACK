using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeRequestResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaptureSnapshotJson",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangesAppliedJson",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                table: "ChangeRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByName",
                table: "ChangeRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "ChangeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseComment",
                table: "ChangeRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaptureSnapshotJson",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ChangesAppliedJson",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ResolvedByName",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ResponseComment",
                table: "ChangeRequests");
        }
    }
}
