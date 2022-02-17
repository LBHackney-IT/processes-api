using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class TenureFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        public TenureInformation Tenure { get; private set; }
        public string tenancyRef { get; private set; }

        public TenureFixture(IDynamoDbFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (Tenure != null)
                    _dbFixture.DynamoDbContext.DeleteAsync<TenureInformationDb>(Tenure.Id).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        private async Task GivenATenureExists(Guid tenureId, Guid tenantId, bool isTenant, bool isSecure)
        {
            var tenancyRef = _fixture.Create<String>();

            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.Id, tenureId)
                        .With(x => x.HouseholdMembers,
                                new List<HouseholdMembers> {
                                    _fixture.Build<HouseholdMembers>()
                                    .With(x => x.Id, tenantId)
                                    .With(x => x.PersonTenureType, isTenant? PersonTenureType.Tenant : PersonTenureType.HouseholdMember)
                                    .With(x => x.DateOfBirth, DateTime.Now.AddYears(-18))
                                    .Create()
                                })
                        .With(x => x.TenureType, isSecure ? TenureTypes.Secure : TenureTypes.NonSecure)
                        .With(x => x.EndOfTenureDate, (DateTime?) null)
                        .With(x => x.LegacyReferences,
                                    new List<LegacyReference> {
                                        new LegacyReference
                                        {
                                            Name = "uh_tag_ref",
                                            Value = tenancyRef
                                        }
                                    })
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();

            await _dbFixture.SaveEntityAsync<TenureInformationDb>(tenure.ToDatabase()).ConfigureAwait(false);
            Tenure = tenure;
        }

        public async Task AndGivenASecureTenureExists(Guid tenureId, Guid tenantId, bool isTenant)
        {
            await GivenATenureExists(tenureId, tenantId, isTenant: isTenant, isSecure: true).ConfigureAwait(false);
        }

        public async Task AndGivenANonSecureTenureExists(Guid tenureId, Guid tenantId, bool isTenant)
        {
            await GivenATenureExists(tenureId, tenantId, isTenant: isTenant, isSecure: false).ConfigureAwait(false);
        }

        public void AndGivenATenureDoesNotExist()
        {
        }

    }
}
