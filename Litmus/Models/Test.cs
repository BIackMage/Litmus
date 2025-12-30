using System.Collections.Generic;

namespace Litmus.Models;

public class Test
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string PrepSteps { get; set; } = string.Empty;
    public Priority Priority { get; set; } = Priority.Medium;
    public int SortOrder { get; set; }

    // Navigation properties
    public Category Category { get; set; } = null!;
    public ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();
}
