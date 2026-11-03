namespace AnimalHospital.Models
{
    public class VsVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string ApptId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? RecheckDate { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Size { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public DateTime? DeceasedDate { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerFirst { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerEmail { get; set; } = string.Empty;
        public string OwnerAlert { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string ApptCode { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime? RoomSince { get; set; }
        public decimal? WeightLb { get; set; }
        public DateTime? WeighedAt { get; set; }
        public int Billed { get; set; }
    }

    public class VsCard
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public DateTime? RoomSince { get; set; }
    }

    public class VsNote
    {
        public string NoteId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
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
        public string SignedName { get; set; } = string.Empty;
    }

    public class VsDx
    {
        public string DxId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string DxText { get; set; } = string.Empty;
        public string DxStatus { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string VisitNo { get; set; } = string.Empty;
        public DateTime? CheckinAt { get; set; }
    }

    public class VsLine
    {
        public string LineId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string Dispense { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string OrderedName { get; set; } = string.Empty;
        public DateTime OrderedUtc { get; set; }
        public string DoneName { get; set; } = string.Empty;
        public DateTime? DoneUtc { get; set; }
        public int LineOrder { get; set; }
        public int ResultCount { get; set; }
    }

    public class VsItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
        public bool Taxable { get; set; }
        public string Dose { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public int VaxMonths { get; set; }
        public string Species { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class VsField
    {
        public string FieldId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public decimal? LowValue { get; set; }
        public decimal? HighValue { get; set; }
        public int Decimals { get; set; }
        public bool IsText { get; set; }
        public int FieldOrder { get; set; }
    }

    public class VsResult
    {
        public string FieldId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public decimal? ValueNum { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal? LowValue { get; set; }
        public decimal? HighValue { get; set; }
        public string Flag { get; set; } = string.Empty;
        public string EnteredName { get; set; } = string.Empty;
        public DateTime EnteredUtc { get; set; }
    }

    public class VsRv
    {
        public string K { get; set; } = string.Empty;
        public string V { get; set; } = string.Empty;
    }

    public class VsFile
    {
        public string FileId { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Mime { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string UploaderName { get; set; } = string.Empty;
        public DateTime UploadedUtc { get; set; }
        public bool SendOwner { get; set; }
        public string StoredName { get; set; } = string.Empty;
    }

    public class VsPrevVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class VsVax
    {
        public string VaxId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }
}
