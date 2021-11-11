using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.DynamoDb.Converters;
using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Infrastructure
{
    [DynamoDBTable("Processes", LowerCamelCaseProperties = true)]
    public class ProcessesDb
    {
        [DynamoDBHashKey]
        public string Id { get; set; }
        
        [DynamoDBRangeKey]
        public string TargetId { get; set; }
        
        [DynamoDBProperty]
        public string ProcessName { get; set; }
        
        [DynamoDBProperty(Converter = typeof(DynamoDbObjectListConverter<String>))]
        public List<String> RelatedEntities { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectConverter<ProcessState>))]
        public ProcessState CurrentState { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectListConverter<ProcessState>))]
        public List<ProcessState> PreviousStates { get; set; }
    }
}
