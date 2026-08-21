namespace PlanningPoker.Client.Services;

/// <summary>
/// Holds the identity the player picked on the create/join screen so Board.razor doesn't need to
/// re-prompt for it. Registered scoped; in a WASM app that's effectively "this browser tab."
/// </summary>
public sealed class PlayerSessionState
{
    public string PlayerName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public bool IsSpectator { get; set; }
}
