using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Api.Features.Issues;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class IssueEndpointsTests(TaskOpsApiFactory factory) : IClassFixture<TaskOpsApiFactory>
{
    private readonly TaskOpsApiFactory _factory = factory;

    [Fact]
    public async Task CreateIssue_ForProjectManager_CreatesProjectScopedIssue()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id, "Backend Platform", "API");

        var projectManagerClient = _factory.CreateClient();
        var projectManager = await projectManagerClient.RegisterAsync($"issue-manager-{Guid.NewGuid():N}@example.com", "Issue Test");
        await ownerClient.AddMemberAsync(organization.Id, projectManager.CurrentUser.Email, "ProjectManager");
        projectManagerClient.Authorize(projectManager);

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"issue-developer-{Guid.NewGuid():N}@example.com", "Issue Test");
        var developerMember = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");

        var response = await projectManagerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues",
            new CreateIssueRequest(
                project.Id,
                "Build issue workflow",
                "Create, assign, filter, and paginate issues.",
                "High",
                developerMember.Id,
                new DateOnly(2026, 6, 15)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Data.OrganizationId.Should().Be(organization.Id);
        envelope.Data.ProjectId.Should().Be(project.Id);
        envelope.Data.ProjectKey.Should().Be("API");
        envelope.Data.Number.Should().Be(1);
        envelope.Data.Key.Should().Be("API-1");
        envelope.Data.Title.Should().Be("Build issue workflow");
        envelope.Data.Status.Should().Be("Todo");
        envelope.Data.Priority.Should().Be("High");
        envelope.Data.Assignee!.MemberId.Should().Be(developerMember.Id);
        envelope.Data.DueDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public async Task IssueManager_CanUpdateDetailsAssignmentPriorityAndDueDate()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-update-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);
        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id, title: "Original issue");

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"issue-update-developer-{Guid.NewGuid():N}@example.com", "Issue Test");
        var developerMember = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");

        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}",
            new UpdateIssueRequest("Updated issue", "Detailed acceptance notes."));
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var assignResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/assignment",
            new AssignIssueRequest(developerMember.Id));
        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var priorityResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/priority",
            new ChangeIssuePriorityRequest("Critical"));
        priorityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var dueDateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/due-date",
            new SetIssueDueDateRequest(new DateOnly(2026, 7, 1)));
        dueDateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var envelope = await dueDateResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueResponse>>();
        envelope!.Data.Title.Should().Be("Updated issue");
        envelope.Data.Description.Should().Be("Detailed acceptance notes.");
        envelope.Data.Assignee!.MemberId.Should().Be(developerMember.Id);
        envelope.Data.Priority.Should().Be("Critical");
        envelope.Data.DueDate.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public async Task CreateIssue_WithAssigneeFromAnotherOrganization_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"issue-assignee-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        client.Authorize(owner);
        var firstOrganization = await client.CreateOrganizationAsync("Issue Test Organization");
        var firstProject = await client.CreateProjectAsync(firstOrganization.Id, "First Project", "FIRST");
        var secondOrganization = await client.CreateOrganizationAsync("Issue Test Organization");

        var otherUserClient = _factory.CreateClient();
        var otherUser = await otherUserClient.RegisterAsync($"issue-assignee-other-{Guid.NewGuid():N}@example.com", "Issue Test");
        var otherOrganizationMember = await client.AddMemberAsync(secondOrganization.Id, otherUser.CurrentUser.Email, "Developer");

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{firstOrganization.Id}/issues",
            new CreateIssueRequest(firstProject.Id, "Wrong assignee", null, "Medium", otherOrganizationMember.Id, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetIssue_ForNonMember_ReturnsNotFound()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-private-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);
        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id);

        var outsiderClient = _factory.CreateClient();
        var outsider = await outsiderClient.RegisterAsync($"issue-outsider-{Guid.NewGuid():N}@example.com", "Issue Test");
        outsiderClient.Authorize(outsider);

        var response = await outsiderClient.GetAsync($"/api/organizations/{organization.Id}/issues/{issue.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateIssue_ForViewer_ReturnsForbidden()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-viewer-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);
        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id);

        var viewerClient = _factory.CreateClient();
        var viewer = await viewerClient.RegisterAsync($"issue-viewer-{Guid.NewGuid():N}@example.com", "Issue Test");
        await ownerClient.AddMemberAsync(organization.Id, viewer.CurrentUser.Email, "Viewer");
        viewerClient.Authorize(viewer);

        var response = await viewerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}",
            new UpdateIssueRequest("Viewer edit", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignedDeveloper_CanChangeOwnIssueStatus()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-status-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"issue-status-developer-{Guid.NewGuid():N}@example.com", "Issue Test");
        var developerMember = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");
        developerClient.Authorize(developer);

        var issue = await ownerClient.CreateIssueAsync(
            organization.Id,
            project.Id,
            title: "Assigned status work",
            assigneeId: developerMember.Id);

        var response = await developerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/status",
            new ChangeIssueStatusRequest("InProgress"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueResponse>>();
        envelope!.Data.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task UnassignedDeveloper_CannotChangeIssueStatus()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"issue-unassigned-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Issue Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"issue-unassigned-developer-{Guid.NewGuid():N}@example.com", "Issue Test");
        await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");
        developerClient.Authorize(developer);

        var issue = await ownerClient.CreateIssueAsync(organization.Id, project.Id, title: "Unassigned status work");

        var response = await developerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{issue.Id}/status",
            new ChangeIssueStatusRequest("InProgress"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListIssues_FiltersSearchesSortsAndPaginates()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"issue-list-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        client.Authorize(owner);
        var organization = await client.CreateOrganizationAsync("Issue Test Organization");
        var apiProject = await client.CreateProjectAsync(organization.Id, "API Project", "API");
        var webProject = await client.CreateProjectAsync(organization.Id, "Web Project", "WEB");

        var first = await client.CreateIssueAsync(
            organization.Id,
            apiProject.Id,
            title: "Fix billing webhook",
            description: "Stripe callback fails on retries.",
            priority: "Critical",
            dueDate: new DateOnly(2026, 6, 10));
        var second = await client.CreateIssueAsync(
            organization.Id,
            apiProject.Id,
            title: "Document billing filters",
            priority: "High",
            dueDate: new DateOnly(2026, 6, 20));
        await client.CreateIssueAsync(
            organization.Id,
            webProject.Id,
            title: "Polish dashboard empty state",
            priority: "Low");

        await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{second.Id}/status",
            new ChangeIssueStatusRequest("InProgress"));

        var listResponse = await client.GetAsync(
            $"/api/organizations/{organization.Id}/issues?projectId={apiProject.Id}&search=billing&status=Todo&sort=dueDate&limit=1");
        var body = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var response = await listResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>();

        response.Should().NotBeNull();
        response!.Data.Items.Should().ContainSingle();
        response.Data.Items[0].Id.Should().Be(first.Id);
        response.Data.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task CreateIssue_ConcurrentRequests_AssignSequentialNumbers()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"issue-concurrent-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        client.Authorize(owner);
        var organization = await client.CreateOrganizationAsync("Issue Test Organization");
        var project = await client.CreateProjectAsync(organization.Id, "Concurrent Project", "CONC");

        var createTasks = Enumerable.Range(1, 20)
            .Select(index => client.PostAsJsonAsync(
                $"/api/organizations/{organization.Id}/issues",
                new CreateIssueRequest(project.Id, $"Concurrent issue {index}", null, "Medium", null, null)))
            .ToArray();

        var responses = await Task.WhenAll(createTasks);
        foreach (var response in responses)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Created, body);
        }

        var issues = await Task.WhenAll(responses.Select(async response =>
        {
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueResponse>>();
            return envelope!.Data;
        }));

        issues.Select(issue => issue.Number)
            .Should()
            .BeEquivalentTo(Enumerable.Range(1, 20));
        issues.Select(issue => issue.Number).Should().OnlyHaveUniqueItems();
        issues.Select(issue => issue.Key).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ListIssues_SortsPriorityAndStatusSemantically()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"issue-sort-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        client.Authorize(owner);
        var organization = await client.CreateOrganizationAsync("Issue Test Organization");
        var project = await client.CreateProjectAsync(organization.Id, "Sort Project", "SORT");

        var low = await client.CreateIssueAsync(organization.Id, project.Id, title: "Low priority", priority: "Low");
        var critical = await client.CreateIssueAsync(organization.Id, project.Id, title: "Critical priority", priority: "Critical");
        var high = await client.CreateIssueAsync(organization.Id, project.Id, title: "High priority", priority: "High");
        var medium = await client.CreateIssueAsync(organization.Id, project.Id, title: "Medium priority", priority: "Medium");

        var inProgressResponse = await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{medium.Id}/status",
            new ChangeIssueStatusRequest("InProgress"));
        var inReviewResponse = await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{high.Id}/status",
            new ChangeIssueStatusRequest("InReview"));
        var doneResponse = await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/issues/{critical.Id}/status",
            new ChangeIssueStatusRequest("Done"));
        inProgressResponse.EnsureSuccessStatusCode();
        inReviewResponse.EnsureSuccessStatusCode();
        doneResponse.EnsureSuccessStatusCode();

        var priorityAscending = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>(
            $"/api/organizations/{organization.Id}/issues?projectId={project.Id}&sort=priority");
        var priorityDescending = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>(
            $"/api/organizations/{organization.Id}/issues?projectId={project.Id}&sort=-priority");
        var statusAscending = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>(
            $"/api/organizations/{organization.Id}/issues?projectId={project.Id}&sort=status");
        var statusDescending = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>(
            $"/api/organizations/{organization.Id}/issues?projectId={project.Id}&sort=-status");

        priorityAscending!.Data.Items.Select(issue => issue.Priority)
            .Should()
            .Equal("Low", "Medium", "High", "Critical");
        priorityDescending!.Data.Items.Select(issue => issue.Priority)
            .Should()
            .Equal("Critical", "High", "Medium", "Low");
        statusAscending!.Data.Items.Select(issue => issue.Status)
            .Should()
            .Equal("Todo", "InProgress", "InReview", "Done");
        statusDescending!.Data.Items.Select(issue => issue.Status)
            .Should()
            .Equal("Done", "InReview", "InProgress", "Todo");
    }

    [Fact]
    public async Task ListIssues_SearchTreatsUnderscoreAsLiteralText()
    {
        var client = _factory.CreateClient();
        var owner = await client.RegisterAsync($"issue-search-owner-{Guid.NewGuid():N}@example.com", "Issue Test");
        client.Authorize(owner);
        var organization = await client.CreateOrganizationAsync("Issue Test Organization");
        var project = await client.CreateProjectAsync(organization.Id, "Search Project", "SRCH");
        var literalMatch = await client.CreateIssueAsync(
            organization.Id,
            project.Id,
            title: "Fix api_gateway callback");
        await client.CreateIssueAsync(
            organization.Id,
            project.Id,
            title: "Fix apiXgateway callback");

        var response = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<IssueListItemResponse>>>(
            $"/api/organizations/{organization.Id}/issues?projectId={project.Id}&search=api_gateway");

        response!.Data.Items.Should().ContainSingle();
        response.Data.Items[0].Id.Should().Be(literalMatch.Id);
    }

    [Fact]
    public async Task IssueEndpoints_RejectAnonymousRequests()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid()}/issues");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
