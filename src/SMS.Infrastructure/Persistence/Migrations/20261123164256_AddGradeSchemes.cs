using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Programmes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GradeSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    OverallPassMark = table.Column<double>(type: "float", nullable: false),
                    CourseworkPassMark = table.Column<double>(type: "float", nullable: false),
                    EosaPassMark = table.Column<double>(type: "float", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeSchemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GradeBands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GradeSchemeId = table.Column<int>(type: "int", nullable: false),
                    Letter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinPercent = table.Column<double>(type: "float", nullable: true),
                    MaxPercent = table.Column<double>(type: "float", nullable: true),
                    QualityPoints = table.Column<double>(type: "float", nullable: false),
                    IsPass = table.Column<bool>(type: "bit", nullable: false),
                    CountsInGpa = table.Column<bool>(type: "bit", nullable: false),
                    IsSpecial = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradeBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradeBands_GradeSchemes_GradeSchemeId",
                        column: x => x.GradeSchemeId,
                        principalTable: "GradeSchemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradeBands_GradeSchemeId",
                table: "GradeBands",
                column: "GradeSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_GradeSchemes_Level_EffectiveFrom",
                table: "GradeSchemes",
                columns: new[] { "Level", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradeBands");

            migrationBuilder.DropTable(
                name: "GradeSchemes");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Programmes");
        }
    }
}
