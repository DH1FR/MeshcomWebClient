namespace MeshcomWebClient.Models;

/// <summary>
/// Serialisable snapshot of the runtime state persisted to disk between restarts.
/// </summary>
public class PersistenceSnapshot
{
    public DateTime SavedAt { get; set; } = DateTime.Now;

    public List<ChatTab> Tabs { get; set; } = [];

    public List<HeardStation> MhList { get; set; } = [];

    public List<MeshcomMessage> MonitorMessages { get; set; } = [];
}
