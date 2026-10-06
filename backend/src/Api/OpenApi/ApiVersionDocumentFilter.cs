using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LifeFitAI.Api.OpenApi;

public sealed class ApiVersionDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var versionedPaths = new OpenApiPaths();

        foreach (var path in swaggerDoc.Paths)
        {
            versionedPaths.Add(
                path.Key.Replace("{version}", "1", StringComparison.Ordinal),
                path.Value);
        }

        swaggerDoc.Paths = versionedPaths;
    }
}
