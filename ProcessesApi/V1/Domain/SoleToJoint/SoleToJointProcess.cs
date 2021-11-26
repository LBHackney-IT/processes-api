using ProcessesApi.V1.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class SoleToJointProcess
    {
        public Guid Id { get; set; }

        private ProcessState<SoleToJointStates, SoleToJointTriggers> _currentState;
        public ProcessState<SoleToJointStates, SoleToJointTriggers> CurrentState => _currentState;

        public IList<ProcessState<SoleToJointStates, SoleToJointTriggers>> ProcessStates => _processStates;
        private readonly IList<ProcessState<SoleToJointStates, SoleToJointTriggers>> _processStates;

        public SoleToJointProcess(Guid id, IList<ProcessState<SoleToJointStates, SoleToJointTriggers>> processStates,
            ProcessState<SoleToJointStates, SoleToJointTriggers> currentState)
        {
            Id = id;
            _currentState = currentState;
            _processStates = processStates;
        }

        public Task AddState(ProcessState<SoleToJointStates, SoleToJointTriggers> state)
        {
            _currentState = state;
            ProcessStates.Add(state);

            return Task.CompletedTask;
        }

        public bool IsEligible()
        {
            var application = ProcessStates.FirstOrDefault(x => x.CurrentStateEnum == SoleToJointStates.CheckingEligibility);

            if (application == null)
                return false;

            var formData = JsonSerializer.Deserialize<SoleToJointFormData>(application.ProcessData.FormData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            var isEligible = formData.Married
                             && formData.HaveSecureTenancy
                             && formData.LivingTogether
                             && formData.NoNosp
                             && formData.NoOvercrowding
                             && formData.NoPersonalRentArrears
                             && formData.NoUnrequiredDisabledAccess
                             && formData.PartnerHasNoExistingTenancy
                             && formData.PartnerHasNoRentArrears
                             && formData.PartnerNeverBeenEvicted
                             && formData.PartnerNotSubjectToImmigrationControl;

            return isEligible;
        }

        public static SoleToJointProcess Create(Guid id,
           IList<ProcessState<SoleToJointStates, SoleToJointTriggers>> processStates,
           ProcessState<SoleToJointStates, SoleToJointTriggers> currentState)
        {
            return new SoleToJointProcess(id, processStates, currentState);
        }
    }
}
