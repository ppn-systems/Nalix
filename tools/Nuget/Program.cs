using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var logFile = "temp.txt";
var apiKeyFile = "api.txt";
var packageDir = @"..\..\build\bin\Release";
var defaultSource = "https://api.nuget.org/v3/index.json";
var log = new List<string>();

void Log(string message)
{
    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
    Console.WriteLine(line);
    log.Add(line);
}

void LogColor(string message, ConsoleColor color)
{
    var old = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.WriteLine(message);
    Console.ForegroundColor = old;
}

void SaveLog()
{
    File.WriteAllLines(logFile, log);
    Log($"Log saved to {logFile}");
}

Log("🚀 NuGet Package Uploader - Debug Edition 🚀");
Log($"Log file: {logFile}");

if (!CheckDotnetExists())
{
    LogColor("❌ .NET SDK not found!", ConsoleColor.Red);
    SaveLog();
    return;
}

if (!CheckWritePermission("temp_output.txt"))
{
    LogColor("❌ Cannot write to temp_output.txt!", ConsoleColor.Red);
    SaveLog();
    return;
}

string apiKey = LoadApiKey();
if (string.IsNullOrWhiteSpace(apiKey))
{
    LogColor("❌ API key is empty!", ConsoleColor.Red);
    SaveLog();
    return;
}

Console.Write("Enter NuGet source (default: https://api.nuget.org/v3/index.json): ");
var source = Console.ReadLine();
if (string.IsNullOrWhiteSpace(source)) source = defaultSource;
LogColor($"Using source: {source}", ConsoleColor.Blue);

if (!Directory.Exists(packageDir))
{
    LogColor($"❌ Directory not found: {packageDir}", ConsoleColor.Red);
    SaveLog();
    return;
}

var packages = Directory.GetFiles(packageDir, "*.nupkg");
if (packages.Length == 0)
{
    LogColor("❌ No .nupkg files found!", ConsoleColor.Red);
    SaveLog();
    return;
}

LogColor($"📦 Found {packages.Length} package(s)", ConsoleColor.Green);
bool anyFailed = false;

foreach (var pkg in packages)
{
    var fileInfo = new FileInfo(pkg);
    if (fileInfo.Length < 1024)
    {
        LogColor($"⚠ {pkg} is too small ({fileInfo.Length} bytes), skipping...", ConsoleColor.Yellow);
        anyFailed = true;
        continue;
    }

    LogColor($"⬆ Uploading {fileInfo.Name}...", ConsoleColor.Yellow);
    var result = RunPush(pkg, apiKey, source, out var error);

    if (result == 0)
    {
        LogColor($"✅ Uploaded {fileInfo.Name}", ConsoleColor.Green);
    }
    else
    {
        LogColor($"⚠ Failed to upload {fileInfo.Name}: {error}", ConsoleColor.Red);
        anyFailed = true;
    }
}

Log("==================================================");
if (!anyFailed)
    LogColor("✅ All packages uploaded successfully!", ConsoleColor.Green);
else
    LogColor($"⚠ Some packages failed. See {logFile}.", ConsoleColor.Red);
Log("==================================================");

SaveLog();
return;

// ==============================
// Helper methods below
// ==============================

bool CheckDotnetExists()
{
    try
    {
        var proc = Process.Start(new ProcessStartInfo("dotnet", "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        });
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

bool CheckWritePermission(string path)
{
    try
    {
        File.WriteAllText(path, "Test");
        File.Delete(path);
        return true;
    }
    catch
    {
        return false;
    }
}

string LoadApiKey()
{
    if (File.Exists(apiKeyFile))
    {
        var key = File.ReadAllText(apiKeyFile).Trim();
        LogColor($"✅ Loaded API Key from {apiKeyFile}", ConsoleColor.Green);
        return key;
    }
    else
    {
        Console.Write("Enter your NuGet API Key: ");
        var key = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(key))
        {
            File.WriteAllText(apiKeyFile, key.Trim());
            LogColor($"✅ Saved API Key to {apiKeyFile}", ConsoleColor.Green);
            return key.Trim();
        }
        return string.Empty;
    }
}

int RunPush(string file, string apiKey, string source, out string error)
{
    var psi = new ProcessStartInfo("dotnet", $"nuget push \"{file}\" --api-key {apiKey} --source {source} --skip-duplicate --disable-buffering --timeout 60")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var proc = Process.Start(psi);
    proc.WaitForExit();

    var stderr = proc.StandardError.ReadToEnd();
    var stdout = proc.StandardOutput.ReadToEnd();
    error = string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : stderr.Trim();
    return proc.ExitCode;
}
