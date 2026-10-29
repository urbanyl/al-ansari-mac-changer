namespace AlAnsariMacChanger.Models;

public sealed record MacTypeOption(string Label, MacStyle Style)
{
    public override string ToString() => Label;
}

public sealed record DnsPresetItem(string Label, string[] Servers)
{
    public override string ToString() => Label;
}

public sealed record HostnameStyle(string Label, string Prefix)
{
    public override string ToString() => Label;
}

public sealed record IdentityProfile(
    string Label,
    string Description,
    MacStyle MacStyle,
    bool Mac,
    bool Hostname,
    bool Workgroup,
    bool MachineGuid,
    bool Dns,
    bool Ipv6,
    bool Advertising)
{
    public override string ToString() => Label;
}
