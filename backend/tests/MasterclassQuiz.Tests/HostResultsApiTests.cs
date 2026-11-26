using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>Only the Host reads a Masterclass's results, including each Participant's Recap token.</summary>
[Collection(nameof(SqlServerCollection))]
public sealed class HostResultsApiTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task The_host_reads_the_results_with_each_participants_recap_token()
    {
        var host = await HostClient.LoggedInAsync(_app);
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(host);
        var phone = _app.CreateClient();
        var joined = await (await phone.PostAsJsonAsync("/api/participants/join", new { joinCode })).Content.ReadFromJsonAsync<JsonObject>();
        var recapToken = (string?)(await phone.GetFromJsonAsync<JsonObject>("/api/participants/me/recap"))!["recapToken"];

        var results = await host.GetFromJsonAsync<JsonObject>($"/api/host/masterclasses/{id}/results");

        Assert.Equal((id, "Open"), ((int)results!["masterclassId"]!, (string?)results["masterclassStatus"]));
        Assert.Empty(results["sessions"]!.AsArray());
        var participant = Assert.Single(results["participants"]!.AsArray())!;
        Assert.Equal(
            ((string?)joined!["nickname"], 0, recapToken),
            ((string?)participant["nickname"], (int)participant["points"]!, (string?)participant["recapToken"]));
    }

    [Fact]
    public async Task Results_need_the_host_session_and_an_existing_masterclass()
    {
        var host = await HostClient.LoggedInAsync(_app);
        var id = await HostClient.CreateMasterclassAsync(host);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _app.CreateClient().GetAsync($"/api/host/masterclasses/{id}/results")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync("/api/host/masterclasses/-1/results")).StatusCode);
    }

    public void Dispose() => _app.Dispose();
}
