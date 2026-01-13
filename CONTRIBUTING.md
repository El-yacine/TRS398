# Contributing to TRS-398 Pro

Thank you for your interest in contributing to TRS-398 Pro! This document provides guidelines and instructions for contributing.

## 🤝 How to Contribute

### Reporting Bugs

If you find a bug, please open an issue with:
- **Clear title and description**
- **Steps to reproduce**
- **Expected vs actual behavior**
- **Screenshots** (if applicable)
- **Environment details** (OS, .NET version, browser)

### Suggesting Features

We welcome feature suggestions! Please:
- Check [DEVELOPMENT_OPPORTUNITIES.md](docs/DEVELOPMENT_OPPORTUNITIES.md) first
- Open an issue with the `enhancement` label
- Describe the use case and benefits

### Pull Requests

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Make your changes**
4. **Test thoroughly**
5. **Commit with clear messages**: `git commit -m 'Add amazing feature'`
6. **Push to your fork**: `git push origin feature/amazing-feature`
7. **Open a Pull Request**

## 📋 Development Setup

### Prerequisites

- .NET 8.0 SDK
- Git
- Code editor (VS Code, Visual Studio, or Rider)

### Getting Started

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/TRS398.git
cd TRS398

# Add upstream remote
git remote add upstream https://github.com/El-yacine/TRS398.git

# Create a branch
git checkout -b feature/your-feature

# Make changes and test
cd server
dotnet run --project MyQC.WebAPI.csproj --urls http://localhost:8000
```

## 🎯 Areas for Contribution

### High Priority
- 🐛 Bug fixes
- 📝 Documentation improvements
- 🧪 Unit tests
- 🔒 Security enhancements

### Medium Priority
- ✨ New features (see [DEVELOPMENT_OPPORTUNITIES.md](docs/DEVELOPMENT_OPPORTUNITIES.md))
- 🎨 UI/UX improvements
- 🌍 Translations
- 📊 Analytics and charts

### Nice to Have
- 📱 Mobile app
- ☁️ Cloud features
- 🔗 Integrations

## 📝 Code Style

### C# Code
- Use 4-space indentation
- File-scoped namespaces
- PascalCase for public types/properties
- camelCase for locals/parameters
- Prefer explicit types over `var` when it clarifies intent

### JavaScript Code
- Use modern ES6+ syntax
- Follow existing code style
- Add comments for complex logic

### Commit Messages

Use conventional commit format:
- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation
- `style:` Formatting
- `refactor:` Code restructuring
- `test:` Tests
- `chore:` Maintenance

Example:
```
feat: add measurement templates feature

- Add template save/load functionality
- Create template management UI
- Add template API endpoints
```

## 🧪 Testing

Before submitting a PR:
- Test your changes thoroughly
- Test on both Linux and Windows (if applicable)
- Check for console errors
- Verify database operations
- Test edge cases

## 📚 Documentation

When adding features:
- Update relevant README files
- Add code comments
- Update API documentation (if applicable)
- Add examples if needed

## ✅ Checklist

Before submitting a PR:
- [ ] Code follows style guidelines
- [ ] Changes are tested
- [ ] Documentation is updated
- [ ] Commit messages are clear
- [ ] No console errors
- [ ] Works on target platforms

## 🎉 Thank You!

Your contributions make TRS-398 Pro better for everyone. Thank you for taking the time to contribute!

