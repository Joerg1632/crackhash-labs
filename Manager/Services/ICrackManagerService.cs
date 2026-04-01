using Manager.DTOs;
using Manager.Enums;

namespace Manager.Services;

public interface ICrackManagerService
{
    string StartCrack(CrackRequest request);
    void ReportResult(string requestId, List<string> words, int workerId);
    (RequestStatus Status, List<string>? Data) GetStatus(string requestId);
    void CheckTimeouts();
}