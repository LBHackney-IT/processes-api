namespace ProcessesApi.V1.Domain.Finance
{
    public class Tenancy
    {
        public string TenancyRef { get; set; }
        public NoticeOfSeekingPossession nosp { get; set; }
    }

    public class NoticeOfSeekingPossession
    {
        public bool active { get; set; }
    }
}
