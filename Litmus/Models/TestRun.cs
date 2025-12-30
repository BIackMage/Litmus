using System;
using System.Collections.Generic;

namespace Litmus.Models;

public class TestRun
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int MajorVersion { get; set; }
    public int MinorVersion { get; set; }
    public int PatchVersion { get; set; }
    public string BuildVersion => $"{MajorVersion}.{MinorVersion}.{PatchVersion}";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;

    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();
}
