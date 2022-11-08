using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain.Constants.SoleToJoint;
using ProcessesApi.V1.Helpers;
using Hackney.Shared.Processes.Domain.Constants.Shared;
using SharedInternalTriggers = Hackney.Shared.Processes.Domain.Constants.SharedInternalTriggers;
using SharedKeys = Hackney.Shared.Processes.Domain.Constants.SharedKeys;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;
using SoleToJointInternalTriggers = Hackney.Shared.Processes.Domain.Constants.SoleToJoint.SoleToJointInternalTriggers;
using SoleToJointKeys = Hackney.Shared.Processes.Domain.Constants.SoleToJoint.SoleToJointKeys;
using SoleToJointPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SoleToJoint.SoleToJointPermittedTriggers;
using Hackney.Shared.Processes.Sns;
using System.Linq;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Services
{
    public class SoleToJointService : ProcessService, ISoleToJointService
    {
        private readonly IDbOperationsHelper _dbOperationsHelper;

        public SoleToJointService(ISnsGateway snsGateway, IDbOperationsHelper automatedChecksHelper)
            : base(snsGateway)
        {
            _snsGateway = snsGateway;
            _dbOperationsHelper = automatedChecksHelper;

            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SharedPermittedTriggers.StartApplication,
                SharedPermittedTriggers.CloseProcess,
                SharedPermittedTriggers.CancelProcess,
                SharedPermittedTriggers.CompleteProcess
            };
        }

        #region Internal Transitions

        private async Task CheckAutomatedEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var process = transition.Parameters[1] as Process;

            var tenantDetails = process.RelatedEntities.Find(x => x.TargetType == TargetType.person && x.SubType == SubType.tenant);
            if (tenantDetails is null) throw new InvalidRelatedEntitiesException(TargetType.person, SubType.tenant, process.RelatedEntities);

            var formData = processRequest.FormData;
            formData.ValidateKeys(new List<string>() { SoleToJointKeys.IncomingTenantId });

            var isEligible = await _dbOperationsHelper.CheckAutomatedEligibility(_process.TargetId,
                                                                                    Guid.Parse(processRequest.FormData[SoleToJointKeys.IncomingTenantId].ToString()),
                                                                                    tenantDetails.Id)
                                                                                    .ConfigureAwait(false);

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.ManualEligibilityPassed,
                                               SoleToJointInternalTriggers.ManualEligibilityFailed,
                                               (SoleToJointKeys.BR11, "true"),
                                               (SoleToJointKeys.BR12, "false"),
                                               (SoleToJointKeys.BR13, "false"),
                                               (SoleToJointKeys.BR15, "false"),
                                               (SoleToJointKeys.BR16, "false"),
                                               (SoleToJointKeys.BR7, "false"),
                                               (SoleToJointKeys.BR8, "false"),
                                               (SoleToJointKeys.BR9, "false"));
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckTenancyBreach(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var expectedFormDataKeys = new List<string> { SoleToJointKeys.BR5, SoleToJointKeys.BR10, SoleToJointKeys.BR17, SoleToJointKeys.BR18 };
            processRequest.FormData.ValidateKeys(expectedFormDataKeys);

            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.BreachChecksPassed,
                                               SoleToJointInternalTriggers.BreachChecksFailed,
                                               (SoleToJointKeys.BR5, processRequest.FormData[SoleToJointKeys.BR10].ToString()),
                                               (SoleToJointKeys.BR17, "false"),
                                               (SoleToJointKeys.BR18, "false"));

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task ReviewDocumentsCheck(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var expectedFormDataKeys = new List<string>{
                  SharedKeys.SeenPhotographicId,
                  SharedKeys.SeenSecondId,
                  SoleToJointKeys.IsNotInImmigrationControl,
                  SoleToJointKeys.SeenProofOfRelationship,
                  SoleToJointKeys.IncomingTenantLivingInProperty
            };
            formData.ValidateKeys(expectedFormDataKeys);

            processRequest.Trigger = SharedInternalTriggers.DocumentChecksPassed;
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

        public async Task CheckTenureInvestigation(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;


            var triggerMappings = new Dictionary<string, string>
            {
                {SharedValues.Appointment, SharedInternalTriggers.TenureInvestigationPassedWithInt },
                { SharedValues.Approve, SharedInternalTriggers.TenureInvestigationPassed },
                { SharedValues.Decline, SharedInternalTriggers.TenureInvestigationFailed }
            };

            processRequest.SelectTriggerFromUserInput(triggerMappings,
                                                SharedKeys.TenureInvestigationRecommendation,
                                                null);

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        #endregion

        #region State Transition Actions
        private async Task OnProcessClosed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = processRequest.ValidateHasNotifiedResident();
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        private async Task OnProcessCancelled(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            processRequest.FormData.ValidateKeys(new List<string>() { SharedKeys.Comment });

            _eventData = processRequest.FormData.CreateEventData(new List<string> { SharedKeys.Comment });
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        private async Task OnProcessCompleted(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = processRequest.ValidateHasNotifiedResident();
            await PublishProcessCompletedEvent(x).ConfigureAwait(false);
        }

        private async Task AddIncomingTenantIdToRelatedEntitiesAsync(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            await _dbOperationsHelper.AddIncomingTenantToRelatedEntities(processRequest.FormData, _process)
                                     .ConfigureAwait(false);
        }

        public void AddAppointmentDateTimeToEvent(Stateless.StateMachine<string, string>.Transition transition)
        {
            var trigger = transition.Parameters[0] as ProcessTrigger;
            trigger.FormData.ValidateKeys(new List<string>() { SharedKeys.AppointmentDateTime });

            _eventData = trigger.FormData.CreateEventData(new List<string> { SharedKeys.AppointmentDateTime });
        }

        private async Task UpdateTenures(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            (var newTenureId, var tenureStartDate) = await _dbOperationsHelper.UpdateTenures(_process, _token, processRequest.FormData).ConfigureAwait(false);

            _process.AddNewTenureToRelatedEntities(newTenureId);
            _eventData = new Dictionary<string, object>
            {
                { SoleToJointKeys.NewTenureId, newTenureId },
                { SoleToJointKeys.TenureStartDate,  tenureStartDate }
            };
        }

        #endregion

        protected override void SetUpStates()
        {
            _machine.Configure(SharedStates.ProcessClosed)
                    .OnEntryAsync(OnProcessClosed);

            _machine.Configure(SharedStates.ProcessCancelled)
                    .OnEntryAsync(OnProcessCancelled);

            _machine.Configure(SharedStates.ApplicationInitialised)
                    .Permit(SharedPermittedTriggers.StartApplication, SoleToJointStates.SelectTenants)
                    .OnExitAsync((x) => PublishProcessStartedEvent(x, EventConstants.PROCESS_STARTED_AGAINST_TENURE_EVENT));

            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckAutomatedEligibility, CheckAutomatedEligibility)
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed)
                    .OnExitAsync(AddIncomingTenantIdToRelatedEntitiesAsync);

            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, CheckManualEligibility)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, CheckTenancyBreach)
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SharedPermittedTriggers.RequestDocumentsDes, SharedStates.DocumentsRequestedDes)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SharedStates.DocumentsRequestedDes)
                    .InternalTransitionAsync(SharedPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .Permit(SharedInternalTriggers.DocumentChecksPassed, SharedStates.DocumentChecksPassed)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment)
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
                    .Permit(SharedPermittedTriggers.SubmitApplication, SharedStates.ApplicationSubmitted);

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
                   .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed)
                   .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


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
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.TenureAppointmentRescheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .PermitReentry(SharedPermittedTriggers.RescheduleTenureAppointment)
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled)
                     .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.TenureUpdated)
                    .OnEntryAsync(UpdateTenures)
                    .Permit(SharedPermittedTriggers.CompleteProcess, SharedStates.ProcessCompleted);

            _machine.Configure(SharedStates.ProcessCompleted)
                    .OnEntryAsync(OnProcessCompleted);
        }
    }
}
