using Nalix.Abstractions.Exceptions;
using Dashboard.Application.Options;
using Dashboard.Application.State;

namespace Dashboard.Infrastructure.Security;

internal sealed class ServerPublicKeyResolver : IServerPublicKeyResolver
{
    private readonly IDashboardStateWriter _state;

    public ServerPublicKeyResolver(IDashboardStateWriter state) => _state = state;

    public string Resolve(DashboardOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ServerPublicKey))
        {
            _state.Log("DEBUG", "Server public key resolved source=configuration.");
            return options.ServerPublicKey.Trim();
        }

        string path = ResolveSharedFile(options.ServerPublicKeyPath, "certificate.public");
        foreach (string line in File.ReadLines(path))
        {
            string trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('#'))
            {
                _state.Log("DEBUG", $"Server public key resolved source=file path=\"{path}\".");
                return trimmed;
            }
        }

        _state.Log("ERROR", $"Server public key missing path=\"{path}\".");
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
