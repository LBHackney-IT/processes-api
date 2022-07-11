namespace ProcessesApi.V1.Domain.Finance
{
    public class Tenancy
    {
        public string TenancyRef { get; set; }
        public NoticeOfSeekingPossession NOSP { get; set; }
    }

    public class NoticeOfSeekingPossession
    {
        public bool Active { get; set; }
    }
}
