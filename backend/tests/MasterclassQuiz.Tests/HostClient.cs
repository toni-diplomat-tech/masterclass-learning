using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>The Host's side of HTTP tests: a logged-in client and the back-office steps before a meeting.</summary>
public static class HostClient
{
    public const string Password = "correct horse battery staple";

    /// <summary>A client holding the Host session cookie. The factory must hash <see cref="Password"/>.</summary>
    public static async Task<HttpClient> LoggedInAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/host/login", new { password = Password });
        login.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Imports the example Course under a unique slug and returns its id.</summary>
    public static async Task<int> ImportCourseAsync(HttpClient host)
    {
        var file = ImportFiles.Example(ImportFiles.UniqueSlug()).ToJsonString();
        var import = await host.PostAsync("/api/host/courses/import", new StringContent(file, Encoding.UTF8, "application/json"));
        import.EnsureSuccessStatusCode();
        return (int)(await import.Content.ReadFromJsonAsync<JsonObject>())!["courseId"]!;
    }

    /// <summary>Creates a Draft Masterclass for a new Course and returns its id.</summary>
    public static async Task<int> CreateMasterclassAsync(HttpClient host)
    {
        var courseId = await ImportCourseAsync(host);
        var create = await host.PostAsJsonAsync("/api/host/masterclasses", new { courseId, name = "Dev team 2026" });
        create.EnsureSuccessStatusCode();
        return (int)(await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!;
    }

    /// <summary>Creates and opens a Masterclass; returns its id and Join code.</summary>
    public static async Task<(int Id, string JoinCode)> OpenMasterclassAsync(HttpClient host)
    {
        var id = await CreateMasterclassAsync(host);
        var open = await host.PostAsync($"/api/host/masterclasses/{id}/open", null);
        open.EnsureSuccessStatusCode();
        return (id, (string)(await open.Content.ReadFromJsonAsync<JsonObject>())!["joinCode"]!);
    }
}
