# 2. Constraints

## 2.1 Technical Constraints

| Constraint                         | Explanation                                                                                      |
|------------------------------------|--------------------------------------------------------------------------------------------------|
| **.NET Standard 2.0 compatibility** | The library must compile against `netstandard2.0` for maximum platform coverage (.NET Framework 4.6.1+, .NET Core 2.0+, Mono, Xamarin, Unity). |
| **.NET 10.0 dual target**          | Additional target `net10.0` for access to current .NET APIs and optimizations.                   |
| **No external dependencies**       | The main library must not reference any third-party NuGet packages (zero dependencies).          |
| **C# latest language version**     | Use of current C# language features where compatible with `netstandard2.0`.                      |
| **Nullable reference types**       | Nullable annotations are enabled (`<Nullable>enable</Nullable>`).                                |
| **InvariantCulture**               | All numeric and date formatting/parsing must use `CultureInfo.InvariantCulture`, as EDS/DCF files are culture-independent. |

### Unavailable APIs (netstandard2.0)

The following .NET APIs are not available and must be worked around:

- `string.Replace(string, string, StringComparison)`
- `string.Contains(string, StringComparison)`
- `string.Contains(char)`
- `string.StartsWith(char)` / `string.EndsWith(char)`
- `[NotNullWhen]`, `[MemberNotNull]` attributes

## 2.2 Organizational Constraints

| Constraint                      | Explanation                                                                    |
|---------------------------------|--------------------------------------------------------------------------------|
| **Conventional Commits**        | All commit messages follow the Conventional Commits specification.             |
| **Semantic Release**            | Versioning and NuGet publishing are automated via `semantic-release`.          |
| **MIT License**                 | Open-source licensing under MIT.                                               |
| **GitHub-based development**    | Repository, CI/CD, and issue tracking via GitHub.                              |

## 2.3 Conventions

| Convention                   | Description                                                               |
|------------------------------|---------------------------------------------------------------------------|
| **XML documentation comments** | All public members must have XML doc comments.                           |
| **File-scoped namespaces**   | Use of `namespace Foo;` instead of block syntax.                          |
| **Test naming convention**   | `MethodName_Scenario_ExpectedBehavior` (e.g., `ParseInteger_HexValue_ReturnsCorrectResult`) |
| **AAA test pattern**         | Tests follow the Arrange-Act-Assert pattern.                              |
| **XUnit + FluentAssertions** | Test framework and assertion library.                                     |
