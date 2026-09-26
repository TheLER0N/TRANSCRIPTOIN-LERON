using System;
using System.Collections.Generic;
using System.IO;

namespace Leron.Audio.Services;

public static class ModelLocator
{
    public static string? FindWhisperCli()
    {
        foreach (var root in EnumerateRoots())
        {
            var direct = Path.Combine(root, "whisper-cli.exe");
            if (File.Exists(direct)) return direct;

            var inBuild = Path.Combine(root, "build", "whisper-cli.exe");
            if (File.Exists(inBuild)) return inBuild;
        }
        return null;
    }

    public static string? FindModel()
    {
        foreach (var root in EnumerateRoots())
        {
            var preferred = Path.Combine(root, "ggml-large-v3-turbo.bin");
            if (File.Exists(preferred)) return preferred;

            if (Directory.Exists(root))
            {
                foreach (var candidate in Directory.GetFiles(root, "ggml-*.bin"))
                    return candidate;
            }
        }
        return null;
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            yield return dir.FullName;
            dir = dir.Parent;
        }
    }
}