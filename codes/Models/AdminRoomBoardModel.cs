namespace AnimalHospital.Models
{
    public class RbRoom
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int TypeOrder { get; set; }
        public int RoomOrder { get; set; }
        public string VisitId { get; set; } = string.Empty;
        public DateTime? InAt { get; set; }
        public string RoomStaffId { get; set; } = string.Empty;
        public string RoomStaffName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime? CheckinAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
    }

    public class RbWait
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public DateTime CheckinAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string Alert { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
    }
}
