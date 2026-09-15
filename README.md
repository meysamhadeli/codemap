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
> **Quick Start**
>
> - **Save a snapshot:** `codemap --format markdown --output repository.md`
> - **Print without a file:** `codemap stdout --format markdown` or `codemap -s`
> - **Copy to clipboard:** `codemap clipboard --format markdown` or `codemap -c`

## Features

| | Capability | What it does |
| --- | --- | --- |
| 📦 | AI-ready packaging | Combines selected source files into one readable artifact. |
| 📤 | Three output modes | Writes to a file, prints to stdout, or copies directly to the clipboard. |
| 🧭 | Deterministic discovery | Processes files in stable path order for repeatable output. |
| 🎯 | Include and exclude rules | Filters paths with globs, `.gitignore`, and `.ignore`. |
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

### 🔀 Include and Exclude

Use `--include` to select files and `--exclude` to remove paths from that selection:

```bash
codemap \
	--include "src/**/*.cs,README.md" \
	--exclude "**/bin/**,**/obj/**" \
	--format markdown \
	--output source-context.md
```

codemap also reads `.gitignore` and `.ignore` automatically.

The same selection can use short aliases: `codemap -i "src/**/*.cs" -e "**/bin/**,**/obj/**" -f markdown -o source-context.md`.

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
codemap stdout [options]
codemap clipboard [options]
codemap -s [options]
codemap -c [options]
```

Show the built-in command reference at any time:

```bash
codemap --help
```

| Option | Alias | Value | Description |
| --- | --- | --- | --- |
| `--remote` | `-r` | URL or `owner/repository` | Clone a remote Git repository into a temporary directory before packing. |
| `--remote-branch` | `-b` | branch | Branch to clone when using `--remote`. |
| `--config` | - | path | Configuration JSON file. Without this option, codemap searches for `codemap.json` and `codemap.config.json`. |
| `--include` | `-i` | comma-separated globs | Include only matching paths, for example `**/*.cs,**/*.md`. |
| `--exclude` | `-e` | comma-separated globs | Add exclusion patterns for this run. |
| `--format` | `-f` | `xml`, `markdown`, `md`, `json`, `plain`, `txt` | Output format. Defaults to Markdown. |
| `--output` | `-o` | path | Output file path. Defaults to `codemap-output.md`. |
| `--max-file-size` | `-m` | bytes | Skip files larger than this size before reading them. |
| `--token-budget` | `-t` | count | Fail if the final rendered output exceeds this token count. |
| `--no-summary` | - | flag | Remove file count and token summary from structured output. |
| `--no-tree` | - | flag | Remove the directory/file listing from structured output. |
| `--line-numbers` | - | flag | Prefix each output line with its line number. |
| `--remove-comments` | - | flag | Remove common `//` and `/* ... */` comments before rendering. |
| `--remove-empty-lines` | - | flag | Remove blank lines after other transformations. |
| `--security-check` | - | flag | Scan original files with DevSkim and exclude files with findings. |
| `--include-diffs` | - | flag | Include `git diff` output. |
| `--include-logs` | - | flag | Include recent one-line Git commits. |
| `--include-logs-count` | - | count | Number of commits to include. Defaults to 20. |
| `--split-output` | - | bytes | Split output into numbered files when the rendered content exceeds this size. |
| `--watch` | `-w` | flag | Watch the source tree and print a notification when files change. Run codemap again to regenerate output. |
| `--version` | `-v` | flag | Show the tool version. |
| `--help` | `-h` | flag | Show command usage, options, and examples without packing. |


Boolean options are enabled by writing the flag.

## Configuration

Configuration uses JSON. codemap automatically loads `codemap.json` or `codemap.config.json` from the source root. Use `--config` to select another file.

```json
{
	"outputPath": "artifacts/repository.md",
	"format": "Markdown",
	"includePatterns": ["**/*.cs", "**/*.md"],
	"excludePatterns": ["**/test-data/**"],
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

Command-line values override configuration values. For list options such as `--include` and `--exclude`, the command-line value replaces the configured list.

## Advanced Capabilities

### Exclude

Use `.ignore` to keep repository-specific files out of generated context, such as local notes, logs, fixtures, or generated output. Place it in the source root and add one glob per line; codemap also reads `.gitignore`, supports comments and ordered rules, and uses `!` to re-include a matching path. Common generated directories are excluded automatically.

### Security

Use `--security-check` when the source may contain credentials, unsafe cryptography, or other known security problems. codemap scans original UTF-8 files before cleanup, omits files with actionable DevSkim findings instead of stopping the entire pack, and reports excluded paths in the console and result model.

### Tokens

Token counts help estimate how much context an AI tool will receive. codemap reports per-file and final-output counts using the GPT-4-compatible `cl100k_base` encoding; use `--token-budget` to reject oversized output, `--max-file-size` to skip large files, or `--split-output` to create smaller parts.

### Workflow

Use `--remote` when the repository is not available locally; codemap clones it into a temporary directory and packs the selected branch. For repository history, `--include-diffs` adds current changes and `--include-logs` adds recent commits. `--watch` monitors a local source tree and reports changes so you can run codemap again.

# 🌟 Support

If you like my work, feel free to:

- ⭐ this repository. And we will be happy together :)

Thanks a bunch for supporting me!

## 🤝 Contribution

Thanks to all [contributors](https://github.com/meysamhadeli/codemap/graphs/contributors), you're awesome and this wouldn't be possible without you! The goal is to build a categorized, community-driven collection of very well-known resources.

Please follow this [contribution guideline](./CONTRIBUTION.md) to submit a pull request or create the issue.