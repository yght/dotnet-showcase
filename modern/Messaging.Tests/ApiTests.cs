using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Messaging;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Messaging.Tests;

public sealed class TestApi : WebApplicationFactory<Program>
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"messages-{Guid.NewGuid():N}.db");
    private readonly SymmetricSecurityKey key = new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Messages"] = $"Data Source={path};Pooling=False;Default Timeout=10" }));
        builder.ConfigureServices(services => services.PostConfigure<JwtBearerOptions>("Bearer", options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                ValidateIssuer = true, ValidIssuer = "integration-tests",
                ValidateAudience = true, ValidAudience = "messaging-tests",
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        }));
    }
    public void DropMessageTable()
    {
        using var db = new SqliteConnection($"Data Source={path};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "DROP TABLE messages";
        command.ExecuteNonQuery();
    }

    public HttpClient User(string user = "alice", string? tenant = "tenant-a", bool expired = false)
    {
        var client = CreateClient();
        var claims = new List<Claim> { new("sub", user) };
        if (tenant != null) claims.Add(new Claim("tenant_id", tenant));
        var token = new JwtSecurityToken("integration-tests", "messaging-tests", claims,
            DateTime.UtcNow.AddHours(-1), expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
    }
}

public sealed class ApiTests
{
    private static SendMessage Input(string id = "client-1", string body = "Hello") => new("bob", body, id);

    [Fact]
    public async Task Readiness_detects_storage_failure_while_liveness_stays_available()
    {
        using var api = new TestApi();
        using var client = api.CreateClient();
        var healthy = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.True(healthy.Headers.CacheControl!.NoStore);
        api.DropMessageTable();
        var failed = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var body = await failed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("no such table", body);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public void Readiness_does_not_create_a_missing_database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.db");
        var store = new MessageStore($"Data Source={path};Pooling=False", TimeProvider.System);
        Assert.Throws<SqliteException>(() => store.CheckReadiness());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Rejects_missing_and_expired_tokens()
    {
        using var api = new TestApi();
        using var anonymous = api.CreateClient();
        using var expired = api.User(expired: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/messages/", Input())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await expired.PostAsJsonAsync("/api/messages/", Input())).StatusCode);
    }

    [Fact]
    public async Task Requires_tenant_claim_even_for_a_valid_token()
    {
        using var api = new TestApi();
        using var client = api.User(tenant: null);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/messages/", Input())).StatusCode);
    }

    [Fact]
    public async Task Concurrent_retries_create_one_message()
    {
        using var api = new TestApi();
        using var client = api.User();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => client.PostAsJsonAsync("/api/messages/", Input())));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode));
        var results = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<SendResult>()));
        Assert.Single(results.Select(r => r!.Message.Id).Distinct());
        var page = await client.GetFromJsonAsync<MessagePage>("/api/messages/conversations/bob");
        Assert.Single(page!.Messages);
    }

    [Theory]
    [InlineData("Changed", "bob")]
    [InlineData("Hello", "charlie")]
    public async Task Reusing_a_key_for_different_payload_returns_conflict(string body, string recipient)
    {
        using var api = new TestApi();
        using var client = api.User();
        await client.PostAsJsonAsync("/api/messages/", Input());
        var response = await client.PostAsJsonAsync("/api/messages/", new SendMessage(recipient, body, "client-1"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_and_participant_boundaries_are_enforced()
    {
        using var api = new TestApi();
        using var alice = api.User();
        using var otherTenant = api.User(tenant: "tenant-b");
        using var stranger = api.User("eve");
        using var bob = api.User("bob");
        await alice.PostAsJsonAsync("/api/messages/", Input());
        Assert.Empty((await otherTenant.GetFromJsonAsync<MessagePage>("/api/messages/conversations/bob"))!.Messages);
        Assert.Empty((await stranger.GetFromJsonAsync<MessagePage>("/api/messages/conversations/bob"))!.Messages);
        Assert.Single((await bob.GetFromJsonAsync<MessagePage>("/api/messages/conversations/alice"))!.Messages);
        Assert.Equal(HttpStatusCode.Created, (await otherTenant.PostAsJsonAsync("/api/messages/", Input())).StatusCode);
    }

    [Fact]
    public async Task Cursor_pages_do_not_repeat_messages_when_new_messages_arrive()
    {
        using var api = new TestApi();
        using var client = api.User();
        await client.PostAsJsonAsync("/api/messages/", Input("1"));
        await client.PostAsJsonAsync("/api/messages/", Input("2"));
        var first = (await client.GetFromJsonAsync<MessagePage>("/api/messages/conversations/bob?limit=1"))!;
        await client.PostAsJsonAsync("/api/messages/", Input("3"));
        var next = (await client.GetFromJsonAsync<MessagePage>($"/api/messages/conversations/bob?after={first.NextCursor}"))!;
        Assert.Equal(2, next.Messages.Count);
        Assert.DoesNotContain(next.Messages, m => m.Id == first.Messages[0].Id);
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=101")]
    [InlineData("?after=-1")]
    public async Task Rejects_invalid_pagination(string query)
    {
        using var api = new TestApi();
        using var client = api.User();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/messages/conversations/bob" + query)).StatusCode);
    }

    [Fact]
    public async Task Rejects_blank_content()
    {
        using var api = new TestApi();
        using var client = api.User();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/messages/", Input(body: " "))).StatusCode);
    }

    [Fact]
    public void Receipt_survives_a_new_store_instance()
    {
        var path = Path.Combine(Path.GetTempPath(), $"restart-{Guid.NewGuid():N}.db");
        try
        {
            var connection = $"Data Source={path};Pooling=False";
            var first = new MessageStore(connection, TimeProvider.System);
            first.Initialize();
            var created = first.Send("tenant", "alice", Input());
            var restarted = new MessageStore(connection, TimeProvider.System);
            var replay = restarted.Send("tenant", "alice", Input());
            Assert.True(replay.Replayed);
            Assert.Equal(created.Message.Id, replay.Message.Id);
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }
}
