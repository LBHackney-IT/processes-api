using System;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain.Finance;
using Tenancy = ProcessesApi.V1.Domain.Finance.Tenancy;

namespace ProcessesApi.V2.Gateways
{
    public interface IIncomeApiGateway
    {
        public Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId);
        public Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId);
    }
}
