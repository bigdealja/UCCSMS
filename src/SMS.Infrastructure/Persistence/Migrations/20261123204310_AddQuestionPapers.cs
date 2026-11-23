using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionPapers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionPapers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    TermId = table.Column<int>(type: "int", nullable: false),
                    SubmissionDeadline = table.Column<DateOnly>(type: "date", nullable: false),
                    AuthorLecturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalReviewRequired = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    ReturnedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawnReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionPapers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionPapers_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionPapers_Terms_TermId",
                        column: x => x.TermId,
                        principalTable: "Terms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrintRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionPaperId = table.Column<int>(type: "int", nullable: false),
                    Copies = table.Column<int>(type: "int", nullable: false),
                    SpareCopies = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecureStorageRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DispatchedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReturnedUnused = table.Column<int>(type: "int", nullable: false),
                    DestroyedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DestructionWitness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrintRuns_QuestionPapers_QuestionPaperId",
                        column: x => x.QuestionPaperId,
                        principalTable: "QuestionPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionPaperVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionPaperId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    SubmittedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionPaperVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionPaperVersions_QuestionPapers_QuestionPaperId",
                        column: x => x.QuestionPaperId,
                        principalTable: "QuestionPapers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintRuns_QuestionPaperId",
                table: "PrintRuns",
                column: "QuestionPaperId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPapers_CourseId",
                table: "QuestionPapers",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPapers_TermId",
                table: "QuestionPapers",
                column: "TermId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionPaperVersions_QuestionPaperId",
                table: "QuestionPaperVersions",
                column: "QuestionPaperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintRuns");

            migrationBuilder.DropTable(
                name: "QuestionPaperVersions");

            migrationBuilder.DropTable(
                name: "QuestionPapers");
        }
    }
}
