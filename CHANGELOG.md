# Changelog

All notable changes to codemap will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning will be adopted when the first release process is defined.

## Unreleased

### Added

- Initial codemap core, CLI, output formats, filtering, transformations, and tests.
- Added JSON configuration, ignore files, Git metadata, remote input, watch notifications, compression, security scanning, size limits, and split output.
- Replaced heuristic compression and secret matching with TreeSitter.DotNet and native Microsoft DevSkim integration.
- Added GPT-4-compatible token counting, per-file token metadata, `.ignore` and negation support, binary-file skipping, security-based file exclusion, and richer output summaries.
