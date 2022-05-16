using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Domain.SoleToJoint;
using System;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class IncomeApiTenanciesFixture : BaseApiFixture<Tenancy>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:5678/api/v1/tenancies/";
        public static string TheApiToken => "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";

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
        public Tenancy GivenTheTenancyHasAnInactiveNoticeOfSeekingPossession(string tenancyRef)
        {
            ResponseObject = _fixture.Build<Tenancy>()
                            .With(x => x.TenancyRef, tenancyRef)
                            .With(x => x.NOSP, new NoticeOfSeekingPossession { Active = false })
                            .Create();
            return ResponseObject;
        }

        public Tenancy GivenTheTenancyHasAnActiveNoticeOfSeekingPossession(string tenancyRef)
        {
            ResponseObject = _fixture.Build<Tenancy>()
                            .With(x => x.TenancyRef, tenancyRef)
                            .With(x => x.NOSP, new NoticeOfSeekingPossession { Active = true })
                            .Create();
            return ResponseObject;
        }

        public void GivenTheTenancyDoesNotExist()
        {
            ResponseObject = null;
        }
    }
}
