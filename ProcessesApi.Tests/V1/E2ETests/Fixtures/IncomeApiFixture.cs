using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Domain.Finance;
using System;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class IncomeApiFixture : BaseApiFixture<Tenancy>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:5678/api/v1/";
        public static string TheApiToken => "sdjkhfgsdkjfgsdjfgh";

        public IncomeApiFixture()
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

        public Tenancy GivenTheTenancyExists(Guid id)
        {
            ResponseObject = _fixture.Build<Tenancy>()
                                      .With(x => x.TenancyRef, id.ToString())
                                      .Create();
            return ResponseObject;
        }

        public void GivenTheTenancyDoesNotExist(Guid id)
        {
            // Nothing to do here
        }
    }
}
