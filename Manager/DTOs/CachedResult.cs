namespace Manager.DTOs;

public record CachedResult(
    string Hash,
    int MaxLength,
    List<string> FoundWords,
    DateTime CreatedAt
);