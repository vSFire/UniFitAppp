using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFitApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "Enrollments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "Enrollments");
        }
    }
}
