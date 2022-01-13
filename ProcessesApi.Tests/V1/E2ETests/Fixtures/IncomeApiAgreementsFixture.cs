using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Domain.Finance;
using System;
using System.Collections.Generic;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class IncomeApiAgreementsFixture : BaseApiFixture<PaymentAgreements>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:5678/api/v1/agreements/";
        public static string TheApiToken => "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";

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

        public PaymentAgreements AndGivenAPaymentAgreementDoesNotExistForTenancy(Guid tenancyId)
        {
            ResponseObject = new PaymentAgreements
            {
                Agreements = new List<PaymentAgreement>()
            };
            return ResponseObject;
        }

        public PaymentAgreements AndGivenAPaymentAgreementExistsForTenancy(Guid tenancyId)
        {
            ResponseObject = new PaymentAgreements
            {
                Agreements = new List<PaymentAgreement>
                {
                    _fixture.Build<PaymentAgreement>()
                            .With(x => x.TenancyRef, tenancyId.ToString())
                            .With(x => x.Amount, 10)
                            .Create()
                }
            };

            return ResponseObject;
        }
    }
}
