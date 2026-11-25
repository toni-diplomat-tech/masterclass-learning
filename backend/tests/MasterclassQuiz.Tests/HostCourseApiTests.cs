using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class HostCourseApiTests(SqlServerFixture sqlServer) : IDisposable
{
    private const string Password = "correct horse battery staple";

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(Password));

    [Fact]
    public async Task The_host_uploads_an_import_file_and_sees_the_course_listed()
    {
        var client = await LoggedInClientAsync();
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());

        var import = await client.PostAsync("/api/host/courses/import", Upload(file.ToJsonString()));
        var courses = await client.GetFromJsonAsync<JsonArray>("/api/host/courses");

        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var summary = await import.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(4, (int)summary!["questionsAdded"]!);
        var course = Assert.Single(courses!, c => (string?)c!["slug"] == (string)file["course"]!["slug"]!)!;
        var unitTests = course["workshops"]![0]!;
        Assert.Equal("Unit tests", (string?)unitTests["title"]);
        Assert.Equal((1, 1, 1), ((int)unitTests["questions"]!["easy"]!, (int)unitTests["questions"]!["medium"]!, (int)unitTests["questions"]!["hard"]!));
    }

    [Fact]
    public async Task An_invalid_import_file_is_answered_with_its_problems()
    {
        var client = await LoggedInClientAsync();
        var file = ImportFiles.Example(ImportFiles.UniqueSlug());
        file.Question("unit-tests-001")["lesson"] = "nowhere";

        var import = await client.PostAsync("/api/host/courses/import", Upload(file.ToJsonString()));

        Assert.Equal(HttpStatusCode.BadRequest, import.StatusCode);
        var problem = await import.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("The import file was rejected.", (string?)problem!["title"]);
        Assert.Equal(
            ["Question 'unit-tests-001' names Lesson 'nowhere', which is not in the file."],
            problem["problems"]!.AsArray().Select(p => (string?)p));
    }

    [Fact]
    public async Task Course_endpoints_need_the_host_session()
    {
        var client = _app.CreateClient();

        var import = await client.PostAsync("/api/host/courses/import", Upload(ImportFiles.Example().ToJsonString()));
        var courses = await client.GetAsync("/api/host/courses");

        Assert.Equal(HttpStatusCode.Unauthorized, import.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, courses.StatusCode);
    }

    private async Task<HttpClient> LoggedInClientAsync()
    {
        var client = _app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/host/login", new { password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static StringContent Upload(string contents) => new(contents, Encoding.UTF8, "application/json");

    public void Dispose() => _app.Dispose();
}
