using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Scoring and the Session leaderboard: a Correct Answer scores base × (1 − 0.5 × elapsed / limit), rounded
/// (Easy 500, Medium 750, Hard 1000); Incorrect and Timed out score 0; equal scores share a rank.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class ScoringTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Every Difficulty has 3 seconds to answer (3.5 s with grace); the reveal pause is 2 seconds.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3, Medium = 3, Hard = 3 },
        RevealPauseSeconds = 2,
    });

    [Fact]
    public async Task A_correct_answer_given_at_once_scores_the_full_base_points()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);

        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(session.SessionId);

        var leaderboard = await Engine.GetSessionLeaderboardAsync(session.SessionId);
        var entry = Assert.Single(leaderboard!.Entries);
        Assert.Equal((1, 500, 500), (entry.Rank, entry.Points, entry.QuestionPoints));
    }

    [Theory]
    [InlineData(1_000, 417)] // 500 × (1 − 0.5 × 1/3) = 416.67
    [InlineData(3_000, 250)] // at the limit: half the base points
    [InlineData(3_400, 250)] // in the grace period: never below half
    [InlineData(3_500, 250)] // the last moment of the grace period
    public async Task A_correct_answer_scores_less_the_longer_it_took(int elapsedMs, int points)
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        _time.Advance(TimeSpan.FromMilliseconds(elapsedMs));

        Assert.Equal(AnswerResult.Accepted, (await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1)).Result);
        await Engine.RevealQuestionAsync(session.SessionId);

        Assert.Equal(points, Assert.Single((await Engine.GetSessionLeaderboardAsync(session.SessionId))!.Entries).Points);
    }

    [Fact]
    public async Task Harder_questions_are_worth_more_and_the_session_sums_them()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1, medium: 1, hard: 1);
        var scored = new List<(int Points, int QuestionPoints)>();

        var question = session.Question;
        for (var i = 0; i < 3; i++)
        {
            await Engine.SubmitAnswerAsync(session.Tokens[0], question.QuestionId, 1);
            await Engine.RevealQuestionAsync(session.SessionId);
            var entry = Assert.Single((await Engine.GetSessionLeaderboardAsync(session.SessionId))!.Entries);
            scored.Add((entry.Points, entry.QuestionPoints));
            _time.Advance(TimeSpan.FromSeconds(2));
            question = (await Engine.AdvanceSessionAsync(session.SessionId)).Started!;
        }

        Assert.Equal([(500, 500), (1_250, 750), (2_250, 1_000)], scored);
    }

    [Fact]
    public async Task Incorrect_and_timed_out_answers_score_nothing()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 2);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(session.SessionId);

        var entries = (await Engine.GetSessionLeaderboardAsync(session.SessionId))!.Entries;

        Assert.Equal([(1, 0, 0), (1, 0, 0)], entries.Select(e => (e.Rank, e.Points, e.QuestionPoints)));
    }

    [Fact]
    public async Task Points_count_only_once_the_question_is_revealed()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);

        var leaderboard = await Engine.GetSessionLeaderboardAsync(session.SessionId);

        Assert.Empty(leaderboard!.Entries);
        Assert.False(leaderboard.Ended);
    }

    [Fact]
    public async Task Participants_with_the_same_points_share_a_rank()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 4, easy: 1);
        var nicknames = new List<string>();
        foreach (var token in session.Tokens)
        {
            nicknames.Add((await Engine.GetParticipantAsync(token))!.Nickname);
        }
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        await Engine.SubmitAnswerAsync(session.Tokens[1], session.Question.QuestionId, 1);
        _time.Advance(TimeSpan.FromMilliseconds(1_500));
        await Engine.SubmitAnswerAsync(session.Tokens[2], session.Question.QuestionId, 1);
        await Engine.SubmitAnswerAsync(session.Tokens[3], session.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(session.SessionId);

        var leaderboard = await Engine.GetSessionLeaderboardAsync(session.SessionId);

        Assert.Equal(
            [
                (1, 500), (1, 500), // equal points: same rank
                (3, 375), (3, 375), // the next rank counts everyone ahead
            ],
            leaderboard!.Entries.Select(e => (e.Rank, e.Points)));
        Assert.Equal(
            [.. new[] { nicknames[0], nicknames[1] }.Order(StringComparer.Ordinal), .. new[] { nicknames[2], nicknames[3] }.Order(StringComparer.Ordinal)],
            leaderboard.Entries.Select(e => e.Nickname));
    }

    [Fact]
    public async Task The_final_leaderboard_says_the_session_ended()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(session.SessionId);

        var leaderboard = await Engine.GetSessionLeaderboardAsync(session.SessionId);

        Assert.True(leaderboard!.Ended);
        Assert.Equal((session.MasterclassId, session.SessionId), (leaderboard.MasterclassId, leaderboard.SessionId));
        Assert.Equal(500, Assert.Single(leaderboard.Entries).Points);
    }

    [Fact]
    public async Task There_is_no_leaderboard_for_an_unknown_session()
    {
        Assert.Null(await Engine.GetSessionLeaderboardAsync(-1));
    }
}
