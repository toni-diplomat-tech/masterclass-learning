using DbUp;
using DbUp.Engine;
using DbUp.Helpers;

namespace MasterclassQuiz.Api.Database;

/// <summary>
/// Brings a database up to date with the SQL scripts in the repo's /database folder:
/// numbered scripts in <c>migrations</c> run once each, in order, and are journaled;
/// <c>CREATE OR ALTER</c> scripts in <c>functions</c>, then in <c>procedures</c>, run again on every call,
/// so a procedure can use any function.
/// </summary>
public static class DatabaseMigrator
{
    public static string DefaultScriptsDirectory => Path.Combine(AppContext.BaseDirectory, "database");

    public static MigrationReport Migrate(DatabaseConnection database, string scriptsDirectory)
    {
        EnsureDatabase.For.SqlDatabase(database.ConnectionString);

        var migrations = Run(database, ScriptsIn(scriptsDirectory, "migrations"), journaled: true);
        var functions = Run(database, ScriptsIn(scriptsDirectory, "functions"), journaled: false);
        var procedures = Run(database, ScriptsIn(scriptsDirectory, "procedures"), journaled: false);

        return new MigrationReport(
            migrations.Scripts.Select(s => s.Name).ToList(),
            functions.Scripts.Select(s => s.Name).ToList(),
            procedures.Scripts.Select(s => s.Name).ToList());
    }

    // A folder with no scripts yet is not copied to the build output, so treat a missing folder as empty.
    private static IEnumerable<SqlScript> ScriptsIn(string scriptsDirectory, string folder)
    {
        var path = Path.Combine(scriptsDirectory, folder);
        return Directory.Exists(path)
            ? Directory.GetFiles(path, "*.sql").Select(file => new SqlScript(Path.GetFileName(file), File.ReadAllText(file)))
            : [];
    }

    // Journaled scripts run once each; unjournaled ones run on every call.
    private static DatabaseUpgradeResult Run(DatabaseConnection database, IEnumerable<SqlScript> scripts, bool journaled)
    {
        var builder = DeployChanges.To
            .SqlDatabase(database.ConnectionString)
            .WithScripts(scripts)
            .WithTransactionPerScript()
            .LogToConsole();
        if (!journaled)
        {
            builder = builder.JournalTo(new NullJournal());
        }

        var result = builder.Build().PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database script {result.ErrorScript?.Name} failed.", result.Error);
        }
        return result;
    }
}

/// <summary>The script names one <see cref="DatabaseMigrator.Migrate"/> call ran, in run order.</summary>
public sealed record MigrationReport(
    IReadOnlyList<string> MigrationsApplied,
    IReadOnlyList<string> FunctionsApplied,
    IReadOnlyList<string> ProceduresApplied);
