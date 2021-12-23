using System;
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

        public async Task GivenATenureExists(Guid id)
        {
            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.Id, id)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dbContext.SaveAsync<TenureInformationDb>(tenure.ToDatabase()).ConfigureAwait(false);

            Tenure = tenure;
        }

    }
}
