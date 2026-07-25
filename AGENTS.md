# LECG Revit 2026 Add-in — Codex Rules

- Target Revit: 2026 only. Do not ask.
- Do not read whole files. Use rg + targeted line ranges; max 80 lines per snippet.
- Prefer local extraction first (rg, dotnet, ls, wc) before opening code.
- Keep diffs small: one step at a time.
- After each step, run: dotnet build -p:SkipRevitDeploy=true
  (a plain `dotnet build` overwrites the live Revit 2026 add-in folder)
- Output markdown artifacts only under docs/review/.
- Do not spawn parallel agents/teams.
- Ask one question at a time only when blocked.
- Network access: OFF (unless explicitly needed).
- Manual approve edits (no auto-apply).