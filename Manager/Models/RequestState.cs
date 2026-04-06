using Manager.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Manager.Models;

public class RequestState
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string RequestId { get; set; }
    public string Hash { get; set; }
    public int MaxLength { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.IN_PROGRESS;
    [BsonElement("FoundWords")]
    public List<string> FoundWords { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    
    public int CompletedTasks { get; set; }
}