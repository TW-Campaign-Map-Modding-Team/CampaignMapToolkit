# Agent skills

When you work out how to do something that will come up again, put it in
`skills/` as a skill and the script it drives, rather than solving it from
scratch next time.

Reading a binary `pathfinding.ppd` is the standard example: the layout and the
parsing are the same every time, so the algorithm belongs in a script under
`skills/` that any later session can run, not in a throwaway script rewritten on
each occasion.

- Check `skills/` before writing a script of your own.
- Add to it whenever you have written something worth keeping.
- Skills and their scripts are committed, so they must work for anyone who
  clones the repository: no absolute paths, no machine-specific assumptions.
- A skill describes what it does and when to reach for it, so the next session
  can tell from the description alone whether it applies.

## Making Claude Code discover them

Claude Code looks for project skills in `.claude/skills/`, which this repository
does not track. Link it to `skills/` once per clone, from the repository root:

```
cmd /c mklink /J .claude\skills skills
```

A junction needs no elevation and no Developer Mode. The link is local and
untracked; `skills/` is the committed source of truth.
