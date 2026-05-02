using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Nalix.Codec.Security.Asymmetric;
using Nalix.Environment.IO;

namespace Nalix.Certificate;

/// <summary>
/// Industrial-grade certificate generator for the Nalix Framework.
/// Produces separate public and private identity files.
/// </summary>
[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
[SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Console output uses default culture.")]
[SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "CLI tool output doesn't require localization.")]
internal static class Program
{
    private const string PublicFileName = "certificate.public";
    private const string PrivateFileName = "certificate.private";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintHeader();

        try
        {
            // 2. Resolve system paths
            string configDir = Directories.ConfigurationDirectory;
            string privatePath = Path.Combine(configDir, PrivateFileName);
            string publicPath = Path.Combine(configDir, PublicFileName);

            // 3. Safety Check & Auto-Backup
            bool force = args.Any(a => a is "--force" or "-f");
            if (File.Exists(privatePath) || File.Exists(publicPath))
            {
                if (!force)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("⚠️  SECURITY ALERT: Existing certificates detected!");
                    Console.WriteLine("   Generating new keys will invalidate current session trust.");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("\n   Proceed and create backups of old keys? (y/N): ");
                    Console.ResetColor();

                    string? input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input) || !input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\n[ABORTED] Operation cancelled. No changes were made.");
                        return 0;
                    }
                }

                // Elite move: Automatic backup instead of simple overwrite
                CreateBackup(privatePath);
                CreateBackup(publicPath);
            }

            // 4. Core Logic: Generate high-entropy X25519 KeyPair
            X25519.X25519KeyPair pair = X25519.GenerateKeyPair();

            // 5. Display Technical Specs (Elite touch)
            PrintSecuritySpecs();
            PrintSection("SERVER IDENTITY (PRIVATE)", pair.PrivateKey.ToString(), ConsoleColor.Yellow);
            PrintSection("CLIENT PINNING (PUBLIC)", pair.PublicKey.ToString(), ConsoleColor.Cyan);

            // 6. Export files
            ExportPrivateFile(privatePath, pair.PrivateKey);
            ExportPublicFile(publicPath, pair.PublicKey);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[SUCCESS] Certificates forged and saved to standard config path:");
            Console.WriteLine($"          {configDir}");
            Console.ResetColor();

            PrintFooter(privatePath, publicPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"\n[ERROR] Certificate generation failed: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                 NALIX IDENTITY CERTIFICATE TOOL                ║");
        Console.WriteLine("║          Secure Asymmetric Key Generation (X25519)             ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintSecuritySpecs()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("-------------------------------------------------------------------------");
        Console.WriteLine("ALGORITHM: X25519 (Curve25519)          STRENGTH: 256-bit (Modern Standard)");
        Console.WriteLine("FORMAT   : UTF-8 / Raw Hex              SECURITY: High Entropy");
        Console.WriteLine("-------------------------------------------------------------------------");
        Console.ResetColor();
    }

    private static void CreateBackup(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            string backupPath = $"{path}.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            File.Move(path, backupPath, overwrite: true);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[BACKUP] Moved old file to: {Path.GetFileName(backupPath)}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[SKIPPED] Backup failed for {Path.GetFileName(path)}: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void PrintSection(string title, string content, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write($"» {title,-28} : ");
        Console.ResetColor();
        Console.WriteLine(content);
    }

    private static void ExportPrivateFile(string path, Nalix.Abstractions.Primitives.Bytes32 privateKey)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("# NALIX PRIVATE CERTIFICATE KEY");
        _ = sb.AppendLine($"# Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        _ = sb.AppendLine("# SECURITY WARNING: KEEP THIS FILE SECRET!");
        _ = sb.AppendLine("###########################################");
        _ = sb.AppendLine(privateKey.ToString());

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"[SAVE] Private key stored to : {path}");
    }

    private static void ExportPublicFile(string path, Nalix.Abstractions.Primitives.Bytes32 publicKey)
    {
        StringBuilder sb = new();
        _ = sb.AppendLine("# NALIX PUBLIC CERTIFICATE KEY");
        _ = sb.AppendLine($"# Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        _ = sb.AppendLine("# Use this key for Client Pinning (MitM protection)");
        _ = sb.AppendLine("###########################################");
        _ = sb.AppendLine(publicKey.ToString());

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"[SAVE] Public key stored to  : {path}");
    }

    private static void PrintFooter(string privatePath, string publicPath)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"Server (Private): {privatePath}");
        Console.WriteLine($"Client (Public) : {publicPath}");
        Console.WriteLine();
        Console.WriteLine("Instruction: Use 'certificate.private' in Server options and share 'certificate.public' with clients.");
        Console.ResetColor();
    }
}
