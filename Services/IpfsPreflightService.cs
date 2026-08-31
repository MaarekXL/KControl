using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed record IpfsPreparationResult(bool GatewayChanged, int OldGatewayPort = 0, int NewGatewayPort = 0);

public sealed class IpfsPreflightService
{
    public async Task<IpfsPreparationResult> PrepareAsync(MinerConfig config, CancellationToken ct = default)
    {
        var minerExecutable = Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Executable), AppContext.BaseDirectory);
        var minerFolder = Path.GetDirectoryName(minerExecutable) ?? AppContext.BaseDirectory;
        var ipfsExecutable = Path.Combine(minerFolder, "ipfs.exe");
        var repository = Path.Combine(minerFolder, ".ipfs");
        var configPath = Path.Combine(repository, "config");
        if (!File.Exists(ipfsExecutable)) return new(false);

        if (!File.Exists(configPath) && !IsPortAvailable(8080))
            await InitializeRepositoryAsync(ipfsExecutable, minerFolder, repository, ct);

        if (!File.Exists(configPath)) return new(false);
        var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath, ct)) as JsonObject;
        var addresses = root?["Addresses"] as JsonObject;
        if (root is null || addresses is null) return new(false);

        var apiPort = ParseTcpPort(addresses["API"]?.GetValue<string>()) ?? 5001;
        if (!IsPortAvailable(apiPort))
            throw new InvalidOperationException($"IPFS API port {apiPort} is already in use.");

        var gatewayPort = ParseTcpPort(addresses["Gateway"]?.GetValue<string>()) ?? 8080;
        if (IsPortAvailable(gatewayPort)) return new(false);
        var replacement = Enumerable.Range(8081, 19).FirstOrDefault(IsPortAvailable);
        if (replacement == 0) throw new InvalidOperationException("IPFS gateway: no free port was found between 8081 and 8099.");

        addresses["Gateway"] = $"/ip4/127.0.0.1/tcp/{replacement}";
        var temporary = configPath + ".keryxcontrol.tmp";
        await File.WriteAllTextAsync(temporary, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
        File.Move(temporary, configPath, true);
        return new(true, gatewayPort, replacement);
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
        if (process.ExitCode != 0) throw new InvalidOperationException($"IPFS init failed: {(await error).Trim()} {(await output).Trim()}");
    }

    private static int? ParseTcpPort(string? multiAddress)
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
}
