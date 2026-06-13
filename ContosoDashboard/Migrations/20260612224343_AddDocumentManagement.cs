using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ContosoDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Department = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    JobTitle = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfilePhotoUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AvailabilityStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    EmailNotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InAppNotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    AnnouncementId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.AnnouncementId);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ProjectManagerId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TargetCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                    table.ForeignKey(
                        name: "FK_Projects_Users_ProjectManagerId",
                        column: x => x.ProjectManagerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    StoredPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Documents_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    ProjectMemberId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => x.ProjectMemberId);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AssignedUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.TaskId);
                    table.ForeignKey(
                        name: "FK_Tasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId");
                    table.ForeignKey(
                        name: "FK_Tasks_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tasks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAuditLogs_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentAuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    RecipientTeamId = table.Column<int>(type: "INTEGER", nullable: true),
                    GrantedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DocumentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTags_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                columns: table => new
                {
                    CommentId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CommentText = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.CommentId);
                    table.ForeignKey(
                        name: "FK_TaskComments_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "AvailabilityStatus", "CreatedDate", "Department", "DisplayName", "Email", "EmailNotificationsEnabled", "InAppNotificationsEnabled", "JobTitle", "LastLoginDate", "PhoneNumber", "ProfilePhotoUrl", "Role" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2026, 6, 12, 22, 43, 41, 522, DateTimeKind.Utc).AddTicks(792), "IT", "System Administrator", "admin@contoso.com", true, true, "Administrator", null, null, null, 3 },
                    { 2, 0, new DateTime(2026, 6, 12, 22, 43, 41, 522, DateTimeKind.Utc).AddTicks(2019), "Engineering", "Camille Nicole", "camille.nicole@contoso.com", true, true, "Project Manager", null, null, null, 2 },
                    { 3, 0, new DateTime(2026, 6, 12, 22, 43, 41, 522, DateTimeKind.Utc).AddTicks(2024), "Engineering", "Floris Kregel", "floris.kregel@contoso.com", true, true, "Team Lead", null, null, null, 1 },
                    { 4, 0, new DateTime(2026, 6, 12, 22, 43, 41, 522, DateTimeKind.Utc).AddTicks(2028), "Engineering", "Ni Kang", "ni.kang@contoso.com", true, true, "Software Engineer", null, null, null, 0 }
                });

            migrationBuilder.InsertData(
                table: "Announcements",
                columns: new[] { "AnnouncementId", "Content", "CreatedByUserId", "ExpiryDate", "IsActive", "PublishDate", "Title" },
                values: new object[] { 1, "Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.", 1, new DateTime(2026, 7, 12, 22, 43, 41, 525, DateTimeKind.Utc).AddTicks(2627), true, new DateTime(2026, 6, 12, 22, 43, 41, 525, DateTimeKind.Utc).AddTicks(2150), "Welcome to ContosoDashboard" });

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "ProjectId", "CreatedDate", "Description", "Name", "ProjectManagerId", "StartDate", "Status", "TargetCompletionDate", "UpdatedDate" },
                values: new object[] { 1, new DateTime(2026, 5, 13, 22, 43, 41, 523, DateTimeKind.Utc).AddTicks(7758), "Internal employee productivity dashboard", "ContosoDashboard Development", 2, new DateTime(2026, 5, 13, 22, 43, 41, 523, DateTimeKind.Utc).AddTicks(6354), 1, new DateTime(2026, 8, 11, 22, 43, 41, 523, DateTimeKind.Utc).AddTicks(6829), new DateTime(2026, 6, 12, 22, 43, 41, 523, DateTimeKind.Utc).AddTicks(8196) });

            migrationBuilder.InsertData(
                table: "ProjectMembers",
                columns: new[] { "ProjectMemberId", "AssignedDate", "ProjectId", "Role", "UserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 5, 13, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(8417), 1, "TeamLead", 3 },
                    { 2, new DateTime(2026, 5, 13, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(8862), 1, "Developer", 4 }
                });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "TaskId", "AssignedUserId", "CreatedByUserId", "CreatedDate", "Description", "DueDate", "Priority", "ProjectId", "Status", "Title", "UpdatedDate" },
                values: new object[,]
                {
                    { 1, 4, 2, new DateTime(2026, 5, 13, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(4224), "Create entity relationship diagram and database design", new DateTime(2026, 5, 23, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(2311), 2, 1, 2, "Design database schema", new DateTime(2026, 5, 23, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(4736) },
                    { 2, 4, 2, new DateTime(2026, 5, 18, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5148), "Set up Microsoft Entra ID authentication", new DateTime(2026, 6, 17, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5144), 3, 1, 1, "Implement authentication", new DateTime(2026, 6, 12, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5149) },
                    { 3, 4, 2, new DateTime(2026, 5, 23, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5154), "Design user interface mockups for all main pages", new DateTime(2026, 6, 22, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5152), 1, 1, 0, "Create UI mockups", new DateTime(2026, 5, 23, 22, 43, 41, 524, DateTimeKind.Utc).AddTicks(5155) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatedByUserId",
                table: "Announcements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_ActorUserId_OccurredAtUtc",
                table: "DocumentAuditLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAuditLogs_DocumentId_OccurredAtUtc",
                table: "DocumentAuditLogs",
                columns: new[] { "DocumentId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Category",
                table: "Documents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ProjectId_UploadedAtUtc",
                table: "Documents",
                columns: new[] { "ProjectId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByUserId_UploadedAtUtc",
                table: "Documents",
                columns: new[] { "UploadedByUserId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId_RecipientUserId",
                table: "DocumentShares",
                columns: new[] { "DocumentId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_GrantedByUserId",
                table: "DocumentShares",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_RecipientUserId",
                table: "DocumentShares",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTags_DocumentId_Value",
                table: "DocumentTags",
                columns: new[] { "DocumentId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId",
                table: "ProjectMembers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectManagerId",
                table: "Projects",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId",
                table: "TaskComments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_UserId",
                table: "TaskComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedUserId",
                table: "Tasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedByUserId",
                table: "Tasks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_DueDate",
                table: "Tasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Status",
                table: "Tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "DocumentAuditLogs");

            migrationBuilder.DropTable(
                name: "DocumentShares");

            migrationBuilder.DropTable(
                name: "DocumentTags");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "TaskComments");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
