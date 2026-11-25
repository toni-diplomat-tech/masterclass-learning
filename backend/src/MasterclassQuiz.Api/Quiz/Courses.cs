using System.Text.Json;
using System.Text.Json.Serialization;

namespace MasterclassQuiz.Api.Quiz;

/// <summary>Stored as 1/2/3; JSON says "Easy"/"Medium"/"Hard" (and still reads the numbers).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Difficulty>))]
public enum Difficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
}

/// <summary>
/// What an import did. <see cref="Problems"/> lists every reason the file was rejected; when there are
/// any, nothing was stored and <see cref="Summary"/> is null.
/// </summary>
public sealed record CourseImportResult(CourseImportSummary? Summary, IReadOnlyList<string> Problems)
{
    public bool Succeeded => Problems.Count == 0;

    public static CourseImportResult Rejected(params IReadOnlyList<string> problems) => new(null, problems);
}

/// <summary>The imported Course's size, and how its Questions changed compared with the previous import.</summary>
public sealed record CourseImportSummary(
    int CourseId,
    string CourseSlug,
    string CourseTitle,
    int Workshops,
    int Lessons,
    int Questions,
    int QuestionsAdded,
    int QuestionsUpdated,
    int QuestionsRetired);

/// <summary>Question counts that exclude retired Questions.</summary>
public sealed record DifficultyCounts(int Easy, int Medium, int Hard)
{
    public int Total => Easy + Medium + Hard;
}

/// <summary>A Course as the Host's back office lists it.</summary>
public sealed record CourseOverview(int Id, string Slug, string Title, IReadOnlyList<WorkshopOverview> Workshops);

public sealed record WorkshopOverview(int Id, string Slug, string Title, int Number, DifficultyCounts Questions);

/// <summary>A Course's current structure in order, with every Question of its Lessons, retired ones flagged.</summary>
public sealed record CourseDetail(int Id, string Slug, string Title, IReadOnlyList<WorkshopDetail> Workshops);

public sealed record WorkshopDetail(int Id, string Slug, string Title, int Number, IReadOnlyList<LessonDetail> Lessons);

public sealed record LessonDetail(int Id, string Slug, string Title, int Number, string Url, IReadOnlyList<QuestionDetail> Questions);

/// <summary><see cref="Id"/> is the database id; <see cref="StableId"/> is the id from the import file.</summary>
public sealed record QuestionDetail(
    int Id,
    string StableId,
    Difficulty Difficulty,
    string Text,
    bool Retired,
    IReadOnlyList<OptionDetail> Options);

/// <summary>An answer option, numbered from 1 in the order of the import file.</summary>
public sealed record OptionDetail(int Position, string Text, bool Correct);

/// <summary>
/// Reads an import file's JSON into the shape <c>dbo.ImportCourse</c> expects. Only the syntax and the
/// types are checked here; the rules (required fields, option counts, known Lessons...) are the procedure's.
/// </summary>
internal static class ImportFile
{
    // Property names are matched exactly (camelCase), as the procedure reads them.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false };

    /// <summary>The file as canonical JSON for the procedure, or the reason it can't be read.</summary>
    public static (string? Json, string? Problem) Normalize(string fileContents)
    {
        if (string.IsNullOrWhiteSpace(fileContents))
        {
            return (null, "The import file is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(fileContents, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            return (null, $"The import file is not valid JSON (line {ex.LineNumber + 1}, position {ex.BytePositionInLine + 1}).");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, "The import file must be a JSON object.");
            }
            try
            {
                var file = document.RootElement.Deserialize<FileDto>(Options)!;
                return (JsonSerializer.Serialize(file, Options), null);
            }
            catch (JsonException ex)
            {
                return (null, $"The import file has a value of the wrong type at {ex.Path}.");
            }
        }
    }

    private sealed record FileDto(CourseDto? Course, List<WorkshopDto?>? Workshops, List<QuestionDto?>? Questions);

    private sealed record CourseDto(string? Slug, string? Title);

    private sealed record WorkshopDto(string? Slug, string? Title, int? Number, List<LessonDto?>? Lessons);

    private sealed record LessonDto(string? Slug, string? Title, int? Number, string? Url);

    private sealed record QuestionDto(string? Id, string? Lesson, string? Difficulty, string? Text, List<OptionDto?>? Options);

    private sealed record OptionDto(string? Text, bool? Correct);
}

/// <summary>Reads the JSON that the course procedures build with <c>FOR JSON</c>.</summary>
internal static class ProcedureJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static T? Read<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json, Options);
}
