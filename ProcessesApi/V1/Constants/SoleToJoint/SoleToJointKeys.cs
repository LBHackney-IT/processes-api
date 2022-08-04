using System.Diagnostics.CodeAnalysis;

namespace ProcessesApi.V1.Constants.SoleToJoint
{
    // NOTE: Key values must be camelCase to avoid issues with Json Serialiser in E2E tests
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class SoleToJointKeys
    {
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

        #endregion Manual Eligibility checks

        #region HO Tenancy breach checks

        /// <summary>
        ///     Is the tenant or proposed tenant a cautionary contact?
        /// </summary>
        public const string BR5 = "br5";

        /// <summary>
        ///    If Yes to above then Allow application to proceed or deny application
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

        /// <summary>
        /// Does the proposed tenant hold a tenancy/property elsewhere
        /// </summary>
        public const string BR9 = "br9";
        public const string ProposedTenantExistingPropertyOrTenure = "proposedTenantExistingPropertyOrTenure";

        #endregion

        #region ReviewDocuments

        /// <summary>
        ///     I confirm that the prespective tenant is not subject to immigration control under the
        ///     Asylum and Immigration Act 1996
        /// </summary>
        public const string IsNotInImmigrationControl = "isNotInImmigrationControl";

        /// <summary>
        ///     I confirm that I have seen proof of relationship to the existing tenant
        /// </summary>
        public const string SeenProofOfRelationship = "seenProofOfRelationship";

        /// <summary>
        ///     I confirm that I have seen 3 seperate documents proving the proposed tenant has been
        ///     living at the property for a minimum of 12 months
        /// </summary>
        public const string IncomingTenantLivingInProperty = "incomingTenantLivingInProperty";

        #endregion

        public const string NewTenureId = "newTenureId";
    }
}
