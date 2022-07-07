using System.Collections.Generic;
using Hackney.Core.Sns;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Helpers;
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

        public void AddNewNameToEvent(Stateless.StateMachine<string, string>.Transition transition)
        {
            var trigger = transition.Parameters[0] as ProcessTrigger;
            ChangeOfNameHelper.ValidateOptionalFormData(trigger.FormData, new List<string>() { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname });

            _eventData = ProcessHelper.CreateEventData(trigger.FormData, new List<string> { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname });
        }

        protected override void SetUpStates()
        {
            _machine.Configure(SharedStates.ApplicationInitialised)
                    .Permit(SharedPermittedTriggers.StartApplication, ChangeOfNameStates.EnterNewName)
                    .OnExitAsync(() => PublishProcessStartedEvent(ProcessEventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT));

            _machine.Configure(ChangeOfNameStates.EnterNewName)
                    .Permit(ChangeOfNamePermittedTriggers.EnterNewName, ChangeOfNameStates.NameSubmitted);

            _machine.Configure(ChangeOfNameStates.NameSubmitted)
                    .OnEntry(AddNewNameToEvent);
            //.Permit(SharedPermittedTriggers.RequestDocumentsDes, SharedStates.DocumentsRequestedDes)
            //.Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
            //.Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);
        }
    }
}
