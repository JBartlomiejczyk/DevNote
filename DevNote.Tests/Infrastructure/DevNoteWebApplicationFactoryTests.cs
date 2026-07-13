using System.Net;
using Xunit;

namespace DevNote.Tests.Infrastructure;

public class DevNoteWebApplicationFactoryTests : IClassFixture<DevNoteWebApplicationFactory>
{
    private readonly DevNoteWebApplicationFactory _factory;

    public DevNoteWebApplicationFactoryTests(DevNoteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Factory_Starts_And_HealthzReturns200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
