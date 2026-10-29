using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AlAnsariMacChanger.Services;

public sealed class IdentityService
{
    private const string TcpipParameters = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
    private const string CryptographyKey = @"SOFTWARE\Microsoft\Cryptography";
    private const string AdvertisingKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo";

    private readonly BackupStore _backup;

    public IdentityService(BackupStore backup) => _backup = backup;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetComputerNameEx(int nameType, string buffer);

    public string ComputerName => Environment.MachineName;

    public (string Domain, bool PartOfDomain) GetDomainInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Domain, PartOfDomain FROM Win32_ComputerSystem");
            using var collection = searcher.Get();
            foreach (ManagementBaseObject item in collection)
            {
                var domain = item["Domain"] as string ?? string.Empty;
                var partOfDomain = item["PartOfDomain"] as bool? ?? false;
                return (domain, partOfDomain);
            }
        }
        catch
        {
            // Ignore WMI failures.
        }

        return (string.Empty, false);
    }

    public string GetMachineGuid()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(CryptographyKey);
            return key?.GetValue("MachineGuid") as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public bool AdvertisingIdEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvertisingKey);
            return key?.GetValue("Enabled") is not int value || value != 0;
        }
        catch
        {
            return true;
        }
    }

    public (bool Randomize, bool Privacy) GetIpv6States()
    {
        var output = RunNetsh("interface ipv6 show global");
        var randomize = ParseState(output, "Randomize Identifiers", "Identifiants aléatoires");
        var privacy = ParseState(output, "Temporary Addresses", "Adresses temporaires");
        return (randomize ?? true, privacy ?? true);
    }

    private static bool? ParseState(string output, params string[] labels)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            foreach (var label in labels)
            {
                var index = rawLine.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (index < 0) continue;

                var rest = rawLine[(index + label.Length)..].Trim(' ', ':', '\t', '\r', '\n');
                if (rest.StartsWith("enabled", StringComparison.OrdinalIgnoreCase) ||
                    rest.StartsWith("activ", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (rest.StartsWith("disabled", StringComparison.OrdinalIgnoreCase) ||
                    rest.StartsWith("désactiv", StringComparison.OrdinalIgnoreCase) ||
                    rest.StartsWith("desactiv", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return null;
    }

    public (bool Ok, string Message) SetComputerName(string name)
    {
        if (!ValidateHostname(name, out var error)) return (false, error);

        _backup.Remember("ComputerName", ComputerName);

        var dnsOk = SetComputerNameEx(5, name);
        var netbiosOk = SetComputerNameEx(4, name);

        if (!dnsOk || !netbiosOk)
            return (false, $"Windows refused the computer name change (code {Marshal.GetLastWin32Error()}).");

        _backup.Log($"Computer name: {ComputerName} -> {name} (reboot required)");
        return (true, $"Computer name set to \"{name}\". A restart is required to apply it.");
    }

    public (bool Ok, string Message) SetWorkgroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "The workgroup name is empty.");
        if (name.Length > 15) return (false, "The workgroup name must not exceed 15 characters.");
        if (name.Any(c => char.IsWhiteSpace(c) || c is '<' or '>' or '\\' or '/' or ':' or '"' or '|' or '?' or '*'))
            return (false, "The workgroup name contains invalid characters.");

        var (domain, partOfDomain) = GetDomainInfo();
        if (partOfDomain)
            return (false, "This computer is joined to a domain: the change is refused to protect authentication.");

        try
        {
            _backup.Remember("Workgroup", domain);

            using var key = Registry.LocalMachine.OpenSubKey(TcpipParameters, writable: true);
            if (key is null) return (false, "Network registry key not found.");

            key.SetValue("Workgroup", name, RegistryValueKind.String);
            _backup.Log($"Workgroup: {domain} -> {name} (reboot required)");
            return (true, $"Workgroup set to \"{name}\". A restart is required.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Administrator rights are required.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) SetMachineGuid(string guid)
    {
        if (!Guid.TryParse(guid, out var parsed))
            return (false, "Invalid machine identifier (GUID expected).");

        try
        {
            _backup.Remember("MachineGuid", GetMachineGuid());

            using var key = Registry.LocalMachine.OpenSubKey(CryptographyKey, writable: true);
            if (key is null) return (false, "Cryptography registry key not found.");

            key.SetValue("MachineGuid", parsed.ToString("D"), RegistryValueKind.String);
            _backup.Log($"MachineGuid replaced with {parsed:D}");
            return (true, "MachineGuid (machine identifier) replaced.");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Administrator rights are required.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) SetAdvertisingId(bool enabled)
    {
        try
        {
            _backup.Remember("AdvertisingId", AdvertisingIdEnabled().ToString());

            using var key = Registry.CurrentUser.CreateSubKey(AdvertisingKey);
            key?.SetValue("Enabled", enabled ? 1 : 0, RegistryValueKind.DWord);
            _backup.Log($"Advertising ID: {(enabled ? "enabled" : "disabled")}");
            return (true, enabled ? "Advertising ID re-enabled." : "Advertising ID disabled.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) SetIpv6Privacy(bool enabled)
    {
        try
        {
            var (randomize, privacy) = GetIpv6States();
            _backup.Remember("Ipv6Randomize", randomize.ToString());
            _backup.Remember("Ipv6Privacy", privacy.ToString());

            var state = enabled ? "enabled" : "disabled";
            RunNetsh($"interface ipv6 set global randomizeidentifiers={state} store=persistent");
            RunNetsh($"interface ipv6 set privacy state={state} store=persistent");
            _backup.Log($"IPv6 privacy (temporary addresses): {(enabled ? "enabled" : "disabled")}");

            return (true, enabled
                ? "IPv6 temporary addresses enabled (randomized identifiers)."
                : "IPv6 temporary addresses disabled.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Ok, string Message) RestoreIdentity()
    {
        var messages = new List<string>();

        if (_backup.GetValue("ComputerName") is { Length: > 0 } originalName &&
            !string.Equals(originalName, ComputerName, StringComparison.OrdinalIgnoreCase))
        {
            var (ok, message) = SetComputerName(originalName);
            messages.Add(ok ? $"Computer name restored ({originalName}), restart required." : message);
        }

        if (_backup.GetValue("Workgroup") is { Length: > 0 } originalWorkgroup)
        {
            var (domain, partOfDomain) = GetDomainInfo();
            if (!partOfDomain && !string.Equals(domain, originalWorkgroup, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(TcpipParameters, writable: true);
                    key?.SetValue("Workgroup", originalWorkgroup, RegistryValueKind.String);
                    messages.Add($"Workgroup restored ({originalWorkgroup}), restart required.");
                }
                catch (Exception ex)
                {
                    messages.Add("Workgroup: " + ex.Message);
                }
            }
        }

        if (_backup.GetValue("MachineGuid") is { Length: > 0 } originalGuid &&
            !string.Equals(originalGuid, GetMachineGuid(), StringComparison.OrdinalIgnoreCase))
        {
            var (ok, message) = SetMachineGuid(originalGuid);
            messages.Add(ok ? "Original MachineGuid restored." : message);
        }

        if (_backup.GetValue("AdvertisingId") is { Length: > 0 } originalAdvertising &&
            bool.TryParse(originalAdvertising, out var advertisingEnabled))
        {
            var (ok, message) = SetAdvertisingId(advertisingEnabled);
            messages.Add(ok ? "Advertising ID restored." : message);
        }

        if (bool.TryParse(_backup.GetValue("Ipv6Privacy"), out var privacy) ||
            bool.TryParse(_backup.GetValue("Ipv6Randomize"), out privacy))
        {
            var (ok, message) = SetIpv6Privacy(privacy);
            messages.Add(ok ? "IPv6 privacy restored." : message);
        }

        if (messages.Count == 0) return (true, "No saved identity to restore.");

        _backup.Log("Identity restore: " + string.Join(" | ", messages));
        return (true, string.Join(" ", messages));
    }

    public static bool ValidateHostname(string name, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "The computer name is empty.";
            return false;
        }

        if (name.Length > 15)
        {
            error = "The computer name must not exceed 15 characters (NetBIOS limit).";
            return false;
        }

        if (!Regex.IsMatch(name, "^[A-Za-z0-9-]+$"))
        {
            error = "Letters, digits and hyphens only.";
            return false;
        }

        if (name.All(char.IsDigit))
        {
            error = "The name cannot be purely numeric.";
            return false;
        }

        return true;
    }

    private static string RunNetsh(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(psi);
            if (process is null) return string.Empty;

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
