using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mosaic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "instance_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    registration_policy = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instance_settings", x => x.id);
                    table.CheckConstraint("ck_instance_settings_registration_policy", "registration_policy IN ('Closed', 'Open')");
                    table.CheckConstraint("ck_instance_settings_single_row", "id = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "instance_settings");
        }
    }
}
