using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskOps.Api.Modules.Identity;
using TaskOps.Api.Modules.Issues;
using TaskOps.Api.Modules.Organizations;
using TaskOps.Api.Modules.Projects;

namespace TaskOps.Api.Tests.Infrastructure;

public static class ApiTestClientHelpers
{
    public static async Task<AuthResponse> RegisterAsync(
        this HttpClient client,
        string email,
        string displayName = "API Test",
        string password = "Password123!")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            displayName,
            password));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponse>>();
        return envelope!.Data;
    }

    public static void Authorize(this HttpClient client, AuthResponse authResponse)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authResponse.AccessToken);
    }

    public static async Task<AuthResponse> RegisterAndAuthorizeAsync(
        this HttpClient client,
        string email,
        string displayName = "API Test",
        string password = "Password123!")
    {
        var authResponse = await client.RegisterAsync(email, displayName, password);
        client.Authorize(authResponse);
        return authResponse;
    }

    public static async Task<OrganizationResponse> CreateOrganizationAsync(
        this HttpClient client,
        string name = "API Test Organization",
        string? slug = null)
    {
        slug ??= $"org-{Guid.NewGuid():N}"[..20];
        var response = await client.PostAsJsonAsync("/api/organizations", new CreateOrganizationRequest(name, slug));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<OrganizationResponse>>();
        return envelope!.Data;
    }

    public static async Task<OrganizationMemberResponse> AddMemberAsync(
        this HttpClient client,
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

    public static async Task<ProjectResponse> CreateProjectAsync(
        this HttpClient client,
        Guid organizationId,
        string name = "API Test Project",
        string? key = null,
        string? description = null)
    {
        key ??= $"P{Guid.NewGuid():N}"[..12];
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/projects",
            new CreateProjectRequest(name, key, description));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<ProjectResponse>>();
        return envelope!.Data;
    }

    public static async Task<IssueResponse> CreateIssueAsync(
        this HttpClient client,
        Guid organizationId,
        Guid projectId,
        string title = "API Test Issue",
        string? description = null,
        string priority = "Medium",
        Guid? assigneeId = null,
        DateOnly? dueDate = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/issues",
            new CreateIssueRequest(projectId, title, description, priority, assigneeId, dueDate));

        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<IssueResponse>>();
        return envelope!.Data;
    }
}
