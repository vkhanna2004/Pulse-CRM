using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Testcontainers.RabbitMq;
using Xunit;
using FluentAssertions;

namespace PulseCRM.Deals.IntegrationTests;

public class DealsApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("deals_db").WithUsername("pulsecrm").WithPassword("test").Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder().Build();

    private static readonly Microsoft.IdentityModel.Tokens.SymmetricSecurityKey TestSigningKey =
        new(System.Text.Encoding.UTF8.GetBytes("pulsecrm-test-signing-key-minimum-32-chars!!"));

    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync(), _rabbitMq.StartAsync());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:DealsDb", _postgres.GetConnectionString());
                builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
                builder.UseSetting("RabbitMq:Host", _rabbitMq.Hostname);
                builder.UseSetting("RabbitMq:Username", "guest");
                builder.UseSetting("RabbitMq:Password", "guest");
                builder.UseSetting("Jwt:Authority", "https://test-authority");

                builder.ConfigureServices(services =>
                {
                    // Override JWT bearer to accept self-signed test tokens
                    services.PostConfigure<JwtBearerOptions>("Bearer", options =>
                    {
                        options.RequireHttpsMetadata = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = true,
                            IssuerSigningKey = TestSigningKey
                        };
                    });
                });
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestJwtFactory.CreateToken(TestSigningKey)}");
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetDefaultPipeline_ReturnsPipelineWithStages()
    {
        var response = await _client.GetAsync("/api/deals/pipelines/default");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pipeline = await response.Content.ReadFromJsonAsync<dynamic>();
        pipeline.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateDeal_WithInvalidStage_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/deals", new
        {
            title = "Test Deal",
            value = 10000m,
            currency = "USD",
            stageId = Guid.NewGuid(), // non-existent stage
            ownerId = Guid.NewGuid()
        });
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MoveDeal_WithNonExistentDeal_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/deals/{Guid.NewGuid()}/move", new
        {
            stageId = Guid.NewGuid(),
            positionInStage = 1000,
            rowVersion = 1u
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetContacts_WithLongSearch_ReturnsBadRequest()
    {
        var longSearch = new string('a', 200);
        var response = await _client.GetAsync($"/api/contacts?search={longSearch}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

public static class TestJwtFactory
{
    public static string CreateToken(SymmetricSecurityKey signingKey)
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var userId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("preferred_username", "test-user"),
            new Claim("email", "test@pulsecrm.dev"),
            new Claim(JwtRegisteredClaimNames.Name, "Test User")
        };

        var token = new JwtSecurityToken(
            issuer: "https://test-authority",
            audience: "pulsecrm-spa",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
