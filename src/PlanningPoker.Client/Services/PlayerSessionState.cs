using System.Text.Json;
using Microsoft.JSInterop;

namespace PlanningPoker.Client.Services;

/// <summary>
/// Holds the identity the player picked on the create/join screen so Board.razor doesn't need to
/// re-prompt for it. Registered scoped; in a WASM app that's effectively "this browser tab" -- except
/// a full page refresh also tears down and re-creates that scope, which is why the identity is
/// additionally mirrored into sessionStorage and restored via <see cref="RestoreAsync"/>, so refreshing
/// mid-room reconnects instead of bouncing back to the join screen.
/// </summary>
public sealed class PlayerSessionState
{
    private const string StorageKey = "planningpoker.session";

    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _jsModule;

    public PlayerSessionState(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public string PlayerName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public bool IsSpectator { get; set; }

    /// <summary>Room the stored <see cref="PlayerId"/> belongs to, so it's only reused for a refresh of
    /// that same room -- not carried over if the tab later visits a different one.</summary>
    public string? RoomId { get; set; }

    /// <summary>Set once the board page successfully joins a room, so a page refresh can rejoin as this
    /// same player (see <c>GameHub.JoinRoom</c>'s <c>existingPlayerId</c>) instead of appearing as a
    /// brand new one -- which is what was silently costing a refreshing host their host status.</summary>
    public Guid? PlayerId { get; set; }

    public async Task RestoreAsync()
    {
        if (!string.IsNullOrWhiteSpace(PlayerName))
        {
            return;
        }

        var module = await GetModuleAsync();
        var json = await module.InvokeAsync<string?>("loadSessionItem", StorageKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var stored = JsonSerializer.Deserialize<StoredSession>(json);
        if (stored is null)
        {
            return;
        }

        PlayerName = stored.PlayerName;
        AvatarUrl = stored.AvatarUrl;
        IsSpectator = stored.IsSpectator;
        RoomId = stored.RoomId;
        PlayerId = stored.PlayerId;
    }

    public async Task SaveAsync()
    {
        var module = await GetModuleAsync();
        var json = JsonSerializer.Serialize(new StoredSession(PlayerName, AvatarUrl, IsSpectator, RoomId, PlayerId));
        await module.InvokeVoidAsync("saveSessionItem", StorageKey, json);
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _jsModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");

    private sealed record StoredSession(
        string PlayerName, string? AvatarUrl, bool IsSpectator, string? RoomId, Guid? PlayerId);
}
