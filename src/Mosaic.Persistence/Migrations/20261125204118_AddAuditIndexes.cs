using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mosaic.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_action_occurred_at",
                table: "audit_entries",
                columns: new[] { "action", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor_id_occurred_at",
                table: "audit_entries",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_occurred_at_id",
                table: "audit_entries",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_outcome_occurred_at",
                table: "audit_entries",
                columns: new[] { "outcome", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_target_type_target_id_occurred_at",
                table: "audit_entries",
                columns: new[] { "target_type", "target_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_entries_action_occurred_at",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_actor_id_occurred_at",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_occurred_at_id",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_outcome_occurred_at",
                table: "audit_entries");

            migrationBuilder.DropIndex(
                name: "ix_audit_entries_target_type_target_id_occurred_at",
                table: "audit_entries");
        }
    }
}
