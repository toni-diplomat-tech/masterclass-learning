# Masterclass Quiz

Live quiz app: a Host presents each Workshop of a Course, then Participants answer a Session on their phones and get a Recap of Lessons to revisit.

## Pointers

- **Domain terms**: read `GLOSSARY.md` before naming anything (code, tables, procedures, UI text) and whenever a term is unclear; use its canonical names.
- **Decisions**: read `docs/adr/` before touching architecture, data access, hosting or the stack.
- **Features**: specs and tickets are GitHub issues (see Agent skills below); when code and an open spec disagree, flag it. Once a spec is closed, the code is the source of truth.

## Conventions

- **Quiz engine is the seam.** Business rules live in SQL Server stored procedures (ADR 0002). The SignalR hub and REST endpoints call the C# Quiz engine; only the engine calls procedures, through Dapper. Tests cross the same seam against a real SQL Server in Testcontainers, so Docker Desktop must be running.
- **Time is UTC from the server.** The backend reads `TimeProvider` UTC time and passes it to procedures as `@Now`; procedures take time only from that parameter. Store times as UTC `datetime2`.
- **Public repo: committed files stay generic.** Examples use the made-up "Testing Fundamentals" Course. Real course material, Question files and import files live in git-ignored `content/`. Never name the employer, the source course or its author in committed files.

## Agent skills

### Issue tracker

Specs and tickets are GitHub issues in this repo, managed with the `gh` CLI. See `docs/agents/issue-tracker.md`.
