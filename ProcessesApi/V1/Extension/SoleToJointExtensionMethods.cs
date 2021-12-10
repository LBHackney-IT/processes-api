using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Helper
{
    public static class SoleToJointExtensionMethods
    {
        public static bool IsEligible(this Process process)
        {
            if (process.CurrentState == null)
                return false;
            if (process.CurrentState.State != SoleToJointStates.SelectTenants)
                return false;

            //TODO: Implement auto checks here
            return true;
        }

    }
}
