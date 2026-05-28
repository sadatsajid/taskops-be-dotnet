using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Api.Shared.Api;
using TaskOps.Application.Modules.Issues;
using TaskOps.Application.SharedKernel.Api;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class IssueCollaborationEndpointsTests(TaskOpsApiFactory factory)
    : IntegrationTestBase(factory),
        IClassFixture<TaskOpsApiFactory>
{
    private readonly TaskOpsApiFactory _factory = factory;

    [Fact]
    public async Task IssueMember_CanCreateListEditOwnAndManagerCanDeleteComment()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"comment-owner-{Guid.NewGuid():N}@example.com", "Comment Owner");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Comment Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);
        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id);

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"comment-developer-{Guid.NewGuid():N}@example.com", "Comment Developer");
        var developerMember = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");
        developerClient.Authorize(developer);

        var createResponse = await developerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments",
            new CreateIssueCommentRequest("  First implementation note.  "));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueCommentResponse>>();
        created!.Data.Body.Should().Be("First implementation note.");
        created.Data.Author.MemberId.Should().Be(developerMember.Id);

        var listResponse = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueCommentResponse>>>(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments");
        listResponse!.Data.Items.Should().ContainSingle(comment => comment.Id == created.Data.Id);

        var updateResponse = await developerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments/{created.Data.Id}",
            new UpdateIssueCommentRequest("Updated implementation note."));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueCommentResponse>>();
        updated!.Data.Body.Should().Be("Updated implementation note.");

        var deleteResponse = await ownerClient.DeleteAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments/{created.Data.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var commentsAfterDelete = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueCommentResponse>>>(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments");
        commentsAfterDelete!.Data.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task OtherMember_CannotEditOrDeleteAnotherMembersComment()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"comment-permission-owner-{Guid.NewGuid():N}@example.com", "Comment Owner");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Comment Permission Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);
        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id);

        var authorClient = _factory.CreateClient();
        var author = await authorClient.RegisterAsync($"comment-author-{Guid.NewGuid():N}@example.com", "Comment Author");
        await ownerClient.AddMemberAsync(organization.Id, author.CurrentUser.Email, "Developer");
        authorClient.Authorize(author);

        var otherClient = _factory.CreateClient();
        var other = await otherClient.RegisterAsync($"comment-other-{Guid.NewGuid():N}@example.com", "Comment Other");
        await ownerClient.AddMemberAsync(organization.Id, other.CurrentUser.Email, "Developer");
        otherClient.Authorize(other);

        var createResponse = await authorClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments",
            new CreateIssueCommentRequest("Owned by the author."));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueCommentResponse>>();

        var updateResponse = await otherClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments/{created!.Data.Id}",
            new UpdateIssueCommentRequest("Hijacked."));
        var deleteResponse = await otherClient.DeleteAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments/{created.Data.Id}");

        updateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IssueActivity_RecordsIssueChangesAndCommentEvents()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"activity-owner-{Guid.NewGuid():N}@example.com", "Activity Owner");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Activity Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"activity-developer-{Guid.NewGuid():N}@example.com", "Activity Developer");
        var developerMember = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");

        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id);
        await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/assignment",
            new AssignIssueRequest(developerMember.Id));
        await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/status",
            new ChangeIssueStatusRequest("InProgress"));
        await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/priority",
            new ChangeIssuePriorityRequest("High"));
        var commentResponse = await ownerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/comments",
            new CreateIssueCommentRequest("Activity should mention this."));
        var comment = await commentResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueCommentResponse>>();

        var activityResponse = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueActivityResponse>>>(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/activity");

        activityResponse!.Data.Items.Select(activity => activity.Type)
            .Should()
            .Contain([
                "IssueCreated",
                "AssigneeChanged",
                "StatusChanged",
                "PriorityChanged",
                "CommentAdded"
            ]);

        activityResponse.Data.Items.Should().Contain(activity =>
            activity.Type == "StatusChanged" &&
            activity.Field == "status" &&
            activity.OldValue == "Todo" &&
            activity.NewValue == "InProgress");
        activityResponse.Data.Items.Should().Contain(activity =>
            activity.Type == "CommentAdded" &&
            activity.CommentId == comment!.Data.Id);
    }

    [Fact]
    public async Task IssueCollaboration_ForIssueOutsideOrganization_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        await client.RegisterAndAuthorizeAsync($"comment-cross-org-owner-{Guid.NewGuid():N}@example.com", "Comment Owner");
        var firstOrganization = await client.CreateOrganizationAsync("First Comment Organization");
        var project = await client.CreateProjectAsync(firstOrganization.Id);
        var issue = await client.CreateIssueAsync(firstOrganization.Id, project.Id);
        var secondOrganization = await client.CreateOrganizationAsync("Second Comment Organization");

        var commentsResponse = await client.GetAsync(
            $"/api/organizations/{secondOrganization.Id}/issues/{issue.Id}/comments");
        var activityResponse = await client.GetAsync(
            $"/api/organizations/{secondOrganization.Id}/issues/{issue.Id}/activity");

        commentsResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        activityResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
