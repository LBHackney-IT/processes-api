using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using Hackney.Shared.Processes.Domain.Constants.Shared;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using ChangeOfNameKeys = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNameKeys;
using ChangeOfNamePermittedTriggers = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNamePermittedTriggers;
using SharedInternalTriggers = Hackney.Shared.Processes.Domain.Constants.SharedInternalTriggers;
using SharedKeys = Hackney.Shared.Processes.Domain.Constants.SharedKeys;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;
using Hackney.Shared.Processes.Sns;

namespace ProcessesApi.V1.Services
{
    public class ChangeOfNameService : ProcessService, IChangeOfNameService
    {
        private readonly IDbOperationsHelper _dbOperationsHelper;

        public ChangeOfNameService(ISnsGateway snsGateway, IDbOperationsHelper dbOperationsHelper) : base(snsGateway)
        {
            _snsGateway = snsGateway;
            _dbOperationsHelper = dbOperationsHelper;
            _permittedTriggersType = typeof(ChangeOfNamePermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SharedPermittedTriggers.CloseProcess,
                SharedPermittedTriggers.CancelProcess,
                SharedPermittedTriggers.StartApplication,
                ChangeOfNamePermittedTriggers.UpdateName
            };
        }

        public void AddNewNameToEvent(Stateless.StateMachine<string, string>.Transition transition)
        {
            var trigger = transition.Parameters[0] as ProcessTrigger;
            trigger.FormData.ValidateKeys(new List<string>() { ChangeOfNameKeys.FirstName, ChangeOfNameKeys.Surname, ChangeOfNameKeys.Title });
            var eventDataKeys = new List<string>() { ChangeOfNameKeys.FirstName, ChangeOfNameKeys.Surname, ChangeOfNameKeys.Title };
            if (trigger.FormData.ContainsKey(ChangeOfNameKeys.MiddleName))
                eventDataKeys.Add(ChangeOfNameKeys.MiddleName);

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

        private async Task OnProcessCompleted(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = processRequest.ValidateHasNotifiedResident();

            await _dbOperationsHelper.UpdatePerson(_process, _token).ConfigureAwait(false);

            await PublishProcessCompletedEvent(x).ConfigureAwait(false);
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
                    .Permit(SharedPermittedTriggers.StartApplication, ChangeOfNameStates.EnterNewName);

            _machine.Configure(ChangeOfNameStates.EnterNewName)
                    .OnEntryAsync(() => PublishProcessStartedEvent(EventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT))
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
                     .Permit(ChangeOfNamePermittedTriggers.UpdateName, ChangeOfNameStates.NameUpdated)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.TenureAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .PermitReentry(SharedPermittedTriggers.RescheduleTenureAppointment)
                    .Permit(ChangeOfNamePermittedTriggers.UpdateName, ChangeOfNameStates.NameUpdated)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(ChangeOfNameStates.NameUpdated)
                    .OnEntryAsync(OnProcessCompleted);



        }
    }
}
