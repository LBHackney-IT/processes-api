using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Domain.Finance;
using System;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class IncomeApiTenanciesFixture : BaseApiFixture<Tenancy>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:5000/api/v1/tenancies/";
        public static string TheApiToken => "abcdefghijklmnopqrstuvwxyz";

        public IncomeApiTenanciesFixture()
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
        public Tenancy AndGivenTheTenancyHasAnInactiveNoticeOfSeekingPossession(Guid tenancyId)
        {
            return _fixture.Build<Tenancy>()
                            .With(x => x.TenancyRef, tenancyId.ToString())
                            .With(x => x.nosp, new NoticeOfSeekingPossession { active = false })
                            .Create();
        }

        public Tenancy AndGivenTheTenancyHasAnActiveNoticeOfSeekingPossession(Guid tenancyId)
        {
            return _fixture.Build<Tenancy>()
                            .With(x => x.TenancyRef, tenancyId.ToString())
                            .With(x => x.nosp, new NoticeOfSeekingPossession { active = true })
                            .Create();
        }

        public void AndGivenTheTenancyDoesNotExist()
        {
        }
    }
}
