# Proxy Miner

A C# console application that automatically collects, verifies, and maintains lists of public proxies. The application checks proxy servers periodically and updates the lists on GitHub.

## Features

- Collects proxies from multiple public sources
- Verifies proxy functionality and response time
- Supports HTTP, SOCKS4, and SOCKS5 proxies
- Automatically updates proxy lists every hour
- Maintains separate files for different proxy types
- Backs up working proxies locally in case of GitHub API issues
- Updates README with current statistics automatically

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- GitHub account and personal access token
- Git (optional, for cloning the repository)

## Installation

1. Clone the repository or download the source code:
   ```bash
   git clone https://github.com/ZoniBoy00/proxy-miner.git
   cd proxy-miner
   ```

2. Create a .env file in the project root directory with the following content:
   ```
   GITHUB_TOKEN=your_github_token
   GITHUB_REPO_OWNER=your_github_username
   GITHUB_REPO_NAME=your_repo_name
   ```

3. Install the required NuGet packages:
   ```bash
   dotnet restore
   ```

## Setting Environment Variables

### Windows

1. Open Environment Variable Settings:
   - Right-click on the Start button and select System.
   - Choose Advanced system settings from the left.
   - In the System Properties window, click the Environment Variables button.

2. Add New Variables:
   - In the Environment Variables window, under User variables, click New.
   - For each variable, do the following:
     - Variable name: `GITHUB_TOKEN`
     - Variable value: `your_github_token`
     - Click OK.
   - Repeat this process for the two other variables:
     - Variable name: `GITHUB_REPO_OWNER`
     - Variable value: `your_github_username`
     - Variable name: `GITHUB_REPO_NAME`
     - Variable value: `your_repo_name`

3. Restart your IDE/Terminal: Ensure you close and reopen the terminal or IDE (e.g., Visual Studio) to recognize the new environment variables.

### macOS/Linux

1. Open Terminal: Launch your terminal application.

2. Set Environment Variables: You can temporarily set them for the current session using the following commands:
   ```bash
   export GITHUB_TOKEN="your_github_token"
   export GITHUB_REPO_OWNER="your_github_username"
   export GITHUB_REPO_NAME="your_repo_name"
   ```

   To make these changes permanent, add the above lines to your shell configuration file (e.g., .bashrc, .bash_profile, .zshrc, etc.) using your preferred text editor:
   ```bash
   nano ~/.bashrc  # or ~/.bash_profile or ~/.zshrc
   ```
   Add the export commands to the end of the file, save, and close the file.

3. Apply Changes: After editing the configuration file, apply the changes with:
   ```bash
   source ~/.bashrc  # or ~/.bash_profile or ~/.zshrc
   ```

### Verify Variables are Set Correctly

To ensure the environment variables are set correctly, you can run the following commands in your terminal or command prompt:

- **Windows**:
   ```powershell
   echo %GITHUB_TOKEN%
   echo %GITHUB_REPO_OWNER%
   echo %GITHUB_REPO_NAME%
   ```

- **macOS/Linux**:
   ```bash
   echo $GITHUB_TOKEN
   echo $GITHUB_REPO_OWNER
   echo $GITHUB_REPO_NAME
   ```

If the variables are set correctly, you will see their respective values printed in the terminal.

### Restart the Application

Once you have set the environment variables, try running your Proxy Miner application again. It should now be able to use the variables without issues. If you continue to experience problems, check that the variable names are spelled correctly without any extra spaces or characters.

## Building and Running

- Build the application:
  ```bash
  dotnet build
  ```

- Run the application:
  ```bash
  dotnet run
  ```

## Usage

Once started, the application will:

- Connect to GitHub to verify credentials
- Start collecting proxies from various sources
- Verify each proxy's functionality
- Save working proxies to separate files based on their type
- Update the repository with new proxy lists
- Wait for one hour before the next check

The application will continue running until manually stopped.

## Output Files

The application creates the following files in your GitHub repository:

- `http_proxies.txt`: List of working HTTP proxies
- `socks4_proxies.txt`: List of working SOCKS4 proxies
- `socks5_proxies.txt`: List of working SOCKS5 proxies

Each file contains:

- Timestamp of last update
- Total number of proxies
- List of proxies with their response times

## Local Backup

If GitHub updates fail, the application creates local backups in:

```
proxy_backup/proxies_YYYYMMDD_HHMMSS.txt
```

## Troubleshooting

### Common Issues:

- **GitHub Authentication Failed**
  - Verify your token has the correct permissions.
  - Check if the token is correctly set in the .env file.

- **No Proxies Found**
  - Check your internet connection.
  - Verify the proxy sources are accessible.

- **Build Errors**
  - Ensure you have .NET 8.0 SDK installed.
  - Run `dotnet restore` to restore packages.

## Contributing

1. Fork the repository.
2. Create a feature branch.
3. Commit your changes.
4. Push to the branch.
5. Create a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Disclaimer

This tool is for educational purposes only. The proxies are collected from public sources, and their use is at your own risk. Always comply with the terms of service of any services you access through these proxies.

## Acknowledgments

- Thanks to all the public proxy providers.
- Built with .NET 8.0.
- Uses Octokit for GitHub integration.
- Uses HtmlAgilityPack for HTML parsing.
- Uses DotNetEnv for environment variable management.

## Contact

If you have any questions or suggestions, please open an issue on GitHub.
