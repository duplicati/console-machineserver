// Copyright (c) 2025 Duplicati Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
// of the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MachineService.Server.Utility;

/// <summary>
/// Filter to manually add types to Swagger docs/json.
/// </summary>
internal class ExportTypeFilter : IDocumentFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var typesToExport = new List<Type>
        {
            typeof(EnvelopedMessage),
            typeof(AuthMessage),
            typeof(AuthResultMessage),
            typeof(MessageTypes),
            typeof(WelcomeMessage)
        };

        foreach (var model in typesToExport)
        {
            var schema = context.SchemaGenerator.GenerateSchema(model, context.SchemaRepository);
            if (!swaggerDoc.Components.Schemas.ContainsKey(model.Name))
                swaggerDoc.Components.Schemas.Add(model.Name, schema);
        }
    }
}