namespace AnimalHospital.Models
{
    public class PtListRow
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Photo { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? DeceasedDate { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime? LastVisit { get; set; }
        public int VaxDue { get; set; }
        public int VaxLate { get; set; }
    }

    public class PtPet
    {
        public string PetId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public bool HasSize { get; set; }
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Microchip { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? DeceasedDate { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerNo { get; set; } = string.Empty;
        public string OwnerPhone { get; set; } = string.Empty;
        public string OwnerAlert { get; set; } = string.Empty;
    }

    public class PtWeight
    {
        public string WeightId { get; set; } = string.Empty;
        public DateTime WeighedAt { get; set; }
        public decimal Lb { get; set; }
        public string VisitId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class PtVax
    {
        public string VaxId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string VaxName { get; set; } = string.Empty;
        public DateTime GivenDate { get; set; }
        public DateTime DueDate { get; set; }
        public string VisitId { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public int Newer { get; set; }
    }

    public class PtVaxItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public int VaxMonths { get; set; }
        public string Species { get; set; } = string.Empty;
    }

    public class PtSpecies
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public bool HasSize { get; set; }
    }

    public class PtBreed
    {
        public string SpeciesKey { get; set; } = string.Empty;
        public string BreedName { get; set; } = string.Empty;
    }

    public class PtOwner
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class PtApptRow
    {
        public string ApptId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public string BookName { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class PtVisitRow
    {
        public string VisitId { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class PtUseRow
    {
        public int Visits { get; set; }
        public int Appts { get; set; }
    }
}
