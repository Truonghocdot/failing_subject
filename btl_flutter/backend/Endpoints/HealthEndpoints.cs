using backend.Data;

namespace backend.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (AppDbContext db, CancellationToken ct) =>
        {
            var dbUp = await db.Database.CanConnectAsync(ct);
            var body = new { status = dbUp ? "ok" : "degraded", database = dbUp ? "up" : "down" };
            return dbUp ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("Health");

        return app;
    }
}
