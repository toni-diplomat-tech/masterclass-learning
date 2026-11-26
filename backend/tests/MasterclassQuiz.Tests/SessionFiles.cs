using System.Text.Json.Nodes;

namespace MasterclassQuiz.Tests;

/// <summary>Import files for Session tests: the example Course with as many Questions as a test needs.</summary>
public static class SessionFiles
{
    /// <summary>The Workshop whose Questions <see cref="WithWorkshopQuestions"/> replaces.</summary>
    public const string WorkshopSlug = "unit-tests";

    /// <summary>
    /// The example Course under a unique slug, its "unit-tests" Workshop holding exactly the given number of
    /// Questions per Difficulty (ids <c>easy-1</c>, <c>medium-1</c>, ...). The other Workshop keeps its own
    /// Questions, so a Session must not take from it.
    /// </summary>
    public static JsonObject WithWorkshopQuestions(int easy, int medium, int hard)
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        var questions = file["questions"]!.AsArray();
        foreach (var question in questions.Where(q => ((string)q!["id"]!).StartsWith("unit-tests-")).ToList())
        {
            questions.Remove(question);
        }
        foreach (var (difficulty, count) in new[] { ("Easy", easy), ("Medium", medium), ("Hard", hard) })
        {
            for (var i = 1; i <= count; i++)
            {
                questions.Add(Question($"{difficulty.ToLowerInvariant()}-{i}", difficulty));
            }
        }
        return file;
    }

    /// <summary>
    /// Renumbers the Workshop to come after every other one in Course order. Re-importing the file after its
    /// Session ran makes Course order differ from the order the Sessions ran in.
    /// </summary>
    public static void MoveWorkshopLast(this JsonObject file, string workshopSlug)
    {
        var workshops = file["workshops"]!.AsArray();
        var last = workshops.Max(w => (int)w!["number"]!);
        workshops.Single(w => (string?)w!["slug"] == workshopSlug)!["number"] = last + 1;
    }

    public static JsonObject Question(string id, string difficulty) => new()
    {
        ["id"] = id,
        ["lesson"] = "why-we-test",
        ["difficulty"] = difficulty,
        ["text"] = $"Question {id}?",
        ["options"] = new JsonArray(
            new JsonObject { ["text"] = $"Right answer to {id}", ["correct"] = true },
            new JsonObject { ["text"] = $"Wrong answer to {id}" },
            new JsonObject { ["text"] = $"Another wrong answer to {id}" }),
    };
}
