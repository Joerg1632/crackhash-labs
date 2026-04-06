using Manager.Enums;
using Manager.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Manager.Infrastructure.Mongo;

public class MongoRequestRepository
{
    private readonly IMongoCollection<RequestState> requestCollection;

    public MongoRequestRepository(IOptions<MongoSettings> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.DatabaseName);
        requestCollection = db.GetCollection<RequestState>("requests");
    }

    public async Task InsertAsync(RequestState state)
    {
        await requestCollection.InsertOneAsync(state);
    }

    public async Task UpdateAsync(
        string requestId,
        List<string> foundWords)
    {
        var update = Builders<RequestState>.Update
            .Inc(x => x.CompletedTasks, 1)
            .PushEach(x => x.FoundWords, foundWords);
        
        await requestCollection.UpdateOneAsync(x => x.RequestId == requestId, update);
    }

    public async Task<RequestState?> GetAsync(string requestId)
    {
        return await requestCollection
            .Find(x => x.RequestId == requestId)
            .FirstOrDefaultAsync();
    }
    
    public async Task SetStatusAsync(string requestId, RequestStatus status, List<string>? foundWords)
    {
        var update = Builders<RequestState>.Update.Set(x => x.Status, status);
        if (foundWords != null)
            update = update.Set(x => x.FoundWords, foundWords);
        
        await requestCollection.UpdateOneAsync(x => x.RequestId == requestId, update);
    }

    public async Task<RequestState?> FindByHashAndLengthAsync(string hash, int maxLength)
    {
        return await requestCollection
            .Find(x => x.Hash == hash && x.MaxLength == maxLength)
            .FirstOrDefaultAsync();
    }

    public async Task<List<RequestState>> FindInProgressAsync()
    {
        return await requestCollection
            .Find(x => x.Status == RequestStatus.IN_PROGRESS)
            .ToListAsync();
    }
}