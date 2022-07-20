using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using ProcessesApi.V1.Constants.Shared;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Infrastructure.JWT;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;

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

        private async Task ReviewDocumentsCheck(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var expectedFormDataKeys = new List<string>{
                  SharedKeys.SeenPhotographicId,
                  SharedKeys.SeenSecondId,
                  ChangeOfNameKeys.AtLeastOneDocument,
            };
            ProcessHelper.ValidateKeys(formData, expectedFormDataKeys);

            processRequest.Trigger = SharedInternalTriggers.DocumentChecksPassed;
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        public async Task CheckTenureInvestigation(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;


            var triggerMappings = new Dictionary<string, string>
            {
                { SharedValues.Appointment, SharedInternalTriggers.TenureInvestigationPassedWithInt },
                { SharedValues.Approve, SharedInternalTriggers.TenureInvestigationPassed },
                { SharedValues.Decline, SharedInternalTriggers.TenureInvestigationFailed }
            };

            processRequest.SelectTriggerFromUserInput(triggerMappings,
                                                SharedKeys.TenureInvestigationRecommendation,
                                                null);

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckHOApproval(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var triggerMappings = new Dictionary<string, string>
            {
                { SharedValues.Approve, SharedInternalTriggers.HOApprovalPassed },
                { SharedValues.Decline, SharedInternalTriggers.HOApprovalFailed }
            };

            processRequest.SelectTriggerFromUserInput(triggerMappings,
                                                SharedKeys.HORecommendation,
                                                new List<string> { SharedKeys.HousingAreaManagerName });

            var eventDataKeys = new List<string> { SharedKeys.HousingAreaManagerName };
            if (formData.ContainsKey(SharedKeys.Reason)) eventDataKeys.Add(SharedKeys.Reason);
            _eventData = formData.CreateEventData(eventDataKeys);

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
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
                    .InternalTransitionAsync(SharedPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
                    .Permit(SharedInternalTriggers.DocumentChecksPassed, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SharedStates.DocumentsRequestedAppointment)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SharedPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .Permit(SharedInternalTriggers.DocumentChecksPassed, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.RescheduleDocumentsAppointment, SharedStates.DocumentsAppointmentRescheduled)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SharedStates.DocumentsAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SharedPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .PermitReentry(SharedPermittedTriggers.RescheduleDocumentsAppointment)
                    .Permit(SharedInternalTriggers.DocumentChecksPassed, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SharedStates.DocumentChecksPassed)
                   .Permit(SharedPermittedTriggers.SubmitApplication, SharedStates.ApplicationSubmitted)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed);

            _machine.Configure(SharedStates.ApplicationSubmitted)
                    .InternalTransitionAsync(SharedPermittedTriggers.TenureInvestigation, CheckTenureInvestigation)
                    .Permit(SharedInternalTriggers.TenureInvestigationFailed, SharedStates.TenureInvestigationFailed)
                    .Permit(SharedInternalTriggers.TenureInvestigationPassed, SharedStates.TenureInvestigationPassed)
                    .Permit(SharedInternalTriggers.TenureInvestigationPassedWithInt, SharedStates.TenureInvestigationPassedWithInt);

            _machine.Configure(SharedStates.TenureInvestigationPassedWithInt)
                    .Permit(SharedPermittedTriggers.ScheduleInterview, SharedStates.InterviewScheduled)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed);

            _machine.Configure(SharedStates.TenureInvestigationPassed)
                    .Permit(SharedPermittedTriggers.ScheduleInterview, SharedStates.InterviewScheduled)
                   .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                   .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                   .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed);

            _machine.Configure(SharedStates.TenureInvestigationFailed)
                    .Permit(SharedPermittedTriggers.ScheduleInterview, SharedStates.InterviewScheduled)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed);

            _machine.Configure(SharedStates.InterviewScheduled)
                   .OnEntry(AddAppointmentDateTimeToEvent)
                   .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                   .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                   .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed)
                   .Permit(SharedPermittedTriggers.RescheduleInterview, SharedStates.InterviewRescheduled)
                   .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SharedStates.InterviewRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .PermitReentry(SharedPermittedTriggers.RescheduleInterview)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.HOApprovalPassed)
                    .Permit(SharedPermittedTriggers.ScheduleTenureAppointment, SharedStates.TenureAppointmentScheduled)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SharedStates.HOApprovalFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SharedStates.TenureAppointmentScheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .Permit(SharedPermittedTriggers.RescheduleTenureAppointment, SharedStates.TenureAppointmentRescheduled)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.TenureAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .PermitReentry(SharedPermittedTriggers.RescheduleTenureAppointment)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);



        }
    }
}
