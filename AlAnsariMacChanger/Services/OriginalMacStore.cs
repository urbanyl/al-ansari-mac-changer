using System.IO;
using System.Text.Json;

namespace AlAnsariMacChanger.Services;

public sealed class OriginalMacStore
{
    private readonly string _filePath;
    private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;

    public OriginalMacStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Al-Ansari",
            "MacChanger");
        _filePath = Path.Combine(dir, "originals.json");
        Load();
    }

    public string? Get(string guid) => _map.TryGetValue(guid, out var value) ? value : null;

    public void Set(string guid, string mac)
    {
        if (string.Equals(Get(guid), mac, StringComparison.OrdinalIgnoreCase)) return;
        _map[guid] = mac;
        _dirty = true;
    }

    public void SaveIfDirty()
    {
        if (!_dirty) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true }));
            _dirty = false;
        }
        catch
        {
            // Silently ignore: the app still works, the original MAC just won't be remembered.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (data is null) return;
            foreach (var pair in data) _map[pair.Key] = pair.Value;
        }
        catch
        {
            // Corrupted store: start fresh.
        }
    }
}
