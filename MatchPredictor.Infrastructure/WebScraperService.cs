using MatchPredictor.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace MatchPredictor.Infrastructure;

public class WebScraperService : IWebScraperService
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

            var chromeOptions = new ChromeOptions();
            chromeOptions.AddUserProfilePreference("download.default_directory", _downloadFolder);
            chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
            chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
            chromeOptions.AddUserProfilePreference("safebrowsing.enabled", true);
            chromeOptions.AddArgument("--headless");
            chromeOptions.AddArgument("--no-sandbox");
            chromeOptions.AddArgument("--disable-dev-shm-usage");

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            DeletePreviousFile();

            var downloadUrl = _configuration["DownloadUrls:ScrapingWebsite"] ?? 
                throw new InvalidOperationException("Download URL not configured in appsettings.json");
            
            using var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(60));
            await driver.Navigate().GoToUrlAsync(downloadUrl);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            IWebElement? downloadButton = null;

            wait.Until(d =>
            {
                try
                {
                    var element = d.FindElement(By.XPath("//*[@id='__next']/div/main/div[1]/div[1]/div[1]/button"));
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
    
    private async Task CheckFileIsDownloaded()
    {
        const string fileName = "predictions.xlsx";
        string[] possiblePaths =
        [
            Path.Combine(_downloadFolder, fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", fileName)
        ];

        const int maxWaitTime = 60; // Increase wait time to 60 seconds
        const int waitInterval = 1000; // Check every second
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
}