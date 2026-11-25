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

[Collection(nameof(SqlServerCollection))]
public sealed class SessionHubTests(SqlServerFixture sqlServer) : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // An Easy time limit that isn't the default, to see that it comes from configuration.
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password))
        .WithWebHostBuilder(builder => builder.UseSetting("Quiz:TimeLimitSeconds:Easy", "4"));

    [Fact]
    public async Task Starting_a_session_shows_its_first_question_on_the_projector_and_the_phones()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (masterclassId, joinCode, workshopId) = await OpenMeetingAsync(host);
        var phoneCookie = await JoinAsync(joinCode);

        await using var projector = Connect(hostCookie);
        var onProjector = Listen(projector);
        await projector.StartAsync();
        await projector.InvokeAsync<JsonObject>("WatchMasterclass", masterclassId);
        await using var phone = Connect(phoneCookie);
        var onPhone = Listen(phone);
        await phone.StartAsync();
        var participant = await phone.InvokeAsync<JsonObject>("WatchAsParticipant");

        var reply = await projector.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 1, medium = 0, hard = 0 });

        Assert.Equal(masterclassId, (int)participant["masterclassId"]!);
        Assert.Empty(reply["problems"]!.AsArray());
        foreach (var started in new[] { await onProjector.ReadAsync().AsTask().WaitAsync(Timeout), await onPhone.ReadAsync().AsTask().WaitAsync(Timeout) })
        {
            Assert.Equal((masterclassId, 1, 1, "Easy"), ((int)started["masterclassId"]!, (int)started["number"]!, (int)started["questionCount"]!, (string?)started["difficulty"]));
            Assert.Equal("Question easy-1?", (string?)started["text"]);
            Assert.Equal(3, started["options"]!.AsArray().Count);
            var startedAt = (DateTime)started["startedAt"]!;
            var deadline = (DateTime)started["deadline"]!;
            Assert.Equal(TimeSpan.FromSeconds(4), deadline - startedAt);
            Assert.EndsWith("Z", (string?)started["deadline"]);
            Assert.DoesNotContain("correct", started.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_refused_start_tells_the_host_why_and_shows_nothing()
    {
        var (host, hostCookie) = await LoggedInHostAsync();
        var (masterclassId, _, workshopId) = await OpenMeetingAsync(host);
        await using var projector = Connect(hostCookie);
        var onProjector = Listen(projector);
        await projector.StartAsync();
        await projector.InvokeAsync<JsonObject>("WatchMasterclass", masterclassId);

        var reply = await projector.InvokeAsync<JsonObject>("StartSession", masterclassId, workshopId, new { easy = 2, medium = 0, hard = 0 });

        Assert.Equal(["The Difficulty mix asks for 2 Questions but the Workshop has only 1."], reply["problems"]!.AsArray().Select(p => (string)p!));
        Assert.False(onProjector.TryRead(out _));
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

    private static ChannelReader<JsonObject> Listen(HubConnection connection)
    {
        var messages = Channel.CreateUnbounded<JsonObject>();
        connection.On<JsonObject>("QuestionStarted", message => messages.Writer.TryWrite(message));
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
