namespace OrderFlow.Order.Application.Idempotency;

public interface IIdempotencyService
{
    Task<bool> TryAcquireAsync(string key, string requestHash);
    Task<IdempotencyRecord?> GetRecordAsync(string key);
    Task CompleteAsync(string key, int statusCode, string contentType, byte[] responseBody, string location, string requestHash);
    Task RemoveAsync(string key);
}

public record IdempotencyRecord(string State, int StatusCode, string ContentType, byte[] ResponseBody, string Location, string RequestHash);


