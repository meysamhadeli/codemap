using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Codemap.Core;

public sealed class CodePacker
{
    private static readonly string[] DefaultExcludedDirectories = [".git", "bin", "obj", "node_modules", "dist", "coverage"];

    public async Task<PackResult> PackAsync(PackOptions options, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(options.RootDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Root directory does not exist: {root}");
        }

        var files = new List<PackedFile>();
        var sourcePaths = new List<string>();
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System,
            ReturnSpecialDirectories = false
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", enumerationOptions).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Normalize(Path.GetRelativePath(root, path));
            if (!IsIncluded(relativePath, options))
            {
                continue;
            }

            if (options.MaxFileSizeBytes is { } maxSize && new FileInfo(path).Length > maxSize)
            {
                continue;
            }

            if (!TryReadText(path, out var content))
            {
                continue;
            }

            sourcePaths.Add(relativePath);
            content = Transform(content, options);
            var lineCount = content.Length == 0 ? 0 : content.Split('\n').Length;
            files.Add(new PackedFile(relativePath, content, content.Length, lineCount, TokenCounter.Count(content)));
        }

        var git = new GitMetadata();
        var gitDiffs = options.IncludeGitDiffs ? await git.RunAsync(root, "diff", cancellationToken) : null;
        var gitLogs = options.IncludeGitLogs ? await git.RunAsync(root, $"log -n {options.GitLogCount} --oneline", cancellationToken) : null;
        var secrets = options.EnableSecurityCheck
            ? await new SecurityScanner().ScanAsync(root, sourcePaths, cancellationToken)
            : [];
        if (secrets.Count > 0) files = files.Where(file => !secrets.Contains(file.RelativePath, StringComparer.OrdinalIgnoreCase)).ToList();

        var contentOutput = Render(files, options, gitDiffs, gitLogs, secrets);
        var result = new PackResult(files, contentOutput, contentOutput.Length, TokenCounter.Count(contentOutput), gitDiffs, gitLogs, secrets);
        if (options.TokenBudget is { } budget && result.EstimatedTokenCount > budget)
        {
            throw new InvalidOperationException($"Packed output exceeds token budget of {budget}.");
        }

        return result;
    }

    private static bool IsIncluded(string relativePath, PackOptions options)
    {
        var segments = relativePath.Split('/');
        if (segments.Any(segment => DefaultExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        var excluded = false;
        foreach (var pattern in options.ExcludePatterns
            .Concat(ExcludeFileLoader.Load(options.RootDirectory, ".gitignore", ".ignore")))
        {
            var negated = pattern.StartsWith('!');
            var value = negated ? pattern[1..] : pattern;
            if (Matches(relativePath, value, options.RootDirectory)) excluded = !negated;
        }
        if (excluded) return false;

        return options.IncludePatterns.Count == 0 || options.IncludePatterns.Any(pattern => Matches(relativePath, pattern, options.RootDirectory));
    }

    private static bool Matches(string path, string pattern, string rootDirectory)
    {
        var normalized = Normalize(pattern).TrimStart('/').TrimEnd('/');
        if (normalized.Length == 0)
        {
            return false;
        }

        var literalDirectory = !HasWildcard(normalized)
            && Directory.Exists(Path.Combine(rootDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (literalDirectory)
        {
            return path.Equals(normalized, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase);
        }

        var expression = Regex.Escape(normalized).Replace("\\*\\*/", "(?:.*/)?").Replace("\\*\\*", ".*").Replace("\\*", "[^/]*");
        expression = expression.Replace("\\?", "[^/]");
        if (!normalized.Contains('/'))
        {
            expression = "(?:.*/)?" + expression;
        }

        expression = "^" + expression + (pattern.EndsWith('/') ? "(?:/.*)?" : string.Empty) + "$";
        return Regex.IsMatch(path, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool HasWildcard(string pattern) => pattern.Contains('*') || pattern.Contains('?');

    private static string Transform(string content, PackOptions options)
    {
        if (options.RemoveComments)
        {
            content = Regex.Replace(content, @"(^|\s)//.*$", "$1", RegexOptions.Multiline);
            content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        }

        if (options.RemoveEmptyLines)
        {
            content = string.Join('\n', content.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        if (options.ShowLineNumbers)
        {
            content = string.Join('\n', content.Split('\n').Select((line, index) => $"{index + 1,4}: {line}"));
        }

        return content;
    }

    private static string Render(IReadOnlyList<PackedFile> files, PackOptions options, string? gitDiffs, string? gitLogs, IReadOnlyList<string> excludedFiles)
    {
        return options.Format switch
        {
            OutputFormat.Json => JsonSerializer.Serialize(new
            {
                summary = new
                {
                    fileCount = files.Count,
                    characterCount = files.Sum(file => file.CharacterCount),
                    tokenCount = files.Sum(file => file.TokenCount)
                },
                files,
                excludedFiles,
                gitDiffs,
                gitLogs
            }, new JsonSerializerOptions { WriteIndented = true }),
            OutputFormat.Markdown => RenderMarkdown(files, options, gitDiffs, gitLogs),
            OutputFormat.Plain => RenderPlain(files, options),
            _ => RenderXml(files, options, gitDiffs, gitLogs)
        };
    }

    private static string RenderXml(IReadOnlyList<PackedFile> files, PackOptions options, string? gitDiffs, string? gitLogs)
    {
        var root = new XElement("codemap",
            options.IncludeFileSummary ? new XElement("file_summary",
                new XAttribute("count", files.Count),
                new XAttribute("characters", files.Sum(file => file.CharacterCount)),
                new XAttribute("tokens", files.Sum(file => file.TokenCount))) : null,
            options.IncludeDirectoryStructure ? new XElement("directory_structure", files.Select(file => new XElement("file", file.RelativePath))) : null,
            new XElement("files", files.Select(file => new XElement("file", new XAttribute("path", file.RelativePath), new XCData(file.Content)))),
            gitDiffs is null ? null : new XElement("git_diffs", new XCData(gitDiffs)),
            gitLogs is null ? null : new XElement("git_logs", new XCData(gitLogs)));
        return root.ToString(SaveOptions.None);
    }

    private static string RenderMarkdown(IReadOnlyList<PackedFile> files, PackOptions options, string? gitDiffs, string? gitLogs)
    {
        var builder = new StringBuilder("# Codemap\n\n");
        if (options.IncludeFileSummary)
        {
            builder.AppendLine("## Repository Summary");
            builder.AppendLine($"- Files: {files.Count}");
            builder.AppendLine($"- Characters: {files.Sum(file => file.CharacterCount)}");
            builder.AppendLine($"- Tokens: {files.Sum(file => file.TokenCount)}");
            builder.AppendLine();
        }
        if (options.IncludeDirectoryStructure)
        {
            builder.AppendLine("## Files");
            builder.AppendLine(string.Join('\n', files.Select(file => $"- `{file.RelativePath}` ({file.TokenCount} tokens)")));
        }
        foreach (var file in files)
        {
            builder.AppendLine($"\n## {file.RelativePath}\n\n```{LanguageFor(file.RelativePath)}\n{file.Content}\n```");
        }
        if (gitDiffs is not null) builder.AppendLine($"\n## Git diff\n\n```diff\n{gitDiffs}\n```");
        if (gitLogs is not null) builder.AppendLine($"\n## Git log\n\n```text\n{gitLogs}\n```");
        return builder.ToString();
    }

    private static string RenderPlain(IReadOnlyList<PackedFile> files, PackOptions options)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            builder.AppendLine($"===== {file.RelativePath} =====");
            builder.AppendLine(file.Content);
        }
        return builder.ToString();
    }

    private static bool TryReadText(string path, out string content)
    {
        content = string.Empty;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Contains((byte)0)) return false;
            content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string LanguageFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".cs" => "csharp", ".js" or ".jsx" => "javascript", ".ts" or ".tsx" => "typescript", ".py" => "python",
        ".java" => "java", ".go" => "go", ".rs" => "rust", ".json" => "json", ".html" => "html", ".css" => "css",
        ".xml" => "xml", ".md" => "markdown", ".sh" or ".bash" => "bash", _ => string.Empty
    };

    private static string Normalize(string path) => path.Replace('\\', '/');
}
