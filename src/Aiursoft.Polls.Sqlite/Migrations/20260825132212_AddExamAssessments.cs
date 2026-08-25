using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aiursoft.Polls.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAssessments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "Submissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "FullScore",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxScore",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverSelectionScore",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartialScore",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Passed",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PassingScore",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Score",
                table: "Submissions",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Submissions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "Submissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Explanation",
                table: "Questions",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRepeatedSubmissions",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailMessage",
                table: "Polls",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FullScore",
                table: "Polls",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OverSelectionScore",
                table: "Polls",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PartialScore",
                table: "Polls",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PassMessage",
                table: "Polls",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PassingScore",
                table: "Polls",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "QuestionsPerAttempt",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleOptions",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleQuestions",
                table: "Polls",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCorrect",
                table: "Options",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE `Polls` SET
                    `AllowRepeatedSubmissions` = 1,
                    `DurationMinutes` = 60,
                    `FullScore` = 4,
                    `PartialScore` = 2,
                    `PassingScore` = 90,
                    `ShuffleQuestions` = 1,
                    `ShuffleOptions` = 1,
                    `PassMessage` = 'You passed the exam.',
                    `FailMessage` = 'Unfortunately, you did not pass the exam.';
                UPDATE `Submissions` SET
                    `AttemptNumber` = 1,
                    `Status` = 1,
                    `StartedAt` = `SubmitTime`,
                    `ExpiresAt` = `SubmitTime`,
                    `SubmittedAt` = `SubmitTime`;
                """);

            migrationBuilder.CreateTable(
                name: "AttemptQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptQuestions_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PollId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AssignedRoleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollAssignments", x => x.Id);
                    table.CheckConstraint("CK_PollAssignments_ExactlyOneRecipient", "(AssignedUserId IS NOT NULL AND AssignedRoleId IS NULL) OR (AssignedUserId IS NULL AND AssignedRoleId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PollAssignments_AspNetRoles_AssignedRoleId",
                        column: x => x.AssignedRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollAssignments_AspNetUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollAssignments_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PollShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PollId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SharedWithUserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SharedWithRoleId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Permission = table.Column<int>(type: "INTEGER", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PollShares", x => x.Id);
                    table.CheckConstraint("CK_PollShares_ExactlyOneRecipient", "(SharedWithUserId IS NOT NULL AND SharedWithRoleId IS NULL) OR (SharedWithUserId IS NULL AND SharedWithRoleId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PollShares_AspNetRoles_SharedWithRoleId",
                        column: x => x.SharedWithRoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollShares_AspNetUsers_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PollShares_Polls_PollId",
                        column: x => x.PollId,
                        principalTable: "Polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttemptOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttemptQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceOptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptOptions_AttemptQuestions_AttemptQuestionId",
                        column: x => x.AttemptQuestionId,
                        principalTable: "AttemptQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttemptSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SubmissionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptQuestionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptOptionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttemptSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttemptSelections_AttemptOptions_AttemptOptionId",
                        column: x => x.AttemptOptionId,
                        principalTable: "AttemptOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptSelections_AttemptQuestions_AttemptQuestionId",
                        column: x => x.AttemptQuestionId,
                        principalTable: "AttemptQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttemptSelections_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttemptOptions_AttemptQuestionId",
                table: "AttemptOptions",
                column: "AttemptQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptQuestions_SubmissionId",
                table: "AttemptQuestions",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptSelections_AttemptOptionId",
                table: "AttemptSelections",
                column: "AttemptOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptSelections_AttemptQuestionId",
                table: "AttemptSelections",
                column: "AttemptQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptSelections_SubmissionId_AttemptQuestionId_AttemptOptionId",
                table: "AttemptSelections",
                columns: new[] { "SubmissionId", "AttemptQuestionId", "AttemptOptionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollAssignments_AssignedRoleId",
                table: "PollAssignments",
                column: "AssignedRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PollAssignments_AssignedUserId",
                table: "PollAssignments",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PollAssignments_PollId_AssignedRoleId",
                table: "PollAssignments",
                columns: new[] { "PollId", "AssignedRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollAssignments_PollId_AssignedUserId",
                table: "PollAssignments",
                columns: new[] { "PollId", "AssignedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_PollId_SharedWithRoleId",
                table: "PollShares",
                columns: new[] { "PollId", "SharedWithRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_PollId_SharedWithUserId",
                table: "PollShares",
                columns: new[] { "PollId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_SharedWithRoleId",
                table: "PollShares",
                column: "SharedWithRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_PollShares_SharedWithUserId",
                table: "PollShares",
                column: "SharedWithUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttemptSelections");

            migrationBuilder.DropTable(
                name: "PollAssignments");

            migrationBuilder.DropTable(
                name: "PollShares");

            migrationBuilder.DropTable(
                name: "AttemptOptions");

            migrationBuilder.DropTable(
                name: "AttemptQuestions");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "FullScore",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "MaxScore",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "OverSelectionScore",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PartialScore",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Passed",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Score",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "Explanation",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "AllowRepeatedSubmissions",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "FailMessage",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "FullScore",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "OverSelectionScore",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "PartialScore",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "PassMessage",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "PassingScore",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "QuestionsPerAttempt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ShuffleOptions",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ShuffleQuestions",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "IsCorrect",
                table: "Options");
        }
    }
}
