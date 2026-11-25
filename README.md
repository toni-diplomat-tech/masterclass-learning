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

## Test

Docker must be running: the engine tests start SQL Server with Testcontainers.

```sh
dotnet test backend/MasterclassQuiz.slnx
```
