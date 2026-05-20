using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TaskOps.Api.Tests.Infrastructure;

namespace TaskOps.Api.Tests;

public sealed class SystemEndpointsTests(TaskOpsApiFactory factory) : IClassFixture<TaskOpsApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsHealthyPostgresCheck()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"status\":\"Healthy\"");
        json.Should().Contain("\"name\":\"postgresql\"");
    }

    [Fact]
    public async Task Status_ReturnsApiEnvelope()
    {
        var response = await _client.GetFromJsonAsync<ApiResponseEnvelope<StatusResponse>>("/api/status");

        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Service.Should().Be("TaskOps.Api");
        response.Data.Environment.Should().Be("Testing");
        response.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record StatusResponse(string Service, string Environment, DateTimeOffset UtcNow);
}
