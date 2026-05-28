using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueCommentsAndActivityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Issues_Id_OrganizationId",
                table: "Issues",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "IssueComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueComments", x => x.Id);
                    table.UniqueConstraint("AK_IssueComments_Id_OrganizationId", x => new { x.Id, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_IssueComments_Issues_IssueId_OrganizationId",
                        columns: x => new { x.IssueId, x.OrganizationId },
                        principalTable: "Issues",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueComments_OrganizationMembers_AuthorMemberId_Organizati~",
                        columns: x => new { x.AuthorMemberId, x.OrganizationId },
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "Id", "OrganizationId" });
                    table.ForeignKey(
                        name: "FK_IssueComments_OrganizationMembers_DeletedByMemberId_Organiz~",
                        columns: x => new { x.DeletedByMemberId, x.OrganizationId },
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "Id", "OrganizationId" });
                });

            migrationBuilder.CreateTable(
                name: "IssueActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Field = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OldValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssueActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IssueActivities_IssueComments_CommentId_OrganizationId",
                        columns: x => new { x.CommentId, x.OrganizationId },
                        principalTable: "IssueComments",
                        principalColumns: new[] { "Id", "OrganizationId" });
                    table.ForeignKey(
                        name: "FK_IssueActivities_Issues_IssueId_OrganizationId",
                        columns: x => new { x.IssueId, x.OrganizationId },
                        principalTable: "Issues",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IssueActivities_OrganizationMembers_ActorMemberId_Organizat~",
                        columns: x => new { x.ActorMemberId, x.OrganizationId },
                        principalTable: "OrganizationMembers",
                        principalColumns: new[] { "Id", "OrganizationId" });
                });

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_ActorMemberId",
                table: "IssueActivities",
                column: "ActorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_ActorMemberId_OrganizationId",
                table: "IssueActivities",
                columns: new[] { "ActorMemberId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_CommentId_OrganizationId",
                table: "IssueActivities",
                columns: new[] { "CommentId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_IssueId_OrganizationId",
                table: "IssueActivities",
                columns: new[] { "IssueId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueActivities_OrganizationId_IssueId_CreatedAt",
                table: "IssueActivities",
                columns: new[] { "OrganizationId", "IssueId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_AuthorMemberId",
                table: "IssueComments",
                column: "AuthorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_AuthorMemberId_OrganizationId",
                table: "IssueComments",
                columns: new[] { "AuthorMemberId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_DeletedByMemberId_OrganizationId",
                table: "IssueComments",
                columns: new[] { "DeletedByMemberId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_IssueId_OrganizationId",
                table: "IssueComments",
                columns: new[] { "IssueId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_IssueComments_OrganizationId_IssueId_CreatedAt",
                table: "IssueComments",
                columns: new[] { "OrganizationId", "IssueId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IssueActivities");

            migrationBuilder.DropTable(
                name: "IssueComments");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Issues_Id_OrganizationId",
                table: "Issues");
        }
    }
}
