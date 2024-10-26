# 🌐 Proxy Miner

A high-performance C# console application that automatically collects, verifies, and maintains lists of public proxies with enhanced features and optimized performance.

## ✨ Key Features

- 🔄 Automatic hourly updates of proxy lists
- 🌍 Support for HTTP, SOCKS4, SOCKS5, and Elite proxies
- ⚡ Parallel processing for faster proxy verification
- 📊 Real-time statistics and monitoring
- 🛡️ Advanced proxy anonymity detection
- 💾 Automatic GitHub repository updates
- ⏱️ Response time measurement for each proxy
- 🔍 Duplicate proxy filtering
- 🚀 Optimized network operations

## 🔧 Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- GitHub account with personal access token
- Git (optional)

## 📦 Required Packages

- Octokit (GitHub API integration)
- HtmlAgilityPack (HTML parsing)
- DotNetEnv (Environment configuration)

## 🚀 Quick Start

1. Clone the repository:
```bash
git clone https://github.com/ZoniBoy00/proxy-miner.git
cd proxy-miner
```

2. Create `.env` file in project root:
```env
GITHUB_TOKEN=your_github_token
GITHUB_REPO_OWNER=your_github_username
GITHUB_REPO_NAME=your_repo_name
```

3. Build and run:
```bash
dotnet restore
dotnet build
dotnet run
```

## 🛠️ Configuration

### Environment Variables Setup

#### Windows
```powershell
setx GITHUB_TOKEN "your_github_token"
setx GITHUB_REPO_OWNER "your_github_username"
setx GITHUB_REPO_NAME "your_repo_name"
```

#### Linux/macOS
```bash
export GITHUB_TOKEN="your_github_token"
export GITHUB_REPO_OWNER="your_github_username"
export GITHUB_REPO_NAME="your_repo_name"
```

## 📊 Output Files

The application maintains four separate proxy lists:

- `http_proxies.txt` - HTTP proxies
- `socks4_proxies.txt` - SOCKS4 proxies
- `socks5_proxies.txt` - SOCKS5 proxies
- `elite_proxies.txt` - Elite anonymous proxies

Each file contains:
- IP:Port combinations
- Last verification timestamp
- Response time metrics

## 🔄 Operation Cycle

1. Fetches proxies from multiple sources
2. Verifies each proxy in parallel
3. Tests proxy anonymity level
4. Measures response times
5. Updates GitHub repository
6. Waits one hour before next cycle

## ⚡ Performance Features

- Parallel proxy verification
- Connection pooling
- Optimized HTTP client settings
- Efficient memory management
- Thread-safe collections
- Batch processing for GitHub updates

## 🛡️ Error Handling

- Automatic retry for failed GitHub operations
- Graceful degradation for unavailable sources
- Detailed error logging
- Connection timeout management
- Invalid proxy filtering

## 🔍 Proxy Verification Process

1. Connection test
2. Protocol verification
3. Anonymity level detection
4. Response time measurement
5. Duplicate filtering
6. Working status confirmation

## 📈 Statistics

- Total proxies found
- Working proxies by type
- Average response times
- Success rate percentage
- Update frequency metrics

## ⚠️ Troubleshooting

### Common Issues:

1. **GitHub Authentication Failed**
   - Verify token permissions
   - Check environment variables
   - Confirm token validity

2. **No Proxies Found**
   - Check internet connection
   - Verify source availability
   - Review firewall settings

3. **Performance Issues**
   - Adjust batch size
   - Check system resources
   - Verify network capacity

## 🤝 Contributing

1. Fork the repository
2. Create feature branch
3. Implement changes
4. Add tests if applicable
5. Submit pull request

## 📜 License

MIT License - See LICENSE file for details

## ⚖️ Legal Disclaimer

This tool is for educational purposes only. Users must:
- Comply with local laws
- Respect terms of service
- Use proxies responsibly
- Maintain ethical practices

## 🔧 Technical Details

- Language: C# 11
- Framework: .NET 8.0
- Architecture: Async/Await pattern
- Threading: Task Parallel Library
- Network: HttpClient with custom handlers
- Storage: GitHub API integration

## 📞 Support

- Open issue on GitHub
- Review documentation
- Join discussions

## 🙏 Acknowledgments

- Proxy source providers
- Open source community
- Framework contributors
- Testing volunteers
