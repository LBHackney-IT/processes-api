using System.Collections.Generic;
using Hackney.Core.Sns;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.JWT;
using ProcessesApi.V1.Services.Interfaces;

namespace ProcessesApi.V1.Services
{
    public class ChangeOfNameService : ProcessService, IChangeOfNameService
    {
        public ChangeOfNameService(ISnsFactory snsFactory, ISnsGateway snsGateway) : base(snsFactory, snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;

            _permittedTriggersType = typeof(ChangeOfNamePermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SharedPermittedTriggers.CloseProcess,
                SharedPermittedTriggers.CancelProcess,
                SharedPermittedTriggers.StartApplication
            };
        }

        protected override void SetUpStates()
        {
            _machine.Configure(SharedStates.ApplicationInitialised)
                    .Permit(SharedPermittedTriggers.StartApplication, ChangeOfNameStates.EnterNewName)
                    .OnExitAsync(() => PublishProcessStartedEvent(ProcessEventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT));
        }
    }
}
