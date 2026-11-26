using System.Text.Json.Nodes;

namespace MasterclassQuiz.Tests;

/// <summary>The documented example import file (docs/import-file), to use as is or edit per test.</summary>
public static class ImportFiles
{
    public static string ExamplePath => Path.Combine(AppContext.BaseDirectory, "import-file", "testing-fundamentals.json");

    /// <summary>
    /// The example as an editable JSON tree. Pass a slug to keep each test's Course apart on the shared database.
    /// </summary>
    public static JsonObject Example(string? courseSlug = null)
    {
        var file = JsonNode.Parse(File.ReadAllText(ExamplePath))!.AsObject();
        if (courseSlug is not null)
        {
            file["course"]!["slug"] = courseSlug;
        }
        return file;
    }

    public static string UniqueSlug() => $"course-{Guid.NewGuid():N}"[..20];

    public static JsonObject Question(this JsonObject file, string id) =>
        file["questions"]!.AsArray().Single(q => (string?)q!["id"] == id)!.AsObject();
}
