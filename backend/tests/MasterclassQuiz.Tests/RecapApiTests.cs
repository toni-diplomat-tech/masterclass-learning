using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>A Participant finds their Recap link with their cookie; anyone with the link reads it without logging in.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class RecapApiTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task The_participant_gets_their_recap_link_and_anyone_can_read_it_without_logging_in()
    {
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(await HostClient.LoggedInAsync(_app), sqlServer.Connection);
        var phone = _app.CreateClient();
        var joined = await (await phone.PostAsJsonAsync("/api/participants/join", new { joinCode })).Content.ReadFromJsonAsync<JsonObject>();

        var link = await phone.GetFromJsonAsync<JsonObject>("/api/participants/me/recap");
        var recapToken = (string?)link!["recapToken"];
        var recap = await _app.CreateClient().GetFromJsonAsync<JsonObject>($"/api/recaps/{recapToken}");

        Assert.Matches("^[0-9a-f]{64}$", recapToken);
        Assert.Equal(
            ((string?)joined!["nickname"], id, "Dev team 2026", "Open"),
            ((string?)recap!["nickname"], (int)recap["masterclassId"]!, (string?)recap["masterclassName"], (string?)recap["masterclassStatus"]));
        Assert.Empty(recap["workshops"]!.AsArray());
    }

    [Fact]
    public async Task Unknown_tokens_are_not_found()
    {
        var client = _app.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recaps/{new string('0', 64)}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/participants/me/recap")).StatusCode);
    }

    public void Dispose() => _app.Dispose();
}
