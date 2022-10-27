using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using System;

namespace ProcessesApi.V1.Infrastructure
{
    public class DynamoDbEnumConverter<TEnum> : IPropertyConverter where TEnum : Enum
    {
        public DynamoDBEntry ToEntry(object value)
        {
            if (null == value) return new DynamoDBNull();

            return new Primitive(Enum.GetName(typeof(TEnum), value));
        }

        public object FromEntry(DynamoDBEntry entry)
        {
            Primitive primitive = entry as Primitive;
            var entryStringValue = primitive?.AsString();
            if (string.IsNullOrEmpty(entryStringValue)) return default(TEnum);

            TEnum valueAsEnum = (TEnum) Enum.Parse(typeof(TEnum), entryStringValue);
            return valueAsEnum;
        }
    }
}
