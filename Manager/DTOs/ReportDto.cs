namespace Manager.DTOs;

public class ReportDto
{
    public string RequestId { get; set; } = string.Empty;
    public List<string>? FoundWords { get; set; }
    public int WorkerId { get; set; }
}