using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class SessionStartTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private readonly QuizEngine _engine = new(sqlServer.Connection, new FakeTimeProvider(Now));

    [Fact]
    public async Task A_session_runs_the_requested_mix_from_easy_to_hard()
    {
        var meeting = await OpenMeetingAsync(easy: 4, medium: 4, hard: 4);

        var started = await _engine.StartSessionAsync(meeting.MasterclassId, meeting.WorkshopId, new DifficultyCounts(2, 2, 1));

        Assert.Empty(started.Problems);
        Assert.Equal(
            [Difficulty.Easy, Difficulty.Easy, Difficulty.Medium, Difficulty.Medium, Difficulty.Hard],
            started.Session!.Questions.Select(q => q.Difficulty));
    }

    private async Task<Meeting> OpenMeetingAsync(int easy, int medium, int hard)
    {
        var import = await _engine.ImportCourseAsync(SessionFiles.WithWorkshopQuestions(easy, medium, hard).ToJsonString());
        Assert.Empty(import.Problems);
        var workshopId = (await _engine.ListCoursesAsync())
            .Single(c => c.Id == import.Summary!.CourseId).Workshops
            .Single(w => w.Slug == SessionFiles.WorkshopSlug).Id;
        var created = await _engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        await _engine.OpenMasterclassAsync(created.Masterclass!.Id);
        return new Meeting(created.Masterclass.Id, workshopId, import.Summary.CourseSlug);
    }

    private sealed record Meeting(int MasterclassId, int WorkshopId, string CourseSlug);
}
