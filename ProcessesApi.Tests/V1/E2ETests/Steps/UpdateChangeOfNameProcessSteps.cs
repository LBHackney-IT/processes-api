using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Person.Infrastructure;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using ProcessesApi.V1.Infrastructure;
using System.Net.Http;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.E2ETests.Steps
{
    public class UpdateChangeOfNameProcessSteps : UpdateProcessBaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public UpdateChangeOfNameProcessSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient, dbFixture)
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
        }
    }
}
