namespace AnimalHospital.Models
{
    public class BktRow
    {
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string TeamKey { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string ShortText { get; set; } = string.Empty;
        public string LongText { get; set; } = string.Empty;
        public string Expect { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public int MinutesM { get; set; }
        public int MinutesL { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string DropFrom { get; set; } = string.Empty;
        public string DropTo { get; set; } = string.Empty;
        public string PickupHm { get; set; } = string.Empty;
        public string Preop { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool Online { get; set; }
        public bool NewOk { get; set; }
        public int BookOrder { get; set; }
        public bool Active { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public decimal? PriceM { get; set; }
        public decimal? PriceL { get; set; }
        public string RoomName { get; set; } = string.Empty;
        public int StaffCount { get; set; }
        public string StaffNames { get; set; } = string.Empty;
    }

    public class BktTeam
    {
        public string TeamKey { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class BktItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool Active { get; set; }
    }

    public class BktRoom
    {
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class BktStaff
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
    }

    public class BktSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string PluralName { get; set; } = string.Empty;
    }
}
