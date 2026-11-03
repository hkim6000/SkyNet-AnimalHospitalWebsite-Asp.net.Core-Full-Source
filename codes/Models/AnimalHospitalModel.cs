namespace AnimalHospital.Models
{
    public class StaffRow
    {
        public string StaffId { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool OwnOnly { get; set; }
        public string PassHash { get; set; } = string.Empty;
        public bool MustChange { get; set; }
        public bool IsDemo { get; set; }
        public bool Active { get; set; }
    }

    public class MenuRow
    {
        public string MenuKey { get; set; } = string.Empty;
        public string SectionKey { get; set; } = string.Empty;
        public string SectionName { get; set; } = string.Empty;
        public int SectionOrder { get; set; }
        public string MenuName { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public int MenuOrder { get; set; }
        public string Rights { get; set; } = string.Empty;
        public string ExtraName { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public bool ShowMenu { get; set; }
    }

    public class PermRow
    {
        public string MenuKey { get; set; } = string.Empty;
        public string Rights { get; set; } = string.Empty;
    }

    public class ActivityRow
    {
        public DateTime ActUtc { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public bool Ok { get; set; }
    }

    public class CountRow
    {
        public string K { get; set; } = string.Empty;
        public int N { get; set; }
    }

    public class BookRow
    {
        public string BooktypeId { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string TeamKey { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public int MinutesM { get; set; }
        public int MinutesL { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public string DropFrom { get; set; } = string.Empty;
        public string DropTo { get; set; } = string.Empty;
        public string PickupHm { get; set; } = string.Empty;
        public bool Online { get; set; }
        public bool NewOk { get; set; }
        public bool Active { get; set; }
    }

    public class HourRow
    {
        public string StaffId { get; set; } = string.Empty;
        public bool IsOn { get; set; }
        public string FromHm { get; set; } = string.Empty;
        public string ToHm { get; set; } = string.Empty;
        public string BreakFrom { get; set; } = string.Empty;
        public string BreakTo { get; set; } = string.Empty;
    }

    public class BusyRow
    {
        public string StaffId { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public DateTime FromAt { get; set; }
        public DateTime ToAt { get; set; }
    }

    public class FlowAppt
    {
        public string ApptId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string PetId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string BookName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string VisitId { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime? PickupAt { get; set; }
    }

    public class FlowVisit
    {
        public string VisitId { get; set; } = string.Empty;
        public string VisitNo { get; set; } = string.Empty;
        public string ApptId { get; set; } = string.Empty;
        public string PetName { get; set; } = string.Empty;
        public string VisitType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
    }

    public class RoomPick
    {
        public string RoomId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public string RoomtypeId { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Species { get; set; } = string.Empty;
        public int TypeOrder { get; set; }
        public int RoomOrder { get; set; }
        public string BusyVisit { get; set; } = string.Empty;
        public string BusyPet { get; set; } = string.Empty;
    }

    public class StaffPick
    {
        public string StaffId { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string HomeRoomId { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public bool OwnOnly { get; set; }
    }
}
