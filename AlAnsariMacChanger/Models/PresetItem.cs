namespace AlAnsariMacChanger.Models;

public sealed record PresetItem(string Label, string? Prefix)
{
    public override string ToString() => Label;
}
