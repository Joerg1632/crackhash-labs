namespace Worker.DTOs;

public class ReportDto
{
    public string RequestId { get; set; } = string.Empty;
    public List<string>? FoundWords { get; set; }
}