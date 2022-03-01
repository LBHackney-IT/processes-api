using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoFixture;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person.Infrastructure;
using ProcessesApi.V1.Factories;
using Amazon.DynamoDBv2.DataModel;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class PersonFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        public readonly IDynamoDBContext _dbContext;

        public Person Person { get; private set; }

        public PersonFixture(IDynamoDBContext dbContext)
        {
            _dbContext = dbContext;
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
                    _dbContext.DeleteAsync<PersonDbEntity>(Person.Id);
                _disposed = true;
            }
        }

        public async Task GivenAPersonExistsWithTenures(Guid personId, List<Guid> tenureIds)
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
            await _dbContext.SaveAsync<PersonDbEntity>(person.ToDatabase()).ConfigureAwait(false);

            Person = person;
        }

        public void GivenAPersonDoesNotExist()
        {
        }

    }
}
