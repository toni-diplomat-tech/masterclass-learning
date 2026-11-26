using MasterclassQuiz.Api.Database;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace MasterclassQuiz.Tests;

/// <summary>
/// One real SQL Server in Docker for the whole test run, migrated with the repo's scripts.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Express edition, like docker-compose.yml and ADR 0001.
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithEnvironment("MSSQL_PID", "Express")
        .Build();

    public DatabaseConnection Connection { get; private set; } = null!;

    /// <summary>
    /// <see cref="Connection"/> of the run's one fixture, for helpers that only get a Quiz engine
    /// (see <see cref="OnlyOpenMasterclass"/>).
    /// </summary>
    public static DatabaseConnection Shared { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Connection = ConnectionFor("MasterclassQuiz");
        Shared = Connection;
        DatabaseMigrator.Migrate(Connection, DatabaseMigrator.DefaultScriptsDirectory);
    }

    /// <summary>Connection to a database of the given name on the test server.</summary>
    public DatabaseConnection ConnectionFor(string database) =>
        new(new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
