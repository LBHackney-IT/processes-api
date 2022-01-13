using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Testing.DynamoDb;
using AutoFixture;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person.Infrastructure;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class PersonFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;

        public Person Person { get; private set; }

        public PersonFixture(IDynamoDbFixture dbFixture)
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
                if (Person != null)
                    _dbFixture.DynamoDbContext.DeleteAsync<PersonDbEntity>(Person.Id);
                _disposed = true;
            }
        }

        public async Task AndGivenAPersonExistsWithTenures(Guid personId, List<Guid> tenureIds)
        {
            var personTenureDetails = new List<TenureDetails>();

            tenureIds.ForEach(id =>
            {
                personTenureDetails.Add(_fixture.Build<TenureDetails>()
                                        .With(x => x.Id, id)
                                        .With(x => x.EndDate, DateTime.Now.AddDays(10).ToString())
                                        .Create());
            });

            var person = _fixture.Build<Person>()
                        .With(x => x.Id, personId)
                        .With(x => x.Tenures, personTenureDetails)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dbFixture.SaveEntityAsync<PersonDbEntity>(person.ToDatabase()).ConfigureAwait(false);

            Person = person;
        }

        public void AndGivenAPersonDoesNotExist()
        {
        }

    }
}
