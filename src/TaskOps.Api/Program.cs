using TaskOps.Api.Features;
using TaskOps.Api.Infrastructure;
using TaskOps.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddTaskOpsApi(builder.Configuration)
    .AddTaskOpsPersistence(builder.Configuration)
    .AddTaskOpsOpenApi();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Environment);

app.UseTaskOpsApi();

app.MapTaskOpsEndpoints();

app.Run();

public partial class Program;
