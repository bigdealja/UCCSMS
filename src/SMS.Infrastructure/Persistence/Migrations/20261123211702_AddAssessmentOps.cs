using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SittingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScriptsCollectedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MarkedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GradeSheetReturnedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EnteredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentToDeanUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VettedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExceptionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LateReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SittingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SittingRecords_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SittingRecords_ExamId",
                table: "SittingRecords",
                column: "ExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SittingRecords");
        }
    }
}
