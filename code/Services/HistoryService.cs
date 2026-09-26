// code/Services/HistoryService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Leron.Audio.Services;

public sealed class HistorySegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}

public sealed class HistoryRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public double DurationSec { get; set; }
    public string Text { get; set; } = string.Empty;
    public List<HistorySegment> Segments { get; set; } = new();
    public string Model { get; set; } = string.Empty;
    public string Runtime { get; set; } = string.Empty;
}

/// Локальная история диктовок: history.jsonl рядом с exe (JSONL, по записи на строку).
public sealed class HistoryService
{
    private readonly object _lock = new();
    private readonly string _path;

    public HistoryService()
    {
        _path = Path.Combine(AppContext.BaseDirectory, "history.jsonl");
    }

    public string FilePath => _path;

    public void Append(HistoryRecord record)
    {
        if (record is null) return;
        try
        {
            var line = JsonSerializer.Serialize(record);
            lock (_lock) File.AppendAllText(_path, line + Environment.NewLine);
        }
        catch
        {
            // Нет прав/диск занят: история не критична, диктовка важнее
        }
    }

    public IReadOnlyList<HistoryRecord> LoadAll()
    {
        var result = new List<HistoryRecord>();
        lock (_lock)
        {
            if (!File.Exists(_path)) return result;
            foreach (var line in File.ReadLines(_path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = JsonSerializer.Deserialize<HistoryRecord>(line);
                    if (rec is not null) result.Add(rec);
                }
                catch
                {
                    // Битая строка пропускается, файл не лечим
                }
            }
        }
        result.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
        return result;
    }

    public IReadOnlyList<HistoryRecord> Search(string query)
    {
        var all = LoadAll();
        if (string.IsNullOrWhiteSpace(query)) return all;
        return all
            .Where(r => r.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        }
    }

    public void ClearRange(DateTime from, DateTime to)
    {
        lock (_lock)
        {
            var kept = LoadAll().Where(r => r.Timestamp < from || r.Timestamp > to).ToList();
            try
            {
                using var sw = new StreamWriter(_path, false);
                foreach (var r in kept) sw.WriteLine(JsonSerializer.Serialize(r));
            }
            catch { }
        }
    }

    public int CountToday()
    {
        var today = DateTime.Now.Date;
        return LoadAll().Count(r => r.Timestamp.Date == today);
    }

    public int WordsToday()
    {
        var today = DateTime.Now.Date;
        return LoadAll()
            .Where(r => r.Timestamp.Date == today)
            .Sum(r => r.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length);
    }
}