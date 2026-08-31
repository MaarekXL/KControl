using System.IO;
using System.Text.Json;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class SettingsService
{
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeryxControl");
    private static string PathName => Path.Combine(Folder, "settings.json");

    public async Task<UserSettings> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(PathName)) return new();
            await using var stream = File.OpenRead(PathName);
            return await JsonSerializer.DeserializeAsync<UserSettings>(stream, _json, ct) ?? new();
        }
        catch { return new(); }
    }

    public async Task SaveAsync(UserSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Folder);
        var temporary = Path.Combine(Folder, "settings.tmp");
        await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, settings, _json, ct);
        File.Move(temporary, PathName, true);
    }
}
