namespace Manager.DTOs;

public record WorkerTaskDto(
    string RequestId,
    string Hash,
    int MaxLength,
    long StartIndex,
    long Count,
    int WorkerId,
    string Alphabet
);