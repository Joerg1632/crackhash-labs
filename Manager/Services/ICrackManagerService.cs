using Manager.DTOs;
using Manager.Enums;
using Manager.Models;

namespace Manager.Services;

public interface ICrackManagerService
{
    Task<string> StartCrackAsync(CrackRequest request);
    Task PublishTasksAsync(RequestState state);
    Task ReportResultAsync(string requestId, List<string> words);
    (RequestStatus Status, List<string>? Data) GetStatus(string requestId);
    Task RecoverInProgressRequestsAsync();
}