namespace AnimalHospital.Models
{
    public class RoType
    {
        public string TypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Sizes { get; set; } = string.Empty;
        public string NightItemId { get; set; } = string.Empty;
        public string NightName { get; set; } = string.Empty;
        public decimal NightPrice { get; set; }
        public string ShortText { get; set; } = string.Empty;
        public string LongText { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool Online { get; set; }
        public int TypeOrder { get; set; }
        public bool Active { get; set; }
        public int RoomCnt { get; set; }
        public int ActiveCnt { get; set; }
    }

    public class RoRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string TypeId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int RoomOrder { get; set; }
        public bool Active { get; set; }
        public int BusyCnt { get; set; }
        public string BusyPet { get; set; } = string.Empty;
    }

    public class RoUse
    {
        public string Kind { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public DateTime At { get; set; }
    }

    public class RoItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Species { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class RoSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string PluralName { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class RoRefs
    {
        public int Rooms { get; set; }
        public int BookTypes { get; set; }
        public int Appts { get; set; }
        public int Visits { get; set; }
        public int Staff { get; set; }
    }
}
