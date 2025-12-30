# Litmus Test Manager

A modern Windows desktop application for managing manual software testing. Track test plans, execute tests, and generate reports with a sleek dark-themed UI.

![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/Platform-Windows-blue)
![License](https://img.shields.io/badge/License-PolyForm%20Noncommercial-blue)

## Features

- **Project Organization** - Organize tests by Project → Category → Test hierarchy
- **Test Execution Mode** - Step through tests with keyboard shortcuts (P=Pass, F=Fail, arrows to navigate)
- **Historical Tracking** - Track results across build versions (major.minor.patch)
- **Reports & Analytics** - Pie charts, pass rate trends, failed test lists
- **PDF Export** - Generate professional test reports
- **JSON Import/Export** - Version control your test plans or generate them with AI
- **Diff View** - Compare test runs to identify regressions and fixes
- **Global Search** - Find tests across all projects
- **Attachments** - Add screenshots to test results
- **Quick Fail Templates** - Common failure reasons at your fingertips

## Screenshots

*Coming soon*

## Installation

### Prerequisites
- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### From Source
```bash
git clone https://github.com/BIackMage/Litmus.git
cd Litmus
dotnet build
dotnet run --project Litmus/Litmus.csproj
```

### Release Build
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Usage

### Creating a Test Plan

1. Click **New Project** on the Dashboard
2. Add categories to group related tests
3. Add tests with:
   - Name and description
   - Command/steps to execute
   - Expected result
   - Prep steps (optional)
   - Priority level

### Importing Test Plans

Litmus supports JSON import for test plans. You can:
- **Download a template** from the Export page
- **Generate tests with AI** - Feed the template to Claude/ChatGPT with your app details
- **Import** via the Import page

See [docs/TEMPLATE_FORMAT.md](docs/TEMPLATE_FORMAT.md) for the JSON schema.

### Running Tests

1. Go to **Test Runs** → **New Test Run**
2. Select project and build version
3. Choose run type:
   - **Full Run** - All tests start as "Not Run"
   - **Copy Previous** - Start with previous run's results
   - **Retest Failed** - Only include failed tests
4. Use **Execution Mode** with keyboard shortcuts:
   - `P` - Mark as Pass
   - `F` - Mark as Fail
   - `←` `→` - Navigate between tests
   - `Ctrl+S` - Save notes

### Generating Reports

- View reports on the **Reports** page
- Filter by project
- Export to PDF for sharing

## Data Storage

Litmus stores data locally in:
- **Database**: `%LOCALAPPDATA%\Litmus\litmus.db` (SQLite)
- **Attachments**: `%LOCALAPPDATA%\Litmus\Attachments\`

## Tech Stack

- **Framework**: .NET 8, WPF
- **Database**: SQLite with Entity Framework Core
- **Charts**: LiveCharts2
- **PDF Generation**: QuestPDF
- **UI**: Custom dark theme

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the **PolyForm Noncommercial License 1.0.0** - free for personal, educational, and noncommercial use. See the [LICENSE](LICENSE) file for details.

**You CAN:**
- Use for personal projects, learning, research
- Use in educational institutions
- Modify and share the code
- Use in nonprofit/government organizations

**You CANNOT:**
- Sell this software
- Use for commercial purposes

## Acknowledgments

- Built with [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- PDF generation by [QuestPDF](https://www.questpdf.com/)
