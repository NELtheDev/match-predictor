using MatchPredictor.Domain.Interfaces;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace MatchPredictor.Infrastructure;

public class WebScraperService : IWebScraperService
{
    //private readonly IConfiguration _config;
    private const string DownloadUrl = "https://www.sports-ai.dev/predictions";
    private readonly string _downloadFolder;

    public WebScraperService()
    {
        var projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName ?? string.Empty;
        _downloadFolder = Path.Combine(projectDirectory, "Resources");
        Directory.CreateDirectory(_downloadFolder); // Ensure the directory exists
    }
    
    // public async Task ScrapeMatchDataAsync()
    // {
    //     try
    //     {
    //         // Automatically download and set up the correct ChromeDriver
    //         new DriverManager().SetUpDriver(new ChromeConfig());
    //         
    //         var chromeOptions = new ChromeOptions();
    //         chromeOptions.AddUserProfilePreference("download.default_directory", _downloadFolder);
    //         chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
    //         chromeOptions.AddUserProfilePreference("download.directory_upgrade", true);
    //         chromeOptions.AddUserProfilePreference("safebrowsing.enabled", true);
    //         chromeOptions.AddArgument("--headless");
    //         chromeOptions.AddArgument("--no-sandbox");
    //         chromeOptions.AddArgument("--disable-dev-shm-usage");
    //
    //         var service = ChromeDriverService.CreateDefaultService();
    //         service.HideCommandPromptWindow = true;
    //
    //         // Delete the previous downloaded file if found
    //         DeletePreviousFile();
    //
    //         using var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(60));
    //         await driver.Navigate().GoToUrlAsync(DownloadUrl);
    //
    //         var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
    //         var downloadButton = wait.Until(d =>
    //         {
    //             var element = d.FindElement(By.XPath("//*[@id='__next']/div/main/div[1]/div[1]/div[1]/button"));
    //             return element is { Displayed: true, Enabled: true } ? element : null;
    //         });
    //
    //         downloadButton.Click();
    //         Console.WriteLine("Download button clicked, waiting for file to be downloaded...");
    //
    //         await CheckFileIsDownloaded();
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"An error occurred while scraping the Excel file: {ex.Message}");
    //         throw; // Re-throw the exception to handle it further up if needed
    //     }
    // }
    
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

            using var driver = new ChromeDriver(service, chromeOptions, TimeSpan.FromSeconds(60));
            await driver.Navigate().GoToUrlAsync(DownloadUrl);

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(60));
            IWebElement? downloadButton = null;

            wait.Until(d =>
            {
                try
                {
                    var element = d.FindElement(By.XPath("//*[@id='__next']/div/main/div[1]/div[1]/div[1]/button"));
                    if (element.Displayed && element.Enabled)
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
            Console.WriteLine("Download button clicked, waiting for file to be downloaded...");

            await CheckFileIsDownloaded();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while scraping the Excel file: {ex.Message}");
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
                Console.WriteLine($"Checking file path: {path}");
                if (!File.Exists(path)) continue;
                Console.WriteLine($"File found at: {path}");
                if (path == Path.Combine(_downloadFolder, fileName)) return;
                File.Move(path, Path.Combine(_downloadFolder, fileName), true);
                Console.WriteLine($"File moved to: {_downloadFolder}");
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
            Console.WriteLine($"Previous file deleted: {filePath}");
        }

        foreach (var otherFile in otherFiles)
        {
            File.Delete(otherFile);
            Console.WriteLine($"Previous file deleted: {otherFile}");
        }
    }
}