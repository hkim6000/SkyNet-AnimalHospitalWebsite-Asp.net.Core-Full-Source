namespace AnimalHospital.Models
{
    public class BdRun
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Sizes { get; set; } = string.Empty;
        public string NightItemId { get; set; } = string.Empty;
        public int TypeOrder { get; set; }
        public int RoomOrder { get; set; }
        public bool Live { get; set; }
    }

    public class BdStay
    {
        public string ApptId { get; set; } = string.Empty;
        public string ApptCode { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string PetAlert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string OwnerFirst { get; set; } = string.Empty;
        public string OwnerLast { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ClientAlert { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public string Addons { get; set; } = string.Empty;
        public decimal EstPrice { get; set; }
        public bool VaxPending { get; set; }
        public string Note { get; set; } = string.Empty;
        public string DeskNote { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string CancelReason { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitStatus { get; set; } = string.Empty;
        public DateTime? CheckinAt { get; set; }
        public DateTime? PlanOut { get; set; }
        public DateTime? ReadyAt { get; set; }
        public string CurRoomId { get; set; } = string.Empty;
        public string CurRoomName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    public class BdSeg
    {
        public string VisitId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public DateTime InAt { get; set; }
        public DateTime? OutAt { get; set; }
    }

    public class BdItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
        public bool Taxable { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public bool Active { get; set; }
        public int ItemOrder { get; set; }
    }

    public class BdPet
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string ClientAlert { get; set; } = string.Empty;
    }

    public class BdVaxRow
    {
        public string PetId { get; set; } = string.Empty;
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class BdLog
    {
        public DateTime LogUtc { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string LogText { get; set; } = string.Empty;
    }

    public class BdNote
    {
        public string NoteId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string Subjective { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
    }

    public class BdBusy
    {
        public string RoomId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
    }

    public class BdDay
    {
        public DateTime HolidayDate { get; set; }
        public string HolidayName { get; set; } = string.Empty;
    }
}
