using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WildGoose.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNIdToOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "n_id",
                table: "wild_goose_organization",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_organization_n_id",
                table: "wild_goose_organization",
                column: "n_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wild_goose_organization_n_id",
                table: "wild_goose_organization");

            migrationBuilder.DropColumn(
                name: "n_id",
                table: "wild_goose_organization");
        }
    }
}
