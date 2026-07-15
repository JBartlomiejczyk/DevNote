using Bunit;
using DevNote.Components.Shared;
using Xunit;

namespace DevNote.Tests.Components;

public class WizardSectionTests : BunitContext
{
    [Fact]
    public void CollapseAndReexpand_RestoresEnteredValue()
    {
        var value = string.Empty;
        var expanded = true;
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.Description, "Opisz problem")
            .Bind(p => p.Value, value, v => value = v)
            .Bind(p => p.IsExpanded, expanded, v => expanded = v));

        cut.Find("textarea.wizard-section-textarea").Change("Zapisana odpowiedź");
        Assert.Equal("Zapisana odpowiedź", value);

        cut.Find("button.wizard-section-header").Click();
        Assert.False(expanded);
        Assert.Empty(cut.FindAll("textarea.wizard-section-textarea"));

        cut.Find("button.wizard-section-header").Click();
        Assert.True(expanded);
        Assert.Equal("Zapisana odpowiedź", cut.Find("textarea.wizard-section-textarea").GetAttribute("value"));
    }

    [Fact]
    public void FirstExpanded_FiresOnceAcrossRepeatedToggles()
    {
        var firstExpandedCount = 0;
        var expanded = false;
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Bind(p => p.IsExpanded, expanded, v => expanded = v)
            .Add(p => p.FirstExpanded, () => firstExpandedCount++));

        cut.Find("button.wizard-section-header").Click();
        cut.Find("button.wizard-section-header").Click();
        cut.Find("button.wizard-section-header").Click();

        Assert.Equal(1, firstExpandedCount);
    }

    [Fact]
    public void RefreshButton_DisabledWhileHelperLoading()
    {
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.IsHelperQuestionsLoading, true));

        Assert.True(cut.Find("button.btn-helper-refresh").HasAttribute("disabled"));
    }

    [Fact]
    public void HelperState_LoadingIsExclusive()
    {
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.IsHelperQuestionsLoading, true)
            .Add(p => p.HelperQuestionsError, "błąd")
            .Add(p => p.HelperQuestions, new[] { "Pytanie 1" }));

        Assert.Single(cut.FindAll(".wizard-helper-loading"));
        Assert.Empty(cut.FindAll(".wizard-helper-error"));
        Assert.Empty(cut.FindAll(".wizard-helper-list"));
        Assert.Empty(cut.FindAll(".wizard-helper-empty"));
    }

    [Fact]
    public void HelperState_ErrorTakesPrecedenceOverQuestions()
    {
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.IsHelperQuestionsLoading, false)
            .Add(p => p.HelperQuestionsError, "Nie udało się wygenerować pytań")
            .Add(p => p.HelperQuestions, new[] { "Pytanie 1" }));

        Assert.Single(cut.FindAll(".wizard-helper-error"));
        Assert.Empty(cut.FindAll(".wizard-helper-loading"));
        Assert.Empty(cut.FindAll(".wizard-helper-list"));
        Assert.Empty(cut.FindAll(".wizard-helper-empty"));
    }

    [Fact]
    public void HelperState_QuestionsRenderWhenPresentAndNoError()
    {
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.IsExpanded, true)
            .Add(p => p.HelperQuestions, new[] { "Pytanie 1", "Pytanie 2" }));

        Assert.Single(cut.FindAll(".wizard-helper-list"));
        Assert.Equal(2, cut.FindAll(".wizard-helper-list li").Count);
        Assert.Empty(cut.FindAll(".wizard-helper-error"));
        Assert.Empty(cut.FindAll(".wizard-helper-loading"));
        Assert.Empty(cut.FindAll(".wizard-helper-empty"));
    }

    [Fact]
    public void HelperState_EmptyWhenNoQuestionsErrorOrLoading()
    {
        var cut = Render<WizardSection>(ps => ps
            .Add(p => p.Title, "Problem")
            .Add(p => p.IsExpanded, true));

        Assert.Single(cut.FindAll(".wizard-helper-empty"));
        Assert.Empty(cut.FindAll(".wizard-helper-list"));
        Assert.Empty(cut.FindAll(".wizard-helper-error"));
        Assert.Empty(cut.FindAll(".wizard-helper-loading"));
    }
}
