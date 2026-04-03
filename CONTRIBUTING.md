# Contributing to StreamShield

Thanks for your interest in contributing. This guide covers the basics.

## Building Each Platform

### Windows (C# / .NET 10)

```bash
cd src/StreamShield
dotnet restore
dotnet build
```

### Browser Extension (Chrome/Edge)

1. Open your browser and navigate to `chrome://extensions` (or `edge://extensions`).
2. Enable **Developer mode**.
3. Click **Load unpacked** and select the `extension/` directory.

### Linux (Python)

```bash
cd linux/
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

## Pull Request Process

1. Fork the repository.
2. Create a feature branch (`feat/short-description`) or bug fix branch (`fix/short-description`).
3. Make your changes and test them locally.
4. Open a Pull Request against `main`.
5. Describe what you changed and why in the PR description.

## Code Style

Follow the existing patterns in each platform directory:

- **C#**: Standard .NET conventions, nullable reference types enabled.
- **Extension**: Plain JS, no build step. Keep it simple.
- **Python**: Format with ruff. Type hints where practical.

## Reporting Issues

Use [GitHub Issues](../../issues) to report bugs or request features. Include:

- Platform (Windows/Linux/Extension)
- Steps to reproduce
- Expected vs actual behavior
- StreamShield version or commit hash
