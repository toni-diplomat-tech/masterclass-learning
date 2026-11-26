using MasterclassQuiz.Api.Live;
using MasterclassQuiz.Api.Quiz;
using static MasterclassQuiz.Tests.LiveMasterclassFixture;

namespace MasterclassQuiz.Tests;

/// <summary>
/// A broadcast to the group failing after the Quiz engine has made a change: the change stands, the caller
/// is told it was made, and the backend timer still follows and wakes the Session.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class LiveMasterclassBroadcastFailureTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly LiveMasterclassFixture _live = new(sqlServer);

    [Fact]
    public async Task A_session_whose_first_question_broadcast_fails_still_starts_and_the_timer_reveals_it()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, _live.Database, _live.Time, participants: 1, easy: 2);
        _live.Broadcasts.Fail(QuizHub.QuestionStarted);

        var problems = await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix);

        Assert.Empty(problems);
        await _live.Time.AdvanceWhenTimerWaitsAsync(AnswersClose);
        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.Equal((ready.MasterclassId, 1), (revealed.MasterclassId, revealed.TimedOut));
    }

    [Fact]
    public async Task Answers_are_accepted_and_the_last_one_reveals_the_question_even_if_the_answer_count_broadcast_fails()
    {
        var ready = await ReadySessionFixture.PrepareAsync(_live.Engine, _live.Database, _live.Time, participants: 2, easy: 2);
        Assert.Empty(await _live.Live.StartSessionAsync(ready.MasterclassId, ready.WorkshopId, ready.Mix));
        var question = await _live.Broadcasts.NextAsync<QuestionStarted>(QuizHub.QuestionStarted);
        _live.Broadcasts.Fail(QuizHub.AnswerCountUpdated);

        Assert.Equal(AnswerResult.Accepted, await _live.Live.SubmitAnswerAsync(ready.Tokens[0], question.QuestionId, 1));
        Assert.Equal(AnswerResult.Accepted, await _live.Live.SubmitAnswerAsync(ready.Tokens[1], question.QuestionId, 2));

        var revealed = await _live.Broadcasts.NextAsync<QuestionRevealed>(QuizHub.QuestionRevealed);
        Assert.Equal((question.QuestionId, 2, 0), (revealed.QuestionId, revealed.Answered, revealed.TimedOut));
    }

    [Fact]
    public async Task Joining_succeeds_even_if_the_participant_count_broadcast_fails()
    {
        var opened = await _live.OpenMasterclassAsync();
        _live.Broadcasts.Fail(QuizHub.ParticipantCountUpdated);

        var joined = await _live.Live.JoinMasterclassAsync(opened.JoinCode!, currentToken: null);

        Assert.Equal(opened.Id, joined!.Participant.MasterclassId);
        Assert.Equal(1, (await _live.Engine.GetMasterclassAsync(opened.Id))!.Participants);
    }

    [Fact]
    public async Task Closing_succeeds_even_if_the_closed_broadcast_fails()
    {
        var opened = await _live.OpenMasterclassAsync();
        _live.Broadcasts.Fail(QuizHub.MasterclassClosed);

        var closed = await _live.Live.CloseMasterclassAsync(opened.Id);

        Assert.Equal(MasterclassStatus.Closed, closed.Masterclass!.Status);
        Assert.Empty(closed.Problems);
    }

    public void Dispose() => _live.Dispose();
}
