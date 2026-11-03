namespace AnimalHospital.Models
{
    public class SfStaff
    {
        public string StaffId { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string StaffName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public string Credentials { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public string Education { get; set; } = string.Empty;
        public string Languages { get; set; } = string.Empty;
        public string Photo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string HomeRoomId { get; set; } = string.Empty;
        public string HomeRoomName { get; set; } = string.Empty;
        public bool OwnOnly { get; set; }
        public bool ShowWeb { get; set; }
        public int WebOrder { get; set; }
        public bool Accepting { get; set; }
        public bool IsDemo { get; set; }
        public bool Active { get; set; }
        public bool MustChange { get; set; }
        public DateTime? LastLogin { get; set; }
        public int Sessions { get; set; }
        public bool HasStaff { get; set; }
    }

    public class SfRole
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int RoleOrder { get; set; }
        public int Used { get; set; }
    }

    public class SfPick
    {
        public string K { get; set; } = string.Empty;
        public string T { get; set; } = string.Empty;
    }
}
