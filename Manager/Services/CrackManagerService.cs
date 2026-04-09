using System.Text;
using System.Text.Json;
using Manager.Core.Tasks;
using Manager.DTOs;
using Manager.Enums;
using Manager.Infrastructure.Mongo;
using Manager.Infrastructure.RabbitMQ;
using Manager.Models;
using Microsoft.Extensions.Options;

namespace Manager.Services;

public class CrackManagerService : ICrackManagerService
{
    private readonly ILogger<CrackManagerService> logger;
    private readonly MongoRequestRepository repository;
    private readonly RabbitMqPublisher publisher;
    private readonly TaskSplitter taskSplitter;
    private readonly CrackManagerSettings settings;

    public CrackManagerService(
        ILogger<CrackManagerService> logger,
        IOptions<CrackManagerSettings> options, 
        TaskSplitter taskSplitter, 
        MongoRequestRepository repository,
        RabbitMqPublisher publisher)
    {
        this.logger = logger;
        this.taskSplitter = taskSplitter;
        this.repository = repository;
        this.publisher = publisher;
        settings = options.Value;
    }

    public async Task<string> StartCrackAsync(CrackRequest request)
    {
        var existing = await repository.FindByHashAndLengthAsync(request.Hash, request.MaxLength);
        if (existing != null)
        {
            logger.LogInformation(
                "Cache hit for hash request. Returning existing requestId {RequestId}",
                existing.RequestId);
            
            return existing.RequestId;
        }

        var requestId = Guid.NewGuid().ToString();
        var state = new RequestState
        {
            RequestId = requestId,
            Hash = request.Hash,
            MaxLength = request.MaxLength,
            Status = RequestStatus.IN_PROGRESS,
            CreatedAt = DateTime.UtcNow
        };

        await repository.InsertAsync(state);
        logger.LogInformation("Request {RequestId}: saved to MongoDB", requestId);
        await PublishTasksAsync(state);
        
        return requestId;
    }

    public async Task PublishTasksAsync(RequestState state)
    {
        var total = taskSplitter.CalculateTotalCombinations(
            settings.Alphabet.Length,
            state.MaxLength);
        
        var tasks = taskSplitter.GetBalancedRanges(total, settings.TaskParts)
            .Select(r => new TaskMessage(
                state.RequestId,
                state.Hash,
                state.MaxLength,
                r.Start,
                r.Count,
                settings.Alphabet))
            .ToList();

        await publisher.PublishTaskAsync(tasks);
        
        logger.LogInformation(
            "Request {RequestId}: published {Count} tasks to RabbitMQ", 
            state.RequestId,
            tasks.Count);
    }

    public (RequestStatus Status, List<string>? Data) GetStatus(string requestId)
    {
        var state = repository.GetAsync(requestId).GetAwaiter().GetResult();
        
        return state == null ? (RequestStatus.ERROR, null) : (state.Status, state.FoundWords);
    }
    
    public async Task ReportResultAsync(string requestId, List<string> words)
    {
        var state = await repository.GetAsync(requestId);
        if (state == null || state.Status != RequestStatus.IN_PROGRESS)
            return;

        await repository.UpdateAsync(requestId, words ?? []);

        state = await repository.GetAsync(requestId);
    
        if (state!.CompletedTasks >= settings.TaskParts)
        {
            var uniqueWords = state.FoundWords.Distinct().ToList();
            await repository.SetStatusAsync(requestId, RequestStatus.READY, uniqueWords);
            logger.LogInformation("Request {RequestId}: READY, found {Count} words",
                requestId, state.FoundWords.Count);
        }
    }
    
    public async Task RecoverInProgressRequestsAsync()
    {
        var inProgress = await repository.FindInProgressAsync();
    
        foreach (var state in inProgress)
        {
            logger.LogInformation(
                "Request {RequestId}: recovering, republishing tasks",
                state.RequestId);
            
            await PublishTasksAsync(state);
        }
    }
}