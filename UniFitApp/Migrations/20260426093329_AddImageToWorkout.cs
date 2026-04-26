using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniFitApp.Migrations
{
    /// <inheritdoc />
    public partial class AddImageToWorkout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location",
                table: "Workouts");

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Workouts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Workouts");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Workouts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
