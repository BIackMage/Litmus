# Changelog

All notable changes to Litmus Test Manager will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2025-01-04

### Added
- **Blocked Status** - New test status for tests that cannot be executed due to external dependencies
  - Orange color (#CC7000) across all UI
  - `B` keyboard shortcut in execution mode
  - Included in reports, PDF exports, and diff comparisons
- **Screenshot Capture** - Capture and attach screenshots during test execution
  - `Ctrl+Shift+S` keyboard shortcut
  - Full-screen capture automatically saved as attachment
- **Drag & Drop Reordering** - Reorder tests within categories by dragging rows
- **Enhanced Diff View** - Side-by-side comparison panel showing:
  - Pass/Fail/Blocked counts for both runs
  - Pass rate percentages
  - Project and build version labels
- **IsAutomated Field** - Mark tests as having automated coverage
  - Filter test runs by Automated Only, Manual Only, or All Tests
- **Category Filtering** - Filter tests by category when creating test runs
- **Move to End Button** - Skip a test and move it to the end of the queue
- **Edit Test Button** - Quick edit button in test lists

### Changed
- Improved test run creation page with filter dropdowns
- Enhanced reporting pages to include Blocked status in charts and statistics
- PDF exports now include Blocked status in summary section

### Fixed
- App icon now displays correctly in taskbar and window title
- Fixed crash when clicking "New Test Run" before page fully loaded
- Fixed ComboBox styling in dark theme

## [1.0.0] - 2025-01-03

### Added
- Initial release
- Project, Category, and Test management
- Test execution mode with keyboard shortcuts (P, F, arrows)
- Test run tracking with build versions
- Reports page with pie charts and trend analysis
- PDF export functionality
- JSON import/export for test plans
- Diff view for comparing test runs
- Global search across all projects
- File attachments for test results
- Quick fail templates
- Dark theme UI
- SQLite local database storage
