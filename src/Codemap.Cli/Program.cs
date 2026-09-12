using Codemap.Core;
using Codemap.Cli;
using System.Text.Json;

var arguments = args.ToList();
if (arguments.Contains("--version", StringComparer.Ordinal))
{
	var informationalVersion = typeof(Program).Assembly
		.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
		.OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
		.Select(attribute => attribute.InformationalVersion)
		.FirstOrDefault();
	Console.WriteLine((informationalVersion ?? typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown").Split('+')[0]);
	return 0;
}

if (arguments.Contains("--help", StringComparer.Ordinal))
{
	PrintHelp();
	return 0;
}

var root = GetOption(arguments, "--root") ?? Directory.GetCurrentDirectory();
var remote = GetOption(arguments, "--remote");
var configPath = GetOption(arguments, "--config") ?? FindDefaultConfig(root);
var include = GetOption(arguments, "--include");
var ignore = GetOption(arguments, "--ignore");

var options = new PackOptions
{
	RootDirectory = root,
	OutputPath = "codemap-output.md",
	IncludePatterns = ["**/*"],
	IncludeFileSummary = true,
	IncludeDirectoryStructure = true,
	ShowLineNumbers = arguments.Contains("--line-numbers"),
	RemoveComments = arguments.Contains("--remove-comments"),
	RemoveEmptyLines = arguments.Contains("--remove-empty-lines"),
	TokenBudget = GetIntOption(arguments, "--token-budget"),
	MaxFileSizeBytes = GetLongOption(arguments, "--max-file-size"),
	EnableSecurityCheck = arguments.Contains("--security-check"),
	IncludeGitDiffs = arguments.Contains("--include-diffs"),
	IncludeGitLogs = arguments.Contains("--include-logs"),
	GitLogCount = GetIntOption(arguments, "--include-logs-count") ?? 20,
	SplitOutputBytes = GetIntOption(arguments, "--split-output"),
	Format = ParseFormat(GetOption(arguments, "--format"))
};

try
{
	var sourceRoot = remote is null
		? root
		: await RepositorySource.ResolveAsync(remote, GetOption(arguments, "--remote-branch"), CancellationToken.None);
	root = sourceRoot;
	options = options with { RootDirectory = sourceRoot };
	if (configPath is not null)
	{
		options = (await PackConfiguration.LoadAsync(configPath)).ApplyTo(options);
	}

	options = options with
	{
		OutputPath = GetOption(arguments, "--output") ?? options.OutputPath,
		Format = GetOption(arguments, "--format") is { } format ? ParseFormat(format) : options.Format,
		IncludePatterns = include is null ? options.IncludePatterns : SplitPatterns(include),
		IgnorePatterns = ignore is null ? options.IgnorePatterns : SplitPatterns(ignore),
		IncludeFileSummary = arguments.Contains("--no-summary") ? false : options.IncludeFileSummary,
		IncludeDirectoryStructure = arguments.Contains("--no-tree") ? false : options.IncludeDirectoryStructure,
		ShowLineNumbers = arguments.Contains("--line-numbers") || options.ShowLineNumbers,
		RemoveComments = arguments.Contains("--remove-comments") || options.RemoveComments,
		RemoveEmptyLines = arguments.Contains("--remove-empty-lines") || options.RemoveEmptyLines,
		TokenBudget = GetIntOption(arguments, "--token-budget") ?? options.TokenBudget
		,MaxFileSizeBytes = GetLongOption(arguments, "--max-file-size") ?? options.MaxFileSizeBytes
		,EnableSecurityCheck = arguments.Contains("--security-check") || options.EnableSecurityCheck
		,IncludeGitDiffs = arguments.Contains("--include-diffs") || options.IncludeGitDiffs
		,IncludeGitLogs = arguments.Contains("--include-logs") || options.IncludeGitLogs
		,SplitOutputBytes = GetIntOption(arguments, "--split-output") ?? options.SplitOutputBytes
	};

	var result = await new CodePacker().PackAsync(options);
	if (options.SplitOutputBytes is { } splitSize && splitSize > 0 && result.Content.Length > splitSize)
	{
		var chunks = result.Content.Chunk(splitSize).ToArray();
		for (var index = 0; index < chunks.Length; index++)
		{
			await File.WriteAllTextAsync($"{options.OutputPath}.{index + 1}", new string(chunks[index]));
		}
	}
	else
	{
		await File.WriteAllTextAsync(options.OutputPath, result.Content);
	}
	Console.WriteLine($"Packed {result.Files.Count} files into {options.OutputPath} ({result.TokenCount} tokens).");
	if (result.ExcludedFiles is { Count: > 0 })
	{
		Console.WriteLine($"Excluded {result.ExcludedFiles.Count} files with security findings: {string.Join(", ", result.ExcludedFiles)}");
	}
	if (arguments.Contains("--watch"))
	{
		using var watcher = new FileSystemWatcher(options.RootDirectory) { IncludeSubdirectories = true, EnableRaisingEvents = true };
		FileSystemEventHandler notify = (_, _) => Console.WriteLine("Source changed. Run codemap again to refresh output.");
		watcher.Changed += notify;
		watcher.Created += notify;
		watcher.Deleted += notify;
		Console.WriteLine("Watching for changes. Press Ctrl+C to stop.");
		await Task.Delay(Timeout.InfiniteTimeSpan);
	}
	return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or DirectoryNotFoundException or JsonException or FormatException)
{
	Console.Error.WriteLine($"codemap: {exception.Message}");
	return 1;
}

static string? GetOption(IReadOnlyList<string> arguments, string name)
{
	for (var index = 0; index + 1 < arguments.Count; index++)
	{
		if (arguments[index].Equals(name, StringComparison.Ordinal))
		{
			return arguments[index + 1];
		}
	}

	return null;
}

static IReadOnlyList<string> SplitPatterns(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static int? GetIntOption(IReadOnlyList<string> arguments, string name)
{
	var value = GetOption(arguments, name);
	return value is null ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}

static long? GetLongOption(IReadOnlyList<string> arguments, string name)
{
	var value = GetOption(arguments, name);
	return value is null ? null : long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}

static string? FindDefaultConfig(string root)
{
	foreach (var name in new[] { "codemap.json", "codemap.config.json" })
	{
		var path = Path.Combine(root, name);
		if (File.Exists(path)) return path;
	}

	return null;
}

static OutputFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
{
	"markdown" or "md" => OutputFormat.Markdown,
	"plain" or "txt" => OutputFormat.Plain,
	"json" => OutputFormat.Json,
	_ => OutputFormat.Markdown
};

static void PrintHelp()
{
	Console.WriteLine("codemap - package repositories into AI-friendly output");
	Console.WriteLine();
	Console.WriteLine("Usage:");
	Console.WriteLine("  codemap [options]");
	Console.WriteLine();
	Console.WriteLine("Source:");
	Console.WriteLine("  --root <path>                 Source directory (default: current directory)");
	Console.WriteLine("  --remote <url|owner/repo>     Clone a remote Git repository");
	Console.WriteLine("  --remote-branch <branch>      Branch to clone");
	Console.WriteLine();
	Console.WriteLine("Selection:");
	Console.WriteLine("  --include <patterns>          Comma-separated include globs");
	Console.WriteLine("  --ignore <patterns>           Comma-separated ignore globs");
	Console.WriteLine("  --max-file-size <bytes>       Skip larger files");
	Console.WriteLine("  --config <path>               Configuration JSON file");
	Console.WriteLine();
	Console.WriteLine("Output:");
	Console.WriteLine("  --format <xml|markdown|json|plain>");
	Console.WriteLine("  --output <path>               Output file (default: codemap-output.md)");
	Console.WriteLine("  --no-summary                  Omit summary metadata");
	Console.WriteLine("  --no-tree                     Omit directory structure");
	Console.WriteLine("  --split-output <bytes>        Split large output into numbered files");
	Console.WriteLine();
	Console.WriteLine("Transformations and limits:");
	Console.WriteLine("  --security-check              Exclude files with DevSkim findings");
	Console.WriteLine("  --remove-comments             Remove common source comments");
	Console.WriteLine("  --remove-empty-lines          Remove blank lines");
	Console.WriteLine("  --line-numbers                Add line numbers");
	Console.WriteLine("  --token-budget <count>        Fail when rendered output exceeds count");
	Console.WriteLine();
	Console.WriteLine("Git and workflow:");
	Console.WriteLine("  --include-diffs               Include git diff");
	Console.WriteLine("  --include-logs                Include recent git commits");
	Console.WriteLine("  --include-logs-count <count>  Number of commits (default: 20)");
	Console.WriteLine("  --watch                       Report source changes");
	Console.WriteLine("  --version                     Show the tool version");
	Console.WriteLine("  --help                       Show this help");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  codemap --root . --format markdown --output repository.md");
	Console.WriteLine("  codemap --include \"**/*.cs\" --security-check");
	Console.WriteLine("  codemap --remote microsoft/generative-ai-for-beginners --remote-branch main");
	Console.WriteLine();
	Console.WriteLine("More documentation: README.md");
}
