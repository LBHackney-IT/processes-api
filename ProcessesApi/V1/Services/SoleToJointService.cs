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
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Services
{
    public class SoleToJointService : ProcessService, ISoleToJointService
    {
        private readonly ISoleToJointAutomatedEligibilityChecksHelper _automatedcheckshelper;
        private readonly IGetPersonByIdHelper _personByIdHelper;


        public SoleToJointService(ISnsFactory snsFactory,
                                  ISnsGateway snsGateway,
                                  ISoleToJointAutomatedEligibilityChecksHelper automatedChecksHelper,
                                  IGetPersonByIdHelper getPersonByIdHelper)
            : base(snsFactory, snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
            _automatedcheckshelper = automatedChecksHelper;
            _personByIdHelper = getPersonByIdHelper;
            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SoleToJointPermittedTriggers.CloseProcess,
                SoleToJointPermittedTriggers.CancelProcess,
                SharedInternalTriggers.StartApplication,
                SoleToJointPermittedTriggers.UpdateTenure
            };
        }

        #region Internal Transitions

        private async Task CheckAutomatedEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;
            SoleToJointHelpers.ValidateFormData(formData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId, SoleToJointFormDataKeys.TenantId });

            var isEligible = await _automatedcheckshelper.CheckAutomatedEligibility(_process.TargetId,
                                                                     Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()),
                                                                     Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.TenantId].ToString()))
                                                                     .ConfigureAwait(false);

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

            processRequest.Trigger = SoleToJointInternalTriggers.DocumentChecksPassed;
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckTenureInvestigation(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var expectedFormDataKeys = new List<string> { SoleToJointFormDataKeys.TenureInvestigationRecommendation };
            SoleToJointHelpers.ValidateFormData(formData, expectedFormDataKeys);
            var tenureInvestigationRecommendation = formData[SoleToJointFormDataKeys.TenureInvestigationRecommendation].ToString();

            switch (tenureInvestigationRecommendation)
            {
                case SoleToJointFormDataValues.Appointment:
                    processRequest.Trigger = SoleToJointInternalTriggers.TenureInvestigationPassedWithInt;
                    break;
                case SoleToJointFormDataValues.Approve:
                    processRequest.Trigger = SoleToJointInternalTriggers.TenureInvestigationPassed;
                    break;
                case SoleToJointFormDataValues.Decline:
                    processRequest.Trigger = SoleToJointInternalTriggers.TenureInvestigationFailed;
                    break;
                default:
                    throw new FormDataInvalidException(String.Format("Tenure Investigation Recommendation must be one of: [{0}, {1}, {2}], but the value provided was: '{3}'.",
                                                                     SoleToJointFormDataValues.Appointment,
                                                                     SoleToJointFormDataValues.Approve,
                                                                     SoleToJointFormDataValues.Decline,
                                                                     tenureInvestigationRecommendation));
            }
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckHOApproval(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as ProcessTrigger;
            var formData = processRequest.FormData;

            var expectedFormDataKeys = new List<string> { SoleToJointFormDataKeys.HORecommendation };
            SoleToJointHelpers.ValidateFormData(formData, expectedFormDataKeys);
            var housingOfficerRecommendation = formData[SoleToJointFormDataKeys.HORecommendation].ToString();

            switch (housingOfficerRecommendation)
            {

                case SoleToJointFormDataValues.Approve:
                    processRequest.Trigger = SoleToJointInternalTriggers.HOApprovalPassed;
                    break;
                case SoleToJointFormDataValues.Decline:
                    processRequest.Trigger = SoleToJointInternalTriggers.HOApprovalFailed;
                    break;
                default:
                    throw new FormDataInvalidException(String.Format("Housing Officer Recommendation must be one of: [{0}, {1}] but the value provided was: '{2}'.",
                                                                     SoleToJointFormDataValues.Approve,
                                                                     SoleToJointFormDataValues.Decline,
                                                                     housingOfficerRecommendation));
            }
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        #endregion

        #region State Transition Actions

        private async Task OnProcessClosed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;

            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.HasNotifiedResident });

            if (processRequest.FormData.ContainsKey(SoleToJointFormDataKeys.Reason))
                _eventData = SoleToJointHelpers.CreateEventData(processRequest.FormData, new List<string> { SoleToJointFormDataKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SoleToJointFormDataKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident) throw new FormDataInvalidException("Housing Officer must notify the resident before closing this process.");

                await PublishProcessClosedEvent(x).ConfigureAwait(false);
            }
            else
            {
                throw new FormDataFormatException("boolean", hasNotifiedResidentString);
            }
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

            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.HasNotifiedResident });

            if (processRequest.FormData.ContainsKey(SoleToJointFormDataKeys.Reason))
                _eventData = SoleToJointHelpers.CreateEventData(processRequest.FormData, new List<string> { SoleToJointFormDataKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SoleToJointFormDataKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident) throw new FormDataInvalidException("Housing Officer must notify the resident before completing this process.");

                await PublishProcessCompletedEvent(x).ConfigureAwait(false);
            }
            else
            {
                throw new FormDataFormatException("boolean", hasNotifiedResidentString);
            }
        }

        private void AddIncomingTenantIdToRelatedEntities(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            SoleToJointHelpers.AddIncomingTenantToRelatedEntities(processRequest.FormData, _process, _personByIdHelper);
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
            _machine.Configure(SharedProcessStates.ProcessClosed)
                    .OnEntryAsync(OnProcessClosed);

            _machine.Configure(SharedProcessStates.ProcessCancelled)
                    .OnEntryAsync(OnProcessCancelled);

            _machine.Configure(SharedProcessStates.ApplicationInitialised)
                    .Permit(SharedInternalTriggers.StartApplication, SoleToJointStates.SelectTenants)
                    .OnExitAsync(PublishProcessStartedEvent);

            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckAutomatedEligibility, CheckAutomatedEligibility)
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed)
                    .OnExit(AddIncomingTenantIdToRelatedEntities);

            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, CheckManualEligibility)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed);

            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, CheckTenancyBreach)
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed);

            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsDes, SoleToJointStates.DocumentsRequestedDes)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);

            _machine.Configure(SoleToJointStates.DocumentsRequestedDes)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .Permit(SoleToJointInternalTriggers.DocumentChecksPassed, SoleToJointStates.DocumentChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.DocumentsRequestedAppointment)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .Permit(SoleToJointInternalTriggers.DocumentChecksPassed, SoleToJointStates.DocumentChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, SoleToJointStates.DocumentsAppointmentRescheduled)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.DocumentsAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.ReviewDocuments, ReviewDocumentsCheck)
                    .PermitReentry(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment)
                    .Permit(SoleToJointInternalTriggers.DocumentChecksPassed, SoleToJointStates.DocumentChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.DocumentChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.SubmitApplication, SoleToJointStates.ApplicationSubmitted);

            _machine.Configure(SoleToJointStates.ApplicationSubmitted)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.TenureInvestigation, CheckTenureInvestigation)
                    .Permit(SoleToJointInternalTriggers.TenureInvestigationFailed, SoleToJointStates.TenureInvestigationFailed)
                    .Permit(SoleToJointInternalTriggers.TenureInvestigationPassed, SoleToJointStates.TenureInvestigationPassed)
                    .Permit(SoleToJointInternalTriggers.TenureInvestigationPassedWithInt, SoleToJointStates.TenureInvestigationPassedWithInt);

            _machine.Configure(SoleToJointStates.TenureInvestigationPassedWithInt)
                    .Permit(SoleToJointPermittedTriggers.ScheduleInterview, SoleToJointStates.InterviewScheduled);

            _machine.Configure(SoleToJointStates.TenureInvestigationPassed)
                   .Permit(SoleToJointPermittedTriggers.ScheduleInterview, SoleToJointStates.InterviewScheduled);

            _machine.Configure(SoleToJointStates.TenureInvestigationFailed)
                    .Permit(SoleToJointPermittedTriggers.ScheduleInterview, SoleToJointStates.InterviewScheduled);

            _machine.Configure(SoleToJointStates.InterviewScheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SoleToJointPermittedTriggers.RescheduleInterview, SoleToJointStates.InterviewRescheduled)
                    .Permit(SoleToJointInternalTriggers.HOApprovalFailed, SoleToJointStates.HOApprovalFailed)
                    .Permit(SoleToJointInternalTriggers.HOApprovalPassed, SoleToJointStates.HOApprovalPassed);

            _machine.Configure(SoleToJointStates.InterviewRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.HOApproval, CheckHOApproval)
                    .Permit(SoleToJointInternalTriggers.HOApprovalFailed, SoleToJointStates.HOApprovalFailed)
                    .Permit(SoleToJointInternalTriggers.HOApprovalPassed, SoleToJointStates.HOApprovalPassed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.HOApprovalPassed)
                    .Permit(SoleToJointPermittedTriggers.ScheduleTenureAppointment, SoleToJointStates.TenureAppointmentScheduled);

            _machine.Configure(SoleToJointStates.HOApprovalFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);

            _machine.Configure(SoleToJointStates.TenureAppointmentScheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .Permit(SoleToJointPermittedTriggers.RescheduleTenureAppointment, SoleToJointStates.TenureAppointmentRescheduled)
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated);


            _machine.Configure(SoleToJointStates.TenureAppointmentRescheduled)
                     .OnEntry(AddAppointmentDateTimeToEvent)
                     .Permit(SoleToJointPermittedTriggers.UpdateTenure, SoleToJointStates.TenureUpdated)
                     .Permit(SoleToJointPermittedTriggers.CancelProcess, SharedProcessStates.ProcessCancelled);


            _machine.Configure(SoleToJointStates.TenureUpdated)
                    .OnEntryAsync(OnProcessCompleted);


            //Add next permitted trigger here
        }
    }
}
