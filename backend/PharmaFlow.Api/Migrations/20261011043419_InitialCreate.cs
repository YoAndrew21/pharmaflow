using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmaFlow.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medications",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    strength_label = table.Column<string>(type: "text", nullable: false),
                    form_code = table.Column<string>(type: "text", nullable: false),
                    presentation_label = table.Column<string>(type: "text", nullable: false),
                    category_code = table.Column<string>(type: "text", nullable: false),
                    prescription_required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_branches",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    fictional_location = table.Column<string>(type: "text", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pharmacy_branches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "medication_active_ingredients",
                columns: table => new
                {
                    medication_id = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    ingredient = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medication_active_ingredients", x => new { x.medication_id, x.position });
                    table.CheckConstraint("ck_medication_active_ingredients_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_medication_active_ingredients_medications",
                        column: x => x.medication_id,
                        principalTable: "medications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "availability",
                columns: table => new
                {
                    medication_id = table.Column<string>(type: "text", nullable: false),
                    branch_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    price_minor_units = table.Column<long>(type: "bigint", nullable: true),
                    price_currency_code = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability", x => new { x.medication_id, x.branch_id });
                    table.CheckConstraint("ck_availability_price_consistency", "(price_minor_units IS NULL AND price_currency_code IS NULL) OR (price_minor_units IS NOT NULL AND price_currency_code IS NOT NULL)");
                    table.CheckConstraint("ck_availability_price_nonnegative", "price_minor_units IS NULL OR price_minor_units >= 0");
                    table.CheckConstraint("ck_availability_status", "status IN ('available', 'low-stock', 'unavailable')");
                    table.ForeignKey(
                        name: "fk_availability_medications",
                        column: x => x.medication_id,
                        principalTable: "medications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_availability_pharmacy_branches",
                        column: x => x.branch_id,
                        principalTable: "pharmacy_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opening_intervals",
                columns: table => new
                {
                    pharmacy_branch_id = table.Column<string>(type: "text", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_intervals", x => new { x.pharmacy_branch_id, x.day_of_week, x.position });
                    table.CheckConstraint("ck_opening_intervals_position", "position >= 0");
                    table.CheckConstraint("ck_opening_intervals_time_order", "opens_at < closes_at");
                    table.CheckConstraint("ck_opening_intervals_weekday", "day_of_week BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "fk_opening_intervals_pharmacy_branches",
                        column: x => x.pharmacy_branch_id,
                        principalTable: "pharmacy_branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_availability_branch_id",
                table: "availability",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ux_medications_slug",
                table: "medications",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_pharmacy_branches_slug",
                table: "pharmacy_branches",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability");

            migrationBuilder.DropTable(
                name: "medication_active_ingredients");

            migrationBuilder.DropTable(
                name: "opening_intervals");

            migrationBuilder.DropTable(
                name: "medications");

            migrationBuilder.DropTable(
                name: "pharmacy_branches");
        }
    }
}
