namespace AnimalHospital.Models
{
    public class SiteInfo
    {
        public DateTime LoadedUtc { get; set; }
        public bool Partial { get; set; }
        public Dictionary<string, string> S { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public ClinicSettings Settings { get; set; } = new ClinicSettings();
        public List<SpeciesInfo> SpeciesList { get; set; } = new List<SpeciesInfo>();
        public List<Specialty> Specialties { get; set; } = new List<Specialty>();
        public List<CareService> Services { get; set; } = new List<CareService>();
        public List<Clinician> Providers { get; set; } = new List<Clinician>();
        public List<RunType> Runs { get; set; } = new List<RunType>();
        public List<AddOn> AddOns { get; set; } = new List<AddOn>();
        public List<Holiday> Holidays { get; set; } = new List<Holiday>();
        public List<Faq> Faqs { get; set; } = new List<Faq>();
        public List<MediaItem> Media { get; set; } = new List<MediaItem>();
        public List<Album> Albums { get; set; } = new List<Album>();
        public Dictionary<string, PageDoc> Pages { get; set; } = new Dictionary<string, PageDoc>();
        public int ClientCount { get; set; }

        public string SpeciesName(string key)
        {
            SpeciesInfo? x = SpeciesList.FirstOrDefault(s => s.Key == key);
            return x != null ? x.Name : key;
        }

        public string SpeciesPlural(string key)
        {
            SpeciesInfo? x = SpeciesList.FirstOrDefault(s => s.Key == key);
            return x != null ? (x.Plural != string.Empty ? x.Plural : x.Name + "s") : key;
        }

        public bool HasSize(string key)
        {
            return SpeciesList.Any(s => s.Key == key && s.HasSize);
        }

        public Specialty? Team(string key)
        {
            return Specialties.FirstOrDefault(x => x.Key == key);
        }

        public CareService? Service(string id)
        {
            return Services.FirstOrDefault(x => x.Key == id);
        }

        public Clinician? Provider(string id)
        {
            return Providers.FirstOrDefault(x => x.Id == id);
        }

        public RunType? Run(string id)
        {
            return Runs.FirstOrDefault(x => x.Key == id);
        }

        public MediaItem? MediaOf(string src)
        {
            return Media.FirstOrDefault(x => x.Full == src);
        }

        public List<Block> BlocksOf(string pageKey)
        {
            return Pages.TryGetValue(pageKey, out PageDoc? doc) && doc != null ? doc.Blocks : new List<Block>();
        }
    }

    public class SpeciesInfo
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Plural { get; set; } = string.Empty;
        public bool HasSize { get; set; }
        public int Order { get; set; }
    }

    public class ClinicSettings
    {
        public string Name { get; set; } = "Animal Hospital";
        public string Tagline { get; set; } = string.Empty;
        public string Town { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Urgent { get; set; } = string.Empty;
        public string ErName { get; set; } = string.Empty;
        public string ErPhone { get; set; } = string.Empty;
        public string ErAddress { get; set; } = string.Empty;
        public string Accent { get; set; } = "#3F7D6B";
        public string Theme { get; set; } = "light";
        public string TimeZone { get; set; } = "America/New_York";
        public bool Clock24 { get; set; }
        public int SlotMinutes { get; set; } = 15;
        public int MinNoticeHours { get; set; } = 2;
        public int BookAheadDays { get; set; } = 60;
        public int CancelHours { get; set; } = 24;
        public bool ShowPrices { get; set; } = true;
        public string ProviderWord { get; set; } = "Vet";
        public string ProvidersWord { get; set; } = "Vets";
        public string RoomWord { get; set; } = "Room";
        public string RoomsWord { get; set; } = "Rooms";
        public string PatientWord { get; set; } = "Client";
        public string PatientsWord { get; set; } = "Clients";
        public List<OpenDay> Hours { get; set; } = new List<OpenDay>();
        public List<string> Insurance { get; set; } = new List<string>();
        public List<string> Species { get; set; } = new List<string>();
        public string CheckInFrom { get; set; } = "07:30";
        public string CheckInTo { get; set; } = "18:00";
        public string CheckOutBy { get; set; } = "11:00";
        public int StayMaxNights { get; set; } = 30;
        public int StayCancelHours { get; set; } = 48;
        public List<VaxRule> BoardingVax { get; set; } = new List<VaxRule>();
        public List<VaxPlan> VaxChart { get; set; } = new List<VaxPlan>();
        public int RemindDays { get; set; } = 30;
        public int WellnessMonths { get; set; } = 12;
        public int MaxVideoMb { get; set; } = 25;
    }

    public class OpenDay
    {
        public int Day { get; set; }
        public bool Open { get; set; }
        public string From { get; set; } = "08:00";
        public string To { get; set; } = "18:00";
    }

    public class VaxRule
    {
        public string Species { get; set; } = string.Empty;
        public List<string> Required { get; set; } = new List<string>();
    }

    public class VaxPlan
    {
        public string Species { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string First { get; set; } = string.Empty;
        public string Every { get; set; } = string.Empty;
        public int Months { get; set; } = 12;
        public bool Core { get; set; } = true;
    }

    public class Specialty
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Blurb { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string Color { get; set; } = "#3F7D6B";
        public int Order { get; set; }
        public bool Active { get; set; } = true;
    }

    public class CareService
    {
        public string Key { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Short { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Kind { get; set; } = "visit";
        public string VisitType { get; set; } = "clinic";
        public int Minutes { get; set; } = 30;
        public int MinutesM { get; set; }
        public int MinutesL { get; set; }
        public string ItemId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PriceM { get; set; }
        public decimal PriceL { get; set; }
        public List<string> Species { get; set; } = new List<string>();
        public string DropFrom { get; set; } = string.Empty;
        public string DropTo { get; set; } = string.Empty;
        public string ProcFrom { get; set; } = string.Empty;
        public int ProcMinutes { get; set; }
        public string RoomKind { get; set; } = string.Empty;
        public string PickUp { get; set; } = string.Empty;
        public List<string> PreOp { get; set; } = new List<string>();
        public string Image { get; set; } = string.Empty;
        public List<string> Providers { get; set; } = new List<string>();
        public List<string> Expect { get; set; } = new List<string>();
        public bool Online { get; set; } = true;
        public bool NewOk { get; set; } = true;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
        public BookRow Book { get; set; } = new BookRow();
    }

    public class Clinician
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Role { get; set; } = "vet";
        public string Credentials { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public List<string> Teams { get; set; } = new List<string>();
        public string Bio { get; set; } = string.Empty;
        public List<string> Education { get; set; } = new List<string>();
        public List<string> Languages { get; set; } = new List<string>();
        public List<string> Species { get; set; } = new List<string>();
        public string Photo { get; set; } = string.Empty;
        public string Color { get; set; } = "#3F7D6B";
        public List<WorkDay> Hours { get; set; } = new List<WorkDay>();
        public List<TimeOff> TimeOff { get; set; } = new List<TimeOff>();
        public bool Accepting { get; set; } = true;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class WorkDay
    {
        public int Day { get; set; }
        public bool On { get; set; }
        public string From { get; set; } = "09:00";
        public string To { get; set; } = "17:00";
        public string BreakFrom { get; set; } = string.Empty;
        public string BreakTo { get; set; } = string.Empty;
    }

    public class TimeOff
    {
        public string Id { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    public class RunType
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Species { get; set; } = "dog";
        public List<string> SpeciesList { get; set; } = new List<string>();
        public List<string> Sizes { get; set; } = new List<string>();
        public string Short { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Count { get; set; }
        public string NightItemId { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Image { get; set; } = string.Empty;
        public string Color { get; set; } = "#3F7D6B";
        public bool Online { get; set; } = true;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class AddOn
    {
        public string Key { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool PerNight { get; set; }
        public string Species { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public int Order { get; set; }
    }

    public class Holiday
    {
        public DateTime Date { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Faq
    {
        public string Q { get; set; } = string.Empty;
        public string A { get; set; } = string.Empty;
    }

    public class MediaItem
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = "photo";
        public string Title { get; set; } = string.Empty;
        public string Alt { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string Full { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public int Seconds { get; set; }
        public int Pages { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long Bytes { get; set; }
        public string By { get; set; } = string.Empty;
        public DateTime When { get; set; }
    }

    public class Album
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Cover { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new List<string>();
        public DateTime Date { get; set; }
        public bool Published { get; set; } = true;
        public string By { get; set; } = string.Empty;
    }

    public class PageDoc
    {
        public string Key { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
        public DateTime Saved { get; set; }
        public string By { get; set; } = string.Empty;
    }

    public class PageVersion
    {
        public string Id { get; set; } = string.Empty;
        public DateTime When { get; set; }
        public string By { get; set; } = string.Empty;
        public string ByName { get; set; } = string.Empty;
        public List<Block> Blocks { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "text";
        public string Source { get; set; } = string.Empty;
        public bool Hidden { get; set; }
        public string Kicker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label2 { get; set; } = string.Empty;
        public string Link2 { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public string Extra { get; set; } = string.Empty;
        public List<BlockItem> Items { get; set; } = new List<BlockItem>();
    }

    public class BlockItem
    {
        public string Icon { get; set; } = string.Empty;
        public string Media { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
