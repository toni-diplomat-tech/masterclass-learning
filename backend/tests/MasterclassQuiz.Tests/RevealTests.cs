using System.Text.Json.Nodes;
using MasterclassQuiz.Api.Quiz;
using Microsoft.Extensions.Time.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>Revealing a Question: when it may happen, who is Timed out, and what everyone sees.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RevealTests(SqlServerFixture sqlServer)
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    // Easy Questions have 3 seconds to answer.
    private readonly FakeTimeProvider _time = new(Now);
    private QuizEngine Engine => new(sqlServer.Connection, _time, settings: new QuizSettings
    {
        TimeLimitSeconds = new TimeLimitSeconds { Easy = 3 },
    });

    [Fact]
    public async Task A_question_is_not_revealed_while_answers_still_count()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        _time.Advance(TimeSpan.FromMilliseconds(3_499));

        Assert.Null(await Engine.RevealQuestionAsync(session.SessionId));
        Assert.Equal(AnswerResult.Accepted, (await Engine.SubmitAnswerAsync(session.Tokens[1], session.Question.QuestionId, 2)).Result);
    }

    [Fact]
    public async Task The_reveal_shows_the_correct_option_and_how_many_chose_each_option()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 5, easy: 1);
        var questionId = session.Question.QuestionId;
        await Engine.SubmitAnswerAsync(session.Tokens[0], questionId, 1);
        await Engine.SubmitAnswerAsync(session.Tokens[1], questionId, 2);
        await Engine.SubmitAnswerAsync(session.Tokens[2], questionId, 2);
        await Engine.SubmitAnswerAsync(session.Tokens[3], questionId, 1);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));

        var revealed = await Engine.RevealQuestionAsync(session.SessionId);

        Assert.NotNull(revealed);
        Assert.Equal(
            (session.MasterclassId, session.SessionId, questionId, 1, 1, "Question easy-1?"),
            (revealed.MasterclassId, revealed.SessionId, revealed.QuestionId, revealed.Number, revealed.QuestionCount, revealed.Text));
        Assert.Equal(1, revealed.CorrectOption);
        Assert.Equal(
            [new RevealedOption(1, "Right answer to easy-1", 2), new RevealedOption(2, "Wrong answer to easy-1", 2), new RevealedOption(3, "Another wrong answer to easy-1", 0)],
            revealed.Options);
        Assert.Equal((4, 1), (revealed.Answered, revealed.TimedOut));
        Assert.Equal(_time.GetUtcNow().UtcDateTime, revealed.RevealedAt);
        Assert.Equal(DateTimeKind.Utc, revealed.RevealedAt.Kind);
    }

    [Fact]
    public async Task A_re_import_while_a_question_runs_changes_neither_its_options_nor_its_correct_option()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        var questionId = session.Question.QuestionId;
        await Engine.SubmitAnswerAsync(session.Tokens[0], questionId, 1);
        session.ImportFile.Question("easy-1")["options"] = new JsonArray(
            new JsonObject { ["text"] = "Wrong answer to easy-1" },
            new JsonObject { ["text"] = "Right answer to easy-1", ["correct"] = true });
        Assert.Empty((await Engine.ImportCourseAsync(session.ImportFile.ToJsonString())).Problems);

        var started = await Engine.GetStartedQuestionAsync(session.SessionId);
        var late = await Engine.SubmitAnswerAsync(session.Tokens[1], questionId, 3);
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        var revealed = await Engine.RevealQuestionAsync(session.SessionId);

        Assert.Equal(session.Question.Options, started!.Options);
        Assert.Equal(AnswerResult.Accepted, late.Result);
        Assert.NotNull(revealed);
        Assert.Equal(1, revealed.CorrectOption);
        Assert.Equal(
            [new RevealedOption(1, "Right answer to easy-1", 1), new RevealedOption(2, "Wrong answer to easy-1", 0), new RevealedOption(3, "Another wrong answer to easy-1", 1)],
            revealed.Options);
    }

    [Fact]
    public async Task A_question_is_revealed_early_once_every_participant_has_answered()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(await Engine.RevealQuestionAsync(session.SessionId));

        var last = await Engine.SubmitAnswerAsync(session.Tokens[1], session.Question.QuestionId, 3);
        var revealed = await Engine.RevealQuestionAsync(session.SessionId);

        Assert.True(last.Count!.EveryoneAnswered);
        Assert.NotNull(revealed);
        Assert.Equal((2, 0), (revealed.Answered, revealed.TimedOut));
        Assert.Equal(Now.UtcDateTime.AddSeconds(1), revealed.RevealedAt);
    }

    [Fact]
    public async Task Without_participants_a_question_runs_until_its_deadline()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 0, easy: 1);

        Assert.Null(await Engine.RevealQuestionAsync(session.SessionId));
        _time.Advance(TimeSpan.FromMilliseconds(3_500));
        Assert.NotNull(await Engine.RevealQuestionAsync(session.SessionId));
    }

    [Fact]
    public async Task A_question_is_revealed_once()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 2);
        _time.Advance(TimeSpan.FromSeconds(4));
        await Engine.RevealQuestionAsync(session.SessionId);

        Assert.Null(await Engine.RevealQuestionAsync(session.SessionId));
        Assert.Equal(AnswerResult.TooLate, (await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1)).Result);
    }

    [Fact]
    public async Task A_late_reveal_does_not_let_late_answers_in()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        _time.Advance(TimeSpan.FromSeconds(10)); // the timer is late

        var submitted = await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        var revealed = await Engine.RevealQuestionAsync(session.SessionId);

        Assert.Equal(AnswerResult.TooLate, submitted.Result);
        Assert.Equal((0, 1), (revealed!.Answered, revealed.TimedOut));
    }

    [Fact]
    public async Task Participants_who_joined_after_a_question_started_are_not_timed_out_for_it()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 2);
        _time.Advance(TimeSpan.FromSeconds(1));
        var late = (await Engine.JoinMasterclassAsync(session.JoinCode, null))!.Token;
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        _time.Advance(TimeSpan.FromSeconds(3));

        var first = await Engine.RevealQuestionAsync(session.SessionId);
        _time.Advance(TimeSpan.FromSeconds(5));
        var next = (await Engine.AdvanceSessionAsync(session.SessionId)).Started!;
        var lateAnswer = await Engine.SubmitAnswerAsync(late, next.QuestionId, 2);
        _time.Advance(TimeSpan.FromSeconds(4));
        var second = await Engine.RevealQuestionAsync(session.SessionId);

        Assert.Equal((1, 1), (first!.Answered, first.TimedOut)); // only the present non-answerer
        Assert.Equal(AnswerResult.Accepted, lateAnswer.Result); // the next Question is theirs
        Assert.Equal((1, 2), (second!.Answered, second.TimedOut));
    }
}
