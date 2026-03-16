using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Filters;

public class SwaggerDefaultValueFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        foreach (var property in context.Type.GetProperties())
        {
            var defaultValueAttribute = property.GetCustomAttributes(typeof(DefaultValueAttribute), false).FirstOrDefault() as DefaultValueAttribute;
            if (defaultValueAttribute != null)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(defaultValueAttribute.Value);
                var openApiAny = OpenApiAnyFactory.CreateFromJson(json);
                if (schema.Properties.ContainsKey(property.Name))
                {
                    schema.Properties[property.Name].Default = openApiAny;
                }
            }
        }
    }
}
