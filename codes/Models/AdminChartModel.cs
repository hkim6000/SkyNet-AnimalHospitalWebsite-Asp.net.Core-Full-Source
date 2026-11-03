namespace AnimalHospital.Models
{
    public class ChPet
    {
        public string PetId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? DeceasedDate { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerAlert { get; set; } = string.Empty;
    }

    public class ChFind
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? DeceasedDate { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public DateTime? SeenAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ChVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? PlanOut { get; set; }
        public DateTime? RecheckDate { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    public class ChNote
    {
        public string NoteId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string NoteKind { get; set; } = string.Empty;
        public string Subjective { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string Assessment { get; set; } = string.Empty;
        public string PlanText { get; set; } = string.Empty;
        public decimal? TempF { get; set; }
        public int? HeartRate { get; set; }
        public int? RespRate { get; set; }
        public decimal? WeightLb { get; set; }
        public string ParentId { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime? SignedUtc { get; set; }
    }

    public class ChDx
    {
        public string DxId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string DxText { get; set; } = string.Empty;
        public string DxStatus { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime VisitAt { get; set; }
    }

    public class ChOrder
    {
        public string LineId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Dose { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Dispense { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime VisitAt { get; set; }
    }

    public class ChResult
    {
        public string LineId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public decimal? ValueNum { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? LowValue { get; set; }
        public decimal? HighValue { get; set; }
        public string Flag { get; set; } = string.Empty;
        public int FieldOrder { get; set; }
        public int Decimals { get; set; }
        public DateTime VisitAt { get; set; }
    }

    public class ChFile
    {
        public string FileId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Mime { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTime UploadedUtc { get; set; }
    }

    public class ChWeight
    {
        public DateTime WeighedAt { get; set; }
        public decimal Lb { get; set; }
    }

    public class ChVax
    {
        public string VaxId { get; set; } = string.Empty;
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class ChAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public string BookName { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
