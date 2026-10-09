using System.Diagnostics;
using System.IO.Compression;
using System.Text;

// Args: <zipPath> <targetDir> <mainExeName> <mainPid>
// aion2-overlay fork: the updater tells what it does (window + update.log), retries files that are briefly locked
// (antivirus scanning the new exe, the old app still exiting), never closes silently on an error, always starts the
// meter again, and stages a new copy of itself (AionDpsMeter.Updater.exe.new) that the meter swaps in on its next start.
Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: AionDpsMeter.Updater <zipPath> <targetDir> <mainExeName> <mainPid>");
    return 1;
}

var zipPath    = args[0];
var targetDir  = args[1];
var mainExe    = args[2];
var mainPid    = int.Parse(args[3]);
var logPath    = Path.Combine(targetDir, "update.log");

void Say(string text)
{
    Console.WriteLine(text);
    try { File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {text}{Environment.NewLine}"); } catch { }
}

Say($"Обновление AION2 Overlay: {zipPath} → {targetDir}");

try
{
    var proc = Process.GetProcessById(mainPid);
    Say("Жду, пока метр закроется…");
    if (!proc.WaitForExit(30_000))
    {
        Say("Метр не закрылся за 30 секунд — закрываю принудительно.");
        proc.Kill();
        proc.WaitForExit(5_000);
    }
}
catch (ArgumentException)
{
    // already gone
}
catch (Exception ex)
{
    Say($"Не удалось дождаться закрытия метра: {ex.Message}");
}

await Task.Delay(500);

var failed = new List<string>();
var extracted = 0;
try
{
    using var archive = ZipFile.OpenRead(zipPath);

    string stripPrefix = string.Empty;
    var topDirs = archive.Entries
        .Select(e => e.FullName.Split('/')[0])
        .Distinct()
        .ToList();
    if (topDirs.Count == 1 && !topDirs[0].Contains('.'))
        stripPrefix = topDirs[0] + "/";

    foreach (var entry in archive.Entries)
    {
        var relativePath = entry.FullName;
        if (!string.IsNullOrEmpty(stripPrefix) && relativePath.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
            relativePath = relativePath[stripPrefix.Length..];

        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.EndsWith('/'))
            continue;

        var destPath = Path.Combine(targetDir, relativePath);
        // The running updater cannot overwrite itself: its new copy waits next to it for the meter to swap in.
        if (Path.GetFileName(relativePath).Equals("AionDpsMeter.Updater.exe", StringComparison.OrdinalIgnoreCase))
            destPath += ".new";

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        Exception? last = null;
        for (var attempt = 1; attempt <= 30; attempt++) // up to 30 s: an antivirus can scan the 190 MB exe for a while
        {
            try
            {
                entry.ExtractToFile(destPath, overwrite: true);
                last = null;
                extracted++;
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                last = ex;
                await Task.Delay(1_000);
            }
        }
        if (last is not null)
        {
            failed.Add(relativePath);
            Say($"Не удалось заменить {relativePath}: {last.Message}");
        }
    }
}
catch (Exception ex)
{
    Say($"Не удалось открыть архив обновления: {ex.Message}");
    failed.Add(Path.GetFileName(zipPath));
}

if (failed.Count == 0)
{
    Say($"Готово: обновлено файлов — {extracted}.");
    try { File.Delete(zipPath); } catch { }
}

var exePath = Path.Combine(targetDir, mainExe);
if (File.Exists(exePath))
{
    Say("Запускаю метр…");
    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
}

if (failed.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Обновление не завершено. Частые причины:");
    Console.WriteLine(" • папка программы защищена (Program Files) или синхронизируется OneDrive — перенеси её, например, в C:\\Games\\Aion2Overlay;");
    Console.WriteLine(" • антивирус заблокировал файл — добавь папку в исключения;");
    Console.WriteLine(" • можно обновиться вручную: скачай архив со страницы релизов и распакуй его поверх папки (метр должен быть закрыт).");
    Console.WriteLine($"Подробности — в файле {logPath}");
    Console.WriteLine("Нажми любую клавишу, чтобы закрыть это окно.");
    try { Console.ReadKey(true); } catch { }
    return 2;
}

return 0;
