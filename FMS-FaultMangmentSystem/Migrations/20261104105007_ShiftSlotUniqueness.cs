using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class ShiftSlotUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_UserId_ShiftDate",
                table: "ShiftAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_UserId_ShiftDate",
                table: "ShiftAssignments",
                columns: new[] { "WeeklyShiftScheduleId", "UserId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_DepartmentId_ShiftDate_ShiftDefinitionId",
                table: "ShiftAssignments",
                columns: new[] { "WeeklyShiftScheduleId", "DepartmentId", "ShiftDate", "ShiftDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_DepartmentId",
                table: "ShiftAssignments",
                column: "DepartmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_DepartmentId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_DepartmentId_ShiftDate_ShiftDefinitionId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_UserId_ShiftDate",
                table: "ShiftAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_UserId_ShiftDate",
                table: "ShiftAssignments",
                columns: new[] { "WeeklyShiftScheduleId", "UserId", "ShiftDate" },
                unique: true);
        }
    }
}
