---
applyTo: "**/*.cs"
---

# C# Code Style

- All comments MUST be written in English.
- Nullable reference types are disabled (`<Nullable>disable</Nullable>`). Do not use `?` annotations or `!` null-forgiving operator.
- Single-line `if`, `for`, `foreach`, `while` statements MUST omit braces: `if (condition) myValue = true;`
  - If the line exceeds ~100 characters, break to the next line (still no braces).
- Single-line methods MUST use expression-bodied syntax (`=>`).
- Single-line `try`/`catch`/`finally` blocks each stay on one line:
  ```csharp
  try { /* code */ }
  catch (Exception exception) { /* handle */ }
  ```
- Variable names MUST NOT use abbreviations. Use full descriptive names.
- MUST use primary constructors wherever possible.
- MUST use collection expressions (`[item1, item2]`) wherever possible.
- Actively use the latest C# language features and syntax.
- When adding an event to a control, always create a corresponding ICommand BindableProperty. When adding a command, always create a corresponding event. Events and commands must always exist as a pair.
