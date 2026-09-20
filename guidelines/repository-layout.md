# Repository layout

| Path | What belongs there |
|---|---|
| `CAIME/` | Application code. |
| `CAIME.Tests/` | Tests. |
| `TestData/` | Test data, including the reference output tests compare against. |
| `docs/` | User-facing documentation, published as the CAIME user guides site. |
| `FileFormatDefinitions/` | Binary file format specifications. |
| `skills/` | Reusable agent skills and the scripts they drive. See [Agent skills](agent-skills.md). |
| `.github/workflows/` | GitHub Actions workflows. |

Other top-level directories — `EsfLibrary/`, `MapDataBuilder/`, `ChecksumGen/`,
`Templates/`, `3rdParty/` — are existing components. Add to them only when the
change genuinely belongs to that component.
