using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterclassQuiz.Tests;

/// <summary>
/// The whole backend in memory, on the test SQL Server, with the Host password hash given as
/// its <c>HOST_PASSWORD_HASH</c> setting (null: not set).
/// </summary>
public sealed class BackendFactory(SqlServerFixture sqlServer, string? passwordHash) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Quiz", sqlServer.Connection.ConnectionString);
        builder.UseSetting("HOST_PASSWORD_HASH", passwordHash ?? "");
    }
}
