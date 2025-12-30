using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Litmus.Services;

public static class TemplateService
{
    /// <summary>
    /// Generates a blank test plan template with documentation and examples.
    /// This template can be filled in manually or fed to an AI for test generation.
    /// </summary>
    public static string GenerateTemplate(bool includeExamples = true)
    {
        Debug.WriteLine("[TemplateService] Generating template...");

        var template = new
        {
            _schema = "https://litmus-testmanager.dev/schema/v1/testplan.json",
            _documentation = new
            {
                description = "Litmus Test Plan Template - Use this template to create test plans for import into Litmus Test Manager.",
                usage = new[]
                {
                    "Fill in the project name and description",
                    "Add categories to group related tests",
                    "Add tests within each category with all required fields",
                    "Save as .json and import via Litmus Import page"
                },
                aiPrompt = "You can provide this template to an AI with the prompt: 'Generate a comprehensive test plan for [your application] using this JSON template format. Include tests for [specific features/areas].'"
            },
            project = new
            {
                name = "[PROJECT_NAME]",
                description = "[Optional description of what this project/application does]"
            },
            categories = includeExamples
                ? GetExampleCategories()
                : GetBlankCategories()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(template, options);
    }

    /// <summary>
    /// Generates a minimal template without examples, just the structure.
    /// </summary>
    public static string GenerateMinimalTemplate()
    {
        Debug.WriteLine("[TemplateService] Generating minimal template...");

        var template = new
        {
            project = new
            {
                name = "",
                description = ""
            },
            categories = new[]
            {
                new
                {
                    name = "",
                    tests = new[]
                    {
                        new
                        {
                            name = "",
                            description = "",
                            command = "",
                            expectedResult = "",
                            prepSteps = (string?)null,
                            priority = "medium"
                        }
                    }
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(template, options);
    }

    /// <summary>
    /// Generates an AI-friendly prompt template that includes the JSON schema.
    /// </summary>
    public static string GenerateAiPromptTemplate(string applicationName = "[YOUR_APP_NAME]", string features = "[FEATURES_TO_TEST]")
    {
        Debug.WriteLine("[TemplateService] Generating AI prompt template...");

        var prompt = $@"# Test Plan Generation Request

Please generate a comprehensive manual test plan for **{applicationName}**.

## Focus Areas
{features}

## Output Format
Generate the test plan as valid JSON using this exact structure:

```json
{{
  ""project"": {{
    ""name"": ""[Project Name]"",
    ""description"": ""[What this application does]""
  }},
  ""categories"": [
    {{
      ""name"": ""[Category Name - e.g., Authentication, Dashboard, API, etc.]"",
      ""tests"": [
        {{
          ""name"": ""[Short test name - what is being tested]"",
          ""description"": ""[Detailed description of the test scenario]"",
          ""command"": ""[Step-by-step instructions to execute the test]"",
          ""expectedResult"": ""[What should happen if the test passes]"",
          ""prepSteps"": ""[Optional: Setup required before running test]"",
          ""priority"": ""[critical|high|medium|low]""
        }}
      ]
    }}
  ]
}}
```

## Priority Guidelines
- **critical**: Core functionality that must work (login, checkout, data integrity)
- **high**: Important features that affect user experience significantly
- **medium**: Standard functionality that should work correctly
- **low**: Edge cases, nice-to-have features, cosmetic issues

## Test Coverage Requirements
1. Include positive tests (happy path scenarios)
2. Include negative tests (error handling, validation)
3. Include boundary tests where applicable
4. Include security-related tests for sensitive operations
5. Group tests logically by feature area or user workflow

## Notes
- Write commands as clear, actionable steps a tester can follow
- Expected results should be specific and verifiable
- Include any test data or accounts needed in prepSteps
";

        return prompt;
    }

    public static void SaveTemplate(string filePath, bool includeExamples = true)
    {
        Debug.WriteLine($"[TemplateService] Saving template to: {filePath}");
        var template = GenerateTemplate(includeExamples);
        File.WriteAllText(filePath, template);
    }

    private static object[] GetExampleCategories()
    {
        return new object[]
        {
            new
            {
                name = "[CATEGORY_1_NAME - e.g., Authentication]",
                tests = new[]
                {
                    new
                    {
                        name = "[Test Name - e.g., Valid Login]",
                        description = "[What this test verifies - e.g., Verify users can log in with valid credentials]",
                        command = "[Steps to execute - e.g., 1. Navigate to /login 2. Enter username 3. Enter password 4. Click Submit]",
                        expectedResult = "[Expected outcome - e.g., User is redirected to dashboard with welcome message]",
                        prepSteps = "[Optional setup - e.g., Ensure test account exists: test@example.com / Pass123]",
                        priority = "critical"
                    },
                    new
                    {
                        name = "[Another Test Name]",
                        description = "[Description]",
                        command = "[Steps]",
                        expectedResult = "[Expected outcome]",
                        prepSteps = "",
                        priority = "high"
                    }
                }
            },
            new
            {
                name = "[CATEGORY_2_NAME - e.g., User Management]",
                tests = new[]
                {
                    new
                    {
                        name = "[Test Name]",
                        description = "[Description]",
                        command = "[Steps]",
                        expectedResult = "[Expected outcome]",
                        prepSteps = "",
                        priority = "medium"
                    }
                }
            }
        };
    }

    private static object[] GetBlankCategories()
    {
        return new object[]
        {
            new
            {
                name = "",
                tests = new[]
                {
                    new
                    {
                        name = "",
                        description = "",
                        command = "",
                        expectedResult = "",
                        prepSteps = "",
                        priority = "medium"
                    }
                }
            }
        };
    }
}
