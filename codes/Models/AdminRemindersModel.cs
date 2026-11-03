namespace AnimalHospital.Models
{
    public class RmDue
    {
        public string Kind { get; set; } = string.Empty;
        public string RefId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Pref { get; set; } = string.Empty;
        public string ItemText { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public DateTime? BookedAt { get; set; }
        public DateTime? LastUtc { get; set; }
        public string LastHow { get; set; } = string.Empty;
        public string LastNote { get; set; } = string.Empty;
        public string LastBy { get; set; } = string.Empty;
    }

    public class RmContact
    {
        public string RemId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string ItemText { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public DateTime ContactedUtc { get; set; }
        public string How { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }
}
