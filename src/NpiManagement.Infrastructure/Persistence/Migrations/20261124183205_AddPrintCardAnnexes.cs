using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintCardAnnexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnnexAttachmentIds",
                table: "PrintCardVersions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnexAttachmentIds",
                table: "PrintCardVersions");
        }
    }
}
