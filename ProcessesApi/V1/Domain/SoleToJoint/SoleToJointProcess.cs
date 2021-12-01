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

        public ProcessState<SoleToJointStates, SoleToJointTriggers> _currentState;
        public ProcessState<SoleToJointStates, SoleToJointTriggers> CurrentState  => _currentState;

        public List<ProcessState<SoleToJointStates, SoleToJointTriggers>> PreviousStates => _previousStates;
        private readonly List<ProcessState<SoleToJointStates, SoleToJointTriggers>> _previousStates;
        public Guid? TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public string ProcessName { get; set; }
        public int? VersionNumber { get; set; }

        public SoleToJointProcess(Guid id, List<ProcessState<SoleToJointStates, SoleToJointTriggers>> previousStates,
            ProcessState<SoleToJointStates, SoleToJointTriggers> currentState, Guid? targetId,
            List<Guid> relatedEntities, string processName, int? versionNumber)
        {
            Id = id;
            _currentState = currentState;
            _previousStates = previousStates;
            TargetId = targetId;
            RelatedEntities = relatedEntities;
            ProcessName = processName;
            VersionNumber = versionNumber;
        }

        public Task AddState(ProcessState<SoleToJointStates, SoleToJointTriggers> state)
        {
            _currentState = state;

            PreviousStates.Add(state);

            return Task.CompletedTask;
        }

        public bool IsEligible()
        {
            var application = PreviousStates.FirstOrDefault(x => x.CurrentStateEnum == SoleToJointStates.SelectTenants);

            if (application == null)
                return false;

            //var formData = JsonSerializer.Deserialize<SoleToJointFormData>(application.ProcessData.FormData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

            //var isEligible = formData.Married
            //                 && formData.HaveSecureTenancy
            //                 && formData.LivingTogether
            //                 && formData.NoNosp
            //                 && formData.NoOvercrowding
            //                 && formData.NoPersonalRentArrears
            //                 && formData.NoUnrequiredDisabledAccess
            //                 && formData.PartnerHasNoExistingTenancy
            //                 && formData.PartnerHasNoRentArrears
            //                 && formData.PartnerNeverBeenEvicted
            //                 && formData.PartnerNotSubjectToImmigrationControl;

            return true;
        }

        public static SoleToJointProcess Create(Guid id,
           List<ProcessState<SoleToJointStates, SoleToJointTriggers>> previousStates,
           ProcessState<SoleToJointStates, SoleToJointTriggers> currentState, Guid? targetId,
           List<Guid> relatedEntities, string processName, int? versionNumber)
        {
            
            return new SoleToJointProcess(id, previousStates, currentState, targetId, relatedEntities, processName, versionNumber);
        }
    }
}
