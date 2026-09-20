# Coding style

## Follow the Microsoft C# coding style

Write C# the way the official Microsoft guidance describes:

- [Identifier naming rules and conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

Those two pages are the reference — they are not restated here. The
`.editorconfig` at the repository root encodes the parts a tool can check; the
pages govern the rest.

## Let the names carry the meaning, not the comments

Follow Robert C. Martin's clean code principles: a class, method, or variable
name should read as close to plain English as the language allows, so that the
code explains itself without a running commentary beside it.

- Name things for what they mean, not for how they are implemented.
- Keep methods small and at a single level of abstraction; extract a
  well-named method instead of writing a comment that introduces a block.
- Do not write comments that restate the code, narrate a change, or mark
  sections. If a comment is needed to make a line understandable, rename or
  restructure until it is not.
- Comments earn their place when they record something the code cannot say:
  why a non-obvious choice was made, a constraint imposed by a game file
  format, a reproduced quirk of the original implementation, or a reference to
  an external spec.
- Public API documentation comments are fine where they add information a
  caller cannot read off the signature.

## Leave code outside your change alone

Not all of the code here follows the rules above. That is expected, and it is
not a task waiting to be picked up.

- Do not reformat, rename, or restructure code outside the scope of the change
  you were asked to make. The `.editorconfig` naming rules report as warnings,
  and existing code produces plenty of them; that is a report, not a work item.
- Code you are already changing is in scope: bring it in line as you go.
- Keep a file internally consistent. Where a file's local convention conflicts
  with these rules, match the file, and raise the conflict rather than
  splitting the file between two styles.
