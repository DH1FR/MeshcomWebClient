using MeshcomWebDesk.Models;

namespace MeshcomWebDesk.Services.Database;

public interface IMonitorDataSink
{
    /// <summary>
    /// Persist a single monitor message asynchronously.
    /// Implementations must not throw – errors are logged and swallowed.
    /// </summary>
    Task WriteAsync(MeshcomMessage message, CancellationToken ct = default);
}
