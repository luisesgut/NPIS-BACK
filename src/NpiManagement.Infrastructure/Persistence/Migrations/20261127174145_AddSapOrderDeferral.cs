using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSapOrderDeferral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SapOrderDeferralReason",
                table: "Npis",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SapOrderDeferredAt",
                table: "Npis",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SapOrderDeferredByName",
                table: "Npis",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SapOrderDeferredUntil",
                table: "Npis",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SapOrderDeferralReason",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "SapOrderDeferredAt",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "SapOrderDeferredByName",
                table: "Npis");

            migrationBuilder.DropColumn(
                name: "SapOrderDeferredUntil",
                table: "Npis");
        }
    }
}
