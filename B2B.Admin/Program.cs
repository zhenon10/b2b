using B2B.Admin.Components;
using B2B.Admin.Services;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<AdminUiNotify>();
builder.Services.AddScoped<AdminAuthRefreshHandler>();

builder.Services.AddHttpClient("apiInternal", (sp, http) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["Api:BaseUrl"] ?? "http://localhost:5000";
    http.BaseAddress = new Uri(baseUrl);
    http.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<B2BApiClient>()
    .ConfigureHttpClient((sp, http) =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseUrl = config["Api:BaseUrl"] ?? "http://localhost:5000";
        http.BaseAddress = new Uri(baseUrl);
        http.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddHttpMessageHandler<AdminAuthRefreshHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
