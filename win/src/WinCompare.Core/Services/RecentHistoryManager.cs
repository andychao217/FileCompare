using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinCompare.Core.Models;

namespace WinCompare.Core.Services;

public sealed class RecentHistoryManager
{
    private static readonly Lazy<RecentHistoryManager> _instance = new(() => new RecentHistoryManager());
    public static RecentHistoryManager Instance => _instance.Value;

    private readonly string _storagePath;
    public ObservableCollection<RecentCompareRecord> Records { get; } = new();

    public event Action? HistoryChanged;

    public RecentHistoryManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string folder = Path.Combine(appData, "WinCompare");
        Directory.CreateDirectory(folder);
        _storagePath = Path.Combine(folder, "recent_history.json");

        LoadRecords();
    }

    public void AddRecord(string leftPath, string rightPath, CompareType type)
    {
        if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
            return;

        var newRecord = new RecentCompareRecord
        {
            LeftPath = leftPath,
            RightPath = rightPath,
            Type = type,
            Timestamp = DateTime.Now
        };
        newRecord.Type = newRecord.EffectiveType;

        // Deduplicate bidirectional
        var existing = Records.FirstOrDefault(r =>
            (r.LeftPath.Equals(leftPath, StringComparison.OrdinalIgnoreCase) && r.RightPath.Equals(rightPath, StringComparison.OrdinalIgnoreCase)) ||
            (r.LeftPath.Equals(rightPath, StringComparison.OrdinalIgnoreCase) && r.RightPath.Equals(leftPath, StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            Records.Remove(existing);
        }

        Records.Insert(0, newRecord);

        // Limit to 10 records
        while (Records.Count > 10)
        {
            Records.RemoveAt(Records.Count - 1);
        }

        SaveRecords();
        HistoryChanged?.Invoke();
    }

    public void RemoveRecord(Guid id)
    {
        var target = Records.FirstOrDefault(r => r.Id == id);
        if (target != null)
        {
            Records.Remove(target);
            SaveRecords();
            HistoryChanged?.Invoke();
        }
    }

    public void ClearAll()
    {
        Records.Clear();
        SaveRecords();
        HistoryChanged?.Invoke();
    }

    private void LoadRecords()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                string json = File.ReadAllText(_storagePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new JsonStringEnumConverter());
                var items = JsonSerializer.Deserialize<List<RecentCompareRecord>>(json, options);
                if (items != null)
                {
                    Records.Clear();
                    bool needsResave = false;
                    foreach (var item in items)
                    {
                        var eff = item.EffectiveType;
                        if (item.Type != eff)
                        {
                            item.Type = eff;
                            needsResave = true;
                        }
                        Records.Add(item);
                    }

                    if (needsResave)
                    {
                        SaveRecords();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RecentHistoryManager.LoadRecords error: {ex.Message}");
        }
    }

    private void SaveRecords()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            string json = JsonSerializer.Serialize(Records.ToList(), options);
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Ignore disk write errors silently
        }
    }
}
