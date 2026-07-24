using DevNote.Models;
using DevNote.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DevNote.Tests.Services;

public class HelperQuestionsCoordinatorTests
{
    private static HelperQuestionsResult Result(params string[] questions) =>
        new()
        {
            Questions = questions.Length > 0 ? questions : ["Q1", "Q2", "Q3"],
            ContextHash = "hash"
        };

    [Fact]
    public async Task GetForSectionAsync_IdenticalContext_ServesFromCacheAndGeneratesOnce()
    {
        var state = new WizardStateService();
        state.Data.Problem = "Some problem";
        var service = Substitute.For<IHelperQuestionsService>();
        service.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result("Cached question"));
        var sut = new HelperQuestionsCoordinator(state, service, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevNote.Services.HelperQuestionsCoordinator>.Instance);

        var first = await sut.GetForSectionAsync(WizardSectionKey.Process);
        var second = await sut.GetForSectionAsync(WizardSectionKey.Process);

        Assert.Same(first, second);
        await service.Received(1).GenerateAsync(
            Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForSectionAsync_ChangedPriorContext_GeneratesAgain()
    {
        var state = new WizardStateService();
        state.Data.Problem = "Original problem";
        var service = Substitute.For<IHelperQuestionsService>();
        service.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result());
        var sut = new HelperQuestionsCoordinator(state, service, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevNote.Services.HelperQuestionsCoordinator>.Instance);

        await sut.GetForSectionAsync(WizardSectionKey.Process);
        state.Data.Problem = "Different problem";
        await sut.GetForSectionAsync(WizardSectionKey.Process);

        await service.Received(2).GenerateAsync(
            Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForSectionAsync_ForceRefresh_BypassesCache()
    {
        var state = new WizardStateService();
        state.Data.Problem = "Some problem";
        var service = Substitute.For<IHelperQuestionsService>();
        service.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result());
        var sut = new HelperQuestionsCoordinator(state, service, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevNote.Services.HelperQuestionsCoordinator>.Instance);

        await sut.GetForSectionAsync(WizardSectionKey.Process);
        await sut.GetForSectionAsync(WizardSectionKey.Process, forceRefresh: true);

        await service.Received(2).GenerateAsync(
            Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadSectionUiStateAsync_GenerationFails_SetsErrorEmptyQuestionsAndStopsLoading()
    {
        var state = new WizardStateService();
        var service = Substitute.For<IHelperQuestionsService>();
        service.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HelperQuestionsResponseException("boom"));
        var sut = new HelperQuestionsCoordinator(state, service, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevNote.Services.HelperQuestionsCoordinator>.Instance);
        var states = sut.CreateUiStateMap();

        await sut.LoadSectionUiStateAsync(states, WizardSectionKey.Problem, forceRefresh: false);

        var sectionState = states[WizardSectionKey.Problem];
        Assert.False(sectionState.IsLoading);
        Assert.Empty(sectionState.Questions);
        Assert.False(string.IsNullOrWhiteSpace(sectionState.Error));
    }

    [Fact]
    public async Task LoadSectionUiStateAsync_GenerationSucceeds_PopulatesQuestionsWithoutError()
    {
        var state = new WizardStateService();
        var service = Substitute.For<IHelperQuestionsService>();
        service.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result("First", "Second", "Third"));
        var sut = new HelperQuestionsCoordinator(state, service, Microsoft.Extensions.Logging.Abstractions.NullLogger<DevNote.Services.HelperQuestionsCoordinator>.Instance);
        var states = sut.CreateUiStateMap();

        await sut.LoadSectionUiStateAsync(states, WizardSectionKey.Problem, forceRefresh: false);

        var sectionState = states[WizardSectionKey.Problem];
        Assert.False(sectionState.IsLoading);
        Assert.Null(sectionState.Error);
        Assert.Equal(3, sectionState.Questions.Count);
    }
}
