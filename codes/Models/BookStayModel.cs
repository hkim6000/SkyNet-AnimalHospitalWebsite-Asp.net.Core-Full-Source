namespace AnimalHospital.Models
{
    public class BsRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public int RoomOrder { get; set; }
    }

    public class BsBusy
    {
        public string RoomId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
    }

    public class BsVax
    {
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class BsClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
    }

    public class BsPet
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
    }

    public class BsPrice
    {
        public string ItemId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
    }
}
