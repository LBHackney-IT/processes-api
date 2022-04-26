using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.V1.Services
{
    public class SoleToJointService : ProcessService, ISoleToJointService
    {
        private readonly ISoleToJointGateway _soleToJointGateway;

        public SoleToJointService(ISoleToJointGateway gateway, ISnsFactory snsFactory, ISnsGateway snsGateway)
            : base(snsFactory, snsGateway)
        {
            _soleToJointGateway = gateway;
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
        }

        private async Task CheckEligibility(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            try
            {
                var isEligible = await _soleToJointGateway.CheckEligibility(_process.TargetId,
                                                                            Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()),
                                                                            Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.TenantId].ToString()))
                                            .ConfigureAwait(false);

                processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

                var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
                await _machine.FireAsync(res, processRequest, _process);
            }
            catch (KeyNotFoundException)
            {
                var expectedFormDataKeys = new List<string>
                {
                    SoleToJointFormDataKeys.IncomingTenantId,
                    SoleToJointFormDataKeys.TenantId
                };
                throw new FormDataNotFoundException(processRequest.FormData.Keys.ToList(), expectedFormDataKeys);
            }
        }

        private async Task ValidateManualCheck(
            StateMachine<string, string>.Transition transition,
            string passedTrigger, string failedTrigger,
            params (string CheckId, string Value)[] expectations)
        {
            var processRequest = (UpdateProcessState) transition.Parameters[0];
            var formData = processRequest.FormData;

            try
            {
                var isCheckPassed = expectations.All(expectation =>
                    String.Equals(
                        expectation.Value,
                        formData[expectation.CheckId].ToString(),
                        StringComparison.OrdinalIgnoreCase));

                processRequest.Trigger = isCheckPassed
                    ? passedTrigger
                    : failedTrigger;

                var trigger = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
                await _machine.FireAsync(trigger, processRequest, _process);
            }
            catch (KeyNotFoundException)
            {
                var expectedFormDataKeys = expectations.Select(expectation => expectation.CheckId).ToList();
                throw new FormDataNotFoundException(formData.Keys.ToList(), expectedFormDataKeys);
            }
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition transition)
        {
            await ValidateManualCheck(
                    transition,
                    SoleToJointInternalTriggers.ManualEligibilityPassed,
                    SoleToJointInternalTriggers.ManualEligibilityFailed,
                    (SoleToJointFormDataKeys.BR11, "true"),
                    (SoleToJointFormDataKeys.BR12, "false"),
                    (SoleToJointFormDataKeys.BR13, "false"),
                    (SoleToJointFormDataKeys.BR15, "false"),
                    (SoleToJointFormDataKeys.BR16, "false"))
                .ConfigureAwait(false);
        }

        private async Task CheckTenancyBreach(StateMachine<string, string>.Transition transition)
        {
            await ValidateManualCheck(
                    transition,
                    SoleToJointInternalTriggers.BreachChecksPassed,
                    SoleToJointInternalTriggers.BreachChecksFailed,
                    (SoleToJointFormDataKeys.BR5, "false"),
                    (SoleToJointFormDataKeys.BR10, "false"),
                    (SoleToJointFormDataKeys.BR17, "false"),
                    (SoleToJointFormDataKeys.BR18, "false"))
                .ConfigureAwait(false);
        }

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_process.RelatedEntities == null)
                _process.RelatedEntities = new List<Guid>();
            _process.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
        }

        protected override void SetUpStates()
        {
            _machine.Configure(SharedProcessStates.ApplicationInitialised)
                    .Permit(SharedInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckEligibility, async (x) => await CheckEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed);
            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, async (x) => await CheckManualEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed);
            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, async (x) => await CheckTenancyBreach(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed);
            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);
        }


        protected override void SetUpStateActions()
        {
            Configure(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), OnAutomatedCheckFailed);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.ProcessCancelled, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.ManualChecksPassed, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.ManualChecksFailed, Assignment.Create("tenants"), OnManualCheckFailed);
            ConfigureAsync(SoleToJointStates.BreachChecksPassed, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.BreachChecksFailed, Assignment.Create("tenants"), OnTenancyBreachCheckFailed);
            ConfigureAsync(SoleToJointStates.DocumentsRequestedAppointment, Assignment.Create("tenants"), OnRequestDocumentsAppointment);
        }

        private async Task OnAutomatedCheckFailed(UpdateProcessState processRequest)
        {
            AddIncomingTenantId(processRequest);

            await PublishProcessClosedEvent("Automatic eligibility check failed - process closed.");
        }

        private async Task OnManualCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessClosedEvent("Manual Eligibility Check failed - process closed.");
        }

        private async Task OnTenancyBreachCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessClosedEvent("Tenancy Breach Check failed - process closed.");
        }

        private async Task OnRequestDocumentsAppointment(UpdateProcessState processRequest)
        {
            processRequest.FormData.TryGetValue(SoleToJointFormDataKeys.AppointmentDateTime, out var appointmentDetails);
            if (appointmentDetails is null) throw new FormDataNotFoundException(processRequest.FormData.Keys.ToList(), new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });

            if (DateTime.TryParse(appointmentDetails.ToString(), out DateTime appointmentDateTime))
            {
                await PublishProcessUpdatedEvent($"Supporting Documents requested via an office appointment on {appointmentDateTime.ToString("dd/MM/yyyy hh:mm tt")}");
            }
            else
            {
                throw new FormDataFormatException("appointment datetime", appointmentDetails);
            }
        }
    }
}
