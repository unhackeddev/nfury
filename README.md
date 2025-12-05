# NFury - API Load Testing Tool

<p align="center">
  <img src="assets/logo.svg" alt="NFury Logo" width="200" height="200">
</p>

<p align="center">
  🔥 <strong>Fast and Efficient API Load Testing Tool</strong>
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License"></a>
  <a href="https://github.com/unhackeddev/nfury/actions"><img src="https://github.com/unhackeddev/nfury/workflows/CI/badge.svg" alt="CI"></a>
</p>

NFury is an open-source tool developed to efficiently and quickly perform load testing on APIs. With NFury, you can simulate multiple virtual users making requests to a target API and analyze important metrics such as average response time, status code distribution, and percentiles.

## ✨ Features

-   **Virtual User Simulation**: Create a defined number of virtual users to simulate traffic on your API
-   **Flexible Configuration**: Specify the API URL, HTTP method, and the number of requests to be made
-   **Detailed Analysis**: Obtain detailed metrics such as average response time, status code distribution, and percentiles
-   **Interactive Interface**: Track test progress in real-time with an interactive command-line interface
-   **Web Dashboard**: Real-time monitoring via web interface with SignalR
-   **Test History**: Store and compare test results with SQLite database
-   **Project Management**: Organize multiple test configurations

## 🚀 Quick Start

### Installation

```bash
# Clone the repository
git clone https://github.com/unhackeddev/nfury.git
cd nfury

# Build the project
dotnet build -c Release

# Run the application
cd src/NFury
dotnet run -- --help
```

### Basic Usage

```bash
# Start the web server
./nfury server

# Access the dashboard at http://localhost:5000
```

## 📊 Web Dashboard

Access the real-time dashboard at `http://localhost:5000` to:

-   Create and manage load test projects
-   Configure test parameters (URL, method, headers, body)
-   Monitor test execution in real-time
-   View detailed metrics and percentiles
-   Compare historical test results

## 📁 Project Structure

```
├── src/
│   └── NFury/
│       ├── Commands/           # CLI commands (run, server)
│       │   ├── Run/            # Load test runner command
│       │   └── Server/         # Web server command
│       ├── Web/                # Web application components
│       │   ├── Data/           # Database access
│       │   ├── Hubs/           # SignalR hubs
│       │   └── Services/       # Business logic services
│       ├── wwwroot/            # Static web assets
│       │   ├── css/            # Stylesheets
│       │   └── js/             # JavaScript files
│       ├── Program.cs          # Application entry point
│       └── NFury.csproj        # Project file
├── README.md                   # Project documentation
├── LICENSE                     # MIT License
├── CONTRIBUTING.md             # Contribution guidelines
├── CODE_OF_CONDUCT.md          # Code of conduct
├── SECURITY.md                 # Security policy
└── CHANGELOG.md                # Version history
```

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔒 Security

For security concerns, please see our [Security Policy](SECURITY.md).

## 🙏 Acknowledgments

-   Built with [.NET](https://dotnet.microsoft.com/)
-   CLI powered by [Spectre.Console](https://spectreconsole.net/)
-   Real-time updates with [SignalR](https://docs.microsoft.com/aspnet/signalr)

---

Made with ❤️ by [Unhacked](https://github.com/unhackeddev)

[GitHub](https://github.com/unhackeddev/nfury) | [Documentation](https://github.com/unhackeddev/nfury/wiki) | [Report an Issue](https://github.com/unhackeddev/nfury/issues)
