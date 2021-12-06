using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.DynamoDb.Converters;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Infrastructure
{
    [DynamoDBTable("Processes", LowerCamelCaseProperties = true)]
    public class ProcessesDb
    {
        public ProcessesDb()
        { }
        [DynamoDBHashKey]
        public Guid Id { get; set; }

        [DynamoDBProperty]
        public Guid TargetId { get; set; }

        [DynamoDBProperty]
        public string ProcessName { get; set; }

        [DynamoDBProperty]
        public List<Guid> RelatedEntities { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectConverter<ProcessState<string, string>>))]
        public ProcessState<string, string> CurrentState { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectListConverter<ProcessState<string, string>>))]
        public List<ProcessState<string, string>> PreviousStates { get; set; }

        [DynamoDBVersion]
        public int? VersionNumber { get; set; }
    }
}
