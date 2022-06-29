using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Shared.Tenure.Boundary.Response;
using ProcessesApi.V1.Domain;
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
    }
}
