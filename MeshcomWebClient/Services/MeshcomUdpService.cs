using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MeshcomWebClient.Models;

namespace MeshcomWebClient.Services;

/// <summary>
/// Background service that handles UDP communication with a MeshCom device.
/// Listens for incoming messages and provides a method to send messages.
/// 
/// MeshCom EXTUDP JSON format:
///   RX: {"src_type":"lora","type":"msg","src":"DH1FR-1","dst":"DH1FR-2","msg":"Hello{034",...}
///   TX: {"type":"msg","dst":"DH1FR-1","msg":"Hello"}
/// </summary>
public partial class MeshcomUdpService : BackgroundService
{
    private readonly ILogger<MeshcomUdpService> _logger;
    private readonly ChatService _chatService;
    private readonly MeshcomSettings _settings;
    private UdpClient? _udpClient;

    /// <summary>Matches trailing MeshCom sequence markers like {034, {333 at end of message text.</summary>
    [GeneratedRegex(@"\{\d+$")]
    private static partial Regex TrailingSequencePattern();

    public MeshcomUdpService(
        ILogger<MeshcomUdpService> logger,
        IOptionsMonitor<MeshcomSettings> settings,
        ChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
        _settings = settings.CurrentValue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MeshCom UDP service starting – listening on {Ip}:{Port}, device at {DevIp}:{DevPort}",
            _settings.ListenIp, _settings.ListenPort, _settings.DeviceIp, _settings.DevicePort);

        try
        {
            var localEp = new IPEndPoint(IPAddress.Parse(_settings.ListenIp), _settings.ListenPort);
            _udpClient = new UdpClient(localEp);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to bind UDP socket on {Ip}:{Port}", _settings.ListenIp, _settings.ListenPort);
            return;
        }

        // Send registration packet so the device adds this client to its sender list.
        // Without this, the device does not know where to deliver UDP data.
        await RegisterWithDeviceAsync();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(stoppingToken);
                    var raw = Encoding.UTF8.GetString(result.Buffer).TrimEnd('\r', '\n');

                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    _logger.LogDebug("UDP RX [{Remote}]: {Data}", result.RemoteEndPoint, raw);
                    var message = ParseMessage(raw);

                    if (message != null)
                    {
                        // Skip node echoes of our own sent messages (already recorded as outgoing)
                        if (string.Equals(message.From, _settings.MyCallsign, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("Skipping node echo from {From}", message.From);
                            _chatService.AddRawMessage(message);
                        }
                        else
                        {
                            _chatService.AddIncomingMessage(message);
                        }
                    }
                    else
                    {
                        // Unparseable data (status, telemetry, etc.) – raw feed only, no tab
                        _chatService.AddRawMessage(new MeshcomMessage
                        {
                            Text = raw,
                            RawData = raw
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving UDP data");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            _udpClient.Dispose();
            _udpClient = null;
            _logger.LogInformation("MeshCom UDP service stopped");
        }
    }

    /// <summary>
    /// Send a registration packet to the MeshCom device so it adds this client
    /// to its UDP sender list and starts delivering data.
    /// </summary>
    private async Task RegisterWithDeviceAsync()
    {
        if (_udpClient == null) return;

        try
        {
            var json = JsonSerializer.Serialize(new { type = "info", src = _settings.MyCallsign, dst = "*", msg = "info" });
            var bytes = Encoding.UTF8.GetBytes(json);
            var remoteEp = new IPEndPoint(IPAddress.Parse(_settings.DeviceIp), _settings.DevicePort);

            await _udpClient.SendAsync(bytes, bytes.Length, remoteEp);
            _logger.LogInformation("UDP registration packet sent to {DeviceIp}:{DevicePort}", _settings.DeviceIp, _settings.DevicePort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send UDP registration packet to {DeviceIp}:{DevicePort}", _settings.DeviceIp, _settings.DevicePort);
        }
    }

    /// <summary>
    /// Send a text message to the MeshCom device via UDP.
    /// </summary>
    public async Task SendMessageAsync(string destination, string text)
    {
        if (_udpClient == null)
        {
            _logger.LogWarning("Cannot send – UDP client not initialized");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(new { type = "msg", dst = destination, msg = text });
            var bytes = Encoding.UTF8.GetBytes(json);
            var remoteEp = new IPEndPoint(IPAddress.Parse(_settings.DeviceIp), _settings.DevicePort);

            await _udpClient.SendAsync(bytes, bytes.Length, remoteEp);
            _logger.LogDebug("UDP TX [{Remote}]: {Data}", remoteEp, json);

            _chatService.AddOutgoingMessage(new MeshcomMessage
            {
                From = _settings.MyCallsign,
                To = destination,
                Text = text,
                IsOutgoing = true,
                RawData = json
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending UDP data to {DeviceIp}:{DevicePort}", _settings.DeviceIp, _settings.DevicePort);
        }
    }

    private MeshcomMessage? ParseMessage(string raw)
    {
        if (!raw.StartsWith('{'))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Only handle messages of type "msg" with required fields
            if (!root.TryGetProperty("type", out var typeProp) ||
                typeProp.GetString() != "msg" ||
                !root.TryGetProperty("src", out var srcProp) ||
                !root.TryGetProperty("dst", out var dstProp) ||
                !root.TryGetProperty("msg", out var msgProp))
            {
                return null;
            }

            var src = srcProp.GetString() ?? string.Empty;
            var dst = dstProp.GetString() ?? string.Empty;
            var msg = msgProp.GetString() ?? string.Empty;

            // For relayed messages ("OE1XAR-62,DB0TAW-13,..."), use the first callsign as sender
            var commaIdx = src.IndexOf(',');
            var sender = commaIdx >= 0 ? src[..commaIdx] : src;

            // Strip trailing sequence marker like {034, {333
            msg = TrailingSequencePattern().Replace(msg, string.Empty);

            return new MeshcomMessage
            {
                From = sender,
                To = dst,
                Text = msg,
                IsOutgoing = false,
                RawData = raw
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON message: {Data}", raw);
            return null;
        }
    }
}
