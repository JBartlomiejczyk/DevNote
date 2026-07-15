using DevNote.Models;
using DevNote.Services;
using Xunit;

namespace DevNote.Tests.Services;

public class WizardStateServiceTests
{
    [Fact]
    public void Reset_ClearsWizardDataAndHelperQuestionsCache()
    {
        var sut = new WizardStateService();
        sut.Data.Problem = "Problem";
        sut.Data.Process = "Process";
        sut.Data.TimeWaste = "Time waste";
        sut.Data.InputData = "Input data";
        sut.Data.Output = "Output";
        sut.Data.Risks = "Risks";
        sut.Data.Users = "Users";
        sut.Data.Scale = "Scale";
        sut.SetCachedHelperQuestions("Problem:hash", CreateHelperQuestionsResult());

        sut.Reset();

        Assert.False(sut.Data.HasAnyContent());
        Assert.False(sut.TryGetCachedHelperQuestions("Problem:hash", out _));
    }

    [Fact]
    public void LoadFromNote_MapsAllWizardFieldsAndClearsHelperQuestionsCache()
    {
        var sut = new WizardStateService();
        sut.SetCachedHelperQuestions("Problem:hash", CreateHelperQuestionsResult());
        var note = new ConversationNote
        {
            Problem = "Problem",
            Process = "Process",
            TimeWaste = "Time waste",
            InputData = "Input data",
            Output = "Output",
            Risks = "Risks",
            Users = "Users",
            Scale = "Scale"
        };

        sut.LoadFromNote(note);

        Assert.Equal("Problem", sut.Data.Problem);
        Assert.Equal("Process", sut.Data.Process);
        Assert.Equal("Time waste", sut.Data.TimeWaste);
        Assert.Equal("Input data", sut.Data.InputData);
        Assert.Equal("Output", sut.Data.Output);
        Assert.Equal("Risks", sut.Data.Risks);
        Assert.Equal("Users", sut.Data.Users);
        Assert.Equal("Scale", sut.Data.Scale);
        Assert.False(sut.TryGetCachedHelperQuestions("Problem:hash", out _));
    }

    private static HelperQuestionsResult CreateHelperQuestionsResult() =>
        new()
        {
            Questions = ["Question 1", "Question 2", "Question 3"],
            ContextHash = "hash"
        };
}
