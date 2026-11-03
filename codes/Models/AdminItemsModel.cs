namespace AnimalHospital.Models
{
    public class ItmRow
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
        public bool TrackStock { get; set; }
        public int ItemOrder { get; set; }
        public string Note { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class ItmUse
    {
        public int Orders { get; set; }
        public int Recent { get; set; }
        public int InvLines { get; set; }
        public int Books { get; set; }
        public int Rooms { get; set; }
        public int Fields { get; set; }
        public string BookNames { get; set; } = string.Empty;
        public string RoomNames { get; set; } = string.Empty;
    }

    public class ItmSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public bool HasSize { get; set; }
    }

    public class ItmCount
    {
        public string ItemType { get; set; } = string.Empty;
        public int Cnt { get; set; }
    }

    public class ItmChange
    {
        public ItmRow Row { get; set; } = new ItmRow();
        public decimal NewPrice { get; set; }
        public decimal NewM { get; set; }
        public decimal NewL { get; set; }
        public bool Skip { get; set; }
        public bool Changed => !Skip && (NewPrice != Row.Price || NewM != Row.PriceM || NewL != Row.PriceL);
    }
}
