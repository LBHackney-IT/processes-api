using System.Collections.Generic;
using System.Threading.Tasks;
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
            ProcessHelper.ValidateOptionalKeys(trigger.FormData, new List<string>() { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname });

            _eventData = ProcessHelper.CreateEventData(trigger.FormData, new List<string> { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname });
        }

        public void AddAppointmentDateTimeToEvent(Stateless.StateMachine<string, string>.Transition transition)
        {
            var trigger = transition.Parameters[0] as ProcessTrigger;
            trigger.FormData.ValidateKeys(new List<string>() { SharedKeys.AppointmentDateTime });

            _eventData = ProcessHelper.CreateEventData(trigger.FormData, new List<string> { SharedKeys.AppointmentDateTime });
        }

        private async Task OnProcessClosed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = ProcessHelper.ValidateHasNotifiedResident(processRequest);
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        private async Task OnProcessCancelled(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            ProcessHelper.ValidateKeys(processRequest.FormData, new List<string>() { SharedKeys.Comment });

            _eventData = ProcessHelper.CreateEventData(processRequest.FormData, new List<string> { SharedKeys.Comment });
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        protected override void SetUpStates()
        {
            _machine.Configure(SharedStates.ProcessClosed)
                    .OnEntryAsync(OnProcessClosed);

            _machine.Configure(SharedStates.ProcessCancelled)
                    .OnEntryAsync(OnProcessCancelled);

            _machine.Configure(SharedStates.ApplicationInitialised)
                    .Permit(SharedPermittedTriggers.StartApplication, ChangeOfNameStates.EnterNewName)
                    .OnExitAsync(() => PublishProcessStartedEvent(ProcessEventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT));

            _machine.Configure(ChangeOfNameStates.EnterNewName)
                    .Permit(ChangeOfNamePermittedTriggers.EnterNewName, ChangeOfNameStates.NameSubmitted);

            _machine.Configure(ChangeOfNameStates.NameSubmitted)
                    .OnEntry(AddNewNameToEvent)
                    .Permit(SharedPermittedTriggers.RequestDocumentsDes, SharedStates.DocumentsRequestedDes)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.DocumentsRequestedDes)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
                    .Permit(SharedPermittedTriggers.ReviewDocuments, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.DocumentsRequestedAppointment)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .Permit(SharedPermittedTriggers.RescheduleDocumentsAppointment, SharedStates.DocumentsAppointmentRescheduled)
                    .Permit(SharedPermittedTriggers.ReviewDocuments, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.DocumentsAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .PermitReentry(SharedPermittedTriggers.RescheduleDocumentsAppointment)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

        }
    }
}
