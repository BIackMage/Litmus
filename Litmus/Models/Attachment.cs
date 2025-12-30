using System;

namespace Litmus.Models;

public class Attachment
{
    public int Id { get; set; }
    public int TestResultId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public TestResult TestResult { get; set; } = null!;
}
