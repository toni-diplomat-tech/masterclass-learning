# masterclass-learning
Monorepo for this platform

| Folder | Contents |
|---|---|
| `frontend/` | Next.js (App Router) |
| `backend/` | ASP.NET Core (.NET 10): REST API and the Quiz engine |
| `database/` | `migrations/` (numbered, run once) and `procedures/` (`CREATE OR ALTER`, run on every startup), applied by DbUp |
| `content/` | git-ignored course material, Question files and import files |

## Run locally

```sh
docker compose up --build
```

Then open http://localhost:3000/health. Copy `.env.example` to `.env` to change the SA password or ports.

## Host password

The Host logs in at `/host/login`. The backend checks the password against a hash in the `HOST_PASSWORD_HASH` environment variable; the password itself is never stored. Print a hash and put it in `.env` (git-ignored):

```sh
dotnet run --project backend/src/MasterclassQuiz.Api -- hash-host-password
```

Without `HOST_PASSWORD_HASH` every login is refused.

## Test

Docker must be running: the engine tests start SQL Server with Testcontainers.

```sh
dotnet test backend/MasterclassQuiz.slnx
```
