using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskOps.Api.Features.Auth;
using TaskOps.Api.Features.Organizations;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class OrganizationEndpointsTests(TaskOpsApiFactory factory) : IClassFixture<TaskOpsApiFactory>
{
    private readonly TaskOpsApiFactory _factory = factory;

    [Fact]
    public async Task CreateOrganization_CreatorBecomesOwner()
    {
        var client = _factory.CreateClient();
        var registered = await RegisterAsync(client, $"org-owner-{Guid.NewGuid():N}@example.com");
        Authorize(client, registered);

        var createResponse = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest(
            "Acme Platform",
            $"acme-{Guid.NewGuid():N}"[..18]));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var envelope = await createResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationResponse>>();

        envelope.Should().NotBeNull();
        envelope!.Data.Name.Should().Be("Acme Platform");
        envelope.Data.CurrentMember.UserId.Should().Be(registered.CurrentUser.Id);
        envelope.Data.CurrentMember.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task GetOrganization_ForNonMember_ReturnsNotFound()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await RegisterAsync(ownerClient, $"org-private-owner-{Guid.NewGuid():N}@example.com");
        Authorize(ownerClient, owner);
        var organization = await CreateOrganizationAsync(ownerClient);

        var outsiderClient = _factory.CreateClient();
        var outsider = await RegisterAsync(outsiderClient, $"org-outsider-{Guid.NewGuid():N}@example.com");
        Authorize(outsiderClient, outsider);

        var response = await outsiderClient.GetAsync($"/api/organizations/{organization.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrganization_ForViewer_ReturnsForbidden()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await RegisterAsync(ownerClient, $"org-update-owner-{Guid.NewGuid():N}@example.com");
        Authorize(ownerClient, owner);
        var organization = await CreateOrganizationAsync(ownerClient);

        var viewerClient = _factory.CreateClient();
        var viewer = await RegisterAsync(viewerClient, $"org-viewer-{Guid.NewGuid():N}@example.com");
        var member = await AddMemberAsync(ownerClient, organization.Id, viewer.CurrentUser.Email, "Viewer");
        member.Role.Should().Be("Viewer");

        Authorize(viewerClient, viewer);
        var response = await viewerClient.PutAsJsonAsync($"/api/organizations/{organization.Id}", new UpdateOrganizationRequest(
            "Viewer Rename",
            organization.Slug));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_CanManageMembers()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await RegisterAsync(ownerClient, $"org-members-owner-{Guid.NewGuid():N}@example.com");
        Authorize(ownerClient, owner);
        var organization = await CreateOrganizationAsync(ownerClient);

        var developerClient = _factory.CreateClient();
        var developer = await RegisterAsync(developerClient, $"org-developer-{Guid.NewGuid():N}@example.com");

        var added = await AddMemberAsync(ownerClient, organization.Id, developer.CurrentUser.Email, "Developer");
        added.Role.Should().Be("Developer");

        var roleResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{added.Id}/role",
            new ChangeOrganizationMemberRoleRequest("ProjectManager"));
        roleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var roleEnvelope = await roleResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationMemberResponse>>();
        roleEnvelope!.Data.Role.Should().Be("ProjectManager");

        var listResponse = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<List<OrganizationMemberResponse>>>(
            $"/api/organizations/{organization.Id}/members");
        listResponse!.Data.Should().Contain(member => member.UserId == developer.CurrentUser.Id && member.Role == "ProjectManager");

        var removeResponse = await ownerClient.DeleteAsync($"/api/organizations/{organization.Id}/members/{added.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterRemove = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<List<OrganizationMemberResponse>>>(
            $"/api/organizations/{organization.Id}/members");
        listAfterRemove!.Data.Should().NotContain(member => member.UserId == developer.CurrentUser.Id);
    }

    [Fact]
    public async Task Owner_CannotDemoteLastOwner()
    {
        var client = _factory.CreateClient();
        var registered = await RegisterAsync(client, $"org-last-owner-{Guid.NewGuid():N}@example.com");
        Authorize(client, registered);
        var organization = await CreateOrganizationAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{organization.CurrentMember.Id}/role",
            new ChangeOrganizationMemberRoleRequest("Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Organization must have an owner.");
    }

    [Fact]
    public async Task OrganizationEndpoints_RejectAnonymousRequests()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            "Organization Test",
            "Password123!"));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponse>>();
        return envelope!.Data;
    }

    private static async Task<OrganizationResponse> CreateOrganizationAsync(HttpClient client)
    {
        var slug = $"org-{Guid.NewGuid():N}"[..20];
        var response = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest(
            "Organization Test",
            slug));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationResponse>>();
        return envelope!.Data;
    }

    private static async Task<OrganizationMemberResponse> AddMemberAsync(
        HttpClient client,
        Guid organizationId,
        string email,
        string role)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/members",
            new AddOrganizationMemberRequest(email, role));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationMemberResponse>>();
        return envelope!.Data;
    }

    private static void Authorize(HttpClient client, AuthResponse authResponse)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authResponse.AccessToken);
    }
}
