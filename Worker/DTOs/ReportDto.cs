namespace Worker.DTOs;

public record ReportDto (string RequestId, List<string>? FoundWords);