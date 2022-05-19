using ProcessesApi.V1.Domain;
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

        public SoleToJointService(ISnsFactory snsFactory, ISnsGateway snsGateway, ISoleToJointAutomatedEligibilityChecksHelper automatedChecksHelper)
            : base(snsFactory, snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
            _automatedcheckshelper = automatedChecksHelper;

            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
            _ignoredTriggersForProcessUpdated = new List<string>
            {
                SoleToJointPermittedTriggers.CloseProcess,
                SharedInternalTriggers.StartApplication
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

        #endregion

        #region State Transition Actions

        private async Task OnProcessClosed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;

            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.HasNotifiedResident });
            var hasNotifiedResidentString = processRequest.FormData[SoleToJointFormDataKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident) throw new FormDataInvalidException("Housing Officer must notify the resident before closing this process.");
                await PublishProcessClosedEvent().ConfigureAwait(false);
            }
            else
            {
                throw new FormDataFormatException("boolean", hasNotifiedResidentString);
            }
        }

        private void AddIncomingTenantId(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId });

            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_process.RelatedEntities == null)
                _process.RelatedEntities = new List<Guid>();

            _process.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
        }

        public void AddAppointmentDateTimeToEvent(Stateless.StateMachine<string, string>.Transition transition)
        {
            var trigger = transition.Parameters[0] as ProcessTrigger;
            SoleToJointHelpers.ValidateFormData(trigger.FormData, new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });
            var appointmentDetails = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, trigger.FormData[SoleToJointFormDataKeys.AppointmentDateTime] }
            };
            _eventData = appointmentDetails;
        }

        #endregion

        protected override void SetUpStates()
        {
            _machine.Configure(SharedProcessStates.ProcessClosed)
                    .OnEntryAsync(OnProcessClosed);

            _machine.Configure(SharedProcessStates.ApplicationInitialised)
                    .Permit(SharedInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);

            _machine.Configure(SoleToJointStates.SelectTenants)
                    .OnEntryAsync(PublishProcessStartedEvent)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckAutomatedEligibility, CheckAutomatedEligibility)
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed)
                    .OnExit(AddIncomingTenantId);

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
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);

            _machine.Configure(SoleToJointStates.DocumentsRequestedAppointment)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .Permit(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, SoleToJointStates.DocumentsAppointmentRescheduled);

            _machine.Configure(SoleToJointStates.DocumentsAppointmentRescheduled)
                    .OnEntry(AddAppointmentDateTimeToEvent)
                    .PermitReentry(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment);
        }
    }
}
