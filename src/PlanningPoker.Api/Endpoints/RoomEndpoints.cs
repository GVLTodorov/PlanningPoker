using PlanningPoker.Api.Giphy;
using PlanningPoker.Api.Mapping;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Domain.Rooms;
using DeckCatalog = PlanningPoker.Domain.Decks.DeckCatalog;

namespace PlanningPoker.Api.Endpoints;

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api");

        group.MapGet("/rooms/name-suggestion", () =>
            Results.Ok(new RoomNameSuggestionResponse(RoomNameGenerator.Generate())));

        group.MapPost("/rooms", (CreateRoomRequest request, IRoomRepository rooms) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Room name is required.");
            }

            var room = rooms.Create(request.Name.Trim(), request.DeckType.ToDomain());
            if (room is null)
            {
                return Results.Conflict(
                    "That room name is already taken or has no usable characters for a room link. Try a different name.");
            }

            return Results.Created($"/api/rooms/{room.Id.Value}", room.ToSummaryResponse());
        });

        group.MapGet("/rooms/{roomId}", (string roomId, IRoomRepository rooms) =>
        {
            if (!RoomId.TryParse(roomId, out var parsed) || !rooms.TryGet(parsed, out var room) || room is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(room.ToSummaryResponse());
        });

        group.MapGet("/decks", () =>
        {
            var decks = DeckCatalog.AllTypes
                .Select(type => new DeckResponse(
                    type.ToDeckType(),
                    DeckCatalog.GetDisplayName(type),
                    DeckCatalog.Get(type).Select(card => card.ToCardOptionResponse()).ToList()))
                .ToList();

            return Results.Ok(decks);
        });

        group.MapGet("/avatars/random", async (IGiphyClient giphy, int count = 3) =>
        {
            var urls = await giphy.GetRandomImageUrlsAsync(Math.Clamp(count, 1, 10));
            return Results.Ok(urls);
        });

        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
    }
}
