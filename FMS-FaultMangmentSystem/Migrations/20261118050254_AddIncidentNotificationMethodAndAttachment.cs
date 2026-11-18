using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentNotificationMethodAndAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotificationMethod",
                table: "Incidents",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [Incidents] SET [NotificationMethod] = 3 WHERE [NotificationMethod] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "NotificationMethod",
                table: "Incidents",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialLetterAttachmentKey",
                table: "Incidents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherNotificationMethodDescription",
                table: "Incidents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationMethod",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OfficialLetterAttachmentKey",
                table: "Incidents");

            migrationBuilder.DropColumn(
                name: "OtherNotificationMethodDescription",
                table: "Incidents");
        }
    }
}
