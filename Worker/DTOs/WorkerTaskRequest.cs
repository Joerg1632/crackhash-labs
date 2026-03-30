namespace Worker.DTOs;

public record WorkerTaskRequest(
    string Hash, 
    int MaxLength, 
    long StartIndex, 
    long Count,
    string RequestId, 
    int WorkerId,
    string Alphabet
);