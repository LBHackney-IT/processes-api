using AutoFixture;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Shared.Tenure.Boundary.Response;
using Hackney.Shared.Tenure.Domain;
using System;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class TenureApiFixture : BaseApiFixture<TenureResponseObject>
    {
        private readonly Fixture _fixture = new Fixture();
        public static string TheApiRoute => "http://localhost:9012/api/v1/tenures/";
        public static string TheApiToken => "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";

        public TenureApiFixture()
            : base(TheApiRoute, TheApiToken)
        {
            // These config values will be needed by the code under test.
            Environment.SetEnvironmentVariable("TenureApiUrl", TheApiRoute);
            Environment.SetEnvironmentVariable("TenureApiToken", TheApiToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                base.Dispose(disposing);
            }
        }

        public void GivenTheTenureApiReturns204(Guid id)
        {
            Responses.Add(id.ToString(), new TenureResponseObject());
        }

        public void GivenTheTenureApiReturns201()
        {
            var tenure = _fixture.Create<TenureResponseObject>();
            Responses.Add("tenures", tenure);
        }

        public void GivenTheTenureApiReturns204ForUpdatingHouseholdMembers(TenureInformation tenure)
        {
            foreach (var householdMember in tenure.HouseholdMembers)
            {
                Responses.Add(householdMember.Id.ToString(), new TenureResponseObject());
            }
        }
    }
}
