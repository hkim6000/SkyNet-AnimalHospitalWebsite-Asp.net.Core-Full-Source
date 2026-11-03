namespace AnimalHospital.Models
{
    public class ApRow
    {
        public string ApptId { get; set; } = string.Empty;
        public string ApptCode { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Minutes { get; set; }
        public DateTime? ProcAt { get; set; }
        public int ProcMinutes { get; set; }
        public DateTime? PickupAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public decimal EstPrice { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string PetAlert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    public class ApDetail
    {
        public string ApptId { get; set; } = string.Empty;
        public string ApptCode { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Minutes { get; set; }
        public DateTime? ProcAt { get; set; }
        public int ProcMinutes { get; set; }
        public DateTime? PickupAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public decimal EstPrice { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string RoomType { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string DeskNote { get; set; } = string.Empty;
        public string CancelReason { get; set; } = string.Empty;
        public bool NewClient { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedName { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string PetAlert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactPref { get; set; } = string.Empty;
        public string ClientAlert { get; set; } = string.Empty;
    }

    public class ApLog
    {
        public DateTime LogUtc { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string LogText { get; set; } = string.Empty;
    }

    public class ApPet
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ClientAlert { get; set; } = string.Empty;
    }

    public class ApPerson
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class ApBook
    {
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
    }

    public class ApVax
    {
        public string VaxName { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
    }

    public class ApSame
    {
        public string ApptId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime? ProcAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
