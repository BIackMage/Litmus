using System;
using System.Collections.Generic;

namespace Litmus.Models;

public class TestResult
{
    public int Id { get; set; }
    public int TestRunId { get; set; }
    public int TestId { get; set; }
    public TestStatus Status { get; set; } = TestStatus.NotRun;
    public string Notes { get; set; } = string.Empty;
    public DateTime? ExecutedDate { get; set; }

    // Navigation properties
    public TestRun TestRun { get; set; } = null!;
    public Test Test { get; set; } = null!;
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
