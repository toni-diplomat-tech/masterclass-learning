using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A running Session for engine tests: an Open Masterclass with <c>participants</c> joined, whose
/// "unit-tests" Workshop Session runs the given mix (in every Question, option 1 is the correct one).
/// <see cref="ImportFile"/> is the Course's import file, to edit and import again.
/// </summary>
public sealed record RunningSessionFixture(int MasterclassId, string JoinCode, int SessionId, IReadOnlyList<string> Tokens, IReadOnlyList<SessionQuestion> Questions, QuestionStarted Question, JsonObject ImportFile)
{
    public static async Task<RunningSessionFixture> StartAsync(QuizEngine engine, int participants, int easy, int medium = 0, int hard = 0)
    {
        var ready = await ReadySessionFixture.PrepareAsync(engine, participants, easy, medium, hard);
        var started = await engine.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix);
        Assert.Empty(started.Problems);
        return new RunningSessionFixture(ready.MasterclassId, ready.JoinCode, started.Session!.Id, ready.Tokens, started.Session.Questions, started.Session.FirstQuestion, ready.ImportFile);
    }
}

/// <summary>
/// Everything <see cref="RunningSessionFixture"/> arranges except starting the Session: an Open Masterclass
/// with <c>participants</c> joined, and the "unit-tests" Workshop, whose Session can start with <see cref="Mix"/>.
/// </summary>
public sealed record ReadySessionFixture(int MasterclassId, string JoinCode, int WorkshopId, DifficultyCounts Mix, IReadOnlyList<string> Tokens, JsonObject ImportFile)
{
    public static async Task<ReadySessionFixture> PrepareAsync(QuizEngine engine, int participants, int easy, int medium = 0, int hard = 0)
    {
        var file = SessionFiles.WithWorkshopQuestions(easy, medium, hard);
        var import = await engine.ImportCourseAsync(file.ToJsonString());
        Assert.Empty(import.Problems);
        var workshopId = (await engine.ListCoursesAsync())
            .Single(c => c.Id == import.Outcome!.CourseId).Workshops
            .Single(w => w.Slug == SessionFiles.WorkshopSlug).Id;
        var created = await engine.CreateMasterclassAsync(import.Outcome!.CourseId, "Dev team 2026");
        var opened = await engine.OpenMasterclassAsync(created.Masterclass!.Id);

        var tokens = new List<string>();
        for (var i = 0; i < participants; i++)
        {
            tokens.Add((await engine.JoinMasterclassAsync(opened!.JoinCode!, null))!.Token);
        }
        return new ReadySessionFixture(opened!.Id, opened.JoinCode!, workshopId, new DifficultyCounts(easy, medium, hard), tokens, file);
    }
}
