using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Order.Api;
using OrderFlow.Order.Application.Orders.Commands.CreateOrder;
using OrderFlow.Order.Infrastructure.Data;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace OrderFlow.IntegrationTests;

[Collection("Integration")]
public class IdempotencyIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine").Build();
    
    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings:OrderDb", _sqlContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings:Redis", _redisContainer.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings:RabbitMq", "amqp://guest:guest@localhost:5672/");

        _factory = new WebApplicationFactory<Program>();

        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // We do not want MassTransit attempting to connect to RabbitMQ in this specific HTTP test.
                // We'll leave it as is; connection failures might just log errors if Outbox works.
            });
        }).CreateClient();

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetService<OrderDbContext>();
        if (db != null)
        {
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        Environment.SetEnvironmentVariable("ConnectionStrings:OrderDb", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:Redis", null);
        Environment.SetEnvironmentVariable("ConnectionStrings:RabbitMq", null);

        await Task.WhenAll(_sqlContainer.DisposeAsync().AsTask(), _redisContainer.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task PostOrder_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 1, 10m)]);
        var response = await _client.PostAsJsonAsync("/api/orders", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_WithIdempotencyKey_ReturnsCreated()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 1, 10m)]);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(command)
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostOrder_DuplicateKey_SamePayload_ReturnsSameResponse()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 1, 10m)]);
        var idempotencyKey = Guid.NewGuid().ToString();

        // First request
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        var body1 = await response1.Content.ReadAsStringAsync();

        // Second request
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode);
        var body2 = await response2.Content.ReadAsStringAsync();

        Assert.Equal(body1, body2);
    }

    [Fact]
    public async Task PostOrder_DuplicateKey_DifferentPayload_ReturnsConflict()
    {
        var idempotencyKey = Guid.NewGuid().ToString();

        // First request
        var command1 = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 1, 10m)]);
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command1) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        await _client.SendAsync(request1);

        // Second request with DIFFERENT payload
        var command2 = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 2, 20m)]);
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command2) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(request2);
        
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task PostOrder_ConcurrentRequests_ReturnsConflictForSecond()
    {
        var command = new CreateOrderCommand(Guid.NewGuid(), [new CreateOrderLine(Guid.NewGuid(), 1, 10m)]);
        var idempotencyKey = Guid.NewGuid().ToString();

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(command) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);

        var task1 = _client.SendAsync(request1);
        var task2 = _client.SendAsync(request2);

        var responses = await Task.WhenAll(task1, task2);

        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);
    }
}
