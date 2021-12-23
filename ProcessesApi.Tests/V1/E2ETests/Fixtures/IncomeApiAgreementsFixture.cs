using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Domain.Finance;
using System;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class IncomeApiAgreementsFixture : BaseApiFixture<PaymentAgreement>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:5000/api/v1/agreements/";
        public static string TheApiToken => "abcdefghijklmnopqrstuvwxyz";

        public IncomeApiAgreementsFixture()
            : base(TheApiRoute, TheApiToken)
        {
            // These config values will be needed by the code under test.
            Environment.SetEnvironmentVariable("IncomeApiUrl", TheApiRoute);
            Environment.SetEnvironmentVariable("IncomeApiToken", TheApiToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                base.Dispose(disposing);
            }
        }

        public void AndGivenAPaymentAgreementDoesNotExistForTenancy(Guid tenancyId)
        {
        }

        public PaymentAgreement AndGivenAPaymentAgreementExistsForTenancy(Guid tenancyId)
        {
            var requests = Responses;
            return _fixture.Build<PaymentAgreement>()
                            .With(x => x.TenancyRef, tenancyId.ToString())
                            .With(x => x.Amount, 0)
                            .Create();
        }
    }
}
