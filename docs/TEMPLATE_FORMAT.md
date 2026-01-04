# Litmus Test Plan Template Format

This document describes the JSON format used by Litmus Test Manager for importing and exporting test plans.

## Quick Start

1. Go to **Export** in Litmus
2. Click **Download Template** to get a blank template
3. Fill in the template manually OR feed it to an AI (see below)
4. Import via the **Import** page

## JSON Structure

```json
{
  "project": {
    "name": "My Application Tests",
    "description": "Optional project description"
  },
  "categories": [
    {
      "name": "Category Name",
      "tests": [
        {
          "name": "Test Name",
          "description": "What this test verifies",
          "command": "Steps to execute the test",
          "expectedResult": "What should happen if passing",
          "prepSteps": "Optional setup instructions",
          "priority": "critical|high|medium|low",
          "isAutomated": false
        }
      ]
    }
  ]
}
```

## Field Descriptions

### Project
| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | The project name (e.g., "MyApp v2.0 Tests") |
| `description` | No | What this project tests, relevant context |

### Category
| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Category name to group related tests (e.g., "Authentication", "API", "UI") |
| `tests` | Yes | Array of test objects |

### Test
| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Short, descriptive test name |
| `description` | No | Detailed description of what's being tested |
| `command` | Yes | Step-by-step instructions to execute the test |
| `expectedResult` | Yes | What the tester should observe if the test passes |
| `prepSteps` | No | Setup required before running (test data, accounts, environment) |
| `priority` | No | `critical`, `high`, `medium` (default), or `low` |
| `isAutomated` | No | `true` if this test has automated coverage, `false` (default) if manual only |

## Priority Guidelines

- **critical** - Core functionality that must work (login, checkout, data saving)
- **high** - Important features affecting user experience
- **medium** - Standard functionality (default)
- **low** - Edge cases, cosmetic issues, nice-to-have features

## Using AI to Generate Test Plans

You can use Claude, ChatGPT, or other AI assistants to generate test plans. Here's a prompt template:

```
Generate a comprehensive manual test plan for [YOUR APPLICATION NAME].

Focus on these areas:
- [Feature 1]
- [Feature 2]
- [etc.]

Output as JSON using this exact format:

{
  "project": {
    "name": "[Project Name]",
    "description": "[Description]"
  },
  "categories": [
    {
      "name": "[Category]",
      "tests": [
        {
          "name": "[Test Name]",
          "description": "[Description]",
          "command": "[Step-by-step instructions]",
          "expectedResult": "[Expected outcome]",
          "prepSteps": "[Setup if needed, or null]",
          "priority": "critical|high|medium|low",
          "isAutomated": false
        }
      ]
    }
  ]
}

Include:
1. Positive tests (happy path)
2. Negative tests (error handling)
3. Boundary tests
4. Security tests for sensitive operations

Use clear, actionable steps that a manual tester can follow.
```

## Example

See `sample-testplan.json` in the repository root for a complete example.

## Import Options

When importing:
- **Overwrite existing**: If a project with the same name exists, replace it
- **Merge categories**: Add new categories/tests to existing project instead of replacing
