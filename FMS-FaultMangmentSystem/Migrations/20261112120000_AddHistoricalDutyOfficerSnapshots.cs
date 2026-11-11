using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalDutyOfficerSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DutyOfficerFullNameSnapshot",
                table: "IncidentTransfers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DutyOfficerRankSnapshot",
                table: "IncidentTransfers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialDutyOfficerFullNameSnapshot",
                table: "Incidents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialDutyOfficerRankSnapshot",
                table: "Incidents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DutyOfficerFullNameSnapshot",
                table: "IncidentDepartmentAssignments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DutyOfficerRankSnapshot",
                table: "IncidentDepartmentAssignments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rank",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DutyOfficerFullNameSnapshot",
                table: "IncidentTransfers");

            migrationBuilder.DropColumn(
                name: "DutyOfficerRankSnapshot",
                table: "IncidentTransfers");

            migrationBuilder.DropColumn(
                name: "InitialDutyOfficerFullNameSnapshot",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "InitialDutyOfficerRankSnapshot",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "DutyOfficerFullNameSnapshot",
                table: "IncidentDepartmentAssignments");

            migrationBuilder.DropColumn(
                name: "DutyOfficerRankSnapshot",
                table: "IncidentDepartmentAssignments");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "AspNetUsers");
        }
    }
}
