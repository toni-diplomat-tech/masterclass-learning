using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>The Masterclass leaderboard: points summed over all of the Masterclass's Sessions; ties share a rank.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class MasterclassLeaderboardTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 13, 0, 0, TimeSpan.Zero);

    // Every Difficulty has 3 seconds to answer; no reveal pause. Easy Questions are worth 500 at once.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3, Medium = 3, Hard = 3 },
        RevealPauseSeconds = 0,
    });

    [Fact]
    public async Task The_masterclass_leaderboard_sums_every_session_and_ties_share_a_rank()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 3, easy: 1);
        var (first, second, third) = (session.Tokens[0], session.Tokens[1], session.Tokens[2]);

        // Session 1: first 500 (at once), second 375 (after 1.5 s), third 0 (wrong).
        await Engine.SubmitAnswerAsync(first, session.Question.QuestionId, 1);
        await Engine.SubmitAnswerAsync(third, session.Question.QuestionId, 2);
        _time.Advance(TimeSpan.FromMilliseconds(1_500));
        await Engine.SubmitAnswerAsync(second, session.Question.QuestionId, 1);
        await FinishAsync(session.SessionId);

        // Session 2: second 500, first 375, third 250 (at the limit).
        var started = await Engine.StartSessionAsync(session.MasterclassId, await WorkshopIdAsync(session.MasterclassId, "test-doubles"), new DifficultyCounts(1, 0, 0));
        var question = started.Session!.FirstQuestion.QuestionId;
        await Engine.SubmitAnswerAsync(second, question, 1);
        _time.Advance(TimeSpan.FromMilliseconds(1_500));
        await Engine.SubmitAnswerAsync(first, question, 1);
        _time.Advance(TimeSpan.FromMilliseconds(1_500));
        await Engine.SubmitAnswerAsync(third, question, 1);
        await FinishAsync(started.Session.Id);

        var leaderboard = await Engine.GetMasterclassLeaderboardAsync(session.MasterclassId);

        Assert.Equal(session.MasterclassId, leaderboard!.MasterclassId);
        Assert.Equal(
            [(1, 875), (1, 875), (3, 250)],
            leaderboard.Entries.Select(e => (e.Rank, e.Points)));
        Assert.Equal(
            [.. new[] { await NicknameAsync(first), await NicknameAsync(second) }.Order(StringComparer.Ordinal), await NicknameAsync(third)],
            leaderboard.Entries.Select(e => e.Nickname));
    }

    [Fact]
    public async Task Points_count_on_the_masterclass_leaderboard_only_once_revealed()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);

        Assert.Empty((await Engine.GetMasterclassLeaderboardAsync(session.MasterclassId))!.Entries);
    }

    [Fact]
    public async Task There_is_no_leaderboard_for_an_unknown_masterclass()
    {
        Assert.Null(await Engine.GetMasterclassLeaderboardAsync(-1));
    }

    private async Task FinishAsync(int sessionId)
    {
        Assert.NotNull(await Engine.RevealQuestionAsync(sessionId));
        Assert.NotNull((await Engine.AdvanceSessionAsync(sessionId)).Ended);
    }

    private async Task<string> NicknameAsync(string token) => (await Engine.GetParticipantAsync(token))!.Nickname;

    private async Task<int> WorkshopIdAsync(int masterclassId, string slug)
    {
        var courseId = (await Engine.GetMasterclassAsync(masterclassId))!.CourseId;
        return (await Engine.ListCoursesAsync()).Single(c => c.Id == courseId).Workshops.Single(w => w.Slug == slug).Id;
    }
}
