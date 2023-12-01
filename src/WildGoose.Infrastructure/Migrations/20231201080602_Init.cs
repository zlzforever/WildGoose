using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WildGoose.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wild_goose_organization",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    parent_id = table.Column<string>(type: "character varying(36)", nullable: true),
                    creation_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creator_id = table.Column<string>(type: "text", nullable: true),
                    creator_name = table.Column<string>(type: "text", nullable: true),
                    last_modifier_id = table.Column<string>(type: "text", nullable: true),
                    last_modifier_name = table.Column<string>(type: "text", nullable: true),
                    last_modification_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleter_id = table.Column<string>(type: "text", nullable: true),
                    deleter_name = table.Column<string>(type: "text", nullable: true),
                    deletion_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_organization", x => x.id);
                    table.ForeignKey(
                        name: "FK_wild_goose_organization_wild_goose_organization_parent_id",
                        column: x => x.parent_id,
                        principalTable: "wild_goose_organization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_organization_administrator",
                columns: table => new
                {
                    organization_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_organization_administrator", x => new { x.organization_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_organization_scope",
                columns: table => new
                {
                    organization_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    scope = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_organization_scope", x => new { x.organization_id, x.scope });
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_organization_user",
                columns: table => new
                {
                    organization_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    user_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_organization_user", x => new { x.organization_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_role",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    statement = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: true),
                    creation_time = table.Column<long>(type: "bigint", nullable: true),
                    creator_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    creator_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    last_modifier_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    last_modifier_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    last_modification_time = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_role_assignable_role",
                columns: table => new
                {
                    role_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    assignable_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_role_assignable_role", x => new { x.role_id, x.assignable_id });
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    given_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    family_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    middle_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    nick_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    picture = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    creation_time = table.Column<long>(type: "bigint", nullable: true),
                    creator_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    creator_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    last_modifier_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    last_modifier_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    last_modification_time = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleter_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    deleter_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    deletion_time = table.Column<long>(type: "bigint", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user_extension",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    departure_time = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    password_contains_digit = table.Column<bool>(type: "boolean", nullable: false),
                    password_contains_lowercase = table.Column<bool>(type: "boolean", nullable: false),
                    password_contains_uppercase = table.Column<bool>(type: "boolean", nullable: false),
                    password_contains_non_alphanumeric = table.Column<bool>(type: "boolean", nullable: false),
                    password_length = table.Column<int>(type: "integer", nullable: false),
                    reset_password_flag = table.Column<bool>(type: "boolean", nullable: false),
                    hidden_sensitive_data = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user_extension", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_role_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "character varying(36)", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_role_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_wild_goose_role_claim_wild_goose_role_role_id",
                        column: x => x.role_id,
                        principalTable: "wild_goose_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(36)", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "FK_wild_goose_user_claim_wild_goose_user_user_id",
                        column: x => x.user_id,
                        principalTable: "wild_goose_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "character varying(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "FK_wild_goose_user_login_wild_goose_user_user_id",
                        column: x => x.user_id,
                        principalTable: "wild_goose_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user_role",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(36)", nullable: false),
                    role_id = table.Column<string>(type: "character varying(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "FK_wild_goose_user_role_wild_goose_role_role_id",
                        column: x => x.role_id,
                        principalTable: "wild_goose_role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wild_goose_user_role_wild_goose_user_user_id",
                        column: x => x.user_id,
                        principalTable: "wild_goose_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wild_goose_user_token",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "character varying(36)", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wild_goose_user_token", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "FK_wild_goose_user_token_wild_goose_user_user_id",
                        column: x => x.user_id,
                        principalTable: "wild_goose_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_organization_parent_id",
                table: "wild_goose_organization",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "wild_goose_role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_role_claim_role_id",
                table: "wild_goose_role_claim",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "wild_goose_user",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "wild_goose_user",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_user_claim_user_id",
                table: "wild_goose_user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_user_login_user_id",
                table: "wild_goose_user_login",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_wild_goose_user_role_role_id",
                table: "wild_goose_user_role",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wild_goose_organization");

            migrationBuilder.DropTable(
                name: "wild_goose_organization_administrator");

            migrationBuilder.DropTable(
                name: "wild_goose_organization_scope");

            migrationBuilder.DropTable(
                name: "wild_goose_organization_user");

            migrationBuilder.DropTable(
                name: "wild_goose_role_assignable_role");

            migrationBuilder.DropTable(
                name: "wild_goose_role_claim");

            migrationBuilder.DropTable(
                name: "wild_goose_user_claim");

            migrationBuilder.DropTable(
                name: "wild_goose_user_extension");

            migrationBuilder.DropTable(
                name: "wild_goose_user_login");

            migrationBuilder.DropTable(
                name: "wild_goose_user_role");

            migrationBuilder.DropTable(
                name: "wild_goose_user_token");

            migrationBuilder.DropTable(
                name: "wild_goose_role");

            migrationBuilder.DropTable(
                name: "wild_goose_user");
        }
    }
}
