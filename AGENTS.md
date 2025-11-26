# AGENTS.md

## Build/Test Commands

This Unity project uses standard Unity workflows. Tests are run through Unity Test Framework:

- **Build**: Use Unity Editor > File > Build Settings or Unity command line: `Unity.exe -batchmode -quit -projectPath . -buildTarget StandaloneWindows -buildPath Build/`
- **Run all tests**: Unity Editor > Window > General > Test Runner > Run All, or command line: `Unity.exe -batchmode -runTests -projectPath .`
- **Run single test**: In Test Runner, find specific test and click Run, or use command line with filter: `Unity.exe -batchmode -runTests -projectPath . -testCategory "TestName"`

## Code Style Guidelines

Follow the .editorconfig configuration in Assets/Packages/LDrawLoader/.editorconfig:

### Formatting & Structure
- 4 spaces indentation, no tabs
- New line before all braces (all, else, catch, finally)
- System using directives outside namespaces, sorted alphabetically
- Insert final newlines, trim trailing whitespace

### Naming Conventions  
- Private/internal fields: `_camelCase` with underscore prefix
- Static fields: `s_camelCase` with s_ prefix
- Constants: `PascalCase`
- Public members: `PascalCase`
- Avoid var unless type is apparent - use explicit types

### Code Quality
- Prefer readonly fields
- Use object/collection initializers
- Null propagation and coalescing preferred
- Expression-bodied members where appropriate
- Include XML documentation for public APIs
- Follow Unity naming patterns for components and methods

### Error Handling
- Use specific exception types
- Validate arguments and throw ArgumentException
- Log Unity errors with Debug.LogError
- Handle LDraw file parsing gracefully with custom exceptions