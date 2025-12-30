using System.Collections.Generic;

namespace Litmus.Models;

public class Category
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<Test> Tests { get; set; } = new List<Test>();
}
