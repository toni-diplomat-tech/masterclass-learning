using MasterclassQuiz.Api.Database;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class DatabaseMigratorTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly string _scripts = Directory.CreateTempSubdirectory("migrator-test-").FullName;

    [Fact]
    public void Migrations_run_once_each_in_order_and_procedures_run_on_every_startup()
    {
        Write("migrations/0002_add_example_column.sql", "ALTER TABLE dbo.Example ADD Name nvarchar(50) NULL;");
        Write("migrations/0001_create_example.sql", "CREATE TABLE dbo.Example (Id int NOT NULL);");
        Write("procedures/GetExample.sql", "CREATE OR ALTER PROCEDURE dbo.GetExample AS SELECT Id, Name FROM dbo.Example;");
        var database = sqlServer.ConnectionFor("MigratorTest");

        var first = DatabaseMigrator.Migrate(database, _scripts);
        var second = DatabaseMigrator.Migrate(database, _scripts);

        Assert.Equal(["0001_create_example.sql", "0002_add_example_column.sql"], first.MigrationsApplied);
        Assert.Equal(["GetExample.sql"], first.ProceduresApplied);
        Assert.Empty(second.MigrationsApplied);
        Assert.Equal(["GetExample.sql"], second.ProceduresApplied);
    }

    private void Write(string relativePath, string sql)
    {
        var path = Path.Combine(_scripts, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sql);
    }

    public void Dispose() => Directory.Delete(_scripts, recursive: true);
}
