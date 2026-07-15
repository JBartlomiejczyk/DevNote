using DevNote.Data;
using Microsoft.EntityFrameworkCore;

namespace DevNote.Tests.Components;

internal static class ComponentTestDb
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ComponentTestDb_{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
