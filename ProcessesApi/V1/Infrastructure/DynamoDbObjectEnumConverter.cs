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
    public class DynamoDbObjectEnumConverter<TEnum> : IPropertyConverter
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

            return Document.FromJson(JsonSerializer.Serialize(value, CreateJsonOptions()));
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            if ((null == entry) || (null != entry.AsDynamoDBNull())) return null;

            var doc = entry.AsDocument();
            if (null == doc)
                throw new ArgumentException("Field value is not a Document. This attribute has been used on a property that is not a custom object.");
            var first = doc.Values.First();
            TEnum valueAsEnum = (TEnum) Enum.Parse(typeof(TEnum), doc.Values.First());
            var deserialize = JsonSerializer.Deserialize<TEnum>(doc.ToJson());
            return deserialize;
        }
    }
}
