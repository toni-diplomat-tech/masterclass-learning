using MasterclassQuiz.Api.Database;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace MasterclassQuiz.Tests;

/// <summary>
/// One real SQL Server in Docker for the whole test run, migrated with the repo's scripts.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public DatabaseConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Connection = ConnectionFor("MasterclassQuiz");
        DatabaseMigrator.Migrate(Connection, DatabaseMigrator.DefaultScriptsDirectory);
    }

    /// <summary>Connection to a database of the given name on the test server.</summary>
    public DatabaseConnection ConnectionFor(string database) =>
        new(new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString);

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
