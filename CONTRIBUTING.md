# Contributing to BackgroundService Patterns

Thank you for your interest in contributing to BackgroundService Patterns! This repository helps .NET developers implement production-ready background services that avoid common pitfalls.

## How to Contribute

### 🐛 Reporting Issues

Found a problem with the patterns or have a suggestion? Please:

1. **Search existing issues** first to avoid duplicates
2. **Provide clear reproduction steps** if reporting a bug
3. **Include relevant context**: .NET version, hosting environment, etc.
4. **Use descriptive titles** that summarize the issue

### 💡 Suggesting New Patterns

We welcome new pattern suggestions! Before submitting:

1. **Check if the pattern already exists** in the examples
2. **Describe the problem** the pattern solves
3. **Provide a minimal reproduction** of the issue
4. **Explain the solution** and why it's needed
5. **Consider production implications** (performance, monitoring, etc.)

### 🔧 Code Contributions

#### Pattern Guidelines

All patterns should follow these principles:

- **Production-ready**: Code should be suitable for production use
- **Well-documented**: Include XML comments and clear explanations
- **Latest standards**: Use modern .NET patterns and conventions
- **Exception handling**: Implement proper error handling strategies
- **Cancellation support**: Respect `CancellationToken` throughout
- **Scope management**: Avoid captive dependencies
- **Health monitoring**: Include health check capabilities

#### Code Style

- Use file-scoped namespaces (C# 10+)
- Follow standard .NET naming conventions
- Include comprehensive XML documentation
- Use `ConfigureAwait(false)` for library code
- Implement proper resource disposal patterns
- Use high-performance logging (`LoggerMessage.Define`)

#### Submission Process

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b pattern/new-pattern-name`)
3. **Implement your pattern** following the guidelines above
4. **Add comprehensive tests** if applicable
5. **Update documentation** as needed
6. **Submit a pull request** with:
   - Clear description of the problem and solution
   - Examples of the pattern in use
   - Any breaking changes noted
   - Production considerations documented

### 📝 Documentation Improvements

Documentation contributions are always welcome:

- Fix typos or grammatical errors
- Improve code examples
- Add usage scenarios
- Clarify existing explanations
- Translate documentation (future consideration)

## Development Setup

### Prerequisites

- .NET 6 SDK or later (.NET 8 LTS recommended)
- IDE with C# support (Visual Studio, VS Code, Rider)

### Local Development

1. Clone the repository:
   ```bash
   git clone https://github.com/your-username/backgroundservice-patterns.git
   cd backgroundservice-patterns
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run any tests:
   ```bash
   dotnet test
   ```

## Pattern Categories

We organize patterns into these categories:

- **Examples/**: Individual pattern demonstrations
- **Templates/**: Complete, ready-to-use implementations
- **HealthChecks/**: Health monitoring and observability

## Review Process

All contributions go through code review:

1. **Automated checks**: Building, testing, code analysis
2. **Manual review**: Pattern correctness, documentation quality
3. **Community feedback**: Other contributors may provide input
4. **Maintainer approval**: Final approval from repository maintainers

## Community Guidelines

- **Be respectful** and constructive in discussions
- **Focus on the code**, not the person
- **Help others learn** by sharing knowledge
- **Stay on topic** in issues and pull requests
- **Follow the Code of Conduct** (standard open source guidelines)

## Questions?

- **General questions**: Open a GitHub Discussion
- **Bug reports**: Create an issue with the bug template
- **Feature requests**: Create an issue with the feature template
- **Direct contact**: For sensitive matters only

## Recognition

Contributors will be:
- Listed in release notes for significant contributions
- Mentioned in the repository's contributor acknowledgments
- Invited to help maintain the project (for regular contributors)

---

By contributing, you agree that your contributions will be licensed under the same MIT License that covers the project.

Thank you for helping make .NET background services more reliable! 🚀