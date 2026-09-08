using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed record IpfsPreparationResult(
    bool GatewayChanged,
    int OldGatewayPort = 0,
    int NewGatewayPort = 0,
    bool TemporaryDirectoryRemoved = false,
    bool KuboWasRunning = false);

public sealed class IpfsPreflightService
{
    private const int MaximumKuboLogBytes = 5 * 1024 * 1024;

    public async Task<IpfsPreparationResult> PrepareAsync(MinerConfig config, CancellationToken ct = default)
    {
        var paths = GetPaths(config);
        var temporaryRemoved = false;

        if (!File.Exists(paths.IpfsExecutable))
        {
            // Recent miners may download Kubo themselves. Probe the existing
            // repository (or Kubo's default API port) before deciding that the
            // daemon belongs to this launch; an already-running user daemon
            // must never be stopped by Keryx Control.
            var existingApiPort = 5001;
            if (File.Exists(paths.ConfigPath))
            {
                var existingRoot = await ReadConfigurationAsync(paths.ConfigPath, ct);
                var existingAddresses = existingRoot["Addresses"] as JsonObject;
                existingApiPort = ParseTcpPort(existingAddresses?["API"]?.GetValue<string>()) ?? existingApiPort;
            }
            var existingKuboWasRunning = await IsIpfsApiReadyAsync(existingApiPort, ct);
            if (!existingKuboWasRunning)
            {
                temporaryRemoved = await RepairTemporaryDirectoryAsync(config, ct);
                RotateKuboLogIfNeeded();
            }
            return new(false, TemporaryDirectoryRemoved: temporaryRemoved, KuboWasRunning: existingKuboWasRunning);
        }

        if (!File.Exists(paths.ConfigPath))
            await InitializeRepositoryAsync(paths.IpfsExecutable, paths.MinerFolder, paths.Repository, ct);

        if (!File.Exists(paths.ConfigPath))
            throw new InvalidOperationException("IPFS configuration was not created.");

        var root = await ReadConfigurationAsync(paths.ConfigPath, ct);
        var addresses = root["Addresses"] as JsonObject
            ?? throw new InvalidOperationException("IPFS configuration does not contain an Addresses section.");
        var apiPort = ParseTcpPort(addresses["API"]?.GetValue<string>()) ?? 5001;
        var kuboWasRunning = await IsIpfsApiReadyAsync(apiPort, ct);

        if (!kuboWasRunning)
        {
            if (!IsPortAvailable(apiPort))
                throw new InvalidOperationException($"IPFS API port {apiPort} is already in use by another service.");
            temporaryRemoved = await RepairTemporaryDirectoryAsync(config, ct);
            RotateKuboLogIfNeeded();
        }

        var gatewayPort = ParseTcpPort(addresses["Gateway"]?.GetValue<string>()) ?? 8080;
        if (kuboWasRunning || IsPortAvailable(gatewayPort))
            return new(false, TemporaryDirectoryRemoved: temporaryRemoved, KuboWasRunning: kuboWasRunning);

        var replacement = Enumerable.Range(8081, 19).FirstOrDefault(IsPortAvailable);
        if (replacement == 0)
            throw new InvalidOperationException("IPFS gateway: no free port was found between 8081 and 8099.");

        addresses["Gateway"] = $"/ip4/127.0.0.1/tcp/{replacement}";
        await WriteConfigurationSafelyAsync(paths.ConfigPath, root, ct);
        return new(true, gatewayPort, replacement, temporaryRemoved, kuboWasRunning);
    }

    public async Task<bool> RepairTemporaryDirectoryAsync(MinerConfig config, CancellationToken ct = default)
    {
        var paths = GetPaths(config);
        var blocks = Path.GetFullPath(Path.Combine(paths.Repository, "blocks"));
        var temporary = Path.GetFullPath(Path.Combine(blocks, ".temp"));
        var expected = Path.Combine(blocks.TrimEnd(Path.DirectorySeparatorChar), ".temp");
        if (!temporary.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsafe IPFS temporary path.");
        if (!Directory.Exists(temporary) && !File.Exists(temporary)) return false;

        Exception? lastError = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
                else if (File.Exists(temporary)) File.Delete(temporary);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                await Task.Delay(500, ct);
            }
        }

        throw new IOException($"Unable to remove the stale IPFS temporary directory '{temporary}'. {lastError?.Message}", lastError);
    }

    public async Task ShutdownAsync(MinerConfig config, CancellationToken ct = default)
    {
        var paths = GetPaths(config);
        if (!File.Exists(paths.ConfigPath)) return;
        var root = await ReadConfigurationAsync(paths.ConfigPath, ct);
        var addresses = root["Addresses"] as JsonObject;
        var apiPort = ParseTcpPort(addresses?["API"]?.GetValue<string>()) ?? 5001;
        if (!await IsIpfsApiReadyAsync(apiPort, ct)) return;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try { await client.PostAsync($"http://127.0.0.1:{apiPort}/api/v0/shutdown", null, ct); }
        catch (HttpRequestException) { }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!await IsIpfsApiReadyAsync(apiPort, ct)) return;
            await Task.Delay(300, ct);
        }
    }

    private static IpfsPaths GetPaths(MinerConfig config)
    {
        var minerExecutable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
        var minerFolder = Path.GetDirectoryName(minerExecutable) ?? AppContext.BaseDirectory;
        var repository = Path.GetFullPath(Path.Combine(minerFolder, ".ipfs"));
        return new(minerFolder, Path.Combine(minerFolder, "ipfs.exe"), repository, Path.Combine(repository, "config"));
    }

    private static async Task<JsonObject> ReadConfigurationAsync(string path, CancellationToken ct)
    {
        try
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(path, ct)) as JsonObject
                ?? throw new JsonException("The root value is not an object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"IPFS configuration is invalid: {ex.Message}", ex);
        }
    }

    private static async Task WriteConfigurationSafelyAsync(string path, JsonObject root, CancellationToken ct)
    {
        var backup = path + ".keryxcontrol.bak";
        var temporary = path + ".keryxcontrol.tmp";
        File.Copy(path, backup, true);
        await File.WriteAllTextAsync(temporary, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
        File.Move(temporary, path, true);
    }

    private static void RotateKuboLogIfNeeded()
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".keryx");
            var path = Path.Combine(folder, "kubo.log");
            if (!File.Exists(path) || new FileInfo(path).Length <= MaximumKuboLogBytes) return;
            File.Move(path, path + ".1", true);
        }
        catch { }
    }

    private static async Task InitializeRepositoryAsync(string executable, string workingDirectory, string repository, CancellationToken ct)
    {
        Directory.CreateDirectory(repository);
        var psi = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("init");
        psi.Environment["IPFS_PATH"] = repository;
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("IPFS repository initialization failed.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("IPFS repository initialization timed out.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"IPFS init failed: {(await error).Trim()} {(await output).Trim()}");
    }

    internal static int? ParseTcpPort(string? multiAddress)
    {
        if (string.IsNullOrWhiteSpace(multiAddress)) return null;
        var marker = "/tcp/";
        var position = multiAddress.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return position >= 0 && int.TryParse(multiAddress[(position + marker.Length)..], out var port) ? port : null;
    }

    private static bool IsPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Server.ExclusiveAddressUse = true;
            listener.Start();
            return true;
        }
        catch (SocketException) { return false; }
        finally { try { listener?.Stop(); } catch { } }
    }

    internal static async Task<bool> IsIpfsApiReadyAsync(int port, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var response = await client.PostAsync($"http://127.0.0.1:{port}/api/v0/version", null, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
        catch (HttpRequestException) { return false; }
    }

    private sealed record IpfsPaths(string MinerFolder, string IpfsExecutable, string Repository, string ConfigPath);
}
