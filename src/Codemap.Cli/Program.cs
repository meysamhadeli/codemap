using Codemap.Core;
using Codemap.Cli;
using System.Text.Json;
using TextCopy;

var arguments = args.ToList();
var applyPatchPath = GetOption(arguments, "--apply", "-a");
if (applyPatchPath is not null)
{
	var applyRoot = Directory.GetCurrentDirectory();
	return await PatchApplier.ApplyAsync(
		applyPatchPath,
		applyRoot,
		CancellationToken.None);
}

var outputCommand = arguments.FirstOrDefault() switch
{
	"stdout" => "stdout",
	"clipboard" => "clipboard",
	_ when HasFlag(arguments, "--stdout") && HasFlag(arguments, "--clipboard") => "invalid",
	_ when HasFlag(arguments, "--stdout") => "stdout",
	_ when HasFlag(arguments, "--clipboard") => "clipboard",
	_ => "file"
};
if (outputCommand is not "file")
{
	arguments.RemoveAt(0);
}

if (HasFlag(arguments, "--version", "-v"))
{
	var informationalVersion = typeof(Program).Assembly
		.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
		.OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
		.Select(attribute => attribute.InformationalVersion)
		.FirstOrDefault();
	Console.WriteLine((informationalVersion ?? typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown").Split('+')[0]);
	return 0;
}

if (HasFlag(arguments, "--help", "-h"))
{
	PrintHelp();
	return 0;
}

var root = Directory.GetCurrentDirectory();
var patchMode = HasFlag(arguments, "--patch", "-p");
var skillSpecifications = GetOptions(arguments, "--skills", "-s");
var remote = GetOption(arguments, "--remote", "-r");
var configPath = GetOption(arguments, "--config", "-c") ?? FindDefaultConfig(root);
var include = GetOption(arguments, "--include", "-i");
var exclude = GetOption(arguments, "--exclude", "-e");

var options = new PackOptions
{
	RootDirectory = root,
	OutputPath = patchMode ? "codemap-patch-context.md" : "codemap-output.md",
	IncludePatterns = ["**/*"],
	IncludeFileSummary = true,
	IncludeDirectoryStructure = true,
	ShowLineNumbers = arguments.Contains("--line-numbers"),
	RemoveComments = arguments.Contains("--remove-comments"),
	RemoveEmptyLines = arguments.Contains("--remove-empty-lines"),
	TokenBudget = GetIntOption(arguments, "--token-budget", "-t"),
	MaxFileSizeBytes = GetLongOption(arguments, "--max-file-size", "-m"),
	EnableSecurityCheck = arguments.Contains("--security-check"),
	IncludeGitDiffs = arguments.Contains("--include-diffs"),
	IncludeGitLogs = arguments.Contains("--include-logs"),
	GitLogCount = GetIntOption(arguments, "--include-logs-count") ?? 20,
	SplitOutputBytes = GetIntOption(arguments, "--split-output"),
	Format = ParseFormat(GetOption(arguments, "--format", "-f"))
};

try
{
	if (outputCommand is "invalid")
	{
		throw new InvalidOperationException("Use only one of --stdout or --clipboard.");
	}

	var sourceRoot = remote is null
		? root
		: await RepositorySource.ResolveAsync(remote, GetOption(arguments, "--remote-branch", "-b"), CancellationToken.None);
	root = sourceRoot;
	options = options with { RootDirectory = sourceRoot };
	if (configPath is not null)
	{
		options = (await PackConfiguration.LoadAsync(configPath)).ApplyTo(options);
	}

	options = options with
	{
		OutputPath = GetOption(arguments, "--output", "-o") ?? options.OutputPath,
		Format = GetOption(arguments, "--format", "-f") is { } format ? ParseFormat(format) : options.Format,
		IncludePatterns = include is null ? options.IncludePatterns : SplitPatterns(include),
		ExcludePatterns = exclude is null ? options.ExcludePatterns : SplitPatterns(exclude),
		IncludeFileSummary = arguments.Contains("--no-summary") ? false : options.IncludeFileSummary,
		IncludeDirectoryStructure = arguments.Contains("--no-tree") ? false : options.IncludeDirectoryStructure,
		ShowLineNumbers = arguments.Contains("--line-numbers") || options.ShowLineNumbers,
		RemoveComments = arguments.Contains("--remove-comments") || options.RemoveComments,
		RemoveEmptyLines = arguments.Contains("--remove-empty-lines") || options.RemoveEmptyLines,
		TokenBudget = GetIntOption(arguments, "--token-budget", "-t") ?? options.TokenBudget
		,MaxFileSizeBytes = GetLongOption(arguments, "--max-file-size", "-m") ?? options.MaxFileSizeBytes
		,EnableSecurityCheck = arguments.Contains("--security-check") || options.EnableSecurityCheck
		,IncludeGitDiffs = arguments.Contains("--include-diffs") || options.IncludeGitDiffs
		,IncludeGitLogs = arguments.Contains("--include-logs") || options.IncludeGitLogs
		,SplitOutputBytes = GetIntOption(arguments, "--split-output") ?? options.SplitOutputBytes
	};

	var result = await new CodePacker().PackAsync(options);
	var skills = await SkillContext.LoadAsync(root, skillSpecifications, CancellationToken.None);
	var content = patchMode ? PatchContext.Wrap(result.Content, options.Format) : result.Content;
	content = SkillContext.Wrap(content, options.Format, skills);
	if (outputCommand is not "file" && options.SplitOutputBytes is > 0)
	{
		throw new InvalidOperationException("The stdout and clipboard commands cannot be combined with --split-output.");
	}

	if (outputCommand is "stdout")
	{
		Console.Write(content);
	}
	else if (outputCommand is "clipboard")
	{
		await CopyToClipboardAsync(content);
		Console.Error.WriteLine($"Copied {result.Files.Count} files to the clipboard ({result.TokenCount} tokens).");
	}
	else if (options.SplitOutputBytes is { } splitSize && splitSize > 0 && content.Length > splitSize)
	{
		var chunks = content.Chunk(splitSize).ToArray();
		for (var index = 0; index < chunks.Length; index++)
		{
			await File.WriteAllTextAsync($"{options.OutputPath}.{index + 1}", new string(chunks[index]));
		}
	}
	else
	{
		await File.WriteAllTextAsync(options.OutputPath, content);
	}
	if (outputCommand is "file")
	{
		Console.WriteLine($"Packed {result.Files.Count} files into {options.OutputPath} ({result.TokenCount} tokens).");
	}
	if (result.ExcludedFiles is { Count: > 0 })
	{
		Console.Error.WriteLine($"Excluded {result.ExcludedFiles.Count} files with security findings: {string.Join(", ", result.ExcludedFiles)}");
	}
	if (HasFlag(arguments, "--watch", "-w"))
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

static bool HasFlag(IReadOnlyList<string> arguments, params string[] names) =>
	names.Any(name => arguments.Contains(name, StringComparer.Ordinal));

static async Task CopyToClipboardAsync(string content)
{
	await ClipboardService.SetTextAsync(content);
}

static string? GetOption(IReadOnlyList<string> arguments, params string[] names)
{
	for (var index = 0; index + 1 < arguments.Count; index++)
	{
		if (names.Any(name => arguments[index].Equals(name, StringComparison.Ordinal)))
		{
			return arguments[index + 1];
		}
	}

	return null;
}

static IReadOnlyList<string> GetOptions(IReadOnlyList<string> arguments, params string[] names) =>
	arguments
		.Select((argument, index) => (argument, index))
		.Where(item => names.Contains(item.argument, StringComparer.Ordinal) && item.index + 1 < arguments.Count)
		.Select(item => arguments[item.index + 1])
		.ToArray();

static IReadOnlyList<string> SplitPatterns(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static int? GetIntOption(IReadOnlyList<string> arguments, params string[] names)
{
	var value = GetOption(arguments, names);
	return value is null ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}

static long? GetLongOption(IReadOnlyList<string> arguments, params string[] names)
{
	var value = GetOption(arguments, names);
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
	"xml" => OutputFormat.Xml,
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
	Console.WriteLine("  codemap stdout [options]       Print packed content to stdout");
	Console.WriteLine("  codemap clipboard [options]    Copy packed content to clipboard");
	Console.WriteLine();
	Console.WriteLine("Source:");
	Console.WriteLine("  -r, --remote <url|owner/repo> Clone a remote Git repository");
	Console.WriteLine("  -b, --remote-branch <branch>  Branch to clone");
	Console.WriteLine();
	Console.WriteLine("Selection:");
	Console.WriteLine("  -i, --include <patterns>      Files, folders, or comma-separated globs");
	Console.WriteLine("  -e, --exclude <patterns>      Files, folders, or comma-separated globs");
	Console.WriteLine("  -m, --max-file-size <bytes>   Skip larger files");
	Console.WriteLine("  -c, --config <path>            Configuration JSON file");
	Console.WriteLine();
	Console.WriteLine("Output:");
	Console.WriteLine("  -f, --format <xml|markdown|json|plain>");
	Console.WriteLine("  -o, --output <path>           Output file (default: codemap-output.md)");
	Console.WriteLine("  --stdout                      Print packed content to stdout");
	Console.WriteLine("  --clipboard                   Copy packed content to clipboard");
	Console.WriteLine("  -a, --apply <patch-file>      Preview, approve, and apply a Git diff");
	Console.WriteLine("  --no-summary                  Omit summary metadata");
	Console.WriteLine("  --no-tree                     Omit directory structure");
	Console.WriteLine("  --split-output <bytes>        Split large output into numbered files");
	Console.WriteLine("  -p, --patch                   Add patch-generation instructions in selected format");
	Console.WriteLine("  -s, --skills <names|paths>     Load named or explicit Skills; comma-separated");
	Console.WriteLine();
	Console.WriteLine("Transformations and limits:");
	Console.WriteLine("  --security-check              Exclude files with DevSkim findings");
	Console.WriteLine("  --remove-comments             Remove common source comments");
	Console.WriteLine("  --remove-empty-lines          Remove blank lines");
	Console.WriteLine("  --line-numbers                Add line numbers");
	Console.WriteLine("  -t, --token-budget <count>    Fail when rendered output exceeds count");
	Console.WriteLine();
	Console.WriteLine("Git and workflow:");
	Console.WriteLine("  --include-diffs               Include git diff");
	Console.WriteLine("  --include-logs                Include recent git commits");
	Console.WriteLine("  --include-logs-count <count>  Number of commits (default: 20)");
	Console.WriteLine("  -w, --watch                   Report source changes");
	Console.WriteLine("  -v, --version                 Show the tool version");
	Console.WriteLine("  -h, --help                    Show this help");
	Console.WriteLine();
	Console.WriteLine("Examples:");
	Console.WriteLine("  codemap --format markdown --output repository.md");
	Console.WriteLine("  codemap --include \"README.md,src,tests/**/*.cs\" --exclude \"**/bin/**,**/*.generated.cs\"");
	Console.WriteLine("  codemap --include \"**/*.cs\" --security-check");
	Console.WriteLine("  codemap --remote microsoft/generative-ai-for-beginners --remote-branch main");
	Console.WriteLine();
	Console.WriteLine("More documentation: README.md");
}
