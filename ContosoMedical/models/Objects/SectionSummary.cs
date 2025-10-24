namespace PatientSummaryTool.Models.Objects
{
    public class SectionSummary
    {
        public string SectionName { get; set; }
        public string Summary { get; set; }
        public int Index { get; set; }
        public int Total { get; set; }
        public bool IsSuccess { get; set; }
    }
}
