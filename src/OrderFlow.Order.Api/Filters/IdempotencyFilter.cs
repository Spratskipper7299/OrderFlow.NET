using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrderFlow.Order.Application.Idempotency;
using StackExchange.Redis;

namespace OrderFlow.Order.Api.Filters;

public class IdempotencyFilter : IAsyncResourceFilter
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(IIdempotencyService idempotencyService, ILogger<IdempotencyFilter> logger)
    {
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        // 1. Check if Idempotency-Key header exists
        if (!context.HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            context.Result = new BadRequestObjectResult(new { message = "Idempotency-Key header is required." });
            return;
        }

        var idempotencyKey = headerValue.ToString();
        var fullKey = $"idempotency:order:create:{idempotencyKey}";

        // 2. Generate Request Hash (Raw Bytes)
        var requestHash = await GenerateRequestHashAsync(context.HttpContext.Request);

        try
        {
            // 3. Try to acquire lock / set InProgress
            var acquired = await _idempotencyService.TryAcquireAsync(fullKey, requestHash);

            if (!acquired)
            {
                // Key exists, check state
                var existingRecord = await _idempotencyService.GetRecordAsync(fullKey);
                if (existingRecord == null)
                {
                    // Expired between TryAcquire and GetRecord
                    context.Result = new ObjectResult(new { message = "Idempotency state conflict, please retry." }) { StatusCode = StatusCodes.Status409Conflict };
                    return;
                }

                if (existingRecord.RequestHash != requestHash)
                {
                    context.Result = new ObjectResult(new { message = "Idempotency-Key is already in use for a different payload." }) { StatusCode = StatusCodes.Status409Conflict };
                    return;
                }

                if (existingRecord.State == "InProgress")
                {
                    context.Result = new ObjectResult(new { message = "A request with this Idempotency-Key is currently being processed." }) { StatusCode = StatusCodes.Status409Conflict };
                    return;
                }

                if (existingRecord.State == "Completed")
                {
                    var result = new ContentResult
                    {
                        StatusCode = existingRecord.StatusCode,
                        ContentType = existingRecord.ContentType,
                        Content = Encoding.UTF8.GetString(existingRecord.ResponseBody)
                    };
                    
                    if (!string.IsNullOrEmpty(existingRecord.Location))
                    {
                        context.HttpContext.Response.Headers.Location = existingRecord.Location;
                    }
                    
                    context.Result = result;
                    return;
                }
            }

            // 4. Proceed to Action, capturing response
            var originalBodyStream = context.HttpContext.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.HttpContext.Response.Body = responseBodyStream;

            var executedContext = await next();

            // 5. Action complete, update Redis or clean up
            if (executedContext.Exception != null || context.HttpContext.Response.StatusCode >= 400)
            {
                // Action failed with exception or 4xx/5xx, delete key to allow retry
                try
                {
                    await _idempotencyService.RemoveAsync(fullKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove InProgress idempotency key after action failure.");
                }
            }
            else
            {
                // Success - capture response
                var statusCode = context.HttpContext.Response.StatusCode;
                var contentType = context.HttpContext.Response.ContentType ?? "application/json";
                var location = context.HttpContext.Response.Headers.Location.ToString();
                
                var responseBytes = responseBodyStream.ToArray();

                try
                {
                    await _idempotencyService.CompleteAsync(fullKey, statusCode, contentType, responseBytes, location, requestHash);
                }
                catch (Exception ex)
                {
                    // IMPORTANT: SQL transaction has already committed. We must return the success result.
                    _logger.LogError(ex, "Failed to mark idempotency key as Completed. The business operation succeeded.");
                }
            }

            // Copy captured response to the original stream so the client gets it
            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalBodyStream);
            context.HttpContext.Response.Body = originalBodyStream;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection failed during idempotency check.");
            context.Result = new ObjectResult(new { message = "Service temporarily unavailable due to idempotency store failure." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }
    }

    private static async Task<string> GenerateRequestHashAsync(HttpRequest request)
    {
        request.EnableBuffering();
        using var streamReader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var requestBody = await streamReader.ReadToEndAsync();
        request.Body.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
        return Convert.ToBase64String(hashBytes);
    }
}
