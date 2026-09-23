namespace JobApplication.Application.Configuration
{
    public class JobSettings
    {
        public const string SectionName = "JobSettings";

        public int AutoCloseAfterDays { get; set; } = 30;
    }
}
