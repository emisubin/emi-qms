using System.Net;
using Microsoft.Extensions.Configuration;

namespace Emi.Qms.Api.Security;

public static class TrustedProxyConfiguration
{
    public static IReadOnlyList<IPAddress> ReadKnownProxies(IConfiguration configuration)
    {
        return Split(configuration["ReverseProxy:KnownProxies"])
            .Select(value => IPAddress.TryParse(value, out var address) ? address : null)
            .Where(address => address is not null)
            .Cast<IPAddress>()
            .ToList();
    }

    public static IReadOnlyList<IPNetwork> ReadKnownNetworks(IConfiguration configuration)
    {
        return Split(configuration["ReverseProxy:KnownNetworks"])
            .Select(value => IPNetwork.TryParse(value, out var network) ? network : (IPNetwork?)null)
            .Where(network => network.HasValue)
            .Select(network => network!.Value)
            .ToList();
    }

    public static bool IsValid(IConfiguration configuration)
    {
        var proxyValues = Split(configuration["ReverseProxy:KnownProxies"]);
        var networkValues = Split(configuration["ReverseProxy:KnownNetworks"]);

        if (proxyValues.Count + networkValues.Count == 0)
        {
            return false;
        }

        return proxyValues.All(value =>
                IPAddress.TryParse(value, out var address)
                && IsSafeAddress(address))
            && networkValues.All(value =>
                IPNetwork.TryParse(value, out var network)
                && IsSafeNetwork(network));
    }

    private static bool IsSafeAddress(IPAddress address)
    {
        return !address.Equals(IPAddress.Any)
            && !address.Equals(IPAddress.IPv6Any)
            && !IPAddress.IsLoopback(address);
    }

    private static bool IsSafeNetwork(IPNetwork network)
    {
        if (network.PrefixLength == 0)
        {
            return false;
        }

        var baseAddress = network.BaseAddress;
        if (baseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return !network.Contains(IPAddress.Any)
                && !network.Contains(IPAddress.Loopback);
        }

        if (baseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return !network.Contains(IPAddress.IPv6Any)
                && !network.Contains(IPAddress.IPv6Loopback);
        }

        return false;
    }

    private static IReadOnlyList<string> Split(string? value)
    {
        return value
            ?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];
    }
}
