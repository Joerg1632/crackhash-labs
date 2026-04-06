namespace Manager;

public class CrackManagerSettings
{
    public string[] WorkerUrls { get; set; } = [];
    public int WorkerTimeoutSeconds { get; set; }
    public int TotalTimeoutSeconds { get; set; }
    public string Alphabet { get; set; } = "";
    
    public int TaskParts { get; set; }
}