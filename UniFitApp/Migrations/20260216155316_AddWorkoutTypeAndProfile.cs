using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFitApp.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutTypeAndProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Workouts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Workouts");
        }
    }
}
