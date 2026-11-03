using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public static class Site
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, (DateTime At, long Ver, DateTime? Start, string StaffId)> NextCache = new ConcurrentDictionary<string, (DateTime, long, DateTime?, string)>();
        private static SiteInfo? cached;
        private static long version;
        public const int CacheSeconds = 30;

        public static readonly string[] DayShort = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public static readonly (string Href, string Name)[] Links = { ("Services", "Services"), ("Vets", "Vets"), ("Boarding", "Boarding"), ("PetOwners", "Pet owners"), ("Contact", "Contact") };

        public static void Forget()
        {
            Interlocked.Increment(ref version);
            cached = null;
            NextCache.Clear();
        }

        public static async Task<SiteInfo> Load(bool fresh = false)
        {
            await App.DemoDaily();
            SiteInfo? c = cached;
            if (!fresh && c != null && c.LoadedUtc > DateTime.UtcNow.AddSeconds(-CacheSeconds))
            {
                return c;
            }
            await Gate.WaitAsync();
            try
            {
                c = cached;
                if (!fresh && c != null && c.LoadedUtc > DateTime.UtcNow.AddSeconds(-CacheSeconds))
                {
                    return c;
                }
                long v = Interlocked.Read(ref version);
                SiteInfo info = await Build();
                if (info.Partial)
                {
                    return cached ?? info;
                }
                if (Interlocked.Read(ref version) == v)
                {
                    cached = info;
                }
                return info;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static string Str(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToString(r[col], CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        }

        private static int Int(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToInt32(r[col], CultureInfo.InvariantCulture) : 0;
        }

        private static long Long(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToInt64(r[col], CultureInfo.InvariantCulture) : 0;
        }

        private static decimal Dec(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDecimal(r[col], CultureInfo.InvariantCulture) : 0m;
        }

        private static bool Bit(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value && Convert.ToBoolean(r[col], CultureInfo.InvariantCulture);
        }

        private static DateTime Day(DataRow r, string col)
        {
            return r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? Convert.ToDateTime(r[col], CultureInfo.InvariantCulture) : DateTime.MinValue;
        }

        private sealed class BuildState
        {
            public bool Failed { get; set; }
        }

        private static async Task<List<DataRow>> Rows(BuildState st, string sql, List<SqlParameter>? ps = null)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, ps);
            if (emsg != string.Empty || dt == null)
            {
                st.Failed = true;
                return new List<DataRow>();
            }
            return dt.Rows.Cast<DataRow>().ToList();
        }

        public static List<string> Lines(string? v)
        {
            return (v ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
        }

        public static List<string> Csv(string? v)
        {
            return (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(x => x != string.Empty).Distinct().ToList();
        }

        public static List<OpenDay> ParseHours(string? v)
        {
            List<OpenDay> list = new List<OpenDay>();
            foreach (string part in (v ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split('|');
                if (f.Length >= 4 && int.TryParse(f[0], NumberStyles.Integer, Ui.Inv, out int d) && d >= 0 && d <= 6 && !list.Any(x => x.Day == d))
                {
                    bool ok = Ui.ParseHm(f[2]) != null && Ui.ParseHm(f[3]) != null && Ui.ParseHm(f[3]) > Ui.ParseHm(f[2]);
                    list.Add(new OpenDay { Day = d, Open = f[1] == "1" && ok, From = ok ? f[2] : "08:00", To = ok ? f[3] : "18:00" });
                }
            }
            for (int d = 0; d <= 6; d++)
            {
                if (!list.Any(x => x.Day == d))
                {
                    list.Add(new OpenDay { Day = d, Open = false });
                }
            }
            return list.OrderBy(x => x.Day).ToList();
        }

        public static List<Faq> ParseFaqs(string? json)
        {
            List<Faq> list = new List<Faq>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return list;
                }
                foreach (JsonElement e in doc.RootElement.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    string q = Prop(e, "q");
                    string a = Prop(e, "a");
                    if (q != string.Empty || a != string.Empty)
                    {
                        list.Add(new Faq { Q = q, A = a });
                    }
                }
            }
            catch (JsonException)
            {
                return new List<Faq>();
            }
            return list;
        }

        public static List<VaxPlan> ParseVaxChart(string? json)
        {
            List<VaxPlan> list = new List<VaxPlan>();
            try
            {
                using JsonDocument doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return list;
                }
                foreach (JsonElement e in doc.RootElement.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }
                    int months = e.TryGetProperty("months", out JsonElement m) && m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out int mv) ? mv : 12;
                    bool core = e.TryGetProperty("core", out JsonElement c) && c.ValueKind == JsonValueKind.True;
                    VaxPlan p = new VaxPlan { Species = Prop(e, "species"), Name = Prop(e, "name"), First = Prop(e, "first"), Every = Prop(e, "every"), Months = months, Core = core };
                    if (p.Name != string.Empty)
                    {
                        list.Add(p);
                    }
                }
            }
            catch (JsonException)
            {
                return new List<VaxPlan>();
            }
            return list;
        }

        private static string Prop(JsonElement e, string name)
        {
            return e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty).Trim() : string.Empty;
        }

        public static string FaqJson(IEnumerable<Faq> list)
        {
            return JsonSerializer.Serialize(list.Select(f => new { q = f.Q, a = f.A }));
        }

        public static string VaxJson(IEnumerable<VaxPlan> list)
        {
            return JsonSerializer.Serialize(list.Select(p => new { species = p.Species, name = p.Name, first = p.First, every = p.Every, months = p.Months, core = p.Core }));
        }

        private static async Task<SiteInfo> Build()
        {
            SiteInfo info = new SiteInfo { LoadedUtc = DateTime.UtcNow };
            BuildState bs = new BuildState();
            Dictionary<string, string> s = await App.Settings();
            if (s.Count == 0)
            {
                bs.Failed = true;
            }
            info.S = s;
            foreach (DataRow r in await Rows(bs, "SELECT SPECIES_KEY, SPECIES_NAME, PLURAL_NAME, HAS_SIZE, SPECIES_ORDER FROM SPECIES WHERE ACTIVE = 1 ORDER BY SPECIES_ORDER, SPECIES_NAME"))
            {
                info.SpeciesList.Add(new SpeciesInfo { Key = Str(r, "SPECIES_KEY"), Name = Str(r, "SPECIES_NAME"), Plural = Str(r, "PLURAL_NAME"), HasSize = Bit(r, "HAS_SIZE"), Order = Int(r, "SPECIES_ORDER") });
            }
            ClinicSettings st = info.Settings;
            st.Name = App.S(s, "name", "Animal Hospital");
            st.Tagline = App.S(s, "tagline");
            st.Town = App.S(s, "town");
            st.Address = App.S(s, "address");
            st.Phone = App.S(s, "phone");
            st.Email = App.S(s, "email");
            st.Urgent = App.S(s, "urgent");
            st.ErName = App.S(s, "er.name");
            st.ErPhone = App.S(s, "er.phone");
            st.ErAddress = App.S(s, "er.address");
            st.Accent = Ui.SafeColor(App.S(s, "accent")) ? App.S(s, "accent") : "#3F7D6B";
            st.Theme = App.S(s, "theme") == "dark" ? "dark" : "light";
            st.TimeZone = App.S(s, "timezone", "America/New_York");
            st.Clock24 = App.S(s, "clock24") == "1";
            st.SlotMinutes = Math.Clamp(App.SInt(s, "slot.minutes", 15), 5, 60);
            st.MinNoticeHours = Math.Clamp(App.SInt(s, "book.notice.hours", 2), 0, 240);
            st.BookAheadDays = Math.Clamp(App.SInt(s, "book.ahead.days", 60), 1, 365);
            st.CancelHours = Math.Clamp(App.SInt(s, "cancel.hours", 24), 0, 720);
            st.ShowPrices = App.S(s, "show.prices", "1") == "1";
            st.Hours = ParseHours(App.S(s, "hours"));
            st.Insurance = Lines(App.S(s, "insurance"));
            st.Species = info.SpeciesList.Select(x => x.Key).ToList();
            st.CheckInFrom = Ui.ParseHm(App.S(s, "stay.checkin.from")) != null ? App.S(s, "stay.checkin.from") : "07:30";
            st.CheckInTo = Ui.ParseHm(App.S(s, "stay.checkin.to")) != null ? App.S(s, "stay.checkin.to") : "18:00";
            st.CheckOutBy = Ui.ParseHm(App.S(s, "stay.checkout.by")) != null ? App.S(s, "stay.checkout.by") : "11:00";
            st.StayMaxNights = Math.Clamp(App.SInt(s, "stay.max.nights", 30), 1, 365);
            st.StayCancelHours = Math.Clamp(App.SInt(s, "stay.cancel.hours", 48), 0, 720);
            foreach (KeyValuePair<string, string> kv in s.Where(k => k.Key.StartsWith("stay.vax.", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.Key == "stay.vax.dog" ? 0 : k.Key == "stay.vax.cat" ? 1 : 2).ThenBy(k => k.Key))
            {
                List<string> req = Csv(kv.Value);
                if (req.Count > 0)
                {
                    st.BoardingVax.Add(new VaxRule { Species = kv.Key.Substring(9).ToLowerInvariant(), Required = req });
                }
            }
            st.VaxChart = ParseVaxChart(App.S(s, "vaxchart"));
            st.RemindDays = App.SInt(s, "remind.days", 30);
            st.WellnessMonths = App.SInt(s, "wellness.months", 12);
            st.MaxVideoMb = Math.Clamp(App.SInt(s, "video.mb", 25), 1, 500);
            info.Faqs = ParseFaqs(App.S(s, "faqs"));

            foreach (DataRow r in await Rows(bs, "SELECT TEAM_KEY, TEAM_NAME, BLURB, IMAGE, COLOR, TEAM_ORDER FROM TEAM WHERE ACTIVE = 1 ORDER BY TEAM_ORDER, TEAM_NAME"))
            {
                info.Specialties.Add(new Specialty { Key = Str(r, "TEAM_KEY"), Name = Str(r, "TEAM_NAME"), Blurb = Str(r, "BLURB"), Image = Str(r, "IMAGE"), Color = Ui.SafeColor(Str(r, "COLOR")) ? Str(r, "COLOR") : "#3F7D6B", Order = Int(r, "TEAM_ORDER"), Active = true });
            }

            Dictionary<string, List<string>> bookStaff = new Dictionary<string, List<string>>();
            foreach (DataRow r in await Rows(bs, "SELECT X.BOOKTYPE_ID, X.STAFF_ID FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE S.ACTIVE = 1 ORDER BY S.WEB_ORDER, S.STAFF_NAME"))
            {
                string b = Str(r, "BOOKTYPE_ID");
                if (!bookStaff.TryGetValue(b, out List<string>? l))
                {
                    l = new List<string>();
                    bookStaff[b] = l;
                }
                l.Add(Str(r, "STAFF_ID"));
            }
            foreach (DataRow r in await Rows(bs, "SELECT B.BOOKTYPE_ID, B.BOOK_NAME, B.TEAM_KEY, B.VISIT_TYPE, B.SHORT_TEXT, B.LONG_TEXT, B.EXPECT, B.MINUTES, B.MINUTES_M, B.MINUTES_L, B.ITEM_ID, B.ROOMTYPE_ID, B.SPECIES, B.DROP_FROM, B.DROP_TO, B.PICKUP_HM, B.PREOP, B.IMAGE, B.ONLINE, B.NEW_OK, B.BOOK_ORDER, ISNULL(I.PRICE, 0) AS P1, ISNULL(I.PRICE_M, 0) AS P2, ISNULL(I.PRICE_L, 0) AS P3 FROM BOOKTYPE B LEFT JOIN ITEM I ON I.ITEM_ID = B.ITEM_ID AND I.ACTIVE = 1 WHERE B.ACTIVE = 1 ORDER BY B.BOOK_ORDER, B.BOOK_NAME"))
            {
                BookRow br = new BookRow
                {
                    BooktypeId = Str(r, "BOOKTYPE_ID"),
                    BookName = Str(r, "BOOK_NAME"),
                    TeamKey = Str(r, "TEAM_KEY"),
                    VisitType = Str(r, "VISIT_TYPE"),
                    Minutes = Int(r, "MINUTES"),
                    MinutesM = Int(r, "MINUTES_M"),
                    MinutesL = Int(r, "MINUTES_L"),
                    ItemId = Str(r, "ITEM_ID"),
                    RoomtypeId = Str(r, "ROOMTYPE_ID"),
                    Species = Str(r, "SPECIES"),
                    DropFrom = Str(r, "DROP_FROM"),
                    DropTo = Str(r, "DROP_TO"),
                    PickupHm = Str(r, "PICKUP_HM"),
                    Online = Bit(r, "ONLINE"),
                    NewOk = Bit(r, "NEW_OK"),
                    Active = true
                };
                bool drop = Slots.Dropoff(br);
                info.Services.Add(new CareService
                {
                    Key = br.BooktypeId,
                    Specialty = br.TeamKey,
                    Name = br.BookName,
                    Short = Str(r, "SHORT_TEXT"),
                    Text = Str(r, "LONG_TEXT"),
                    Kind = drop ? "dropoff" : br.VisitType == "grooming" ? "groom" : "visit",
                    VisitType = br.VisitType,
                    Minutes = Math.Max(5, br.Minutes),
                    MinutesM = br.MinutesM,
                    MinutesL = br.MinutesL,
                    ItemId = br.ItemId,
                    Price = Dec(r, "P1"),
                    PriceM = Dec(r, "P2"),
                    PriceL = Dec(r, "P3"),
                    Species = Csv(br.Species),
                    DropFrom = br.DropFrom,
                    DropTo = br.DropTo != string.Empty ? br.DropTo : br.DropFrom,
                    ProcFrom = br.DropTo != string.Empty ? br.DropTo : br.DropFrom,
                    ProcMinutes = drop ? Math.Max(5, br.Minutes) : 0,
                    RoomKind = br.RoomtypeId,
                    PickUp = br.PickupHm,
                    PreOp = Lines(Str(r, "PREOP")),
                    Image = Str(r, "IMAGE"),
                    Providers = bookStaff.TryGetValue(br.BooktypeId, out List<string>? ps) ? ps : new List<string>(),
                    Expect = Lines(Str(r, "EXPECT")),
                    Online = br.Online,
                    NewOk = br.NewOk,
                    Active = true,
                    Order = Int(r, "BOOK_ORDER"),
                    Book = br
                });
            }

            Dictionary<string, List<string>> species = new Dictionary<string, List<string>>();
            foreach (DataRow r in await Rows(bs, "SELECT X.STAFF_ID, X.SPECIES_KEY FROM STAFFSPECIES X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID LEFT JOIN SPECIES P ON P.SPECIES_KEY = X.SPECIES_KEY WHERE S.SHOW_WEB = 1 AND S.ACTIVE = 1 ORDER BY ISNULL(P.SPECIES_ORDER, 99), X.SPECIES_KEY"))
            {
                string id = Str(r, "STAFF_ID");
                if (!species.TryGetValue(id, out List<string>? l))
                {
                    l = new List<string>();
                    species[id] = l;
                }
                l.Add(Str(r, "SPECIES_KEY"));
            }
            Dictionary<string, List<WorkDay>> hours = new Dictionary<string, List<WorkDay>>();
            foreach (DataRow r in await Rows(bs, "SELECT H.STAFF_ID, H.DAY_NO, H.IS_ON, H.FROM_HM, H.TO_HM, H.BREAK_FROM, H.BREAK_TO FROM STAFFHOURS H JOIN STAFF S ON S.STAFF_ID = H.STAFF_ID WHERE S.SHOW_WEB = 1 AND S.ACTIVE = 1 ORDER BY H.DAY_NO"))
            {
                string id = Str(r, "STAFF_ID");
                if (!hours.TryGetValue(id, out List<WorkDay>? l))
                {
                    l = new List<WorkDay>();
                    hours[id] = l;
                }
                l.Add(new WorkDay { Day = Int(r, "DAY_NO"), On = Bit(r, "IS_ON"), From = Str(r, "FROM_HM"), To = Str(r, "TO_HM"), BreakFrom = Str(r, "BREAK_FROM"), BreakTo = Str(r, "BREAK_TO") });
            }
            DateTime today = App.Now(s).Date;
            Dictionary<string, List<TimeOff>> off = new Dictionary<string, List<TimeOff>>();
            foreach (DataRow r in await Rows(bs, "SELECT T.TIMEOFF_ID, T.STAFF_ID, T.FROM_DATE, T.TO_DATE FROM TIMEOFF T JOIN STAFF S ON S.STAFF_ID = T.STAFF_ID WHERE S.SHOW_WEB = 1 AND S.ACTIVE = 1 AND T.TO_DATE >= @d ORDER BY T.FROM_DATE", new List<SqlParameter> { new SqlParameter("d", SqlDbType.Date) { Value = today } }))
            {
                string id = Str(r, "STAFF_ID");
                if (!off.TryGetValue(id, out List<TimeOff>? l))
                {
                    l = new List<TimeOff>();
                    off[id] = l;
                }
                l.Add(new TimeOff { Id = Str(r, "TIMEOFF_ID"), From = Day(r, "FROM_DATE").Date, To = Day(r, "TO_DATE").Date });
            }
            foreach (DataRow r in await Rows(bs, "SELECT STAFF_ID, STAFF_NAME, TITLE, PROVIDER_TYPE, CREDENTIALS, BIO, EDUCATION, LANGUAGES, PHOTO, COLOR, WEB_ORDER, ACCEPTING FROM STAFF WHERE SHOW_WEB = 1 AND ACTIVE = 1 ORDER BY WEB_ORDER, STAFF_NAME"))
            {
                string id = Str(r, "STAFF_ID");
                List<string> teams = info.Services.Where(x => x.Providers.Contains(id)).GroupBy(x => x.Specialty).OrderByDescending(g => g.Count()).ThenBy(g => info.Specialties.FindIndex(t => t.Key == g.Key) is int i && i >= 0 ? i : 99).Select(g => g.Key).Where(k => info.Specialties.Any(t => t.Key == k)).ToList();
                info.Providers.Add(new Clinician
                {
                    Id = id,
                    Name = Str(r, "STAFF_NAME"),
                    Title = Str(r, "TITLE"),
                    Role = Str(r, "PROVIDER_TYPE") is "vet" or "tech" or "groomer" ? Str(r, "PROVIDER_TYPE") : "vet",
                    Credentials = Str(r, "CREDENTIALS"),
                    Specialty = teams.Count > 0 ? teams[0] : string.Empty,
                    Teams = teams,
                    Bio = Str(r, "BIO"),
                    Education = Lines(Str(r, "EDUCATION")),
                    Languages = Csv(Str(r, "LANGUAGES")),
                    Species = species.TryGetValue(id, out List<string>? sp) ? sp : new List<string>(),
                    Photo = Str(r, "PHOTO"),
                    Color = Ui.SafeColor(Str(r, "COLOR")) ? Str(r, "COLOR") : "#3F7D6B",
                    Hours = hours.TryGetValue(id, out List<WorkDay>? h) ? h : new List<WorkDay>(),
                    TimeOff = off.TryGetValue(id, out List<TimeOff>? o) ? o : new List<TimeOff>(),
                    Accepting = Bit(r, "ACCEPTING"),
                    Active = true,
                    Order = Int(r, "WEB_ORDER")
                });
            }

            foreach (DataRow r in await Rows(bs, "SELECT T.ROOMTYPE_ID, T.TYPE_NAME, T.SPECIES, T.SIZES, T.NIGHT_ITEM_ID, T.SHORT_TEXT, T.LONG_TEXT, T.IMAGE, T.COLOR, T.TYPE_ORDER, ISNULL(I.PRICE, 0) AS NIGHT, (SELECT COUNT(*) FROM ROOM R WHERE R.ROOMTYPE_ID = T.ROOMTYPE_ID AND R.ACTIVE = 1) AS RUNS FROM ROOMTYPE T LEFT JOIN ITEM I ON I.ITEM_ID = T.NIGHT_ITEM_ID WHERE T.PURPOSE = N'boarding' AND T.ONLINE = 1 AND T.ACTIVE = 1 ORDER BY T.TYPE_ORDER, T.TYPE_NAME"))
            {
                List<string> sl = Csv(Str(r, "SPECIES"));
                info.Runs.Add(new RunType
                {
                    Key = Str(r, "ROOMTYPE_ID"),
                    Name = Str(r, "TYPE_NAME"),
                    Species = sl.Count > 0 ? sl[0] : "dog",
                    SpeciesList = sl,
                    Sizes = Csv(Str(r, "SIZES")),
                    Short = Str(r, "SHORT_TEXT"),
                    Text = Str(r, "LONG_TEXT"),
                    NightItemId = Str(r, "NIGHT_ITEM_ID"),
                    Price = Dec(r, "NIGHT"),
                    Count = Int(r, "RUNS"),
                    Image = Str(r, "IMAGE"),
                    Color = Ui.SafeColor(Str(r, "COLOR")) ? Str(r, "COLOR") : "#3F7D6B",
                    Online = true,
                    Active = true,
                    Order = Int(r, "TYPE_ORDER")
                });
            }
            foreach (DataRow r in await Rows(bs, "SELECT I.ITEM_ID, I.ITEM_CODE, I.ITEM_NAME, I.UNIT, I.PRICE, I.SPECIES, I.NOTE, I.ITEM_ORDER FROM ITEM I WHERE I.ITEM_TYPE = N'boarding' AND I.ACTIVE = 1 AND NOT EXISTS (SELECT 1 FROM ROOMTYPE T WHERE T.NIGHT_ITEM_ID = I.ITEM_ID) ORDER BY I.ITEM_ORDER, I.ITEM_NAME"))
            {
                string unit = Str(r, "UNIT");
                info.AddOns.Add(new AddOn { Key = Str(r, "ITEM_ID"), Code = Str(r, "ITEM_CODE"), Name = Str(r, "ITEM_NAME"), Text = Str(r, "NOTE"), Price = Dec(r, "PRICE"), Unit = unit, PerNight = unit is "day" or "night", Species = Str(r, "SPECIES"), Active = true, Order = Int(r, "ITEM_ORDER") });
            }
            foreach (DataRow r in await Rows(bs, "SELECT HOLIDAY_DATE, HOLIDAY_NAME FROM HOLIDAY WHERE HOLIDAY_DATE >= @d ORDER BY HOLIDAY_DATE", new List<SqlParameter> { new SqlParameter("d", SqlDbType.Date) { Value = today.AddDays(-7) } }))
            {
                info.Holidays.Add(new Holiday { Date = Day(r, "HOLIDAY_DATE").Date, Name = Str(r, "HOLIDAY_NAME") });
            }
            foreach (DataRow r in await Rows(bs, "SELECT MEDIA_ID, KIND, TITLE, ALT, TAGS, FULL_PATH, THUMB_PATH, SECONDS, PAGES, WIDTH, HEIGHT, BYTES, ADDED_BY, ADDED_UTC FROM MEDIA ORDER BY ADDED_UTC DESC, MEDIA_ID"))
            {
                info.Media.Add(new MediaItem { Id = Str(r, "MEDIA_ID"), Kind = Str(r, "KIND"), Title = Str(r, "TITLE"), Alt = Str(r, "ALT"), Tags = Csv(Str(r, "TAGS")), Full = Str(r, "FULL_PATH"), Thumb = Str(r, "THUMB_PATH"), Seconds = Int(r, "SECONDS"), Pages = Int(r, "PAGES"), Width = Int(r, "WIDTH"), Height = Int(r, "HEIGHT"), Bytes = Long(r, "BYTES"), By = Str(r, "ADDED_BY"), When = Day(r, "ADDED_UTC") });
            }
            Dictionary<string, List<string>> items = new Dictionary<string, List<string>>();
            foreach (DataRow r in await Rows(bs, "SELECT I.ALBUM_ID, I.FULL_PATH FROM ALBUMITEM I JOIN ALBUM A ON A.ALBUM_ID = I.ALBUM_ID WHERE A.PUBLISHED = 1 ORDER BY I.ALBUM_ID, I.ITEM_ORDER, I.FULL_PATH"))
            {
                string id = Str(r, "ALBUM_ID");
                if (!items.TryGetValue(id, out List<string>? l))
                {
                    l = new List<string>();
                    items[id] = l;
                }
                l.Add(Str(r, "FULL_PATH"));
            }
            foreach (DataRow r in await Rows(bs, "SELECT ALBUM_ID, TITLE, TEXT, COVER, ALBUM_DATE, ADDED_BY FROM ALBUM WHERE PUBLISHED = 1 ORDER BY ALBUM_DATE DESC, TITLE"))
            {
                string id = Str(r, "ALBUM_ID");
                List<string> l = items.TryGetValue(id, out List<string>? x) ? x : new List<string>();
                string cover = Str(r, "COVER");
                info.Albums.Add(new Album { Id = id, Title = Str(r, "TITLE"), Text = Str(r, "TEXT"), Cover = cover != string.Empty ? cover : l.FirstOrDefault() ?? string.Empty, Items = l, Date = Day(r, "ALBUM_DATE").Date, Published = true, By = Str(r, "ADDED_BY") });
            }
            foreach (DataRow r in await Rows(bs, "SELECT PAGE_KEY, BLOCKS, SAVED_UTC, SAVED_BY FROM PAGEDOC"))
            {
                string key = Str(r, "PAGE_KEY");
                info.Pages[key] = new PageDoc { Key = key, Blocks = Blocks.Parse(Str(r, "BLOCKS")), Saved = Day(r, "SAVED_UTC"), By = Str(r, "SAVED_BY") };
            }
            foreach (PageSpec p in Blocks.Pages)
            {
                if (!info.Pages.ContainsKey(p.Key))
                {
                    info.Pages[p.Key] = new PageDoc { Key = p.Key };
                }
                Blocks.Ensure(p, info.Pages[p.Key].Blocks);
            }
            List<DataRow> cc = await Rows(bs, "SELECT COUNT(*) AS N FROM CLIENT");
            info.ClientCount = cc.Count > 0 ? Int(cc[0], "N") : 0;
            info.Partial = bs.Failed;
            return info;
        }

        public static DateTime Now(SiteInfo info)
        {
            return App.Now(info.S);
        }

        public static DateTime Today(SiteInfo info)
        {
            return App.Now(info.S).Date;
        }

        public static Holiday? HolidayOn(SiteInfo info, DateTime day)
        {
            return info.Holidays.FirstOrDefault(h => h.Date == day.Date);
        }

        public static (TimeSpan From, TimeSpan To)? Clinic(SiteInfo info, DateTime day)
        {
            if (HolidayOn(info, day) != null)
            {
                return null;
            }
            OpenDay? o = info.Settings.Hours.FirstOrDefault(h => h.Day == (int)day.DayOfWeek);
            TimeSpan? a = o != null && o.Open ? Ui.ParseHm(o.From) : null;
            TimeSpan? z = o != null && o.Open ? Ui.ParseHm(o.To) : null;
            return a != null && z != null && z > a ? (a.Value, z.Value) : null;
        }

        public static DateTime Earliest(SiteInfo info)
        {
            return Now(info).AddHours(info.Settings.MinNoticeHours);
        }

        public static DateTime LastDay(SiteInfo info)
        {
            return Today(info).AddDays(info.Settings.BookAheadDays);
        }

        public static bool ForSpecies(CareService sv, string species)
        {
            return species == string.Empty || sv.Species.Count == 0 || sv.Species.Contains(species);
        }

        public static bool Treats(Clinician p, string species)
        {
            return species == string.Empty || p.Species.Count == 0 || p.Species.Contains(species);
        }

        public static bool Online(Clinician p, CareService sv)
        {
            return p.Active && p.Accepting && sv.Active && sv.Online && sv.Providers.Contains(p.Id);
        }

        public static List<Clinician> For(SiteInfo info, CareService sv, string species = "")
        {
            return info.Providers.Where(p => Online(p, sv) && Treats(p, species)).OrderBy(p => p.Order).ToList();
        }

        public static int MinutesFor(CareService sv, string size)
        {
            return Slots.Length(sv.Book, size);
        }

        public static decimal PriceFor(CareService sv, string size)
        {
            decimal p = size switch { "medium" => sv.PriceM, "large" => sv.PriceL, _ => 0m };
            return p > 0 ? p : sv.Price;
        }

        public static async Task<List<(Opening O, Clinician P)>> Openings(SiteInfo info, CareService sv, string staffId, DateTime day, string size, string species, string ignoreAppt = "")
        {
            List<(Opening, Clinician)> list = new List<(Opening, Clinician)>();
            DateTime d = day.Date;
            if (!sv.Online || !ForSpecies(sv, species) || d < Today(info) || d > LastDay(info) || Clinic(info, d) == null)
            {
                return list;
            }
            List<Clinician> who = For(info, sv, species).Where(p => staffId == string.Empty || p.Id == staffId).ToList();
            if (who.Count == 0)
            {
                return list;
            }
            List<Opening> found = await Slots.Find(info.S, sv.Book, who.Count == 1 ? who[0].Id : string.Empty, d, size, ignoreAppt, Earliest(info));
            foreach (Opening o in found)
            {
                Clinician? p = who.FirstOrDefault(x => x.Id == o.StaffId);
                if (p != null)
                {
                    list.Add((o, p));
                }
            }
            return list;
        }

        public static async Task<(DateTime Start, Clinician P)?> Next(SiteInfo info, CareService sv, Clinician? only, int days = 28, string species = "", string size = "")
        {
            string key = sv.Key + "|" + (only?.Id ?? string.Empty) + "|" + days.ToString(Ui.Inv) + "|" + species + "|" + size;
            long ver = Interlocked.Read(ref version);
            if (NextCache.TryGetValue(key, out var hit) && hit.Ver == ver && hit.At > DateTime.UtcNow.AddSeconds(-CacheSeconds))
            {
                Clinician? hp = hit.Start != null ? info.Provider(hit.StaffId) : null;
                return hit.Start != null && hp != null ? (hit.Start.Value, hp) : null;
            }
            (DateTime Start, Clinician P)? best = null;
            if (sv.Online && For(info, sv, species).Any(p => only == null || p.Id == only.Id))
            {
                DateTime today = Today(info);
                DateTime last = LastDay(info);
                for (int i = 0; i < days; i++)
                {
                    DateTime d = today.AddDays(i);
                    if (d > last)
                    {
                        break;
                    }
                    if (Clinic(info, d) == null)
                    {
                        continue;
                    }
                    List<(Opening O, Clinician P)> l = await Openings(info, sv, only?.Id ?? string.Empty, d, size, species);
                    if (l.Count > 0)
                    {
                        best = (l[0].O.Start, l[0].P);
                        break;
                    }
                }
            }
            if (NextCache.Count > 5000)
            {
                NextCache.Clear();
            }
            if (Interlocked.Read(ref version) == ver)
            {
                NextCache[key] = (DateTime.UtcNow, ver, best?.Start, best?.P.Id ?? string.Empty);
            }
            return best;
        }

        public static string Tel(string? phone)
        {
            return Regex.Replace(phone ?? string.Empty, "[^0-9+]", string.Empty);
        }

        public static string DarkAccent(string hex)
        {
            if (!Ui.SafeColor(hex) || hex.Equals("#3F7D6B", StringComparison.OrdinalIgnoreCase))
            {
                return "#6FB49E";
            }
            int v = Convert.ToInt32(hex.Substring(1), 16);
            int r = (int)Math.Round(((v >> 16) & 255) * 0.62 + 255 * 0.38);
            int g = (int)Math.Round(((v >> 8) & 255) * 0.62 + 255 * 0.38);
            int b = (int)Math.Round((v & 255) * 0.62 + 255 * 0.38);
            return "#" + r.ToString("X2", Ui.Inv) + g.ToString("X2", Ui.Inv) + b.ToString("X2", Ui.Inv);
        }

        public static string Style(string p, SiteInfo info)
        {
            string a = info.Settings.Accent;
            return Ui.SafeColor(a) ? "--" + p + "-accent:" + a + ";--" + p + "-accent-d:" + DarkAccent(a) : string.Empty;
        }

        public static string Theme(SiteInfo info)
        {
            return info.Settings.Theme == "dark" ? "dark" : "light";
        }

        public static string Hm(SiteInfo info, DateTime d)
        {
            return d.ToString(info.Settings.Clock24 ? "HH:mm" : "h:mm tt", Ui.Us);
        }

        public static string Hm(SiteInfo info, TimeSpan t)
        {
            return Hm(info, DateTime.Today.Add(t));
        }

        public static string HmText(SiteInfo info, string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? string.Empty : Hm(info, t.Value);
        }

        public static string When(SiteInfo info, DateTime d, bool year = false)
        {
            return Ui.Day(d, year) + " &middot; " + Hm(info, d);
        }

        public static string Dur(int minutes)
        {
            if (minutes < 60)
            {
                return minutes.ToString(Ui.Inv) + " min";
            }
            int h = minutes / 60;
            int m = minutes % 60;
            return h.ToString(Ui.Inv) + " h" + (m > 0 ? " " + m.ToString(Ui.Inv) + " min" : string.Empty);
        }

        public static string Price(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Ui.Inv);
        }

        public static string SizeName(string key)
        {
            return key switch { "small" => "Small", "medium" => "Medium", "large" => "Large", _ => string.Empty };
        }

        public static string Days(IEnumerable<int> days)
        {
            List<int> d = days.Distinct().OrderBy(x => x == 0 ? 7 : x).ToList();
            if (d.Count == 0)
            {
                return string.Empty;
            }
            if (d.Count == 1)
            {
                return Ui.DayNames[d[0]] + "s";
            }
            return string.Join(" &amp; ", d.Select(x => DayShort[x]));
        }

        public static string Tok(SiteInfo info, string v)
        {
            return (v ?? string.Empty).Replace("{name}", info.Settings.Name).Replace("{town}", info.Settings.Town).Replace("{phone}", info.Settings.Phone);
        }

        public static string TokH(SiteInfo info, string v)
        {
            return (v ?? string.Empty).Replace("{name}", Ui.Enc(info.Settings.Name)).Replace("{town}", Ui.Enc(info.Settings.Town)).Replace("{phone}", Ui.Enc(info.Settings.Phone));
        }

        public static string FootAddr(SiteInfo info)
        {
            ClinicSettings st = info.Settings;
            return Ui.Enc(st.Address) + "<br><a href=\"tel:" + Ui.Attr(Tel(st.Phone)) + "\">" + Ui.Enc(st.Phone) + "</a><br><a href=\"mailto:" + Ui.Attr(st.Email) + "\">" + Ui.Enc(st.Email) + "</a>";
        }

        public static string Nav(string p, string page, bool drawer)
        {
            StringBuilder sb = new StringBuilder();
            string on = " class=\"" + p + "-on\" aria-current=\"page\"";
            if (drawer)
            {
                sb.Append("<a href=\"Home\"" + (page == "Home" ? on : string.Empty) + ">Home</a>");
            }
            foreach ((string href, string name) in Links)
            {
                bool here = href == page || (href == "Services" && page == "Service") || (href == "Vets" && page == "Vet") || (href == "Boarding" && page == "BookStay");
                sb.Append("<a href=\"" + href + "\"" + (here ? on : string.Empty) + ">" + Ui.Enc(name) + "</a>");
            }
            if (drawer)
            {
                sb.Append("<a href=\"BookStay\"" + (page == "BookStay" ? on : string.Empty) + ">Book a stay</a><a href=\"Media\"" + (page == "Media" ? on : string.Empty) + ">Media</a><a href=\"MyAppointment\"" + (page == "MyAppointment" ? on : string.Empty) + ">My booking</a><a class=\"" + p + "-dr-er\" href=\"Emergency\"" + (page == "Emergency" ? " aria-current=\"page\"" : string.Empty) + ">Emergency</a>");
            }
            return sb.ToString();
        }

        public static string TodayLine(string p, SiteInfo info)
        {
            DateTime now = Now(info);
            (TimeSpan From, TimeSpan To)? w = Clinic(info, now.Date);
            Holiday? h = HolidayOn(info, now.Date);
            if (w != null && now.TimeOfDay >= w.Value.From && now.TimeOfDay < w.Value.To)
            {
                return "<i></i>Open now &middot; until " + Hm(info, w.Value.To);
            }
            if (w != null && now.TimeOfDay < w.Value.From)
            {
                return "<i class=\"" + p + "-shut\"></i>Closed now &middot; opens today at " + Hm(info, w.Value.From);
            }
            for (int i = 1; i <= 14; i++)
            {
                (TimeSpan From, TimeSpan To)? n = Clinic(info, now.Date.AddDays(i));
                if (n != null)
                {
                    return "<i class=\"" + p + "-shut\"></i>" + (h != null ? "Closed today for " + Ui.Enc(h.Name) : "Closed now") + " &middot; opens " + (i == 1 ? "tomorrow" : now.Date.AddDays(i).ToString("dddd", Ui.Us)) + " at " + Hm(info, n.Value.From);
                }
            }
            return "<i class=\"" + p + "-shut\"></i>Closed";
        }

        public static string HoursRows(string p, SiteInfo info, string cls)
        {
            int today = (int)Today(info).DayOfWeek;
            StringBuilder sb = new StringBuilder();
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                OpenDay? o = info.Settings.Hours.FirstOrDefault(h => h.Day == d);
                string t = o != null && o.Open ? HmText(info, o.From) + " &ndash; " + HmText(info, o.To) : "Closed";
                sb.Append("<span class=\"" + cls + (d == today ? " " + p + "-now" : string.Empty) + "\"><span>" + Ui.DayNames[d] + (d == today ? " <em>today</em>" : string.Empty) + "</span><span>" + t + "</span></span>");
            }
            return sb.ToString();
        }

        public static string DemoNote()
        {
            return App.Demo ? "Demo website &middot; bookings and messages go into a shared demo hospital that resets every day. <a href=\"AdminLogin\">Open the hospital admin</a>" : string.Empty;
        }

        private static string Logo(string p, int size)
        {
            return "<svg class=\"" + p + "-logo\" viewBox=\"0 0 40 40\" width=\"" + size + "\" height=\"" + size + "\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"12\" fill=\"var(--" + p + "-accent)\"/><ellipse cx=\"20\" cy=\"25.5\" rx=\"6.6\" ry=\"5.6\" fill=\"#fff\"/><ellipse cx=\"11.6\" cy=\"18.4\" rx=\"2.7\" ry=\"3.4\" transform=\"rotate(-18 11.6 18.4)\" fill=\"#fff\"/><ellipse cx=\"16.6\" cy=\"12.6\" rx=\"2.8\" ry=\"3.6\" transform=\"rotate(-6 16.6 12.6)\" fill=\"#fff\"/><ellipse cx=\"23.4\" cy=\"12.6\" rx=\"2.8\" ry=\"3.6\" transform=\"rotate(6 23.4 12.6)\" fill=\"#fff\"/><ellipse cx=\"28.4\" cy=\"18.4\" rx=\"2.7\" ry=\"3.4\" transform=\"rotate(18 28.4 18.4)\" fill=\"#fff\"/></svg>";
        }

        public static string Skip(string p)
        {
            return "<a class=\"" + p + "-skip\" href=\"#" + p + "-main\">Skip to content</a>";
        }

        public static string Header(string p, string js, string page, SiteInfo info)
        {
            ClinicSettings st = info.Settings;
            string name = Ui.Enc(st.Name);
            StringBuilder sb = new StringBuilder();
            sb.Append(Skip(p));
            sb.Append("<div class=\"" + p + "-strip\"><div class=\"" + p + "-wrap " + p + "-strip-in\"><span class=\"" + p + "-today\" id=\"" + p + "-today\">" + TodayLine(p, info) + "</span><a class=\"" + p + "-stel\" href=\"tel:" + Ui.Attr(Tel(st.Phone)) + "\" id=\"" + p + "-sphone\"><svg viewBox=\"0 0 24 24\" width=\"15\" height=\"15\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.9\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg><span id=\"" + p + "-sphonet\">" + Ui.Enc(st.Phone) + "</span></a></div></div>");
            sb.Append("<header class=\"" + p + "-top\"><div class=\"" + p + "-wrap " + p + "-top-in\">");
            sb.Append("<button type=\"button\" class=\"" + p + "-ibtn " + p + "-burger\" aria-label=\"Menu\" aria-controls=\"" + p + "-nav\" onclick=\"" + js + ".openNav()\"><svg viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M4 12h16M4 17h16\"/></svg></button>");
            sb.Append("<a class=\"" + p + "-brand\" href=\"Home\">" + Logo(p, 40) + "<span class=\"" + p + "-bname\"><b id=\"" + p + "-brand\">" + name + "</b><small id=\"" + p + "-town\">" + Ui.Enc(st.Town) + "</small></span></a>");
            sb.Append("<nav class=\"" + p + "-nav\" aria-label=\"Main\">" + Nav(p, page, false) + "</nav>");
            sb.Append("<div class=\"" + p + "-tools\"><a class=\"" + p + "-er\" href=\"Emergency\" aria-label=\"Emergency\"><svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg><span>Emergency</span></a><a class=\"" + p + "-tl " + p + "-mylink\" href=\"MyAppointment\">My booking</a>");
            sb.Append("<button type=\"button\" class=\"" + p + "-ibtn " + p + "-theme\" aria-label=\"Light or dark\" title=\"Light or dark\" onclick=\"" + js + ".toggleTheme()\"><svg class=\"" + p + "-moon\" viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M20 14.5A8 8 0 0 1 9.5 4a7 7 0 1 0 10.5 10.5z\"/></svg><svg class=\"" + p + "-sun\" viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"4\"/><path d=\"M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4\"/></svg></button>");
            sb.Append("<a class=\"" + p + "-btn " + p + "-btn-accent " + p + "-book\" href=\"Book\">Book</a></div></div></header>");
            sb.Append("<div class=\"" + p + "-demo-bar\" id=\"" + p + "-demo-bar\">" + DemoNote() + "</div>");
            sb.Append("<aside class=\"" + p + "-drawer\" id=\"" + p + "-nav\" aria-label=\"Menu\"><div class=\"" + p + "-dr-top\"><b id=\"" + p + "-dname\">" + name + "</b><button type=\"button\" class=\"" + p + "-ibtn\" aria-label=\"Close\" onclick=\"" + js + ".closeNav()\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button></div><nav class=\"" + p + "-dr-list\" aria-label=\"Menu links\">" + Nav(p, page, true) + "</nav><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-ghost " + p + "-btn-wide\" onclick=\"" + js + ".toggleTheme()\">Light or dark</button><a class=\"" + p + "-btn " + p + "-btn-accent " + p + "-btn-wide\" href=\"Book\">Book a visit</a></aside>");
            sb.Append("<div class=\"" + p + "-scrim\" id=\"" + p + "-scrim\" onclick=\"" + js + ".closeNav()\"></div>");
            return sb.ToString();
        }

        public static string Footer(string p, SiteInfo info)
        {
            ClinicSettings st = info.Settings;
            string name = Ui.Enc(st.Name);
            StringBuilder sb = new StringBuilder();
            sb.Append("<footer class=\"" + p + "-foot\"><div class=\"" + p + "-wrap " + p + "-foot-in\">");
            sb.Append("<div class=\"" + p + "-fcol " + p + "-fabout\"><a class=\"" + p + "-fbrand\" href=\"Home\">" + Logo(p, 36) + "<span id=\"" + p + "-fname\">" + name + "</span></a><p id=\"" + p + "-ftag\">" + Ui.Enc(st.Tagline) + "</p><p class=\"" + p + "-faddr\" id=\"" + p + "-faddr\">" + FootAddr(info) + "</p></div>");
            sb.Append("<div class=\"" + p + "-fcol\"><b>Visit</b><a href=\"Book\">Book a visit</a><a href=\"BookStay\">Book a stay</a><a href=\"MyAppointment\">Change or cancel</a><a href=\"Contact\">Hours &amp; directions</a></div>");
            sb.Append("<div class=\"" + p + "-fcol\"><b>Care</b><a href=\"Services\">Services</a><a href=\"Vets\">Vets</a><a href=\"Boarding\">Boarding</a><a href=\"Media\">Media</a></div>");
            sb.Append("<div class=\"" + p + "-fcol\"><b>Owners</b><a href=\"PetOwners\">Pet owners</a><a href=\"Emergency\">Emergency</a><a href=\"AdminLogin\">Staff sign in</a></div>");
            sb.Append("<div class=\"" + p + "-fcol " + p + "-fhours\"><b>Opening hours</b><div id=\"" + p + "-fhours\">" + HoursRows(p, info, p + "-hrow") + "</div></div>");
            sb.Append("</div><p class=\"" + p + "-wrap " + p + "-urgent\" id=\"" + p + "-urgent\">" + Ui.Enc(st.Urgent) + "</p><p class=\"" + p + "-wrap " + p + "-copy\">&copy; " + Today(info).Year.ToString(Ui.Inv) + " <span id=\"" + p + "-copyname\">" + name + "</span> &middot; A fictional animal hospital built with the SkyNet Framework. Every person, pet and price is invented. This site does not give veterinary advice.</p></footer>");
            sb.Append("<div class=\"" + p + "-toast\" id=\"" + p + "-toast\" role=\"status\" aria-live=\"polite\"></div>");
            return sb.ToString();
        }

        public static string Fill(string html, string p, string js, string page, SiteInfo info)
        {
            ClinicSettings st = info.Settings;
            string body = html ?? string.Empty;
            string bar = "<div class=\"" + p + "-demo-bar\" id=\"" + p + "-demo-bar\"></div>";
            if (body.Contains(bar, StringComparison.Ordinal))
            {
                body = body.Replace(bar, "<div class=\"" + p + "-demo-bar\" id=\"" + p + "-demo-bar\">" + DemoNote() + "</div>");
            }
            string strip = "<div class=\"" + p + "-strip\">";
            int at = body.IndexOf(strip, StringComparison.Ordinal);
            if (at >= 0 && !body.Contains("class=\"" + p + "-skip\"", StringComparison.Ordinal))
            {
                body = body.Insert(at, Skip(p));
            }
            return body
                .Replace("{plhd_header}", Header(p, js, page, info))
                .Replace("{plhd_footer}", Footer(p, info))
                .Replace("{plhd_demo}", DemoNote())
                .Replace("{plhd_name}", Ui.Enc(st.Name))
                .Replace("{plhd_town}", Ui.Enc(st.Town))
                .Replace("{plhd_tagline}", Ui.Enc(st.Tagline))
                .Replace("{plhd_faddr}", FootAddr(info))
                .Replace("{plhd_phone}", Ui.Enc(st.Phone))
                .Replace("{plhd_tel}", Ui.Attr(Tel(st.Phone)))
                .Replace("{plhd_urgent}", Ui.Enc(st.Urgent))
                .Replace("{plhd_today}", TodayLine(p, info))
                .Replace("{plhd_hours}", HoursRows(p, info, p + "-hrow"))
                .Replace("{plhd_theme}", Theme(info))
                .Replace("{plhd_style}", Style(p, info))
                .Replace("{plhd_nav}", Nav(p, page, false))
                .Replace("{plhd_drawer}", Nav(p, page, true));
        }
    }
}
