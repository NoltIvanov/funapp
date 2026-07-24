# Local Agent Instructions

## 1. Think Before Coding

Do not assume. Do not hide confusion. Surface tradeoffs.

Before implementing:
- State assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them. Do not pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what is confusing. Ask.

## 2. Simplicity First

Minimum code that solves the problem. Nothing speculative.

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that was not requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?"

## 3. Surgical Changes

Touch only what you must. Clean up only your own mess.

When editing existing code:
- Do not "improve" adjacent code, comments, or formatting.
- Do not refactor things that are not broken.
- Match existing style, even if you would do it differently.
- If you notice unrelated dead code, mention it. Do not delete it.

When your changes create orphans:
- Remove imports, variables, and functions that your changes made unused.
- Do not remove pre-existing dead code unless asked.

The test: every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

Define success criteria. Loop until verified.

Transform tasks into verifiable goals:
- "Add validation" -> "Write tests for invalid inputs, then make them pass."
- "Fix the bug" -> "Write a test that reproduces it, then make it pass."
- "Refactor X" -> "Ensure tests pass before and after."

For multi-step tasks, state a brief plan:

1. [Step] -> verify: [check]
2. [Step] -> verify: [check]
3. [Step] -> verify: [check]

Strong success criteria let you loop independently.
Weak criteria ("make it work") require constant clarification.

## 5. Local File Safety

- Never delete, move, truncate, overwrite, or clean up `AGENTS.local.md`.
- Treat `AGENTS.local.md` as user-owned local configuration, even though it is ignored by Git.

## 6. Commit Naming

- Commit names/messages must be written in English only.

## 7. Git Operations

- Commit, push, and create or update pull requests only when the user explicitly asks for that git action.
- Do not infer permission to commit, push, or open a pull request from completed code changes, successful builds, or an existing pull request.
- If code changes are ready but no git action was requested, leave them uncommitted and report the status.
