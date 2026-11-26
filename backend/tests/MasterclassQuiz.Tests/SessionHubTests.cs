using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using MasterclassQuiz.Api.HostAuth;
using MasterclassQuiz.Api.Participants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace MasterclassQuiz.Tests;

/// <summary>
/// What the hub owns for a Session: Host-only starts, the participant cookie, and one end-to-end smoke test
/// that a real SignalR client in the Masterclass group hears a short Session under the wire message names.
/// The Session's behaviour itself is covered by the live Masterclass module tests (LiveMasterclass*Tests).
/// </summary>
[Collection(nameof(SqlServerCollection))]
public sealed class SessionHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly string[] Messages = ["QuestionStarted", "AnswerCountUpdated", "QuestionRevealed", "SessionEnded", "LeaderboardUpdated"];

    // An Easy time limit that isn't the default, to see that it comes from configuration; a short reveal
    // pause, the only real wait in the smoke test.
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
        .WithWebHostBuilder(builder => builder
            .UseSetting("Quiz:TimeLimitSeconds:Easy", "4")
            .UseSetting("Quiz:RevealPauseSeconds", "0.3"));

    [Fact]
    public async Task A_short_session_reaches_the_projector_and_the_phone_under_the_wire_message_names()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (masterclassId, joinCode, workshopId) = await OpenMeetingAsync(host);
        await using var projector = Connect(hostCookie);
        var onProjector = Listen(projector);
        await projector.StartAsync();
        await projector.InvokeAsync<JsonObject>("WatchMasterclass", masterclassId);
        await using var phone = Connect(await JoinAsync(joinCode));
        var onPhone = Listen(phone);
        await phone.StartAsync();
        await phone.InvokeAsync<JsonObject>("GetCurrentState", masterclassId);

        var reply = await projector.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 1, medium = 0, hard = 0 });
        Assert.Empty(reply["problems"]!.AsArray());
        var started = await NextAsync(onPhone, "QuestionStarted");
        Assert.Equal((masterclassId, "Question easy-1?"), ((int)started["masterclassId"]!, (string?)started["text"]));
        Assert.Equal(TimeSpan.FromSeconds(4), (DateTime)started["deadline"]! - (DateTime)started["startedAt"]!);
        Assert.EndsWith("Z", (string?)started["deadline"]);
        Assert.DoesNotContain("correct", started.ToJsonString(), StringComparison.OrdinalIgnoreCase);

        // The only Participant answers, so the Question is revealed at once; the Session ends after the pause.
        var answer = await phone.InvokeAsync<JsonObject>("SubmitAnswer", (int)started["questionId"]!, 1);

        Assert.Equal("Accepted", (string?)answer["result"]);
        string[] heard = ["QuestionStarted", "AnswerCountUpdated", "QuestionRevealed", "LeaderboardUpdated", "SessionEnded", "LeaderboardUpdated"];
        foreach (var (messages, expected) in new[] { (onProjector, heard), (onPhone, heard[1..]) })
        {
            var names = new List<string>();
            while (names.Count < expected.Length)
            {
                var (name, message) = await NextAsync(messages);
                names.Add(name);
                Assert.Equal(masterclassId, (int)message["masterclassId"]!);
            }
            Assert.Equal(expected, names);
        }
    }

    [Fact]
    public async Task Only_the_host_can_start_a_session()
    {
        var (host, _) = await LoggedInHostAsync();
        var (masterclassId, joinCode, workshopId) = await OpenMeetingAsync(host);
        await using var phone = Connect(await JoinAsync(joinCode));
        await phone.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            phone.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 1, medium = 0, hard = 0 }));
    }

    [Fact]
    public async Task A_browser_without_a_participant_cookie_follows_nothing()
    {
        await using var stranger = Connect(cookie: null);
        await stranger.StartAsync();

        Assert.Null(await stranger.InvokeAsync<JsonObject?>("WatchAsParticipant"));
    }

    [Fact]
    public async Task A_browser_without_a_participant_cookie_cannot_answer()
    {
        await using var stranger = Connect(cookie: null);
        await stranger.StartAsync();

        var reply = await stranger.InvokeAsync<JsonObject>("SubmitAnswer", 1, 1);

        Assert.Equal("UnknownParticipant", (string?)reply["result"]);
    }

    private static async Task<(string Name, JsonObject Message)> NextAsync(ChannelReader<(string, JsonObject)> messages) =>
        await messages.ReadAsync().AsTask().WaitAsync(Timeout);

    // Skips other messages until the named one arrives.
    private static async Task<JsonObject> NextAsync(ChannelReader<(string, JsonObject)> messages, string name)
    {
        while (true)
        {
            var (received, message) = await NextAsync(messages);
            if (received == name)
            {
                return message;
            }
        }
    }

    // Every broadcast the connection hears, by message name, in order.
    private static ChannelReader<(string, JsonObject)> Listen(HubConnection connection)
    {
        var messages = Channel.CreateUnbounded<(string, JsonObject)>();
        foreach (var name in Messages)
        {
            connection.On<JsonObject>(name, message => messages.Writer.TryWrite((name, message)));
        }
        return messages.Reader;
    }

    // An Open Masterclass for a Course whose "unit-tests" Workshop has one Easy Question.
    private async Task<(int MasterclassId, string JoinCode, int WorkshopId)> OpenMeetingAsync(HttpClient host)
    {
        var file = SessionFiles.WithWorkshopQuestions(easy: 1, medium: 0, hard: 0);
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
