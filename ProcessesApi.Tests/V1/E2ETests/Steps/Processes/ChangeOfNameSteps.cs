using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Infrastructure;
using System.Net.Http;
using System.Threading.Tasks;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using ChangeOfNameKeys = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNameKeys;

namespace ProcessesApi.Tests.V1.E2ETests.Steps
{
    public class ChangeOfNameSteps : UpdateProcessBaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public ChangeOfNameSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient, dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentsRequestedAppointment(UpdateProcessQuery request, string initialState)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentsRequestedAppointment, initialState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToUpdateName(UpdateProcessQuery request, string initialState)
        {
            await CheckProcessState(request.Id, ChangeOfNameStates.NameUpdated, initialState).ConfigureAwait(false);

            var process = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);
            var updatedName = process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData;

            var person = await _dbFixture.DynamoDbContext.LoadAsync<PersonDbEntity>(process.TargetId).ConfigureAwait(false);
            if (updatedName.ContainsKey(ChangeOfNameKeys.FirstName)) person.FirstName.Should().BeEquivalentTo(updatedName[ChangeOfNameKeys.FirstName].ToString());
            if (updatedName.ContainsKey(ChangeOfNameKeys.MiddleName)) person.MiddleName.Should().BeEquivalentTo(updatedName[ChangeOfNameKeys.MiddleName].ToString());
            if (updatedName.ContainsKey(ChangeOfNameKeys.Surname)) person.Surname.Should().BeEquivalentTo(updatedName[ChangeOfNameKeys.Surname].ToString());
        }
    }
}
