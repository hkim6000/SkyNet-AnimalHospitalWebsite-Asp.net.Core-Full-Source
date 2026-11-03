namespace AnimalHospital.Models
{
    public class HrPerson
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }

    public class HrDay
    {
        public string StaffId { get; set; } = string.Empty;
        public int DayNo { get; set; }
        public bool IsOn { get; set; }
        public string FromHm { get; set; } = string.Empty;
        public string ToHm { get; set; } = string.Empty;
        public string BreakFrom { get; set; } = string.Empty;
        public string BreakTo { get; set; } = string.Empty;
    }

    public class HrOff
    {
        public string TimeoffId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string Note { get; set; } = string.Empty;
        public int Clash { get; set; }
    }

    public class HrHoliday
    {
        public DateTime HolidayDate { get; set; }
        public string HolidayName { get; set; } = string.Empty;
        public int Booked { get; set; }
    }

    public class HrAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
    }
}
