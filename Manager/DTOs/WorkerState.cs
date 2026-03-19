namespace Manager.DTOs;

public class WorkerState
{
    public bool Completed { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}