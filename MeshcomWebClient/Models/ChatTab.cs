namespace MeshcomWebClient.Models;

public class ChatTab
{
    /// <summary>
    /// Unique key for this tab. Callsign, group name, or special values like "*" (broadcast).
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display title for the tab.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Messages belonging to this conversation.</summary>
    public List<MeshcomMessage> Messages { get; set; } = [];
}
