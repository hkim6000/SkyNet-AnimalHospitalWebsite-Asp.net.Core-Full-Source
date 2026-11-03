namespace AnimalHospital.Models
{
    public class TstTest
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public bool Active { get; set; }
        public int Fields { get; set; }
    }

    public class TstField
    {
        public string FieldId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string FieldName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public decimal? LowValue { get; set; }
        public decimal? HighValue { get; set; }
        public int Decimals { get; set; }
        public bool IsText { get; set; }
        public int FieldOrder { get; set; }
        public int Results { get; set; }
    }

    public class TstSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
    }
}
