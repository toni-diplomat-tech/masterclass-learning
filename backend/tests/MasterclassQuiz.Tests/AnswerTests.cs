using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>Answering a running Question: the server's time decides, one Answer each, never changed.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class AnswerTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Easy Questions have 3 seconds to answer.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
    });

    [Theory]
    [InlineData(0, AnswerResult.Accepted)]
    [InlineData(3_000, AnswerResult.Accepted)]
    [InlineData(3_499, AnswerResult.Accepted)]
    [InlineData(3_501, AnswerResult.TooLate)]
    public async Task An_answer_counts_until_half_a_second_after_the_time_limit(int afterStartMs, AnswerResult expected)
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        _time.Advance(TimeSpan.FromMilliseconds(afterStartMs));

        var submitted = await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);

        Assert.Equal(expected, submitted.Result);
    }

    [Fact]
    public async Task A_participant_answers_a_question_once_and_cannot_change_it()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 2);

        var again = await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);

        Assert.Equal(AnswerResult.AlreadyAnswered, again.Result);
        Assert.Equal(1, again.Count!.Answered);
    }

    [Fact]
    public async Task Each_accepted_answer_raises_the_questions_answer_count()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 3, easy: 1);

        var first = await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        var second = await Engine.SubmitAnswerAsync(session.Tokens[1], session.Question.QuestionId, 2);

        Assert.Equal(new AnswerCount(session.MasterclassId, session.SessionId, session.Question.QuestionId, 1, 3), first.Count);
        Assert.Equal(new AnswerCount(session.MasterclassId, session.SessionId, session.Question.QuestionId, 2, 3), second.Count);
        Assert.False(second.Count!.EveryoneAnswered);
    }

    [Fact]
    public async Task A_participant_who_joined_after_the_question_started_cannot_answer_it()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        _time.Advance(TimeSpan.FromSeconds(1));
        var late = (await Engine.JoinMasterclassAsync(session.JoinCode, null))!.Token;

        var submitted = await Engine.SubmitAnswerAsync(late, session.Question.QuestionId, 1);

        Assert.Equal(AnswerResult.JoinedAfterStart, submitted.Result);
        Assert.Equal(1, submitted.Count!.Participants);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)] // the Questions have three options
    public async Task An_answer_must_be_one_of_the_questions_options(int option)
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);

        var submitted = await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, option);

        Assert.Equal(AnswerResult.UnknownOption, submitted.Result);
        Assert.Equal(0, submitted.Count!.Answered);
    }

    [Fact]
    public async Task Only_a_question_that_ran_in_the_participants_masterclass_can_be_answered()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 2);
        var other = await RunningSessionFixture.StartAsync(Engine, sqlServer.Connection, _time, participants: 1, easy: 1);
        var notStartedYet = session.Questions[1].QuestionId;

        Assert.Equal(AnswerResult.NoSuchQuestion, (await Engine.SubmitAnswerAsync(session.Tokens[0], other.Question.QuestionId, 1)).Result);
        Assert.Equal(AnswerResult.UnknownParticipant, (await Engine.SubmitAnswerAsync("nobody", session.Question.QuestionId, 1)).Result);
        Assert.Equal(AnswerResult.NoSuchQuestion, (await Engine.SubmitAnswerAsync(session.Tokens[0], notStartedYet, 1)).Result);
    }
}
