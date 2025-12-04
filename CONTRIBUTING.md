# Contributing to NFury

First off, thank you for considering contributing to NFury! It's people like you that make this project such a great tool.

## Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues as you might find out that you don't need to create one. When you are creating a bug report, please include as many details as possible:

- **Use a clear and descriptive title** for the issue to identify the problem.
- **Describe the exact steps which reproduce the problem** in as many details as possible.
- **Provide specific examples to demonstrate the steps**.
- **Describe the behavior you observed after following the steps** and point out what exactly is the problem with that behavior.
- **Explain which behavior you expected to see instead and why.**
- **Include the version of NFury you're using.**
- **Include your .NET version** (`dotnet --version`).

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

- **Use a clear and descriptive title** for the issue to identify the suggestion.
- **Provide a step-by-step description of the suggested enhancement** in as many details as possible.
- **Provide specific examples to demonstrate the steps** or point out the part of the project where the suggestion is related.
- **Describe the current behavior** and **explain which behavior you expected to see instead** and why.
- **Explain why this enhancement would be useful** to most users.

### Pull Requests

1. Fork the repo and create your branch from `main`.
2. If you've added code that should be tested, add tests.
3. If you've changed APIs, update the documentation.
4. Ensure the test suite passes (`dotnet test`).
5. Make sure your code follows the existing code style (use the `.editorconfig`).
6. Issue that pull request!

## Development Setup

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- An IDE (Visual Studio, VS Code, or JetBrains Rider)

### Building the Project

```bash
# Clone the repository
git clone https://github.com/unhackeddev/nfury.git
cd nfury

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run the application
cd src/NFury
dotnet run -- server
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Coding Guidelines

### C# Coding Style

- Follow the [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
- Use file-scoped namespaces
- Use expression-bodied members where appropriate
- Prefer pattern matching
- Use nullable reference types

### Naming Conventions

- Use `PascalCase` for public members
- Use `_camelCase` for private fields
- Use `s_camelCase` for private static fields
- Prefix interfaces with `I`
- Suffix async methods with `Async`

### Documentation

- Add XML documentation comments to all public APIs
- Keep comments up to date with code changes
- Write clear and concise commit messages

## Project Structure

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
├── tests/                      # Test projects
├── README.md                   # Project documentation
├── LICENSE                     # MIT License
├── CONTRIBUTING.md             # Contribution guidelines
├── CODE_OF_CONDUCT.md          # Code of conduct
├── SECURITY.md                 # Security policy
└── CHANGELOG.md                # Version history
```

## Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters or less
- Reference issues and pull requests liberally after the first line

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

Types:

- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `perf`: A code change that improves performance
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools

## Release Process

1. Update the version in `Directory.Build.props`
2. Update `CHANGELOG.md`
3. Create a pull request with the version bump
4. After merging, create a GitHub release
5. GitHub Actions will automatically build and publish

## Questions?

Feel free to open an issue with your question or reach out to the maintainers.

Thank you for your contribution! 🎉
