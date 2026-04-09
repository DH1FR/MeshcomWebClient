using MySqlConnector;
using MeshcomWebDesk.Models;
using Microsoft.Extensions.Options;

namespace MeshcomWebDesk.Services.Database;

public sealed class MySqlMonitorSink
{
    private readonly IOptionsMonitor<MeshcomSettings> _settings;
    private readonly ILogger<MySqlMonitorSink>        _logger;

    public MySqlMonitorSink(IOptionsMonitor<MeshcomSettings> settings, ILogger<MySqlMonitorSink> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    public async Task WriteAsync(MeshcomMessage msg, CancellationToken ct = default)
    {
        var db = _settings.CurrentValue.Database;
        try
        {
            await using var conn = new MySqlConnection(db.MySqlConnectionString);
            await conn.OpenAsync(ct);

            var sql = $"""
                INSERT INTO `{db.MySqlTableName}`
                    (timestamp, from_call, to_call, text, rssi, snr,
                     latitude, longitude, altitude, relay_path, msg_id,
                     src_type, battery, firmware, is_outgoing, is_position_beacon, is_telemetry)
                VALUES
                    (@ts, @from, @to, @text, @rssi, @snr,
                     @lat, @lon, @alt, @relay, @msgId,
                     @srcType, @batt, @fw, @out, @pos, @tele)
                """;

            await using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ts",      msg.Timestamp);
            cmd.Parameters.AddWithValue("@from",    msg.From);
            cmd.Parameters.AddWithValue("@to",      msg.To);
            cmd.Parameters.AddWithValue("@text",    msg.Text);
            cmd.Parameters.AddWithValue("@rssi",    (object?)msg.Rssi      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@snr",     (object?)msg.Snr       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lat",     (object?)msg.Latitude  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lon",     (object?)msg.Longitude ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@alt",     (object?)msg.Altitude  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@relay",   (object?)msg.RelayPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@msgId",   (object?)msg.MsgId     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@srcType", (object?)msg.SrcType   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@batt",    (object?)msg.Battery   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fw",      (object?)msg.Firmware  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@out",     msg.IsOutgoing);
            cmd.Parameters.AddWithValue("@pos",     msg.IsPositionBeacon);
            cmd.Parameters.AddWithValue("@tele",    msg.IsTelemetry);

            await cmd.ExecuteNonQueryAsync(ct);

            if (db.LogInserts)
            {
                var logSql = $"""
                    INSERT INTO `{db.MySqlTableName}`
                        (timestamp, from_call, to_call, text, rssi, snr,
                         latitude, longitude, altitude, relay_path, msg_id,
                         src_type, battery, firmware, is_outgoing, is_position_beacon, is_telemetry)
                    VALUES
                        ('{msg.Timestamp:yyyy-MM-dd HH:mm:ss.fff}', '{msg.From}', '{msg.To}', '{msg.Text.Replace("'", "''")}',
                         {N(msg.Rssi)}, {N(msg.Snr)},
                         {N(msg.Latitude)}, {N(msg.Longitude)}, {N(msg.Altitude)},
                         {S(msg.RelayPath)}, {S(msg.MsgId)},
                         {S(msg.SrcType)}, {N(msg.Battery)}, {S(msg.Firmware)},
                         {B(msg.IsOutgoing)}, {B(msg.IsPositionBeacon)}, {B(msg.IsTelemetry)})
                    """;
                _logger.LogInformation("DB INSERT:\n{Sql}", logSql);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MySQL: Fehler beim Schreiben der Monitor-Daten");
        }
    }

    // ── Log-Hilfsmethoden (nur für die lesbare SQL-Darstellung) ───────────
    private static string N(object? v)  => v is null ? "NULL" : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!;
    private static string S(string? v)  => v is null ? "NULL" : $"'{v.Replace("'", "''")}'";
    private static string B(bool v)     => v ? "1" : "0";
}
