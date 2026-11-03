namespace AnimalHospital.Models
{
    public class ClListRow
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public bool Active { get; set; }
        public string Pets { get; set; } = string.Empty;
        public DateTime? LastVisit { get; set; }
        public DateTime? NextAppt { get; set; }
        public decimal Balance { get; set; }
        public int Total { get; set; }
    }

    public class ClClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactPref { get; set; } = string.Empty;
        public string Address1 { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string CreatedName { get; set; } = string.Empty;
    }

    public class ClPetRow
    {
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string SpeciesName { get; set; } = string.Empty;
        public string Breed { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public bool Fixed { get; set; }
        public DateTime? BirthDate { get; set; }
        public string Alert { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? DeceasedDate { get; set; }
    }

    public class ClApptRow
    {
        public string ApptId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public string PetName { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ClVisitRow
    {
        public string VisitId { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public string PetName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }

    public class ClInvRow
    {
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Balance { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class ClDupRow
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientNo { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Phone2 { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class ClUseRow
    {
        public int Pets { get; set; }
        public int Visits { get; set; }
        public int Appts { get; set; }
        public int Invoices { get; set; }
        public int Payments { get; set; }
    }
}
