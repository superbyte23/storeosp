using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using StoreOps.Application.Contracts.Auth;
using StoreOps.Application.Contracts.Products;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Auth;
using StoreOps.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Xunit;

namespace StoreOps.Api.Tests;

public sealed class AuthFlowTests
{
    [Fact]
    public async Task Login_protects_api_rotates_and_revokes_refresh_tokens()
    {
        using var factory = new StoreOpsApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/products")).StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@storeops.test", "Test-Only-Password-42!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var initial = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(initial);
        Assert.Equal(900, initial.ExpiresIn);
        Assert.NotEqual(initial.AccessToken, initial.RefreshToken);
        new JwtSecurityTokenHandler().ValidateToken(initial.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("test-signing-key-that-is-at-least-32-bytes-long")),
                ValidateIssuer = true,
                ValidIssuer = "StoreOps.Tests",
                ValidateAudience = true,
                ValidAudience = "StoreOps.Tests.Client",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            },
            out _);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreOpsDbContext>();
            var storedHash = await db.RefreshTokens.Select(token => token.TokenHash).SingleAsync();
            Assert.NotEqual(initial.RefreshToken, storedHash);
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", initial.AccessToken);
        var productsResponse = await client.GetAsync("/api/v1/products");
        Assert.True(productsResponse.StatusCode == HttpStatusCode.OK,
            $"Expected authenticated products response, received {(int)productsResponse.StatusCode} " +
            $"with challenge '{string.Join(",", productsResponse.Headers.WwwAuthenticate)}'.");
        client.DefaultRequestHeaders.Authorization = null;

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(initial.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(initial.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var revokedReplacementResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, revokedReplacementResponse.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreOpsDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
            db.Users.Add(new User
            {
                Email = "viewer@storeops.test",
                NormalizedEmail = "VIEWER@STOREOPS.TEST",
                PasswordHash = passwordHasher.Hash("Viewer-Only-Password-42!"),
                Roles = { new UserRole { Role = "Viewer" } }
            });
            await db.SaveChangesAsync();
        }

        var viewerLoginResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("viewer@storeops.test", "Viewer-Only-Password-42!"));
        var viewerTokens = await viewerLoginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(viewerTokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", viewerTokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/orders")).StatusCode);
    }

    [Fact]
    public async Task Invalid_credentials_do_not_issue_tokens()
    {
        using var factory = new StoreOpsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@storeops.test", "incorrect-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Product_create_and_detail_return_category_name()
    {
        using var factory = new StoreOpsApiFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@storeops.test", "Test-Only-Password-42!"));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        Guid categoryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StoreOpsDbContext>();
            categoryId = await db.Categories.Select(category => category.Id).FirstAsync();
        }

        var createResponse = await client.PostAsJsonAsync("/api/v1/products",
            new SaveProductRequest(categoryId, "TEST-PRODUCT-001", "Test product",
                "Product creation regression test.", 12.50m, 4));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(created);
        Assert.Equal("Test product", created.Name);
        Assert.False(string.IsNullOrWhiteSpace(created.CategoryName));

        var detailResponse = await client.GetAsync($"/api/v1/products/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(detail);
        Assert.Equal(created.CategoryName, detail.CategoryName);
    }

    private sealed class StoreOpsApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private const string TestAdminPassword = "Test-Only-Password-42!";

        public StoreOpsApiFactory() => _connection.Open();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:StoreOps"] = "Data Source=:memory:",
                    ["Jwt:Issuer"] = "StoreOps.Tests",
                    ["Jwt:Audience"] = "StoreOps.Tests.Client",
                    ["Jwt:SigningKey"] = "test-signing-key-that-is-at-least-32-bytes-long",
                    ["BootstrapAdmin:Email"] = "admin@storeops.test",
                    ["BootstrapAdmin:Password"] = TestAdminPassword,
                    ["SpaOrigin"] = "http://localhost:5173"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<StoreOpsDbContext>>();
                services.AddDbContext<StoreOpsDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _connection.Dispose();
        }
    }
}
