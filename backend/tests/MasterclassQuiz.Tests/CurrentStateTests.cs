using System.Text.Json;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// GetCurrentState: what a (re)connecting phone or projector should show right now, since SignalR doesn't
/// replay the messages it missed.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class CurrentStateTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Easy Questions have 3 seconds to answer (3.5 s with grace); a revealed Question stays up 2 seconds.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
        RevealPauseSeconds = 2,
    });

    [Fact]
    public async Task Before_any_session_everyone_waits()
    {
        var import = await Engine.ImportCourseAsync(SessionFiles.WithWorkshopQuestions(1, 0, 0).ToJsonString());
        var created = await Engine.CreateMasterclassAsync(import.Summary!.CourseId, "Dev team 2026");
        var opened = await Engine.OpenMasterclassAsync(created.Masterclass!.Id);
        var token = (await Engine.JoinMasterclassAsync(opened!.JoinCode!, null))!.Token;

        var state = await Engine.GetCurrentStateAsync(opened.Id, token);

        Assert.NotNull(state);
        Assert.Equal((opened.Id, LiveStep.Waiting), (state.MasterclassId, state.Step));
        Assert.Equal((null, null, null, null), (state.Question, state.Count, state.Revealed, state.Ended));
        Assert.Null(state.Participant!.Answer);
    }

    [Fact]
    public async Task A_running_question_comes_back_with_its_deadline_and_answer_count()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 3, easy: 2);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 2);
        _time.Advance(TimeSpan.FromSeconds(1));

        var state = await Engine.GetCurrentStateAsync(meeting.MasterclassId, participantToken: null);

        Assert.NotNull(state);
        Assert.Equal(LiveStep.Question, state.Step);
        Assert.Equal(Json(meeting.Question), Json(state.Question));
        Assert.Equal(Now.UtcDateTime.AddSeconds(3), state.Question!.Deadline);
        Assert.Equal(new AnswerCount(meeting.MasterclassId, meeting.SessionId, meeting.Question.QuestionId, 1, 3), state.Count);
        Assert.Equal((null, null, null), (state.Revealed, state.Ended, state.Participant));
    }

    [Fact]
    public async Task A_participant_who_already_answered_gets_their_option_back_but_not_its_outcome()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 2);

        var answered = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]))!.Participant!;
        var notYet = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[1]))!.Participant!;

        Assert.Equal(new OwnAnswer(meeting.Question.QuestionId, CanAnswer: true, Option: 2, Outcome: null), answered.Answer);
        Assert.Equal(new OwnAnswer(meeting.Question.QuestionId, CanAnswer: true, Option: null, Outcome: null), notYet.Answer);
    }

    [Fact]
    public async Task A_participant_who_joined_during_the_question_cannot_answer_it()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        _time.Advance(TimeSpan.FromSeconds(1));
        var late = (await Engine.JoinMasterclassAsync(meeting.JoinCode, null))!;

        var state = await Engine.GetCurrentStateAsync(meeting.MasterclassId, late.Token);

        Assert.Equal(new ParticipantState(late.Participant.Id, late.Participant.Nickname, new OwnAnswer(meeting.Question.QuestionId, false, null, null), Standing: null), state!.Participant);
    }

    [Fact]
    public async Task A_participant_of_another_masterclass_gets_no_participant_state()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        var other = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);

        var state = await Engine.GetCurrentStateAsync(meeting.MasterclassId, other.Tokens[0]);

        Assert.Equal(LiveStep.Question, state!.Step);
        Assert.Null(state.Participant);
    }

    [Fact]
    public async Task A_revealed_question_comes_back_with_each_participants_outcome()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 3, easy: 2);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[1], meeting.Question.QuestionId, 3);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        var revealed = await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(1));

        var states = new List<CurrentState>();
        foreach (var token in meeting.Tokens)
        {
            states.Add((await Engine.GetCurrentStateAsync(meeting.MasterclassId, token))!);
        }

        Assert.All(states, state => Assert.Equal((LiveStep.Revealed, Json(revealed), null, null), (state.Step, Json(state.Revealed), state.Question, state.Ended)));
        Assert.Equal(
            [
                new OwnAnswer(meeting.Question.QuestionId, true, 1, AnswerOutcome.Correct),
                new OwnAnswer(meeting.Question.QuestionId, true, 3, AnswerOutcome.Incorrect),
                new OwnAnswer(meeting.Question.QuestionId, true, null, AnswerOutcome.TimedOut),
            ],
            states.Select(state => state.Participant!.Answer));
    }

    [Fact]
    public async Task A_revealed_question_comes_back_with_the_session_leaderboard_and_each_participants_standing()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 3, easy: 2);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1); // at once: 500 points
        await Engine.SubmitAnswerAsync(meeting.Tokens[1], meeting.Question.QuestionId, 3);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(meeting.SessionId);

        var states = new List<CurrentState>();
        foreach (var token in meeting.Tokens)
        {
            states.Add((await Engine.GetCurrentStateAsync(meeting.MasterclassId, token))!);
        }
        var projector = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, participantToken: null))!;

        Assert.Equal(Json(await Engine.GetSessionLeaderboardAsync(meeting.SessionId)), Json(projector.Leaderboard));
        Assert.False(projector.Leaderboard!.Ended);
        Assert.Equal(
            [(1, 500, 500), (2, 0, 0), (2, 0, 0)],
            states.Select(state => state.Participant!.Standing!).Select(entry => (entry.Rank, entry.Points, entry.QuestionPoints)));
        Assert.Equal(states.Select(state => state.Participant!.ParticipantId), states.Select(state => state.Participant!.Standing!.ParticipantId));
    }

    [Fact]
    public async Task A_running_question_comes_back_without_a_leaderboard()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);

        var state = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]))!;

        Assert.Equal((LiveStep.Question, null, null), (state.Step, state.Leaderboard, state.Participant!.Standing));
    }

    [Fact]
    public async Task Session_over_comes_back_with_the_final_leaderboard_and_the_participants_rank()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[1], meeting.Question.QuestionId, 1);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(meeting.SessionId);

        var first = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]))!;
        var second = (await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[1]))!;

        Assert.Equal((LiveStep.Ended, true), (first.Step, first.Leaderboard!.Ended));
        Assert.Equal((2, 0), (first.Participant!.Standing!.Rank, first.Participant.Standing.Points));
        Assert.Equal((1, 500), (second.Participant!.Standing!.Rank, second.Participant.Standing.Points));
    }

    [Fact]
    public async Task The_next_question_follows_the_reveal_pause()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 2);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        var next = (await Engine.AdvanceSessionAsync(meeting.SessionId)).Started!;

        var state = await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]);

        Assert.Equal((LiveStep.Question, Json(next)), (state!.Step, Json(state.Question)));
        Assert.Equal(new OwnAnswer(next.QuestionId, true, null, null), state.Participant!.Answer);
    }

    [Fact]
    public async Task After_the_session_ends_everyone_sees_session_over()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        var ended = (await Engine.AdvanceSessionAsync(meeting.SessionId)).Ended;

        var state = await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]);

        Assert.Equal((LiveStep.Ended, ended, null, null), (state!.Step, state.Ended, state.Question, state.Revealed));
        Assert.Null(state.Participant!.Answer);
    }

    [Fact]
    public async Task A_closed_masterclass_says_so_to_screens_that_missed_the_close()
    {
        var meeting = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(meeting.SessionId);
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(meeting.SessionId);
        var whileOpen = await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]);

        Assert.True((await Engine.CloseMasterclassAsync(meeting.MasterclassId)).Succeeded);
        var afterClose = await Engine.GetCurrentStateAsync(meeting.MasterclassId, meeting.Tokens[0]);

        Assert.Equal(MasterclassStatus.Open, whileOpen!.MasterclassStatus);
        Assert.Equal((MasterclassStatus.Closed, LiveStep.Ended), (afterClose!.MasterclassStatus, afterClose.Step));
    }

    [Fact]
    public async Task Running_sessions_are_listed_until_they_end()
    {
        var running = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        var ending = await MeetingFixture.StartAsync(Engine, participants: 1, easy: 1);
        await Engine.SubmitAnswerAsync(ending.Tokens[0], ending.Question.QuestionId, 1);
        await Engine.RevealQuestionAsync(ending.SessionId);
        var revealedOnly = await Engine.ListRunningSessionsAsync();
        _time.Advance(TimeSpan.FromSeconds(2));
        await Engine.AdvanceSessionAsync(ending.SessionId);

        var afterEnd = await Engine.ListRunningSessionsAsync();

        Assert.Contains(running.SessionId, revealedOnly);
        Assert.Contains(ending.SessionId, revealedOnly);
        Assert.Contains(running.SessionId, afterEnd);
        Assert.DoesNotContain(ending.SessionId, afterEnd);
        Assert.Equal([running.SessionId], await Engine.ListRunningSessionsAsync(running.MasterclassId));
    }

    [Fact]
    public async Task There_is_no_state_for_an_unknown_masterclass()
    {
        Assert.Null(await Engine.GetCurrentStateAsync(int.MaxValue, participantToken: null));
    }

    // Records holding lists compare the lists by reference; compare what goes over the wire instead.
    private static string Json(object? value) => JsonSerializer.Serialize(value);
}
