using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNpiBasedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BasedOnNpiId",
                table: "Npis",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasedOnNpiId",
                table: "Npis");
        }
    }
}
