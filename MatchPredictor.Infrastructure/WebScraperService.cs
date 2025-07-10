using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace MatchPredictor.Infrastructure;

public partial class WebScraperService : IWebScraperService
{
    private readonly string _downloadFolder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebScraperService> _logger;

    public WebScraperService(IConfiguration configuration, ILogger<WebScraperService> logger)
    {
        _logger = logger;
        _configuration = configuration;
        var projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? string.Empty;
        _downloadFolder = Path.Combine(projectDirectory, "Resources");
        Directory.CreateDirectory(_downloadFolder); // Ensure the directory exists
    }
    
    public async Task ScrapeMatchDataAsync()
    {
        try
        {
            new DriverManager().SetUpDriver(new ChromeConfig());

            var chromeOptions = GetChromeOptions();

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            DeletePreviousFile();

            var downloadUrl = _configuration["ScrapingValues:ScrapingWebsite"] ?? 
                throw new InvalidOperationException("Download URL not configured in appsettings.json");
            
            using var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(60));
            await driver.Navigate().GoToUrlAsync(downloadUrl);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            IWebElement? downloadButton = null;

            wait.Until(d =>
            {
                try
                {
                    var element = d.FindElement(By.XPath(_configuration["ScrapingValues:PredictionsButtonSelector"] 
                        ?? throw new InvalidOperationException("Predictions button selector not configured in appsettings.json")));
                    if (element is { Displayed: true, Enabled: true })
                    {
                        downloadButton = element;
                        return true;
                    }
                    return false;
                }
                catch (StaleElementReferenceException)
                {
                    return false;
                }
                catch (NoSuchElementException)
                {
                    return false;
                }
            });

            downloadButton?.Click();
            _logger.LogInformation("Download button clicked successfully.");

            await CheckFileIsDownloaded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while scraping match data.");
            throw;
        }
    }

    public async Task<List<MatchScore>> ScrapeMatchScoresAsync()
    {
        try
        {
            new DriverManager().SetUpDriver(new ChromeConfig());

            var chromeOptions = GetChromeOptions();
            
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            var downloadUrl = _configuration["ScrapingValues:ScoresWebsite"] ?? 
                              throw new InvalidOperationException("Download URL for scores is not configured in appsettings.json");
            
            using var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(160));
            _logger.LogInformation("Checking URL for scores...");
            await driver.Navigate().GoToUrlAsync(downloadUrl);
            
            _logger.LogInformation("Commencing scrapping for scores in inner HTML...");
            var container = driver.FindElement(By.Id("score-data"));
            var rawHtml = container.GetAttribute("innerHTML");

            var doc = new HtmlDocument();
            doc.LoadHtml($"<div>{rawHtml}</div>");

            var currentLeague = "";

            var nodes = doc.DocumentNode.ChildNodes.Nodes().ToList();
            
            var matchScores = new List<MatchScore>();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                switch (node.Name)
                {
                    case "h4":
                        currentLeague = node.InnerText.Split("Standings")[0].Trim();
                        break;
                    case "span":
                    {
                        var currentTime = node.InnerText.Trim();

                        // Next node is the teams (text)
                        var teamsNode = nodes[i + 1];
                        var currentTeams = teamsNode.InnerText.Trim();

                        // Next node may be <a class="fin"> with score
                        string? score = null;

                        for (var j = 2; j <= 3; j++)
                        {
                            if (i + j < nodes.Count && nodes[i + j].Name == "a" && nodes[i + j].GetAttributeValue("class", "") == "fin")
                            {
                                var rawString = nodes[i + j].InnerText.Trim();
                                score = MyRegex().Match(rawString).Value;
                                break;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(score) && currentTeams.Contains(" - "))
                        {
                            var split = currentTeams.Split(" - ");
                            var home = split[0].Trim();
                            var away = split[1].Trim();
                            
                            matchScores.Add(new MatchScore
                            {
                                League = currentLeague,
                                HomeTeam = home,
                                AwayTeam = away,
                                Score = score,
                                MatchTime = ParseTime(currentTime),
                                BTTSLabel = IsBTTS(score)
                            });
                        }

                        break;
                    }
                }
            }
            
            return matchScores;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while scraping match score.");
            throw;
        }
    }
    
    private async Task CheckFileIsDownloaded()
    {
        var fileName = _configuration["ScrapingValues:PredictionsFileName"]
                       ?? throw new InvalidOperationException("Predictions file name not configured in appsettings.json");
        string[] possiblePaths =
        [
            Path.Combine(_downloadFolder, fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName)
        ];

        _ = int.TryParse(_configuration["ScrapingValues:ScrapingMaxWaitTime"], out var maxWaitTime);
        _ = int.TryParse(_configuration["ScrapingValues:ScrapingMaxWaitInterval"], out var waitInterval);
        var totalWaitTime = 0;

        while (totalWaitTime < maxWaitTime * 1000)
        {
            foreach (var path in possiblePaths)
            {
                _logger.LogInformation("Checking for file at: {Path}", path);
                if (!File.Exists(path)) continue;
                _logger.LogInformation("File found at: {Path}", path);
                if (path == Path.Combine(_downloadFolder, fileName)) return;
                File.Move(path, Path.Combine(_downloadFolder, fileName), true);
                _logger.LogInformation("File moved to download folder: {DownloadFolder}", _downloadFolder);
                return;
            }
            await Task.Delay(waitInterval);
            totalWaitTime += waitInterval;
        }

        throw new FileNotFoundException($"File {fileName} not found in any expected location after {maxWaitTime} seconds.");
    }
    
    private void DeletePreviousFile()
    {
        var files = Directory.GetFiles(_downloadFolder, "*predictions*");
        var otherFiles = Directory.GetFiles(_downloadFolder, "*Unconfirmed");

        foreach (var filePath in files)
        {
            File.Delete(filePath);
            _logger.LogInformation("Deleted previous file: {FilePath}", filePath);
        }

        foreach (var otherFile in otherFiles)
        {
            File.Delete(otherFile);
            _logger.LogInformation("Deleted unconfirmed file: {OtherFile}", otherFile);
        }
    }

    private ChromeOptions GetChromeOptions()
    {
        var chromeOptions = new ChromeOptions();
        chromeOptions.AddUserProfilePreference("download.default_directory", _downloadFolder);
        chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
        chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
        chromeOptions.AddUserProfilePreference("safebrowsing.enabled", true);
        chromeOptions.AddArgument("--remote-debugging-address=127.0.0.1");
        chromeOptions.AddArgument("--headless");
        chromeOptions.AddArgument("--no-sandbox");
        chromeOptions.AddArgument("--disable-dev-shm-usage");
        
        return chromeOptions;
    }
    
    private bool IsBTTS(string score)
    {
        var parts = score.Split(":"); // Split "2:1" into ["2", "1"]
        return parts.Length == 2 &&
               int.TryParse(parts[0], out var h) && // Convert "2" to integer h = 2
               int.TryParse(parts[1], out var a) && // Convert "1" to integer a = 1
               h > 0 && a > 0; // Check that both teams scored
    }
    
    private DateTime ParseTime(string time)
    {
        var today = DateTime.Today;
        return DateTime.ParseExact($"{today:dd-MM-yyyy} {time}", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^\d{1,2}:\d{2}")]
    private static partial Regex MyRegex();
}