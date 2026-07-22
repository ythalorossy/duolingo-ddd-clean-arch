using System.Net;
using System.Net.Http.Json;
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
}
