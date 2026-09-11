# codemap

codemap is a .NET 10 command-line tool and reusable C# library for turning a repository into compact, AI-friendly output. It provides deterministic discovery, Git-aware filtering, token counting, Tree-sitter compression, DevSkim security scanning, and configurable output formats.

## Installation

codemap is distributed as a .NET tool. Install the published package globally with:

```bash
dotnet tool install --global Meysamhadeli.Codemap
```

Then run it from any directory with `codemap`.

## Features: What codemap Provides

- **AI-ready repository packaging**: combine selected source files into one readable artifact.
- **Deterministic discovery**: files are processed in stable path order for repeatable output.
- **Include and ignore rules**: filter by glob patterns and repository ignore files.
- **Git awareness**: respect `.gitignore` and optionally include Git diffs and recent commits.
- **Accurate token counts**: use GPT-4-compatible `cl100k_base` tokenization per file and for the complete output.
- **Security filtering**: use the native .NET DevSkim rule engine; files with actionable findings are excluded and listed.
- **Code compression**: use Tree-sitter to keep declarations and structural code while reducing context size.
- **Content cleanup**: optionally remove comments, remove empty lines, or add line numbers.
- **Output formats**: Markdown, XML, JSON, and plain text.
- **Large-repository controls**: file-size limits, token budgets, and split output.
- **Repository sources**: pack the current directory or clone a remote Git repository and branch.
- **Developer workflow features**: watch a directory and report changes, or use the core library directly from another .NET application.

## How to Run codemap

codemap's main workflow is simple: choose a source directory, select an output format, and write the generated repository context to a file. The examples below focus on core commands.

### Help

Use the built-in help whenever you need to check available commands and options:

```bash
codemap --help
```

### Root

Pack the current directory into Markdown, which is convenient for sharing with an AI tool:

```bash
codemap \
	--root . \
	--format markdown \
	--output repository.md
```

### Include and Ignore

Use `--include` to select files and `--ignore` to remove paths from that selection:

```bash
codemap \
	--root . \
	--include "src/**/*.cs,README.md" \
	--ignore "**/bin/**,**/obj/**" \
	--format markdown \
	--output source-context.md
```

codemap also reads `.gitignore` and `.ignore` automatically.

### Compress

Use compression and cleanup options when the full repository is too large for your model's context window:

```bash
codemap \
	--root . \
	--include "src/**/*.cs" \
	--compress \
	--remove-comments \
	--remove-empty-lines \
	--token-budget 12000 \
	--format markdown \
	--output compact-context.md
```

`--compress` keeps important declarations such as classes, methods, interfaces, properties, and types while reducing implementation detail.

### Security Check

Use DevSkim to exclude files with actionable security findings before they enter the generated context:

```bash
codemap \
	--root . \
	--security-check \
	--format markdown \
	--output reviewed-context.md
```

codemap reports excluded files in the terminal. Node.js and npm are not required.

### Format

Use `--format` to choose the output that fits your workflow:

```bash
# Human- and AI-friendly document
codemap --format markdown --output repository.md

# Structured data for another program
codemap --format json --output repository.json

# XML or simple text output
codemap --format xml --output repository.xml
codemap --format plain --output repository.txt
```

### Remote

codemap can clone a repository and pack a selected branch:

```bash
codemap \
	--remote microsoft/generative-ai-for-beginners \
	--remote-branch main \
	--format markdown \
	--output remote-context.md
```

The same command accepts a complete Git URL. Git must be installed and available on `PATH` for remote repositories and Git metadata.

## Requirements

- .NET SDK 10 or newer.
- Git only when using `--remote`, `--include-diffs`, or `--include-logs`.
- Node.js and npm are not required. Security scanning is implemented with DevSkim for .NET.

## Command Reference

General form:

```text
codemap [options]
```

Show the built-in command reference at any time:

```bash
codemap --help
```

| Option | Value | Description |
| --- | --- | --- |
| `--root` | path | Source directory. Defaults to the current directory. |
| `--remote` | URL or `owner/repository` | Clone a remote Git repository into a temporary directory before packing. |
| `--remote-branch` | branch | Branch to clone when using `--remote`. |
| `--config` | path | Configuration JSON file. Without this option, codemap searches for `codemap.json` and `codemap.config.json`. |
| `--include` | comma-separated globs | Include only matching paths, for example `**/*.cs,**/*.md`. |
| `--ignore` | comma-separated globs | Add ignore patterns for this run. |
| `--format` | `xml`, `markdown`, `md`, `json`, `plain`, `txt` | Output format. Defaults to XML. |
| `--output` | path | Output file path. Defaults to `codemap-output.xml`. |
| `--no-summary` | flag | Remove file count and token summary from structured output. |
| `--no-tree` | flag | Remove the directory/file listing from structured output. |
| `--line-numbers` | flag | Prefix each output line with its line number. |
| `--remove-comments` | flag | Remove common `//` and `/* ... */` comments before rendering. |
| `--remove-empty-lines` | flag | Remove blank lines after other transformations. |
| `--compress` | flag | Extract structural declarations with Tree-sitter. |
| `--security-check` | flag | Scan original files with DevSkim and exclude files with findings. |
| `--max-file-size` | bytes | Skip files larger than this size before reading them. |
| `--token-budget` | count | Fail if the final rendered output exceeds this token count. |
| `--include-diffs` | flag | Include `git diff` output. |
| `--include-logs` | flag | Include recent one-line Git commits. |
| `--include-logs-count` | count | Number of commits to include. Defaults to 20. |
| `--split-output` | bytes | Split output into numbered files when the rendered content exceeds this size. |
| `--watch` | flag | Watch the source tree and print a notification when files change. Run codemap again to regenerate output. |
| `--help` | flag | Show command usage, options, and examples without packing. |

Boolean options are enabled by writing the flag. For example, use `--compress`, not `--compress true`.

## Configuration

Configuration uses JSON. codemap automatically loads `codemap.json` or `codemap.config.json` from the source root. Use `--config` to select another file.

```json
{
	"outputPath": "artifacts/repository.md",
	"format": "Markdown",
	"includePatterns": ["**/*.cs", "**/*.md"],
	"ignorePatterns": ["**/test-data/**"],
	"includeFileSummary": true,
	"includeDirectoryStructure": true,
	"showLineNumbers": false,
	"removeComments": true,
	"removeEmptyLines": true,
	"compressCode": true,
	"enableSecurityCheck": true,
	"maxFileSizeBytes": 500000,
	"tokenBudget": 12000,
	"includeGitDiffs": false,
	"includeGitLogs": true,
	"gitLogCount": 10,
	"splitOutputBytes": 200000
}
```

Command-line values override configuration values. For list options such as `--include` and `--ignore`, the command-line value replaces the configured list.

## Include and Ignore Rules

codemap always skips these generated or repository directories:

```text
.git  bin  obj  node_modules  dist  coverage
```

It also reads these files from the source root, in this order:

```text
.gitignore
.ignore
```

Patterns are evaluated in order. A pattern ignores a matching path; a pattern beginning with `!` re-includes it.

```gitignore
# Ignore generated files
generated/

# Keep one useful fixture
!generated/example.cs

# Ignore all logs
*.log
```

Include patterns are applied after ignore rules. Common examples:

```text
**/*.cs       all C# files at any depth
src/**        everything under src
*.md          Markdown files at any directory depth
```

## Security Scanning

`--security-check` uses Microsoft DevSkim embedded rules. codemap scans the original UTF-8 source before compression or cleanup transformations. Files with actionable DevSkim findings are excluded from the packed result instead of causing the entire operation to fail.

Excluded paths are reported on the console and exposed through the result model. Use this mode when creating context from repositories that may contain credentials, weak cryptography, or other known security patterns.

## Code Compression

`--compress` uses Tree-sitter grammars and keeps structural declarations such as classes, interfaces, functions, methods, properties, and types. It is useful when the full implementation is too large for an AI context but the public shape of the code should remain visible.

Supported extension-based languages include:

```text
C#, JavaScript, TypeScript, TSX, Python, Java, Go, Rust, C, C++,
Ruby, PHP, Kotlin, HTML, CSS, JSON, Bash, Scala, Swift, TOML
```

Unknown extensions and parser failures fall back to the original content. Compression is opt-in and should be disabled when exact implementation details are required.

## Output Formats

### Markdown

Markdown includes a repository summary, file listing, per-file token counts, language-aware code fences, and optional Git sections. It is the most convenient format for humans and chat-based AI tools.

### XML

XML contains a `codemap` root, summary attributes, directory structure, file elements, and optional Git metadata. It is useful for structured downstream processing.

### JSON

JSON contains summary data, file records, excluded security paths, and optional Git metadata. Each file includes its relative path, content, character count, line count, and token count.

### Plain text

Plain text emits each file under a clear path separator. It is useful for tools that do not parse Markdown, XML, or JSON.

## Token Counts and Limits

codemap uses the GPT-4-compatible `cl100k_base` tokenizer from `Microsoft.ML.Tokenizers`. Each packed file has a token count, and the final rendered output has an aggregate count.

`--token-budget` validates the final rendered output. If the output is too large, codemap returns an error rather than silently producing an incomplete result. Use `--include`, `--ignore`, `--compress`, `--max-file-size`, or `--split-output` to control size.

## Remote Repositories, Git Metadata, and Watch Mode

Clone and pack a GitHub repository:

```bash
codemap \
	--remote microsoft/generative-ai-for-beginners \
	--remote-branch main \
	--format markdown \
	--output repository.md
```

Use a normal URL when preferred:

```bash
codemap \
	--remote https://github.com/microsoft/TypeScript.git \
	--include-diffs \
	--include-logs \
	--include-logs-count 10
```

For a local repository:

```bash
codemap --root . --include-diffs --include-logs
```

Watch mode reports changes but does not automatically repack:

```bash
codemap --root . --watch
```

## Using the C# Library

The reusable core can be called without the CLI:

```csharp
using Codemap.Core;

var result = await new CodePacker().PackAsync(new PackOptions
{
		RootDirectory = ".",
		Format = OutputFormat.Markdown,
		IncludePatterns = ["**/*.cs"],
		CompressCode = true,
		EnableSecurityCheck = true,
		TokenBudget = 12000
});

Console.WriteLine(result.Content);
Console.WriteLine($"Files: {result.Files.Count}");
Console.WriteLine($"Tokens: {result.TokenCount}");
```

The core API returns packed files, rendered content, character and token counts, Git metadata, and security-excluded paths.

## Development

```bash
dotnet build Codemap.slnx
dotnet test --solution Codemap.slnx
```

Tests are organized under `tests/Codemap.Tests/Unit/` for core behavior and `tests/Codemap.Tests/Integration/` for CLI process behavior.

See [docs/how-it-works.md](docs/how-it-works.md) for pipeline boundaries and extension guidance. See [AGENTS.md](AGENTS.md) for repository conventions.

## Troubleshooting

**The output is too large**

Use `--compress`, narrower `--include` patterns, more `--ignore` patterns, `--max-file-size`, or `--token-budget`.

**A file is missing**

Check the default ignored directories, `.gitignore`, `.ignore`, and the include patterns. Binary and invalid UTF-8 files are intentionally skipped.

**Git metadata is empty**

Run from a Git working tree and verify that `git` is installed and available on `PATH`.

**Remote cloning fails**

Verify the repository URL, branch name, network access, and Git installation.

**Security files are excluded**

Review the DevSkim findings reported by the CLI. codemap excludes actionable findings by design so sensitive or risky content does not enter the generated AI context.