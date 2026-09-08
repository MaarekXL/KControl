using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Channels;
using KeryxControl.Models;

namespace KeryxControl.Services;

public sealed class Esp32TelemetryService : IAsyncDisposable
{
    private static readonly TimeSpan DestinationRefreshInterval = TimeSpan.FromSeconds(30);
    private readonly Channel<Esp32DisplaySnapshot> _pending = Channel.CreateBounded<Esp32DisplaySnapshot>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _worker;
    private long _sequence;

    public Esp32TelemetryService() => _worker = RunAsync(_lifetime.Token);

    public void Publish(Esp32DisplaySnapshot snapshot) => _pending.Writer.TryWrite(snapshot);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
        IReadOnlyList<IPEndPoint> destinations = [];
        var refreshAt = DateTime.MinValue;

        try
        {
            while (await _pending.Reader.WaitToReadAsync(cancellationToken))
            {
                Esp32DisplaySnapshot? latest = null;
                while (_pending.Reader.TryRead(out var candidate)) latest = candidate;
                if (latest is null) continue;

                if (DateTime.UtcNow >= refreshAt)
                {
                    destinations = FindBroadcastDestinations();
                    refreshAt = DateTime.UtcNow + DestinationRefreshInterval;
                }

                var packet = Esp32TelemetryProtocol.Encode(Interlocked.Increment(ref _sequence), latest);
                foreach (var destination in destinations)
                {
                    try { await udp.SendAsync(packet, destination, cancellationToken); }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static IReadOnlyList<IPEndPoint> FindBroadcastDestinations()
    {
        var addresses = new HashSet<IPAddress> { IPAddress.Broadcast };

        try
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up
                    || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                        || unicast.IPv4Mask is null
                        || IPAddress.IsLoopback(unicast.Address))
                        continue;

                    var addressBytes = unicast.Address.GetAddressBytes();
                    var maskBytes = unicast.IPv4Mask.GetAddressBytes();
                    var broadcastBytes = new byte[4];
                    for (var i = 0; i < broadcastBytes.Length; i++)
                        broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
                    addresses.Add(new IPAddress(broadcastBytes));
                }
            }
        }
        catch (NetworkInformationException) { }
        catch (SocketException) { }

        return addresses.Select(address => new IPEndPoint(address, Esp32TelemetryProtocol.Port)).ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        _pending.Writer.TryComplete();
        _lifetime.Cancel();
        try { await _worker; } catch (OperationCanceledException) { }
        _lifetime.Dispose();
    }
}
