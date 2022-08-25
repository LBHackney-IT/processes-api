using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.DynamoDb.Converters;
using ProcessesApi.V2.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V2.Infrastructure
{
    [DynamoDBTable("Processes", LowerCamelCaseProperties = true)]
    public class ProcessesDb
    {

        [DynamoDBHashKey]
        [DynamoDBGlobalSecondaryIndexRangeKey("ProcessesByTargetId")]
        public Guid Id { get; set; }

        [DynamoDBProperty]
        [DynamoDBGlobalSecondaryIndexHashKey("ProcessesByTargetId")]
        public Guid TargetId { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbEnumConverter<TargetType>))]
        public TargetType TargetType { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbEnumConverter<ProcessName>))]
        public ProcessName ProcessName { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectListConverter<RelatedEntity>))]
        public List<RelatedEntity> RelatedEntities { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectConverter<PatchAssignmentEntity>))]
        public PatchAssignmentEntity PatchAssignmentEntity { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectConverter<ProcessState>))]
        public ProcessState CurrentState { get; set; }

        [DynamoDBProperty(Converter = typeof(DynamoDbObjectListConverter<ProcessState>))]
        public List<ProcessState> PreviousStates { get; set; }

        [DynamoDBVersion]
        public int? VersionNumber { get; set; }
    }
}
