using System.Diagnostics;

namespace Codemap.Cli;

internal static class RepositorySource
{
    public static async Task<string> ResolveAsync(string? remote, string? branch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remote))
        {
            return Directory.GetCurrentDirectory();
        }

        var destination = Path.Combine(Path.GetTempPath(), $"codemap-remote-{Guid.NewGuid():N}");
        var arguments = $"clone --depth 1{(string.IsNullOrWhiteSpace(branch) ? string.Empty : $" --branch {Quote(branch)}")} {Quote(NormalizeRemote(remote))} {Quote(destination)}";
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start git.");
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Unable to clone repository: {error.Trim()}");
        }

        return destination;
    }

    private static string NormalizeRemote(string remote) => remote.Contains('/') && !remote.Contains("://") ? $"https://github.com/{remote}.git" : remote;
    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
