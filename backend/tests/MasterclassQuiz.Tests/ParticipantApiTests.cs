using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class ParticipantApiTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task Joining_sets_an_http_only_cookie_and_answers_with_the_nickname()
    {
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(await HostClient.LoggedInAsync(_app));
        var phone = _app.CreateClient();

        var join = await phone.PostAsJsonAsync("/api/participants/join", new { joinCode });

        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var joined = (await join.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((id, "Dev team 2026", "Open"), ((int)joined["masterclassId"]!, (string?)joined["masterclassName"], (string?)joined["masterclassStatus"]));
        Assert.Matches("^[A-Z][a-z]+ [A-Z][a-z]+$", (string?)joined["nickname"]);
        Assert.Null(joined["token"]);
        var cookie = Assert.Single(join.Headers.GetValues("Set-Cookie"), c => c.StartsWith("mq_participant="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_cookie_brings_back_the_same_participant()
    {
        var (_, joinCode) = await HostClient.OpenMasterclassAsync(await HostClient.LoggedInAsync(_app));
        var phone = _app.CreateClient();
        var joined = await (await phone.PostAsJsonAsync("/api/participants/join", new { joinCode })).Content.ReadFromJsonAsync<JsonObject>();

        var me = await phone.GetFromJsonAsync<JsonObject>("/api/participants/me");
        var joinedAgain = await (await phone.PostAsJsonAsync("/api/participants/join", new { joinCode })).Content.ReadFromJsonAsync<JsonObject>();

        Assert.Equal(((int)joined!["id"]!, (string?)joined["nickname"]), ((int)me!["id"]!, (string?)me["nickname"]));
        Assert.Equal(((int)joined["id"]!, (string?)joined["nickname"]), ((int)joinedAgain!["id"]!, (string?)joinedAgain["nickname"]));
    }

    [Fact]
    public async Task Without_a_known_cookie_there_is_no_participant()
    {
        var phone = _app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/participants/me")).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/participants/me");
        request.Headers.Add("Cookie", "mq_participant=made-up");
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task A_join_code_of_no_open_masterclass_is_refused()
    {
        var phone = _app.CreateClient();

        var join = await phone.PostAsJsonAsync("/api/participants/join", new { joinCode = "abc" });

        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
        var problem = await join.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("No open Masterclass has this Join code.", (string?)problem!["title"]);
        Assert.False(join.Headers.Contains("Set-Cookie"));
    }

    public void Dispose() => _app.Dispose();
}
