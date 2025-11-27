# Unity LDraw Loader - Agent Development Guidelines

This project provides the Unity editor with tools to load LDraw model and part files. To maintain code quality and consistency, follow these guidelines when contributing.

## Code Style Guidelines

- Use the rules in @.editorconfig to ensure consistent formatting across the codebase.

## Guidelines for Contributions

- Document new code with XML comments. Be brief but informative.
- Break down large methods into smaller, reusable functions.
- Value readability over cleverness.
- Put large classes into separate files.
- Identify and handle edge cases and errors gracefully.

## References

- [LDraw File Format Specification](https://www.ldraw.org/article/218.html)
- [Colour Definition (!COLOUR) Language Extension](https://www.ldraw.org/article/299.html)
  - Quick Reference: https://www.ldraw.org/article/547.html
- [Back Face Culling (BFC) Language Extension](https://www.ldraw.org/article/415.html)
- [!CATEGORY and !KEYWORDS Language Extension](https://www.ldraw.org/article/340.html)
- [Multi-Part Document (MPD) and Image Embedding (!DATA) Language Extension](https://www.ldraw.org/article/47.html)
- [Texture Mapping (!TEXMAP) Language Extension](https://www.ldraw.org/texmap-spec.html)