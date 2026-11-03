namespace AnimalHospital.Models
{
    public class RsLine
    {
        public string LineId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string VisitStatus { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrderedName { get; set; } = string.Empty;
        public DateTime OrderedUtc { get; set; }
    }

    public class RsRecent
    {
        public string LineId { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public DateTime EnteredUtc { get; set; }
        public string EnteredName { get; set; } = string.Empty;
        public int Flagged { get; set; }
    }

    public class RsFlag
    {
        public string LineId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public decimal? ValueNum { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
    }

    public class RsField
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

    public class RsResult
    {
        public string FieldId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public decimal? ValueNum { get; set; }
        public string ValueText { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Flag { get; set; } = string.Empty;
        public DateTime EnteredUtc { get; set; }
    }

    public class RsRv
    {
        public string K { get; set; } = string.Empty;
        public string V { get; set; } = string.Empty;
    }
}
