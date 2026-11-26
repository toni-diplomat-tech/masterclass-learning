using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The backend restarts mid-Session: on startup it resumes every running Session through the live Masterclass
/// module, so an overdue Question is revealed (Timed out for whoever didn't answer) and the Session carries on
/// to its end.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RestartRecoveryTests(SqlServerFixture sqlServer)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task After_a_restart_an_overdue_question_is_revealed_and_the_session_carries_on()
    {
        // The Session started while the backend was up, then nothing moved it on: no backend runs yet.
        var engine = new QuizEngine(sqlServer.Connection, TimeProvider.System, settings: new QuizSettings
        {
            TimeLimitSeconds = new TimeLimitSeconds { Easy = 1 },
        });
        var session = await RunningSessionFixture.StartAsync(engine, participants: 1, easy: 2);
        await Task.Delay(TimeSpan.FromSeconds(1.6)); // past the first Question's deadline and grace

        using var restarted = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
            .WithWebHostBuilder(builder => builder
                .UseSetting("Live:ResumeSessionsOnStartup", "true")
                .UseSetting("Quiz:RevealPauseSeconds", "0.3"));
        _ = restarted.Services; // starts the backend

        var second = await WaitForAsync(engine, session, state => state is { Step: LiveStep.Question, Question.Number: 2 });
        var firstRevealed = await engine.GetRevealedQuestionAsync(session.SessionId);
        Assert.Equal((session.Question.QuestionId, 0, 1), (firstRevealed!.QuestionId, firstRevealed.Answered, firstRevealed.TimedOut));
        Assert.Equal(session.Questions[1].QuestionId, second.Question!.QuestionId);

        var ended = await WaitForAsync(engine, session, state => state.Step == LiveStep.Ended);
        Assert.Equal(session.SessionId, ended.Ended!.SessionId);
    }

    // Polls the Masterclass's state until it matches (the backend timer moves it on in the background).
    private static async Task<CurrentState> WaitForAsync(QuizEngine engine, RunningSessionFixture session, Func<CurrentState, bool> reached)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var state = (await engine.GetCurrentStateAsync(session.MasterclassId, session.Tokens[0], timeout.Token))!;
            if (reached(state))
            {
                return state;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }
}
