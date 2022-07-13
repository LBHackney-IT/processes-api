namespace ProcessesApi.V1.Constants
{
    public static class SharedKeys
    {
        #region Close or Cancel Process
        public const string HasNotifiedResident = "hasNotifiedResident";
        public const string Reason = "reason";
        public const string Comment = "comment";

        #endregion

        public const string AppointmentDateTime = "appointmentDateTime";

        #region Shared Review Documents

        /// <summary>
        ///     I confirm I have seen a government issue photographic ID
        /// </summary>
        public const string SeenPhotographicId = "seenPhotographicId";

        /// <summary>
        ///     I confirm I have seen a second form of ID (does not have to be photographic)
        /// </summary>
        public const string SeenSecondId = "seenSecondId";

        #endregion

    }
}
