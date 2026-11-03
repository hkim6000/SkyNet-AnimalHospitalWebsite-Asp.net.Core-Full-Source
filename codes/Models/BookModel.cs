namespace AnimalHospital.Models
{
    public class BkClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class BkPet
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
    }
}
