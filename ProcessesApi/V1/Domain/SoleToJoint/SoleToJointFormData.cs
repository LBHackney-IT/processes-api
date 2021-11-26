using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class SoleToJointFormData
    {
        public bool Married { get; set; }
        public bool LivingTogether { get; set; }
        public bool HaveSecureTenancy { get; set; }
        public bool NoPersonalRentArrears { get; set; }
        public bool NoNosp { get; set; }
        public bool PartnerHasNoExistingTenancy { get; set; }
        public bool PartnerHasNoRentArrears { get; set; }
        public bool PartnerNeverBeenEvicted { get; set; }
        public bool PartnerNotSubjectToImmigrationControl { get; set; }
        public bool NoOvercrowding { get; set; }
        public bool NoUnrequiredDisabledAccess { get; set; }
    }
}
