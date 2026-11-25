using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

namespace MasterclassQuiz.Api.Database;

/// <summary>
/// Brings a database up to date with the SQL scripts in the repo's /database folder:
/// numbered scripts in <c>migrations</c> run once each, in order, and are journaled;
/// <c>CREATE OR ALTER</c> scripts in <c>procedures</c> run again on every call.
/// </summary>
public static class DatabaseMigrator
{
    public static string DefaultScriptsDirectory => Path.Combine(AppContext.BaseDirectory, "database");

    public static MigrationReport Migrate(string connectionString, string scriptsDirectory)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var migrations = Run(DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsFromFileSystem(ScriptFolder(scriptsDirectory, "migrations"))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build());

        var procedures = Run(DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsFromFileSystem(ScriptFolder(scriptsDirectory, "procedures"))
            .JournalTo(new NullJournal())
            .WithTransactionPerScript()
            .LogToConsole()
            .Build());

        return new MigrationReport(
            migrations.Scripts.Select(s => s.Name).ToList(),
            procedures.Scripts.Select(s => s.Name).ToList());
    }

    // A folder with no scripts yet is not copied to the build output, so create it rather than fail.
    private static string ScriptFolder(string scriptsDirectory, string name) =>
        Directory.CreateDirectory(Path.Combine(scriptsDirectory, name)).FullName;

    private static DatabaseUpgradeResult Run(UpgradeEngine upgrader)
    {
        var result = upgrader.PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database script {result.ErrorScript?.Name} failed.", result.Error);
        }
        return result;
    }
}

/// <summary>The script names one <see cref="DatabaseMigrator.Migrate"/> call ran, in run order.</summary>
public sealed record MigrationReport(IReadOnlyList<string> MigrationsApplied, IReadOnlyList<string> ProceduresApplied);
