namespace Manager.DTOs;

public record TaskMessage(
    string RequestId,
    string Hash,
    int MaxLength,
    long StartIndex,
    long Count,
    string Alphabet
);