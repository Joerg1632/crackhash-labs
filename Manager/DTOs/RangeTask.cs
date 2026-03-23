namespace Manager.DTOs;

public class RangeTask
{
    public long Start { get; set; }
    public long Count { get; set; }
    public bool Completed { get; set; }
    public int? WorkerId { get; set; }
    public DateTime? StartedAt { get; set; }
}