using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class TenureFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        public readonly IDynamoDBContext _dbContext;

        public TenureInformation Tenure { get; private set; }

        public TenureFixture(IDynamoDBContext context)
        {
            _dbContext = context;
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
                    _dbContext.DeleteAsync<TenureInformationDb>(Tenure.Id).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        private async Task GivenATenureExists(Guid tenureId, Guid tenantId, bool isTenant, bool isSecure)
        {
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
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();

            await _dbContext.SaveAsync<TenureInformationDb>(tenure.ToDatabase()).ConfigureAwait(false);

            Tenure = tenure;
        }

        public async Task AndGivenATenureExists(Guid tenureId, Guid tenantId, bool isTenant)
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