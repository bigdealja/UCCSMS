using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoodleMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MoodleUserId",
                table: "Students",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MoodleCourseId",
                table: "Courses",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoodleUserId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "MoodleCourseId",
                table: "Courses");
        }
    }
}
