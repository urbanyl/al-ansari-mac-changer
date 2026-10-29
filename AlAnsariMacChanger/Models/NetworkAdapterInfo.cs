using AlAnsariMacChanger.Infrastructure;

namespace AlAnsariMacChanger.Models;

public sealed class NetworkAdapterInfo : ObservableObject
{
    private bool _isConnected;
    private string _linkStatus = "—";
    private ulong _speed;
    private long _bytesReceived;
    private long _bytesSent;
    private string _ipv4 = string.Empty;
    private string _ipv6 = string.Empty;
    private string _macAddress = string.Empty;
    private bool _isChanged;

    public required string Guid { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string HardwareId { get; init; }
    public required string DeviceId { get; init; }
    public required string ConfigId { get; init; }
    public int InterfaceIndex { get; init; }
    public string OriginalMac { get; init; } = "Inconnue";

    public string MacAddress
    {
        get => _macAddress;
        set => SetProperty(ref _macAddress, value);
    }

    public bool IsChanged
    {
        get => _isChanged;
        set
        {
            if (!SetProperty(ref _isChanged, value)) return;
            OnPropertyChanged(nameof(ChangedText));
        }
    }

    public string ChangedText => IsChanged ? "Yes" : "No";

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (!SetProperty(ref _isConnected, value)) return;
            OnPropertyChanged(nameof(LinkStatus));
        }
    }

    public string LinkStatus
    {
        get => _linkStatus;
        set => SetProperty(ref _linkStatus, value);
    }

    public ulong Speed
    {
        get => _speed;
        set
        {
            if (!SetProperty(ref _speed, value)) return;
            OnPropertyChanged(nameof(SpeedText));
        }
    }

    public string SpeedText => Speed == 0
        ? "—"
        : Speed >= 1_000_000_000
            ? $"{Speed / 1_000_000_000.0:0.#} Gb/s"
            : Speed >= 1_000_000
                ? $"{Speed / 1_000_000.0:0.#} Mb/s"
                : $"{Speed / 1_000.0:0.#} Kb/s";

    public long BytesReceived
    {
        get => _bytesReceived;
        set
        {
            if (!SetProperty(ref _bytesReceived, value)) return;
            OnPropertyChanged(nameof(ReceivedText));
        }
    }

    public long BytesSent
    {
        get => _bytesSent;
        set
        {
            if (!SetProperty(ref _bytesSent, value)) return;
            OnPropertyChanged(nameof(SentText));
        }
    }

    public string ReceivedText => FormatBytes(BytesReceived);
    public string SentText => FormatBytes(BytesSent);

    public string IPv4
    {
        get => _ipv4;
        set
        {
            if (!SetProperty(ref _ipv4, value)) return;
            OnPropertyChanged(nameof(IPv4Text));
        }
    }

    public string IPv6
    {
        get => _ipv6;
        set
        {
            if (!SetProperty(ref _ipv6, value)) return;
            OnPropertyChanged(nameof(IPv6Text));
        }
    }

    public string IPv4Text => string.IsNullOrWhiteSpace(IPv4) ? "—" : IPv4;
    public string IPv6Text => string.IsNullOrWhiteSpace(IPv6) ? "—" : IPv6;

    public static string FormatBytes(long value)
    {
        if (value < 1024) return $"{value} B";
        var kb = value / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        var mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }
}
