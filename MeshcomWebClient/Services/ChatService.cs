using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MeshcomWebClient.Models;

namespace MeshcomWebClient.Services;

/// <summary>
/// Manages chat tabs and routes messages to the correct conversation.
/// Thread-safe singleton shared across all Blazor circuits.
/// </summary>
public class ChatService
{
    private readonly ConcurrentDictionary<string, ChatTab> _tabs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshcomMessage> _allMessages = [];
    private readonly object _lock = new();
    private readonly MeshcomSettings _settings;

    /// <summary>Raised when a message is added or a tab changes.</summary>
    public event Action? OnChange;

    public ChatService(IOptions<MeshcomSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>All open tabs.</summary>
    public IReadOnlyList<ChatTab> Tabs
    {
        get
        {
            lock (_lock)
            {
                return _tabs.Values.ToList();
            }
        }
    }

    /// <summary>All messages sorted newest-first (for the bottom pane).</summary>
    public IReadOnlyList<MeshcomMessage> AllMessages
    {
        get
        {
            lock (_lock)
            {
                return _allMessages.OrderByDescending(m => m.Timestamp).ToList();
            }
        }
    }

    /// <summary>
    /// Route an incoming message to the correct tab. Creates tab automatically if needed.
    /// </summary>
    public void AddIncomingMessage(MeshcomMessage message)
    {
        // Determine tab key based on destination:
        //   Broadcast (* / CQCQCQ)      → tab "*" ("Alle")
        //   Direct to us (MyCallsign)   → tab by sender callsign
        //   Group (any other dst)       → tab "#<group>"
        string tabKey;
        if (message.IsBroadcast)
        {
            tabKey = "*";
        }
        else if (string.Equals(message.To, _settings.MyCallsign, StringComparison.OrdinalIgnoreCase))
        {
            tabKey = message.From;
        }
        else
        {
            tabKey = "#" + message.To;
        }

        var tab = GetOrCreateTab(tabKey);
        lock (_lock)
        {
            _allMessages.Add(message);
            tab.Messages.Add(message);
        }

        NotifyChange();
    }

    /// <summary>
    /// Add an outgoing message to the correct tab.
    /// </summary>
    public void AddOutgoingMessage(MeshcomMessage message)
    {
        // Determine tab key: for broadcast use "*", otherwise use the destination callsign
        var tabKey = message.IsBroadcast ? "*" : message.To;
        var tab = GetOrCreateTab(tabKey);
        lock (_lock)
        {
            _allMessages.Add(message);
            tab.Messages.Add(message);
        }

        NotifyChange();
    }

    /// <summary>
    /// Add a message to the raw feed only, without routing it to any tab.
    /// Used for unparseable device data (status, telemetry, etc.).
    /// </summary>
    public void AddRawMessage(MeshcomMessage message)
    {
        lock (_lock)
        {
            _allMessages.Add(message);
        }

        NotifyChange();
    }

    /// <summary>Open a new tab manually.</summary>
    public ChatTab OpenTab(string key)
    {
        var tab = GetOrCreateTab(key);
        NotifyChange();
        return tab;
    }

    /// <summary>Close a tab.</summary>
    public void CloseTab(string key)
    {
        _tabs.TryRemove(key, out _);
        NotifyChange();
    }

    /// <summary>Get a specific tab.</summary>
    public ChatTab? GetTab(string key)
    {
        _tabs.TryGetValue(key, out var tab);
        return tab;
    }

    /// <summary>Get a thread-safe snapshot of a tab's messages.</summary>
    public IReadOnlyList<MeshcomMessage> GetTabMessages(string key)
    {
        if (!_tabs.TryGetValue(key, out var tab))
            return [];

        lock (_lock)
        {
            return tab.Messages.ToList();
        }
    }

    private ChatTab GetOrCreateTab(string key)
    {
        return _tabs.GetOrAdd(key, k => new ChatTab
        {
            Key = k,
            Title = k switch
            {
                "*" => "Alle",
                _ when k.StartsWith('#') => k,
                _ => k
            }
        });
    }

    private void NotifyChange()
    {
        OnChange?.Invoke();
    }
}
