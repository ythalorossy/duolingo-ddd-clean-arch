using System.Net;
using System.Text.Json;
using Xunit;

namespace Learning.Integration.Tests.EndToEnd;

public class OpenApiAndCorsTests(OpenApiApiFactory factory) : IClassFixture<OpenApiApiFactory>
{
    [Fact]
    public async Task OpenApi_document_is_served_and_describes_the_catalog()
    {
        var json = await factory.CreateClient().GetStringAsync("/openapi/v1.json");

        using var doc = JsonDocument.Parse(json);
        var schemas = doc.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.True(schemas.TryGetProperty("CatalogDto", out _),
            "OpenAPI components must include the CatalogDto schema so the frontend can generate its types.");
    }

    [Fact]
    public async Task Cross_origin_get_from_the_spa_origin_echoes_allow_origin_header()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:4200");

        // Probe the DB-free OpenAPI endpoint; the "spa" policy is applied app-wide,
        // so the CORS header appears on any endpoint's response without needing the DB.
        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"),
            "A cross-origin GET from the SPA dev origin must echo an Access-Control-Allow-Origin header.");
        Assert.Contains("http://localhost:4200",
            response.Headers.GetValues("Access-Control-Allow-Origin"));
    }
}
