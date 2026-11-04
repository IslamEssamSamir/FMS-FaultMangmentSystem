using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class Prompt10IncidentResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "Incidents",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ResolvedByUserId",
                table: "Incidents",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_AspNetUsers_ResolvedByUserId",
                table: "Incidents",
                column: "ResolvedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_AspNetUsers_ResolvedByUserId",
                table: "Incidents");

            migrationBuilder.DropIndex(
                name: "IX_Incidents_ResolvedByUserId",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "Incidents");
        }
    }
}
