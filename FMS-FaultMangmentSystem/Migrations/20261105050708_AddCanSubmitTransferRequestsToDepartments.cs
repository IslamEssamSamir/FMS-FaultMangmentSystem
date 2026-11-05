using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddCanSubmitTransferRequestsToDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanSubmitTransferRequests",
                table: "Departments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanSubmitTransferRequests",
                table: "Departments");
        }
    }
}
