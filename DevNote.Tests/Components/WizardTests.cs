using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DevNote.Components.Pages;
using DevNote.Models;
using DevNote.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DevNote.Tests.Components;

public class WizardTests : BunitContext
{
    private readonly IClassificationService _classification = Substitute.For<IClassificationService>();
    private readonly IHelperQuestionsService _helper = Substitute.For<IHelperQuestionsService>();

    public WizardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(new WizardStateService());
        Services.AddSingleton(_classification);
        Services.AddSingleton(_helper);
        Services.AddScoped<HelperQuestionsCoordinator>();
        Services.AddScoped<NoteService>();
        Services.AddSingleton(ComponentTestDb.Create());

        var auth = AddAuthorization();
        auth.SetAuthorized("user-1");
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-1"));
    }

    [Fact]
    public void EnteringContent_EnablesClassification()
    {
        _helper.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HelperQuestionsResult { Questions = ["Q1"], ContextHash = "h" });
        var cut = Render<Wizard>();

        Assert.True(cut.Find("button.btn-classify").HasAttribute("disabled"));

        cut.Find("summary.wizard-section-header").Click();
        cut.Find("textarea.wizard-section-textarea").Change("Realny problem biznesowy");

        Assert.False(cut.Find("button.btn-classify").HasAttribute("disabled"));
    }

    [Fact]
    public void HelperGenerationFailure_LeavesClassificationAndTextEntryUsable()
    {
        _helper.GenerateAsync(Arg.Any<HelperQuestionsRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HelperQuestionsResponseException("boom"));
        var cut = Render<Wizard>();

        cut.Find("summary.wizard-section-header").Click();

        Assert.Single(cut.FindAll(".wizard-helper-error"));

        cut.Find("textarea.wizard-section-textarea").Change("Wpisany tekst mimo błędu");

        var state = Services.GetRequiredService<WizardStateService>();
        Assert.Equal("Wpisany tekst mimo błędu", state.Data.Problem);
        Assert.False(cut.Find("button.btn-classify").HasAttribute("disabled"));
        Assert.Single(cut.FindAll(".wizard-helper-error"));
    }
}
