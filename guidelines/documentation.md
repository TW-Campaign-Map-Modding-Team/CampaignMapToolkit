# Documentation

The code is the single source of truth for how this project behaves.
Documentation reflects the code; it never leads it.

- When code and documentation disagree, the code is right and the
  documentation is out of date.
- Documentation describes what the code does; it never defines behaviour the
  code has yet to implement.
- Change the code first, then update whatever documentation described the old
  behaviour in the same change.
- Never edit documentation to describe intended or hoped-for behaviour, and
  never leave a document claiming something the code does not do.

User-facing documentation lives in `docs/` and is published as the CAIME user
guides site. See [Repository layout](repository-layout.md).
