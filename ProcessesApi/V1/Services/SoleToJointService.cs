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
using System.Globalization;
using System.Linq;
using ProcessesApi.V1.Infrastructure.Extensions;

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

        private async Task OnAutomatedCheckFailed(Stateless.StateMachine<string, string>.Transition x)
        {
            AddIncomingTenantId(x);
            await PublishProcessUpdatedEvent("Automatic eligibility check failed.");
        }

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

        private async Task OnManualCheckFailed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            await PublishProcessUpdatedEvent("Manual Eligibility Check failed.");
        }

        private async Task OnTenancyBreachCheckFailed(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            await PublishProcessUpdatedEvent("Tenancy Breach Check failed.");
        }

        private async Task OnDocumentsRequestedDes(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            await PublishProcessUpdatedEvent("Supporting Documents requested through the Document Evidence Store.");
        }

        private async Task OnRequestDocumentsAppointment(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });
            var appointmentDetails = processRequest.FormData[SoleToJointFormDataKeys.AppointmentDateTime];

            if (DateTime.TryParse(appointmentDetails.ToString(), out DateTime appointmentDateTime))
            {
                await PublishProcessUpdatedEvent($"Supporting Documents requested via an office appointment on {appointmentDateTime.ToString("dd/MM/yyyy hh:mm tt")}.");
            }
            else
            {
                throw new FormDataFormatException("appointment datetime", appointmentDetails);
            }
        }

        protected async Task PublishProcessUpdatedEventWithRescheduledAppointment(string oldAppointmentTime, string newAppointmentTime)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessUpdatedWithAppointmentRescheduled(_process, _token, oldAppointmentTime, newAppointmentTime);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        private async Task OnDocumentsAppointmentRescheduled(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            var oldAppointmentDateTime = SoleToJointHelpers.GetAppointmentDateTime(_process.CurrentState.ProcessData.FormData);
            var newAppointmentDateTime = SoleToJointHelpers.GetAppointmentDateTime(processRequest.FormData);

            await PublishProcessUpdatedEventWithRescheduledAppointment(oldAppointmentDateTime.ToString("dd/MM/yyyy hh:mm tt"), newAppointmentDateTime.ToString("dd/MM/yyyy hh:mm tt"));
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
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed);

            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .OnEntryAsync(OnAutomatedCheckFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .OnEntry(AddIncomingTenantId)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, CheckManualEligibility)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed);

            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .OnEntryAsync(OnManualCheckFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, CheckTenancyBreach)
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed);

            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .OnEntryAsync(OnTenancyBreachCheckFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsDes, SoleToJointStates.DocumentsRequestedDes)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);

            _machine.Configure(SoleToJointStates.DocumentsRequestedDes)
                    .OnEntryAsync(OnDocumentsRequestedDes)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);

            _machine.Configure(SoleToJointStates.DocumentsRequestedAppointment)
                    .OnEntryAsync(OnRequestDocumentsAppointment)
                    .Permit(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, SoleToJointStates.DocumentsAppointmentRescheduled);

            _machine.Configure(SoleToJointStates.DocumentsAppointmentRescheduled)
                    .OnEntryAsync(OnDocumentsAppointmentRescheduled)
                    .PermitReentry(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment);
        }
    }
}
