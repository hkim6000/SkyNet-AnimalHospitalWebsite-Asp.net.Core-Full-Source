namespace AnimalHospital.Models
{
    public class MaAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public string ApptCode { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public int Minutes { get; set; }
        public DateTime? ProcAt { get; set; }
        public int ProcMinutes { get; set; }
        public DateTime? PickupAt { get; set; }
        public string Addons { get; set; } = string.Empty;
        public decimal EstPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string VisitStatus { get; set; } = string.Empty;
        public bool VaxPending { get; set; }
        public string Note { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
    }

    public class MaLog
    {
        public DateTime LogUtc { get; set; }
        public string LogText { get; set; } = string.Empty;
    }

    public class MaRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public int RoomOrder { get; set; }
        public string RoomName { get; set; } = string.Empty;
    }

    public class MaBusy
    {
        public string RoomId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
    }

    public class MaVax
    {
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class MaItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
    }
}
