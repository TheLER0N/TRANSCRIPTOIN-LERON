// code/Services/ModelCatalogService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace Leron.Audio.Services;
public sealed record ModelInfo(string Id, string DisplayName, string FileName, string Url, int ApproxSizeMb);
/// Каталог моделей Whisper.cpp + загрузка с HuggingFace.
/// Хранилище — %LocalAppData%\LERON-AUDIO\models: папка вне bin/Debug, поэтому
/// clean-пересборки, publish и смена конфигурации больше не удаляют скачанные модели
/// (фикс бага «модель исчезает после установки»).
public sealed class ModelCatalogService
{
private const string BaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";
private const uint GgmlMagic = 0x67676d6cu; // "ggml" little-endian
private static readonly Lazy<HttpClient> Http = new(() => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
public string ModelsDir { get; }
/// Старое проблемное хранилище внутри build-output — только для миграции.
private string LegacyModelsDir { get; } = Path.Combine(AppContext.BaseDirectory, "models");
public ModelCatalogService()
{
ModelsDir = Path.Combine(
Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
"LERON-AUDIO", "models");
try
{
Directory.CreateDirectory(ModelsDir);
MigrateLegacy();
}
catch
{
// Нет прав на LocalAppData — работаем как есть, загрузки упадут с понятной ошибкой
}
}
/// Переносит уже скачанные модели из старой папки bin/.../models в стабильную.
private void MigrateLegacy()
{
if (!Directory.Exists(LegacyModelsDir)) return;
foreach (var m in Catalog)
{
var from = Path.Combine(LegacyModelsDir, m.FileName);
var to = Path.Combine(ModelsDir, m.FileName);
if (!File.Exists(from) || File.Exists(to)) continue;
try { File.Move(from, to); } catch { }
}
}
public static IReadOnlyList<ModelInfo> Catalog { get; } = BuildCatalog();
private static ModelInfo M(string id, string name, string file, int mb)
=> new(id, name, file, BaseUrl + file, mb);
private static List<ModelInfo> BuildCatalog() => new()
{
M("tiny", "Tiny — макс. скорость, базовое качество", "ggml-tiny.bin", 75),
M("base", "Base — быстрая, простое качество", "ggml-base.bin", 142),
M("small", "Small — баланс скорости и качества", "ggml-small.bin", 466),
M("medium", "Medium — высокое качество", "ggml-medium.bin", 1500),
M("large-v3", "Large v3 — максимальное качество", "ggml-large-v3.bin", 3100),
M("large-v3-turbo", "Large v3 Turbo — быстрая и точная", "ggml-large-v3-turbo.bin", 1600),
M("tiny-q5_0", "Tiny Q5 (квант, легче)", "ggml-tiny-q5_0.bin", 31),
M("base-q5_0", "Base Q5 (квант, легче)", "ggml-base-q5_0.bin", 57),
M("small-q5_0", "Small Q5 (квант, легче)", "ggml-small-q5_0.bin", 181),
M("medium-q5_0", "Medium Q5 (квант, легче)", "ggml-medium-q5_0.bin", 570),
M("large-v3-q5_0", "Large v3 Q5 (квант, легче)", "ggml-large-v3-q5_0.bin", 1100),
M("large-v3-turbo-q5_0", "Large v3 Turbo Q5 (квант, легче)", "ggml-large-v3-turbo-q5_0.bin", 540),
M("tiny-q8_0", "Tiny Q8 (квант, почти без потерь)", "ggml-tiny-q8_0.bin", 42),
M("base-q8_0", "Base Q8 (квант, почти без потерь)", "ggml-base-q8_0.bin", 78),
M("small-q8_0", "Small Q8 (квант, почти без потерь)", "ggml-small-q8_0.bin", 252),
M("medium-q8_0", "Medium Q8 (квант, почти без потерь)", "ggml-medium-q8_0.bin", 790),
M("large-v3-q8_0", "Large v3 Q8 (квант, почти без потерь)", "ggml-large-v3-q8_0.bin", 1550),
M("large-v3-turbo-q8_0", "Large v3 Turbo Q8 (квант, почти без потерь)", "ggml-large-v3-turbo-q8_0.bin", 800),
};
public IReadOnlyList<ModelInfo> Downloaded()
{
if (!Directory.Exists(ModelsDir)) return Array.Empty<ModelInfo>();
var files = new HashSet<string>(
Directory.GetFiles(ModelsDir, "ggml-*.bin").Select(Path.GetFileName)!,
StringComparer.OrdinalIgnoreCase);
return Catalog.Where(m => files.Contains(m.FileName)).ToList();
}
public bool IsDownloaded(ModelInfo info) => File.Exists(GetPath(info));
public string GetPath(ModelInfo info) => Path.Combine(ModelsDir, info.FileName);
public void Delete(ModelInfo info)
{
try { File.Delete(GetPath(info)); } catch { }
try { File.Delete(Path.Combine(LegacyModelsDir, info.FileName)); } catch { }
}
/// Скачивание с прогрессом (0..1) и отменой; битый файл не становится моделью.
/// ВАЖНО: поток записи закрывается ДО валидации magic-байтов, иначе FileStream
/// с FileShare.None держит .part и ValidateMagic падает с "used by another process".
public async Task DownloadAsync(ModelInfo info, IProgress<double>? progress, CancellationToken ct)
{
Directory.CreateDirectory(ModelsDir);
var final = GetPath(info);
var part = final + ".part";
try
{
// Стейл .part от убитого процесса — убираем, чтобы не мешал CreateNew
try { if (File.Exists(part)) File.Delete(part); } catch { }
using (var dst = new FileStream(part, FileMode.CreateNew, FileAccess.Write, FileShare.None))
{
using var response = await Http.Value.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
response.EnsureSuccessStatusCode();
var total = response.Content.Headers.ContentLength ?? -1;
using var src = await response.Content.ReadAsStreamAsync(ct);
var buffer = new byte[81920];
long copied = 0;
int read;
while ((read = await src.ReadAsync(buffer, ct)) > 0)
{
await dst.WriteAsync(buffer.AsMemory(0, read), ct);
copied += read;
if (total > 0) progress?.Report((double)copied / total);
}
await dst.FlushAsync(ct);
}
// Поток закрыт — файл свободен, валидация и переименование безопасны
ValidateMagic(part);
File.Move(part, final, overwrite: true);
progress?.Report(1.0);
}
catch
{
try { if (File.Exists(part)) File.Delete(part); } catch { }
throw;
}
}
private static void ValidateMagic(string path)
{
using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
var header = new byte[4];
var read = fs.Read(header, 0, 4);
uint magic = (uint)(header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24));
if (read != 4 || magic != GgmlMagic)
{
throw new InvalidDataException(
$"Скачанный файл не является ggml-моделью (magic {magic:X8} вместо 67676D6C).");
}
}
}