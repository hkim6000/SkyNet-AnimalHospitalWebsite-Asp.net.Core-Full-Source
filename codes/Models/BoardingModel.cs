namespace AnimalHospital.Models
{
    public class BoNightRow
    {
        public string RoomtypeId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime FromAt { get; set; }
        public DateTime ToAt { get; set; }
    }
}
