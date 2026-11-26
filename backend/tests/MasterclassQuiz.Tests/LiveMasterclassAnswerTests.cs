using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using static MasterclassQuiz.Tests.LiveMasterclassFixture;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Participants answering through the live Masterclass module: an accepted Answer updates the answer
/// count, and the last outstanding Answer has the backend timer reveal the Question at once.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassAnswerTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task An_accepted_answer_broadcasts_the_answer_count()
    {
        var (question, tokens) = await StartAsync(participants: 2);

        var result = await _live.Live.SubmitAnswerAsync(tokens[0], question.QuestionId, 1);

        Assert.Equal(AnswerResult.Accepted, result);
        var count = await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated);
        Assert.Equal(new AnswerCount(question.MasterclassId, question.SessionId, question.QuestionId, 1, 2), count);
    }

    [Fact]
    public async Task An_answer_while_others_are_still_answering_reveals_nothing_before_the_deadline()
    {
        var (question, tokens) = await StartAsync(participants: 3);

        await _live.Live.SubmitAnswerAsync(tokens[0], question.QuestionId, 1);
        await _live.Live.SubmitAnswerAsync(tokens[1], question.QuestionId, 2);

        Assert.Equal(1, (await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated)).Answered);
        Assert.Equal(2, (await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated)).Answered);
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task An_answer_from_an_unknown_participant_broadcasts_nothing()
    {
        var session = await RunningSessionFixture.StartAsync(_live.Engine, participants: 1, easy: 2);

        var result = await _live.Live.SubmitAnswerAsync("nobody", session.Question.QuestionId, 1);

        Assert.Equal(AnswerResult.UnknownParticipant, result);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task An_answer_to_a_question_that_is_not_running_broadcasts_nothing()
    {
        var session = await RunningSessionFixture.StartAsync(_live.Engine, participants: 1, easy: 2);
        var notStartedYet = session.Questions[1].QuestionId;

        var result = await _live.Live.SubmitAnswerAsync(session.Tokens[0], notStartedYet, 1);

        Assert.Equal(AnswerResult.NoSuchQuestion, result);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task A_second_answer_to_the_same_question_broadcasts_nothing()
    {
        var (question, tokens) = await StartAsync(participants: 2);
        await _live.Live.SubmitAnswerAsync(tokens[0], question.QuestionId, 2);
        await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated);

        var result = await _live.Live.SubmitAnswerAsync(tokens[0], question.QuestionId, 1);

        Assert.Equal(AnswerResult.AlreadyAnswered, result);
        await _live.Broadcasts.AssertQuietAsync();
    }

    [Fact]
    public async Task The_last_outstanding_answer_reveals_the_question_without_the_clock_moving()
    {
        var (question, tokens) = await StartAsync(participants: 2);
        await _live.Live.SubmitAnswerAsync(tokens[0], question.QuestionId, 1);
        await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated);
        var startedAt = _live.Time.GetUtcNow();

        Assert.Equal(AnswerResult.Accepted, await _live.Live.SubmitAnswerAsync(tokens[1], question.QuestionId, 2));

        var count = await _live.Broadcasts.NextAsync<AnswerCount>(QuizHub.AnswerCountUpdated);
        Assert.Equal((2, 2), (count.Answered, count.Participants));
        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.Equal((question.QuestionId, 2, 0), (revealed.QuestionId, revealed.Answered, revealed.TimedOut));
        Assert.Equal(startedAt.UtcDateTime, revealed.RevealedAt);
        var leaderboard = await _live.Broadcasts.NextAsync<SessionLeaderboard>(QuizHub.LeaderboardUpdated);
        Assert.Equal((question.SessionId, false), (leaderboard.SessionId, leaderboard.Ended));
        await _live.Time.WaitForTimerAsync();
        await _live.Broadcasts.AssertQuietAsync();
    }

    public void Dispose() => _live.Dispose();

    // Starts a two-Question Session through the module (so the backend timer follows it) and takes its
    // first QuestionStarted broadcast.
    private async Task<(QuestionStarted Question, IReadOnlyList<string> Tokens)> StartAsync(int participants)
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, participants, easy: 2);
        Assert.Empty(await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix));
        return (await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted), ready.Tokens);
    }
}
