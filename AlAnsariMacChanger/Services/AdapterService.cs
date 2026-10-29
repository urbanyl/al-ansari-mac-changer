using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using AlAnsariMacChanger.Models;
using Microsoft.Win32;

namespace AlAnsariMacChanger.Services;

public sealed class AdapterService
{
    private const string ClassRoot =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private readonly OriginalMacStore _store = new();
    private readonly BackupStore _backup;

    public AdapterService(BackupStore backup) => _backup = backup;

    public List<NetworkAdapterInfo> LoadAdapters(bool includeVirtual)
    {
        var result = new List<NetworkAdapterInfo>();

        var nics = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                        and not NetworkInterfaceType.Tunnel)
            .GroupBy(n => NormalizeGuid(n.Id), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var query = includeVirtual
            ? "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL"
            : "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE AND NetConnectionID IS NOT NULL";

        using var searcher = new ManagementObjectSearcher(query);
        using var collection = searcher.Get();

        foreach (ManagementBaseObject item in collection)
        {
            try
            {
                var guid = NormalizeGuid(item["GUID"] as string);
                var connectionName = item["NetConnectionID"] as string;
                var pnpId = item["PNPDeviceID"] as string;

                if (string.IsNullOrWhiteSpace(guid) ||
                    string.IsNullOrWhiteSpace(connectionName) ||
                    string.IsNullOrWhiteSpace(pnpId))
                    continue;

                if (pnpId.StartsWith("ROOT\\", StringComparison.OrdinalIgnoreCase) ||
                    pnpId.StartsWith("SWD\\", StringComparison.OrdinalIgnoreCase))
                    continue;

                var configKey = FindConfigKey(guid);
                if (configKey is null) continue;

                var overrideMac = NormalizeHex(ReadRegistryString(configKey, "NetworkAddress"));
                var permanentMac = NormalizeHex(ReadRegistryString(configKey, "PermanentAddress"));
                var hardwareMac = NormalizeHex(item["MACAddress"] as string);

                var activeHex = overrideMac ?? hardwareMac;
                if (activeHex is null) continue;

                var originalHex = _store.Get(guid)
                                  ?? permanentMac;

                if (originalHex is null && overrideMac is null && hardwareMac is not null)
                {
                    originalHex = hardwareMac;
                    _store.Set(guid, hardwareMac);
                }

                var isChanged = overrideMac is not null &&
                                (originalHex is null || !string.Equals(originalHex, activeHex, StringComparison.OrdinalIgnoreCase));

                var info = new NetworkAdapterInfo
                {
                    Guid = guid,
                    Name = connectionName,
                    Description = (item["Name"] as string) ?? connectionName,
                    HardwareId = pnpId,
                    DeviceId = item["DeviceID"]?.ToString() ?? string.Empty,
                    ConfigId = configKey,
                    InterfaceIndex = item["InterfaceIndex"] is uint index ? (int)index : 0,
                    MacAddress = FormatMac(activeHex),
                    OriginalMac = originalHex is null ? "Unknown" : FormatMac(originalHex),
                    IsChanged = isChanged
                };

                RefreshRuntime(info, nics.TryGetValue(guid, out var nic) ? nic : null);
                result.Add(info);
            }
            catch
            {
                // Skip adapters that refuse to expose their properties.
            }
        }

        _store.SaveIfDirty();

        return result
            .OrderByDescending(a => a.IsConnected)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void RefreshRuntime(NetworkAdapterInfo adapter)
    {
        NetworkInterface? nic = null;
        try
        {
            nic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => string.Equals(NormalizeGuid(n.Id), adapter.Guid, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            // Ignore enumeration failures.
        }

        RefreshRuntime(adapter, nic);
    }

    private static void RefreshRuntime(NetworkAdapterInfo adapter, NetworkInterface? nic)
    {
        if (nic is null)
        {
            adapter.IsConnected = false;
            adapter.LinkStatus = "Unavailable";
            adapter.Speed = 0;
            return;
        }

        try
        {
            adapter.Speed = (ulong)Math.Max(0, nic.Speed);
            adapter.IsConnected = nic.OperationalStatus == OperationalStatus.Up;
            adapter.LinkStatus = nic.OperationalStatus switch
            {
                OperationalStatus.Up => "Connected",
                OperationalStatus.Down => "Disconnected",
                OperationalStatus.Dormant => "Standby",
                OperationalStatus.NotPresent => "Absent",
                OperationalStatus.LowerLayerDown => "No link",
                _ => "Disconnected"
            };

            var stats = nic.GetIPv4Statistics();
            adapter.BytesReceived = stats.BytesReceived;
            adapter.BytesSent = stats.BytesSent;

            var props = nic.GetIPProperties();
            adapter.IPv4 = string.Join(", ", props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString()));

            adapter.IPv6 = string.Join(", ", props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 && !a.Address.IsIPv6LinkLocal)
                .Select(a => a.Address.ToString())
                .Take(2));
        }
        catch
        {
            // Some virtual drivers throw on stats queries.
        }
    }

    public (bool Ok, string Message) ApplyMac(NetworkAdapterInfo adapter, string mac, bool restart)
    {
        var hex = NormalizeHex(mac);
        if (hex is null) return (false, "Invalid MAC address: 6 hexadecimal octets expected.");
        if (!IsValidMac(hex, out var error)) return (false, error);

        try
        {
            using (var root = Registry.LocalMachine.OpenSubKey(ClassRoot, writable: true))
            {
                if (root is null) return (false, "Network registry key not found.");

                using var key = root.OpenSubKey(adapter.ConfigId, writable: true);
                if (key is null) return (false, "Network driver registry entry not found.");

                key.SetValue("NetworkAddress", hex, RegistryValueKind.String);
            }

            if (restart)
            {
                var (ok, message) = Restart(adapter);
                return ok
                    ? (true, $"MAC address of \"{adapter.Name}\" set to {FormatMac(hex)}.")
                    : (false, $"Address saved, but the adapter could not be restarted: {message}");
            }

            return (true,
                $"MAC address of \"{adapter.Name}\" set to {FormatMac(hex)}. " +
                "It will apply the next time the adapter reconnects.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Administrator rights are required to modify the registry.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) RestoreOriginal(NetworkAdapterInfo adapter, bool restart)
    {
        try
        {
            using (var root = Registry.LocalMachine.OpenSubKey(ClassRoot, writable: true))
            {
                if (root is null) return (false, "Network registry key not found.");

                using var key = root.OpenSubKey(adapter.ConfigId, writable: true);
                key?.DeleteValue("NetworkAddress", throwOnMissingValue: false);
            }

            if (restart)
            {
                var (ok, message) = Restart(adapter);
                return ok
                    ? (true, $"Original MAC address restored for \"{adapter.Name}\".")
                    : (false, $"Address restored, but the adapter could not be restarted: {message}");
            }

            return (true,
                $"Original MAC address restored for \"{adapter.Name}\". " +
                "It will apply the next time the adapter reconnects.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Administrator rights are required to modify the registry.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) Restart(NetworkAdapterInfo adapter)
    {
        var (disabled, disableMessage) = InvokeAdapterMethod(adapter, "Disable");
        if (!disabled)
        {
            var fallback = RunNetsh(adapter.Name, false);
            if (!fallback.Ok) return (false, disableMessage);
        }

        WaitForEnabled(adapter.Guid, false, 8000);
        Thread.Sleep(600);

        var (enabled, enableMessage) = EnableWithRetry(adapter);
        if (!enabled)
            return (false, "the adapter could not be re-enabled automatically. " + enableMessage);

        return (true, "Connection restarted.");
    }

    private static (bool Ok, string Message) EnableWithRetry(NetworkAdapterInfo adapter)
    {
        var lastMessage = "The driver refused to enable the adapter.";

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var (ok, message) = InvokeAdapterMethod(adapter, "Enable");
            if (!ok) message = RunNetsh(adapter.Name, true).Message;

            WaitForEnabled(adapter.Guid, true, 6000);

            if (IsEnabled(adapter.Guid))
                return (true, "OK");

            lastMessage = message;
            Thread.Sleep(900);
        }

        return (false,
            "Re-enable the adapter manually (Settings > Network, or Get-NetAdapter / Enable-NetAdapter). " +
            lastMessage);
    }

    private static bool IsEnabled(string guid)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT NetEnabled FROM Win32_NetworkAdapter WHERE GUID='{EscapeWql(guid)}'");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject item in collection)
            {
                if (item["NetEnabled"] is true) return true;
            }
        }
        catch
        {
            // Treat as not confirmed.
        }

        return false;
    }

    private static (bool Ok, string Message) InvokeAdapterMethod(NetworkAdapterInfo adapter, string method)
    {
        try
        {
            var deviceId = EscapeWql(adapter.DeviceId);
            using var managementObject = new ManagementObject($"Win32_NetworkAdapter.DeviceID='{deviceId}'");
            managementObject.Get();
            var result = managementObject.InvokeMethod(method, null);
            var code = result is null ? 0u : Convert.ToUInt32(result, CultureInfo.InvariantCulture);
            return code == 0
                ? (true, "OK")
                : (false, $"The driver refused the \"{method}\" operation (code {code}).");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool Ok, string Message) RunNetsh(string connectionName, bool enable)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"interface set interface name=\"{connectionName}\" admin={(enable ? "enabled" : "disabled")}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is null) return (false, "Unable to start netsh.");

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(15000);

            return process.ExitCode == 0
                ? (true, "OK")
                : (false, string.IsNullOrWhiteSpace(output) ? $"netsh failed (code {process.ExitCode})." : output.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void WaitForEnabled(string guid, bool enabled, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        var escaped = EscapeWql(guid);

        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT NetEnabled FROM Win32_NetworkAdapter WHERE GUID='{escaped}'");
                using var collection = searcher.Get();
                foreach (ManagementBaseObject item in collection)
                {
                    if ((item["NetEnabled"] as bool?) == enabled) return;
                }
            }
            catch
            {
                // Keep polling until the timeout.
            }

            Thread.Sleep(400);
        }
    }

    private static string? FindConfigKey(string guid)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ClassRoot);
            if (root is null) return null;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                if (subKeyName.Length != 4 || !subKeyName.All(char.IsDigit)) continue;

                using var subKey = root.OpenSubKey(subKeyName);
                var instanceId = subKey?.GetValue("NetCfgInstanceId") as string;
                if (string.Equals(NormalizeGuid(instanceId), guid, StringComparison.OrdinalIgnoreCase))
                    return subKeyName;
            }
        }
        catch
        {
            // Ignore registry failures.
        }

        return null;
    }

    private static string? ReadRegistryString(string configKey, string valueName)
    {
        try
        {
            using var root = Registry.LocalMachine.OpenSubKey(ClassRoot);
            using var key = root?.OpenSubKey(configKey);
            return key?.GetValue(valueName) as string;
        }
        catch
        {
            return null;
        }
    }

    public static string RandomMac()
    {
        var bytes = new byte[6];
        Random.Shared.NextBytes(bytes);
        bytes[0] = (byte)((bytes[0] & 0xFC) | 0x02);
        return Convert.ToHexString(bytes);
    }

    public static string ForceLocalAdmin(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        bytes[0] = (byte)((bytes[0] & 0xFC) | 0x02);
        return Convert.ToHexString(bytes);
    }

    public static string RandomizeWithPrefix(string prefixHex)
    {
        var random = Convert.FromHexString(RandomMac());
        var prefix = Convert.FromHexString(prefixHex);
        random[0] = prefix[0];
        random[1] = prefix[1];
        random[2] = prefix[2];
        return Convert.ToHexString(random);
    }

    public static string GenerateMac(MacStyle style, string? vendorPrefix)
    {
        var bytes = new byte[6];
        Random.Shared.NextBytes(bytes);

        switch (style)
        {
            case MacStyle.LocallyAdministered:
                bytes[0] = (byte)((bytes[0] & 0xFC) | 0x02);
                break;

            case MacStyle.GlobalUnicast:
                bytes[0] = (byte)(bytes[0] & 0xFC);
                if (bytes[0] == 0x00) bytes[0] = 0x02;
                break;

            case MacStyle.Vendor:
                var prefix = Convert.FromHexString(vendorPrefix is { Length: 6 } ? vendorPrefix : "001B63");
                bytes[0] = prefix[0];
                bytes[1] = prefix[1];
                bytes[2] = prefix[2];
                bytes[3] = (byte)(bytes[3] & 0x7F);
                break;
        }

        return Convert.ToHexString(bytes);
    }

    public static List<string> GenerateCandidates(MacStyle style, string? vendorPrefix, int count)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var attempts = 0;

        while (result.Count < count && attempts++ < count * 25)
            result.Add(FormatMac(GenerateMac(style, vendorPrefix)));

        return result.ToList();
    }

    public string[] GetDnsServers(NetworkAdapterInfo adapter)
    {
        try
        {
            using var configuration = GetConfiguration(adapter);
            return configuration?["DNSServerSearchOrder"] as string[] ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public (bool Ok, string Message) SetDnsServers(NetworkAdapterInfo adapter, string[] servers)
    {
        try
        {
            _backup.RememberDns(adapter.Guid, GetDnsServers(adapter));

            using var configuration = GetConfiguration(adapter);
            if (configuration is null) return (false, "Network configuration not found for this adapter.");

            var result = configuration.InvokeMethod("SetDNSServerSearchOrder", new object?[] { servers });
            var code = result is null ? 0u : Convert.ToUInt32(result, CultureInfo.InvariantCulture);
            if (code is not (0 or 1))
                return (false, $"Windows refused the DNS servers (code {code}).");

            _backup.Log($"DNS {adapter.Name}: {string.Join(", ", servers)}");
            return (true, $"DNS for \"{adapter.Name}\" set to {string.Join(", ", servers)}.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) RestoreDns(NetworkAdapterInfo adapter)
    {
        try
        {
            if (!_backup.HasDns(adapter.Guid))
                return (false, "No DNS backup exists for this adapter.");

            var original = _backup.GetDns(adapter.Guid);

            using var configuration = GetConfiguration(adapter);
            if (configuration is null) return (false, "Network configuration not found for this adapter.");

            var result = configuration.InvokeMethod(
                "SetDNSServerSearchOrder",
                new object?[] { original is { Length: > 0 } ? original : null });

            var code = result is null ? 0u : Convert.ToUInt32(result, CultureInfo.InvariantCulture);
            if (code is not (0 or 1))
                return (false, $"Windows refused the DNS restore (code {code}).");

            _backup.Log($"DNS {adapter.Name} restored");
            return (true, $"Original DNS restored for \"{adapter.Name}\".");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static ManagementObject? GetConfiguration(NetworkAdapterInfo adapter)
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT Index FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex={adapter.InterfaceIndex}");

        foreach (ManagementBaseObject found in searcher.Get())
        {
            var index = Convert.ToUInt32(found["Index"], CultureInfo.InvariantCulture);
            var configuration = new ManagementObject($"Win32_NetworkAdapterConfiguration.Index={index}");
            configuration.Get();
            return configuration;
        }

        return null;
    }

    private static string NormalizeGuid(string? guid) => guid?.Trim().Trim('{', '}').ToLowerInvariant() ?? string.Empty;

    private static string EscapeWql(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");

    public static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var buffer = new char[12];
        var count = 0;

        foreach (var c in value)
        {
            if (Uri.IsHexDigit(c))
            {
                if (count >= 12) return null;
                buffer[count++] = char.ToUpperInvariant(c);
            }
            else if (c is not ('-' or ':' or '.' or ' '))
            {
                return null;
            }
        }

        return count == 12 ? new string(buffer) : null;
    }

    public static string FormatMac(string hex)
    {
        if (hex.Length != 12) return hex;
        return string.Join("-", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }

    public static bool IsValidMac(string? hex, out string error)
    {
        error = string.Empty;

        if (hex is null || hex.Length != 12)
        {
            error = "Incomplete MAC address: 6 hexadecimal octets expected.";
            return false;
        }

        var bytes = Convert.FromHexString(hex);

        if (bytes.All(b => b == 0))
        {
            error = "The address 00-00-00-00-00-00 is not valid.";
            return false;
        }

        if ((bytes[0] & 0x01) != 0)
        {
            error = "A multicast MAC address cannot be assigned to a network adapter.";
            return false;
        }

        return true;
    }
}
