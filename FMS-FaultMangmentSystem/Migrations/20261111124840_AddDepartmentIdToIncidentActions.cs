using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentIdToIncidentActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "IncidentActions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [action] " +
                "SET [DepartmentId] = COALESCE([user].[DepartmentId], [incident].[CurrentDepartmentId]) " +
                "FROM [IncidentActions] AS [action] " +
                "INNER JOIN [Incidents] AS [incident] ON [action].[IncidentId] = [incident].[Id] " +
                "LEFT JOIN [AspNetUsers] AS [user] ON [action].[UserId] = [user].[Id]");

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "IncidentActions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActions_DepartmentId",
                table: "IncidentActions",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentActions_Departments_DepartmentId",
                table: "IncidentActions",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IncidentActions_Departments_DepartmentId",
                table: "IncidentActions");

            migrationBuilder.DropIndex(
                name: "IX_IncidentActions_DepartmentId",
                table: "IncidentActions");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "IncidentActions");
        }
    }
}
