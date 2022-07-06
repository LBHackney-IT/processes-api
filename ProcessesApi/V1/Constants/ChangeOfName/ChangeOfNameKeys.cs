using System.Diagnostics.CodeAnalysis;

namespace ProcessesApi.V1.Constants.ChangeOfName
{
    // NOTE: Key values must be camelCase to avoid issues with Json Serialiser in E2E tests
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class ChangeOfNameKeys
    {
        public const string NameSubmitted = "nameSubmitted";

    }
}
