namespace AnimalHospital.Models
{
    public class PgMedia
    {
        public string MediaId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string ThumbPath { get; set; } = string.Empty;
        public int Seconds { get; set; }
        public DateTime AddedUtc { get; set; }
    }
}
