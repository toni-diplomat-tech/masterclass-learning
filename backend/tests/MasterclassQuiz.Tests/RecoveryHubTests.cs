using System.Net.Http.Json;
using System.Text.Json.Nodes;
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
/// What the hub owns for recovery: GetCurrentState answers the Host and the Masterclass's Participants
/// (known by the participant cookie) and nobody else, and only the Host can resume. What resuming does is
/// covered by the live Masterclass module tests (LiveMasterclassResumeTests).
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RecoveryHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
        .WithWebHostBuilder(builder => builder
            .UseSetting("Quiz:TimeLimitSeconds:Easy", "30"));

    private QuizEngine Engine => _app.Services.GetRequiredService<QuizEngine>();

    [Fact]
    public async Task A_reloaded_phone_gets_the_running_question_and_its_locked_answer_but_not_the_correct_option()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        var cookie = $"{ParticipantEndpoints.CookieName}={session.Tokens[0]}";
        await using (var before = await ConnectedAsync(cookie))
        {
            await before.InvokeAsync<JsonObject>("GetCurrentState", session.MasterclassId);
            Assert.Equal("Accepted", (string?)(await before.InvokeAsync<JsonObject>("SubmitAnswer", session.Question.QuestionId, 2))["result"]);
        }

        await using var reloaded = await ConnectedAsync(cookie);
        var state = await reloaded.InvokeAsync<JsonObject>("GetCurrentState", session.MasterclassId);

        Assert.Equal(("Question", session.Question.QuestionId), ((string?)state["step"], (int)state["question"]!["questionId"]!));
        Assert.Equal(session.Question.Deadline, (DateTime)state["question"]!["deadline"]!);
        var answer = state["participant"]!["answer"]!;
        Assert.Equal((true, 2, null), ((bool)answer["canAnswer"]!, (int?)answer["option"], (string?)answer["outcome"]));
        Assert.DoesNotContain("correct", state.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_reloaded_projector_gets_the_running_question_with_its_answer_count()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 2, easy: 1);
        await Engine.SubmitAnswerAsync(session.Tokens[0], session.Question.QuestionId, 1);
        await using var projector = await ConnectedAsync(await HostCookieAsync());

        var state = await projector.InvokeAsync<JsonObject>("GetCurrentState", session.MasterclassId);

        Assert.Equal(("Question", session.Question.QuestionId), ((string?)state["step"], (int)state["question"]!["questionId"]!));
        Assert.Equal((1, 2), ((int)state["count"]!["answered"]!, (int)state["count"]!["participants"]!));
        Assert.Null(state["participant"]);
    }

    [Fact]
    public async Task A_stranger_gets_no_state_and_cannot_resume()
    {
        var session = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        var other = await RunningSessionFixture.StartAsync(Engine, participants: 1, easy: 1);
        await using var stranger = await ConnectedAsync(cookie: null);
        await using var otherPhone = await ConnectedAsync($"{ParticipantEndpoints.CookieName}={other.Tokens[0]}");
        await using var phone = await ConnectedAsync($"{ParticipantEndpoints.CookieName}={session.Tokens[0]}");

        Assert.Null(await stranger.InvokeAsync<JsonObject?>("GetCurrentState", session.MasterclassId));
        Assert.Null(await otherPhone.InvokeAsync<JsonObject?>("GetCurrentState", session.MasterclassId));
        await Assert.ThrowsAsync<HubException>(() => phone.InvokeAsync<JsonObject>("ResumeSession", session.MasterclassId));
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
