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
            var processRequest = transition.Parameters[0] as UpdateProcessState;
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
            var processRequest = transition.Parameters[0] as UpdateProcessState;
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

            var processRequest = transition.Parameters[0] as UpdateProcessState;
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

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId });

            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_process.RelatedEntities == null)
                _process.RelatedEntities = new List<Guid>();

            _process.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
        }

        private async Task OnAutomatedCheckFailed(UpdateProcessState processRequest)
        {
            AddIncomingTenantId(processRequest);
            await PublishProcessUpdatedEvent("Automatic eligibility check failed.");
        }

        private async Task OnProcessClosed(UpdateProcessState processRequest)
        {
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

        private async Task OnManualCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessUpdatedEvent("Manual Eligibility Check failed.");
        }

        private async Task OnTenancyBreachCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessUpdatedEvent("Tenancy Breach Check failed.");
        }

        private async Task OnDocumentsRequestedDes(UpdateProcessState processRequest)
        {
            await PublishProcessUpdatedEvent("Supporting Documents requested through the Document Evidence Store.");
        }

        private async Task OnRequestDocumentsAppointment(UpdateProcessState processRequest)
        {
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

        private async Task OnDocumentsAppointmentRescheduled(UpdateProcessState processRequest)
        {
            var oldAppointmentDateTime = GetAppointmentDateTime(_process.CurrentState.ProcessData.FormData);
            var newAppointmentDateTime = GetAppointmentDateTime(processRequest.FormData);

            await PublishProcessUpdatedEventWithRescheduledAppointment(oldAppointmentDateTime.ToString("dd/MM/yyyy hh:mm tt"), newAppointmentDateTime.ToString("dd/MM/yyyy hh:mm tt"));
        }


        private static DateTime GetAppointmentDateTime(Dictionary<string, object> formData)
        {
            formData.TryGetValue(SoleToJointFormDataKeys.AppointmentDateTime, out var dateTimeString);

            if (dateTimeString != null)
            {
                return DateTime
                    .Parse(dateTimeString.ToString(), null, DateTimeStyles.RoundtripKind)
                    .ToUniversalTime();
            }

            throw new FormDataNotFoundException(formData.Keys.ToList(), new List<string> { SoleToJointFormDataKeys.AppointmentDateTime });
        }

        #endregion

        protected override void SetUpStates()
        {
            _machine.Configure(SharedProcessStates.ApplicationInitialised)
                    .Permit(SharedInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);

            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckAutomatedEligibility, async (x) => await CheckAutomatedEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed);

            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, async (x) => await CheckManualEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed);

            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CloseProcess, SharedProcessStates.ProcessClosed);

            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, async (x) => await CheckTenancyBreach(x).ConfigureAwait(false))
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
                    .Permit(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, SoleToJointStates.DocumentsAppointmentRescheduled);

            _machine.Configure(SoleToJointStates.DocumentsAppointmentRescheduled)
                    .PermitReentry(SoleToJointPermittedTriggers.RescheduleDocumentsAppointment);
        }

        protected override void SetUpStateActions()
        {
            ConfigureAsync(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), (x) => PublishProcessStartedEvent());
            ConfigureAsync(SharedProcessStates.ProcessClosed, Assignment.Create("tenants"), OnProcessClosed);

            ConfigureAsync(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), OnAutomatedCheckFailed);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);

            ConfigureAsync(SoleToJointStates.ManualChecksFailed, Assignment.Create("tenants"), OnManualCheckFailed);
            Configure(SoleToJointStates.ManualChecksPassed, Assignment.Create("tenants"));

            ConfigureAsync(SoleToJointStates.BreachChecksFailed, Assignment.Create("tenants"), OnTenancyBreachCheckFailed);
            ConfigureAsync(SoleToJointStates.BreachChecksPassed, Assignment.Create("tenants"));

            ConfigureAsync(SoleToJointStates.DocumentsRequestedDes, Assignment.Create("tenants"), OnDocumentsRequestedDes);
            ConfigureAsync(SoleToJointStates.DocumentsRequestedAppointment, Assignment.Create("tenants"), OnRequestDocumentsAppointment);
            ConfigureAsync(SoleToJointStates.DocumentsAppointmentRescheduled, Assignment.Create("tenants"), OnDocumentsAppointmentRescheduled);
        }
    }
}
