using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
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
        public readonly IDynamoDBContext _dbContext;

        public Person Person { get; private set; }

        public PersonFixture(IDynamoDBContext context)
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
                if (Person != null)
                    _dbContext.DeleteAsync<PersonDbEntity>(Person.Id).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        public async Task AndGivenAPersonExists(Guid id)
        {
            var person = _fixture.Build<Person>()
                        .With(x => x.Id, id)
                        .With(x => x.Tenures, new List<TenureDetails>())
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dbContext.SaveAsync<PersonDbEntity>(person.ToDatabase()).ConfigureAwait(false);

            Person = person;
        }

        public void AndGivenAPersonDoesNotExist()
        {
        }

    }
}
