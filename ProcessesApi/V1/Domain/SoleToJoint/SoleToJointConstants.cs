using System.Diagnostics.CodeAnalysis;

namespace ProcessesApi.V1.Domain
{
    public static class SoleToJointStates
    {
        public const string SelectTenants = "SelectTenants";
        public const string AutomatedChecksFailed = "AutomatedChecksFailed";
        public const string AutomatedChecksPassed = "AutomatedChecksPassed";
        public const string ProcessCancelled = "ProcessCancelled";
        public const string ManualChecksFailed = "ManualChecksFailed";
        public const string ManualChecksPassed = "ManualChecksPassed";
        public const string BreachChecksFailed = "BreachChecksFailed";
        public const string BreachChecksPassed = "BreachChecksPassed";
        public const string DocumentsRequestedDes = "DocumentsRequestedDes";
        public const string DocumentsRequestedAppointment = "DocumentsRequestedAppointment";
        public const string DocumentsAppointmentRescheduled = "DocumentsAppointmentRescheduled";
        public const string DocumentChecksPassed = "DocumentChecksPassed";
    }

    public static class SoleToJointPermittedTriggers
    {
        public const string CheckAutomatedEligibility = "CheckAutomatedEligibility";
        public const string CancelProcess = "CancelProcess";
        public const string CheckManualEligibility = "CheckManualEligibility";
        public const string RequestDocuments = "RequestDocuments";
        public const string CheckTenancyBreach = "CheckTenancyBreach";
        public const string RequestDocumentsDes = "RequestDocumentsDes";
        public const string RequestDocumentsAppointment = "RequestDocumentsAppointment";
        public const string RescheduleDocumentsAppointment = "RescheduleDocumentsAppointment";
        public const string SubmitApplication = "SubmitApplication";
        public const string ReviewDocuments = "ReviewDocuments";


    }

    public static class SoleToJointInternalTriggers
    {
        public const string EligibiltyPassed = "EligibiltyPassed";
        public const string EligibiltyFailed = "EligibiltyFailed";
        public const string ManualEligibilityFailed = "ManualEligibilityFailed";
        public const string ManualEligibilityPassed = "ManualEligibilityPassed";
        public const string BreachChecksFailed = "BreachChecksFailed";
        public const string BreachChecksPassed = "BreachChecksPassed";
        public const string DocumentChecksPassed = "DocumentChecksPassed";

    }

    // NOTE: Form data key values must be camelCase to avoid issues with Json Serialiser in E2E tests
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class SoleToJointFormDataKeys
    {
        public const string AppointmentDateTime = "appointmentDateTime";

        #region Automated eligibility checks

        /// <summary>
        ///     The ID of the proposed tenant
        /// </summary>
        public const string IncomingTenantId = "incomingTenantId";

        /// <summary>
        ///     The ID of the current tenant
        /// </summary>
        public const string TenantId = "tenantId";

        #endregion

        #region Manual eligibility checks

        /// <summary>
        ///     Have the tenant and proposed tenant been living together for 12 months or more, or are they married or in a civil
        ///     partnership?
        /// </summary>
        public const string BR11 = "br11";

        /// <summary>
        ///     Does the tenant or the proposed tenant hold or intend to hold any other property or tenancy besides this one, as
        ///     their only or main home?
        /// </summary>
        public const string BR12 = "br12";

        /// <summary>
        ///     is the tenant a survivor of one of more joint tenants?
        /// </summary>
        public const string BR13 = "br13";

        /// <summary>
        ///     Has the proposed tenant been evicted by London Borough of Hackney or any other local authority or housing
        ///     association?
        /// </summary>
        public const string BR15 = "br15";

        /// <summary>
        ///     Is the proposed tenant subject to immigration control under the Asylum And Immigration Act 1996?
        /// </summary>
        public const string BR16 = "br16";

        /// <summary>
        /// Does the tenant have a live notice seeking possession?
        /// </summary>
        public const string BR8 = "br8";

        /// <summary>
        /// Does the tenant have rent arrears over £500?
        /// </summary>
        public const string BR7 = "br7";

        #endregion ManualeEligibility checks

        #region HO Tenancy breach checks

        /// <summary>
        ///     Is the tenant or proposed tenant a cautionary contact?
        /// </summary>
        public const string BR5 = "br5";

        /// <summary>
        ///     Does the tenant have rent arrears remaining with LBH or another local authority or housing association property?
        /// </summary>
        public const string BR10 = "br10";

        /// <summary>
        ///     Has the tenure previously been succeeded?
        /// </summary>
        public const string BR17 = "br17";

        /// <summary>
        ///     Other than a NOSP, does the tenant have any live notices against the tenure, e.g. a breach of tenancy?
        /// </summary>
        public const string BR18 = "br18";

        #endregion

        #region ReviewDocuments

        /// <summary>
        ///     I confirm I have seen a government issue photographic ID
        /// </summary>
        public const string SeenPhotographicId = "SeenPhotographicId";

        /// <summary>
        ///     I confirm I have seen a second form of ID (does not have to be photographic)
        /// </summary>
        public const string SeenSecondId = "SeenSecondId";

        /// <summary>
        ///     I confirm that the prespective tenant is not subject to immigration control under the
        ///     Asylum and Immigration Act 1996
        /// </summary>
        public const string IsNotInImmigrationControl = "IsNotInImmigrationControl";

        /// <summary>
        ///     I confirm that I have seen proof of relationship to the existing tenant
        /// </summary>
        public const string SeenProofOfRelationship = "SeenProofOfRelationship";

        /// <summary>
        ///     I confirm that I have seen 3 seperate documents proving the proposed tenant has been
        ///     living at the property for a minimum of 12 months
        /// </summary>
        public const string IncomingTenantLivingInProperty = "IncomingTenantLivingInProperty";








        #endregion
    }

}
