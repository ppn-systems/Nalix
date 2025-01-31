using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notio.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePasswordHashColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nchar(200)",
                fixedLength: true,
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(60)",
                oldFixedLength: true,
                oldMaxLength: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nchar(60)",
                fixedLength: true,
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(200)",
                oldFixedLength: true,
                oldMaxLength: 200);
        }
    }
}