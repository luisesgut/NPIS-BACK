using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPtSapTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PtSapTable",
                table: "Npis",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PtSapTable",
                table: "Npis");
        }
    }
}
