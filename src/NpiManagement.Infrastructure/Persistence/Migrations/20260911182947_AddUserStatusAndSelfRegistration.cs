using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NpiManagement.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sustituye el booleano <c>IsActive</c> por el enum <c>Status</c> (Pending/Active/Disabled) y
    /// agrega la trazabilidad del alta (RegisteredAt, ApprovedByUserId, ApprovedAt).
    ///
    /// El andamiaje automático de EF proponía eliminar IsActive ANTES de leerlo y dejar Status en
    /// cadena vacía — es decir, perder qué cuentas estaban dadas de baja y dejar a todos los
    /// usuarios existentes con un estado que el enum no sabe interpretar. Esta versión agrega las
    /// columnas como nulas, traduce el valor anterior, y sólo entonces las vuelve obligatorias y
    /// suelta la columna vieja.
    /// </summary>
    public partial class AddUserStatusAndSelfRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Columnas nuevas, nulas por ahora para poder rellenarlas fila por fila.
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            // 2. Traducción del estado anterior. Nadie queda en Pending: las cuentas que ya
            //    existían fueron creadas administrativamente, así que o estaban operando (Active)
            //    o habían sido dadas de baja (Disabled). Pending es exclusivo del auto-registro,
            //    que no existía antes de esta migración.
            migrationBuilder.Sql(@"
UPDATE AspNetUsers
SET Status       = CASE WHEN IsActive = 1 THEN 'Active' ELSE 'Disabled' END,
    RegisteredAt = SYSDATETIMEOFFSET(),
    ApprovedAt   = CASE WHEN IsActive = 1 THEN SYSDATETIMEOFFSET() ELSE NULL END;");

            // 3. Con todas las filas pobladas, ya pueden ser obligatorias.
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "AspNetUsers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            // 4. Sólo ahora se puede soltar la columna vieja, ya exprimida.
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Status",
                table: "AspNetUsers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Simétrico: se reconstruye IsActive a partir de Status antes de perderlo. Las cuentas
            // que estaban en Pending vuelven como inactivas, que es lo más cercano al significado
            // que tenía el booleano.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE AspNetUsers
SET IsActive = CASE WHEN Status = 'Active' THEN 1 ELSE 0 END;");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Status",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(name: "Status", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "RegisteredAt", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "ApprovedByUserId", table: "AspNetUsers");
            migrationBuilder.DropColumn(name: "ApprovedAt", table: "AspNetUsers");
        }
    }
}
