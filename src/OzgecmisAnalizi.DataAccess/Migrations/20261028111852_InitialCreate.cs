using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OzgecmisAnalizi.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Professions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SearchKeywords = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JuniorSalaryFactor = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    SeniorSalaryFactor = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    CareerTitles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Importance = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LearningHint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessionSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessionSkills_Professions_ProfessionId",
                        column: x => x.ProfessionId,
                        principalTable: "Professions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetProfessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    RawText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resumes_Professions_TargetProfessionId",
                        column: x => x.TargetProfessionId,
                        principalTable: "Professions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Resumes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoverLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoverLetters_Resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "Resumes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MatchScore = table.Column<int>(type: "int", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InterviewAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "Resumes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JobApplications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResumeAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OverallScore = table.Column<int>(type: "int", nullable: false),
                    ComprehensivenessScore = table.Column<int>(type: "int", nullable: false),
                    StructureScore = table.Column<int>(type: "int", nullable: false),
                    ClarityScore = table.Column<int>(type: "int", nullable: false),
                    AtsScore = table.Column<int>(type: "int", nullable: false),
                    SkillMatchScore = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TopPercent = table.Column<int>(type: "int", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumeAnalyses", x => x.Id);
                    table.CheckConstraint("CK_ResumeAnalyses_Overall", "[OverallScore] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_ResumeAnalyses_Professions_ProfessionId",
                        column: x => x.ProfessionId,
                        principalTable: "Professions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResumeAnalyses_Resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "Resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoverLetters_ResumeId",
                table: "CoverLetters",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_CoverLetters_UserId_CreatedAt",
                table: "CoverLetters",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ResumeId",
                table: "JobApplications",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_UserId_Stage",
                table: "JobApplications",
                columns: new[] { "UserId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Professions_Category",
                table: "Professions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Professions_Code",
                table: "Professions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessionSkills_ProfessionId",
                table: "ProfessionSkills",
                column: "ProfessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_ProfessionId",
                table: "ResumeAnalyses",
                column: "ProfessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_ResumeId",
                table: "ResumeAnalyses",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumeAnalyses_UserId_CreatedAt",
                table: "ResumeAnalyses",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_TargetProfessionId",
                table: "Resumes",
                column: "TargetProfessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Resumes_UserId_CreatedAt",
                table: "Resumes",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoverLetters");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "ProfessionSkills");

            migrationBuilder.DropTable(
                name: "ResumeAnalyses");

            migrationBuilder.DropTable(
                name: "Resumes");

            migrationBuilder.DropTable(
                name: "Professions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
