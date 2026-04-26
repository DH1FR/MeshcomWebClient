using System.Net.Http.Json;
using System.Text.Json;
using MeshcomWebDesk.Helpers;

namespace MeshcomWebDesk.Services;

/// <summary>
/// Retrieves elevation profiles from the free OpenTopoData API (SRTM 90 m)
/// and performs Line-of-Sight calculations for coverage prediction.
/// No API key required. Rate limit: 1 request/second, 100 points/request.
/// </summary>
public sealed class ElevationService
{
    private const string ApiBase      = "https://api.opentopodata.org/v1/srtm90m";
    private const int    RadialCount  = 72;    // every 5 degrees
    private const int    ProfileSteps = 15;    // sample points per radial
    private const double MaxRangeKm   = 200.0; // max prediction radius
    private const double EarthRadiusKm = 6371.0;

    private readonly HttpClient            _http;
    private readonly ILogger<ElevationService> _logger;

    public ElevationService(ILogger<ElevationService> logger)
    {
        _logger = logger;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Computes a LOS-based coverage prediction polygon around <paramref name="ownLat"/>/<paramref name="ownLon"/>.
    /// Returns a list of (lat, lon) polygon vertices ordered by bearing.
    /// Returns empty list on failure.
    /// </summary>
    public async Task<List<double[]>> GetCoveragePolygonAsync(
        double ownLat, double ownLon,
        double antennaHeightM = 2.0,
        CancellationToken ct  = default)
    {
        try
        {
            var ownElevation = await GetSingleElevationAsync(ownLat, ownLon, ct);
            if (ownElevation is null)
            {
                _logger.LogWarning("ElevationService: failed to get own elevation");
                return [];
            }

            var totalAgl = ownElevation.Value + antennaHeightM;
            var polygon  = new List<double[]>(RadialCount);

            // Build all sample points for all radials in batches of 100
            var allPoints = new List<(int radial, int step, double lat, double lon)>();
            for (var r = 0; r < RadialCount; r++)
            {
                var bearing = r * (360.0 / RadialCount);
                for (var s = 1; s <= ProfileSteps; s++)
                {
                    var distKm = MaxRangeKm * s / ProfileSteps;
                    var (lat, lon) = DestinationPoint(ownLat, ownLon, bearing, distKm);
                    allPoints.Add((r, s, lat, lon));
                }
            }

            // Fetch elevations in batches of 100
            var elevations = await FetchElevationsBatchedAsync(allPoints, ct);

            // Per radial: find LOS horizon
            for (var r = 0; r < RadialCount; r++)
            {
                var bearing       = r * (360.0 / RadialCount);
                var maxHorizonDeg = double.MinValue;
                var reachKm       = MaxRangeKm; // default = full range if no obstruction

                for (var s = 1; s <= ProfileSteps; s++)
                {
                    var idx = r * ProfileSteps + (s - 1);
                    if (idx >= elevations.Count) break;

                    var distKm     = MaxRangeKm * s / ProfileSteps;
                    var elevation  = elevations[idx] ?? 0;
                    var earthBulge = (distKm * distKm) / (2.0 * EarthRadiusKm); // km
                    var effElev    = elevation - earthBulge * 1000;              // back to metres

                    var heightDiff  = effElev - totalAgl;
                    var horizonDeg  = Math.Atan2(heightDiff, distKm * 1000) * 180.0 / Math.PI;

                    if (horizonDeg > maxHorizonDeg)
                    {
                        maxHorizonDeg = horizonDeg;
                        // LOS blocked beyond this point
                        reachKm = distKm;
                    }
                }

                var (pLat, pLon) = DestinationPoint(ownLat, ownLon, bearing, reachKm);
                polygon.Add([pLat, pLon]);
            }

            return polygon;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ElevationService: GetCoveragePolygonAsync failed");
            return [];
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task<double?> GetSingleElevationAsync(double lat, double lon, CancellationToken ct)
    {
        try
        {
            var url  = $"{ApiBase}?locations={lat.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}";
            var resp = await _http.GetFromJsonAsync<OpenTopoResponse>(url, ct);
            return resp?.Results?.FirstOrDefault()?.Elevation;
        }
        catch { return null; }
    }

    private async Task<List<double?>> FetchElevationsBatchedAsync(
        List<(int radial, int step, double lat, double lon)> points,
        CancellationToken ct)
    {
        var result     = new List<double?>(points.Count);
        const int batchSize = 100;

        for (var i = 0; i < points.Count; i += batchSize)
        {
            if (ct.IsCancellationRequested) break;

            var batch = points.Skip(i).Take(batchSize).ToList();
            var locs  = string.Join("|", batch.Select(p =>
                $"{p.lat.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)},{p.lon.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}"));

            try
            {
                var url  = $"{ApiBase}?locations={locs}";
                var resp = await _http.GetFromJsonAsync<OpenTopoResponse>(url, ct);
                if (resp?.Results != null)
                    result.AddRange(resp.Results.Select(r => r.Elevation));
                else
                    result.AddRange(Enumerable.Repeat<double?>(null, batch.Count));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ElevationService: batch {I} failed", i / batchSize);
                result.AddRange(Enumerable.Repeat<double?>(null, batch.Count));
            }

            // Respect rate limit: 1 request/second
            if (i + batchSize < points.Count)
                await Task.Delay(1100, ct);
        }

        return result;
    }

    /// <summary>
    /// Calculates the destination point given a start, bearing (degrees) and distance (km).
    /// Uses the Haversine/spherical formula.
    /// </summary>
    private static (double lat, double lon) DestinationPoint(
        double lat, double lon, double bearingDeg, double distKm)
    {
        var R      = EarthRadiusKm;
        var d      = distKm / R;
        var b      = bearingDeg * Math.PI / 180.0;
        var lat1   = lat * Math.PI / 180.0;
        var lon1   = lon * Math.PI / 180.0;

        var lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(d)
                 + Math.Cos(lat1) * Math.Sin(d) * Math.Cos(b));
        var lon2 = lon1 + Math.Atan2(
            Math.Sin(b) * Math.Sin(d) * Math.Cos(lat1),
            Math.Cos(d) - Math.Sin(lat1) * Math.Sin(lat2));

        return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
    }

    // ── JSON models ───────────────────────────────────────────────────────

    private sealed class OpenTopoResponse
    {
        public List<ElevationResult>? Results { get; set; }
    }

    private sealed class ElevationResult
    {
        public double? Elevation { get; set; }
    }
}
