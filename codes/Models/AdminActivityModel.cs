namespace AnimalHospital.Models
{
    public class AlRow
    {
        public long ActId { get; set; }
        public DateTime ActUtc { get; set; }
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; }
        public string Ip { get; set; } = string.Empty;
    }
}
