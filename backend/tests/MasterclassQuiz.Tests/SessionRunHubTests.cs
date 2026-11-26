using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.Participants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The Session runs by itself over SignalR: answers, reveals and the next Question follow one another
/// on the backend's timer until the Session ends. Real (short) times: 2 s per Question, 0.3 s pause.
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SessionRunHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly string[] Messages = ["QuestionStarted", "AnswerCountUpdated", "QuestionRevealed", "SessionEnded", "LeaderboardUpdated"];

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
        .WithWebHostBuilder(builder => builder
            .UseSetting("Quiz:TimeLimitSeconds:Easy", "2")
            .UseSetting("Quiz:RevealPauseSeconds", "0.3"));

    [Fact]
    public async Task A_session_runs_from_the_first_question_to_its_end_by_itself()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (masterclassId, joinCode, workshopId) = await OpenMeetingAsync(host, easy: 2);
        await using var projector = Connect(hostCookie);
        var onProjector = Listen(projector);
        await projector.StartAsync();
        await projector.InvokeAsync<JsonObject>("WatchMasterclass", masterclassId);
        await using var phone = Connect(await JoinAsync(joinCode));
        var onPhone = Listen(phone);
        await phone.StartAsync();
        await phone.InvokeAsync<JsonObject>("WatchAsParticipant");

        await projector.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 2, medium = 0, hard = 0 });
        var first = await NextAsync(onPhone, "QuestionStarted");
        await NextAsync(onProjector, "QuestionStarted");

        // The only Participant answers: the projector's count goes up and the Question is revealed early.
        var answer = await phone.InvokeAsync<JsonObject>("SubmitAnswer", (int)first["questionId"]!, 2);
        var again = await phone.InvokeAsync<JsonObject>("SubmitAnswer", (int)first["questionId"]!, 1);
        Assert.Equal("Accepted", (string?)answer["result"]);
        Assert.Equal("AlreadyAnswered", (string?)again["result"]);
        var count = await NextAsync(onProjector, "AnswerCountUpdated");
        Assert.Equal((masterclassId, (int)first["questionId"]!, 1, 1), ((int)count["masterclassId"]!, (int)count["questionId"]!, (int)count["answered"]!, (int)count["participants"]!));
        foreach (var revealed in new[] { await NextAsync(onProjector, "QuestionRevealed"), await NextAsync(onPhone, "QuestionRevealed") })
        {
            Assert.Equal(((int)first["questionId"]!, 1, 1, 0, false), ((int)revealed["questionId"]!, (int)revealed["correctOption"]!, (int)revealed["answered"]!, (int)revealed["timedOut"]!, (bool)revealed["lastQuestion"]!));
            Assert.Equal([0, 1, 0], revealed["options"]!.AsArray().Select(o => (int)o!["answers"]!));
            Assert.True((DateTime)revealed["revealedAt"]! < (DateTime)first["deadline"]!, "Revealed only at the deadline.");
        }

        // After the pause the next Question starts; nobody answers it, so it is revealed at its deadline.
        var second = await NextAsync(onPhone, "QuestionStarted");
        await NextAsync(onProjector, "QuestionStarted");
        Assert.Equal(2, (int)second["number"]!);
        var secondRevealed = await NextAsync(onPhone, "QuestionRevealed");
        await NextAsync(onProjector, "QuestionRevealed");
        Assert.Equal((0, 1, true), ((int)secondRevealed["answered"]!, (int)secondRevealed["timedOut"]!, (bool)secondRevealed["lastQuestion"]!));
        Assert.True((DateTime)secondRevealed["revealedAt"]! >= ((DateTime)second["deadline"]!).AddMilliseconds(500));

        // After the last reveal's pause, the Session ends.
        foreach (var ended in new[] { await NextAsync(onProjector, "SessionEnded"), await NextAsync(onPhone, "SessionEnded") })
        {
            Assert.Equal((masterclassId, (int)first["sessionId"]!), ((int)ended["masterclassId"]!, (int)ended["sessionId"]!));
            Assert.True((DateTime)ended["endedAt"]! >= ((DateTime)secondRevealed["revealedAt"]!).AddMilliseconds(300));
        }
    }

    [Fact]
    public async Task The_session_leaderboard_follows_each_reveal_and_the_end()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (masterclassId, joinCode, workshopId) = await OpenMeetingAsync(host, easy: 1);
        await using var projector = Connect(hostCookie);
        var onProjector = Listen(projector);
        await projector.StartAsync();
        await projector.InvokeAsync<JsonObject>("WatchMasterclass", masterclassId);
        await using var phone = Connect(await JoinAsync(joinCode));
        var onPhone = Listen(phone);
        await phone.StartAsync();
        var me = await phone.InvokeAsync<JsonObject>("WatchAsParticipant");

        await projector.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 1, medium = 0, hard = 0 });
        var question = await NextAsync(onPhone, "QuestionStarted");
        await phone.InvokeAsync<JsonObject>("SubmitAnswer", (int)question["questionId"]!, 1);

        // After the reveal, then once more (final) after the Session ends.
        foreach (var (step, ended) in new[] { ("QuestionRevealed", false), ("SessionEnded", true) })
        {
            await NextAsync(onProjector, step);
            await NextAsync(onPhone, step);
            foreach (var leaderboard in new[] { await NextAsync(onProjector, "LeaderboardUpdated"), await NextAsync(onPhone, "LeaderboardUpdated") })
            {
                Assert.Equal((masterclassId, (int)question["sessionId"]!, ended), ((int)leaderboard["masterclassId"]!, (int)leaderboard["sessionId"]!, (bool)leaderboard["ended"]!));
                var entry = Assert.Single(leaderboard["entries"]!.AsArray())!;
                Assert.Equal(((int)me["id"]!, (string?)me["nickname"], 1), ((int)entry["participantId"]!, (string?)entry["nickname"], (int)entry["rank"]!));
                Assert.InRange((int)entry["points"]!, 250, 500);
            }
        }
    }

    [Fact]
    public async Task A_browser_without_a_participant_cookie_cannot_answer()
    {
        await using var stranger = Connect(cookie: null);
        await stranger.StartAsync();

        var reply = await stranger.InvokeAsync<JsonObject>("SubmitAnswer", 1, 1);

        Assert.Equal("UnknownParticipant", (string?)reply["result"]);
    }

    // Skips other messages until the named one arrives.
    private static async Task<JsonObject> NextAsync(ChannelReader<(string Name, JsonObject Message)> messages, string name)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            var (received, message) = await messages.ReadAsync(timeout.Token);
            if (received == name)
            {
                return message;
            }
        }
    }

    private static ChannelReader<(string, JsonObject)> Listen(HubConnection connection)
    {
        var messages = Channel.CreateUnbounded<(string, JsonObject)>();
        foreach (var name in Messages)
        {
            connection.On<JsonObject>(name, message => messages.Writer.TryWrite((name, message)));
        }
        return messages.Reader;
    }

    // An Open Masterclass for a Course whose "unit-tests" Workshop has the given Easy Questions.
    private async Task<(int MasterclassId, string JoinCode, int WorkshopId)> OpenMeetingAsync(HttpClient host, int easy)
    {
        var file = SessionFiles.WithWorkshopQuestions(easy, medium: 0, hard: 0);
        var import = await host.PostAsync("/api/host/courses/import", new StringContent(file.ToJsonString(), Encoding.UTF8, "application/json"));
        import.EnsureSuccessStatusCode();
        var courseId = (int)(await import.Content.ReadFromJsonAsync<JsonObject>())!["courseId"]!;
        var courses = await host.GetFromJsonAsync<JsonArray>("/api/host/courses");
        var workshopId = (int)courses!.Single(c => (int)c!["id"]! == courseId)!["workshops"]!.AsArray()
            .Single(w => (string?)w!["slug"] == SessionFiles.WorkshopSlug)!["id"]!;

        var create = await host.PostAsJsonAsync("/api/host/masterclasses", new { courseId, name = "Dev team 2026" });
        var masterclassId = (int)(await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
        var open = await host.PostAsync($"/api/host/masterclasses/{masterclassId}/open", null);
        var joinCode = (string)(await open.Content.ReadFromJsonAsync<JsonObject>())!["joinCode"]!;
        return (masterclassId, joinCode, workshopId);
    }

    private async Task<string> JoinAsync(string joinCode)
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var join = await client.PostAsJsonAsync("/api/participants/join", new { joinCode });
        join.EnsureSuccessStatusCode();
        return join.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(ParticipantEndpoints.CookieName + "=")).Split(';')[0];
    }

    private async Task<(HttpClient Client, string Cookie)> LoggedInHostAsync()
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await client.PostAsJsonAsync("/api/host/login", new { password = HostClient.Password });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(HostSession.CookieName + "=")).Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return (client, cookie);
    }

    // Long polling: the in-memory test server has no WebSockets for the client to use.
    private HubConnection Connect(string? cookie) => new HubConnectionBuilder()
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

    public void Dispose() => _app.Dispose();
}
