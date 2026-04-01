using Manager.DTOs;
using Manager.Enums;
namespace Manager.Models;
using System.Collections.Concurrent;

public class RequestState
{
    public string requestId { get; set; }
    public string Hash { get; set; } = string.Empty;
    public int MaxLength { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.IN_PROGRESS; 
    public List<string> FoundWords { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool[] WorkerAlive { get; set; } = Array.Empty<bool>();
    public ConcurrentQueue<RangeTask> PendingTasks { get; set; } = new();
    public ConcurrentDictionary<int, RangeTask> InProgress { get; set; } = new();
}