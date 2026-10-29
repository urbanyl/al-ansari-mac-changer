using System.IO;
using System.Text.Json;

namespace AlAnsariMacChanger.Services;

public sealed class BackupStore
{
    private BackupData _data = new();
    private bool _loaded;

    public BackupStore()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Al-Ansari",
            "MacChanger");
        BackupFile = Path.Combine(DirectoryPath, "backup.json");
        LogFile = Path.Combine(DirectoryPath, "history.log");
        EnsureLoaded();
    }

    public string DirectoryPath { get; }
    public string BackupFile { get; }
    public string LogFile { get; }

    public BackupData Data
    {
        get
        {
            EnsureLoaded();
            return _data;
        }
    }

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (!File.Exists(BackupFile)) return;
            var json = File.ReadAllText(BackupFile);
            _data = JsonSerializer.Deserialize<BackupData>(json) ?? new BackupData();
        }
        catch
        {
            _data = new BackupData();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(BackupFile, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Backup failures must never break the app.
        }
    }

    public string? GetValue(string key) => Data.Values.TryGetValue(key, out var value) ? value : null;

    public void Remember(string key, string? value)
    {
        if (Data.Values.ContainsKey(key)) return;
        Data.Values[key] = value;
        Save();
    }

    public void SetValue(string key, string? value)
    {
        Data.Values[key] = value;
        Save();
    }

    public bool HasDns(string guid) => Data.Dns.ContainsKey(guid);
    public string[]? GetDns(string guid) => Data.Dns.TryGetValue(guid, out var value) ? value : null;

    public void RememberDns(string guid, string[]? servers)
    {
        if (Data.Dns.ContainsKey(guid)) return;
        Data.Dns[guid] = servers;
        Save();
    }

    public void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging is best-effort only.
        }
    }

    public string ReadLog(int maxLines = 400)
    {
        try
        {
            if (!File.Exists(LogFile)) return string.Empty;
            var lines = File.ReadAllLines(LogFile);
            return string.Join(Environment.NewLine, lines.TakeLast(maxLines));
        }
        catch
        {
            return string.Empty;
        }
    }
}

public sealed class BackupData
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string?> Values { get; set; } = new();
    public Dictionary<string, string[]?> Dns { get; set; } = new();
}
