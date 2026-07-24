using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DevNote.Components.Pages;
using DevNote.Data;
using DevNote.Models;
using DevNote.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DevNote.Tests.Components;

public class EditNoteTests : BunitContext
{
    private const string OwnerId = "user-1";
    private readonly IClassificationService _classification = Substitute.For<IClassificationService>();
    private readonly IHelperQuestionsService _helper = Substitute.For<IHelperQuestionsService>();
    private readonly ApplicationDbContext _db = ComponentTestDb.Create();

    public EditNoteTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(new WizardStateService());
        Services.AddSingleton(_classification);
        Services.AddSingleton(_helper);
        Services.AddScoped<HelperQuestionsCoordinator>();
        Services.AddScoped<NoteService>();
        Services.AddSingleton(_db);

        var auth = AddAuthorization();
        auth.SetAuthorized(OwnerId);
        auth.SetClaims(new Claim(ClaimTypes.NameIdentifier, OwnerId));
    }

    private ConversationNote SeedCompletedNote()
    {
        var note = new ConversationNote
        {
            Id = Guid.NewGuid(),
            UserId = OwnerId,
            Title = "Stara notatka",
            Status = NoteStatus.Completed,
            Classification = Classification.C,
            Justification = "Stare uzasadnienie",
            Problem = "Stary problem",
            Process = "Stary proces",
            TimeWaste = "Stara strata czasu",
            InputData = "Stare dane wejściowe",
            Output = "Stary wynik",
            Risks = "Stare ryzyka",
            Users = "Starzy użytkownicy",
            Scale = "Stara skala",
            SummaryProblem = "Stare podsumowanie problemu",
            SummaryUsers = "Stare podsumowanie użytkowników",
            CurrentProcess = "Stary obecny proces",
            SummaryTimeWaste = "Stara strata",
            SummaryInputData = "Stare dane",
            ExpectedOutput = "Stary oczekiwany wynik",
            RecommendedPath = "Stara ścieżka",
            MvpScope = "Stary MVP",
            OutOfScope = "Stary poza zakresem",
            AcceptanceCriteria = "Stare kryteria",
            NextStep = "Stary następny krok",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.ConversationNotes.Add(note);
        _db.SaveChanges();
        return note;
    }

    private static ClassificationResult FreshResult() => new()
    {
        Classification = Classification.B,
        Justification = "Nowe uzasadnienie",
        Problem = "Nowe podsumowanie problemu",
        Users = "Nowi użytkownicy",
        CurrentProcess = "Nowy proces",
        TimeWaste = "Nowa strata",
        InputData = "Nowe dane",
        ExpectedOutput = "Nowy wynik",
        RecommendedPath = "Nowa ścieżka",
        MvpScope = "Nowy MVP",
        OutOfScope = "Nowy poza zakresem",
        AcceptanceCriteria = "Nowe kryteria",
        NextStep = "Nowy następny krok"
    };

    [Fact]
    public void Load_CompletedNote_PrefillsFieldsAndShowsSavedClassification()
    {
        var note = SeedCompletedNote();

        var cut = Render<EditNote>(ps => ps.Add(p => p.NoteId, note.Id));

        var data = Services.GetRequiredService<WizardStateService>().Data;
        Assert.Equal("Stary problem", data.Problem);
        Assert.Equal("Stary proces", data.Process);
        Assert.Equal("Stara strata czasu", data.TimeWaste);
        Assert.Equal("Stare dane wejściowe", data.InputData);
        Assert.Equal("Stary wynik", data.Output);
        Assert.Equal("Stare ryzyka", data.Risks);
        Assert.Equal("Starzy użytkownicy", data.Users);
        Assert.Equal("Stara skala", data.Scale);

        var persisted = _db.ConversationNotes.Single(n => n.Id == note.Id);
        Assert.Equal(NoteStatus.Completed, persisted.Status);
        Assert.Equal(Classification.C, persisted.Classification);
        Assert.Equal("Stare podsumowanie problemu", persisted.SummaryProblem);

        Assert.Single(cut.FindAll(".result-panel"));
        Assert.DoesNotContain("Notatka zaktualizowana", cut.Markup);
    }

    [Fact]
    public void Classify_Success_UpdatesOriginalNoteAndRendersResult()
    {
        var note = SeedCompletedNote();
        _classification.ClassifyAsync(Arg.Any<WizardData>(), Arg.Any<CancellationToken>())
            .Returns(FreshResult());
        var cut = Render<EditNote>(ps => ps.Add(p => p.NoteId, note.Id));

        cut.Find("button.btn-classify").Click();

        Assert.Single(cut.FindAll(".result-panel"));
        Assert.Contains("Notatka zaktualizowana", cut.Markup);

        var notes = _db.ConversationNotes.ToList();
        Assert.Single(notes);
        var persisted = notes[0];
        Assert.Equal(note.Id, persisted.Id);
        Assert.Equal(NoteStatus.Completed, persisted.Status);
        Assert.Equal(Classification.B, persisted.Classification);
        Assert.Equal("Nowe podsumowanie problemu", persisted.SummaryProblem);
        Assert.Equal("Nowy następny krok", persisted.NextStep);
    }

    [Fact]
    public void Classify_Failure_RendersRetryAndLeavesNoteAsDraft()
    {
        var note = SeedCompletedNote();
        _classification.ClassifyAsync(Arg.Any<WizardData>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("azure down"));
        var cut = Render<EditNote>(ps => ps.Add(p => p.NoteId, note.Id));

        cut.Find("button.btn-classify").Click();

        Assert.Single(cut.FindAll(".error-banner"));
        Assert.Single(cut.FindAll("button.btn-retry"));
        Assert.Empty(cut.FindAll(".result-panel"));

        var persisted = _db.ConversationNotes.Single(n => n.Id == note.Id);
        Assert.Equal(NoteStatus.Draft, persisted.Status);
        Assert.Null(persisted.Classification);
        Assert.Equal(string.Empty, persisted.SummaryProblem);
    }

    [Fact]
    public void Load_DraftNote_ShowsNoClassificationAndClassifyButton()
    {
        var note = new ConversationNote
        {
            Id = Guid.NewGuid(),
            UserId = OwnerId,
            Title = "Szkic",
            Status = NoteStatus.Draft,
            Classification = null,
            Problem = "Problem szkicu",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.ConversationNotes.Add(note);
        _db.SaveChanges();

        var cut = Render<EditNote>(ps => ps.Add(p => p.NoteId, note.Id));

        Assert.Empty(cut.FindAll(".result-panel"));
        Assert.Contains("Klasyfikuj", cut.Find("button.btn-classify").TextContent);
        Assert.DoesNotContain("Ponów klasyfikację", cut.Markup);

        var persisted = _db.ConversationNotes.Single(n => n.Id == note.Id);
        Assert.Equal(NoteStatus.Draft, persisted.Status);
        Assert.Null(persisted.Classification);
    }

    [Fact]
    public void WizardEdit_AfterLoadCompletedNote_HidesResultAndRevertsToDbDraft()
    {
        var note = SeedCompletedNote();
        var cut = Render<EditNote>(ps => ps.Add(p => p.NoteId, note.Id));

        Assert.Single(cut.FindAll(".result-panel"));

        cut.Find("summary.wizard-section-header").Click();
        cut.Find("textarea.wizard-section-textarea").Change("Zmieniona treść");

        Assert.Empty(cut.FindAll(".result-panel"));
        Assert.Contains("Klasyfikuj", cut.Find("button.btn-classify").TextContent);

        var persisted = _db.ConversationNotes.Single(n => n.Id == note.Id);
        Assert.Equal(NoteStatus.Draft, persisted.Status);
        Assert.Null(persisted.Classification);
    }
}
