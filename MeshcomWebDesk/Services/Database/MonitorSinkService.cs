using MeshcomWebDesk.Models;
using Microsoft.Extensions.Options;

namespace MeshcomWebDesk.Services.Database;

/// <summary>
/// Singleton IMonitorDataSink implementation that routes writes to the
/// currently configured backend (MySQL, InfluxDB 2, or nowhere).
/// Provider changes in settings take effect immediately without restart.
/// </summary>
public sealed class MonitorSinkService : IMonitorDataSink
{
    private readonly IOptionsMonitor<MeshcomSettings> _settings;
    private readonly MySqlMonitorSink                 _mysql;
    private readonly InfluxDbMonitorSink              _influx;

    public MonitorSinkService(
        IOptionsMonitor<MeshcomSettings> settings,
        MySqlMonitorSink                 mysql,
        InfluxDbMonitorSink              influx)
    {
        _settings = settings;
        _mysql    = mysql;
        _influx   = influx;
    }

    public Task WriteAsync(MeshcomMessage message, CancellationToken ct = default) =>
        _settings.CurrentValue.Database.Provider switch
        {
            DatabaseSettings.ProviderMySql    => _mysql.WriteAsync(message, ct),
            DatabaseSettings.ProviderInfluxDb => _influx.WriteAsync(message, ct),
            _                                  => Task.CompletedTask
        };
}
