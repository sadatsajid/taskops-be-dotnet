using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Api.Features.Organizations;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class OrganizationEndpointsTests(TaskOpsApiFactory factory) : IntegrationTestBase(factory), IClassFixture<TaskOpsApiFactory>
{
    private readonly TaskOpsApiFactory _factory = factory;

    [Fact]
    public async Task CreateOrganization_CreatorBecomesOwner()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"org-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        client.Authorize(registered);

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
        var owner = await ownerClient.RegisterAsync($"org-private-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var outsiderClient = _factory.CreateClient();
        var outsider = await outsiderClient.RegisterAsync($"org-outsider-{Guid.NewGuid():N}@example.com", "Organization Test");
        outsiderClient.Authorize(outsider);

        var response = await outsiderClient.GetAsync($"/api/organizations/{organization.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateOrganization_ForViewer_ReturnsForbidden()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-update-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var viewerClient = _factory.CreateClient();
        var viewer = await viewerClient.RegisterAsync($"org-viewer-{Guid.NewGuid():N}@example.com", "Organization Test");
        var member = await ownerClient.AddMemberAsync(organization.Id, viewer.CurrentUser.Email, "Viewer");
        member.Role.Should().Be("Viewer");

        viewerClient.Authorize(viewer);
        var response = await viewerClient.PutAsJsonAsync($"/api/organizations/{organization.Id}", new UpdateOrganizationRequest(
            "Viewer Rename",
            organization.Slug));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_CanManageMembers()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-members-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"org-developer-{Guid.NewGuid():N}@example.com", "Organization Test");

        var added = await ownerClient.AddMemberAsync(organization.Id, developer.CurrentUser.Email, "Developer");
        added.Role.Should().Be("Developer");

        var roleResponse = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{added.Id}/role",
            new ChangeOrganizationMemberRoleRequest("ProjectManager"));
        roleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var roleEnvelope = await roleResponse.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationMemberResponse>>();
        roleEnvelope!.Data.Role.Should().Be("ProjectManager");

        var listResponse = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<OrganizationMemberResponse>>>(
            $"/api/organizations/{organization.Id}/members");
        listResponse!.Data.Items.Should().Contain(member => member.UserId == developer.CurrentUser.Id && member.Role == "ProjectManager");

        var removeResponse = await ownerClient.DeleteAsync($"/api/organizations/{organization.Id}/members/{added.Id}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listAfterRemove = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<OrganizationMemberResponse>>>(
            $"/api/organizations/{organization.Id}/members");
        listAfterRemove!.Data.Items.Should().NotContain(member => member.UserId == developer.CurrentUser.Id);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ProjectManager")]
    [InlineData("Developer")]
    [InlineData("Viewer")]
    public async Task AddMember_ForNonOwner_ReturnsForbidden(string role)
    {
        var ownerClient = _factory.CreateClient();
        await ownerClient.RegisterAndAuthorizeAsync($"org-non-owner-owner-{role}-{Guid.NewGuid():N}@example.com", "Organization Test");
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var memberClient = _factory.CreateClient();
        var member = await memberClient.RegisterAndAuthorizeAsync($"org-non-owner-member-{role}-{Guid.NewGuid():N}@example.com", "Organization Test");
        await ownerClient.AddMemberAsync(organization.Id, member.CurrentUser.Email, role);

        var candidateClient = _factory.CreateClient();
        var candidate = await candidateClient.RegisterAsync($"org-non-owner-candidate-{role}-{Guid.NewGuid():N}@example.com", "Organization Test");

        var response = await memberClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddOrganizationMemberRequest(candidate.CurrentUser.Email, "Developer"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListMembers_ForNonMember_ReturnsNotFound()
    {
        var ownerClient = _factory.CreateClient();
        await ownerClient.RegisterAndAuthorizeAsync($"org-list-private-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var outsiderClient = _factory.CreateClient();
        await outsiderClient.RegisterAndAuthorizeAsync($"org-list-outsider-{Guid.NewGuid():N}@example.com", "Organization Test");

        var response = await outsiderClient.GetAsync($"/api/organizations/{organization.Id}/members");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Owner_CannotDemoteLastOwner()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"org-last-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        client.Authorize(registered);
        var organization = await client.CreateOrganizationAsync("Organization Test");

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

    [Fact]
    public async Task ListOrganizations_ReturnsPagedResult()
    {
        var client = _factory.CreateClient();
        var registered = await client.RegisterAsync($"org-page-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        client.Authorize(registered);
        await client.CreateOrganizationAsync("Organization Test");
        await client.CreateOrganizationAsync("Organization Test");

        var response = await client.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<OrganizationListItemResponse>>>(
            "/api/organizations?limit=1");

        response.Should().NotBeNull();
        response!.Data.Items.Should().HaveCount(1);
        response.Data.Offset.Should().Be(0);
        response.Data.Limit.Should().Be(1);
        response.Data.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task ListMembers_ClampsInvalidLimit()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-member-page-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var response = await ownerClient.GetFromJsonAsync<ApiResponseEnvelope<PagedResponse<OrganizationMemberResponse>>>(
            $"/api/organizations/{organization.Id}/members?limit=0");

        response.Should().NotBeNull();
        response!.Data.Items.Should().HaveCount(1);
        response.Data.Limit.Should().Be(1);
        response.Data.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task AddMember_WithNumericRole_ReturnsValidationProblem()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-numeric-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var developerClient = _factory.CreateClient();
        var developer = await developerClient.RegisterAsync($"org-numeric-developer-{Guid.NewGuid():N}@example.com", "Organization Test");

        var response = await ownerClient.PostAsJsonAsync(
            $"/api/organizations/{organization.Id}/members",
            new AddOrganizationMemberRequest(developer.CurrentUser.Email, "1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("role");
    }

    [Fact]
    public async Task ChangeMemberRole_WithNumericRole_ReturnsValidationProblem()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-role-numeric-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var response = await ownerClient.PutAsJsonAsync(
            $"/api/organizations/{organization.Id}/members/{organization.CurrentMember.Id}/role",
            new ChangeOrganizationMemberRoleRequest("1"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("role");
    }

    [Fact]
    public async Task ConcurrentOwnerDemotions_LeaveAtLeastOneOwner()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-concurrent-demote-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var secondOwnerClient = _factory.CreateClient();
        var secondOwner = await secondOwnerClient.RegisterAsync($"org-concurrent-demote-second-{Guid.NewGuid():N}@example.com", "Organization Test");
        var secondOwnerMember = await ownerClient.AddMemberAsync(organization.Id, secondOwner.CurrentUser.Email, "Owner");
        secondOwnerClient.Authorize(secondOwner);

        var responses = await Task.WhenAll(
            ownerClient.PutAsJsonAsync(
                $"/api/organizations/{organization.Id}/members/{organization.CurrentMember.Id}/role",
                new ChangeOrganizationMemberRoleRequest("Admin")),
            secondOwnerClient.PutAsJsonAsync(
                $"/api/organizations/{organization.Id}/members/{secondOwnerMember.Id}/role",
                new ChangeOrganizationMemberRoleRequest("Admin")));

        responses.Count(response => response.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var ownerCount = await CountOwnersAsync(organization.Id);
        ownerCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentOwnerRemovals_LeaveAtLeastOneOwner()
    {
        var ownerClient = _factory.CreateClient();
        var owner = await ownerClient.RegisterAsync($"org-concurrent-remove-owner-{Guid.NewGuid():N}@example.com", "Organization Test");
        ownerClient.Authorize(owner);
        var organization = await ownerClient.CreateOrganizationAsync("Organization Test");

        var secondOwnerClient = _factory.CreateClient();
        var secondOwner = await secondOwnerClient.RegisterAsync($"org-concurrent-remove-second-{Guid.NewGuid():N}@example.com", "Organization Test");
        var secondOwnerMember = await ownerClient.AddMemberAsync(organization.Id, secondOwner.CurrentUser.Email, "Owner");
        secondOwnerClient.Authorize(secondOwner);

        var responses = await Task.WhenAll(
            ownerClient.DeleteAsync($"/api/organizations/{organization.Id}/members/{organization.CurrentMember.Id}"),
            secondOwnerClient.DeleteAsync($"/api/organizations/{organization.Id}/members/{secondOwnerMember.Id}"));

        responses.Count(response => response.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        var ownerCount = await CountOwnersAsync(organization.Id);
        ownerCount.Should().Be(1);
    }

    private async Task<int> CountOwnersAsync(Guid organizationId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskOpsDbContext>();

        return await dbContext.OrganizationMembers.CountAsync(member =>
            member.OrganizationId == organizationId &&
            member.Role == OrganizationRole.Owner);
    }
}
