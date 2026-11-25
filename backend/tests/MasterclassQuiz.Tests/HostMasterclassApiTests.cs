using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class HostMasterclassApiTests(SqlServerFixture sqlServer) : IDisposable
{
    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(HostClient.Password));

    [Fact]
    public async Task The_host_creates_a_draft_masterclass_and_sees_it_listed()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var courseId = await HostClient.ImportCourseAsync(client);

        var create = await client.PostAsJsonAsync("/api/host/masterclasses", new { courseId, name = "Dev team 2026" });
        var list = await client.GetFromJsonAsync<JsonArray>("/api/host/masterclasses");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(("Dev team 2026", "Draft", null), ((string?)created["name"], (string?)created["status"], (string?)created["joinCode"]));
        Assert.Equal($"/api/host/masterclasses/{(int)created["id"]!}", create.Headers.Location?.OriginalString);
        var listed = Assert.Single(list!, m => (int)m!["id"]! == (int)created["id"]!)!;
        Assert.Equal(("Testing Fundamentals", 0), ((string?)listed["courseTitle"], (int)listed["participants"]!));
    }

    [Fact]
    public async Task A_masterclass_without_a_name_is_answered_with_its_problems()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var courseId = await HostClient.ImportCourseAsync(client);

        var create = await client.PostAsJsonAsync("/api/host/masterclasses", new { courseId, name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        var problem = await create.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(["The Masterclass needs a name."], problem!["problems"]!.AsArray().Select(p => (string?)p));
    }

    [Fact]
    public async Task The_host_opens_a_masterclass_and_gets_its_join_code()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var id = await HostClient.CreateMasterclassAsync(client);

        var open = await client.PostAsync($"/api/host/masterclasses/{id}/open", null);
        var masterclass = await client.GetFromJsonAsync<JsonObject>($"/api/host/masterclasses/{id}");

        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        Assert.Equal("Open", (string?)masterclass!["status"]);
        Assert.Matches("^[0-9]{6}$", (string?)masterclass["joinCode"]);
    }

    [Fact]
    public async Task The_host_closes_a_masterclass_and_its_join_code_stops_working()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var (id, joinCode) = await HostClient.OpenMasterclassAsync(client);

        var close = await client.PostAsync($"/api/host/masterclasses/{id}/close", null);
        var join = await _app.CreateClient().PostAsJsonAsync("/api/participants/join", new { joinCode });

        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        Assert.Equal("Closed", (string?)(await close.Content.ReadFromJsonAsync<JsonObject>())!["status"]);
        Assert.Equal(HttpStatusCode.NotFound, join.StatusCode);
    }

    [Fact]
    public async Task Closing_a_draft_is_answered_with_its_problems()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var id = await HostClient.CreateMasterclassAsync(client);

        var close = await client.PostAsync($"/api/host/masterclasses/{id}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        var problem = await close.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(["Only an Open Masterclass can be closed."], problem!["problems"]!.AsArray().Select(p => (string?)p));
    }

    [Fact]
    public async Task The_host_gets_the_masterclass_leaderboard()
    {
        var client = await HostClient.LoggedInAsync(_app);
        var (id, _) = await HostClient.OpenMasterclassAsync(client);

        var leaderboard = await client.GetFromJsonAsync<JsonObject>($"/api/host/masterclasses/{id}/leaderboard");

        Assert.Equal(id, (int)leaderboard!["masterclassId"]!);
        Assert.Empty(leaderboard["entries"]!.AsArray());
    }

    [Fact]
    public async Task An_unknown_masterclass_is_not_found()
    {
        var client = await HostClient.LoggedInAsync(_app);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/host/masterclasses/-1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/host/masterclasses/-1/open", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/host/masterclasses/-1/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/host/masterclasses/-1/leaderboard")).StatusCode);
    }

    [Fact]
    public async Task Masterclass_endpoints_need_the_host_session()
    {
        var client = _app.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/host/masterclasses")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/host/masterclasses/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/host/masterclasses", new { courseId = 1, name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/host/masterclasses/1/open", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/host/masterclasses/1/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/host/masterclasses/1/leaderboard")).StatusCode);
    }

    public void Dispose() => _app.Dispose();
}
