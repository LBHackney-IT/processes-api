using System;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain.Finance;

namespace ProcessesApi.V1.Gateways
{
    public interface IIncomeApiGateway
    {
        public Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId);
        public Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId);
    }
}
