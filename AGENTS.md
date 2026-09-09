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

## Minimal implementation (Ponytail)

For every code change, stop at the first option that works: skip speculative work, reuse existing code, use the standard library, use the Revit/.NET platform, use an installed dependency, then write the minimum new code.

Prefer deletion and concrete code over wrappers, single-implementation interfaces, factories, and configuration without a current consumer. Trace all callers and fix the shared root cause. Preserve Revit transactions, rollback, confirmations, validation, error handling, security, accessibility, and data-loss protections. Add the smallest meaningful check for non-trivial logic. Mark intentional ceilings with a `ponytail:` comment that names the limit and the condition for replacing it.

Adapted from [DietrichGebert/ponytail](https://github.com/DietrichGebert/ponytail), MIT licensed.
