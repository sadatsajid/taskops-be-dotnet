using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskOps.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueTenantConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Projects_ProjectId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ProjectId",
                table: "Issues");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Projects_Id_OrganizationId",
                table: "Projects",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_OrganizationMembers_Id_OrganizationId",
                table: "OrganizationMembers",
                columns: new[] { "Id", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_AssigneeId_OrganizationId",
                table: "Issues",
                columns: new[] { "AssigneeId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId_OrganizationId",
                table: "Issues",
                columns: new[] { "ProjectId", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_OrganizationMembers_AssigneeId_OrganizationId",
                table: "Issues",
                columns: new[] { "AssigneeId", "OrganizationId" },
                principalTable: "OrganizationMembers",
                principalColumns: new[] { "Id", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Projects_ProjectId_OrganizationId",
                table: "Issues",
                columns: new[] { "ProjectId", "OrganizationId" },
                principalTable: "Projects",
                principalColumns: new[] { "Id", "OrganizationId" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_OrganizationMembers_AssigneeId_OrganizationId",
                table: "Issues");

            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Projects_ProjectId_OrganizationId",
                table: "Issues");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Projects_Id_OrganizationId",
                table: "Projects");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_OrganizationMembers_Id_OrganizationId",
                table: "OrganizationMembers");

            migrationBuilder.DropIndex(
                name: "IX_Issues_AssigneeId_OrganizationId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_ProjectId_OrganizationId",
                table: "Issues");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_ProjectId",
                table: "Issues",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Projects_ProjectId",
                table: "Issues",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
