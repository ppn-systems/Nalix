using Nalix.Abstractions.Exceptions;
using Nalix.Examples.Dashboard.Application.Options;

namespace Nalix.Examples.Dashboard.Infrastructure.Security;

internal sealed class ServerPublicKeyResolver : IServerPublicKeyResolver
{
    public string Resolve(DashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ServerPublicKey))
        {
            return options.ServerPublicKey.Trim();
        }

        string path = ResolveSharedFile(options.ServerPublicKeyPath, "certificate.public");
        foreach (string line in File.ReadLines(path))
        {
            string trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
            {
                return trimmed;
            }
        }

        throw new NetworkException($"No public key was found in '{path}'.");
    }

    private static string ResolveSharedFile(string configuredPath, string fileName)
    {
        foreach (string root in EnumerateSearchRoots())
        {
            string configuredCandidate = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(root, configuredPath);

            if (File.Exists(configuredCandidate))
            {
                return configuredCandidate;
            }

            string sharedCandidate = Path.Combine(root, "shared", fileName);
            if (File.Exists(sharedCandidate))
            {
                return sharedCandidate;
            }
        }

        return Path.GetFullPath(configuredPath);
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        foreach (string seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new(seed);
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }
}
