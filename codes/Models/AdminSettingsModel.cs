namespace AnimalHospital.Models
{
    public class SetSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string PluralName { get; set; } = string.Empty;
        public bool HasSize { get; set; }
        public int SpeciesOrder { get; set; }
        public bool Active { get; set; }
        public int Pets { get; set; }
    }

    public class SetVax
    {
        public string ItemName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
    }
}
