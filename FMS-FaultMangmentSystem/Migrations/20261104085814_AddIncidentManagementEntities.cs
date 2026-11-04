using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentManagementEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime2(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsIncidentReceivingDepartment = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    IsOvernight = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ShiftDefinitions_TimeRange", "([IsOvernight] = 1 AND [EndTime] <= [StartTime]) OR ([IsOvernight] = 0 AND [StartTime] < [EndTime])");
                });

            migrationBuilder.CreateTable(
                name: "WeeklyShiftSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeekStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyShiftSchedules", x => x.Id);
                    table.CheckConstraint("CK_WeeklyShiftSchedules_WeekStartDate_Saturday", "DATEDIFF(day, CONVERT(date, '19000106', 112), [WeekStartDate]) % 7 = 0");
                    table.ForeignKey(
                        name: "FK_WeeklyShiftSchedules_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Incoming"),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReportingDepartmentId = table.Column<int>(type: "int", nullable: false),
                    InitialReceivingDepartmentId = table.Column<int>(type: "int", nullable: false),
                    CurrentDepartmentId = table.Column<int>(type: "int", nullable: false),
                    CurrentResponsibleUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_AspNetUsers_CurrentResponsibleUserId",
                        column: x => x.CurrentResponsibleUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Incidents_AspNetUsers_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Departments_CurrentDepartmentId",
                        column: x => x.CurrentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Departments_InitialReceivingDepartmentId",
                        column: x => x.InitialReceivingDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Incidents_Departments_ReportingDepartmentId",
                        column: x => x.ReportingDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WeeklyShiftScheduleId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ShiftDefinitionId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ShiftDefinitions_ShiftDefinitionId",
                        column: x => x.ShiftDefinitionId,
                        principalTable: "ShiftDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_WeeklyShiftSchedules_WeeklyShiftScheduleId",
                        column: x => x.WeeklyShiftScheduleId,
                        principalTable: "WeeklyShiftSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActionDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentActions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentActions_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    SupervisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReviewDecision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentReviews_AspNetUsers_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentReviews_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChangeDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentStatusHistories_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentStatusHistories_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RequestStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ReviewedBySupervisorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewDate = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTransferRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTransferRequests_AspNetUsers_ReviewedBySupervisorId",
                        column: x => x.ReviewedBySupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IncidentTransferRequests_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncidentTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IncidentId = table.Column<int>(type: "int", nullable: false),
                    FromDepartmentId = table.Column<int>(type: "int", nullable: false),
                    ToDepartmentId = table.Column<int>(type: "int", nullable: false),
                    TransferredBySupervisorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransferReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TransferDate = table.Column<DateTime>(type: "datetime2(7)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentTransfers_AspNetUsers_TransferredBySupervisorId",
                        column: x => x.TransferredBySupervisorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTransfers_Departments_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTransfers_Departments_ToDepartmentId",
                        column: x => x.ToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncidentTransfers_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DepartmentId",
                table: "AspNetUsers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsActive_IsIncidentReceivingDepartment",
                table: "Departments",
                columns: new[] { "IsActive", "IsIncidentReceivingDepartment" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActions_IncidentId_ActionDate",
                table: "IncidentActions",
                columns: new[] { "IncidentId", "ActionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActions_UserId",
                table: "IncidentActions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReviews_IncidentId_ReviewDate",
                table: "IncidentReviews",
                columns: new[] { "IncidentId", "ReviewDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReviews_SupervisorId",
                table: "IncidentReviews",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CreatedAt",
                table: "Incidents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CurrentDepartmentId_Status",
                table: "Incidents",
                columns: new[] { "CurrentDepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_CurrentResponsibleUserId",
                table: "Incidents",
                column: "CurrentResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_IncidentNumber",
                table: "Incidents",
                column: "IncidentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_InitialReceivingDepartmentId",
                table: "Incidents",
                column: "InitialReceivingDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ReportedByUserId",
                table: "Incidents",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ReportingDepartmentId",
                table: "Incidents",
                column: "ReportingDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentStatusHistories_ChangedByUserId",
                table: "IncidentStatusHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentStatusHistories_IncidentId_ChangeDate",
                table: "IncidentStatusHistories",
                columns: new[] { "IncidentId", "ChangeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_IncidentId_RequestDate",
                table: "IncidentTransferRequests",
                columns: new[] { "IncidentId", "RequestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_RequestedByUserId",
                table: "IncidentTransferRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_RequestStatus_RequestDate",
                table: "IncidentTransferRequests",
                columns: new[] { "RequestStatus", "RequestDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransferRequests_ReviewedBySupervisorId",
                table: "IncidentTransferRequests",
                column: "ReviewedBySupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransfers_FromDepartmentId_ToDepartmentId",
                table: "IncidentTransfers",
                columns: new[] { "FromDepartmentId", "ToDepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransfers_IncidentId_TransferDate",
                table: "IncidentTransfers",
                columns: new[] { "IncidentId", "TransferDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransfers_ToDepartmentId",
                table: "IncidentTransfers",
                column: "ToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentTransfers_TransferredBySupervisorId",
                table: "IncidentTransfers",
                column: "TransferredBySupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_DepartmentId_ShiftDate",
                table: "ShiftAssignments",
                columns: new[] { "DepartmentId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftDefinitionId",
                table: "ShiftAssignments",
                column: "ShiftDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_UserId",
                table: "ShiftAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_WeeklyShiftScheduleId_UserId_ShiftDate",
                table: "ShiftAssignments",
                columns: new[] { "WeeklyShiftScheduleId", "UserId", "ShiftDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDefinitions_Name",
                table: "ShiftDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyShiftSchedules_CreatedByUserId",
                table: "WeeklyShiftSchedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyShiftSchedules_WeekStartDate",
                table: "WeeklyShiftSchedules",
                column: "WeekStartDate",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Departments_DepartmentId",
                table: "AspNetUsers",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Departments_DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "IncidentActions");

            migrationBuilder.DropTable(
                name: "IncidentReviews");

            migrationBuilder.DropTable(
                name: "IncidentStatusHistories");

            migrationBuilder.DropTable(
                name: "IncidentTransferRequests");

            migrationBuilder.DropTable(
                name: "IncidentTransfers");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "ShiftDefinitions");

            migrationBuilder.DropTable(
                name: "WeeklyShiftSchedules");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");
        }
    }
}
