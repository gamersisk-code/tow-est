using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TowEstimator;

public sealed class Storage
{
    private const int MaxLogs = 200;
    private readonly string _prefsPath;
    private readonly string _logsPath;

    public Storage()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SouthernPrideTowing");
        Directory.CreateDirectory(root);
        _prefsPath = Path.Combine(root, "prefs.json");
        _logsPath = Path.Combine(root, "logs.json");
    }

    public EstimatorPreferences LoadPreferences()
    {
        try
        {
            if (!File.Exists(_prefsPath))
            {
                return new EstimatorPreferences();
            }

            var json = File.ReadAllText(_prefsPath);
            return JsonSerializer.Deserialize<EstimatorPreferences>(json) ?? new EstimatorPreferences();
        }
        catch
        {
            return new EstimatorPreferences();
        }
    }

    public void SavePreferences(EstimatorPreferences prefs)
    {
        var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_prefsPath, json);
    }

    public List<LogEntry> LoadLogs()
    {
        try
        {
            if (!File.Exists(_logsPath))
            {
                return new List<LogEntry>();
            }

            var json = File.ReadAllText(_logsPath);
            return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
        }
        catch
        {
            return new List<LogEntry>();
        }
    }

    public void SaveLogs(List<LogEntry> logs)
    {
        var trimmed = logs.Count > MaxLogs ? logs.GetRange(0, MaxLogs) : logs;
        var json = JsonSerializer.Serialize(trimmed, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_logsPath, json);
    }
}
