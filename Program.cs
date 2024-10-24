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
using System.Timers;
using HtmlAgilityPack;
using Octokit;
using DotNetEnv;

namespace ProxyCheckerConsoleApp
{
    class Program
    {
        private static readonly ConcurrentBag<ProxyInfo> proxyList = new();
        private static string? githubToken;
        private static string? githubRepoOwner;
        private static string? githubRepoName;
        private static readonly ConcurrentDictionary<string, byte> uniqueProxies = new();
        private static readonly SemaphoreSlim semaphore = new(100);

        private static readonly HttpClientHandler handler = new()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            MaxConnectionsPerServer = 100
        };

        private static readonly HttpClient httpClient = new(handler)
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        static Program()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.ReusePort = true;
        }

        private class ProxyInfo
        {
            public required string Ip { get; set; }
            public required string Port { get; set; }
            public required string Type { get; set; }
            public bool IsWorking { get; set; }
            public int ResponseTime { get; set; }
        }

        private static readonly string[] proxySources = {
            "https://www.sslproxies.org/",
            "https://www.us-proxy.org/",
            "https://free-proxy-list.net/",
            "https://www.socks-proxy.net/",
            "https://api.proxyscrape.com/v2/?request=getproxies&protocol=http",
            "https://api.proxyscrape.com/v2/?request=getproxies&protocol=socks4",
            "https://api.proxyscrape.com/v2/?request=getproxies&protocol=socks5",
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
            "https://proxylist.geonode.com/api/proxy-list?limit=500&page=1&sort_by=lastChecked&sort_type=desc",
            "https://www.proxynova.com/proxy-server-list/",
            "https://www.iplocation.net/proxy-list"
        };


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

                await TestGitHubConnection();
                await RunProxyCheck();
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

        private static async Task RunProxyCheck()
        {
            while (true)
            {
                Console.WriteLine("Starting proxy check...");
                await FetchProxiesParallel();

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

                proxyList.Clear();
                uniqueProxies.Clear();

                Console.WriteLine("Waiting 1 hour before next check...");
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }

        private static async Task FetchProxiesParallel()
        {
            var tasks = proxySources.Select(source => FetchFromSource(source));
            await Task.WhenAll(tasks);
        }

        private static async Task FetchFromSource(string url)
        {
            try
            {
                var response = await httpClient.GetStringAsync(url);

                if (url.Contains("geonode"))
                {
                    ParseJsonProxies(response);
                }
                else if (url.Contains("fate0"))
                {
                    foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        ParseJsonProxies(line);
                    }
                }
                else if (url.EndsWith(".txt"))
                {
                    ParsePlainTextProxies(response);
                }
                else
                {
                    ParseHtmlProxies(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching from {url}: {ex.Message}");
            }
        }

        private static void ParsePlainTextProxies(string content)
        {
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var type = line.ToLower().Contains("socks5") ? "socks5" :
                              line.ToLower().Contains("socks4") ? "socks4" : "http";

                    var proxy = new ProxyInfo
                    {
                        Ip = parts[0],
                        Port = parts[1].Split(' ')[0],
                        Type = type,
                        IsWorking = false,
                        ResponseTime = 0
                    };

                    if (uniqueProxies.TryAdd($"{proxy.Ip}:{proxy.Port}", 1))
                    {
                        proxyList.Add(proxy);
                    }
                }
            }
        }

        private static void ParseJsonProxies(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("ip", out var ip) &&
                            item.TryGetProperty("port", out var port))
                        {
                            var proxy = new ProxyInfo
                            {
                                Ip = ip.GetString() ?? "",
                                Port = port.GetString() ?? port.GetInt32().ToString(),
                                Type = "http",
                                IsWorking = false,
                                ResponseTime = 0
                            };

                            if (uniqueProxies.TryAdd($"{proxy.Ip}:{proxy.Port}", 1))
                            {
                                proxyList.Add(proxy);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
        }

        private static void ParseHtmlProxies(string html)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//table[@class='table table-striped table-bordered']//tr");
                if (rows == null) return;

                foreach (var row in rows.Skip(1))
                {
                    var cells = row.SelectNodes("td");
                    if (cells?.Count >= 2)
                    {
                        var proxy = new ProxyInfo
                        {
                            Ip = cells[0].InnerText.Trim(),
                            Port = cells[1].InnerText.Trim(),
                            Type = html.Contains("socks-proxy") ? "socks4" : "http",
                            IsWorking = false,
                            ResponseTime = 0
                        };

                        if (uniqueProxies.TryAdd($"{proxy.Ip}:{proxy.Port}", 1))
                        {
                            proxyList.Add(proxy);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing HTML: {ex.Message}");
            }
        }

        private static async Task CheckProxiesParallel()
        {
            var tasks = proxyList.Select(proxy => CheckProxy(proxy));
            await Task.WhenAll(tasks);
        }

        private static async Task CheckProxy(ProxyInfo proxy)
        {
            await semaphore.WaitAsync();
            try
            {
                using var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"{proxy.Type}://{proxy.Ip}:{proxy.Port}"),
                    UseProxy = true
                };

                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var response = await client.GetAsync("http://www.google.com");
                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    proxy.IsWorking = true;
                    proxy.ResponseTime = (int)sw.ElapsedMilliseconds;
                    Console.WriteLine($"Working proxy found: {proxy.Type}://{proxy.Ip}:{proxy.Port} ({proxy.ResponseTime}ms)");
                }
            }
            catch
            {
                proxy.IsWorking = false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task SaveResultsToGitHub(List<ProxyInfo> workingProxies)
        {
            if (!workingProxies.Any() || githubToken == null || githubRepoOwner == null || githubRepoName == null)
                return;

            try
            {
                var client = new GitHubClient(new ProductHeaderValue("ProxyCheckerConsoleApp"))
                {
                    Credentials = new Credentials(githubToken)
                };

                var proxyTypes = workingProxies
                    .Select(p => p.Type.ToLower())
                    .Distinct()
                    .ToArray();

                var proxyTypeCounts = new Dictionary<string, int>();

                foreach (var proxyType in proxyTypes)
                {
                    var proxiesOfType = workingProxies
                        .Where(p => p.Type.Equals(proxyType, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(p => p.ResponseTime)
                        .Select(p => $"{p.Ip}:{p.Port} # Response: {p.ResponseTime}ms")
                        .ToList();

                    if (!proxiesOfType.Any()) continue;

                    proxyTypeCounts[proxyType] = proxiesOfType.Count;

                    var fileName = $"{proxyType}_proxies.txt";
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var content = $"# Updated at {timestamp}\n" +
                                $"# Total proxies: {proxiesOfType.Count}\n" +
                                $"# Format: IP:PORT # Response time\n\n" +
                                string.Join(Environment.NewLine, proxiesOfType);

                    try
                    {
                        var existingFile = await client.Repository.Content
                            .GetAllContents(githubRepoOwner, githubRepoName, fileName);

                        var updateRequest = new UpdateFileRequest(
                            $"Update {fileName} - {timestamp}",
                            content,
                            existingFile[0].Sha,
                            branch: "main");

                        await client.Repository.Content.UpdateFile(
                            githubRepoOwner,
                            githubRepoName,
                            fileName,
                            updateRequest);

                        Console.WriteLine($"Successfully updated {fileName}");
                    }
                    catch (NotFoundException)
                    {
                        var createRequest = new CreateFileRequest(
                            $"Create {fileName} - {timestamp}",
                            content,
                            branch: "main");

                        await client.Repository.Content.CreateFile(
                            githubRepoOwner,
                            githubRepoName,
                            fileName,
                            createRequest);

                        Console.WriteLine($"Successfully created {fileName}");
                    }
                }

                // Update README.md with current statistics
                await UpdateReadme(client, proxyTypeCounts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to GitHub: {ex.Message}");
                SaveLocalBackup(workingProxies);
            }
        }

        private static async Task UpdateReadme(GitHubClient client, Dictionary<string, int> proxyTypeCounts)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("dddd dd-MM-yyyy HH:mm:ss");
                var totalProxies = proxyTypeCounts.Values.Sum();

                var readmeContent = $@"# Free Proxy List

This list gets free public proxies that are updated from time to time.
I collected them from the Internet for easy access. Remember, I'm not in charge of these proxies.

Last Updated: {timestamp} UTC
Total Proxies: {totalProxies}

## DOWNLOAD

{string.Join("\n", proxyTypeCounts.Select(kvp => $@"### {kvp.Key.ToUpper()}
https://raw.githubusercontent.com/{githubRepoOwner}/{githubRepoName}/main/{kvp.Key.ToLower()}_proxies.txt
Total: {kvp.Value} proxies"))}

## NOTES
- It is Only For Educational Purposes. Neither I Say Nor I Promote To Do Anything Illegal.
- Developer Please Give Credits, Stars, And Follow If You Use This Proxy List.

## Proxy Sources
- Various public proxy sources
- Regular updates every hour
- Automatically checked for validity
- Response time verified

## Statistics
{string.Join("\n", proxyTypeCounts.Select(kvp => $"- {kvp.Key.ToUpper()}: {kvp.Value} working proxies"))}

## Disclaimer
These proxies are gathered from public sources. Use them at your own risk.
";

                try
                {
                    var existingFile = await client.Repository.Content
                        .GetAllContents(githubRepoOwner, githubRepoName, "README.md");

                    var updateRequest = new UpdateFileRequest(
                        $"Update README.md - {timestamp} UTC",
                        readmeContent,
                        existingFile[0].Sha,
                        branch: "main");

                    await client.Repository.Content.UpdateFile(
                        githubRepoOwner,
                        githubRepoName,
                        "README.md",
                        updateRequest);

                    Console.WriteLine("Successfully updated README.md");
                }
                catch (NotFoundException)
                {
                    var createRequest = new CreateFileRequest(
                        $"Create README.md - {timestamp} UTC",
                        readmeContent,
                        branch: "main");

                    await client.Repository.Content.CreateFile(
                        githubRepoOwner,
                        githubRepoName,
                        "README.md",
                        createRequest);

                    Console.WriteLine("Successfully created README.md");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating README: {ex.Message}");
            }
        }

        private static void SaveLocalBackup(List<ProxyInfo> workingProxies)
        {
            try
            {
                var backupDir = "proxy_backup";
                Directory.CreateDirectory(backupDir);
                var backupFile = Path.Combine(backupDir, $"proxies_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var content = workingProxies
                    .OrderBy(p => p.Type)
                    .ThenBy(p => p.ResponseTime)
                    .Select(p => $"{p.Ip}:{p.Port}:{p.Type} # Response: {p.ResponseTime}ms");

                File.WriteAllLines(backupFile, content);
                Console.WriteLine($"Backup saved to {backupFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving backup: {ex.Message}");
            }
        }
    }
}