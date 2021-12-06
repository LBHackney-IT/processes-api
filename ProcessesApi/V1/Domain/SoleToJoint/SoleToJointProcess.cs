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

        public ProcessState CurrentState { get; set; }
        public List<ProcessState> PreviousStates { get; set; }
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public string ProcessName { get; set; }
        public int? VersionNumber { get; set; }

        public SoleToJointProcess() { }

        public SoleToJointProcess(Guid id, List<ProcessState> previousStates,
            ProcessState currentState, Guid targetId,
            List<Guid> relatedEntities, string processName, int? versionNumber)
        {
            Id = id;
            CurrentState = currentState;
            PreviousStates = previousStates;
            TargetId = targetId;
            RelatedEntities = relatedEntities;
            ProcessName = processName;
            VersionNumber = versionNumber;
        }

        public Task AddState(ProcessState state)
        {
            if (CurrentState != null) PreviousStates.Add(CurrentState);
            CurrentState = state;

            return Task.CompletedTask;
        }

        public bool IsEligible()
        {
            var application = PreviousStates.FirstOrDefault(x => x.State == SoleToJointStates.SelectTenants);

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
           List<ProcessState> previousStates,
           ProcessState currentState, Guid targetId,
           List<Guid> relatedEntities, string processName, int? versionNumber)
        {

            return new SoleToJointProcess(id, previousStates, currentState, targetId, relatedEntities, processName, versionNumber);
        }
    }
}
