using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using MeshcomWebDesk.Models;
using Microsoft.Extensions.Options;

namespace MeshcomWebDesk.Services;

/// <summary>
/// Manages QSO conversation summaries generated via OpenAI API.
/// Summaries are stored in the MySQL table <c>qso_summaries</c> (auto-created).
/// Only active when the database provider is MySQL and AI is enabled.
/// </summary>
public sealed class QsoSummaryService
{
    private const string SummaryTable = "qso_summaries";

    private readonly IOptionsMonitor<MeshcomSettings> _settings;
    private readonly ILogger<QsoSummaryService>        _logger;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    // Schema migration: runs once per connection string
    private string _lastMigratedConnStr = string.Empty;
    private readonly SemaphoreSlim _migrateLock = new(1, 1);

    public QsoSummaryService(IOptionsMonitor<MeshcomSettings> settings, ILogger<QsoSummaryService> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a summary exists for <paramref name="callsignBase"/> AND
    /// the last QSO is older than <see cref="AiSettings.ThresholdDays"/>.
    /// <summary>
    /// Returns true when either:
    /// <list type="bullet">
    ///   <item>a stored summary already exists for <paramref name="callsignBase"/>, OR</item>
    ///   <item>the monitor table contains direct messages with this station whose last QSO
    ///         is older than <see cref="AiSettings.ThresholdDays"/> (existing data).</item>
    /// </list>
    /// </summary>
    public async Task<bool> HasSummaryAsync(string callsignBase, CancellationToken ct = default)
    {
        if (!IsDbAvailable(out var db, out var ai))
        {
            var s = _settings.CurrentValue;
            _logger.LogInformation("QsoSummaryService: HasSummaryAsync({Callsign}) → IsDbAvailable=false " +
                "(AiEnabled={Enabled}, Provider={Provider}, ConnStr={HasConn})",
                callsignBase, s.Ai.Enabled, s.Database.Provider,
                !string.IsNullOrWhiteSpace(s.Database.MySqlConnectionString));
            return false;
        }

        try
        {
            await using var conn = new MySqlConnection(db.MySqlConnectionString);
            await conn.OpenAsync(ct);
            await EnsureSchemaAsync(conn, db, ct);

            // 1. A stored summary already exists → always show the icon
            await using var summaryCmd = new MySqlCommand(
                $"SELECT COUNT(*) FROM `{SummaryTable}` WHERE callsign_base = @cs", conn);
            summaryCmd.Parameters.AddWithValue("@cs", callsignBase);
            var summaryCount = Convert.ToInt32(await summaryCmd.ExecuteScalarAsync(ct));
            if (summaryCount > 0)
            {
                _logger.LogInformation("QsoSummaryService: HasSummaryAsync({Callsign}) → true (existing summary)", callsignBase);
                return true;
            }

            // 2. No summary yet – check if there are messages in the monitor table
            await using var msgCmd = new MySqlCommand(
                $"""
                SELECT MAX(timestamp) FROM `{db.MySqlTableName}`
                WHERE (from_call = @cs OR from_call LIKE @csLike
                    OR to_call   = @cs OR to_call   LIKE @csLike)
                  AND is_position_beacon = 0
                  AND is_telemetry       = 0
                  AND text IS NOT NULL AND text != ''
                """, conn);
            msgCmd.Parameters.AddWithValue("@cs",     callsignBase);
            msgCmd.Parameters.AddWithValue("@csLike", callsignBase + "-%");

            var result = await msgCmd.ExecuteScalarAsync(ct);
            if (result is null or DBNull)
            {
                _logger.LogInformation("QsoSummaryService: HasSummaryAsync({Callsign}) → false (no messages found in table '{Table}')",
                    callsignBase, db.MySqlTableName);
                return false;
            }

            var lastQso  = Convert.ToDateTime(result);
            var ageDays  = (DateTime.UtcNow - lastQso.ToUniversalTime()).TotalDays;
            var hasIcon  = ageDays >= ai.ThresholdDays;
            _logger.LogInformation(
                "QsoSummaryService: HasSummaryAsync({Callsign}) → {Result} (lastQso={LastQso:yyyy-MM-dd}, age={Age:F1}d, threshold={Threshold}d)",
                callsignBase, hasIcon, lastQso, ageDays, ai.ThresholdDays);
            return hasIcon;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QsoSummaryService: HasSummaryAsync failed for {Callsign}", callsignBase);
            return false;
        }
    }

    /// <summary>
    /// Reads the stored summary record for <paramref name="callsignBase"/>.
    /// If no summary exists yet but messages are present in the monitor table,
    /// returns a placeholder record so the modal can still show last-QSO info.
    /// </summary>
    public async Task<QsoSummaryRecord?> GetSummaryAsync(string callsignBase, CancellationToken ct = default)
    {
        if (!IsDbAvailable(out var db, out _)) return null;

        try
        {
            await using var conn = new MySqlConnection(db.MySqlConnectionString);
            await conn.OpenAsync(ct);
            await EnsureSchemaAsync(conn, db, ct);

            // Try to load existing summary first
            await using var cmd = new MySqlCommand(
                $"""
                SELECT callsign_base, summary_text, last_qso_at, message_count, created_at
                FROM `{SummaryTable}`
                WHERE callsign_base = @cs
                LIMIT 1
                """, conn);
            cmd.Parameters.AddWithValue("@cs", callsignBase);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new QsoSummaryRecord(
                    CallsignBase : reader.GetString(0),
                    SummaryText  : reader.GetString(1),
                    LastQsoAt    : reader.GetDateTime(2),
                    MessageCount : reader.GetInt32(3),
                    CreatedAt    : reader.GetDateTime(4)
                );
            }
            await reader.CloseAsync();

            // No stored summary yet – build a placeholder from existing monitor data
            var lastQso = await GetLastQsoTimeInternalAsync(conn, db, callsignBase, ct);
            if (lastQso is null) return null;

            var msgCount = await GetMessageCountInternalAsync(conn, db, callsignBase, ct);
            return new QsoSummaryRecord(
                CallsignBase : callsignBase,
                SummaryText  : string.Empty,   // empty = "not yet generated" indicator for the modal
                LastQsoAt    : lastQso.Value,
                MessageCount : msgCount,
                CreatedAt    : DateTime.MinValue
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QsoSummaryService: GetSummaryAsync failed for {Callsign}", callsignBase);
            return null;
        }
    }

    /// <summary>
    /// Reads messages for <paramref name="callsignBase"/> from the monitor table,
    /// sends them to the OpenAI API and stores the result.
    /// </summary>
    public async Task<QsoSummaryRecord?> GenerateSummaryAsync(string callsignBase, CancellationToken ct = default)
    {
        if (!IsAvailable(out var db, out var ai)) return null;

        try
        {
            await using var conn = new MySqlConnection(db.MySqlConnectionString);
            await conn.OpenAsync(ct);
            await EnsureSchemaAsync(conn, db, ct);

            // Load messages for this callsign (both directions), filtered by SummaryDays
            var messages = await LoadMessagesAsync(conn, db, callsignBase, ai, ct);
            if (messages.Count == 0)
            {
                _logger.LogInformation("QsoSummaryService: no messages found for {Callsign} (SummaryDays={Days})",
                    callsignBase, ai.SummaryDays);
                return null;
            }

            var lastQsoAt    = messages.Max(m => m.Timestamp);
            var messageCount = messages.Count;

            var summaryText = await CallOpenAiAsync(ai, callsignBase, messages, ct);
            if (summaryText is null) return null;

            // Upsert summary
            await using var upsert = new MySqlCommand(
                $"""
                INSERT INTO `{SummaryTable}` (callsign_base, summary_text, last_qso_at, message_count, created_at)
                VALUES (@cs, @txt, @lqa, @cnt, @now)
                ON DUPLICATE KEY UPDATE
                    summary_text  = VALUES(summary_text),
                    last_qso_at   = VALUES(last_qso_at),
                    message_count = VALUES(message_count),
                    created_at    = VALUES(created_at)
                """, conn);
            upsert.Parameters.AddWithValue("@cs",  callsignBase);
            upsert.Parameters.AddWithValue("@txt", summaryText);
            upsert.Parameters.AddWithValue("@lqa", lastQsoAt);
            upsert.Parameters.AddWithValue("@cnt", messageCount);
            upsert.Parameters.AddWithValue("@now", DateTime.Now);
            await upsert.ExecuteNonQueryAsync(ct);

            _logger.LogInformation("QsoSummaryService: summary generated and stored for {Callsign} ({Count} messages)", callsignBase, messageCount);

            return new QsoSummaryRecord(callsignBase, summaryText, lastQsoAt, messageCount, DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QsoSummaryService: GenerateSummaryAsync failed for {Callsign}", callsignBase);
            return null;
        }
    }

    /// <summary>Returns the timestamp of the last direct message with <paramref name="callsignBase"/> from the DB.</summary>
    public async Task<DateTime?> GetLastQsoTimeAsync(string callsignBase, CancellationToken ct = default)
    {
        if (!IsAvailable(out var db, out _)) return null;

        try
        {
            await using var conn = new MySqlConnection(db.MySqlConnectionString);
            await conn.OpenAsync(ct);

            // Match callsign with or without SSID
            await using var cmd = new MySqlCommand(
                $"""
                SELECT MAX(timestamp) FROM `{db.MySqlTableName}`
                WHERE (from_call = @cs OR from_call LIKE @csLike
                    OR to_call  = @cs OR to_call  LIKE @csLike)
                  AND is_position_beacon = 0
                  AND is_telemetry       = 0
                """, conn);
            cmd.Parameters.AddWithValue("@cs",     callsignBase);
            cmd.Parameters.AddWithValue("@csLike", callsignBase + "-%");

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is null or DBNull ? null : Convert.ToDateTime(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QsoSummaryService: GetLastQsoTimeAsync failed for {Callsign}", callsignBase);
            return null;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Full availability check: AI enabled + API key set + MySQL configured.
    /// Used before calling the AI API (Generate).
    /// </summary>
    private bool IsAvailable(out DatabaseSettings db, out AiSettings ai)
    {
        var s = _settings.CurrentValue;
        db = s.Database;
        ai = s.Ai;
        return ai.Enabled
            && !string.IsNullOrWhiteSpace(ai.ApiKey)
            && db.Provider == DatabaseSettings.ProviderMySql
            && !string.IsNullOrWhiteSpace(db.MySqlConnectionString);
    }

    /// <summary>
    /// DB-only availability check: AI enabled + MySQL configured (no API key required).
    /// Used for HasSummaryAsync and GetSummaryAsync – the icon should appear even before
    /// the user has entered an API key, as long as data exists in the database.
    /// </summary>
    private bool IsDbAvailable(out DatabaseSettings db, out AiSettings ai)
    {
        var s = _settings.CurrentValue;
        db = s.Database;
        ai = s.Ai;
        return ai.Enabled
            && db.Provider == DatabaseSettings.ProviderMySql
            && !string.IsNullOrWhiteSpace(db.MySqlConnectionString);
    }

    private static async Task<DateTime?> GetLastQsoTimeInternalAsync(
        MySqlConnection conn, DatabaseSettings db, string callsignBase, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(
            $"""
            SELECT MAX(timestamp) FROM `{db.MySqlTableName}`
            WHERE (from_call = @cs OR from_call LIKE @csLike
                OR to_call   = @cs OR to_call   LIKE @csLike)
              AND is_position_beacon = 0
              AND is_telemetry       = 0
              AND text IS NOT NULL AND text != ''
            """, conn);
        cmd.Parameters.AddWithValue("@cs",     callsignBase);
        cmd.Parameters.AddWithValue("@csLike", callsignBase + "-%");
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToDateTime(result);
    }

    private static async Task<int> GetMessageCountInternalAsync(
        MySqlConnection conn, DatabaseSettings db, string callsignBase, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand(
            $"""
            SELECT COUNT(*) FROM `{db.MySqlTableName}`
            WHERE (from_call = @cs OR from_call LIKE @csLike
                OR to_call   = @cs OR to_call   LIKE @csLike)
              AND is_position_beacon = 0
              AND is_telemetry       = 0
              AND text IS NOT NULL AND text != ''
            """, conn);
        cmd.Parameters.AddWithValue("@cs",     callsignBase);
        cmd.Parameters.AddWithValue("@csLike", callsignBase + "-%");
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task<List<RawMessage>> LoadMessagesAsync(
        MySqlConnection conn, DatabaseSettings db, string callsignBase, AiSettings ai, CancellationToken ct)
    {
        // Build optional date filter based on SummaryDays (0 = no limit)
        var dateFilter = ai.SummaryDays > 0
            ? "AND timestamp >= @since"
            : string.Empty;

        await using var cmd = new MySqlCommand(
            $"""
            SELECT timestamp, from_call, to_call, text, is_outgoing
            FROM `{db.MySqlTableName}`
            WHERE (from_call = @cs OR from_call LIKE @csLike
                OR to_call   = @cs OR to_call   LIKE @csLike)
              AND is_position_beacon = 0
              AND is_telemetry       = 0
              AND text IS NOT NULL AND text != ''
              {dateFilter}
            ORDER BY timestamp DESC
            LIMIT @max
            """, conn);
        cmd.Parameters.AddWithValue("@cs",     callsignBase);
        cmd.Parameters.AddWithValue("@csLike", callsignBase + "-%");
        cmd.Parameters.AddWithValue("@max",    ai.MaxMessages);
        if (ai.SummaryDays > 0)
            cmd.Parameters.AddWithValue("@since", DateTime.UtcNow.AddDays(-ai.SummaryDays));

        var list = new List<RawMessage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new RawMessage(
                reader.GetDateTime(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4)));
        }

        // Reverse to chronological order for the prompt
        list.Reverse();
        return list;
    }

    private async Task<string?> CallOpenAiAsync(
        AiSettings ai, string callsignBase, List<RawMessage> messages, CancellationToken ct)
    {
        // Build conversation text for the prompt
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var dir = m.IsOutgoing ? "→" : "←";
            sb.AppendLine($"[{m.Timestamp:yyyy-MM-dd HH:mm}] {dir} {m.From} → {m.To}: {m.Text}");
        }

        var prompt = $"""
            The following are MeshCom radio QSO messages exchanged with station {callsignBase}.
            Please provide a concise summary organized by date (newest date first).
            For each day include: date, number of messages, and a brief summary of topics discussed.
            Also mention the date and time of the last QSO.
            Respond in the same language as the messages. If messages are mixed language, use German.

            Messages:
            {sb}
            """;

        var requestBody = JsonSerializer.Serialize(new
        {
            model    = ai.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant summarizing amateur radio QSO conversations." },
                new { role = "user",   content = prompt }
            },
            max_tokens  = 1000,
            temperature = 0.3
        });

        if (ai.LogRequests)
            _logger.LogInformation("QsoSummaryService: {Provider} request for {Callsign}:\n{Body}",
                ai.Provider, callsignBase, requestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, ai.GetApiUrl());

        // Azure OpenAI uses "api-key" header; OpenAI and Grok use "Authorization: Bearer"
        if (ai.Provider == AiSettings.ProviderAzureOpenAi)
            request.Headers.Add("api-key", ai.ApiKey);
        else
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ai.ApiKey);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("QsoSummaryService: {Provider} returned {Status}: {Body}",
                ai.Provider, response.StatusCode, responseBody);
            return null;
        }

        if (ai.LogRequests)
            _logger.LogInformation("QsoSummaryService: {Provider} response:\n{Body}", ai.Provider, responseBody);

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
    }

    /// <summary>Creates the <c>qso_summaries</c> table if it does not exist yet.</summary>
    private async Task EnsureSchemaAsync(MySqlConnection conn, DatabaseSettings db, CancellationToken ct)
    {
        if (_lastMigratedConnStr == db.MySqlConnectionString) return;

        await _migrateLock.WaitAsync(ct);
        try
        {
            if (_lastMigratedConnStr == db.MySqlConnectionString) return;

            await using var cmd = new MySqlCommand(
                $"""
                CREATE TABLE IF NOT EXISTS `{SummaryTable}` (
                    id             INT          AUTO_INCREMENT PRIMARY KEY,
                    callsign_base  VARCHAR(20)  NOT NULL,
                    summary_text   TEXT         NOT NULL,
                    last_qso_at    DATETIME     NOT NULL,
                    message_count  INT          NOT NULL,
                    created_at     DATETIME     NOT NULL,
                    UNIQUE KEY uk_callsign (callsign_base)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """, conn);
            await cmd.ExecuteNonQueryAsync(ct);

            _lastMigratedConnStr = db.MySqlConnectionString;
            _logger.LogInformation("QsoSummaryService: table '{Table}' ensured", SummaryTable);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QsoSummaryService: EnsureSchemaAsync failed");
        }
        finally
        {
            _migrateLock.Release();
        }
    }

    // ── Internal record types ─────────────────────────────────────────────

    private sealed record RawMessage(DateTime Timestamp, string From, string To, string Text, bool IsOutgoing);
}

/// <summary>A stored QSO summary record.</summary>
public sealed record QsoSummaryRecord(
    string   CallsignBase,
    string   SummaryText,
    DateTime LastQsoAt,
    int      MessageCount,
    DateTime CreatedAt
);
