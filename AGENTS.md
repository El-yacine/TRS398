# Repository Guidelines

## Project Structure & Module Organization
- Root: `README.md`, `START.sh` launcher, `detector_library.json` (detector metadata consumed by `/api/detectors`).
- Backend: `server/` .NET 8 minimal API (`Program.cs`), SQLite DB at `server/trs398.db`, EF Core context in `Data/`, domain model in `Models/`, core calculations and PDF export in `Services/`.
- Frontend: static UI in `server/wwwroot/` (`index.html` main form, `history.html` history view). Built assets live directly in the repo; avoid editing `bin/` or `obj/`.

## Build, Test, and Development Commands
- `./START.sh`: kills stale `dotnet` processes, runs the API on `http://localhost:8000`, logs to `/tmp/trs398_clean.log`.
- `dotnet build server/MyQC.WebAPI.csproj`: compile and verify dependencies.
- `dotnet run --project server/MyQC.WebAPI.csproj --urls http://localhost:8000`: run without the wrapper script (useful when iterating).
- `curl http://localhost:8000/api/health`: quick service health check.

## Coding Style & Naming Conventions
- C# code uses 4-space indentation, top-level statements, and file-scoped namespaces.
- Public types/properties: `PascalCase`; locals/parameters: `camelCase`; constants: `ALL_CAPS` only when truly constant.
- Keep business logic in services (`TRSService` for calculations, `PdfReportService` for rendering). Controllers/endpoints should stay thin.
- Favor explicit types over `var` when it clarifies intent; prefer `readonly` and `private` where possible.

## Testing Guidelines
- Automated tests are not present yet; add xUnit tests under `server/Tests/` when introducing new logic (e.g., `TRSService` calculation cases).
- Manual smoke: load `http://localhost:8000`, submit a measurement, verify it appears in `history.html`, export CSV (`/api/trs/export`), and generate a PDF report (`/api/trs/report/{id}`).
- When changing DB shape, confirm `EnsureSchema` in `Program.cs` still covers new columns or add a migration strategy.

## Commit & Pull Request Guidelines
- No prior Git history is committed here; use conventional, present-tense messages (e.g., `feat: add kpol display to reports`, `fix: guard null clinic fields`).
- PRs should include: summary of changes, steps to reproduce/test, screenshots/GIFs for UI updates, and a note if `trs398.db` or `detector_library.json` changed.
- Keep generated artifacts (`bin/`, `obj/`) out of commits; only commit database changes intentionally and call them out.

## Security & Configuration Tips
- SQLite file is local to `server/`; back up before destructive testing.
- The app auto-creates/updates the DB at startup; ensure any schema changes are idempotent.
- API endpoints are open locally; avoid exposing them without adding auth and rate limits.
