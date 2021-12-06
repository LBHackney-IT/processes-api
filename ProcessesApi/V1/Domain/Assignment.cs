namespace ProcessesApi.V1.Domain
{
    public class Assignment
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Patch { get; set; }
        public Assignment()
        { }
        public Assignment(string patch)
        {
            Patch = patch;
        }

        public static Assignment Create(string patch)
        {
            return new Assignment(patch);
        }
    }
}
