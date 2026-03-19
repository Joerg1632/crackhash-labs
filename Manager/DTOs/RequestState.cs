namespace Manager.DTOs;

public class RequestState
{
    public string Status { get; set; } = "IN_PROGRESS";
    public List<string> FoundWords { get; set; } = new();
    public int PendingWorkers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<int, WorkerState> Workers { get; set; } = new();
}