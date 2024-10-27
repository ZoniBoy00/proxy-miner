#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Octokit;
using DotNetEnv;
using System.Diagnostics;

namespace ProxyCheckerConsoleApp
{
    class Program
    {
        // Thread-safe collections
        private static readonly ConcurrentBag<ProxyInfo> proxyList = new();
        private static readonly ConcurrentDictionary<string, byte> uniqueProxies = new();
        private static readonly SemaphoreSlim semaphore = new(250);

        // GitHub configuration
        private static string? githubToken;
        private static string? githubRepoOwner;
        private static string? githubRepoName;

        // User agents for rotation
        private static readonly string[] userAgents = {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:89.0) Gecko/20100101 Firefox/89.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.1.1 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Edge/91.0.864.59"
        };

        // HTTP client configuration
        private static readonly HttpClientHandler handler = new()
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            MaxConnectionsPerServer = 1000
        };

        private static readonly HttpClient httpClient = new(handler)
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        // Global network settings
        static Program()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.ReusePort = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            ServicePointManager.CheckCertificateRevocationList = true; // Enabled for security
        }

        // Proxy model
        private class ProxyInfo
        {
            public string Ip { get; set; } = string.Empty;
            public string Port { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool IsWorking { get; set; }
            public int ResponseTime { get; set; }
            public string Anonymity { get; set; } = "Unknown";
            public DateTime LastChecked { get; set; }
            public string Country { get; set; } = "Unknown";
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
        }

        // Proxy sources
        private static readonly string[] proxySources = new string[]
        {
            "https://www.sslproxies.org",
            "https://www.us-proxy.org",
            "https://free-proxy-list.net",
            "https://www.socks-proxy.net",
            "https://www.proxynova.com/proxy-server-list",
            "https://www.iplocation.net/proxy-list",
            "https://openproxy.space/list/http",
            "https://openproxy.space/list/socks4",
            "https://openproxy.space/list/socks5",
            "https://proxylist.geonode.com/api/proxy-list",
            "https://free-proxy-list.com",
            "https://api.proxyscrape.com",
            "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/http.txt",
            "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/socks4.txt",
            "https://raw.githubusercontent.com/TheSpeedX/PROXY-List/master/socks5.txt",
            "https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/http.txt",
            "https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/socks4.txt",
            "https://raw.githubusercontent.com/ShiftyTR/Proxy-List/master/socks5.txt",
            "https://raw.githubusercontent.com/hookzof/socks5_list/master/proxy.txt",
            "https://raw.githubusercontent.com/clarketm/proxy-list/master/proxy-list.txt",
            "https://raw.githubusercontent.com/sunny9577/proxy-scraper/master/proxies.txt",
            "https://raw.githubusercontent.com/fate0/proxylist/master/proxy.list",
            "https://raw.githubusercontent.com/roosterkid/openproxylist/main/HTTPS_RAW.txt",
            "https://raw.githubusercontent.com/monosans/proxy-list/main/proxies/http.txt",
            "https://raw.githubusercontent.com/monosans/proxy-list/main/proxies_anonymous/http.txt",
            "https://raw.githubusercontent.com/jetkai/proxy-list/main/online-proxies/txt/proxies.txt",
            "https://raw.githubusercontent.com/rdavydov/proxy-list/main/proxies/http.txt",
            "https://raw.githubusercontent.com/rdavydov/proxy-list/main/proxies/socks4.txt",
            "https://raw.githubusercontent.com/rdavydov/proxy-list/main/proxies/socks5.txt",
            "https://raw.githubusercontent.com/TheSpeedX/SOCKS-List/master/http.txt",
            "https://raw.githubusercontent.com/TheSpeedX/SOCKS-List/master/socks4.txt",
            "https://raw.githubusercontent.com/TheSpeedX/SOCKS-List/master/socks5.txt",
            "https://raw.githubusercontent.com/mmpx12/proxy-list/master/http.txt",
            "https://raw.githubusercontent.com/mmpx12/proxy-list/master/socks4.txt",
            "https://raw.githubusercontent.com/mmpx12/proxy-list/master/socks5.txt",
            "https://raw.githubusercontent.com/proxy4parsing/proxy-list/main/http.txt",
            "https://raw.githubusercontent.com/roosterkid/openproxylist/main/SOCKS4_RAW.txt",
            "https://raw.githubusercontent.com/roosterkid/openproxylist/main/SOCKS5_RAW.txt",
            "https://raw.githubusercontent.com/ZoniBoy00/proxy-lists/master/http_proxies.txt",
            "https://raw.githubusercontent.com/ZoniBoy00/proxy-lists/master/socks4_proxies.txt",
            "https://raw.githubusercontent.com/ZoniBoy00/proxy-lists/master/socks5_proxies.txt",
            "https://raw.githubusercontent.com/ZoniBoy00/proxy-lists/master/elite_proxies.txt",
            "https://www.proxynova.com/proxy-server-list/elite-proxies",
            "https://www.proxynova.com/proxy-server-list/anonymous-proxies",
            "https://api.proxyscrape.com/v4/free-proxy-list/get?request=display_proxies&proxy_format=ipport&format=text&anonymity=Elite&timeout=20000",
            "https://api.proxyscrape.com/v4/free-proxy-list/get?request=display_proxies&proxy_format=protocolipport&format=text",
            "https://hidemy.name/en/proxy-list/",
            "https://spys.one/en/free-proxy-list/",
            "https://proxy-daily.com/",
            "https://proxyscan.io/download?type=http",
            "https://proxydb.net/?protocol=https",
            "https://sockslist.us/api?request=display&country=all&level=all&token=free",
            "https://www.proxy-list.download/api/v1/get?type=https",
            "https://proxyservers.pro/proxy/list/",
            "https://checkerproxy.net/api/archive/",
            "https://www.my-proxy.com/free-proxy-list.html"
        };

        // Main entry point
        static async Task Main()
        {
            try
            {
                Env.Load();
                githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                githubRepoOwner = Environment.GetEnvironmentVariable("GITHUB_REPO_OWNER");
                githubRepoName = Environment.GetEnvironmentVariable("GITHUB_REPO_NAME");

                if (string.IsNullOrEmpty(githubToken) || string.IsNullOrEmpty(githubRepoOwner) || string.IsNullOrEmpty(githubRepoName))
                {
                    throw new Exception("GitHub configuration is missing in .env file");
                }

                Console.WriteLine("Starting Proxy Checker...");
                await TestGitHubConnection();

                while (true)
                {
                    await RunProxyCheck();

                    // Wait for one hour before the next scan
                    Console.WriteLine("Waiting for one hour before the next scan...");
                    await Task.Delay(TimeSpan.FromHours(1));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private static async Task TestGitHubConnection()
        {
            try
            {
                var client = new GitHubClient(new ProductHeaderValue("ProxyCheckerConsoleApp"))
                {
                    Credentials = new Credentials(githubToken)
                };

                var user = await client.User.Current();
                Console.WriteLine($"Successfully connected to GitHub as {user.Login}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to GitHub: {ex.Message}");
            }
        }

        // Main proxy checking process
        private static async Task RunProxyCheck()
        {
            Console.WriteLine("Starting new proxy check cycle...");
            var startTime = DateTime.Now;

            try
            {
                await FetchProxiesParallel();
                Console.WriteLine($"Found {proxyList.Count} unique proxies to check");

                await CheckProxiesParallel();

                var workingProxies = proxyList
                    .Where(p => p.IsWorking)
                    .OrderBy(p => p.Type)
                    .ThenBy(p => p.ResponseTime)
                    .ToList();

                Console.WriteLine($"Found {workingProxies.Count} working proxies");

                if (workingProxies.Any())
                {
                    await SaveResultsToGitHub(workingProxies);
                }

                var duration = DateTime.Now - startTime;
                Console.WriteLine($"Check cycle completed in {duration.TotalMinutes:F1} minutes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in check cycle: {ex.Message}");
            }
            finally
            {
                proxyList.Clear();
                uniqueProxies.Clear();
            }
        }

        // Fetch proxies from all sources in parallel
        private static async Task FetchProxiesParallel()
        {
            Console.WriteLine("Fetching proxies from all sources...");
            var tasks = proxySources.Select(source => FetchFromSource(source));
            await Task.WhenAll(tasks);
        }

        // Fetch from individual source
        private static async Task FetchFromSource(string url)
        {
            try
            {
                var response = await httpClient.GetStringAsync(url);
                Console.WriteLine($"Successfully fetched from {url}");

                if (url.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    ParsePlainTextProxies(response, url);
                }
                else
                {
                    ParseHtmlProxies(response, url);
                }
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"Network error fetching from {url}: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching from {url}: {ex.Message}");
            }
        }

        // Check proxies in parallel
        private static async Task CheckProxiesParallel()
        {
            Console.WriteLine("Starting proxy verification...");
            var batchSize = 100;
            var proxyArray = proxyList.ToArray();
            var batches = (int)Math.Ceiling(proxyArray.Length / (double)batchSize);

            for (var i = 0; i < batches; i++)
            {
                var currentBatch = proxyArray
                    .Skip(i * batchSize)
                    .Take(batchSize);

                var tasks = currentBatch.Select(proxy => CheckProxy(proxy));
                await Task.WhenAll(tasks);

                var progress = ((i + 1.0) / batches) * 100;
                var workingCount = proxyList.Count(p => p.IsWorking);
                Console.WriteLine($"Progress: {progress:F1}% | Working proxies found: {workingCount}");
            }
        }

        // Check individual proxy
        private static async Task CheckProxy(ProxyInfo proxy)
        {
            await semaphore.WaitAsync();
            try
            {
                var proxyUrl = proxy.Type.ToLower() switch
                {
                    "socks5" => $"socks5://{proxy.Ip}:{proxy.Port}",
                    "socks4" => $"socks4://{proxy.Ip}:{proxy.Port}",
                    _ => $"http://{proxy.Ip}:{proxy.Port}"
                };

                if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var uriResult))
                {
                    Console.WriteLine($"Invalid proxy URL: {proxyUrl}. Skipping this proxy.");
                    return;
                }

                using var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(uriResult),
                    UseProxy = true,
                    AllowAutoRedirect = false
                };

                using var client = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };

                client.DefaultRequestHeaders.UserAgent.ParseAdd(GetRandomUserAgent());

                var sw = Stopwatch.StartNew();
                proxy.LastChecked = DateTime.UtcNow;

                var testUrls = new[] { "http://ip-api.com/json/", "http://httpbin.org/ip", "https://api.ipify.org?format=json" };

                foreach (var url in testUrls)
                {
                    try
                    {
                        var response = await FetchWithRetryAsync(client, url);
                        sw.Stop();

                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            proxy.IsWorking = true;
                            proxy.ResponseTime = (int)sw.ElapsedMilliseconds;
                            proxy.SuccessCount++;

                            proxy.Anonymity = DetermineAnonymity(content, response);
                            return;
                        }
                    }
                    catch
                    {
                        proxy.FailureCount++;
                        continue;
                    }
                }

                proxy.IsWorking = false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static string DetermineAnonymity(string content, HttpResponseMessage response)
        {
            if (content.Contains("transparent"))
                return "Transparent";

            if (!response.Headers.Contains("X-Forwarded-For") && !response.Headers.Contains("Via"))
                return "Elite";

            return "Anonymous";
        }

        // Helper methods
        private static async Task<HttpResponseMessage> FetchWithRetryAsync(HttpClient client, string url, int maxRetries = 2)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await client.GetAsync(url);
                }
                catch (Exception)
                {
                    if (i == maxRetries - 1) throw;
                    await Task.Delay((i + 1) * 1000);
                }
            }
            throw new HttpRequestException($"Failed to fetch {url} after {maxRetries} retries.");
        }

        private static string GetRandomUserAgent()
        {
            return userAgents[Random.Shared.Next(userAgents.Length)];
        }

        private static void ParseHtmlProxies(string html, string sourceUrl)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var tablePatterns = new[]
                {
                    "//table[@class='table table-striped table-bordered']//tr",
                    "//table[contains(@class, 'proxy-list')]//tr",
                    "//table[contains(@class, 'proxies')]//tr",
                    "//table[@id='proxylisttable']//tr",
                    "//table//tr[contains(@class, 'odd')]"
                };

                foreach (var pattern in tablePatterns)
                {
                    var rows = doc.DocumentNode.SelectNodes(pattern);
                    if (rows != null)
                    {
                        foreach (var row in rows.Skip(1))
                        {
                            var cells = row.SelectNodes("td");
                            if (cells?.Count >= 2)
                            {
                                var anonymityCell = cells.Count >= 5 ? cells[4].InnerText.Trim().ToLower() : "unknown";
                                var type = DetermineProxyType(sourceUrl, anonymityCell);
                                var country = cells.Count >= 3 ? cells[2].InnerText.Trim() : "Unknown";

                                AddProxyToList(
                                    cells[0].InnerText.Trim(),
                                    cells[1].InnerText.Trim(),
                                    type,
                                    anonymityCell,
                                    country
                                );
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing HTML from {sourceUrl}: {ex.Message}");
            }
        }

        private static void ParsePlainTextProxies(string content, string sourceUrl)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var type = DetermineProxyType(sourceUrl + line.ToLower(), "");
                    AddProxyToList(parts[0], parts[1].Split(' ')[0], type, type == "elite" ? "Elite" : "Unknown", "Unknown");
                }
            }
        }

        private static void AddProxyToList(string ip, string port, string type, string anonymity, string country)
        {
            if (uniqueProxies.TryAdd($"{ip}:{port}", 1))
            {
                proxyList.Add(new ProxyInfo
                {
                    Ip = ip,
                    Port = port,
                    Type = type,
                    IsWorking = false,
                    ResponseTime = 0,
                    Anonymity = anonymity,
                    Country = country,
                    LastChecked = DateTime.UtcNow
                });
            }
        }

        private static string DetermineProxyType(string content, string anonymity)
        {
            content = content.ToLower();
            if (content.Contains("socks5") || content.Contains("sock5"))
                return "socks5";
            if (content.Contains("socks4") || content.Contains("sock4"))
                return "socks4";
            if (content.Contains("elite") || anonymity.Contains("elite") ||
                content.Contains("high anonymous") || anonymity.Contains("high anonymous"))
                return "elite";
            return "http";
        }

        private static async Task SaveResultsToGitHub(List<ProxyInfo> workingProxies)
        {
            Console.WriteLine("Saving results to GitHub...");
            var client = new GitHubClient(new ProductHeaderValue("ProxyCheckerConsoleApp"))
            {
                Credentials = new Credentials(githubToken)
            };

            var proxyTypes = new[] { "http", "socks4", "socks5", "elite" };
            var proxyTypeCounts = proxyTypes.ToDictionary(t => t, t => 0);

            foreach (var type in proxyTypes)
            {
                var proxiesOfType = workingProxies.Where(p => p.Type.ToLower() == type).ToList();
                proxyTypeCounts[type] = proxiesOfType.Count;

                if (proxiesOfType.Any())
                {
                    await UpdateProxyFile(client, type, proxiesOfType);
                }
            }

            await UpdateReadme(client, proxyTypeCounts);
        }

        private static async Task UpdateProxyFile(GitHubClient client, string type, List<ProxyInfo> proxies)
        {
            var filePath = $"{type}_proxies.txt";
            var proxyData = string.Join("\n", proxies.Select(p => $"{p.Ip}:{p.Port}"));

            try
            {
                var existingFile = await client.Repository.Content.GetAllContents(githubRepoOwner, githubRepoName, filePath);

                if (existingFile.Any())
                {
                    var updateRequest = new UpdateFileRequest(
                        $"Updated {type} proxies",
                        proxyData,
                        existingFile.First().Sha
                    );
                    await client.Repository.Content.UpdateFile(githubRepoOwner, githubRepoName, filePath, updateRequest);
                }
                else
                {
                    var createRequest = new CreateFileRequest($"Created {type} proxies file", proxyData);
                    await client.Repository.Content.CreateFile(githubRepoOwner, githubRepoName, filePath, createRequest);
                }

                Console.WriteLine($"Updated {filePath} successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating {filePath}: {ex.Message}");
            }
        }

        private static async Task UpdateReadme(GitHubClient client, Dictionary<string, int> proxyTypeCounts)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("dddd dd-MM-yyyy HH:mm:ss");
                var totalProxies = proxyTypeCounts.Values.Sum();

                var readmeContent = GenerateReadmeContent(timestamp, totalProxies, proxyTypeCounts);

                var existingReadme = await client.Repository.Content.GetAllContents(githubRepoOwner, githubRepoName, "README.md");
                var updateRequest = new UpdateFileRequest(
                    "Updated README with latest proxy statistics",
                    readmeContent,
                    existingReadme.First().Sha
                );

                await client.Repository.Content.UpdateFile(
                    githubRepoOwner,
                    githubRepoName,
                    "README.md",
                    updateRequest
                );

                Console.WriteLine("README.md updated successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update README.md: {ex.Message}");
            }
        }

        private static string GenerateReadmeContent(string timestamp, int totalProxies, Dictionary<string, int> proxyTypeCounts)
        {
            return $@"# 🌐 Free Proxy List

## 📊 Real-Time Statistics
- 🕒 Last Updated: {timestamp} UTC
- 📈 Total Working Proxies: {totalProxies}

## 📥 Proxy Downloads

### HTTP Proxies
- Count: {proxyTypeCounts["http"]}
- [Download HTTP Proxies](https://raw.githubusercontent.com/{githubRepoOwner}/{githubRepoName}/master/http_proxies.txt)

### SOCKS4 Proxies
- Count: {proxyTypeCounts["socks4"]}
- [Download SOCKS4 Proxies](https://raw.githubusercontent.com/{githubRepoOwner}/{githubRepoName}/master/socks4_proxies.txt)

### SOCKS5 Proxies
- Count: {proxyTypeCounts["socks5"]}
- [Download SOCKS5 Proxies](https://raw.githubusercontent.com/{githubRepoOwner}/{githubRepoName}/master/socks5_proxies.txt)

### Elite Proxies
- Count: {proxyTypeCounts["elite"]}
- [Download Elite Proxies](https://raw.githubusercontent.com/{githubRepoOwner}/{githubRepoName}/master/elite_proxies.txt)

## 📈 Proxy Types Overview

| Type | Working Proxies |
|------|----------------|
| HTTP | {proxyTypeCounts["http"]} |
| SOCKS4 | {proxyTypeCounts["socks4"]} |
| SOCKS5 | {proxyTypeCounts["socks5"]} |
| ELITE | {proxyTypeCounts["elite"]} |

## ✨ Features
- 🔄 Auto-updates every day
- ✅ All proxies are tested
- ⚡ Speed tested
- 🌍 Support for multiple proxy types
- 🛡️ Elite proxy detection

## 📝 Notes
- Proxies are gathered from public sources
- Speed and status may vary
- Implement your own validation for critical uses

## ⚠️ Disclaimer
These proxies are for educational purposes only. Users must comply with local laws and regulations.

---
*Updated: {timestamp} UTC*";
        }
    }
}
