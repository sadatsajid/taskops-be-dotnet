using TaskOps.Api.Features.Auth;
using TaskOps.Api.Features.System;
using TaskOps.Api.Infrastructure;
using TaskOps.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddTaskOpsApi(builder.Configuration)
    .AddTaskOpsPersistence(builder.Configuration)
    .AddTaskOpsOpenApi();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Configuration, app.Environment);

app.UseTaskOpsApi();

app.MapSystemEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program;
