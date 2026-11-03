namespace AnimalHospital.Models
{
    public class TdAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime? ProcAt { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public bool NewClient { get; set; }
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class TdVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string ApptId { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime? RoomSince { get; set; }
    }

    public class TdRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int TypeOrder { get; set; }
        public int RoomOrder { get; set; }
        public string BusyVisit { get; set; } = string.Empty;
        public string BusyPet { get; set; } = string.Empty;
        public string BusyStaff { get; set; } = string.Empty;
    }

    public class TdPet
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int OpenVisits { get; set; }
    }
}
