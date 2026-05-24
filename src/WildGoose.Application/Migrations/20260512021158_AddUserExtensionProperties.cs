using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WildGoose.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExtensionProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "property01",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property02",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property03",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property04",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property05",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property06",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property07",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property08",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "property09",
                table: "user_extension",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "property01",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property02",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property03",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property04",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property05",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property06",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property07",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property08",
                table: "user_extension");

            migrationBuilder.DropColumn(
                name: "property09",
                table: "user_extension");
        }
    }
}
