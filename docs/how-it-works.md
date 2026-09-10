# How codemap Works

codemap follows a small pipeline so each stage can evolve independently:

For installation, command examples, configuration, and the complete feature reference, see the [user guide](../README.md).

Users can run `codemap --help` at any time to display all commands and options directly in the terminal.

1. `CodePacker` discovers files beneath the configured root in deterministic path order.
2. Include patterns select paths, while default and custom ignore patterns remove paths.
3. Content transformations add line numbers or remove comments and empty lines.
4. Renderers produce XML, Markdown, plain text, or JSON.
5. The result reports file count, character count, and GPT-4-compatible token counts.

Configuration is loaded from `codemap.json` or `codemap.config.json` when present. Explicit CLI options override file configuration. Ignore patterns are read from `.gitignore` and `.ignore`, in addition to options and built-in generated-directory exclusions. Patterns are evaluated in order and support `!` negation.

Optional pipeline stages provide bounded file-size filtering, Tree-sitter declaration compression, native DevSkim security analysis, Git diff/log metadata, and output splitting. DevSkim scans original source content before transformations and excludes files with actionable findings. Binary and invalid UTF-8 files are skipped. Tree-sitter language selection is extension-based and currently covers C#, JavaScript, TypeScript, TSX, Python, Java, Go, Rust, C, C++, Ruby, PHP, HTML, CSS, JSON, Bash, Scala, Swift, and TOML. The CLI can clone a remote Git repository into a temporary directory and can watch a local source tree for changes. Watch mode currently reports changes so callers can rerun packing without keeping a second packer process alive.

The CLI maps command-line arguments to `PackOptions`; it does not own discovery or rendering. Future capabilities should follow the same boundary. Remote repository acquisition, Git metadata, DevSkim security analysis, AST compression, external processors, split output, and watch mode can be added as independent services or pipeline stages with focused tests.

Token counts use the `cl100k_base` encoding through `Microsoft.ML.Tokenizers`; counts are included per file and in output summaries.
