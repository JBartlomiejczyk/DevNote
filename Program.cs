var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DevNote.Services.WizardStateService>();
builder.Services.Configure<DevNote.Services.AzureOpenAIOptions>(
    builder.Configuration.GetSection(DevNote.Services.AzureOpenAIOptions.SectionName));
builder.Services.AddScoped<DevNote.Services.ClassificationService>();

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/healthz", () => Results.Ok("Healthy"));

app.MapRazorComponents<DevNote.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
