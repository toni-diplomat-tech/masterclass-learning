# masterclass-learning
Monorepo for this platform

| Folder | Contents |
|---|---|
| `frontend/` | Next.js (App Router) |
| `backend/` | ASP.NET Core (.NET 10): REST API and the Quiz engine |
| `database/` | `migrations/` (numbered, run once) and `procedures/` (`CREATE OR ALTER`, run on every startup), applied by DbUp |
| `tools/` | `import-file-builder/`: builds a Course import file from a course folder and Question files |
| `content/` | git-ignored course material, Question files and import files (format: `docs/import-file/`) |
| `deploy/` | production Caddyfile, deploy wizard, swap script |

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

Without `HOST_PASSWORD_HASH` every login is refused. For the VPS, `deploy/wizard.sh` asks for the password and writes the hash to the server's `.env`.

## Quiz settings

Time to answer per Difficulty is in the `Quiz` section of `backend/src/MasterclassQuiz.Api/appsettings.json` (Easy 5 s, Medium 7 s, Hard 10 s). Override one with an environment variable on the backend, e.g. `Quiz__TimeLimitSeconds__Hard=12`. A Session keeps the limits it started with. After each reveal, the next Question starts after `Quiz:RevealPauseSeconds` (5 s; e.g. `Quiz__RevealPauseSeconds=8`).

## Test

Docker must be running: the engine tests start SQL Server with Testcontainers.

```sh
dotnet test backend/MasterclassQuiz.slnx
```

## Deploy

Production is one Hetzner VPS: a CX23 or another x86 plan, because SQL Server does not run on the Arm (CAX) plans. On the VPS, `docker-compose.prod.yml` is layered on `docker-compose.yml`. It adds Caddy (automatic HTTPS for the Host's domain, the only service with published ports), drops the local default passwords and restarts services after a reboot. SQL Server runs as Express with its memory capped at 2 GB (`MSSQL_MEMORY_LIMIT_MB`), and the server gets a 2 GB swap file (`deploy/setup-swap.sh`).

The first deploy, and any redeploy, is one command run from the repo root in Git Bash:

```sh
bash deploy/wizard.sh
```

It walks you through creating the VPS, pointing DNS at it and choosing secrets. It writes the server's `.env` to git-ignored `deploy/.env.production` (the layout is in `deploy/.env.production.example`), then over SSH installs Docker, clones the repo into `/opt/masterclass-quiz`, adds swap, copies the `.env` and runs `docker compose up -d --build`. That `.env` sets `COMPOSE_FILE`, so a plain `docker compose ...` on the server always includes the production file.

Every secret lives only in that `.env`. To add one, put it in `deploy/.env.production.example`, pass it to its service in `docker-compose.prod.yml` as `${NAME:?...}` so compose refuses to start without it, and ask for it in the wizard's secrets stage.

To redeploy by hand: `ssh root@<server> 'cd /opt/masterclass-quiz && git pull && docker compose up -d --build'`.
