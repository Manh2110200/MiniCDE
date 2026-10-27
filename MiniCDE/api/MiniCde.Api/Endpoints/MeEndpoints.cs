using MiniCde.Api.Auth;

namespace MiniCde.Api.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (CurrentUserService current, CancellationToken ct) =>
        {
            var me = await current.GetOrCreateAsync(ct);
            return Results.Ok(new
            {
                me.Id, me.Email, me.DisplayName,
                Organization = new { me.Organization.Id, me.Organization.Name }
            });
        }).RequireAuthorization();
    }
}
