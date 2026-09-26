// code/Services/RuntimeService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace Leron.Audio.Services;

public enum RuntimeMode
{
    Auto,
    Cpu,
    Vulkan,
    Cuda
}

/// Определение GPU и выбор бэкенда Whisper: Авто/CPU/Vulkan/CUDA.
public sealed class RuntimeService
{
    private const string VideoClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public RuntimeService()
    {
        GpuVendor = DetectVendor();
        GpuDescription = DetectDescription();
        AvailableRuntimes = DetectAvailable();
    }

    public string GpuVendor { get; }
    public string GpuDescription { get; }
    public IReadOnlyList<string> AvailableRuntimes { get; }

    public static RuntimeMode ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "cpu" => RuntimeMode.Cpu,
        "vulkan" => RuntimeMode.Vulkan,
        "cuda" => RuntimeMode.Cuda,
        _ => RuntimeMode.Auto
    };

    /// Фактический бэкенд: ручной выбор или авторезолв по вендору GPU.
    public string ResolveBackend(RuntimeMode mode) => mode switch
    {
        RuntimeMode.Cpu => "cpu",
        RuntimeMode.Vulkan => "vulkan",
        RuntimeMode.Cuda => "cuda",
        _ => GpuVendor switch
        {
            "NVIDIA" => "cuda",
            "AMD" => "vulkan",
            "Intel" => "vulkan",
            _ => "cpu"
        }
    };

    public string ResolveBackend(string? modeText) => ResolveBackend(ParseMode(modeText));

    /// Пиннинг бэкенда в Whisper.net через RuntimeOptions
    /// (ForcedRuntimeLibrary / RuntimeLibraryOrder — что есть в пакете).
    /// Vulkan/CUDA пинятся ТОЛЬКО если их native-библиотеки обнаружены на диске:
    /// force обходит проверки совместимости лоадера, несовместимый force может
    /// уронить процесс на нативном уровне.
    public bool ApplyBackendPin(string backend)
    {
        return backend switch
        {
            "cpu" => TryPin("Cpu"),
            "vulkan" when AvailableRuntimes.Contains("vulkan") => TryPin("Vulkan"),
            "cuda" when AvailableRuntimes.Contains("cuda") => TryPin("Cuda"),
            _ => false
        };
    }

    private static bool TryPin(string enumName)
    {
        try
        {
            var asm = typeof(Whisper.net.WhisperFactory).Assembly;
            var optionsType = asm.GetTypes().FirstOrDefault(t => t.Name == "RuntimeOptions");
            var libType = asm.GetTypes().FirstOrDefault(t => t.Name == "RuntimeLibrary" && t.IsEnum);
            if (optionsType is null || libType is null) return false;
            var value = Enum.Parse(libType, enumName);

            var forced = optionsType.GetProperty("ForcedRuntimeLibrary", BindingFlags.Public | BindingFlags.Static);
            if (forced is not null && forced.PropertyType == libType)
            {
                forced.SetValue(null, value);
                return true;
            }

            var order = optionsType.GetProperty("RuntimeLibraryOrder", BindingFlags.Public | BindingFlags.Static);
            if (order is not null)
            {
                object? list = null;
                if (order.PropertyType.IsArray)
                {
                    var arr = Array.CreateInstance(libType, 1);
                    arr.SetValue(value, 0);
                    list = arr;
                }
                else if (typeof(System.Collections.IList).IsAssignableFrom(order.PropertyType))
                {
                    var inst = Activator.CreateInstance(order.PropertyType);
                    if (inst is System.Collections.IList il)
                    {
                        il.Add(value);
                        list = il;
                    }
                }
                if (list is not null)
                {
                    order.SetValue(null, list);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            // Рефлексия не нашла API этой версии пакета — оставляем авто-порядок лоадера
            return false;
        }
    }

    private static string DetectVendor()
    {
        var vendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(VideoClassKey);
            if (baseKey is not null)
            {
                foreach (var sub in baseKey.GetSubKeyNames())
                {
                    using var k = baseKey.OpenSubKey(sub);
                    var blob = ((k?.GetValue("ProviderName") as string) + " " +
                                (k?.GetValue("DriverDesc") as string)).ToUpperInvariant();
                    if (blob.Contains("NVIDIA")) vendors.Add("NVIDIA");
                    else if (blob.Contains("AMD") || blob.Contains("ADVANCED MICRO DEVICES")) vendors.Add("AMD");
                    else if (blob.Contains("INTEL")) vendors.Add("Intel");
                }
            }
        }
        catch { }
        if (vendors.Contains("NVIDIA")) return "NVIDIA";
        if (vendors.Contains("AMD")) return "AMD";
        if (vendors.Contains("Intel")) return "Intel";
        return "Unknown";
    }

    private static string DetectDescription()
    {
        var names = new List<string>();
        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(VideoClassKey);
            if (baseKey is not null)
            {
                foreach (var sub in baseKey.GetSubKeyNames())
                {
                    using var k = baseKey.OpenSubKey(sub);
                    var desc = k?.GetValue("DriverDesc") as string;
                    if (!string.IsNullOrWhiteSpace(desc) && !names.Contains(desc)) names.Add(desc);
                }
            }
        }
        catch { }
        return names.Count > 0 ? string.Join(", ", names) : "GPU не определён";
    }

    /// Наличие native-библиотек рантаймов рядом с exe / в runtimes/win-x64/native.
    private static IReadOnlyList<string> DetectAvailable()
    {
        var list = new List<string> { "cpu" };
        if (ProbeNative("ggml-vulkan.dll", "libggml-vulkan.dll", "whisper-vulkan.dll")) list.Add("vulkan");
        if (ProbeNative("ggml-cuda.dll", "libggml-cuda.dll", "whisper-cuda.dll")) list.Add("cuda");
        return list;
    }

    private static bool ProbeNative(params string[] candidates)
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native")
        };
        foreach (var root in roots)
        foreach (var name in candidates)
        {
            if (File.Exists(Path.Combine(root, name))) return true;
        }
        return false;
    }
}