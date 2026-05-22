using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Application.Features.Projects;
using TaskOps.Application.Shared.Api;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class ProjectEndpointsTests(TaskOpsApiFactory factory) : IntegrationTestBase(factory), IClassFixture<TaskOpsApiFactory>
{
    private readonly TaskOpsApiFactory _factory = factory;

    [Fact]
    public async Task CreateProject_ForOwner_CreatesOrganizationScopedProject()
    {
        var client = _factory.CreateClient();
        await client.RegisterAndAuthorizeAsync($"project-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        var organization = await client.CreateOrganizationAsync("Project Test Organization");

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/projects",
            new CreateProjectRequest("Backend Platform", "api", "Core service work"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<ProjectResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Data.OrganizationId.Should().Be(organization.Id);
        envelope.Data.Name.Should().Be("Backend Platform");
        envelope.Data.Key.Should().Be("API");
        envelope.Data.Description.Should().Be("Core service work");
        envelope.Data.IsArchived.Should().BeFalse();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ProjectManager")]
    public async Task CreateProject_ForOrganizationManagers_CreatesProject(string role)
    {
        var ownerClient = _factory.CreateClient();
        await ownerClient.RegisterAndAuthorizeAsync($"project-manager-owner-{role}-{Guid.NewGuid():N}@example.com", "Project Test");
        var organization = await ownerClient.CreateOrganizationAsync("Project Test Organization");

        var managerClient = _factory.CreateClient();
        var manager = await managerClient.RegisterAndAuthorizeAsync($"project-manager-{role}-{Guid.NewGuid():N}@example.com", "Project Test");
        await ownerClient.AddMemberAsync(organization.Id, manager.CurrentUser.Email, role);

        var response = await managerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/projects",
            new CreateProjectRequest($"{role} Project", $"{role[..3]}{Guid.NewGuid():N}"[..8], null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("Viewer")]
    public async Task CreateProject_ForNonManagerMember_ReturnsForbidden(string role)
    {
        var ownerClient = _factory.CreateClient();
        await ownerClient.RegisterAndAuthorizeAsync($"project-forbidden-owner-{role}-{Guid.NewGuid():N}@example.com", "Project Test");
        var organization = await ownerClient.CreateOrganizationAsync("Project Test Organization");

        var memberClient = _factory.CreateClient();
        var member = await memberClient.RegisterAndAuthorizeAsync($"project-forbidden-member-{role}-{Guid.NewGuid():N}@example.com", "Project Test");
        await ownerClient.AddMemberAsync(organization.Id, member.CurrentUser.Email, role);

        var response = await memberClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/projects",
            new CreateProjectRequest($"{role} Project", $"{role[..3]}{Guid.NewGuid():N}"[..8], null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProject_ForViewer_ReturnsForbidden()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"project-viewer-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Project Test Organization");

        var viewerClient = _factory.CreateClient();
        var viewer = await viewerClient.RegisterAsync($"project-viewer-{Guid.NewGuid():N}@example.com", "Project Test");
        await ownerClient.AddMemberAsync(organization.Id, viewer.CurrentUser.Email, "Viewer");
        viewerClient.Authorize(viewer);

        var response = await viewerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/projects",
            new CreateProjectRequest("Viewer Project", "VIEW", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProject_ForNonMember_ReturnsNotFound()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"project-private-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Project Test Organization");
        var project = await ownerClient.CreateProjectAsync(organization.Id);

        var outsiderClient = _factory.CreateClient();
        var outsider = await outsiderClient.RegisterAsync($"project-outsider-{Guid.NewGuid():N}@example.com", "Project Test");
        outsiderClient.Authorize(outsider);

        var response = await outsiderClient.GetAsync($"/api/organizations/{organization.Id}/projects/{project.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateProject_WithProjectFromAnotherOrganization_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        await client.RegisterAndAuthorizeAsync($"project-cross-route-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        var firstOrganization = await client.CreateOrganizationAsync("First Organization");
        var secondOrganization = await client.CreateOrganizationAsync("Second Organization");
        var project = await client.CreateProjectAsync(firstOrganization.Id, "First Project", "FIRST");

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{secondOrganization.Id}/projects/{project.Id}",
            new UpdateProjectRequest("Moved Project", "MOVED", null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListProjects_ExcludesArchivedProjectsByDefault()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"project-list-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        client.Authorize(registered);
        var organization = await client.CreateOrganizationAsync("Project Test Organization");
        var active = await client.CreateProjectAsync(organization.Id, "Active Project", "ACT");
        var archived = await client.CreateProjectAsync(organization.Id, "Archived Project", "ARC");

        var archiveResponse = await client.PostAsync(
            $"/api/organizations/{organization.Id}/projects/{archived.Id}/archive",
            content: null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var defaultList = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<ProjectListItemResponse>>>(
            $"/api/organizations/{organization.Id}/projects");
        var listWithArchived = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<ProjectListItemResponse>>>(
            $"/api/organizations/{organization.Id}/projects?includeArchived=true");

        defaultList!.Data.Items.Should().ContainSingle(project => project.Id == active.Id);
        defaultList.Data.Items.Should().NotContain(project => project.Id == archived.Id);
        listWithArchived!.Data.Items.Should().Contain(project => project.Id == archived.Id && project.IsArchived);
    }

    [Fact]
    public async Task UpdateProject_WithDuplicateKeyInOrganization_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"project-duplicate-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        client.Authorize(registered);
        var organization = await client.CreateOrganizationAsync("Project Test Organization");
        await client.CreateProjectAsync(organization.Id, "First Project", "FIRST");
        var second = await client.CreateProjectAsync(organization.Id, "Second Project", "SECOND");

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/projects/{second.Id}",
            new UpdateProjectRequest("Second Project", "FIRST", null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateProject_AllowsSameKeyAcrossDifferentOrganizations()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"project-cross-org-owner-{Guid.NewGuid():N}@example.com", "Project Test");
        client.Authorize(registered);
        var firstOrganization = await client.CreateOrganizationAsync("Project Test Organization");
        var secondOrganization = await client.CreateOrganizationAsync("Project Test Organization");

        var first = await client.CreateProjectAsync(firstOrganization.Id, "First API", "SHARED");
        var second = await client.CreateProjectAsync(secondOrganization.Id, "Second API", "SHARED");

        first.Key.Should().Be("SHARED");
        second.Key.Should().Be("SHARED");
        first.OrganizationId.Should().NotBe(second.OrganizationId);
    }

    [Fact]
    public async Task ProjectEndpoints_RejectAnonymousRequests()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid()}/projects");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
