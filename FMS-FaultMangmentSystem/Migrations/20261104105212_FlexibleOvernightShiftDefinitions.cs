using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleOvernightShiftDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShiftDefinitions_TimeRange",
                table: "ShiftDefinitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShiftDefinitions_TimeRange",
                table: "ShiftDefinitions",
                sql: "([IsOvernight] = 1) OR ([IsOvernight] = 0 AND [StartTime] < [EndTime])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ShiftDefinitions_TimeRange",
                table: "ShiftDefinitions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ShiftDefinitions_TimeRange",
                table: "ShiftDefinitions",
                sql: "([IsOvernight] = 1 AND [EndTime] <= [StartTime]) OR ([IsOvernight] = 0 AND [StartTime] < [EndTime])");
        }
    }
}
