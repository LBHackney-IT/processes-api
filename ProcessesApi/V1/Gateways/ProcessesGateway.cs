using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Hackney.Core.DynamoDb;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.UseCase.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProcessesApi.V1.Infrastructure;
using Hackney.Shared.Processes.Infrastructure;
using ProcessesDb = ProcessesApi.V1.Infrastructure.ProcessesDb;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.V1.Gateways
{
    public class ProcessesGateway : IProcessesGateway
    {
        private const int MAX_RESULTS = 10;
        private const string GETPROCESSESBYTARGETIDINDEX = "ProcessesByTargetId";
        private const string TARGETID = "targetId";

        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly IEntityUpdater _updater;
        private readonly ILogger<ProcessesGateway> _logger;

        public ProcessesGateway(IDynamoDBContext dynamoDbContext, IEntityUpdater updater, ILogger<ProcessesGateway> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _updater = updater;
            _logger = logger;
        }

        [LogCall]
        public async Task<Process> GetProcessById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<ProcessesDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<Process> SaveProcess(Process query)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for id {query.Id}");
            var processDbEntity = query.ToDatabase();

            await _dynamoDbContext.SaveAsync(processDbEntity).ConfigureAwait(false);
            return processDbEntity.ToDomain();
        }

        [LogCall]
        public async Task<UpdateEntityResult<ProcessState>> UpdateProcessById(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {query.Id}");

            var currentProcess = await _dynamoDbContext.LoadAsync<ProcessesDb>(query.Id).ConfigureAwait(false);
            if (currentProcess == null) return null;


            if (ifMatch != currentProcess.VersionNumber)
                throw new VersionNumberConflictException(ifMatch, currentProcess.VersionNumber);

            var updatedResult = _updater.UpdateEntity(currentProcess.CurrentState, requestBody, requestObject);
            if (updatedResult.NewValues.Any())
            {
                _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync to update id {query.Id}");
                updatedResult.UpdatedEntity.UpdatedAt = DateTime.UtcNow;
                currentProcess.CurrentState = updatedResult.UpdatedEntity;
                await _dynamoDbContext.SaveAsync(currentProcess).ConfigureAwait(false);
            }
            return updatedResult;
        }

        [LogCall]
        public async Task<PagedResult<Process>> GetProcessesByTargetId(GetProcessesByTargetIdRequest request)
        {
            int pageSize = request.PageSize.HasValue ? request.PageSize.Value : MAX_RESULTS;
            var dbProcesses = new List<ProcessesDb>();
            var table = _dynamoDbContext.GetTargetTable<ProcessesDb>();

            var queryConfig = new QueryOperationConfig
            {
                IndexName = GETPROCESSESBYTARGETIDINDEX,
                Limit = pageSize,
                PaginationToken = PaginationDetails.DecodeToken(request.PaginationToken),
                Filter = new QueryFilter(TARGETID, QueryOperator.Equal, request.TargetId)
            };

            var search = table.Query(queryConfig);
            _logger.LogDebug($"Querying {queryConfig.IndexName} index for targetId {request.TargetId}");
            var resultsSet = await search.GetNextSetAsync().ConfigureAwait(false);

            var paginationToken = search.PaginationToken;
            if (resultsSet.Any())
            {
                dbProcesses.AddRange(_dynamoDbContext.FromDocuments<ProcessesDb>(resultsSet));

                // Look ahead for any more, but only if we have a token
                if (!string.IsNullOrEmpty(PaginationDetails.EncodeToken(paginationToken)))
                {
                    queryConfig.PaginationToken = paginationToken;
                    queryConfig.Limit = 1;
                    search = table.Query(queryConfig);
                    resultsSet = await search.GetNextSetAsync().ConfigureAwait(false);
                    if (!resultsSet.Any())
                        paginationToken = null;
                }
            }

            return new PagedResult<Process>(dbProcesses.Select(x => x.ToDomain()),
                                            new PaginationDetails(paginationToken));
        }


    }
}
