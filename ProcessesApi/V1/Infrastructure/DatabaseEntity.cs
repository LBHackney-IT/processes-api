using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.DynamoDb.Converters;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProcessesApi.V1.Infrastructure
{
    //TODO: rename table and add needed fields relating to the table columns.
    // There's an example of this in the wiki https://github.com/LBHackney-IT/lbh-processes-api/wiki/DatabaseContext

    [DynamoDBTable("Processes", LowerCamelCaseProperties = true)]
    public class DatabaseEntity
    {
        [DynamoDBHashKey]
        public int Id { get; set; }

        [DynamoDBRangeKey]
        public int TargetId { get; set; }


        [DynamoDBProperty(Converter = typeof(DynamoDbDateTimeConverter))]
        public DateTime CreatedAt { get; set; }
    }
}
