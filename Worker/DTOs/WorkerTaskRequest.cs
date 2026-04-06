namespace Worker.DTOs;

public record WorkerTaskRequest(
    string RequestId, 
    string Hash, 
    int MaxLength, 
    long StartIndex, 
    long Count,
    string Alphabet
);