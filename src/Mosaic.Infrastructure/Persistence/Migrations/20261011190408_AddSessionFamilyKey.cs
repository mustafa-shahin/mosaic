using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mosaic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionFamilyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_session_tokens_family_id",
                table: "session_tokens",
                column: "family_id");

            migrationBuilder.AddForeignKey(
                name: "fk_session_tokens_session_tokens_family_id",
                table: "session_tokens",
                column: "family_id",
                principalTable: "session_tokens",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_session_tokens_session_tokens_family_id",
                table: "session_tokens");

            migrationBuilder.DropIndex(
                name: "ix_session_tokens_family_id",
                table: "session_tokens");
        }
    }
}
