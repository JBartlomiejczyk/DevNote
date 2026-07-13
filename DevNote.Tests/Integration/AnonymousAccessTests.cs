using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using DevNote.Tests.Infrastructure;

namespace DevNote.Tests.Integration;

public class AnonymousAccessTests : IClassFixture<DevNoteWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnonymousAccessTests(DevNoteWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task GetRootPage_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetNotesPage_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/notes");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetEditNotePage_Anonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/edit/00000000-0000-0000-0000-000000000001");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetHealthz_Anonymous_Returns200()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminDbCheck_AfterRemoval_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/admin/db-check");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.ToString());
    }
}
