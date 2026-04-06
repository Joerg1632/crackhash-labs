using Manager.Enums;
namespace Manager.DTOs;

public record CrackStatusResponse(RequestStatus Status, List<string>? Data);