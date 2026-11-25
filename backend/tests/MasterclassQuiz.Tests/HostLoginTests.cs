using System.Net;
using System.Net.Http.Json;
using MasterclassQuiz.Api.HostAuth;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

[Collection(nameof(SqlServerCollection))]
public sealed class HostLoginTests(SqlServerFixture sqlServer) : IDisposable
{
    private const string Password = "correct horse battery staple";

    private readonly WebApplicationFactory<Program> _app = new BackendFactory(sqlServer, HostPassword.Hash(Password));

    [Fact]
    public async Task Correct_password_creates_an_http_only_cookie_session()
    {
        var client = _app.CreateClient();

        var login = await client.PostAsJsonAsync("/api/host/login", new { password = Password });
        var session = await client.GetAsync("/api/host/session");

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith($"{HostSession.CookieName}=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Theory]
    [InlineData("wrong password")]
    [InlineData("")]
    public async Task Wrong_password_is_refused_without_a_session(string password)
    {
        var client = _app.CreateClient();

        var login = await client.PostAsJsonAsync("/api/host/login", new { password });
        var session = await client.GetAsync("/api/host/session");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.False(login.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task Host_endpoints_answer_401_without_a_session_instead_of_redirecting()
    {
        var client = _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var session = await client.GetAsync("/api/host/session");

        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task Logging_out_ends_the_session()
    {
        var client = _app.CreateClient();
        await client.PostAsJsonAsync("/api/host/login", new { password = Password });

        var logout = await client.PostAsync("/api/host/logout", null);
        var session = await client.GetAsync("/api/host/session");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
    }

    [Fact]
    public async Task Without_a_configured_password_hash_every_login_is_refused()
    {
        using var app = new BackendFactory(sqlServer, passwordHash: null);
        var client = app.CreateClient();

        var login = await client.PostAsJsonAsync("/api/host/login", new { password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    public void Dispose() => _app.Dispose();
}
