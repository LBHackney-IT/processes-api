namespace ProcessesApi.V1.Domain
{
    public static class TestStates
    {
        public const string StateOne = "StateOne";
        public const string Two = "Two";
        public const string ConditionalThreeA = "ConditionalThreeA";
        public const string ConditionalThreeB = "ConditionalThreeB";
    }

    public static class TestPermittedTriggers
    {
        public const string FirstTrigger = "FirstTrigger";
        public const string SecondTrigger = "SecondTrigger";
        public const string TriggerConditional = "TriggerConditional";
    }

    public static class TestInternalTriggers
    {
        public const string ConditionPassed = "ConditionPassed";
        public const string ConditionFailed = "ConditionFailed";
    }

}
