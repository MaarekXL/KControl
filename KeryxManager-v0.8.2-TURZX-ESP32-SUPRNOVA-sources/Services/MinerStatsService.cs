using System.Net.Http;
using System.Text.Json;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class MinerStatsService : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(1.5) };
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<MinerApiStats?> TryGetAsync(string address, int port, CancellationToken ct = default)
    {
        try
        {
            await using var stream = await _http.GetStreamAsync($"http://{address}:{port}/stats", ct);
            return await JsonSerializer.DeserializeAsync<MinerApiStats>(stream, _json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
