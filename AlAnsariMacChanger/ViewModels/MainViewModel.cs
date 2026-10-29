using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using AlAnsariMacChanger.Infrastructure;
using AlAnsariMacChanger.Models;
using AlAnsariMacChanger.Services;
using MessageBox = System.Windows.MessageBox;

namespace AlAnsariMacChanger.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly BackupStore _backup = new();
    private readonly AdapterService _service;
    private readonly IdentityService _identity;
    private readonly DispatcherTimer _statsTimer;

    private NetworkAdapterInfo? _selectedAdapter;
    private PresetItem? _selectedPreset;
    private string _statusMessage = "Ready.";
    private bool _statusIsError;
    private bool _autoRestart;
    private bool _useZeroTwo;
    private bool _showVirtualAdapters;
    private bool _presetsReady;
    private bool _isInitialized;

    private string _octet1 = string.Empty;
    private string _octet2 = string.Empty;
    private string _octet3 = string.Empty;
    private string _octet4 = string.Empty;
    private string _octet5 = string.Empty;
    private string _octet6 = string.Empty;

    private MacTypeOption? _selectedMacType;
    private PresetItem? _selectedVendor;
    private int _candidateCount = 10;
    private string? _selectedCandidate;
    private bool _isGenerating;

    private IdentityProfile? _selectedIdentityProfile;
    private string _previewText = "Generate a preview to inspect the new identity.";

    private string _computerNameCurrent = string.Empty;
    private string _newComputerName = string.Empty;
    private HostnameStyle? _selectedHostnameStyle;
    private string _workgroupCurrent = string.Empty;
    private string _newWorkgroup = "WORKGROUP";
    private bool _isDomainJoined;
    private string _machineGuidCurrent = string.Empty;
    private string _newMachineGuid = string.Empty;
    private bool _ipv6PrivacyEnabled = true;
    private bool _advertisingIdEnabled = true;
    private string _rebootRequiredMessage = string.Empty;

    private bool _includeMac = true;
    private bool _includeHostname = true;
    private bool _includeWorkgroup;
    private bool _includeMachineGuid = true;
    private bool _includeDns;
    private bool _includeIpv6 = true;
    private bool _includeAdvertising = true;

    private DnsPresetItem? _selectedDnsPreset;
    private bool _applyDnsToAll = true;
    private string _currentDnsText = "—";

    private string _backupSummary = string.Empty;
    private string _logText = string.Empty;

    public MainViewModel()
    {
        _service = new AdapterService(_backup);
        _identity = new IdentityService(_backup);

        Presets = new ObservableCollection<PresetItem>
        {
            new("Random address", null),
            new("Apple", "001B63"),
            new("Intel", "3C970E"),
            new("Samsung", "342387"),
            new("Cisco", "001AA1"),
            new("Google", "F4F5D8"),
            new("VMware", "005056")
        };

        Vendors = new ObservableCollection<PresetItem>
        {
            new("No prefix (random)", null),
            new("Apple", "001B63"),
            new("Intel", "3C970E"),
            new("Samsung", "342387"),
            new("Cisco", "001AA1"),
            new("Dell", "001422"),
            new("HP", "001B78"),
            new("Lenovo", "00215D"),
            new("Asus", "002618"),
            new("Acer", "001EEC"),
            new("Microsoft", "0017FA"),
            new("Broadcom", "001018"),
            new("Realtek", "00E04C"),
            new("Google", "F4F5D8"),
            new("VMware", "005056"),
            new("VirtualBox", "080027"),
            new("TP-Link", "50C7BF"),
            new("Xiaomi", "64CC2E"),
            new("Huawei", "001E10"),
            new("Netgear", "A00460"),
            new("Ubiquiti", "0418D6"),
            new("Raspberry Pi", "B827EB"),
            new("Sony", "0013A9"),
            new("Toshiba", "002622"),
            new("LG", "001E75"),
            new("Motorola", "001A1B"),
            new("Nokia", "001F5C"),
            new("Qualcomm", "000E32"),
            new("Foxconn", "0026B9"),
            new("Gigabyte", "001FD0"),
            new("ASRock", "002522"),
            new("MSI", "00242C"),
            new("Supermicro", "002590"),
            new("Amazon", "44650D"),
            new("Espressif", "240AC4"),
            new("Nordic", "0011D2"),
            new("Zyxel", "001349"),
            new("D-Link", "001CF0"),
            new("Aruba", "000B86"),
            new("MikroTik", "4C5E0C")
        };

        MacTypes = new ObservableCollection<MacTypeOption>
        {
            new("Locally administered (02)", MacStyle.LocallyAdministered),
            new("Universally administered", MacStyle.GlobalUnicast),
            new("Vendor OUI (real prefix)", MacStyle.Vendor)
        };

        HostnameStyles = new ObservableCollection<HostnameStyle>
        {
            new("DESKTOP-XXXXXXX", "DESKTOP-"),
            new("PC-XXXXXXXX", "PC-"),
            new("LAPTOP-XXXXXX", "LAPTOP-"),
            new("WORKSTATION-XXX", "WORKSTATION-"),
            new("OFFICE-XXXXXXX", "OFFICE-"),
            new("HOME-XXXXXXXX", "HOME-"),
            new("WIN-XXXXXXXX", "WIN-"),
            new("LAB-XXXXXXXXX", "LAB-"),
            new("NODE-XXXXXXXX", "NODE-"),
            new("STATION-XXXXX", "STATION-")
        };

        DnsPresets = new ObservableCollection<DnsPresetItem>
        {
            new("Cloudflare (1.1.1.1)", new[] { "1.1.1.1", "1.0.0.1" }),
            new("Cloudflare Family (1.1.1.3)", new[] { "1.1.1.3", "1.0.0.3" }),
            new("Quad9 (9.9.9.9)", new[] { "9.9.9.9", "149.112.112.112" }),
            new("Google (8.8.8.8)", new[] { "8.8.8.8", "8.8.4.4" }),
            new("OpenDNS (208.67.222.222)", new[] { "208.67.222.222", "208.67.220.220" }),
            new("AdGuard (94.140.14.14)", new[] { "94.140.14.14", "94.140.15.15" }),
            new("Mullvad (194.242.2.2)", new[] { "194.242.2.2" }),
            new("NextDNS (45.90.28.0)", new[] { "45.90.28.0", "45.90.30.0" }),
            new("dns0.eu (193.110.81.0)", new[] { "193.110.81.0", "185.253.5.0" }),
            new("ControlD (76.76.2.0)", new[] { "76.76.2.0", "76.76.10.0" }),
            new("LibreDNS (116.202.176.26)", new[] { "116.202.176.26" }),
            new("DNS.SB (185.222.222.222)", new[] { "185.222.222.222", "45.11.45.11" })
        };

        IdentityProfiles = new ObservableCollection<IdentityProfile>
        {
            new("Discreet", "Local MAC + hostname + IPv6 privacy. Keeps system identifiers.",
                MacStyle.LocallyAdministered, true, true, false, false, false, true, true),
            new("Corporate", "Vendor MAC + workstation name + MachineGuid + Quad9 DNS.",
                MacStyle.Vendor, true, true, false, true, true, true, true),
            new("Full makeover", "Everything: MAC, hostname, workgroup, MachineGuid, DNS, IPv6, advertising ID.",
                MacStyle.LocallyAdministered, true, true, true, true, true, true, true),
            new("Network only", "MAC + DNS + IPv6 privacy. No system identifier changes.",
                MacStyle.LocallyAdministered, true, false, false, false, true, true, false),
            new("Privacy only", "IPv6 privacy and advertising ID only.",
                MacStyle.LocallyAdministered, false, false, false, false, false, true, true)
        };

        _selectedPreset = Presets[0];
        _selectedVendor = Vendors[0];
        _selectedMacType = MacTypes[0];
        _selectedHostnameStyle = HostnameStyles[0];
        _selectedDnsPreset = DnsPresets[0];
        _selectedIdentityProfile = IdentityProfiles[0];

        RefreshCommand = new AsyncRelayCommand(() => RefreshAsync());
        RandomCommand = new RelayCommand(Randomize);
        RandomizeAllCommand = new AsyncRelayCommand(RandomizeAllAsync);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => SelectedAdapter is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedAdapter is not null);
        RestoreAllCommand = new AsyncRelayCommand(RestoreAllAsync);

        GenerateCandidatesCommand = new RelayCommand(GenerateCandidates);
        UseCandidateCommand = new RelayCommand(UseCandidate, () => SelectedCandidate is not null);
        CopyCandidateCommand = new RelayCommand(CopyCandidate, () => SelectedCandidate is not null);
        ApplyCandidateAllCommand = new AsyncRelayCommand(ApplyCandidateToAllAsync, () => SelectedCandidate is not null);

        ApplyIdentityProfileCommand = new RelayCommand(ApplyIdentityProfile);
        GenerateHostnameCommand = new RelayCommand(() => NewComputerName = GenerateHostnameValue());
        GenerateWorkgroupCommand = new RelayCommand(() => NewWorkgroup = GenerateWorkgroupValue());
        GenerateMachineGuidCommand = new RelayCommand(() => NewMachineGuid = Guid.NewGuid().ToString("D"));
        PreviewIdentityCommand = new RelayCommand(PreviewIdentity);
        ApplyHostnameCommand = new AsyncRelayCommand(ApplyHostnameAsync);
        ApplyWorkgroupCommand = new AsyncRelayCommand(ApplyWorkgroupAsync);
        ApplyMachineGuidCommand = new AsyncRelayCommand(ApplyMachineGuidAsync);
        ApplyPrivacyCommand = new AsyncRelayCommand(ApplyPrivacyAsync);
        GenerateIdentityCommand = new AsyncRelayCommand(GenerateIdentityAsync);
        RestoreIdentityCommand = new AsyncRelayCommand(RestoreIdentityAsync);

        ApplyDnsCommand = new AsyncRelayCommand(ApplyDnsAsync, () => SelectedAdapter is not null);
        RestoreDnsCommand = new AsyncRelayCommand(RestoreDnsAsync, () => SelectedAdapter is not null);

        RestoreEverythingCommand = new AsyncRelayCommand(RestoreEverythingAsync);
        RefreshBackupCommand = new RelayCommand(RefreshBackupInfo);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        ExportReportCommand = new RelayCommand(ExportReport);
        RebootCommand = new RelayCommand(Reboot);

        _statsTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statsTimer.Tick += (_, _) =>
        {
            if (SelectedAdapter is { } adapter) _service.RefreshRuntime(adapter);
        };
    }

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = new();
    public ObservableCollection<PresetItem> Presets { get; }
    public ObservableCollection<PresetItem> Vendors { get; }
    public ObservableCollection<MacTypeOption> MacTypes { get; }
    public ObservableCollection<HostnameStyle> HostnameStyles { get; }
    public ObservableCollection<DnsPresetItem> DnsPresets { get; }
    public ObservableCollection<IdentityProfile> IdentityProfiles { get; }
    public ObservableCollection<string> Candidates { get; } = new();
    public int[] CandidateCounts { get; } = { 5, 10, 20, 50, 100 };

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand RandomCommand { get; }
    public AsyncRelayCommand RandomizeAllCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand RestoreAllCommand { get; }
    public RelayCommand GenerateCandidatesCommand { get; }
    public RelayCommand UseCandidateCommand { get; }
    public RelayCommand CopyCandidateCommand { get; }
    public AsyncRelayCommand ApplyCandidateAllCommand { get; }
    public RelayCommand ApplyIdentityProfileCommand { get; }
    public RelayCommand GenerateHostnameCommand { get; }
    public RelayCommand GenerateWorkgroupCommand { get; }
    public RelayCommand GenerateMachineGuidCommand { get; }
    public RelayCommand PreviewIdentityCommand { get; }
    public AsyncRelayCommand ApplyHostnameCommand { get; }
    public AsyncRelayCommand ApplyWorkgroupCommand { get; }
    public AsyncRelayCommand ApplyMachineGuidCommand { get; }
    public AsyncRelayCommand ApplyPrivacyCommand { get; }
    public AsyncRelayCommand GenerateIdentityCommand { get; }
    public AsyncRelayCommand RestoreIdentityCommand { get; }
    public AsyncRelayCommand ApplyDnsCommand { get; }
    public AsyncRelayCommand RestoreDnsCommand { get; }
    public AsyncRelayCommand RestoreEverythingCommand { get; }
    public RelayCommand RefreshBackupCommand { get; }
    public RelayCommand OpenBackupFolderCommand { get; }
    public RelayCommand ExportReportCommand { get; }
    public RelayCommand RebootCommand { get; }

    public NetworkAdapterInfo? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (!SetProperty(ref _selectedAdapter, value)) return;

            LoadOctetsFrom(value);
            LoadDnsForSelection();
            ApplyCommand.RaiseCanExecuteChanged();
            RestoreCommand.RaiseCanExecuteChanged();
            ApplyDnsCommand.RaiseCanExecuteChanged();
            RestoreDnsCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedAdapter is not null;

    public PresetItem? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!SetProperty(ref _selectedPreset, value)) return;
            if (value is not null && _presetsReady) ApplyPreset(value);
        }
    }

    public MacTypeOption? SelectedMacType
    {
        get => _selectedMacType;
        set
        {
            if (!SetProperty(ref _selectedMacType, value)) return;
            OnPropertyChanged(nameof(IsVendorMode));
        }
    }

    public bool IsVendorMode => SelectedMacType?.Style == MacStyle.Vendor;

    public PresetItem? SelectedVendor
    {
        get => _selectedVendor;
        set => SetProperty(ref _selectedVendor, value);
    }

    public int SelectedCandidateCount
    {
        get => _candidateCount;
        set => SetProperty(ref _candidateCount, value);
    }

    public string? SelectedCandidate
    {
        get => _selectedCandidate;
        set
        {
            if (!SetProperty(ref _selectedCandidate, value)) return;
            UseCandidateCommand.RaiseCanExecuteChanged();
            CopyCandidateCommand.RaiseCanExecuteChanged();
            ApplyCandidateAllCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsGenerating
    {
        get => _isGenerating;
        private set => SetProperty(ref _isGenerating, value);
    }

    public IdentityProfile? SelectedIdentityProfile
    {
        get => _selectedIdentityProfile;
        set
        {
            if (!SetProperty(ref _selectedIdentityProfile, value)) return;
            OnPropertyChanged(nameof(ProfileDescription));
        }
    }

    public string ProfileDescription => SelectedIdentityProfile?.Description ?? string.Empty;

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    public string Octet1 { get => _octet1; set => SetOctet(ref _octet1, value); }
    public string Octet2 { get => _octet2; set => SetOctet(ref _octet2, value); }
    public string Octet3 { get => _octet3; set => SetOctet(ref _octet3, value); }
    public string Octet4 { get => _octet4; set => SetOctet(ref _octet4, value); }
    public string Octet5 { get => _octet5; set => SetOctet(ref _octet5, value); }
    public string Octet6 { get => _octet6; set => SetOctet(ref _octet6, value); }

    public bool AutoRestart
    {
        get => _autoRestart;
        set => SetProperty(ref _autoRestart, value);
    }

    public bool UseZeroTwo
    {
        get => _useZeroTwo;
        set => SetProperty(ref _useZeroTwo, value);
    }

    public bool ShowVirtualAdapters
    {
        get => _showVirtualAdapters;
        set
        {
            if (!SetProperty(ref _showVirtualAdapters, value)) return;
            if (_isInitialized) _ = RefreshAsync();
        }
    }

    public string ComputerNameCurrent
    {
        get => _computerNameCurrent;
        private set => SetProperty(ref _computerNameCurrent, value);
    }

    public string NewComputerName
    {
        get => _newComputerName;
        set => SetProperty(ref _newComputerName, value);
    }

    public HostnameStyle? SelectedHostnameStyle
    {
        get => _selectedHostnameStyle;
        set => SetProperty(ref _selectedHostnameStyle, value);
    }

    public string WorkgroupCurrent
    {
        get => _workgroupCurrent;
        private set => SetProperty(ref _workgroupCurrent, value);
    }

    public string NewWorkgroup
    {
        get => _newWorkgroup;
        set => SetProperty(ref _newWorkgroup, value);
    }

    public bool IsDomainJoined
    {
        get => _isDomainJoined;
        private set
        {
            if (!SetProperty(ref _isDomainJoined, value)) return;
            OnPropertyChanged(nameof(CanChangeWorkgroup));
        }
    }

    public bool CanChangeWorkgroup => !IsDomainJoined;

    public string MachineGuidCurrent
    {
        get => _machineGuidCurrent;
        private set => SetProperty(ref _machineGuidCurrent, value);
    }

    public string NewMachineGuid
    {
        get => _newMachineGuid;
        set => SetProperty(ref _newMachineGuid, value);
    }

    public bool Ipv6PrivacyEnabled
    {
        get => _ipv6PrivacyEnabled;
        set => SetProperty(ref _ipv6PrivacyEnabled, value);
    }

    public bool AdvertisingIdEnabled
    {
        get => _advertisingIdEnabled;
        set => SetProperty(ref _advertisingIdEnabled, value);
    }

    public string RebootRequiredMessage
    {
        get => _rebootRequiredMessage;
        private set => SetProperty(ref _rebootRequiredMessage, value);
    }

    public bool IncludeMac
    {
        get => _includeMac;
        set => SetProperty(ref _includeMac, value);
    }

    public bool IncludeHostname
    {
        get => _includeHostname;
        set => SetProperty(ref _includeHostname, value);
    }

    public bool IncludeWorkgroup
    {
        get => _includeWorkgroup;
        set => SetProperty(ref _includeWorkgroup, value);
    }

    public bool IncludeMachineGuid
    {
        get => _includeMachineGuid;
        set => SetProperty(ref _includeMachineGuid, value);
    }

    public bool IncludeDns
    {
        get => _includeDns;
        set => SetProperty(ref _includeDns, value);
    }

    public bool IncludeIpv6
    {
        get => _includeIpv6;
        set => SetProperty(ref _includeIpv6, value);
    }

    public bool IncludeAdvertising
    {
        get => _includeAdvertising;
        set => SetProperty(ref _includeAdvertising, value);
    }

    public DnsPresetItem? SelectedDnsPreset
    {
        get => _selectedDnsPreset;
        set => SetProperty(ref _selectedDnsPreset, value);
    }

    public bool ApplyDnsToAll
    {
        get => _applyDnsToAll;
        set => SetProperty(ref _applyDnsToAll, value);
    }

    public string CurrentDnsText
    {
        get => _currentDnsText;
        private set => SetProperty(ref _currentDnsText, value);
    }

    public string BackupSummary
    {
        get => _backupSummary;
        private set => SetProperty(ref _backupSummary, value);
    }

    public string LogText
    {
        get => _logText;
        private set => SetProperty(ref _logText, value);
    }

    public string BackupFolder => _backup.DirectoryPath;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public async Task InitializeAsync()
    {
        _isInitialized = true;
        await RefreshAsync();
        _presetsReady = true;
        LoadIdentityState();
        RefreshBackupInfo();
        _statsTimer.Start();
    }

    public async Task RefreshAsync(string? selectGuid = null)
    {
        var guidToSelect = selectGuid ?? SelectedAdapter?.Guid;

        try
        {
            var list = await Task.Run(() => _service.LoadAdapters(ShowVirtualAdapters));

            Adapters.Clear();
            foreach (var adapter in list)
                Adapters.Add(adapter);

            SelectedAdapter = Adapters.FirstOrDefault(a =>
                                 string.Equals(a.Guid, guidToSelect, StringComparison.OrdinalIgnoreCase))
                             ?? Adapters.FirstOrDefault();

            RefreshBackupInfo();

            var count = Adapters.Count;
            SetStatus(count == 0
                ? "No modifiable network adapter was detected."
                : $"{count} network adapter{(count > 1 ? "s" : "")} detected.");
        }
        catch (Exception ex)
        {
            SetStatus("Error while reading network adapters: " + ex.Message, isError: true);
        }
    }

    private void LoadIdentityState()
    {
        ComputerNameCurrent = _identity.ComputerName;

        var (domain, partOfDomain) = _identity.GetDomainInfo();
        WorkgroupCurrent = domain;
        IsDomainJoined = partOfDomain;

        MachineGuidCurrent = _identity.GetMachineGuid();

        var (randomize, privacy) = _identity.GetIpv6States();
        Ipv6PrivacyEnabled = randomize && privacy;
        AdvertisingIdEnabled = _identity.AdvertisingIdEnabled();

        if (string.IsNullOrWhiteSpace(NewComputerName) || NewComputerName == ComputerNameCurrent)
            NewComputerName = GenerateHostnameValue();

        if (string.IsNullOrWhiteSpace(NewMachineGuid))
            NewMachineGuid = Guid.NewGuid().ToString("D");

        PreviewText = BuildPreview();
    }

    private void LoadDnsForSelection()
    {
        try
        {
            CurrentDnsText = SelectedAdapter is null
                ? "—"
                : _service.GetDnsServers(SelectedAdapter) is { Length: > 0 } servers
                    ? string.Join(", ", servers)
                    : "Automatic (DHCP)";
        }
        catch
        {
            CurrentDnsText = "—";
        }
    }

    private void LoadOctetsFrom(NetworkAdapterInfo? adapter)
    {
        if (adapter is null)
        {
            LoadOctets(null);
            return;
        }

        LoadOctets(AdapterService.NormalizeHex(adapter.MacAddress));
    }

    private void LoadOctets(string? hex)
    {
        if (hex is null || hex.Length != 12)
        {
            Octet1 = Octet2 = Octet3 = Octet4 = Octet5 = Octet6 = string.Empty;
            return;
        }

        Octet1 = hex.Substring(0, 2);
        Octet2 = hex.Substring(2, 2);
        Octet3 = hex.Substring(4, 2);
        Octet4 = hex.Substring(6, 2);
        Octet5 = hex.Substring(8, 2);
        Octet6 = hex.Substring(10, 2);
    }

    public void SetMacAddress(string hex)
    {
        var normalized = AdapterService.NormalizeHex(hex);
        if (normalized is null) return;
        LoadOctets(normalized);
    }

    private string? ComposeMac()
    {
        var octets = new[] { Octet1, Octet2, Octet3, Octet4, Octet5, Octet6 };

        foreach (var octet in octets)
        {
            if (octet.Length != 2 ||
                !byte.TryParse(octet, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                return null;
        }

        return string.Concat(octets).ToUpperInvariant();
    }

    private void SetOctet(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        value = value.ToUpperInvariant();
        if (!SetProperty(ref field, value, propertyName)) return;
        ApplyCommand.RaiseCanExecuteChanged();
    }

    private void Randomize()
    {
        LoadOctets(AdapterService.RandomMac());
        SelectedPreset = Presets[0];
    }

    private void ApplyPreset(PresetItem? preset)
    {
        if (preset?.Prefix is null)
        {
            if (Octet1.Length == 0) return;
            LoadOctets(AdapterService.RandomMac());
            return;
        }

        LoadOctets(AdapterService.RandomizeWithPrefix(preset.Prefix));
    }

    private async Task RandomizeAllAsync()
    {
        if (Adapters.Count == 0)
        {
            SetStatus("No network adapter available.", isError: true);
            return;
        }

        var adapters = Adapters.ToList();
        var style = SelectedMacType?.Style ?? MacStyle.LocallyAdministered;
        var vendor = style == MacStyle.Vendor ? SelectedVendor?.Prefix : null;
        var restart = AutoRestart;

        SetStatus($"Generating a new MAC address for {adapters.Count} adapter(s)…");

        var results = await Task.Run(() =>
            adapters.Select(adapter => _service.ApplyMac(adapter, AdapterService.GenerateMac(style, vendor), restart)).ToList());

        await RefreshAsync();
        RefreshBackupInfo();

        var failures = results.Count(r => !r.Ok);
        SetStatus(failures == 0
            ? $"New MAC addresses applied to {adapters.Count} adapter(s)."
            : $"{failures} adapter(s) rejected the change. {results.FirstOrDefault(r => !r.Ok).Message}",
            failures > 0);
    }

    private void GenerateCandidates()
    {
        if (IsGenerating) return;

        IsGenerating = true;
        try
        {
            var style = SelectedMacType?.Style ?? MacStyle.LocallyAdministered;
            var prefix = style == MacStyle.Vendor ? SelectedVendor?.Prefix : null;
            var generated = AdapterService.GenerateCandidates(style, prefix, SelectedCandidateCount);

            Candidates.Clear();
            foreach (var mac in generated) Candidates.Add(mac);

            SelectedCandidate = Candidates.FirstOrDefault();
            SetStatus($"{Candidates.Count} MAC addresses generated.");
        }
        finally
        {
            IsGenerating = false;
        }
    }

    private void UseCandidate()
    {
        if (SelectedCandidate is null) return;
        SetMacAddress(SelectedCandidate);
        SetStatus($"Address {SelectedCandidate} loaded into the editor.");
    }

    private void CopyCandidate()
    {
        if (SelectedCandidate is null) return;

        try
        {
            System.Windows.Clipboard.SetText(SelectedCandidate);
            SetStatus("Address copied to the clipboard.");
        }
        catch (Exception ex)
        {
            SetStatus("Clipboard unavailable: " + ex.Message, isError: true);
        }
    }

    private async Task ApplyCandidateToAllAsync()
    {
        if (SelectedCandidate is null) return;

        var style = SelectedMacType?.Style ?? MacStyle.LocallyAdministered;
        var prefix = style == MacStyle.Vendor ? SelectedVendor?.Prefix : null;
        var adapters = Adapters.ToList();

        var macs = AdapterService.GenerateCandidates(style, prefix, adapters.Count);
        var restart = AutoRestart;
        var failures = 0;

        SetStatus($"Applying new MAC addresses to {adapters.Count} adapter(s)…");

        var messages = await Task.Run(() =>
        {
            var log = new List<string>();
            for (var i = 0; i < adapters.Count && i < macs.Count; i++)
            {
                var (ok, message) = _service.ApplyMac(adapters[i], macs[i], restart);
                if (!ok) failures++;
                log.Add(message);
            }
            return log;
        });

        await RefreshAsync();
        RefreshBackupInfo();

        SetStatus(failures == 0
            ? $"MAC addresses applied to {adapters.Count} adapter(s)."
            : $"{failures} adapter(s) rejected the change. {messages.FirstOrDefault(m => m.Contains("refused"))}",
            failures > 0);
    }

    private async Task ApplyAsync()
    {
        if (SelectedAdapter is not { } adapter) return;

        var mac = ComposeMac();
        if (mac is null)
        {
            SetStatus("Incomplete MAC address: fill in all 6 octets.", isError: true);
            return;
        }

        if (UseZeroTwo) mac = AdapterService.ForceLocalAdmin(mac);

        if (!AdapterService.IsValidMac(mac, out var error))
        {
            SetStatus(error, isError: true);
            return;
        }

        SetStatus($"Applying {AdapterService.FormatMac(mac)} to \"{adapter.Name}\"…");

        var restart = AutoRestart;
        var (ok, message) = await Task.Run(() => _service.ApplyMac(adapter, mac, restart));

        SetStatus(message, isError: !ok);
        await RefreshAsync(adapter.Guid);
        RefreshBackupInfo();
    }

    private async Task RestoreAsync()
    {
        if (SelectedAdapter is not { } adapter) return;

        SetStatus($"Restoring the original address of \"{adapter.Name}\"…");

        var restart = AutoRestart;
        var (ok, message) = await Task.Run(() => _service.RestoreOriginal(adapter, restart));

        SetStatus(message, isError: !ok);
        await RefreshAsync(adapter.Guid);
        RefreshBackupInfo();
    }

    private async Task RestoreAllAsync()
    {
        var targets = Adapters.Where(a => a.IsChanged).ToList();
        if (targets.Count == 0)
        {
            SetStatus("No modified MAC address to restore.");
            return;
        }

        SetStatus($"Restoring {targets.Count} adapter(s)…");

        var restart = AutoRestart;
        var failures = 0;

        foreach (var adapter in targets)
        {
            var (ok, _) = await Task.Run(() => _service.RestoreOriginal(adapter, restart));
            if (!ok) failures++;
        }

        SetStatus(failures == 0
            ? "All original addresses have been restored."
            : $"{failures} restore operation(s) failed.", isError: failures > 0);

        await RefreshAsync();
        RefreshBackupInfo();
    }

    private string GenerateHostnameValue()
    {
        var prefix = SelectedHostnameStyle?.Prefix ?? "DESKTOP-";
        var length = Math.Max(3, 15 - prefix.Length);
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new string(Enumerable.Range(0, length)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());

        return prefix + suffix;
    }

    private static string GenerateWorkgroupValue()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());

        return "WG-" + suffix;
    }

    private void ApplyIdentityProfile()
    {
        if (SelectedIdentityProfile is not { } profile) return;

        var macType = MacTypes.FirstOrDefault(t => t.Style == profile.MacStyle);
        if (macType is not null) SelectedMacType = macType;

        IncludeMac = profile.Mac;
        IncludeHostname = profile.Hostname;
        IncludeWorkgroup = profile.Workgroup && CanChangeWorkgroup;
        IncludeMachineGuid = profile.MachineGuid;
        IncludeDns = profile.Dns;
        IncludeIpv6 = profile.Ipv6;
        IncludeAdvertising = profile.Advertising;

        if (profile.Dns && SelectedDnsPreset is null)
            SelectedDnsPreset = DnsPresets[Math.Min(2, DnsPresets.Count - 1)];

        PreviewIdentity();
        SetStatus($"Profile \"{profile.Label}\" loaded. Review the preview, then generate the identity.");
    }

    private void PreviewIdentity()
    {
        NewComputerName = GenerateHostnameValue();
        NewMachineGuid = Guid.NewGuid().ToString("D");
        PreviewText = BuildPreview();
        SetStatus("Preview updated.");
    }

    private string BuildPreview()
    {
        var macStyle = SelectedMacType?.Label ?? "Locally administered (02)";
        if (IsVendorMode && SelectedVendor?.Prefix is not null)
            macStyle += $" — {SelectedVendor.Label} {SelectedVendor.Prefix}";

        var dns = IncludeDns && SelectedDnsPreset is not null
            ? $"{SelectedDnsPreset.Label} [{string.Join(", ", SelectedDnsPreset.Servers)}]"
            : "unchanged";

        var workgroup = IncludeWorkgroup
            ? (CanChangeWorkgroup ? NewWorkgroup : "domain joined — locked")
            : "unchanged";

        var builder = new StringBuilder()
            .AppendLine($"Hostname       : {(IncludeHostname ? NewComputerName : "unchanged")}")
            .AppendLine($"Machine GUID   : {(IncludeMachineGuid ? NewMachineGuid : "unchanged")}")
            .AppendLine($"MAC profile    : {macStyle}")
            .AppendLine($"Adapters       : {Adapters.Count} ({Adapters.Count(a => a.IsConnected)} connected)")
            .AppendLine($"DNS            : {dns}")
            .AppendLine($"IPv6 privacy   : {(IncludeIpv6 ? "enabled" : "unchanged")}")
            .AppendLine($"Advertising ID : {(IncludeAdvertising ? "disabled" : "unchanged")}")
            .AppendLine($"Workgroup      : {workgroup}")
            .AppendLine($"Restart        : {(IncludeHostname || IncludeWorkgroup ? "required" : "not required")}");

        return builder.ToString();
    }

    private Task ApplyHostnameAsync()
    {
        var name = NewComputerName.Trim();
        var (ok, message) = _identity.SetComputerName(name);

        if (ok) RebootRequiredMessage = "A restart is required to apply the new computer name.";
        SetStatus(message, isError: !ok);

        LoadIdentityState();
        RefreshBackupInfo();
        return Task.CompletedTask;
    }

    private Task ApplyWorkgroupAsync()
    {
        var (ok, message) = _identity.SetWorkgroup(NewWorkgroup.Trim());

        if (ok) RebootRequiredMessage = "A restart is required to apply the new workgroup.";
        SetStatus(message, isError: !ok);

        LoadIdentityState();
        RefreshBackupInfo();
        return Task.CompletedTask;
    }

    private Task ApplyMachineGuidAsync()
    {
        var (ok, message) = _identity.SetMachineGuid(NewMachineGuid.Trim());
        SetStatus(message, isError: !ok);

        LoadIdentityState();
        RefreshBackupInfo();
        return Task.CompletedTask;
    }

    private Task ApplyPrivacyAsync()
    {
        var messages = new List<string>();
        var errors = 0;

        var ipv6 = _identity.SetIpv6Privacy(Ipv6PrivacyEnabled);
        messages.Add(ipv6.Message);
        if (!ipv6.Ok) errors++;

        var advertising = _identity.SetAdvertisingId(AdvertisingIdEnabled);
        messages.Add(advertising.Message);
        if (!advertising.Ok) errors++;

        SetStatus(string.Join(" ", messages), errors > 0);
        LoadIdentityState();
        RefreshBackupInfo();
        return Task.CompletedTask;
    }

    private async Task GenerateIdentityAsync()
    {
        var adapters = Adapters.ToList();
        var style = SelectedMacType?.Style ?? MacStyle.LocallyAdministered;
        var vendor = style == MacStyle.Vendor ? SelectedVendor?.Prefix : null;
        var restart = AutoRestart;
        var includeMac = IncludeMac;
        var parts = new List<string>();
        var errors = new List<string>();

        SetStatus("Generating a new identity…");

        if (includeMac)
        {
            var macResults = await Task.Run(() =>
            {
                var results = new List<(bool Ok, string Message)>();
                foreach (var adapter in adapters)
                {
                    var mac = AdapterService.GenerateMac(style, vendor);
                    results.Add(_service.ApplyMac(adapter, mac, restart));
                }
                return results;
            });

            var changed = macResults.Count(r => r.Ok);
            if (changed > 0) parts.Add($"{changed} MAC address(es)");
            foreach (var result in macResults.Where(r => !r.Ok)) errors.Add(result.Message);
        }

        var reboot = false;

        if (IncludeHostname)
        {
            var name = string.IsNullOrWhiteSpace(NewComputerName)
                ? GenerateHostnameValue()
                : NewComputerName.Trim();

            var (ok, message) = _identity.SetComputerName(name);
            if (ok)
            {
                parts.Add("computer name");
                NewComputerName = name;
                reboot = true;
            }
            else errors.Add(message);
        }

        if (IncludeWorkgroup && CanChangeWorkgroup)
        {
            var (ok, message) = _identity.SetWorkgroup(NewWorkgroup.Trim());
            if (ok)
            {
                parts.Add("workgroup");
                reboot = true;
            }
            else errors.Add(message);
        }

        if (IncludeMachineGuid)
        {
            var guid = Guid.TryParse(NewMachineGuid, out var parsed)
                ? parsed.ToString("D")
                : Guid.NewGuid().ToString("D");

            var (ok, message) = _identity.SetMachineGuid(guid);
            if (ok)
            {
                parts.Add("machine GUID");
                NewMachineGuid = guid;
            }
            else errors.Add(message);
        }

        if (IncludeDns && SelectedDnsPreset is { } dns)
        {
            var targets = ApplyDnsToAll
                ? adapters.Where(a => a.IsConnected).ToList()
                : SelectedAdapter is { } selected ? new List<NetworkAdapterInfo> { selected } : new List<NetworkAdapterInfo>();

            foreach (var adapter in targets)
            {
                var (ok, message) = _service.SetDnsServers(adapter, dns.Servers);
                if (ok) parts.Add($"DNS on {adapter.Name}");
                else errors.Add(message);
            }
        }

        if (IncludeIpv6)
        {
            var (ok, message) = _identity.SetIpv6Privacy(true);
            if (ok) parts.Add("IPv6 privacy");
            else errors.Add(message);
        }

        if (IncludeAdvertising)
        {
            var (ok, message) = _identity.SetAdvertisingId(false);
            if (ok) parts.Add("advertising ID disabled");
            else errors.Add(message);
        }

        RebootRequiredMessage = reboot
            ? "A restart is required to apply the computer name / workgroup."
            : string.Empty;

        await RefreshAsync();
        LoadIdentityState();
        RefreshBackupInfo();

        var summary = parts.Count == 0
            ? "No change applied."
            : "New identity: " + string.Join(", ", parts) + ".";

        if (errors.Count > 0)
            summary += " Errors: " + string.Join(" ", errors.Take(3));

        SetStatus(summary, errors.Count > 0);
        _backup.Log("Identity: " + summary);
    }

    private async Task ApplyDnsAsync()
    {
        if (SelectedDnsPreset is null) return;

        var targets = ApplyDnsToAll
            ? Adapters.Where(a => a.IsConnected).ToList()
            : SelectedAdapter is { } selected ? new List<NetworkAdapterInfo> { selected } : new List<NetworkAdapterInfo>();

        if (targets.Count == 0)
        {
            SetStatus("No active adapter to apply DNS to.", isError: true);
            return;
        }

        var errors = new List<string>();
        var applied = 0;

        foreach (var adapter in targets)
        {
            var (ok, message) = await Task.Run(() => _service.SetDnsServers(adapter, SelectedDnsPreset.Servers));
            if (ok) applied++;
            else errors.Add(message);
        }

        LoadDnsForSelection();
        RefreshBackupInfo();

        SetStatus(errors.Count == 0
            ? $"DNS applied to {applied} adapter(s)."
            : $"DNS applied to {applied} adapter(s), {errors.Count} failure(s). {errors.FirstOrDefault()}",
            errors.Count > 0);
    }

    private async Task RestoreDnsAsync()
    {
        if (SelectedAdapter is not { } adapter) return;

        var (ok, message) = await Task.Run(() => _service.RestoreDns(adapter));
        SetStatus(message, isError: !ok);

        LoadDnsForSelection();
        RefreshBackupInfo();
    }

    private async Task RestoreIdentityAsync()
    {
        SetStatus("Restoring the original identity…");

        var (ok, message) = await Task.Run(() => _identity.RestoreIdentity());

        LoadIdentityState();
        RefreshBackupInfo();

        SetStatus(ok ? message : "Incomplete restore: " + message, isError: !ok);
        if (ok) RebootRequiredMessage = "A restart may be required to apply the original identity.";
    }

    private async Task RestoreEverythingAsync()
    {
        var answer = MessageBox.Show(
            "Restore all original MAC addresses, the system identity and the saved DNS settings?",
            "Al-Ansari MAC Changer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        SetStatus("Full restore in progress…");

        var adapters = Adapters.ToList();

        var (identityOk, identityMessage) = await Task.Run(() =>
        {
            foreach (var adapter in adapters)
                _service.RestoreOriginal(adapter, restart: false);

            foreach (var adapter in adapters.Where(a => _backup.HasDns(a.Guid)))
                _service.RestoreDns(adapter);

            return _identity.RestoreIdentity();
        });

        await RefreshAsync();
        LoadIdentityState();
        RefreshBackupInfo();

        RebootRequiredMessage = "A restart is recommended after a full restore.";
        SetStatus("Restore complete. " + identityMessage, isError: !identityOk);
        _backup.Log("Full restore: " + identityMessage);
    }

    private void RefreshBackupInfo()
    {
        var changedMacs = Adapters.Count(a => a.IsChanged);
        BackupSummary =
            $"{_backup.Data.Values.Count} system identifier(s) saved  ·  " +
            $"{_backup.Data.Dns.Count} DNS configuration(s)  ·  " +
            $"{changedMacs} modified MAC address(es)";
        LogText = _backup.ReadLog();
    }

    private void OpenBackupFolder()
    {
        try
        {
            Directory.CreateDirectory(_backup.DirectoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_backup.DirectoryPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SetStatus("Unable to open the folder: " + ex.Message, isError: true);
        }
    }

    private void ExportReport()
    {
        try
        {
            Directory.CreateDirectory(_backup.DirectoryPath);

            var builder = new StringBuilder();
            builder.AppendLine("Al-Ansari MAC Changer — identity report");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine(new string('-', 60));
            builder.AppendLine($"Computer name : {ComputerNameCurrent}");
            builder.AppendLine($"Workgroup     : {WorkgroupCurrent}{(IsDomainJoined ? " (domain)" : "")}");
            builder.AppendLine($"Machine GUID  : {MachineGuidCurrent}");
            builder.AppendLine($"IPv6 privacy  : {(Ipv6PrivacyEnabled ? "enabled" : "disabled")}");
            builder.AppendLine($"Advertising ID: {(AdvertisingIdEnabled ? "enabled" : "disabled")}");
            builder.AppendLine(new string('-', 60));
            builder.AppendLine("Adapters:");
            foreach (var adapter in Adapters)
            {
                builder.AppendLine(
                    $"  {adapter.Name} | {adapter.MacAddress} | original {adapter.OriginalMac} | " +
                    $"{adapter.LinkStatus} | {adapter.SpeedText} | modified: {adapter.ChangedText}");
            }
            builder.AppendLine(new string('-', 60));
            builder.AppendLine(BackupSummary);
            builder.AppendLine(new string('-', 60));
            builder.AppendLine("Recent log:");
            builder.AppendLine(LogText);

            var file = Path.Combine(_backup.DirectoryPath, $"report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, builder.ToString());

            SetStatus("Report exported: " + file);
        }
        catch (Exception ex)
        {
            SetStatus("Unable to export the report: " + ex.Message, isError: true);
        }
    }

    private void Reboot()
    {
        var answer = MessageBox.Show(
            "Restart Windows now to apply the changes?\n\nThe computer will restart in 10 seconds.",
            "Al-Ansari MAC Changer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 10 /c \"Al-Ansari MAC Changer: restart to apply the new identity\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            SetStatus("Restart scheduled in 10 seconds. Save your work.");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to schedule the restart: " + ex.Message, isError: true);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
