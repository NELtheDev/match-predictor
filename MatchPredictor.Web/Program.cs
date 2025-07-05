using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using MatchPredictor.Application.Services;
using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Infrastructure;
using MatchPredictor.Infrastructure.Persistence;
using MatchPredictor.Infrastructure.Repositories;
using MatchPredictor.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();

// Configure database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register application services
builder.Services.AddScoped<IMatchDataRepository, MatchDataRepository>();
builder.Services.AddScoped<IDataAnalyzerService, DataAnalyzerService>();
builder.Services.AddScoped<IWebScraperService, WebScraperService>();
builder.Services.AddScoped<IExtractFromExcel, ExtractFromExcel>();
builder.Services.AddScoped<AnalyzerService>();

// Configure data protection
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>();

// Configure logging
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
);

// Configure Hangfire
builder.Services.AddLogging();
builder.Services.AddSingleton<LogFailureAttribute>();
builder.Services.AddSingleton<IJobFilterProvider, DependencyInjectionFilterProvider>();

// Configure Hangfire
builder.Services.AddHangfire((provider, config) =>
{
    config.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => 
        {
            options.UseNpgsqlConnection(connectionString);
        });
    
    config.UseFilter(new AutomaticRetryAttribute { Attempts = 3 });

    // Register recurring jobs HERE
    RecurringJob.AddOrUpdate<AnalyzerService>(
        "daily-prediction-job",
        service => service.RunScraperAndAnalyzerAsync(),
        "5 0,12 * * *", // Every day at 00:05 and 12:05 UTC
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            MisfireHandling = MisfireHandlingMode.Relaxed
        }
    );

    RecurringJob.AddOrUpdate<AnalyzerService>(
        "cleanup-old-predictions",
        service => service.CleanupOldPredictionsAsync(),
        "0 1 * * *", // At 1:00 AM daily
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        }
    );
});

// Configure Kestrel
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Configure Hangfire dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Match Predictor Jobs"
});

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();