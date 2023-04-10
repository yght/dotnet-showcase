using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace MessagePlatform.Infrastructure.Auth;

// machine-to-machine auth is crucial for microservices
public interface IM2MTokenService
{
    Task<string> GetAccessTokenAsync(string audience, string[] scopes);
    Task<bool> ValidateServiceTokenAsync(string token, string expectedAudience);
}

public class M2MTokenService : IM2MTokenService
{
    private readonly HttpClient _httpClient;
    private readonly Auth0Settings _settings;
    private readonly ILogger<M2MTokenService> _logger;
    private readonly Dictionary<string, CachedToken> _tokenCache;

    public M2MTokenService(
        HttpClient httpClient,
        IOptions<Auth0Settings> settings,
        ILogger<M2MTokenService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _tokenCache = new Dictionary<string, CachedToken>();
    }

    public async Task<string> GetAccessTokenAsync(string audience, string[] scopes)
    {
        var cacheKey = $"{audience}:{string.Join(",", scopes)}";
        
        // check cache first
        if (_tokenCache.TryGetValue(cacheKey, out var cachedToken) && 
            cachedToken.ExpiresAt > DateTime.UtcNow.AddMinutes(5)) // 5 min buffer
        {
            return cachedToken.AccessToken;
        }

        try
        {
            var tokenRequest = new
            {
                client_id = _settings.ManagementApiClientId,
                client_secret = _settings.ManagementApiClientSecret,
                audience = audience,
                grant_type = "client_credentials",
                scope = string.Join(" ", scopes)
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"https://{_settings.Domain}/oauth/token", 
                tokenRequest);

            response.EnsureSuccessStatusCode();
            
            var tokenResponse = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<TokenResponse>(tokenResponse);

            if (tokenData?.access_token == null)
            {
                throw new InvalidOperationException("Failed to get access token from Auth0");
            }

            // cache the token
            var newCachedToken = new CachedToken
            {
                AccessToken = tokenData.access_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.expires_in - 60) // 1 min buffer
            };
            
            _tokenCache[cacheKey] = newCachedToken;
            
            _logger.LogDebug("Retrieved M2M token for audience {Audience}", audience);
            return tokenData.access_token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get M2M token for audience {Audience}", audience);
            throw;
        }
    }

    public async Task<bool> ValidateServiceTokenAsync(string token, string expectedAudience)
    {
        try
        {
            // in real implementation, you'd validate JWT signature and claims
            // this is simplified for demo
            
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(token);
            
            var audience = jsonToken.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
            var grantType = jsonToken.Claims.FirstOrDefault(c => c.Type == "gty")?.Value;
            
            var isValid = audience == expectedAudience && 
                         grantType == "client-credentials" &&
                         jsonToken.ValidTo > DateTime.UtcNow;
            
            if (!isValid)
            {
                _logger.LogWarning("Invalid service token - audience: {Audience}, expected: {Expected}", 
                    audience, expectedAudience);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating service token");
            return false;
        }
    }

    private class TokenResponse
    {
        public string? access_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
    }

    private class CachedToken
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}