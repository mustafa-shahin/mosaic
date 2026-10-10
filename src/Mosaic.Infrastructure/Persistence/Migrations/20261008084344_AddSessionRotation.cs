using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mosaic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "family_id",
                table: "session_tokens",
                type: "uuid",
                nullable: true);

            // each row here came from a sign-in, so it starts its own family; the generated all-zero default would make one family of them all
            migrationBuilder.Sql("UPDATE session_tokens SET family_id = id;");

            migrationBuilder.AlterColumn<Guid>(
                name: "family_id",
                table: "session_tokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "replaces_id",
                table: "session_tokens",
                type: "uuid",
                nullable: true);

            // before rotation existed every revoked row was a sign-out, and a sign-out now ends expires_at too
            migrationBuilder.Sql("UPDATE session_tokens SET expires_at = revoked_at WHERE revoked_at IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_session_tokens_replaces_id",
                table: "session_tokens",
                column: "replaces_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_session_tokens_session_tokens_replaces_id",
                table: "session_tokens",
                column: "replaces_id",
                principalTable: "session_tokens",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_session_tokens_session_tokens_replaces_id",
                table: "session_tokens");

            migrationBuilder.DropIndex(
                name: "ix_session_tokens_replaces_id",
                table: "session_tokens");

            migrationBuilder.DropColumn(
                name: "family_id",
                table: "session_tokens");

            migrationBuilder.DropColumn(
                name: "replaces_id",
                table: "session_tokens");
        }
    }
}
