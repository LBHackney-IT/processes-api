using System;
using System.Threading.Tasks;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.V1.Gateways
{
    public interface ISoleToJointGateway
    {
        public Task<TenureInformation> GetTenureById(Guid id);
        public Task<Person> GetPersonById(Guid id);
        public Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId);
        public Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId);
    }
}
