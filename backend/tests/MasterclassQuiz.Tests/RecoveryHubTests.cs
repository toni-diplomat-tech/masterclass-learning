using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.Participants;
using MasterclassQuiz.Api.Quiz;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace MasterclassQuiz.Tests;

/// <summary>
/// Nothing is lost when a phone or the projector reloads (GetCurrentState on every connect), and the
/// projector's Resume button makes the backend follow a Session nobody was moving on.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RecoveryHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
        .WithWebHostBuilder(builder => builder
            .UseSetting("Quiz:TimeLimitSeconds:Easy", "30")
            .UseSetting("Quiz:RevealPauseSeconds", "0.3"));

    private QuizEngine Engine => _app.Services.GetRequiredService<QuizEngine>();

    [Fact]
    public async Task A_reloaded_phone_gets_the_running_question_and_its_locked_answer_but_not_the_correct_option()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        var cookie = $"{ParticipantEndpoints.CookieName}={meeting.Tokens[0]}";
        await using (var before = await ConnectedAsync(cookie))
        {
            await before.InvokeAsync<JsonObject>("GetCurrentState", meeting.MasterclassId);
            Assert.Equal("Accepted", (string?)(await before.InvokeAsync<JsonObject>("SubmitAnswer", meeting.Question.QuestionId, 2))["result"]);
        }

        await using var reloaded = await ConnectedAsync(cookie);
        var state = await reloaded.InvokeAsync<JsonObject>("GetCurrentState", meeting.MasterclassId);

        Assert.Equal(("Question", meeting.Question.QuestionId), ((string?)state["step"], (int)state["question"]!["questionId"]!));
        Assert.Equal(meeting.Question.Deadline, (DateTime)state["question"]!["deadline"]!);
        var answer = state["participant"]!["answer"]!;
        Assert.Equal((true, 2, null), ((bool)answer["canAnswer"]!, (int?)answer["option"], (string?)answer["outcome"]));
        Assert.DoesNotContain("correct", state.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_reloaded_projector_gets_the_running_question_with_its_answer_count()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(meeting.Tokens[0], meeting.Question.QuestionId, 1);
        await using var projector = await ConnectedAsync(await HostCookieAsync());

        var state = await projector.InvokeAsync<JsonObject>("GetCurrentState", meeting.MasterclassId);

        Assert.Equal(("Question", meeting.Question.QuestionId), ((string?)state["step"], (int)state["question"]!["questionId"]!));
        Assert.Equal((1, 2), ((int)state["count"]!["answered"]!, (int)state["count"]!["participants"]!));
        Assert.Null(state["participant"]);
    }

    [Fact]
    public async Task A_stranger_gets_no_state_and_cannot_resume()
    {
        var meeting = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        var other = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        await using var stranger = await ConnectedAsync(cookie: null);
        await using var otherPhone = await ConnectedAsync($"{ParticipantEndpoints.CookieName}={other.Tokens[0]}");
        await using var phone = await ConnectedAsync($"{ParticipantEndpoints.CookieName}={meeting.Tokens[0]}");

        Assert.Null(await stranger.InvokeAsync<JsonObject?>("GetCurrentState", meeting.MasterclassId));
        Assert.Null(await otherPhone.InvokeAsync<JsonObject?>("GetCurrentState", meeting.MasterclassId));
        await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<JsonObject>("ResumeSession", meeting.MasterclassId));
    }

    [Fact]
    public async Task Resume_moves_on_a_session_that_nobody_was_following()
    {
        // Started straight through the engine, with a 1 s limit: no backend timer follows this Session.
        var engine = new QuizEngine(sqlServer.Connection, TimeProvider.System, settings: new QuizSettings { TimeLimitSeconds = new TimeLimitSeconds { Easy = 1 } });
        var meeting = await RunningSessionFixture.StartAsync(engine, participants: 1, easy: 2);
        await Task.Delay(TimeSpan.FromSeconds(1.6)); // past the deadline and grace
        await using var phone = await ConnectedAsync($"{ParticipantEndpoints.CookieName}={meeting.Tokens[0]}");
        var onPhone = Listen(phone, "QuestionRevealed");
        await using var projector = await ConnectedAsync(await HostCookieAsync());
        var onProjector = Listen(projector, "QuestionRevealed");
        Assert.Equal("Question", (string?)(await phone.InvokeAsync<JsonObject>("GetCurrentState", meeting.MasterclassId))["step"]);
        await projector.InvokeAsync<JsonObject>("GetCurrentState", meeting.MasterclassId);

        var resumed = await projector.InvokeAsync<JsonObject>("ResumeSession", meeting.MasterclassId);

        Assert.Equal(meeting.MasterclassId, (int)resumed["masterclassId"]!);
        foreach (var revealed in new[] { await NextAsync(onProjector), await NextAsync(onPhone) })
        {
            Assert.Equal((meeting.Question.QuestionId, 0, 1), ((int)revealed["questionId"]!, (int)revealed["answered"]!, (int)revealed["timedOut"]!));
        }
    }

    private static async Task<JsonObject> NextAsync(ChannelReader<JsonObject> messages)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        return await messages.ReadAsync(timeout.Token);
    }

    private static ChannelReader<JsonObject> Listen(HubConnection connection, string name)
    {
        var messages = Channel.CreateUnbounded<JsonObject>();
        connection.On<JsonObject>(name, message => messages.Writer.TryWrite(message));
        return messages.Reader;
    }

    private async Task<string> JoinAsync(string joinCode)
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var join = await client.PostAsJsonAsync("/api/participants/join", new { joinCode });
        join.EnsureSuccessStatusCode();
        return join.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(ParticipantEndpoints.CookieName + "=")).Split(';')[0];
    }

    private async Task<string> HostCookieAsync()
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/host/login", new { password = HostClient.Password });
        login.EnsureSuccessStatusCode();
        return login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(HostSession.CookieName + "=")).Split(';')[0];
    }

    // Long polling: the in-memory test server has no WebSockets for the client to use.
    private async Task<HubConnection> ConnectedAsync(string? cookie)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_app.Server.BaseAddress, "/hubs/quiz"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _app.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (cookie is not null)
                {
                    options.Headers["Cookie"] = cookie;
                }
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    public void Dispose() => _app.Dispose();
}
