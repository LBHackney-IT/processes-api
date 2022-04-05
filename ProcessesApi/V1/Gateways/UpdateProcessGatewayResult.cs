using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Gateways
{
    public class UpdateProcessGatewayResult
    {
        public UpdateProcessGatewayResult(Process old, Process updated)
        {
            OldProcess = old;
            UpdatedProcess = updated;
        }

        public Process OldProcess { get; private set; }
        public Process UpdatedProcess { get; private set; }
    }
}
