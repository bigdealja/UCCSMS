using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SMS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScriptCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScriptBundles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExamId = table.Column<int>(type: "int", nullable: false),
                    ScriptCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Custodian = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetentionUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    DisposedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DisposalWitness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScriptBundles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScriptBundles_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustodyEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptBundleId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromCustodian = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToCustodian = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    By = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustodyEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustodyEvents_ScriptBundles_ScriptBundleId",
                        column: x => x.ScriptBundleId,
                        principalTable: "ScriptBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarkedScriptUploads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptBundleId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkedScriptUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkedScriptUploads_ScriptBundles_ScriptBundleId",
                        column: x => x.ScriptBundleId,
                        principalTable: "ScriptBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScanBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScriptBundleId = table.Column<int>(type: "int", nullable: false),
                    ScannedCount = table.Column<int>(type: "int", nullable: false),
                    CollectionRef = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScannedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanBatches_ScriptBundles_ScriptBundleId",
                        column: x => x.ScriptBundleId,
                        principalTable: "ScriptBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustodyEvents_ScriptBundleId",
                table: "CustodyEvents",
                column: "ScriptBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkedScriptUploads_ScriptBundleId",
                table: "MarkedScriptUploads",
                column: "ScriptBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanBatches_ScriptBundleId",
                table: "ScanBatches",
                column: "ScriptBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScriptBundles_ExamId",
                table: "ScriptBundles",
                column: "ExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustodyEvents");

            migrationBuilder.DropTable(
                name: "MarkedScriptUploads");

            migrationBuilder.DropTable(
                name: "ScanBatches");

            migrationBuilder.DropTable(
                name: "ScriptBundles");
        }
    }
}
