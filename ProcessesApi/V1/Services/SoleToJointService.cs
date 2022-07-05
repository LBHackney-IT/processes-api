using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Infrastructure.JWT;

namespace ProcessesApi.V1.Services
{
    public class SoleToJointService : ProcessService, ISoleToJointService
    {
        private readonly ISoleToJointDbOperationsHelper _dbOperationsHelper;

        public SoleToJointService(ISnsFactory snsFactory, ISnsGateway snsGateway, ISoleToJointDbOperationsHelper automatedChecksHelper)
            : base(snsFactory, snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
            _dbOperationsHelper = automatedChecksHelper;

            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SharedPermittedTriggers.CloseProcess,
                SharedPermittedTriggers.CancelProcess,
                SharedPermittedTriggers.StartApplication,
                SoleToJointPermittedTriggers.UpdateTenure
            };
        }

        #region Internal Transitions

        private async Task CheckAutomatedEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;
            SoleToJointHelpers.ValidateFormData(formData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId, SoleToJointFormDataKeys.TenantId });

            var isEligible = await _dbOperationsHelper.CheckAutomatedEligibility(_process.TargetId,
                                                                                    Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()),
                                                                                    Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.TenantId].ToString())
                                                                                   ).ConfigureAwait(false);

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.ManualEligibilityPassed,
                                               SoleToJointInternalTriggers.ManualEligibilityFailed,
                                               (SoleToJointFormDataKeys.BR11, "true"),
                                               (SoleToJointFormDataKeys.BR12, "false"),
                                               (SoleToJointFormDataKeys.BR13, "false"),
                                               (SoleToJointFormDataKeys.BR15, "false"),
                                               (SoleToJointFormDataKeys.BR16, "false"),
                                               (SoleToJointFormDataKeys.BR7, "false"),
                                               (SoleToJointFormDataKeys.BR8, "false"));
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckTenancyBreach(StateMachine<string, string>.Transition transition)
        {

            var processRequest = transition.Parameters[0] as ProcessTrigger;
            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.BreachChecksPassed,
                                               SoleToJointInternalTriggers.BreachChecksFailed,
                                               (SoleToJointFormDataKeys.BR5, "false"),
                                               (SoleToJointFormDataKeys.BR10, "false"),
                                               (SoleToJointFormDataKeys.BR17, "false"),
                                               (SoleToJointFormDataKeys.BR18, "false"));
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task ReviewDocumentsCheck(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var expectedFormDataKeys = new List<string>{
                  SoleToJointFormDataKeys.SeenPhotographicId,
                  SoleToJointFormDataKeys.SeenSecondId,
                  SoleToJointFormDataKeys.IsNotInImmigrationControl,
                  SoleToJointFormDataKeys.SeenProofOfRelationship,
                  SoleToJointFormDataKeys.IncomingTenantLivingInProperty
            };
            SoleToJointHelpers.ValidateFormData(formData, expectedFormDataKeys);

            processRequest.Trigger = SharedInternalTriggers.DocumentChecksPassed;
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckTenureInvestigation(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;


            var triggerMappings = new Dictionary<string, string>
            {
                {SoleToJointFormDataValues.Appointment, SharedInternalTriggers.TenureInvestigationPassedWithInt },
                { SoleToJointFormDataValues.Approve, SharedInternalTriggers.TenureInvestigationPassed },
                { SoleToJointFormDataValues.Decline, SharedInternalTriggers.TenureInvestigationFailed }
            };
            SoleToJointHelpers.ValidateRecommendation(processRequest,
                                                        triggerMappings,
                                                        SoleToJointFormDataKeys.TenureInvestigationRecommendation,
                                                        null);

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckHOApproval(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;

            var triggerMappings = new Dictionary<string, string>
            {
                { SoleToJointFormDataValues.Approve, SharedInternalTriggers.HOApprovalPassed },
                { SoleToJointFormDataValues.Decline, SharedInternalTriggers.HOApprovalFailed }
            };
            SoleToJointHelpers.ValidateRecommendation(processRequest,
                                                        triggerMappings,
                                                        SoleToJointFormDataKeys.HORecommendation,
                                                        new List<string> { SoleToJointFormDataKeys.HousingAreaManagerName });
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        #endregion

        #region State Transition Actions

        private async Task OnProcessClosed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = SoleToJointHelpers.ValidateHasNotifiedResident(processRequest);
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        private async Task OnProcessCancelled(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.Comment });

            _eventData = SoleToJointHelpers.CreateEventData(processRequest.FormData, new List<string> { SoleToJointFormDataKeys.Comment });
            await PublishProcessClosedEvent(x).ConfigureAwait(false);
        }

        private async Task OnProcessCompleted(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            _eventData = SoleToJointHelpers.ValidateHasNotifiedResident(processRequest);

            var newTenureId = await _dbOperationsHelper.UpdateTenures(_process, _token).ConfigureAwait(false);
            _eventData.Add(SoleToJointFormDataKeys.NewTenureId, newTenureId);
            SoleToJointHelpers.AddNewTenureToRelatedEntities(newTenureId, _process);

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
            SoleToJointHelpers.ValidateFormData(trigger.FormData, new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });

            _eventData = SoleToJointHelpers.CreateEventData(trigger.FormData, new List<string> { SoleToJointFormDataKeys.AppointmentDateTime });
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
                    .OnExitAsync(() => PublishProcessStartedEvent(ProcessEventConstants.PROCESS_STARTED_AGAINST_TENURE_EVENT));

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
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed);

            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, CheckTenancyBreach)
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed);

            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SharedPermittedTriggers.RequestDocumentsDes, SharedStates.DocumentsRequestedDes)
                    .Permit(SharedPermittedTriggers.RequestDocumentsAppointment, SharedStates.DocumentsRequestedAppointment);

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
                   .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed); ;

            _machine.Configure(SharedStates.TenureInvestigationFailed)
                    .Permit(SharedPermittedTriggers.ScheduleInterview, SharedStates.InterviewScheduled)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed); ;

            _machine.Configure(SharedStates.InterviewScheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SharedPermittedTriggers.RescheduleInterview, SharedStates.InterviewRescheduled)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SharedStates.InterviewRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SharedPermittedTriggers.HOApproval, CheckHOApproval)
                    .PermitReentry(SharedPermittedTriggers.RescheduleInterview)
                    .Permit(SharedInternalTriggers.HOApprovalFailed, SharedStates.HOApprovalFailed)
                    .Permit(SharedInternalTriggers.HOApprovalPassed, SharedStates.HOApprovalPassed)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SharedStates.HOApprovalPassed)
                    .Permit(SoleToJointPermittedTriggers.ScheduleTenureAppointment, SoleToJointStates.TenureAppointmentScheduled)
                    .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);


            _machine.Configure(SharedStates.HOApprovalFailed)
                    .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.TenureAppointmentScheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .Permit(SoleToJointPermittedTriggers.RescheduleTenureAppointment, SoleToJointStates.TenureAppointmentRescheduled)
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.TenureAppointmentRescheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .PermitReentry(SoleToJointPermittedTriggers.RescheduleTenureAppointment)
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated)
                     .Permit(SharedPermittedTriggers.CancelProcess, SharedStates.ProcessCancelled)
                     .Permit(SharedPermittedTriggers.CloseProcess, SharedStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.TenureUpdated)
                    .OnEntryAsync(OnProcessCompleted);
        }
    }
}
