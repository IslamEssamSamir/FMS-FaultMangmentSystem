using IncidentManagementSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261105050709_AddIncidentDepartmentAssignments")]
public partial class AddIncidentDepartmentAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IncidentDepartmentAssignments",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                IncidentId = table.Column<int>(type: "int", nullable: false),
                DepartmentId = table.Column<int>(type: "int", nullable: false),
                AssignedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                AssignedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                RequiredTask = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                CompletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                RemovedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                RemovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                RemovalReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentDepartmentAssignments", x => x.Id);
                table.UniqueConstraint("AK_IncidentDepartmentAssignments_Id_IncidentId", x => new { x.Id, x.IncidentId });
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignments_AspNetUsers_AssignedByUserId",
                    column: x => x.AssignedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignments_AspNetUsers_RemovedByUserId",
                    column: x => x.RemovedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignments_Departments_DepartmentId",
                    column: x => x.DepartmentId,
                    principalTable: "Departments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignments_Incidents_IncidentId",
                    column: x => x.IncidentId,
                    principalTable: "Incidents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IncidentDepartmentAssignmentHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AssignmentId = table.Column<int>(type: "int", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ChangedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentDepartmentAssignmentHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignmentHistories_AspNetUsers_ChangedByUserId",
                    column: x => x.ChangedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentAssignmentHistories_IncidentDepartmentAssignments_AssignmentId",
                    column: x => x.AssignmentId,
                    principalTable: "IncidentDepartmentAssignments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IncidentDepartmentActions",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                IncidentId = table.Column<int>(type: "int", nullable: false),
                AssignmentId = table.Column<int>(type: "int", nullable: false),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ActionDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                ActionDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IncidentDepartmentActions", x => x.Id);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentActions_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentActions_IncidentDepartmentAssignments_AssignmentId_IncidentId",
                    columns: x => new { x.AssignmentId, x.IncidentId },
                    principalTable: "IncidentDepartmentAssignments",
                    principalColumns: new[] { "Id", "IncidentId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IncidentDepartmentActions_Incidents_IncidentId",
                    column: x => x.IncidentId,
                    principalTable: "Incidents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentActions_AssignmentId_IncidentId",
            table: "IncidentDepartmentActions",
            columns: new[] { "AssignmentId", "IncidentId" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentActions_IncidentId_ActionDate",
            table: "IncidentDepartmentActions",
            columns: new[] { "IncidentId", "ActionDate" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentActions_UserId",
            table: "IncidentDepartmentActions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignmentHistories_AssignmentId_ChangedAt",
            table: "IncidentDepartmentAssignmentHistories",
            columns: new[] { "AssignmentId", "ChangedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignmentHistories_ChangedByUserId",
            table: "IncidentDepartmentAssignmentHistories",
            column: "ChangedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignments_AssignedByUserId",
            table: "IncidentDepartmentAssignments",
            column: "AssignedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignments_DepartmentId",
            table: "IncidentDepartmentAssignments",
            column: "DepartmentId");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignments_IncidentId_DepartmentId",
            table: "IncidentDepartmentAssignments",
            columns: new[] { "IncidentId", "DepartmentId" },
            unique: true,
            filter: "[RemovedAt] IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignments_IncidentId_Status_RemovedAt",
            table: "IncidentDepartmentAssignments",
            columns: new[] { "IncidentId", "Status", "RemovedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_IncidentDepartmentAssignments_RemovedByUserId",
            table: "IncidentDepartmentAssignments",
            column: "RemovedByUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IncidentDepartmentActions");
        migrationBuilder.DropTable(name: "IncidentDepartmentAssignmentHistories");
        migrationBuilder.DropTable(name: "IncidentDepartmentAssignments");
    }
}
