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
using System.Linq;
using Hackney.Shared.Tenure.Domain;

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

        public async Task GivenAnAdultPersonExists(Guid personId)
        {
            var person = _fixture.Build<Person>()
                        .With(x => x.Id, personId)
                        .With(x => x.Tenures, new List<TenureDetails>())
                        .With(x => x.DateOfBirth, DateTime.UtcNow.AddYears(-20))
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dbContext.SaveAsync<PersonDbEntity>(person.ToDatabase()).ConfigureAwait(false);

            Person = person;
        }

        public void GivenAPersonDoesNotExist()
        {
        }

        public async Task GivenAPersonHasAnActiveTenure(Guid tenureId)
        {
            var tenures = Person.Tenures.Append(_fixture.Build<TenureDetails>()
                                                        .With(x => x.Id, tenureId)
                                                        .With(x => x.EndDate, DateTime.UtcNow.AddYears(10).ToString())
                                                        .With(x => x.Type, TenureTypes.Secure.Code)
                                                        .Create());
            Person.Tenures = tenures;
            Person.VersionNumber = 0;

            await _dbContext.SaveAsync<PersonDbEntity>(Person.ToDatabase()).ConfigureAwait(false);
        }

    }
}
