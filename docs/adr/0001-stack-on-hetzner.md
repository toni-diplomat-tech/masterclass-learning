# Next.js, .NET 10, SignalR and SQL Server, self-hosted with Docker on Hetzner

The app uses a Next.js frontend, a .NET 10 backend with SignalR for the live quiz, and SQL Server as the single database where all data lives, running as Docker containers on one Hetzner VPS. We chose this because it is the stack its maintainers already work in every day, so they can read, extend and maintain it without learning a new ecosystem. For an app this size a single-runtime stack (e.g. Next.js alone with WebSockets), a lighter database (PostgreSQL, SQLite) or a managed quiz service would be simpler; we accept two runtimes, SQL Server's heavier footprint (it needs at least 2 GB RAM on the VPS) and running our own server in exchange for staying on a stack we know.

## Consequences

- SQL Server runs as the Express edition in Docker: free for production use, limited to 10 GB per database, which is far above what quiz data needs. The Developer edition must not be used on the server, because its license does not allow production use.
