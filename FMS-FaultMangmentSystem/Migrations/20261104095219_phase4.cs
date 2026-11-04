using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class phase4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromDepartmentId",
                table: "IncidentTransferRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ToDepartmentId",
                table: "IncidentTransferRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FinalSolutionDescription",
                table: "Incidents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionSteps",
                table: "Incidents",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_FromDepartmentId",
                table: "IncidentTransferRequests",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_ToDepartmentId",
                table: "IncidentTransferRequests",
                column: "ToDepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentTransferRequests_Departments_FromDepartmentId",
                table: "IncidentTransferRequests",
                column: "FromDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentTransferRequests_Departments_ToDepartmentId",
                table: "IncidentTransferRequests",
                column: "ToDepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IncidentTransferRequests_Departments_FromDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentTransferRequests_Departments_ToDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_IncidentTransferRequests_FromDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_IncidentTransferRequests_ToDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropColumn(
                name: "FromDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropColumn(
                name: "ToDepartmentId",
                table: "IncidentTransferRequests");

            migrationBuilder.DropColumn(
                name: "FinalSolutionDescription",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResolutionSteps",
                table: "Incidents");
        }
    }
}
