namespace AnimalHospital.Models
{
    public class SchedAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public DateTime? ProcAt { get; set; }
        public int ProcMinutes { get; set; }
        public DateTime? PickupAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string VisitStatus { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
    }

    public class SchedOff
    {
        public string StaffId { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
