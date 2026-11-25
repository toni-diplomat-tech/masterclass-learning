# Business rules in stored procedures, no ORM

The backend has no Entity Framework or other ORM. All data access goes through SQL Server stored procedures, called with Dapper, and the business rules live in those procedures too: Question selection, timing and outcomes, scoring, leaderboards and building the Recap. We chose this for full control over the SQL and its performance. A .NET reader would expect EF with rules in C#, so do not "fix" this by adding an ORM or moving rules into C#.

## Consequences

- **The seam is the Quiz engine's C# interface, not the procedures.** Callers (SignalR hub, REST endpoints) and tests go through that interface. Nothing else calls a procedure directly.
- **Rules are tested against a real SQL Server.** Tests run SQL Server in Docker (Testcontainers), so a developer machine needs Docker to run them. There is no in-memory fake, because it would duplicate the rules in C#.
- **Procedures receive the current time as a parameter** instead of reading the clock, so timing rules can be tested without waiting.
- **Schema changes are versioned SQL scripts applied by DbUp**, and procedures are re-applied as `CREATE OR ALTER` on every deploy.
