namespace CentralNuGetUpdater.Models;

public class ProjectInfo
{
    public string ProjectPath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public List<string> TargetFrameworks { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class SolutionInfo
{
    public string SolutionPath { get; set; } = string.Empty;
    public List<ProjectInfo> Projects { get; set; } = new();
    public HashSet<string> AllTargetFrameworks => Projects.SelectMany(p => p.TargetFrameworks).ToHashSet();
}