using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Infrastructure;
using TaskOps.Api.Persistence;
using TaskOps.Api.Shared.Api;

var builder = WebApplication.CreateBuilder(args);
var databaseConnectionString = builder.Configuration.GetConnectionString("TaskOpsDatabase");

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.AddDbContext<TaskOpsDbContext>(options =>
{
    options.UseNpgsql(databaseConnectionString);
});
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql");

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration, app.Environment);

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    var statusCode = httpContext.Response.StatusCode;

    await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
    {
        HttpContext = httpContext,
        ProblemDetails = new ProblemDetails
        {
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Status = statusCode,
            Instance = httpContext.Request.Path
        }
    });
});
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapSwaggerUi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", (HttpContext httpContext) =>
    Results.Ok(ApiResponse.Success(new
    {
        service = "TaskOps.Api",
        message = "TaskOps API is running."
    }, httpContext.TraceIdentifier)))
    .WithName("Root")
    .WithTags("System");

app.MapGet("/api/status", (IHostEnvironment environment, HttpContext httpContext) =>
    Results.Ok(ApiResponse.Success(new
    {
        service = "TaskOps.Api",
        environment = environment.EnvironmentName,
        utcNow = DateTimeOffset.UtcNow
    }, httpContext.TraceIdentifier)))
    .WithName("GetApiStatus")
    .WithTags("System");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync
})
.WithName("Health")
.WithTags("System");

app.Run();
