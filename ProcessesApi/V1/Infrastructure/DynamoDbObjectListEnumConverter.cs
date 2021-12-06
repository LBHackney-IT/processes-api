using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Infrastructure
{
    public class DynamoDbObjectListEnumConverter<TEnum> : IPropertyConverter
    {
        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        public DynamoDBEntry ToEntry(object value)
        {
            if (null == value) return new DynamoDBNull();

            var list = value as IEnumerable<TEnum>;
            if (null == list)
                throw new ArgumentException($"Field value is not a list of {typeof(TEnum).Name}. This attribute has been used on a property that is not a list of custom objects.");

            return new DynamoDBList(list.Select(x => Document.FromJson(JsonSerializer.Serialize(x, CreateJsonOptions()))));
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            if ((null == entry) || (null != entry.AsDynamoDBNull())) return null;

            var list = entry.AsDynamoDBList();
            if (null == list)
                throw new ArgumentException("Field value is not a DynamoDBList. This attribute has been used on a property that is not a list of custom objects.");

            return list.AsListOfDocument().Select(x => JsonSerializer.Deserialize<TEnum>(x.ToJson(), CreateJsonOptions())).ToList();
        }
    }
}
