using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A running Session for engine tests: an Open Masterclass with <c>participants</c> joined, whose
/// "unit-tests" Workshop Session runs the given mix (in every Question, option 1 is the correct one).
/// <see cref="ImportFile"/> is the Course's import file, to edit and import again.
/// </summary>
public sealed record MeetingFixture(int MasterclassId, string JoinCode, int SessionId, IReadOnlyList<string> Tokens, IReadOnlyList<SessionQuestion> Questions, QuestionStarted Question, JsonObject ImportFile)
{
    public static async Task<MeetingFixture> StartAsync(QuizEngine engine, int participants, int easy, int medium = 0, int hard = 0)
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

        var started = await engine.StartSessionAsync(opened!.Id, workshopId, new DifficultyCounts(easy, medium, hard));
        Assert.Empty(started.Problems);
        return new MeetingFixture(opened.Id, opened.JoinCode!, started.Session!.Id, tokens, started.Session.Questions, started.Session.FirstQuestion, file);
    }
}
