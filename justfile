[private]
just:
    just -l

# Format C# whitespace using the repository-local .editorconfig. This Unity package repo has no .sln,
# so the formatter is pointed at the tracked .cs files directly (folder mode) instead of a solution.
[group('refactoring')]
format:
    bash -lc 'dotnet format whitespace --folder --include $(git ls-files "*.cs")'

alias f := format
