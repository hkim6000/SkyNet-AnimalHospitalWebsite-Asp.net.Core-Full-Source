using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Book : WebPage
    {
        private const string DefAccent = "#3F7D6B";
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string PhoneDigits = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), N'+', N'')";
        private static readonly Dictionary<string, List<DateTime>> Made = new Dictionary<string, List<DateTime>>();
        private static readonly Dictionary<string, DateTime> Forms = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, (DateTime At, List<Opening> List)> SlotCache = new Dictionary<string, (DateTime, List<Opening>)>();
        private static readonly (string Key, string Name)[] Sizes = { ("small", "Small (under 20 lb)"), ("medium", "Medium (20 to 50 lb)"), ("large", "Large (over 50 lb)") };

        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
        }

        private sealed class St
        {
            public string Sp { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
            public string Team { get; set; } = string.Empty;
            public CareService? Sv { get; set; }
            public string P { get; set; } = string.Empty;
            public Clinician? Pa { get; set; }
            public Clinician? Pref { get; set; }
            public DateTime? T { get; set; }
            public DateTime Wk { get; set; }
            public DateTime Day { get; set; }
            public bool Single { get; set; }
            public string Msg { get; set; } = string.Empty;
            public string At { get; set; } = string.Empty;
            public string Want { get; set; } = string.Empty;
        }

        private sealed class Slot
        {
            public DateTime Start { get; set; }
            public Clinician P { get; set; } = new Clinician();
            public Opening O { get; set; } = new Opening();
        }

        private sealed class Eng
        {
            private readonly Dictionary<string, List<Opening>> cache = new Dictionary<string, List<Opening>>();

            public Eng(SiteInfo site)
            {
                Site = site;
                Now = App.Now(site.S);
                Last = Now.Date.AddDays(Math.Clamp(site.Settings.BookAheadDays, 1, 365));
                NotBefore = Now.AddHours(Math.Max(0, site.Settings.MinNoticeHours));
            }

            public SiteInfo Site { get; }
            public DateTime Now { get; }
            public DateTime Today => Now.Date;
            public DateTime Last { get; }
            public DateTime NotBefore { get; }

            public Holiday? HolidayOn(DateTime day)
            {
                return Site.Holidays.FirstOrDefault(h => h.Date.Date == day.Date);
            }

            public (TimeSpan From, TimeSpan To)? Clinic(DateTime day)
            {
                if (HolidayOn(day) != null)
                {
                    return null;
                }
                var (open, f, t) = Slots.Hours(Site.S, day);
                return open ? (f, t) : null;
            }

            public List<Clinician> For(CareService sv, string sp)
            {
                return Site.Providers.Where(p => p.Active && p.Accepting && sv.Providers.Contains(p.Id) && (sp == string.Empty || p.Species.Count == 0 || p.Species.Contains(sp))).OrderBy(p => p.Order).ToList();
            }

            public async Task<List<Opening>> Raw(CareService sv, DateTime day, string size, bool fresh = false)
            {
                if (day.Date < Today || day.Date > Last || Clinic(day) == null)
                {
                    return new List<Opening>();
                }
                string key = sv.Key + "||" + Ui.Ymd(day) + "|" + size;
                if (!fresh && cache.TryGetValue(key, out List<Opening>? mine))
                {
                    return mine;
                }
                List<Opening>? list = null;
                if (!fresh)
                {
                    lock (SlotCache)
                    {
                        if (SlotCache.TryGetValue(key, out var hit) && hit.At > DateTime.UtcNow.AddSeconds(-60))
                        {
                            list = hit.List;
                        }
                    }
                }
                if (list == null)
                {
                    list = await Slots.Find(Site.S, Row(sv), string.Empty, day.Date, size, string.Empty, NotBefore);
                    lock (SlotCache)
                    {
                        if (SlotCache.Count > 3000)
                        {
                            SlotCache.Clear();
                        }
                        SlotCache[key] = (DateTime.UtcNow, list);
                    }
                }
                list = list.Where(o => o.Start >= NotBefore).ToList();
                cache[key] = list;
                return list;
            }

            public async Task<List<Slot>> Times(CareService sv, Clinician? only, DateTime day, string sp, string size)
            {
                List<Clinician> ps = For(sv, sp).Where(x => only == null || x.Id == only.Id).ToList();
                List<Slot> all = new List<Slot>();
                foreach (Opening o in await Raw(sv, day, size))
                {
                    Clinician? p = ps.FirstOrDefault(x => x.Id == o.StaffId);
                    if (p != null)
                    {
                        all.Add(new Slot { Start = o.Start, P = p, O = o });
                    }
                }
                List<Slot> list = new List<Slot>();
                foreach (Slot x in all.OrderBy(x => x.Start).ThenBy(x => x.O.ProcAt ?? x.Start).ThenBy(x => x.P.Order))
                {
                    if (!list.Any(y => y.Start == x.Start))
                    {
                        list.Add(x);
                    }
                }
                return list;
            }

            public async Task<Opening?> Hit(CareService sv, Clinician who, DateTime when, string size, bool fresh = false)
            {
                return (await Raw(sv, when.Date, size, fresh)).Where(o => o.StaffId == who.Id && o.Start == when).OrderBy(o => o.ProcAt ?? o.Start).FirstOrDefault();
            }

            public async Task<Slot?> NextFree(CareService sv, Clinician? only, string sp, string size)
            {
                for (int i = 0; i < 28; i++)
                {
                    DateTime d = Today.AddDays(i);
                    if (d > Last)
                    {
                        break;
                    }
                    List<Slot> l = await Times(sv, only, d, sp, size);
                    if (l.Count > 0)
                    {
                        return l[0];
                    }
                }
                return null;
            }
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string sv = Ui.Clip(QueryValue("sv"), 80);
            string p = Ui.Clip(QueryValue("p"), 80);
            View v = await Render(site, sv != string.Empty ? sv : Ui.Clip(QueryValue("s"), 80), p != string.Empty ? p : Ui.Clip(QueryValue("v"), 80), Ui.Clip(QueryValue("t"), 80), Ui.Clip(QueryValue("sp"), 20), Ui.Clip(QueryValue("at"), 20));
            Head(site, v.Title, v.Desc);
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, "bk", "BookJs", "Book", site).Replace("{plhd_main}", v.Main);
        }

        private void Head(SiteInfo site, string title, string desc)
        {
            string full = title + " | " + site.Settings.Name;
            HtmlDoc.SetTitle(Ui.Enc(full));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", Ui.Enc(desc));
            HttpContext? h = HttpCurrent.Context;
            string root = h != null ? h.Request.Scheme + "://" + h.Request.Host + h.Request.PathBase + "/" : string.Empty;
            foreach ((string k, string v) in new[] { ("og:type", "website"), ("og:site_name", site.Settings.Name), ("og:title", full), ("og:description", desc), ("og:image", root + "images/hospital/full/reception.webp") })
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Enc(v) } });
            }
        }

        private static BookRow Row(CareService sv)
        {
            if (sv.Book != null && sv.Book.BooktypeId != string.Empty)
            {
                return sv.Book;
            }
            return new BookRow { BooktypeId = sv.Key, BookName = sv.Name, TeamKey = sv.Specialty, VisitType = sv.VisitType, Minutes = sv.Minutes, MinutesM = sv.MinutesM, MinutesL = sv.MinutesL, ItemId = sv.ItemId, Species = string.Join(",", sv.Species), DropFrom = sv.DropFrom, DropTo = sv.DropTo, PickupHm = sv.PickUp, Online = sv.Online, NewOk = sv.NewOk, Active = sv.Active };
        }

        private static string Kind(CareService sv)
        {
            BookRow b = Row(sv);
            return Slots.Dropoff(b) ? "dropoff" : b.VisitType == "grooming" ? "groom" : "visit";
        }

        private static int MinutesFor(CareService sv, string size)
        {
            return Slots.Length(Row(sv), size);
        }

        private static decimal PriceFor(CareService sv, string size)
        {
            if (size == "large" && sv.PriceL > 0)
            {
                return sv.PriceL;
            }
            if (size == "medium" && sv.PriceM > 0)
            {
                return sv.PriceM;
            }
            return sv.Price;
        }

        private static bool ForSpecies(CareService sv, string sp)
        {
            return sv.Species.Count == 0 || sv.Species.Contains(sp);
        }

        private static string Cash(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Ui.Inv);
        }

        private static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd d MMM yyyy" : "ddd d MMM", Ui.Inv);
        }

        private static string LongDay(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", Ui.Inv);
        }

        private static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv);
        }

        private static string Hm(SiteInfo site, DateTime d)
        {
            return d.ToString(site.Settings.Clock24 ? "HH:mm" : "h:mm tt", Ui.Inv);
        }

        private static string HmText(SiteInfo site, string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? string.Empty : Hm(site, DateTime.Today.Add(t.Value));
        }

        private static string Dur(int minutes)
        {
            if (minutes < 60)
            {
                return minutes + " min";
            }
            int h = minutes / 60;
            int m = minutes % 60;
            return h + " h" + (m > 0 ? " " + m + " min" : string.Empty);
        }

        private static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..") && File.Exists(Path.Combine(App.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Thumb(string src)
        {
            string t = src.Contains("/full/", StringComparison.Ordinal) ? src.Replace("/full/", "/thumbs/") : src;
            return t != src && SafeImage(t) ? t : src;
        }

        private static string Pic(string src, string cls)
        {
            return "<img class=\"" + cls + "\" src=\"" + Ui.Attr(src) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\">";
        }

        private static string Face(Clinician? v)
        {
            if (v != null && SafeImage(v.Photo))
            {
                return "<img class=\"bk-face\" src=\"" + Ui.Attr(v.Photo) + "\" alt=\"\" loading=\"lazy\" width=\"480\" height=\"600\">";
            }
            return "<span class=\"bk-face bk-noface\" style=\"--h:" + Ui.Hue(v?.Id ?? "x") + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(v?.Name ?? "?")) + "</span>";
        }

        private static string SizeLong(string key)
        {
            foreach ((string k, string n) in Sizes)
            {
                if (k == key)
                {
                    return n;
                }
            }
            return key;
        }

        private static string SizeName(string key)
        {
            return key switch { "small" => "Small", "medium" => "Medium", "large" => "Large", _ => string.Empty };
        }

        private static List<string> SpeciesOn(SiteInfo site)
        {
            List<string> on = site.SpeciesList.OrderBy(x => x.Order).Select(x => x.Key).ToList();
            return on.Count > 0 ? on : new List<string> { "dog", "cat" };
        }

        private static bool PetDone(SiteInfo site, St st)
        {
            return st.Sp != string.Empty && (!site.HasSize(st.Sp) || st.Size != string.Empty);
        }

        private static string KindName(CareService sv)
        {
            return Kind(sv) switch { "groom" => "Grooming", "dropoff" => "Drop-off", _ => "Visit" };
        }

        private static string T12(SiteInfo site, TimeSpan t, bool suffix)
        {
            if (site.Settings.Clock24)
            {
                return t.ToString("hh\\:mm", Ui.Inv);
            }
            int h = t.Hours % 12 == 0 ? 12 : t.Hours % 12;
            string m = t.Minutes == 0 ? string.Empty : ":" + t.Minutes.ToString("00", Ui.Inv);
            return h.ToString(Ui.Inv) + m + (suffix ? (t.Hours < 12 ? " am" : " pm") : string.Empty);
        }

        private static string DropLine(SiteInfo site, CareService sv)
        {
            BookRow b = Row(sv);
            TimeSpan df = Ui.ParseHm(b.DropFrom) ?? TimeSpan.FromHours(7.5);
            TimeSpan dt = Ui.ParseHm(b.DropTo) ?? df;
            TimeSpan pu = Ui.ParseHm(b.PickupHm) ?? TimeSpan.FromHours(15);
            bool same = !site.Settings.Clock24 && (df.Hours < 12) == (dt.Hours < 12);
            return "Drop off " + T12(site, df, !same) + "&ndash;" + T12(site, dt, true) + " &middot; home from " + T12(site, pu, true);
        }

        private static string DropWindow(SiteInfo site, CareService sv)
        {
            BookRow b = Row(sv);
            TimeSpan df = Ui.ParseHm(b.DropFrom) ?? TimeSpan.FromHours(7.5);
            TimeSpan dt = Ui.ParseHm(b.DropTo) ?? df;
            return dt > df ? Hm(site, DateTime.Today.Add(df)) + " &ndash; " + Hm(site, DateTime.Today.Add(dt)) : Hm(site, DateTime.Today.Add(df));
        }

        private static string Price(SiteInfo site, CareService sv, string size)
        {
            decimal p = PriceFor(sv, size);
            if (!site.Settings.ShowPrices || p <= 0)
            {
                return string.Empty;
            }
            return (Kind(sv) == "groom" ? string.Empty : "From ") + Cash(p);
        }

        private static string Facts(SiteInfo site, CareService sv, string size)
        {
            string price = Price(site, sv, size);
            string len = Kind(sv) == "dropoff" ? "Day stay" : Dur(MinutesFor(sv, size));
            return len + (price != string.Empty ? " &middot; " + price : string.Empty);
        }

        private static string WhoWord(SiteInfo site, Eng e, CareService? sv, string sp)
        {
            if (sv == null)
            {
                return site.Settings.ProviderWord;
            }
            List<Clinician> ps = e.For(sv, sp);
            return ps.Count > 0 && ps.All(p => p.Role == "groomer") ? "Groomer" : site.Settings.ProviderWord;
        }

        private static string NextText(SiteInfo site, Slot? n, Eng e)
        {
            if (n == null)
            {
                return "No online times in the next 4 weeks";
            }
            DateTime d = n.Start.Date;
            string day = d == e.Today ? "Today" : d == e.Today.AddDays(1) ? "Tomorrow" : Day(d, false);
            return day + " at " + Hm(site, n.Start);
        }

        private static bool Bookable(Eng e, CareService sv, string sp, Clinician? pref)
        {
            List<Clinician> ps = e.For(sv, sp);
            return sv.Active && sv.Online && ForSpecies(sv, sp) && (pref == null || ps.Any(x => x.Id == pref.Id)) && ps.Count > 0;
        }

        private static async Task<View> Render(SiteInfo site, string sv, string p, string team, string sp, string at)
        {
            Eng e = new Eng(site);
            St st = await Norm(e, sp, string.Empty, sv, p, string.Empty, string.Empty, string.Empty, string.Empty, p, team, at);
            return new View { Title = "Book a visit", Desc = "Book a vet visit, grooming or a drop-off procedure at " + site.Settings.Name + " online in a few minutes.", Main = "<section class=\"bk-phead\"><div class=\"bk-wrap\"><span class=\"bk-kick\">Online booking</span><h1>Book a visit</h1><p class=\"bk-lead\">Tell us about your pet, then pick a service, a " + Ui.Enc(site.Settings.ProviderWord.ToLowerInvariant()) + " and a time. No account needed. Boarding? <a class=\"bk-lk\" href=\"BookStay\">Book a stay</a>.</p></div></section><div class=\"bk-wrap bk-bgrid\"><div id=\"bk-flow\" class=\"bk-flow\">" + await Flow(e, st) + "</div><aside class=\"bk-box bk-sum\" id=\"bk-sum\" aria-label=\"Your visit\">" + Summary(e, st) + "</aside></div>" };
        }

        private static string ServiceKey(SiteInfo site, string key)
        {
            if (key == string.Empty || site.Services.Any(x => x.Key == key))
            {
                return key;
            }
            return site.Services.Any(x => x.Key == "bt-" + key) ? "bt-" + key : key;
        }

        private static string StaffKey(SiteInfo site, string key)
        {
            if (key == string.Empty || key == "any" || site.Providers.Any(x => x.Id == key))
            {
                return key;
            }
            string alt = "s-" + (key.StartsWith("v-", StringComparison.Ordinal) ? key.Substring(2) : key);
            return site.Providers.Any(x => x.Id == alt) ? alt : key;
        }

        private static async Task<St> Norm(Eng e, string sp, string size, string sv, string p, string pa, string t, string day, string wk, string pref, string team, string at = "")
        {
            SiteInfo site = e.Site;
            St st = new St();
            List<string> species = SpeciesOn(site);
            sv = ServiceKey(site, sv);
            p = StaffKey(site, p);
            pa = StaffKey(site, pa);
            pref = StaffKey(site, pref);
            st.Team = site.Specialties.Any(x => x.Key == team && x.Active) ? team : string.Empty;
            st.Pref = site.Providers.FirstOrDefault(x => x.Id == pref && x.Active && x.Accepting);
            CareService? want = site.Services.FirstOrDefault(x => x.Key == sv && x.Active && x.Online && x.VisitType != "boarding");
            if (species.Contains(sp))
            {
                st.Sp = sp;
            }
            else if (want != null && want.Species.Count == 1 && species.Contains(want.Species[0]))
            {
                st.Sp = want.Species[0];
            }
            else if (st.Pref != null && st.Pref.Species.Count == 1 && species.Contains(st.Pref.Species[0]))
            {
                st.Sp = st.Pref.Species[0];
            }
            if (site.HasSize(st.Sp) && size is "small" or "medium" or "large")
            {
                st.Size = size;
            }
            bool petDone = PetDone(site, st);
            if (petDone && want != null && Bookable(e, want, st.Sp, null))
            {
                st.Sv = want;
            }
            else if (!petDone && want != null)
            {
                st.Want = want.Key;
            }
            Clinician? pp = site.Providers.FirstOrDefault(x => x.Id == p && x.Active && x.Accepting);
            if (st.Sv != null)
            {
                List<Clinician> ps = e.For(st.Sv, st.Sp);
                if (ps.Count == 1)
                {
                    st.P = ps[0].Id;
                    st.Single = true;
                }
                else if (p == "any" && ps.Count > 0)
                {
                    st.P = "any";
                }
                else if (pp != null && ps.Any(x => x.Id == pp.Id))
                {
                    st.P = pp.Id;
                }
            }
            DateTime? atT = Ui.ParseStamp(at);
            DateTime? when = Ui.ParseStamp(t);
            bool fromAt = false;
            if (when == null && atT != null && st.Sv != null && st.P != string.Empty)
            {
                when = atT;
                fromAt = true;
            }
            if (st.Sv != null && st.P != string.Empty && when != null)
            {
                Clinician? who = st.P == "any" ? site.Provider(pa) : site.Provider(st.P);
                if (fromAt && st.P == "any")
                {
                    who = (await e.Times(st.Sv, null, when.Value.Date, st.Sp, st.Size)).Where(x => x.Start == when.Value).Select(x => x.P).FirstOrDefault();
                }
                if (who != null && e.For(st.Sv, st.Sp).Any(x => x.Id == who.Id) && await e.Hit(st.Sv, who, when.Value, st.Size) != null)
                {
                    st.T = when;
                    st.Pa = who;
                }
            }
            st.At = st.T == null && atT != null && (st.Sv == null || st.P == string.Empty) ? Stamp(atT.Value) : string.Empty;
            DateTime w = Ui.ParseDay(wk) ?? (when?.Date ?? atT?.Date ?? e.Today);
            st.Wk = w < e.Today ? e.Today : w > e.Last ? e.Last : w;
            DateTime? d = Ui.ParseDay(day);
            if (d == null || d < st.Wk || d > st.Wk.AddDays(6))
            {
                d = null;
            }
            if (d == null && atT != null && atT.Value.Date >= st.Wk && atT.Value.Date <= st.Wk.AddDays(6) && e.Clinic(atT.Value.Date) != null)
            {
                d = atT.Value.Date;
            }
            if (d == null && st.Sv != null && st.P != string.Empty)
            {
                Clinician? only = st.P == "any" ? null : site.Provider(st.P);
                for (int i = 0; i < 7 && d == null; i++)
                {
                    if ((await e.Times(st.Sv, only, st.Wk.AddDays(i), st.Sp, st.Size)).Count > 0)
                    {
                        d = st.Wk.AddDays(i);
                    }
                }
            }
            st.Day = d ?? st.Wk;
            return st;
        }

        private static string Done(string label, string body, string change)
        {
            return "<section class=\"bk-stp bk-done\"><span class=\"bk-n\" aria-hidden=\"true\"></span><div class=\"bk-stb\"><small>" + Ui.Enc(label) + "</small><div class=\"bk-dv\">" + body + "</div></div>" + (change != string.Empty ? "<button type=\"button\" class=\"bk-chg\" onclick=\"BookJs.go('" + change + "')\">Change<span class=\"bk-sr\"> " + Ui.Enc(label.ToLowerInvariant()) + "</span></button>" : string.Empty) + "</section>";
        }

        private static string Open(int n, string title, string body)
        {
            return "<section class=\"bk-stp bk-open\" id=\"bk-cur\"><span class=\"bk-n\">" + n + "</span><div class=\"bk-stb\"><h2>" + Ui.Enc(title) + "</h2>" + body + "</div></section>";
        }

        private static string Locked(int n, string title)
        {
            return "<section class=\"bk-stp bk-lock\"><span class=\"bk-n\">" + n + "</span><div class=\"bk-stb\"><h2>" + Ui.Enc(title) + "</h2></div></section>";
        }

        private static async Task<string> Flow(Eng e, St st)
        {
            SiteInfo site = e.Site;
            StringBuilder sb = new StringBuilder("<i id=\"bk-st\" hidden data-sp=\"" + Ui.Attr(st.Sp) + "\" data-size=\"" + Ui.Attr(st.Size) + "\" data-sv=\"" + Ui.Attr(st.Sv?.Key ?? st.Want) + "\" data-p=\"" + Ui.Attr(st.P) + "\" data-pa=\"" + Ui.Attr(st.Pa?.Id ?? string.Empty) + "\" data-t=\"" + (st.T != null ? Stamp(st.T.Value) : string.Empty) + "\" data-wk=\"" + Ui.Ymd(st.Wk) + "\" data-day=\"" + Ui.Ymd(st.Day) + "\" data-pref=\"" + Ui.Attr(st.Pref?.Id ?? string.Empty) + "\" data-team=\"" + Ui.Attr(st.Team) + "\" data-at=\"" + Ui.Attr(st.At) + "\"></i>");
            bool petDone = PetDone(site, st);
            if (petDone)
            {
                sb.Append(Done("Your pet", "<b>" + Ui.Enc(site.SpeciesName(st.Sp)) + "</b>" + (st.Size != string.Empty ? "<span>" + Ui.Enc(SizeLong(st.Size)) + "</span>" : string.Empty), "pet"));
            }
            else
            {
                sb.Append(Open(1, "Who is coming in?", PetPick(site, st)));
            }
            if (!petDone)
            {
                sb.Append(Locked(2, "Service"));
            }
            else if (st.Sv != null)
            {
                sb.Append(Done("Service", "<b>" + Ui.Enc(st.Sv.Name) + "</b><span>" + (Kind(st.Sv) == "dropoff" ? DropLine(site, st.Sv) : Facts(site, st.Sv, st.Size)) + "</span>", "sv"));
            }
            else
            {
                sb.Append(Open(2, "What does your " + site.SpeciesName(st.Sp).ToLowerInvariant() + " need?", ServicePick(e, st)));
            }
            string word = WhoWord(site, e, st.Sv, st.Sp);
            if (st.Sv == null)
            {
                sb.Append(Locked(3, "Who you will see"));
            }
            else if (st.P != string.Empty)
            {
                Clinician? p = site.Provider(st.P);
                sb.Append(Done(word, st.P == "any" ? "<b>First available</b><span>We pick whoever is free at the time you choose</span>" : "<span class=\"bk-dp\">" + Face(p) + "<b>" + Ui.Enc(p?.Name ?? string.Empty) + "</b></span>", st.Single ? string.Empty : "p"));
            }
            else
            {
                sb.Append(Open(3, "Who would you like to see?", await ProviderPick(e, st)));
            }
            bool drop = st.Sv != null && Kind(st.Sv) == "dropoff";
            if (st.Sv == null || st.P == string.Empty)
            {
                sb.Append(Locked(4, "Date and time"));
            }
            else if (st.T != null)
            {
                string with = st.P == "any" && st.Pa != null ? " &middot; with " + Ui.Enc(st.Pa.Name) : string.Empty;
                string body = drop
                    ? "<b>" + LongDay(st.T.Value) + "</b><span>Drop off " + DropWindow(site, st.Sv) + " &middot; home from " + HmText(site, Row(st.Sv).PickupHm) + with + "</span>"
                    : "<b>" + LongDay(st.T.Value) + "</b><span>" + Hm(site, st.T.Value) + " &ndash; " + Hm(site, st.T.Value.AddMinutes(MinutesFor(st.Sv, st.Size))) + with + "</span>";
                sb.Append(Done("Date and time", body, "t"));
            }
            else
            {
                string msg = st.Msg != string.Empty ? "<p class=\"bk-warn-box\" role=\"alert\">" + st.Msg + "</p>" : string.Empty;
                sb.Append(Open(4, drop ? "Choose a drop-off day" : "Choose a time", msg + await Strip(e, st.Sv, st.P == "any" ? null : site.Provider(st.P), st.Wk, st.Day, st.Sp, st.Size)));
            }
            if (st.T == null)
            {
                sb.Append(Locked(5, "Your details"));
            }
            else
            {
                sb.Append(Open(5, "Your details", Form(site, st)));
            }
            return sb.ToString();
        }

        private static void Forget(DateTime day)
        {
            string mark = "||" + Ui.Ymd(day) + "|";
            lock (SlotCache)
            {
                foreach (string k in SlotCache.Keys.Where(k => k.Contains(mark, StringComparison.Ordinal)).ToList())
                {
                    SlotCache.Remove(k);
                }
            }
        }

        private static string NewForm()
        {
            string tok = App.NewId();
            lock (Forms)
            {
                if (Forms.Count > 2000)
                {
                    foreach (string old in Forms.Where(x => x.Value < DateTime.UtcNow.AddHours(-2)).Select(x => x.Key).ToList())
                    {
                        Forms.Remove(old);
                    }
                    if (Forms.Count > 4000)
                    {
                        Forms.Clear();
                    }
                }
                Forms[tok] = DateTime.UtcNow;
            }
            return tok;
        }

        private static bool FormAge(string tok)
        {
            lock (Forms)
            {
                return Forms.TryGetValue(tok, out DateTime at) && at > DateTime.UtcNow.AddHours(-2) && at <= DateTime.UtcNow.AddSeconds(-4);
            }
        }

        private static bool Take(string key, int limit, TimeSpan window)
        {
            lock (Made)
            {
                DateTime now = DateTime.UtcNow;
                if (!Made.TryGetValue(key, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Made[key] = list;
                }
                list.RemoveAll(x => x < now - window);
                if (Made.Count > 5000)
                {
                    foreach (string old in Made.Where(x => x.Value.Count == 0 || x.Value.Max() < now.AddDays(-1)).Select(x => x.Key).ToList())
                    {
                        Made.Remove(old);
                    }
                    if (Made.Count > 10000)
                    {
                        foreach (string old in Made.OrderBy(x => x.Value.Count == 0 ? DateTime.MinValue : x.Value.Max()).Take(Made.Count - 5000).Select(x => x.Key).ToList())
                        {
                            Made.Remove(old);
                        }
                    }
                    Made[key] = list;
                }
                if (list.Count >= limit)
                {
                    return false;
                }
                list.Add(now);
                return true;
            }
        }

        private static string Paw()
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"currentColor\" aria-hidden=\"true\"><ellipse cx=\"6\" cy=\"10\" rx=\"1.9\" ry=\"2.4\"/><ellipse cx=\"9.5\" cy=\"6\" rx=\"1.9\" ry=\"2.5\"/><ellipse cx=\"14.5\" cy=\"6\" rx=\"1.9\" ry=\"2.5\"/><ellipse cx=\"18\" cy=\"10\" rx=\"1.9\" ry=\"2.4\"/><path d=\"M12 11c-3 0-5.4 3.4-5.4 5.7 0 1.6 1.2 2.4 2.7 2.4 1.1 0 1.7-.5 2.7-.5s1.6.5 2.7.5c1.5 0 2.7-.8 2.7-2.4 0-2.3-2.4-5.7-5.4-5.7z\"/></svg>";
        }

        private static string PetPick(SiteInfo site, St st)
        {
            StringBuilder sb = new StringBuilder("<p class=\"bk-hint\">Pick the kind of pet, so we only show services and " + Ui.Enc(site.Settings.ProvidersWord.ToLowerInvariant()) + " that fit.</p><div class=\"bk-spc\" role=\"group\" aria-label=\"Kind of pet\">");
            foreach (string k in SpeciesOn(site))
            {
                sb.Append("<button type=\"button\" class=\"bk-sp" + (k == st.Sp ? " bk-act" : string.Empty) + "\" aria-pressed=\"" + (k == st.Sp ? "true" : "false") + "\" onclick=\"BookJs.pickSpecies('" + Ui.Js(k) + "')\">" + Paw() + "<span>" + Ui.Enc(site.SpeciesName(k)) + "</span></button>");
            }
            sb.Append("</div>");
            if (site.HasSize(st.Sp))
            {
                string word = site.SpeciesName(st.Sp).ToLowerInvariant();
                sb.Append("<p class=\"bk-sect\" id=\"bk-sizes\">How big is your " + Ui.Enc(word) + "?</p><div class=\"bk-sizes\">");
                foreach ((string k, string n) in Sizes)
                {
                    int i = n.IndexOf(" (", StringComparison.Ordinal);
                    string name = i > 0 ? n.Substring(0, i) : n;
                    string range = i > 0 ? n.Substring(i + 2).TrimEnd(')') : string.Empty;
                    sb.Append("<button type=\"button\" class=\"bk-sz\" onclick=\"BookJs.pickSize('" + Ui.Js(k) + "')\"><b>" + Ui.Enc(name) + "</b><small>" + Ui.Enc(range) + "</small></button>");
                }
                sb.Append("</div><p class=\"bk-hint\">Grooming time and price depend on size. Not sure? Pick your best guess; we check the weight at the desk.</p>");
            }
            return sb.ToString();
        }

        private static string ServicePick(Eng e, St st)
        {
            SiteInfo site = e.Site;
            List<CareService> list = site.Services.Where(x => x.VisitType != "boarding" && Bookable(e, x, st.Sp, st.Pref)).OrderBy(x => site.Team(x.Specialty)?.Order ?? 99).ThenBy(x => x.Order).ToList();
            List<Specialty> specs = site.Specialties.Where(s => s.Active && list.Any(x => x.Specialty == s.Key)).OrderBy(s => s.Order).ToList();
            string team = specs.Any(s => s.Key == st.Team) ? st.Team : string.Empty;
            StringBuilder sb = new StringBuilder();
            if (st.Pref != null)
            {
                sb.Append("<p class=\"bk-info-box bk-pref\">Showing services with " + Ui.Enc(st.Pref.Name) + ". <a href=\"Book?sp=" + Uri.EscapeDataString(st.Sp) + "\">Show every service</a></p>");
            }
            if (specs.Count > 1)
            {
                sb.Append("<div class=\"bk-chips\" role=\"group\" aria-label=\"Filter by team\"><button type=\"button\" class=\"bk-chip" + (team == string.Empty ? " bk-act" : string.Empty) + "\" onclick=\"BookJs.filter(this,'')\">All</button>");
                foreach (Specialty s in specs)
                {
                    sb.Append("<button type=\"button\" class=\"bk-chip" + (team == s.Key ? " bk-act" : string.Empty) + "\" onclick=\"BookJs.filter(this,'" + Ui.Js(s.Key) + "')\"><i style=\"background:" + Ui.Attr(Ui.SafeColor(s.Color) ? s.Color : DefAccent) + "\"></i>" + Ui.Enc(s.Name) + "</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("<div class=\"bk-opts\">");
            foreach (CareService sv in list)
            {
                Specialty? sp = site.Team(sv.Specialty);
                string price = Price(site, sv, st.Size);
                bool drop = Kind(sv) == "dropoff";
                string side = drop ? price : Dur(MinutesFor(sv, st.Size)) + (price != string.Empty ? "<br>" + price : string.Empty);
                sb.Append("<button type=\"button\" class=\"bk-opt\" data-sp=\"" + Ui.Attr(sv.Specialty) + "\" style=\"--c:" + Ui.Attr(sp != null && Ui.SafeColor(sp.Color) ? sp.Color : DefAccent) + "\" onclick=\"BookJs.pickService('" + Ui.Js(sv.Key) + "')\"" + (team != string.Empty && sv.Specialty != team ? " hidden" : string.Empty) + "><span><i class=\"bk-kind\">" + KindName(sv) + "</i><b>" + Ui.Enc(sv.Name) + "</b><small>" + Ui.Enc(sv.Short) + "</small>" + (drop ? "<small class=\"bk-drop\">" + DropLine(site, sv) + "</small>" : string.Empty) + (!sv.NewOk ? "<small>For existing clients</small>" : string.Empty) + "</span><em>" + side + "</em></button>");
            }
            sb.Append("</div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bk-none\">Nothing can be booked online for this pet right now. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            return sb.ToString();
        }

        private static async Task<string> ProviderPick(Eng e, St st)
        {
            SiteInfo site = e.Site;
            CareService sv = st.Sv!;
            List<Clinician> ps = e.For(sv, st.Sp);
            string many = WhoWord(site, e, sv, st.Sp) == "Groomer" ? "groomers" : site.Settings.ProvidersWord.ToLowerInvariant();
            StringBuilder sb = new StringBuilder("<div class=\"bk-popts\">");
            Slot? any = await e.NextFree(sv, null, st.Sp, st.Size);
            sb.Append("<button type=\"button\" class=\"bk-popt bk-any\" onclick=\"BookJs.pickProvider('any')\"><span class=\"bk-anyic\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"26\" height=\"26\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 7v5l3 2\"/></svg></span><span><b>First available</b><small>Any of our " + ps.Count + " " + Ui.Enc(many) + " for this service</small><em>" + NextText(site, any, e) + "</em></span></button>");
            foreach (Clinician p in ps)
            {
                Slot? n = await e.NextFree(sv, p, st.Sp, st.Size);
                sb.Append("<button type=\"button\" class=\"bk-popt\" onclick=\"BookJs.pickProvider('" + Ui.Js(p.Id) + "')\">" + Face(p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + "</small><em>" + NextText(site, n, e) + "</em></span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Form(SiteInfo site, St st)
        {
            CareService sv = st.Sv!;
            bool sized = site.HasSize(st.Sp);
            bool sexNeeded = st.Sp is "dog" or "cat";
            string word = site.SpeciesName(st.Sp).ToLowerInvariant();
            StringBuilder sb = new StringBuilder("<form class=\"bk-form bk-dform\" id=\"bk-dform\" data-ft=\"" + NewForm() + "\" onsubmit=\"return BookJs.confirm()\" novalidate>");
            sb.Append("<div class=\"bk-hp\" aria-hidden=\"true\"><label>Leave this field empty <input type=\"text\" id=\"bk-hp\" tabindex=\"-1\" autocomplete=\"off\"></label></div>");
            sb.Append("<div class=\"bk-seg\" role=\"radiogroup\" aria-label=\"Have you been here before?\"><label><input type=\"radio\" name=\"bk-new\" value=\"back\" checked><span>We have been here before</span></label><label><input type=\"radio\" name=\"bk-new\" value=\"new\"" + (sv.NewOk ? string.Empty : " disabled") + "><span>We are new</span></label></div>");
            if (!sv.NewOk)
            {
                sb.Append("<p class=\"bk-hint\">This service is for existing " + Ui.Enc(site.Settings.PatientsWord.ToLowerInvariant()) + ". New to us? Please call " + Ui.Enc(site.Settings.Phone) + " or book a wellness exam first.</p>");
            }
            sb.Append("<fieldset class=\"bk-fset\"><h3>About you</h3>");
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>First name</span><input type=\"text\" id=\"bk-first\" maxlength=\"40\" autocomplete=\"given-name\"><em class=\"bk-err\" id=\"bk-e-first\"></em></label><label class=\"bk-field\"><span>Last name</span><input type=\"text\" id=\"bk-last\" maxlength=\"40\" autocomplete=\"family-name\"><em class=\"bk-err\" id=\"bk-e-last\"></em></label></div>");
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>Mobile phone</span><input type=\"tel\" id=\"bk-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"bk-err\" id=\"bk-e-phone\"></em></label><label class=\"bk-field\"><span>Email</span><input type=\"email\" id=\"bk-email\" maxlength=\"120\" autocomplete=\"email\"><em class=\"bk-err\" id=\"bk-e-email\"></em></label></div>");
            sb.Append("<p class=\"bk-hint\">Been here before? Use the email and phone number we have on file, so we can find your records.</p>");
            sb.Append("<label class=\"bk-field\"><span>How should we contact you?</span><select id=\"bk-contact\">" + Ui.Opts(new[] { ("phone", "Phone call"), ("email", "Email"), ("text", "Text message") }, "text") + "</select></label></fieldset>");
            sb.Append("<fieldset class=\"bk-fset\"><h3>About your " + Ui.Enc(word) + "</h3>");
            sb.Append("<div class=\"bk-row2\"><label class=\"bk-field\"><span>Pet's name</span><input type=\"text\" id=\"bk-pname\" maxlength=\"40\" autocomplete=\"off\"><em class=\"bk-err\" id=\"bk-e-pname\"></em></label><label class=\"bk-field\"><span>Breed <i>(optional)</i></span><input type=\"text\" id=\"bk-breed\" maxlength=\"60\" autocomplete=\"off\"></label></div>");
            sb.Append("<div class=\"bk-field\"><span>Sex" + (sexNeeded ? string.Empty : " <i>(optional)</i>") + "</span><div class=\"bk-yn\"><label><input type=\"radio\" name=\"bk-sex\" value=\"m\"><span>Male</span></label><label><input type=\"radio\" name=\"bk-sex\" value=\"f\"><span>Female</span></label></div><label class=\"bk-agree\"><input type=\"checkbox\" id=\"bk-fixed\"><span>Spayed or neutered</span></label><em class=\"bk-err\" id=\"bk-e-sex\"></em></div>");
            sb.Append("<div class=\"bk-row3\"><label class=\"bk-field\"><span>Age in years <i>(if known)</i></span><input type=\"number\" id=\"bk-agey\" min=\"0\" max=\"40\" step=\"1\" inputmode=\"numeric\"><em class=\"bk-err\" id=\"bk-e-agey\"></em></label><label class=\"bk-field\"><span>And months</span><input type=\"number\" id=\"bk-agem\" min=\"0\" max=\"11\" step=\"1\" inputmode=\"numeric\"><em class=\"bk-err\" id=\"bk-e-agem\"></em></label><label class=\"bk-field\"><span>Weight in lb" + (sized ? string.Empty : " <i>(optional)</i>") + "</span><input type=\"text\" id=\"bk-weight\" maxlength=\"6\" inputmode=\"decimal\"><em class=\"bk-err\" id=\"bk-e-weight\"></em></label></div>");
            sb.Append("</fieldset>");
            sb.Append("<label class=\"bk-field\"><span>Anything the front desk should know? <i>(optional)</i></span><textarea id=\"bk-note\" rows=\"3\" maxlength=\"400\" placeholder=\"For example: she is nervous with other dogs, or we may be a few minutes late.\"></textarea></label>");
            sb.Append("<label class=\"bk-agree\"><input type=\"checkbox\" id=\"bk-agree\"><span>We can come at this time, and I will cancel at least " + site.Settings.CancelHours + " hours before if we can't.</span></label><em class=\"bk-err\" id=\"bk-e-agree\"></em><em class=\"bk-err\" id=\"bk-e-form\" role=\"alert\"></em>");
            sb.Append("<button type=\"submit\" class=\"bk-btn bk-btn-accent bk-cbtn\" id=\"bk-cbtn\">Confirm booking</button></form>");
            return sb.ToString();
        }

        private static string Summary(Eng e, St st)
        {
            SiteInfo site = e.Site;
            StringBuilder sb = new StringBuilder("<h2>Your visit</h2>");
            if (st.Sp == string.Empty)
            {
                sb.Append("<p class=\"bk-hint\">Your choices appear here as you go.</p>");
            }
            else
            {
                if (st.Sv != null)
                {
                    sb.Append("<div class=\"bk-sumtop\">" + (SafeImage(st.Sv.Image) ? Pic(Thumb(st.Sv.Image), "bk-sumimg") : string.Empty) + "<div><b>" + Ui.Enc(st.Sv.Name) + "</b><small>" + Facts(site, st.Sv, st.Size) + "</small></div></div>");
                }
                Clinician? p = st.Sv == null ? null : st.P == "any" ? st.Pa : site.Provider(st.P);
                sb.Append("<dl class=\"bk-sumdl\"><div><dt>Pet</dt><dd>" + Ui.Enc(site.SpeciesName(st.Sp)) + (st.Size != string.Empty ? "<br><small>" + Ui.Enc(SizeLong(st.Size)) + "</small>" : string.Empty) + "</dd></div>");
                if (st.Sv != null)
                {
                    sb.Append("<div><dt>With</dt><dd>" + (p != null ? Ui.Enc(p.Name) : st.P == "any" ? "First available" : "&mdash;") + "</dd></div>");
                    if (Kind(st.Sv) == "dropoff")
                    {
                        sb.Append("<div><dt>Drop off</dt><dd>" + (st.T != null ? Day(st.T.Value, false) + "<br>" + DropWindow(site, st.Sv) : "&mdash;") + "</dd></div><div><dt>Pick up</dt><dd>" + (st.T != null ? "From " + HmText(site, Row(st.Sv).PickupHm) : "&mdash;") + "</dd></div>");
                    }
                    else
                    {
                        sb.Append("<div><dt>When</dt><dd>" + (st.T != null ? Day(st.T.Value, false) + "<br>" + Hm(site, st.T.Value) : "&mdash;") + "</dd></div>");
                        sb.Append("<div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Name) + "</dd></div>");
                    }
                }
                sb.Append("</dl>");
            }
            sb.Append("<p class=\"bk-sumnote\">Free to change or cancel online up to " + site.Settings.CancelHours + " hours before.</p>");
            return sb.ToString();
        }

        private string V(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private (string Sp, string Size, string Sv, string P, string Pa, string T, string Day, string Wk, string Pref, string Team, string At) Keys()
        {
            return (V("ksp", 20), V("ksz", 10), V("ksv", 60), V("kp", 50), V("kpa", 50), V("kt", 20), V("kday", 12), V("kwk", 12), V("kpref", 50), V("kteam", 40), V("kat", 20));
        }

        public async Task<ApiResponse> Step()
        {
            ApiResponse response = new ApiResponse();
            if (!Take("step:" + App.Ip(), 60, TimeSpan.FromMinutes(1)))
            {
                response.ExecuteScript("BookJs.toast('Please slow down a moment, then try again.');");
                return response;
            }
            Eng e = new Eng(await Site.Load());
            var k = Keys();
            St st = await Norm(e, k.Sp, k.Size, k.Sv, k.P, k.Pa, k.T, k.Day, k.Wk, k.Pref, k.Team, k.At);
            response.SetElementContents("bk-flow", await Flow(e, st));
            response.SetElementContents("bk-sum", Summary(e, st));
            response.ExecuteScript("BookJs.after();");
            return response;
        }

        private static string SizeFromLb(decimal lb)
        {
            return lb < 20m ? "small" : lb <= 50m ? "medium" : "large";
        }

        private static bool SamePhone(string a, string digits)
        {
            string d = Ui.Digits(a);
            if (d.Length < 7 || digits.Length < 7)
            {
                return false;
            }
            return d == digits || (d.Length >= 10 && digits.Length >= 10 && d.Substring(d.Length - 10) == digits.Substring(digits.Length - 10));
        }

        private static async Task<string> NewCode()
        {
            for (int i = 0; i < 12; i++)
            {
                StringBuilder sb = new StringBuilder("CAH-");
                for (int k = 0; k < 8; k++)
                {
                    char ch;
                    do
                    {
                        ch = CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)];
                    }
                    while (k == 0 && ch == 'S');
                    sb.Append(ch);
                }
                string code = sb.ToString();
                var (dt, emsg) = await Helper.GetDataTable("SELECT COUNT(*) FROM APPOINTMENT WHERE APPT_CODE = @code", new List<SqlParameter> { App.P("code", code, 20) });
                if (emsg == string.Empty && Convert.ToInt32(dt.Rows[0][0]) == 0)
                {
                    return code;
                }
            }
            return string.Empty;
        }

        private static string Call(SiteInfo site)
        {
            return "We could not take this booking online. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.";
        }

        public async Task<ApiResponse> Confirm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("BookJs.unlock();");
            var k = Keys();
            string first = Ui.OneLine(GetDataValue("first"), 40);
            string last = Ui.OneLine(GetDataValue("last"), 40);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string contact = V("contact", 10) is "phone" or "email" ? V("contact", 10) : "text";
            string pname = Ui.OneLine(GetDataValue("pname"), 40);
            string breed = Ui.OneLine(GetDataValue("breed"), 60);
            string sex = V("sex", 2) is "m" or "f" ? V("sex", 2) : string.Empty;
            bool isFixed = V("fixed", 2) == "1";
            string ageY = V("agey", 4);
            string ageM = V("agem", 4);
            string wIn = V("weight", 8);
            string note = Ui.Clip(GetDataValue("note"), 400);
            bool isNew = V("newp", 10) == "new";
            string ftok = V("ftok", 40);
            SiteInfo site = await Site.Load();
            Eng e = new Eng(site);
            string ip = App.Ip();
            if (V("hp", 200) != string.Empty)
            {
                await App.Log("web", "book online", "blocked form from " + ip, false);
                response.SetElementContents("bk-e-form", Call(site));
                return response;
            }
            if (!FormAge(ftok))
            {
                bool known;
                lock (Forms)
                {
                    known = Forms.ContainsKey(ftok);
                }
                if (!known)
                {
                    response.ExecuteScript("BookJs.form('" + NewForm() + "');");
                }
                response.SetElementContents("bk-e-form", "Please check your details, then press Confirm booking again.");
                return response;
            }
            St ls = await Norm(e, k.Sp, k.Size, k.Sv, k.P, k.Pa, k.T, string.Empty, k.Wk, k.Pref, k.Team);
            if (ls.Sv == null || ls.P == string.Empty)
            {
                response.SetElementContents("bk-e-form", "This service can no longer be booked online. Please start again.");
                return response;
            }
            CareService svc = ls.Sv;
            Dictionary<string, string> errs = new Dictionary<string, string>();
            if (first == string.Empty)
            {
                errs["first"] = "Enter your first name.";
            }
            if (last == string.Empty)
            {
                errs["last"] = "Enter your last name.";
            }
            if (Ui.Digits(phone).Length < 7)
            {
                errs["phone"] = "Enter a phone number we can reach you on.";
            }
            if (!Ui.Email(email))
            {
                errs["email"] = "Enter a valid email address.";
            }
            if (pname == string.Empty)
            {
                errs["pname"] = "Enter your pet's name.";
            }
            if (sex == string.Empty && ls.Sp is "dog" or "cat")
            {
                errs["sex"] = "Choose male or female.";
            }
            int years = 0;
            int months = 0;
            if (ageY != string.Empty && (!int.TryParse(ageY, NumberStyles.Integer, Ui.Inv, out years) || years < 0 || years > 40))
            {
                errs["agey"] = "Enter years from 0 to 40.";
            }
            if (ageM != string.Empty && (!int.TryParse(ageM, NumberStyles.Integer, Ui.Inv, out months) || months < 0 || months > 11))
            {
                errs["agem"] = "Enter months from 0 to 11.";
            }
            decimal? lb = null;
            bool sized = site.HasSize(ls.Sp);
            if (wIn != string.Empty)
            {
                if (decimal.TryParse(wIn.Replace(',', '.'), NumberStyles.Number, Ui.Inv, out decimal w) && w >= 0.1m && w <= 300m)
                {
                    lb = Math.Round(w, 1);
                }
                else
                {
                    errs["weight"] = "Enter the weight in pounds, for example 24.5.";
                }
            }
            else if (sized)
            {
                errs["weight"] = "Enter your " + site.SpeciesName(ls.Sp).ToLowerInvariant() + "'s weight in pounds. A good guess is fine.";
            }
            if (isNew && !svc.NewOk)
            {
                errs["form"] = "This service is for existing " + site.Settings.PatientsWord.ToLowerInvariant() + ". Please call us on " + site.Settings.Phone + " and we will help you book.";
            }
            if (V("agree", 2) != "1")
            {
                errs["agree"] = "Please tick this box to continue.";
            }
            if (errs.Count > 0)
            {
                foreach (KeyValuePair<string, string> kv in errs)
                {
                    response.SetElementContents("bk-e-" + kv.Key, Ui.Enc(kv.Value));
                }
                response.ExecuteScript("BookJs.invalid();");
                return response;
            }
            if (sized && lb != null && SizeFromLb(lb.Value) != ls.Size && (MinutesFor(svc, SizeFromLb(lb.Value)) != MinutesFor(svc, ls.Size) || PriceFor(svc, SizeFromLb(lb.Value)) != PriceFor(svc, ls.Size)))
            {
                string right = SizeFromLb(lb.Value);
                St fix = await Norm(e, ls.Sp, right, svc.Key, ls.P, string.Empty, string.Empty, string.Empty, k.Wk, k.Pref, k.Team);
                string rw = SizeName(right).ToLowerInvariant();
                string dw = site.SpeciesName(ls.Sp).ToLowerInvariant();
                fix.Msg = Ui.Enc(pname) + " weighs " + lb.Value.ToString("0.#", Ui.Inv) + " lb, so " + (sex == "f" ? "she" : sex == "m" ? "he" : "your " + dw) + " is a " + rw + " " + dw + ". " + Ui.Enc(svc.Name) + " for a " + rw + " " + dw + " takes " + Dur(MinutesFor(svc, right)) + (site.Settings.ShowPrices && PriceFor(svc, right) > 0 ? " and costs " + Cash(PriceFor(svc, right)) : string.Empty) + ". Please pick a time again; your details are kept.";
                response.SetElementContents("bk-flow", await Flow(e, fix));
                response.SetElementContents("bk-sum", Summary(e, fix));
                response.ExecuteScript("BookJs.after();BookJs.toast('Please pick a new time for a " + rw + " " + Ui.Js(dw) + ".');");
                return response;
            }
            if (ls.T == null || ls.Pa == null)
            {
                await TimeGone(response, e, ls);
                return response;
            }
            Opening? hit = await e.Hit(svc, ls.Pa, ls.T.Value, ls.Size, true);
            if (hit == null)
            {
                await TimeGone(response, e, ls);
                return response;
            }
            string digits = Ui.Digits(phone);
            if (!Take("try:" + ip, 10, TimeSpan.FromHours(1)) || !Take("tryemail:" + email, 5, TimeSpan.FromHours(1)))
            {
                await App.Log("web", "book online", "too many tries from " + ip, false);
                response.SetElementContents("bk-e-form", "We have had a lot of booking attempts from you in the last hour. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.");
                return response;
            }
            string capErr = await Caps(site, email, digits);
            if (capErr != string.Empty)
            {
                await App.Log("web", "book online", "daily cap reached", false);
                response.SetElementContents("bk-e-form", capErr);
                return response;
            }
            var (cands, cerr) = await Helper.GetListT<BkClient>("SELECT CLIENT_ID AS ClientId, FIRST_NAME AS FirstName, LAST_NAME AS LastName, PHONE AS Phone, PHONE2 AS Phone2, EMAIL AS Email FROM CLIENT WHERE ACTIVE = 1 AND LOWER(EMAIL) = @email ORDER BY CREATED_UTC", new List<SqlParameter> { App.P("email", email, 120) });
            if (cerr != string.Empty || cands == null)
            {
                response.SetElementContents("bk-e-form", Call(site));
                return response;
            }
            BkClient? found = cands.FirstOrDefault(c => SamePhone(c.Phone, digits) || SamePhone(c.Phone2, digits));
            bool newClient = found == null;
            if (newClient && !svc.NewOk)
            {
                response.SetElementContents("bk-e-form", Call(site));
                return response;
            }
            string cid = found?.ClientId ?? App.NewId();
            BkPet? pet = null;
            if (found != null)
            {
                var (cnt, nerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM APPOINTMENT WHERE CLIENT_ID = @cid AND START_AT >= @now AND STATUS IN (N'booked', N'confirmed') AND SOURCE = N'web'", new List<SqlParameter> { App.P("cid", cid, 50), App.PDate("now", e.Now) });
                if (nerr != string.Empty || Convert.ToInt32(cnt.Rows[0][0]) >= 6)
                {
                    response.SetElementContents("bk-e-form", Call(site));
                    return response;
                }
                var (prow, perr) = await Helper.GetOneT<BkPet>("SELECT TOP 1 PET_ID AS PetId, PET_NAME AS PetName, BREED AS Breed, SEX AS Sex, SIZE AS Size FROM PET WHERE CLIENT_ID = @cid AND ACTIVE = 1 AND DECEASED_DATE IS NULL AND SPECIES_KEY = @sp AND LOWER(PET_NAME) = @pn ORDER BY CREATED_UTC", new List<SqlParameter> { App.P("cid", cid, 50), App.P("sp", ls.Sp, 20), App.P("pn", pname.ToLowerInvariant(), 60) });
                if (perr != string.Empty)
                {
                    response.SetElementContents("bk-e-form", Call(site));
                    return response;
                }
                pet = prow;
            }
            bool newPet = pet == null;
            string pid = pet?.PetId ?? App.NewId();
            string petName = pet?.PetName ?? pname;
            DateTime? birth = ageY != string.Empty || ageM != string.Empty ? e.Today.AddYears(-years).AddMonths(-months) : null;
            string size = sized ? (lb != null ? SizeFromLb(lb.Value) : ls.Size) : string.Empty;
            Clinician who = ls.Pa;
            BookRow row = Row(svc);
            DateTime ba = hit.ProcAt ?? hit.Start;
            DateTime bz = hit.ProcAt.HasValue ? hit.ProcAt.Value.AddMinutes(hit.ProcMinutes) : hit.End;
            DateTime pz = hit.PickupAt ?? hit.End;
            decimal price = PriceFor(svc, size != string.Empty ? size : ls.Size);
            string aid = App.NewId();
            string code = string.Empty;
            string result = string.Empty;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                code = await NewCode();
                if (code == string.Empty)
                {
                    break;
                }
                List<SqlParameter> ps = new List<SqlParameter>
                {
                    App.P("sid", who.Id, 50), App.P("rt", row.RoomtypeId, 50), App.PDate("ba", ba), App.PDate("bz", bz), App.PDate("st", hit.Start), App.PDate("en", hit.End), App.PDate("pz", pz),
                    App.PInt("mins", (int)(hit.End - hit.Start).TotalMinutes), App.PDate("proc", hit.ProcAt), App.PInt("pmins", hit.ProcAt.HasValue ? hit.ProcMinutes : 0), App.PDate("pick", hit.PickupAt),
                    App.P("aid", aid, 50), App.P("code", code, 20), App.P("cid", cid, 50), App.P("pid", pid, 50), App.P("bt", svc.Key, 50), App.P("vt", row.VisitType, 20),
                    App.PDec("price", price), App.PBit("nc", newClient || isNew), App.P("note", note, 600), App.P("lid", App.NewId(), 50),
                    App.P("first", first, 60), App.P("last", last, 60), App.P("phone", phone, 30), App.P("email", email, 120), App.P("pref", contact, 10),
                    App.P("pname", pname, 60), App.P("sp", ls.Sp, 20), App.P("breed", breed, 60), App.P("sex", sex, 1), App.PBit("fixed", isFixed), new SqlParameter("birth", SqlDbType.Date) { Value = birth.HasValue ? birth.Value : DBNull.Value }, App.P("size", size, 10),
                    App.P("wid", App.NewId(), 50), App.PDec("lb", lb ?? 0m), App.PDate("now", e.Now)
                };
                List<string> sql = new List<string>
                {
                    "UPDATE STAFF SET SYSDTE = SYSDTE WHERE STAFF_ID = @sid",
                    "UPDATE ROOMTYPE SET SYSDTE = SYSDTE WHERE ROOMTYPE_ID = @rt",
                    "IF @rt <> N'' AND (SELECT COUNT(*) FROM APPOINTMENT WHERE ROOMTYPE_ID = @rt AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) >= (SELECT COUNT(*) FROM ROOM WHERE ROOMTYPE_ID = @rt AND ACTIVE = 1) THROW 50003, N'SLOTTAKEN', 1;",
                    "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE STAFF_ID = @sid AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) THROW 50003, N'SLOTTAKEN', 1;"
                };
                if (newClient)
                {
                    sql.Add("INSERT INTO CLIENT (CLIENT_ID, CLIENT_NO, FIRST_NAME, LAST_NAME, PHONE, PHONE2, EMAIL, CONTACT_PREF, ADDRESS1, CITY, STATE, ZIP, NOTE, ALERT, ACTIVE, CREATED_UTC, CREATED_BY, SYSDTE) " +
                        "SELECT @cid, N'C' + RIGHT(N'0000' + CAST(X.N AS NVARCHAR(10)), CASE WHEN X.N > 99999 THEN LEN(CAST(X.N AS NVARCHAR(10))) ELSE 5 END), @first, @last, @phone, N'', @email, @pref, N'', N'', N'', N'', N'', N'', 1, SYSUTCDATETIME(), N'web', GETDATE() " +
                        "FROM (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(CLIENT_NO, 2, 10) AS INT)), 1000) + 1 AS N FROM CLIENT WITH (UPDLOCK, HOLDLOCK) WHERE CLIENT_NO LIKE N'C[0-9]%' AND SUBSTRING(CLIENT_NO, 2, 20) NOT LIKE N'%[^0-9]%') X");
                }
                if (newPet)
                {
                    sql.Add("INSERT INTO PET (PET_ID, CLIENT_ID, PET_NAME, SPECIES_KEY, BREED, SEX, FIXED, BIRTH_DATE, COLOR, SIZE, MICROCHIP, ALERT, PHOTO, ACTIVE, DECEASED_DATE, CREATED_UTC, CREATED_BY, SYSDTE) VALUES (@pid, @cid, @pname, @sp, @breed, @sex, @fixed, @birth, N'', @size, N'', N'', N'', 1, NULL, SYSUTCDATETIME(), N'web', GETDATE())");
                }
                else
                {
                    sql.Add("UPDATE PET SET BREED = CASE WHEN BREED = N'' THEN @breed ELSE BREED END, SEX = CASE WHEN SEX = N'' THEN @sex ELSE SEX END, FIXED = CASE WHEN @fixed = 1 THEN 1 ELSE FIXED END, BIRTH_DATE = ISNULL(BIRTH_DATE, @birth), SIZE = CASE WHEN @size <> N'' THEN @size ELSE SIZE END, SYSDTE = GETDATE() WHERE PET_ID = @pid");
                    sql.Add("IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid AND APPT_ID <> @aid AND STATUS IN (N'booked', N'confirmed', N'checkedin') AND VISIT_TYPE <> N'boarding' AND START_AT < @pz AND ISNULL(PICKUP_AT, END_AT) > @st) THROW 50006, N'PETBUSY', 1;");
                }
                if (lb != null)
                {
                    sql.Add("INSERT INTO PETWEIGHT (WEIGHT_ID, PET_ID, WEIGHED_AT, LB, VISIT_ID, STAFF_ID) VALUES (@wid, @pid, @now, @lb, N'', N'web')");
                }
                sql.Add("INSERT INTO APPOINTMENT (APPT_ID, APPT_CODE, CLIENT_ID, PET_ID, BOOKTYPE_ID, VISIT_TYPE, STAFF_ID, ROOMTYPE_ID, START_AT, END_AT, MINUTES, PROC_AT, PROC_MINUTES, PICKUP_AT, EST_PRICE, SOURCE, STATUS, NEW_CLIENT, NOTE, CREATED_UTC, CREATED_BY) VALUES (@aid, @code, @cid, @pid, @bt, @vt, @sid, @rt, @st, @en, @mins, @proc, @pmins, @pick, @price, N'web', N'booked', @nc, @note, SYSUTCDATETIME(), N'web')");
                sql.Add("INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), N'web', N'Booked online')");
                result = await Helper.PutData(sql, ps);
                bool clash = Helper.IsError(result) && (result.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || result.Contains("unique", StringComparison.OrdinalIgnoreCase) || result.Contains("UX_", StringComparison.OrdinalIgnoreCase));
                if (!clash)
                {
                    break;
                }
            }
            if (code == string.Empty || Helper.IsError(result))
            {
                await App.Log("web", "book online", petName + " · " + svc.Name + " · failed", false);
                if (result.Contains("SLOTTAKEN", StringComparison.OrdinalIgnoreCase) || result.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    Forget(hit.Start.Date);
                    await TimeGone(response, new Eng(site), ls, result.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase) ? "That time isn't available. Please pick another." : string.Empty);
                    return response;
                }
                response.SetElementContents("bk-e-form", Call(site));
                return response;
            }
            Forget(hit.Start.Date);
            await App.Log("web", "book online", petName + " · " + svc.Name + " · " + code, true);
            await Mail(site, cid, email, first, petName, svc, who, hit, code);
            response.SetElementContents("bk-flow", Booked(site, svc, who, hit, code, petName, breed != string.Empty ? breed : pet?.Breed ?? string.Empty, ls.Sp, first, price));
            response.SetElementContents("bk-sum", "<h2>What happens next</h2><ol class=\"bk-nextl\"><li>We hold this time for you and " + Ui.Enc(petName) + ".</li><li>The front desk may call or text to confirm.</li><li>Need to change? Use your code on <a href=\"MyAppointment?c=" + Uri.EscapeDataString(code) + "\">My booking</a>.</li></ol>");
            response.ExecuteScript("BookJs.booked();");
            return response;
        }

        private static async Task TimeGone(ApiResponse response, Eng e, St st, string msg = "")
        {
            if (st.T != null)
            {
                Forget(st.T.Value.Date);
            }
            st.T = null;
            st.Pa = null;
            Eng fresh = new Eng(e.Site);
            response.SetElementContents("bk-flow", await Flow(fresh, st));
            response.SetElementContents("bk-sum", Summary(fresh, st));
            response.ExecuteScript("BookJs.after();BookJs.toast('" + Ui.Js(msg != string.Empty ? msg : "Sorry, that time was just taken. Please choose another.") + "');");
        }

        private static async Task<string> Caps(SiteInfo site, string email, string digits)
        {
            string d10 = digits.Length > 10 ? digits.Substring(digits.Length - 10) : digits;
            List<SqlParameter> ps = new List<SqlParameter> { App.PDate("since", DateTime.UtcNow.AddDays(-1)), App.PDate("upto", DateTime.UtcNow.AddMinutes(5)), App.P("email", email, 120), App.P("d10", d10, 30) };
            const string Web = "A.SOURCE = N'web' AND A.CREATED_BY = N'web' AND A.CREATED_UTC >= @since AND A.CREATED_UTC <= @upto";
            var (dt, emsg) = await Helper.GetDataTable("SELECT (SELECT COUNT(*) FROM APPOINTMENT A WHERE " + Web + ") AS AllN, (SELECT COUNT(*) FROM APPOINTMENT A JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID WHERE " + Web + " AND LOWER(C.EMAIL) = @email) AS MailN, (SELECT COUNT(*) FROM APPOINTMENT A JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID WHERE " + Web + " AND RIGHT(" + PhoneDigits + ", 10) = @d10) AS PhoneN", ps);
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                return Call(site);
            }
            if (Convert.ToInt32(dt.Rows[0]["AllN"]) >= 200)
            {
                return "We have had a lot of online bookings today. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will find you a time.";
            }
            if (Convert.ToInt32(dt.Rows[0]["MailN"]) >= 3 || Convert.ToInt32(dt.Rows[0]["PhoneN"]) >= 3)
            {
                return "You have already made several bookings online today. Please call us on " + Ui.Enc(site.Settings.Phone) + " for more.";
            }
            return string.Empty;
        }

        private static async Task Mail(SiteInfo site, string cid, string email, string first, string petName, CareService sv, Clinician who, Opening o, string code)
        {
            if (!Letters.Kinds.Contains("booking"))
            {
                return;
            }
            const string F = "font-family:Arial,Helvetica,sans-serif;color:#1f2a27;";
            string when = o.ProcAt.HasValue ? LongDay(o.Start) + ", drop off " + DropWindow(site, sv) + ", pick up from " + Hm(site, o.PickupAt ?? o.End) : LongDay(o.Start) + " at " + Hm(site, o.Start);
            string html = "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"" + F + "max-width:560px;\"><tr><td style=\"padding:20px 0;font-size:20px;font-weight:bold;\">" + Ui.Enc(site.Settings.Name) + "</td></tr><tr><td style=\"font-size:15px;padding-bottom:12px;\">Hi " + Ui.Enc(first) + ", you're booked.</td></tr><tr><td style=\"font-size:15px;padding-bottom:6px;\"><b>" + Ui.Enc(sv.Name) + "</b> for " + Ui.Enc(petName) + " with " + Ui.Enc(who.Name) + "</td></tr><tr><td style=\"font-size:15px;padding-bottom:12px;\">" + when + "</td></tr><tr><td style=\"font-size:15px;padding-bottom:12px;\">Booking code: <b>" + Ui.Enc(code) + "</b></td></tr><tr><td style=\"font-size:13px;color:#5b6b66;\">" + Ui.Enc(site.Settings.Address) + " &middot; " + Ui.Enc(site.Settings.Phone) + "</td></tr></table>";
            Ctx web = new Ctx { U = new StaffRow { StaffId = "web", StaffName = "Website" } };
            await Letters.Send(web, "booking", cid, string.Empty, string.Empty, email, "Your booking " + code + " at " + site.Settings.Name, html, new List<string>());
        }

        private static string Booked(SiteInfo site, CareService sv, Clinician who, Opening o, string code, string petName, string breed, string sp, string first, decimal price)
        {
            bool drop = o.ProcAt.HasValue;
            string petLine = petName + " (" + (breed != string.Empty ? breed : site.SpeciesName(sp)) + ")";
            StringBuilder sb = new StringBuilder("<section class=\"bk-booked\" id=\"bk-booked\" tabindex=\"-1\"" + IcsAttrs(site, sv, who, o, code, petName) + "><div class=\"bk-tick\" aria-hidden=\"true\"></div><h2>You're booked, " + Ui.Enc(first) + "</h2><p>Keep your booking code. You need it with your email or phone to change or cancel.</p><div class=\"bk-code\">" + Ui.Enc(code) + "</div><dl class=\"bk-sumdl\">");
            sb.Append("<div><dt>Pet</dt><dd>" + Ui.Enc(petLine) + "</dd></div><div><dt>Service</dt><dd>" + Ui.Enc(sv.Name) + "</dd></div><div><dt>With</dt><dd>" + Ui.Enc(who.Name) + "</dd></div>");
            if (drop)
            {
                sb.Append("<div><dt>Drop off</dt><dd>" + LongDay(o.Start) + ", " + DropWindow(site, sv) + "</dd></div><div><dt>Pick up</dt><dd>From " + Hm(site, o.PickupAt ?? o.End) + " the same day. We call you when " + Ui.Enc(petName) + " is ready.</dd></div>");
            }
            else
            {
                sb.Append("<div><dt>When</dt><dd>" + LongDay(o.Start) + " at " + Hm(site, o.Start) + " <small>(" + Dur((int)(o.End - o.Start).TotalMinutes) + ")</small></dd></div>");
            }
            sb.Append("<div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Address) + "</dd></div>");
            if (site.Settings.ShowPrices && price > 0)
            {
                sb.Append("<div><dt>Price</dt><dd>" + (Kind(sv) == "groom" ? string.Empty : "From ") + Cash(price) + "</dd></div>");
            }
            sb.Append("</dl>");
            if (drop && sv.PreOp.Count > 0)
            {
                sb.Append("<div class=\"bk-preop\"><h3>Before you come</h3><ul>");
                foreach (string line in sv.PreOp)
                {
                    sb.Append("<li>" + Ui.Enc(line) + "</li>");
                }
                sb.Append("</ul></div>");
            }
            else
            {
                sb.Append("<p class=\"bk-info-box\">Please arrive 10 minutes early. Dogs on a lead and cats in a carrier, please.</p>");
            }
            sb.Append("<div class=\"bk-bacts\"><button type=\"button\" class=\"bk-btn bk-btn-accent\" onclick=\"BookJs.ics('bk-booked')\">Add to calendar</button><a class=\"bk-btn bk-btn-ghost\" href=\"MyAppointment?c=" + Uri.EscapeDataString(code) + "\">Manage booking</a><a class=\"bk-btn bk-btn-ghost\" href=\"Book\">Book another</a></div>");
            if (App.Demo)
            {
                sb.Append("<p class=\"bk-hint bk-demo-note\">Demo data: this booking is in the shared demo, which resets every day. <a href=\"AdminLogin\">Sign in to the admin</a> to see it on today's list and the appointment book.</p>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string Utc(SiteInfo site, DateTime local)
        {
            DateTime u;
            try
            {
                u = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), App.Zone(site.Settings.TimeZone));
            }
            catch (ArgumentException)
            {
                u = DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
            return u.ToString("yyyyMMdd'T'HHmmss'Z'", Ui.Inv);
        }

        private static string IcsAttrs(SiteInfo site, CareService sv, Clinician who, Opening o, string code, string petName)
        {
            string loc = site.Settings.Name + ", " + site.Settings.Address;
            List<Dictionary<string, string>> ev = new List<Dictionary<string, string>>();
            if (o.ProcAt.HasValue)
            {
                DateTime pick = o.PickupAt ?? o.End;
                ev.Add(new Dictionary<string, string> { ["s"] = Utc(site, o.Start), ["e"] = Utc(site, o.End), ["t"] = "Drop off " + petName + ": " + sv.Name + " | " + site.Settings.Name, ["d"] = "Booking code " + code + ". " + string.Join(" ", sv.PreOp), ["u"] = code + "-in" });
                ev.Add(new Dictionary<string, string> { ["s"] = Utc(site, pick), ["e"] = Utc(site, pick.AddMinutes(30)), ["t"] = "Pick up " + petName + " | " + site.Settings.Name, ["d"] = "Booking code " + code + ". We call you when " + petName + " is ready.", ["u"] = code + "-out" });
            }
            else
            {
                ev.Add(new Dictionary<string, string> { ["s"] = Utc(site, o.Start), ["e"] = Utc(site, o.End), ["t"] = petName + ": " + sv.Name + " with " + who.Name + " | " + site.Settings.Name, ["d"] = "Booking code " + code, ["u"] = code });
            }
            return " data-code=\"" + Ui.Attr(code) + "\" data-loc=\"" + Ui.Attr(loc) + "\" data-ev=\"" + Ui.Attr(JsonSerializer.Serialize(ev)) + "\"";
        }

        private static string Short(Clinician p)
        {
            return p.Name.StartsWith("Dr", StringComparison.Ordinal) ? "Dr. " + p.Name.Split(' ')[^1] : p.Name.Split(' ')[0];
        }

        private static async Task<string> Strip(Eng e, CareService sv, Clinician? only, DateTime wk, DateTime day, string sp, string size)
        {
            SiteInfo site = e.Site;
            bool drop = Kind(sv) == "dropoff";
            DateTime last = e.Last;
            DateTime prev = wk.AddDays(-7) < e.Today ? e.Today : wk.AddDays(-7);
            StringBuilder sb = new StringBuilder("<div class=\"bk-wk\"><button type=\"button\" class=\"bk-wnav\" aria-label=\"Earlier days\" onclick=\"BookJs.week('" + Ui.Ymd(prev) + "')\"" + (wk <= e.Today ? " disabled" : string.Empty) + ">&lsaquo;</button><div class=\"bk-days\">");
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = wk.AddDays(i);
                bool closed = e.Clinic(d) == null;
                int n = d > last || closed ? 0 : (await e.Times(sv, only, d, sp, size)).Count;
                total += n;
                string label = closed ? "Closed" : d > last ? "&mdash;" : n == 0 ? "Full" : drop ? "Space" : n + (n == 1 ? " time" : " times");
                sb.Append("<button type=\"button\" class=\"bk-day" + (d == day && n > 0 ? " bk-act" : string.Empty) + "\" aria-pressed=\"" + (d == day && n > 0 ? "true" : "false") + "\" onclick=\"BookJs.day('" + Ui.Ymd(d) + "')\"" + (n == 0 ? " disabled" : string.Empty) + ">" + (d == e.Today ? "<small class=\"bk-tdy\" data-d=\"" + d.ToString("ddd", Ui.Inv) + "\">Today</small>" : "<small>" + d.ToString("ddd", Ui.Inv) + "</small>") + "<b>" + d.Day + "</b><small>" + d.ToString("MMM", Ui.Inv) + "</small><em>" + label + "</em></button>");
            }
            sb.Append("</div><button type=\"button\" class=\"bk-wnav\" aria-label=\"Later days\" onclick=\"BookJs.week('" + Ui.Ymd(wk.AddDays(7)) + "')\"" + (wk.AddDays(7) > last ? " disabled" : string.Empty) + ">&rsaquo;</button></div>");
            if (total == 0)
            {
                Slot? n = null;
                for (int i = 7; i < 70 && n == null; i++)
                {
                    DateTime d = wk.AddDays(i);
                    if (d > last)
                    {
                        break;
                    }
                    List<Slot> l = await e.Times(sv, only, d, sp, size);
                    if (l.Count > 0)
                    {
                        n = l[0];
                    }
                }
                sb.Append("<div class=\"bk-nofree\"><p>" + (drop ? "No free procedure days in these seven days." : "No free times in these seven days.") + "</p>" + (n != null ? "<button type=\"button\" class=\"bk-btn bk-btn-sm bk-btn-accent\" onclick=\"BookJs.week('" + Ui.Ymd(n.Start.Date) + "')\">Jump to " + Day(n.Start, false) + "</button>" : "<p class=\"bk-hint\">Please call " + Ui.Enc(site.Settings.Phone) + " and we will find you a time.</p>") + "</div>");
                return sb.ToString();
            }
            List<Slot> slots = await e.Times(sv, only, day, sp, size);
            sb.Append("<p class=\"bk-dayname\">" + LongDay(day) + "</p>");
            if (drop)
            {
                sb.Append("<div class=\"bk-tgroup\"><h3>Drop-off time</h3><div class=\"bk-times\">");
                foreach (Slot x in slots)
                {
                    sb.Append("<button type=\"button\" class=\"bk-ts\" onclick=\"BookJs.pickTime('" + Stamp(x.Start) + "','" + Ui.Js(x.P.Id) + "')\"><b>" + DropWindow(site, sv) + "</b>" + (only == null ? "<small>" + Ui.Enc(Short(x.P)) + "</small>" : string.Empty) + "</button>");
                }
                sb.Append("</div></div><p class=\"bk-pick\">Pick-up from " + HmText(site, Row(sv).PickupHm) + " the same day. We call you when your pet is ready.</p>");
                if (sv.PreOp.Count > 0)
                {
                    sb.Append("<div class=\"bk-preop\"><h3>Before you come</h3><ul>");
                    foreach (string line in sv.PreOp)
                    {
                        sb.Append("<li>" + Ui.Enc(line) + "</li>");
                    }
                    sb.Append("</ul></div>");
                }
                return sb.ToString();
            }
            (string Name, int From, int To)[] parts = { ("Morning", 0, 720), ("Afternoon", 720, 1020), ("Evening", 1020, 1440) };
            foreach ((string name, int from, int to) in parts)
            {
                List<Slot> part = slots.Where(x => x.Start.TimeOfDay.TotalMinutes >= from && x.Start.TimeOfDay.TotalMinutes < to).ToList();
                if (part.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"bk-tgroup\"><h3>" + name + "</h3><div class=\"bk-times\">");
                foreach (Slot x in part)
                {
                    sb.Append("<button type=\"button\" class=\"bk-ts\" onclick=\"BookJs.pickTime('" + Stamp(x.Start) + "','" + Ui.Js(x.P.Id) + "')\"><b>" + Hm(site, x.Start) + "</b>" + (only == null ? "<small>" + Ui.Enc(Short(x.P)) + "</small>" : string.Empty) + "</button>");
                }
                sb.Append("</div></div>");
            }
            return sb.ToString();
        }
    }
}
