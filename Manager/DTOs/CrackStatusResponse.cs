using Manager.Enums;

namespace Manager.DTOs;

public class CrackStatusResponse
{
    public RequestStatus Status { get; set; }
    public List<string>? Data { get; set; }
}