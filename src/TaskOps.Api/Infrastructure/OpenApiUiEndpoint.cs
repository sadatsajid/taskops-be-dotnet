namespace TaskOps.Api.Infrastructure;

public static class OpenApiUiEndpoint
{
    public static IEndpointRouteBuilder MapSwaggerUi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/swagger", () => Results.Content(SwaggerHtml, "text/html"))
            .ExcludeFromDescription();

        endpoints.MapGet("/swagger/index.html", () => Results.Content(SwaggerHtml, "text/html"))
            .ExcludeFromDescription();

        return endpoints;
    }

    private const string SwaggerHtml = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>TaskOps API</title>
          <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css">
        </head>
        <body>
          <div id="swagger-ui"></div>
          <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
          <script>
            window.ui = SwaggerUIBundle({
              url: "/openapi/v1.json",
              dom_id: "#swagger-ui"
            });
          </script>
        </body>
        </html>
        """;
}
