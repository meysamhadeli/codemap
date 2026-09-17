# Changelog

All notable changes to codemap will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning will be adopted when the first release process is defined.

## Unreleased

### Added

- Initial codemap core, CLI, output formats, filtering, transformations, and tests.
- Added short aliases for primary CLI options, including help, version, format, output, remote, config, include, exclude, and max file size.
- Added `-t` for `--token-budget` and `-w` for `--watch`.
- Added `stdout` and `clipboard` subcommands for file-free output.
- Added `-s` and `-c` aliases for `stdout` and `clipboard`, using TextCopy for cross-platform clipboard support; `--config` is now long-only.
- Added JSON configuration, ignore files, Git metadata, remote input, watch notifications, security scanning, size limits, and split output.
- Added `--patch` mode for producing Markdown context with Git diff generation instructions.
- Added explicit patch application workflow with preview and per-file approval.
- Changed patch application to standard Git diffs with `codemap patch`, Git preflight validation, and per-file approval.
- Added GPT-4-compatible token counting, per-file token metadata, `.ignore` and negation support, binary-file skipping, security-based file exclusion, and richer output summaries.
