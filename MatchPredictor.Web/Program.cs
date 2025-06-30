using Hangfire;
using Hangfire.SQLite;
using MatchPredictor.Application.Services;
using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Infrastructure;
using MatchPredictor.Infrastructure.Persistence;
using MatchPredictor.Infrastructure.Repositories;
using MatchPredictor.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

var env = builder.Environment;

var dbFileName = "app.db";
var dbFolder = env.IsDevelopment() ? 
    Path.Combine(AppContext.BaseDirectory, "data") : 
    "/app/data";

var dbPath = Path.Combine(dbFolder, dbFileName);

// Create folder if missing (in local dev mode)
if (env.IsDevelopment() && !Directory.Exists(dbFolder))
{
    Directory.CreateDirectory(dbFolder);
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// builder.Services.AddDbContext<ApplicationDbContext>(options =>
//     options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IMatchDataRepository, MatchDataRepository>();
builder.Services.AddScoped<IDataAnalyzerService, DataAnalyzerService>();
builder.Services.AddScoped<IWebScraperService, WebScraperService>();
builder.Services.AddScoped<IExtractFromExcel, ExtractFromExcel>();
builder.Services.AddScoped<AnalyzerService>();

// var hangfireDbPath = Path.Combine(Path.GetTempPath(), "hangfire.db");
// var sqliteConnection = new SqliteConnection($"Data Source={hangfireDbPath}");
// sqliteConnection.Open();

var hangfireDbPath = Path.Combine(dbFolder, "hangfire.db");
var sqliteConnection = new SqliteConnection($"Data Source={hangfireDbPath}");
sqliteConnection.Open();


// ✅ Register Hangfire core services
builder.Services.AddHangfire(config =>
{
    config.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseStorage(new SQLiteStorage(sqliteConnection));
});

builder.Services.AddHangfireServer(); 

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated(); // or use db.Database.Migrate() if using EF migrations
}

app.UseHangfireDashboard();

app.Lifetime.ApplicationStarted.Register(() =>
{
    RecurringJob.AddOrUpdate<AnalyzerService>(
        "daily-prediction-job",
        service => service.RunScraperAndAnalyzerAsync(),
        "5 0,12 * * *" // At 12:05 AM and 12:05 PM every day
    );
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseHangfireDashboard("/hangfire");

app.UseAuthorization();

app.MapRazorPages();

app.Run();