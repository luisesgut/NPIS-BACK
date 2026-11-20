using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialDirector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DirectorUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DirectorUserId",
                table: "AspNetUsers",
                column: "DirectorUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DirectorUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DirectorUserId",
                table: "AspNetUsers");
        }
    }
}
