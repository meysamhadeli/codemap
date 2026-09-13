# codemap

> **codemap** scans codebases and turns selected files into focused, deterministic context for AI tools and developers. It filters files, can include Git history, enforces token and size limits, and excludes files with security findings before producing the output.

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/10.0) [![License](https://img.shields.io/badge/license-MIT-2ea44f?logo=opensourceinitiative&logoColor=white)](LICENSE)

## Contents

- [Installation](#installation)
- [Features](#features-what-codemap-provides)
- [How to run](#how-to-run)
- [Requirements](#requirements)
- [Command reference](#command-reference)
- [Configuration](#configuration)
- [Advanced capabilities](#advanced-capabilities)
- [Support](#support)
- [Contribution](#contribution)

## Installation

codemap is distributed as a .NET tool. Install the published package globally with:

```bash
dotnet tool install --global Codemap.Cli
```

Then run it from any directory with `codemap`.

Running `codemap` without options scans the current directory. Change into the repository directory before running it.

> [!TIP]
> Start with `codemap --format markdown --output repository.md` to create a shareable repository snapshot.

## Features: What codemap Provides

| | Capability | What it does |
| --- | --- | --- |
| 📦 | AI-ready packaging | Combines selected source files into one readable artifact. |
| 🧭 | Deterministic discovery | Processes files in stable path order for repeatable output. |
| 🎯 | Include and ignore rules | Filters paths with globs, `.gitignore`, and `.ignore`. |
| 🌿 | Git awareness | Includes diffs and recent commits when requested. |
| 🔢 | Token counts | Reports GPT-4-compatible `cl100k_base` counts per file and overall. |
| 🛡️ | Security filtering | Uses DevSkim and excludes files with actionable findings. |
| 🧹 | Content cleanup | Removes comments or empty lines and can add line numbers. |
| 📝 | Multiple formats | Writes Markdown, XML, JSON, or plain text. |
| 📏 | Size controls | Supports file-size limits, token budgets, and split output. |
| 🌐 | Repository sources | Packs a local directory or clones a remote Git repository. |
| 👀 | Workflow support | Watches a directory for changes or exposes a reusable C# library. |

## How to Run

codemap's main workflow is simple: choose a source directory, select an output format, and write the generated repository context to a file. The examples below focus on core commands.

### ❔ Help

Use the built-in help whenever you need to check available commands and options:

```bash
codemap --help
```

### 🔀 Include and Ignore

Use `--include` to select files and `--ignore` to remove paths from that selection:

```bash
codemap \
	--include "src/**/*.cs,README.md" \
	--ignore "**/bin/**,**/obj/**" \
	--format markdown \
	--output source-context.md
```

codemap also reads `.gitignore` and `.ignore` automatically.

### 🛡️ Security Check

Use DevSkim to exclude files with actionable security findings before they enter the generated context:

```bash
codemap \
	--security-check \
	--format markdown \
	--output reviewed-context.md
```

codemap reports excluded files in the terminal. Node.js and npm are not required.

### 📝 Format

The default format is Markdown. Use `--format` to choose another output format when needed:

```bash
# Human- and AI-friendly document
codemap --format markdown --output repository.md

# Structured data for another program
codemap --format json --output repository.json

# XML or simple text output
codemap --format xml --output repository.xml
codemap --format plain --output repository.txt
```

### 🌐 Remote

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
| `--remote` | URL or `owner/repository` | Clone a remote Git repository into a temporary directory before packing. |
| `--remote-branch` | branch | Branch to clone when using `--remote`. |
| `--config` | path | Configuration JSON file. Without this option, codemap searches for `codemap.json` and `codemap.config.json`. |
| `--include` | comma-separated globs | Include only matching paths, for example `**/*.cs,**/*.md`. |
| `--ignore` | comma-separated globs | Add ignore patterns for this run. |
| `--format` | `xml`, `markdown`, `md`, `json`, `plain`, `txt` | Output format. Defaults to Markdown. |
| `--output` | path | Output file path. Defaults to `codemap-output.md`. |
| `--no-summary` | flag | Remove file count and token summary from structured output. |
| `--no-tree` | flag | Remove the directory/file listing from structured output. |
| `--line-numbers` | flag | Prefix each output line with its line number. |
| `--remove-comments` | flag | Remove common `//` and `/* ... */` comments before rendering. |
| `--remove-empty-lines` | flag | Remove blank lines after other transformations. |
| `--security-check` | flag | Scan original files with DevSkim and exclude files with findings. |
| `--max-file-size` | bytes | Skip files larger than this size before reading them. |
| `--token-budget` | count | Fail if the final rendered output exceeds this token count. |
| `--include-diffs` | flag | Include `git diff` output. |
| `--include-logs` | flag | Include recent one-line Git commits. |
| `--include-logs-count` | count | Number of commits to include. Defaults to 20. |
| `--split-output` | bytes | Split output into numbered files when the rendered content exceeds this size. |
| `--watch` | flag | Watch the source tree and print a notification when files change. Run codemap again to regenerate output. |
| `--help` | flag | Show command usage, options, and examples without packing. |

Boolean options are enabled by writing the flag.

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

## Advanced Capabilities

The sections below explain behavior that is useful when tuning output for a larger repository or an automated workflow.

### 🎯 Include and Ignore Rules

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

### 🛡️ Security Scanning

`--security-check` uses Microsoft DevSkim embedded rules. codemap scans the original UTF-8 source before cleanup transformations. Files with actionable DevSkim findings are excluded from the packed result instead of causing the entire operation to fail.

Excluded paths are reported on the console and exposed through the result model. Use this mode when creating context from repositories that may contain credentials, weak cryptography, or other known security patterns.

### 📝 Output Formats

### Markdown

Markdown includes a repository summary, file listing, per-file token counts, language-aware code fences, and optional Git sections. It is the most convenient format for humans and chat-based AI tools.

### XML

XML contains a `codemap` root, summary attributes, directory structure, file elements, and optional Git metadata. It is useful for structured downstream processing.

### JSON

JSON contains summary data, file records, excluded security paths, and optional Git metadata. Each file includes its relative path, content, character count, line count, and token count.

### Plain text

Plain text emits each file under a clear path separator. It is useful for tools that do not parse Markdown, XML, or JSON.

### 🔢 Token Counts and Limits

codemap uses the GPT-4-compatible `cl100k_base` tokenizer from `Microsoft.ML.Tokenizers`. Each packed file has a token count, and the final rendered output has an aggregate count.

`--token-budget` validates the final rendered output. If the output is too large, codemap returns an error rather than silently producing an incomplete result. Use `--include`, `--ignore`, `--max-file-size`, or `--split-output` to control size.

### 🌐 Remote Repositories, Git Metadata, and Watch Mode

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
codemap --include-diffs --include-logs
```

Watch mode reports changes but does not automatically repack:

```bash
codemap --watch
```

# 🌟 Support

If you like my work, feel free to:

- ⭐ this repository. And we will be happy together :)

Thanks a bunch for supporting me!

## 🤝 Contribution

Thanks to all [contributors](https://github.com/meysamhadeli/codemap/graphs/contributors), you're awesome and this wouldn't be possible without you! The goal is to build a categorized, community-driven collection of very well-known resources.

Please follow this [contribution guideline](./CONTRIBUTION.md) to submit a pull request or create the issue.