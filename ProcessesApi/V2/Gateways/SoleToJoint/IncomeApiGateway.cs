using System;
using System.Threading.Tasks;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using Hackney.Core.Http;
using ProcessesApi.V1.Domain.Finance;

namespace ProcessesApi.V2.Gateways
{
    public class IncomeApiGateway : IIncomeApiGateway
    {
        private readonly ILogger<IncomeApiGateway> _logger;
        private const string ApiName = "Income";
        private const string IncomeApiUrl = "IncomeApiUrl";
        private const string IncomeApiToken = "IncomeApiToken";
        private readonly IApiGateway _apiGateway;

        public IncomeApiGateway(ILogger<IncomeApiGateway> logger, IApiGateway apiGateway)
        {
            _logger = logger;
            _apiGateway = apiGateway;
            _apiGateway.Initialise(ApiName, IncomeApiUrl, IncomeApiToken, null, useApiKey: true);
        }

        [LogCall]
        public async Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API for payment agreement with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/agreements/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<PaymentAgreements>(route, tenancyRef, correlationId);
        }

        [LogCall]
        public async Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/tenancies/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<Tenancy>(route, tenancyRef, correlationId);
        }
    }
}
