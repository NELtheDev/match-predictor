using Hangfire;
using Hangfire.PostgreSql;
using MatchPredictor.Application.Services;
using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Infrastructure;
using MatchPredictor.Infrastructure.Persistence;
using MatchPredictor.Infrastructure.Repositories;
using MatchPredictor.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();

var env = builder.Environment;

// Use a writable path in production (Render/Docker)
var dbFolder = env.IsDevelopment()
    ? Path.Combine(AppContext.BaseDirectory, "data")
    : Path.Combine(Path.GetTempPath(), "matchpredictor");

// Ensure directory exists
if (!Directory.Exists(dbFolder))
    Directory.CreateDirectory(dbFolder);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IMatchDataRepository, MatchDataRepository>();
builder.Services.AddScoped<IDataAnalyzerService, DataAnalyzerService>();
builder.Services.AddScoped<IWebScraperService, WebScraperService>();
builder.Services.AddScoped<IExtractFromExcel, ExtractFromExcel>();
builder.Services.AddScoped<AnalyzerService>();

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddHangfire(config =>
{
    config.UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UsePostgreSqlStorage(options => {
              options.UseNpgsqlConnection(connectionString);
          });
});

builder.Services.AddHangfireServer();

// Register Serilog for logging
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
);

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

app.UseHangfireDashboard();

app.Lifetime.ApplicationStarted.Register(() =>
{
    RecurringJob.AddOrUpdate<AnalyzerService>(
        "daily-prediction-job",
        service => service.RunScraperAndAnalyzerAsync(),
        "5 0,12 * * *" // Every day at 00:05 and 12:05 UTC
    );
    
    // Job 2: Cleanup old predictions daily at 1:00 AM
    RecurringJob.AddOrUpdate<AnalyzerService>(
        "cleanup-old-predictions",
        service => service.CleanupOldPredictionsAsync(),
        "0 1 * * *" // At 1:00 AM daily
    );
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseHangfireDashboard("/hangfire");
app.UseAuthorization();
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // ✅ Applies pending migrations
}

app.Run();
