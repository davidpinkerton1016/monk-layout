namespace MonkLayout;

// The loader and UI callbacks must never read game objects directly. Only the
// framework update refreshes this cache. Publish one immutable reference so UI
// readers cannot observe a partially updated PlayerSession value.
internal sealed record SessionSnapshot(PlayerSession Session, bool IsAvailable, string Error);

internal sealed class SessionCache(Func<PlayerSession> readSession)
{
    private SessionSnapshot snapshot = new(default, false, "Waiting for the first framework update.");

    internal SessionSnapshot Snapshot => Volatile.Read(ref snapshot);

    internal void Refresh()
    {
        SessionSnapshot next;
        try { next = new(readSession(), true, ""); }
        catch (Exception ex) { next = new(default, false, $"Player data is not available yet: {ex.Message}"); }
        Volatile.Write(ref snapshot, next);
    }
}
