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
    public class BookStay : WebPage
    {
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string PhoneDigits = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), N'+', N'')";
        private static readonly Dictionary<string, List<DateTime>> Made = new Dictionary<string, List<DateTime>>();
        private static readonly Dictionary<string, DateTime> Forms = new Dictionary<string, DateTime>();
        private static readonly (string Key, string Name)[] Sizes = { ("small", "Small (under 20 lb)"), ("medium", "Medium (20 to 50 lb)"), ("large", "Large (over 50 lb)") };

        private const string BusyCond = "SELECT A.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND A.ROOM_ID <> N'' AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din " +
            "UNION ALL SELECT X.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))";

        private const string RoomGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT A WHERE A.ROOM_ID = @room AND A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID WHERE X.ROOM_ID = @room AND X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))) THROW 50001, N'ROOMBUSY', 1;";

        private const string PetGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND APPT_ID <> @ign AND CAST(START_AT AS DATE) < @dout AND CAST(END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom' AND VISIT_ID <> @ivis AND (CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) > @din OR CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) < @today)) THROW 50005, N'PETBUSY', 1;";

        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
        }

        private sealed class St
        {
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public string In { get; set; } = string.Empty;
            public bool DatesOk { get; set; }
            public string Err { get; set; } = string.Empty;
            public string Sp { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
            public string Want { get; set; } = string.Empty;
            public RunType? Run { get; set; }
            public List<string> Adds { get; set; } = new List<string>();
            public bool AddsDone { get; set; }

            public int Nights => From != null && To != null ? Math.Max(0, (To.Value.Date - From.Value.Date).Days) : 0;
        }

        private sealed class Eng
        {
            private readonly Dictionary<string, HashSet<string>> busy = new Dictionary<string, HashSet<string>>();
            private List<BsRoom>? rooms;

            public Dictionary<string, BsPrice> Prices { get; private set; } = new Dictionary<string, BsPrice>();

            public static async Task<Eng> Make(SiteInfo site)
            {
                Eng e = new Eng(site);
                var (list, emsg) = await Helper.GetListT<BsPrice>("SELECT ITEM_ID AS ItemId, PRICE AS Price, PRICE_M AS PriceM, PRICE_L AS PriceL FROM ITEM WHERE ITEM_TYPE = N'boarding'");
                if (emsg == string.Empty && list != null)
                {
                    e.Prices = list.GroupBy(x => x.ItemId).ToDictionary(g => g.Key, g => g.First());
                }
                return e;
            }

            private Eng(SiteInfo site)
            {
                Site = site;
                Now = App.Now(site.S);
                Last = Now.Date.AddDays(Math.Clamp(site.Settings.BookAheadDays, 1, 365));
                Earliest = Now.AddHours(Math.Max(0, site.Settings.MinNoticeHours));
            }

            public SiteInfo Site { get; }
            public DateTime Now { get; }
            public DateTime Today => Now.Date;
            public DateTime Last { get; }
            public DateTime Earliest { get; }

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

            public async Task<List<BsRoom>> Rooms()
            {
                if (rooms == null)
                {
                    var (list, emsg) = await Helper.GetListT<BsRoom>("SELECT R.ROOM_ID AS RoomId, R.ROOM_NAME AS RoomName, R.ROOMTYPE_ID AS RoomtypeId, R.ROOM_ORDER AS RoomOrder FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID WHERE R.ACTIVE = 1 AND T.ACTIVE = 1 AND T.PURPOSE = N'boarding' ORDER BY R.ROOM_ORDER, R.ROOM_NAME");
                    rooms = emsg == string.Empty && list != null ? list : new List<BsRoom>();
                }
                return rooms;
            }

            public async Task<HashSet<string>> Busy(DateTime from, DateTime to, string ignore)
            {
                string key = Ui.Ymd(from) + "|" + Ui.Ymd(to) + "|" + ignore;
                if (!busy.TryGetValue(key, out HashSet<string>? set))
                {
                    var (list, emsg) = await Helper.GetListT<BsBusy>(BusyCond, new List<SqlParameter> { App.PDate("din", from.Date), App.PDate("dout", to.Date), App.PDate("today", Today), App.P("ign", ignore, 50), App.P("ivis", string.Empty, 50), App.PBit("inst", false) });
                    set = emsg == string.Empty && list != null ? list.Select(x => x.RoomId).ToHashSet() : (await Rooms()).Select(x => x.RoomId).ToHashSet();
                    busy[key] = set;
                }
                return set;
            }

            public async Task<List<BsRoom>> FreeRooms(RunType r, DateTime from, DateTime to, string ignore = "")
            {
                HashSet<string> b = await Busy(from, to, ignore);
                return (await Rooms()).Where(x => x.RoomtypeId == r.Key && !b.Contains(x.RoomId)).ToList();
            }

            public async Task<DateTime?> FullOn(RunType r, DateTime from, DateTime to, string ignore = "")
            {
                for (DateTime d = from.Date; d < to.Date; d = d.AddDays(1))
                {
                    if ((await FreeRooms(r, d, d.AddDays(1), ignore)).Count == 0)
                    {
                        return d;
                    }
                }
                return null;
            }
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string run = Ui.Clip(QueryValue("run"), 50);
            View v = await Render(site, run != string.Empty ? run : Ui.Clip(QueryValue("r"), 50));
            Head(site, v.Title, v.Desc);
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, "bs", "BookStayJs", "BookStay", site).Replace("{plhd_main}", v.Main);
        }

        private void Head(SiteInfo site, string title, string desc)
        {
            string full = title + " | " + site.Settings.Name;
            HtmlDoc.SetTitle(Ui.Enc(full));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", Ui.Enc(desc));
            HttpContext? h = HttpCurrent.Context;
            string root = h != null ? h.Request.Scheme + "://" + h.Request.Host + h.Request.PathBase + "/" : string.Empty;
            foreach ((string k, string v) in new[] { ("og:type", "website"), ("og:site_name", site.Settings.Name), ("og:title", full), ("og:description", desc), ("og:image", root + "images/hospital/full/dog-runs.webp") })
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Enc(v) } });
            }
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

        private static string Hm(SiteInfo site, DateTime d)
        {
            return d.ToString(site.Settings.Clock24 ? "HH:mm" : "h:mm tt", Ui.Inv);
        }

        private static string HmText(SiteInfo site, string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? string.Empty : Hm(site, DateTime.Today.Add(t.Value));
        }

        private static string Hm24(TimeSpan t)
        {
            return t.ToString("hh\\:mm", Ui.Inv);
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

        private static List<RunType> Runs(SiteInfo site)
        {
            return site.Runs.Where(r => r.Active && r.Online && r.Count > 0).OrderBy(r => r.Order).ToList();
        }

        private static List<string> SpeciesOn(SiteInfo site)
        {
            List<RunType> runs = Runs(site);
            return site.SpeciesList.OrderBy(x => x.Order).Select(x => x.Key).Where(k => runs.Any(r => r.SpeciesList.Count == 0 || r.SpeciesList.Contains(k))).ToList();
        }

        private static List<string> InTimes(SiteInfo site)
        {
            TimeSpan from = Ui.ParseHm(site.Settings.CheckInFrom) ?? TimeSpan.FromHours(7.5);
            TimeSpan to = Ui.ParseHm(site.Settings.CheckInTo) ?? TimeSpan.FromHours(18);
            List<string> list = new List<string>();
            for (TimeSpan t = from; t <= to; t += TimeSpan.FromMinutes(30))
            {
                list.Add(Hm24(t));
            }
            return list;
        }

        private static TimeSpan OutBy(SiteInfo site)
        {
            return Ui.ParseHm(site.Settings.CheckOutBy) ?? TimeSpan.FromHours(11);
        }

        private static string ClosedWhy(Eng e, DateTime d)
        {
            Holiday? h = e.HolidayOn(d);
            return Day(d, false) + (h != null ? " (" + h.Name + ")" : string.Empty);
        }

        private static string StayCheck(Eng e, DateTime? from, string inTime, DateTime? to)
        {
            SiteInfo site = e.Site;
            if (from == null)
            {
                return "Choose a check-in date.";
            }
            if (to == null)
            {
                return "Choose a check-out date.";
            }
            TimeSpan? t = Ui.ParseHm(inTime);
            if (t == null || !InTimes(site).Contains(inTime))
            {
                return "Choose a check-in time.";
            }
            if (to.Value <= from.Value)
            {
                return "Check-out must be at least one day after check-in.";
            }
            int max = Math.Clamp(site.Settings.StayMaxNights, 1, 365);
            if ((to.Value.Date - from.Value.Date).Days > max)
            {
                return "Stays can be up to " + max + " nights online. For a longer stay, please call us.";
            }
            if (from.Value.Add(t.Value) < e.Earliest)
            {
                return "Check-in must be at least " + Math.Max(0, site.Settings.MinNoticeHours) + " hours from now.";
            }
            if (from.Value > e.Last)
            {
                return "We take online bookings up to " + Math.Clamp(site.Settings.BookAheadDays, 1, 365) + " days ahead.";
            }
            (TimeSpan From, TimeSpan To)? c1 = e.Clinic(from.Value);
            if (c1 == null)
            {
                return "We are closed on " + ClosedWhy(e, from.Value) + ", so check-in can't be that day.";
            }
            if (t.Value < c1.Value.From || t.Value >= c1.Value.To)
            {
                return "On " + from.Value.ToString("dddd", Ui.Inv) + " we are open " + Hm(site, DateTime.Today.Add(c1.Value.From)) + " to " + Hm(site, DateTime.Today.Add(c1.Value.To)) + ". Please pick a check-in time in those hours.";
            }
            if (e.Clinic(to.Value) == null)
            {
                return "We are closed on " + ClosedWhy(e, to.Value) + ", so check-out can't be that day.";
            }
            return string.Empty;
        }

        private static DateTime NextOpen(Eng e, DateTime d)
        {
            for (int i = 0; i < 14; i++)
            {
                if (e.Clinic(d.AddDays(i)) != null)
                {
                    return d.AddDays(i);
                }
            }
            return d;
        }

        private static bool Fits(RunType r, string sp, string size)
        {
            if (r.SpeciesList.Count > 0 && !r.SpeciesList.Contains(sp))
            {
                return false;
            }
            return size == string.Empty || r.Sizes.Count == 0 || r.Sizes.Contains(size);
        }

        private static List<AddOn> AddsFor(SiteInfo site, string sp)
        {
            return site.AddOns.Where(a => a.Active && Slots.ForSpecies(a.Species, sp)).OrderBy(a => a.Order).ToList();
        }

        private static decimal Sized(Eng e, string itemId, decimal fallback, string size)
        {
            if (!e.Prices.TryGetValue(itemId ?? string.Empty, out BsPrice? i))
            {
                return fallback;
            }
            if (size == "medium" && i.PriceM > 0)
            {
                return i.PriceM;
            }
            if (size == "large" && i.PriceL > 0)
            {
                return i.PriceL;
            }
            return i.Price;
        }

        private static decimal Night(Eng e, RunType r, string size)
        {
            return Sized(e, r.NightItemId, r.Price, size);
        }

        private static decimal AddPrice(Eng e, AddOn a, string size)
        {
            return Sized(e, a.Key, a.Price, size);
        }

        private static decimal PriceOf(Eng e, RunType r, int nights, List<string> adds, string sp, string size)
        {
            decimal total = Night(e, r, size) * nights;
            foreach (AddOn a in AddsFor(e.Site, sp).Where(x => adds.Contains(x.Key)))
            {
                total += a.PerNight ? AddPrice(e, a, size) * nights : AddPrice(e, a, size);
            }
            return total;
        }

        private static async Task<View> Render(SiteInfo site, string run)
        {
            Eng e = await Eng.Make(site);
            DateTime from = NextOpen(e, e.Today.AddDays(1));
            DateTime to = NextOpen(e, from.AddDays(2));
            string inT = InTimes(site).Contains("09:00") ? "09:00" : InTimes(site).FirstOrDefault() ?? "09:00";
            St st = await Norm(e, Ui.Ymd(from), inT, Ui.Ymd(to), string.Empty, string.Empty, string.Empty, run, string.Empty, string.Empty);
            return new View { Title = "Book a stay", Desc = "Book boarding for your dog or cat at " + site.Settings.Name + ": dog runs by size, quiet cat condos and daily extras.", Main = "<section class=\"bs-phead\"><div class=\"bs-wrap\"><span class=\"bs-kick\">Boarding</span><h1>Book a stay</h1><p class=\"bs-lead\">Pick your dates, tell us about your pet and choose a room. We check vaccines and space for every night of the stay. <a class=\"bs-lk\" href=\"Boarding\">How boarding works</a>.</p></div></section><div class=\"bs-wrap bs-bgrid\"><div id=\"bs-flow\" class=\"bs-flow\">" + await Flow(e, st) + "</div><aside class=\"bs-box bs-sum\" id=\"bs-sum\" aria-label=\"Your stay\">" + Summary(e, st) + "</aside></div>" };
        }

        private static async Task<St> Norm(Eng e, string from, string inT, string to, string dd, string sp, string size, string run, string adds, string xd)
        {
            SiteInfo site = e.Site;
            St st = new St { From = Ui.ParseDay(from), To = Ui.ParseDay(to), In = InTimes(site).Contains(inT) ? inT : string.Empty };
            if (dd == "1")
            {
                st.Err = StayCheck(e, st.From, st.In, st.To);
                st.DatesOk = st.Err == string.Empty;
            }
            List<string> species = SpeciesOn(site);
            RunType? want = Runs(site).FirstOrDefault(r => r.Key == run) ?? (run != string.Empty ? Runs(site).FirstOrDefault(r => r.Key == "rt-" + run || (run.StartsWith("run-", StringComparison.Ordinal) && run.Length > 4 && r.Key == "rt-run-" + run.Substring(4, 1)) || (run.StartsWith("cat-", StringComparison.Ordinal) && r.Key == "rt-" + run.Substring(4))) : null);
            st.Want = want?.Key ?? string.Empty;
            if (species.Contains(sp))
            {
                st.Sp = sp;
            }
            else if (want != null && species.Contains(want.Species) && sp == string.Empty)
            {
                st.Sp = want.Species;
            }
            if (site.HasSize(st.Sp))
            {
                if (size is "small" or "medium" or "large")
                {
                    st.Size = size;
                }
                else if (want != null && want.Species == st.Sp && want.Sizes.Count == 1 && size == string.Empty)
                {
                    st.Size = want.Sizes[0];
                }
            }
            bool petDone = st.Sp != string.Empty && (!site.HasSize(st.Sp) || st.Size != string.Empty);
            if (st.DatesOk && petDone && want != null && Fits(want, st.Sp, st.Size) && (await e.FreeRooms(want, st.From!.Value, st.To!.Value)).Count >= 1)
            {
                st.Run = want;
            }
            if (st.Run != null)
            {
                List<string> ok = AddsFor(site, st.Sp).Select(a => a.Key).ToList();
                st.Adds = adds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(ok.Contains).Distinct().ToList();
                st.AddsDone = xd == "1";
            }
            return st;
        }

        private static string Done(string label, string body, string change)
        {
            return "<section class=\"bs-stp bs-done\"><span class=\"bs-n\" aria-hidden=\"true\"></span><div class=\"bs-stb\"><small>" + Ui.Enc(label) + "</small><div class=\"bs-dv\">" + body + "</div></div>" + (change != string.Empty ? "<button type=\"button\" class=\"bs-chg\" onclick=\"BookStayJs.go('" + change + "')\">Change<span class=\"bs-sr\"> " + Ui.Enc(label.ToLowerInvariant()) + "</span></button>" : string.Empty) + "</section>";
        }

        private static string Open(int n, string title, string body)
        {
            return "<section class=\"bs-stp bs-open\" id=\"bs-cur\"><span class=\"bs-n\">" + n + "</span><div class=\"bs-stb\"><h2>" + Ui.Enc(title) + "</h2>" + body + "</div></section>";
        }

        private static string Locked(int n, string title)
        {
            return "<section class=\"bs-stp bs-lock\"><span class=\"bs-n\">" + n + "</span><div class=\"bs-stb\"><h2>" + Ui.Enc(title) + "</h2></div></section>";
        }

        private static string DatesText(SiteInfo site, St st)
        {
            return Day(st.From!.Value, false) + " at " + HmText(site, st.In) + " &rarr; " + Day(st.To!.Value, false) + " by " + HmText(site, site.Settings.CheckOutBy);
        }

        private static async Task<string> Flow(Eng e, St st)
        {
            SiteInfo site = e.Site;
            StringBuilder sb = new StringBuilder("<i id=\"bs-st\" hidden data-from=\"" + (st.From != null ? Ui.Ymd(st.From.Value) : string.Empty) + "\" data-in=\"" + Ui.Attr(st.In) + "\" data-to=\"" + (st.To != null ? Ui.Ymd(st.To.Value) : string.Empty) + "\" data-dd=\"" + (st.DatesOk ? "1" : string.Empty) + "\" data-sp=\"" + Ui.Attr(st.Sp) + "\" data-size=\"" + Ui.Attr(st.Size) + "\" data-run=\"" + Ui.Attr(st.Run?.Key ?? st.Want) + "\" data-adds=\"" + Ui.Attr(string.Join(",", st.Adds)) + "\" data-xd=\"" + (st.AddsDone ? "1" : string.Empty) + "\"></i>");
            if (st.DatesOk)
            {
                sb.Append(Done("Dates", "<b>" + DatesText(site, st) + "</b><span>" + Ui.Plural(st.Nights, "night", "nights") + "</span>", "dates"));
            }
            else
            {
                sb.Append(Open(1, "When is the stay?", DatesForm(e, st)));
            }
            bool petDone = st.Sp != string.Empty && (!site.HasSize(st.Sp) || st.Size != string.Empty);
            if (!st.DatesOk)
            {
                sb.Append(Locked(2, "Your pet"));
            }
            else if (petDone)
            {
                sb.Append(Done("Your pet", "<b>" + Ui.Enc(site.SpeciesName(st.Sp)) + "</b>" + (st.Size != string.Empty ? "<span>" + Ui.Enc(SizeLong(st.Size)) + "</span>" : string.Empty), "pet"));
            }
            else
            {
                sb.Append(Open(2, "Who is staying?", PetPick(site, st)));
            }
            if (!st.DatesOk || !petDone)
            {
                sb.Append(Locked(3, "Room"));
            }
            else if (st.Run != null)
            {
                sb.Append(Done("Room", "<b>" + Ui.Enc(st.Run.Name) + "</b>" + (site.Settings.ShowPrices ? "<span>" + Cash(Night(e, st.Run, st.Size)) + " a night</span>" : string.Empty), "run"));
            }
            else
            {
                sb.Append(Open(3, "Choose a room", await RunPick(e, st)));
            }
            if (st.Run == null)
            {
                sb.Append(Locked(4, "Extras"));
            }
            else if (st.AddsDone)
            {
                List<AddOn> chosen = AddsFor(site, st.Sp).Where(a => st.Adds.Contains(a.Key)).ToList();
                sb.Append(Done("Extras", chosen.Count > 0 ? "<b>" + Ui.Enc(string.Join(", ", chosen.Select(a => a.Name))) + "</b>" : "<b>No extras</b>", "adds"));
            }
            else
            {
                sb.Append(Open(4, "Any extras?", AddsPick(e, st)));
            }
            if (!st.AddsDone || st.Run == null)
            {
                sb.Append(Locked(5, "Your details"));
            }
            else
            {
                sb.Append(Open(5, "Your details", Form(site, st)));
            }
            return sb.ToString();
        }

        private static string DatesForm(Eng e, St st)
        {
            SiteInfo site = e.Site;
            List<(string, string)> opts = InTimes(site).Select(x => (x, HmText(site, x))).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"bs-dates\"><label class=\"bs-field\"><span>Check-in date</span><input type=\"date\" id=\"bs-from\" min=\"" + Ui.Ymd(e.Today) + "\" max=\"" + Ui.Ymd(e.Last) + "\" value=\"" + (st.From != null ? Ui.Ymd(st.From.Value) : string.Empty) + "\"></label><label class=\"bs-field\"><span>Check-in time</span><select id=\"bs-in\">" + Ui.Opts(opts, st.In) + "</select></label><label class=\"bs-field\"><span>Check-out date</span><input type=\"date\" id=\"bs-to\" min=\"" + Ui.Ymd(e.Today.AddDays(1)) + "\" value=\"" + (st.To != null ? Ui.Ymd(st.To.Value) : string.Empty) + "\"></label></div>");
            sb.Append("<p class=\"bs-hint\">Check-in between " + HmText(site, site.Settings.CheckInFrom) + " and " + HmText(site, site.Settings.CheckInTo) + " on days we are open. Check-out is by " + HmText(site, site.Settings.CheckOutBy) + ". Up to " + Math.Clamp(site.Settings.StayMaxNights, 1, 365) + " nights online.</p>");
            sb.Append("<em class=\"bs-err\" id=\"bs-e-dates\" role=\"alert\">" + Ui.Enc(st.Err) + "</em><button type=\"button\" class=\"bs-btn bs-btn-accent\" onclick=\"BookStayJs.dates()\">Continue</button>");
            return sb.ToString();
        }

        private static string PetPick(SiteInfo site, St st)
        {
            StringBuilder sb = new StringBuilder("<div class=\"bs-spc\" role=\"group\" aria-label=\"Kind of pet\">");
            foreach (string k in SpeciesOn(site))
            {
                sb.Append("<button type=\"button\" class=\"bs-sp" + (k == st.Sp ? " bs-act" : string.Empty) + "\" aria-pressed=\"" + (k == st.Sp ? "true" : "false") + "\" onclick=\"BookStayJs.pickSpecies('" + Ui.Js(k) + "')\"><svg viewBox=\"0 0 24 24\" width=\"22\" height=\"22\" fill=\"currentColor\" aria-hidden=\"true\"><ellipse cx=\"6\" cy=\"10\" rx=\"1.9\" ry=\"2.4\"/><ellipse cx=\"9.5\" cy=\"6\" rx=\"1.9\" ry=\"2.5\"/><ellipse cx=\"14.5\" cy=\"6\" rx=\"1.9\" ry=\"2.5\"/><ellipse cx=\"18\" cy=\"10\" rx=\"1.9\" ry=\"2.4\"/><path d=\"M12 11c-3 0-5.4 3.4-5.4 5.7 0 1.6 1.2 2.4 2.7 2.4 1.1 0 1.7-.5 2.7-.5s1.6.5 2.7.5c1.5 0 2.7-.8 2.7-2.4 0-2.3-2.4-5.7-5.4-5.7z\"/></svg><span>" + Ui.Enc(site.SpeciesName(k)) + "</span></button>");
            }
            sb.Append("</div>");
            if (site.HasSize(st.Sp))
            {
                sb.Append("<p class=\"bs-sect\">How big is your " + Ui.Enc(site.SpeciesName(st.Sp).ToLowerInvariant()) + "?</p><div class=\"bs-sizes\">");
                foreach ((string k, string n) in Sizes)
                {
                    int i = n.IndexOf(" (", StringComparison.Ordinal);
                    sb.Append("<button type=\"button\" class=\"bs-sz\" onclick=\"BookStayJs.pickSize('" + Ui.Js(k) + "')\"><b>" + Ui.Enc(i > 0 ? n.Substring(0, i) : n) + "</b><small>" + Ui.Enc(i > 0 ? n.Substring(i + 2).TrimEnd(')') : string.Empty) + "</small></button>");
                }
                sb.Append("</div><p class=\"bs-hint\">Dog runs are sized by weight. We check the weight at check-in.</p>");
            }
            sb.Append("<p class=\"bs-hint\">We board " + Ui.Enc(string.Join(" and ", SpeciesOn(site).Select(k => site.SpeciesPlural(k).ToLowerInvariant()))) + ". For other pets, please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            return sb.ToString();
        }

        private static async Task<string> RunPick(Eng e, St st)
        {
            SiteInfo site = e.Site;
            List<RunType> list = Runs(site).Where(r => Fits(r, st.Sp, st.Size)).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"bs-runs\">");
            bool anyFree = false;
            foreach (RunType r in list)
            {
                int free = (await e.FreeRooms(r, st.From!.Value, st.To!.Value)).Count;
                anyFree = anyFree || free > 0;
                DateTime? full = free < 1 ? await e.FullOn(r, st.From.Value, st.To.Value) : null;
                string img = SafeImage(r.Image) ? "<img class=\"bs-rimg\" src=\"" + Ui.Attr(Thumb(r.Image)) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\">" : "<span class=\"bs-rimg\"></span>";
                string price = site.Settings.ShowPrices ? "<span class=\"bs-rprice\"><b>" + Cash(Night(e, r, st.Size) * st.Nights) + "</b><small>" + Cash(Night(e, r, st.Size)) + " &times; " + Ui.Plural(st.Nights, "night", "nights") + "</small></span>" : string.Empty;
                string left = free < 1 ? "<em class=\"bs-full\">Full on " + (full != null ? Day(full.Value, false) : "these dates") + "</em>" : "<em>" + (free <= 2 ? "Only " + free + " left" : free + " free") + " for these nights</em>";
                sb.Append("<button type=\"button\" class=\"bs-run\" onclick=\"BookStayJs.pickRun('" + Ui.Js(r.Key) + "')\"" + (free < 1 ? " disabled" : string.Empty) + ">" + img + "<span class=\"bs-rb\"><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(r.Short) + "</small>" + left + "</span>" + price + "</button>");
            }
            sb.Append("</div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bs-none\">We have no room for this pet online. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            else if (!anyFree)
            {
                sb.Append("<p class=\"bs-warn-box\">Sorry, we are full for these nights. <button type=\"button\" class=\"bs-chg\" onclick=\"BookStayJs.go('dates')\">Try other dates</button></p>");
            }
            return sb.ToString();
        }

        private static string AddsPick(Eng e, St st)
        {
            SiteInfo site = e.Site;
            List<AddOn> list = AddsFor(site, st.Sp);
            StringBuilder sb = new StringBuilder("<div class=\"bs-adds\">");
            foreach (AddOn a in list)
            {
                bool on = st.Adds.Contains(a.Key);
                string price = !site.Settings.ShowPrices ? string.Empty : AddPrice(e, a, st.Size) <= 0 ? "Free" : a.PerNight ? Cash(AddPrice(e, a, st.Size)) + " a night" : Cash(AddPrice(e, a, st.Size)) + " once";
                sb.Append("<label class=\"bs-add\"><input type=\"checkbox\" class=\"bs-addck\" value=\"" + Ui.Attr(a.Key) + "\"" + (on ? " checked" : string.Empty) + " onchange=\"BookStayJs.addon()\"><span class=\"bs-ab\"><b>" + Ui.Enc(a.Name) + "</b><small>" + Ui.Enc(a.Text) + "</small></span><em>" + price + "</em></label>");
            }
            sb.Append("</div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bs-none\">No extras for this pet.</p>");
            }
            sb.Append("<button type=\"button\" class=\"bs-btn bs-btn-accent\" onclick=\"BookStayJs.addsDone()\">Continue</button>");
            return sb.ToString();
        }

        private static string Form(SiteInfo site, St st)
        {
            bool sized = site.HasSize(st.Sp);
            VaxRule? rule = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == st.Sp);
            StringBuilder sb = new StringBuilder("<form class=\"bs-form bs-dform\" id=\"bs-dform\" data-ft=\"" + NewForm() + "\" onsubmit=\"return BookStayJs.confirm()\" novalidate>");
            sb.Append("<div class=\"bs-hp\" aria-hidden=\"true\"><label>Leave this field empty <input type=\"text\" id=\"bs-hp\" tabindex=\"-1\" autocomplete=\"off\"></label></div>");
            sb.Append("<div class=\"bs-seg\" role=\"radiogroup\" aria-label=\"Have you been here before?\"><label><input type=\"radio\" name=\"bs-new\" value=\"back\" checked><span>We have been here before</span></label><label><input type=\"radio\" name=\"bs-new\" value=\"new\"><span>We are new</span></label></div>");
            sb.Append("<fieldset class=\"bs-fset\"><h3>About you</h3>");
            sb.Append("<div class=\"bs-row2\"><label class=\"bs-field\"><span>First name</span><input type=\"text\" id=\"bs-first\" maxlength=\"40\" autocomplete=\"given-name\"><em class=\"bs-err\" id=\"bs-e-first\"></em></label><label class=\"bs-field\"><span>Last name</span><input type=\"text\" id=\"bs-last\" maxlength=\"40\" autocomplete=\"family-name\"><em class=\"bs-err\" id=\"bs-e-last\"></em></label></div>");
            sb.Append("<div class=\"bs-row2\"><label class=\"bs-field\"><span>Mobile phone</span><input type=\"tel\" id=\"bs-phone\" maxlength=\"30\" autocomplete=\"tel\"><em class=\"bs-err\" id=\"bs-e-phone\"></em></label><label class=\"bs-field\"><span>Email</span><input type=\"email\" id=\"bs-email\" maxlength=\"120\" autocomplete=\"email\"><em class=\"bs-err\" id=\"bs-e-email\"></em></label></div>");
            sb.Append("<p class=\"bs-hint\">Been here before? Use the email and phone number we have on file, so we can find your pet's records.</p>");
            sb.Append("<label class=\"bs-field\"><span>How should we contact you?</span><select id=\"bs-contact\">" + Ui.Opts(new[] { ("phone", "Phone call"), ("email", "Email"), ("text", "Text message") }, "text") + "</select></label></fieldset>");
            sb.Append("<fieldset class=\"bs-fset\"><h3>About your " + Ui.Enc(site.SpeciesName(st.Sp).ToLowerInvariant()) + "</h3>");
            sb.Append("<div class=\"bs-row2\"><label class=\"bs-field\"><span>Pet's name</span><input type=\"text\" id=\"bs-pname\" maxlength=\"40\" autocomplete=\"off\"><em class=\"bs-err\" id=\"bs-e-pname\"></em></label><label class=\"bs-field\"><span>Breed <i>(optional)</i></span><input type=\"text\" id=\"bs-breed\" maxlength=\"60\" autocomplete=\"off\"></label></div>");
            sb.Append("<div class=\"bs-field\"><span>Sex</span><div class=\"bs-yn\"><label><input type=\"radio\" name=\"bs-sex\" value=\"m\"><span>Male</span></label><label><input type=\"radio\" name=\"bs-sex\" value=\"f\"><span>Female</span></label></div><label class=\"bs-agree\"><input type=\"checkbox\" id=\"bs-fixed\"><span>Spayed or neutered</span></label><em class=\"bs-err\" id=\"bs-e-sex\"></em></div>");
            sb.Append("<div class=\"bs-row3\"><label class=\"bs-field\"><span>Age in years <i>(if known)</i></span><input type=\"number\" id=\"bs-agey\" min=\"0\" max=\"40\" step=\"1\" inputmode=\"numeric\"><em class=\"bs-err\" id=\"bs-e-agey\"></em></label><label class=\"bs-field\"><span>And months</span><input type=\"number\" id=\"bs-agem\" min=\"0\" max=\"11\" step=\"1\" inputmode=\"numeric\"><em class=\"bs-err\" id=\"bs-e-agem\"></em></label><label class=\"bs-field\"><span>Weight in lb" + (sized ? string.Empty : " <i>(optional)</i>") + "</span><input type=\"text\" id=\"bs-weight\" maxlength=\"6\" inputmode=\"decimal\"><em class=\"bs-err\" id=\"bs-e-weight\"></em></label></div>");
            sb.Append("<label class=\"bs-field\"><span>Feeding <i>(optional)</i></span><textarea id=\"bs-feeding\" rows=\"2\" maxlength=\"300\" placeholder=\"For example: own food, one cup morning and evening.\"></textarea></label>");
            sb.Append("</fieldset>");
            if (rule != null && rule.Required.Count > 0)
            {
                sb.Append("<fieldset class=\"bs-fset bs-vax\"><h3>Vaccines</h3><p>These must be valid until check-out on " + Day(st.To!.Value, false) + ":</p><ul class=\"bs-vlist\">");
                foreach (string v in rule.Required)
                {
                    sb.Append("<li>" + Ui.Enc(v) + "</li>");
                }
                sb.Append("</ul><p class=\"bs-hint\">If your pet's records are already with us, we check them for you when you book.</p><label class=\"bs-agree\"><input type=\"checkbox\" id=\"bs-vaxok\"><span>I will bring proof of these vaccines at check-in if they are not already on file with you.</span></label><em class=\"bs-err\" id=\"bs-e-vaxok\"></em></fieldset>");
            }
            sb.Append("<label class=\"bs-field\"><span>Anything else we should know? <i>(optional)</i></span><textarea id=\"bs-note\" rows=\"3\" maxlength=\"280\" placeholder=\"For example: afraid of thunder, or loves a soft toy at night.\"></textarea></label>");
            sb.Append("<label class=\"bs-agree\"><input type=\"checkbox\" id=\"bs-agree\"><span>I will cancel at least " + site.Settings.StayCancelHours + " hours before check-in if our plans change.</span></label><em class=\"bs-err\" id=\"bs-e-agree\"></em><div class=\"bs-err bs-formerr\" id=\"bs-e-form\" role=\"alert\"></div>");
            sb.Append("<button type=\"submit\" class=\"bs-btn bs-btn-accent bs-cbtn\" id=\"bs-cbtn\">Confirm stay</button></form>");
            return sb.ToString();
        }

        private static string Summary(Eng e, St st)
        {
            SiteInfo site = e.Site;
            StringBuilder sb = new StringBuilder("<h2>Your stay</h2>");
            if (st.Run != null && SafeImage(st.Run.Image))
            {
                sb.Append("<div class=\"bs-sumtop\"><img class=\"bs-sumimg\" src=\"" + Ui.Attr(Thumb(st.Run.Image)) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\"><div><b>" + Ui.Enc(st.Run.Name) + "</b><small>" + Ui.Enc(st.Run.Short) + "</small></div></div>");
            }
            sb.Append("<dl class=\"bs-sumdl\">");
            sb.Append("<div><dt>Check in</dt><dd>" + (st.DatesOk ? Day(st.From!.Value, false) + "<br><small>at " + HmText(site, st.In) + "</small>" : "&mdash;") + "</dd></div>");
            sb.Append("<div><dt>Check out</dt><dd>" + (st.DatesOk ? Day(st.To!.Value, false) + "<br><small>by " + HmText(site, site.Settings.CheckOutBy) + "</small>" : "&mdash;") + "</dd></div>");
            sb.Append("<div><dt>Pet</dt><dd>" + (st.Sp != string.Empty ? Ui.Enc(site.SpeciesName(st.Sp)) + (st.Size != string.Empty ? "<br><small>" + Ui.Enc(SizeLong(st.Size)) + "</small>" : string.Empty) : "&mdash;") + "</dd></div>");
            sb.Append("<div><dt>Room</dt><dd>" + (st.Run != null ? Ui.Enc(st.Run.Name) : "&mdash;") + "</dd></div></dl>");
            if (st.Run != null && st.DatesOk && site.Settings.ShowPrices)
            {
                int n = st.Nights;
                sb.Append("<div class=\"bs-lines\"><p><span>" + Ui.Enc(st.Run.Name) + " &times; " + n + "</span><span>" + Cash(Night(e, st.Run, st.Size) * n) + "</span></p>");
                foreach (AddOn a in AddsFor(site, st.Sp).Where(x => st.Adds.Contains(x.Key)))
                {
                    decimal v = a.PerNight ? AddPrice(e, a, st.Size) * n : AddPrice(e, a, st.Size);
                    sb.Append("<p><span>" + Ui.Enc(a.Name) + (a.PerNight ? " &times; " + n : string.Empty) + "</span><span>" + (v > 0 ? Cash(v) : "Free") + "</span></p>");
                }
                sb.Append("</div><div class=\"bs-total\"><span>Total</span><b id=\"bs-total\">" + Cash(PriceOf(e, st.Run, n, st.Adds, st.Sp, st.Size)) + "</b></div>");
            }
            sb.Append("<p class=\"bs-sumnote\">Free to change or cancel online up to " + site.Settings.StayCancelHours + " hours before check-in.</p>");
            return sb.ToString();
        }

        private string V(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private async Task<St> FromKeys(Eng e)
        {
            return await Norm(e, V("kfrom", 12), V("kin", 5), V("kto", 12), V("kdd", 1), V("ksp", 20), V("ksz", 10), V("krun", 50), V("kadds", 600), V("kxd", 1));
        }

        public async Task<ApiResponse> Step()
        {
            ApiResponse response = new ApiResponse();
            if (!Take("step:" + App.Ip(), 60, TimeSpan.FromMinutes(1)))
            {
                response.ExecuteScript("BookStayJs.toast('Please slow down a moment, then try again.');");
                return response;
            }
            Eng e = await Eng.Make(await Site.Load());
            St st = await FromKeys(e);
            response.SetElementContents("bs-flow", await Flow(e, st));
            response.SetElementContents("bs-sum", Summary(e, st));
            response.ExecuteScript("BookStayJs.after();");
            return response;
        }

        public async Task<ApiResponse> Total()
        {
            ApiResponse response = new ApiResponse();
            if (!Take("step:" + App.Ip(), 60, TimeSpan.FromMinutes(1)))
            {
                return response;
            }
            Eng e = await Eng.Make(await Site.Load());
            St st = await FromKeys(e);
            response.SetElementContents("bs-sum", Summary(e, st));
            return response;
        }

        private static string SizeFromLb(decimal lb)
        {
            return lb < 20m ? "small" : lb <= 50m ? "medium" : "large";
        }

        private static string FormErr(string html)
        {
            return "<div class=\"bs-warn-box\">" + html + "</div>";
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

        private static string Call(SiteInfo site)
        {
            return FormErr("We could not take this booking online. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.");
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
                return FormErr("We have had a lot of online bookings today. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.");
            }
            if (Convert.ToInt32(dt.Rows[0]["MailN"]) >= 3 || Convert.ToInt32(dt.Rows[0]["PhoneN"]) >= 3)
            {
                return FormErr("You have already made several bookings online today. Please call us on " + Ui.Enc(site.Settings.Phone) + " for more.");
            }
            return string.Empty;
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
                StringBuilder sb = new StringBuilder("CAH-S");
                for (int k = 0; k < 7; k++)
                {
                    sb.Append(CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]);
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

        private static async Task<List<string>> Missing(SiteInfo site, string sp, string petId, DateTime outDay)
        {
            VaxRule? rule = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == sp);
            if (rule == null || rule.Required.Count == 0)
            {
                return new List<string>();
            }
            List<BsVax> rows = new List<BsVax>();
            if (petId != string.Empty)
            {
                var (list, emsg) = await Helper.GetListT<BsVax>("SELECT VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE PET_ID = @pid", new List<SqlParameter> { App.P("pid", petId, 50) });
                rows = emsg == string.Empty && list != null ? list : rows;
            }
            List<string> miss = new List<string>();
            foreach (string name in rule.Required)
            {
                BsVax? last = rows.Where(r => r.VaxName.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.GivenDate).ThenByDescending(r => r.DueDate).FirstOrDefault();
                if (last == null || last.DueDate.Date < outDay.Date)
                {
                    miss.Add(name);
                }
            }
            return miss;
        }

        private static async Task Gone(ApiResponse response, Eng e, St st, string msg)
        {
            Eng fresh = await Eng.Make(e.Site);
            St again = await Norm(fresh, st.From.HasValue ? Ui.Ymd(st.From.Value) : string.Empty, st.In, st.To.HasValue ? Ui.Ymd(st.To.Value) : string.Empty, "1", st.Sp, st.Size, st.Run?.Key ?? string.Empty, string.Join(",", st.Adds), "1");
            response.SetElementContents("bs-flow", await Flow(fresh, again));
            response.SetElementContents("bs-sum", Summary(fresh, again));
            response.ExecuteScript("BookStayJs.after();BookStayJs.toast('" + Ui.Js(msg) + "');");
        }

        public async Task<ApiResponse> Confirm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("BookStayJs.unlock();");
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
            string feeding = Ui.OneLine(GetDataValue("feeding"), 300);
            string note = Ui.Clip(GetDataValue("note"), 280);
            bool vaxOk = V("vaxok", 2) == "1";
            bool isNew = V("newp", 10) == "new";
            string ftok = V("ftok", 40);
            SiteInfo site = await Site.Load();
            Eng e = await Eng.Make(site);
            string ip = App.Ip();
            if (V("hp", 200) != string.Empty)
            {
                await App.Log("web", "book stay online", "blocked form from " + ip, false);
                response.SetElementContents("bs-e-form", Call(site));
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
                    response.ExecuteScript("BookStayJs.form('" + NewForm() + "');");
                }
                response.SetElementContents("bs-e-form", FormErr("Please check your details, then press Confirm stay again."));
                return response;
            }
            St ls = await FromKeys(e);
            if (!ls.DatesOk || ls.Run == null || !ls.AddsDone)
            {
                response.SetElementContents("bs-flow", await Flow(e, ls));
                response.SetElementContents("bs-sum", Summary(e, ls));
                response.ExecuteScript("BookStayJs.after();BookStayJs.toast('Sorry, that room is no longer free for these dates.');");
                return response;
            }
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
            if (sex == string.Empty)
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
            bool sized = site.HasSize(ls.Sp);
            decimal? lb = null;
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
            if (V("agree", 2) != "1")
            {
                errs["agree"] = "Please tick this box to continue.";
            }
            VaxRule? need = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == ls.Sp);
            if (need != null && need.Required.Count > 0 && !vaxOk)
            {
                errs["vaxok"] = "Please tick this box. We check your pet's vaccine records with you at check-in.";
            }
            if (errs.Count > 0)
            {
                foreach (KeyValuePair<string, string> kv in errs)
                {
                    response.SetElementContents("bs-e-" + kv.Key, Ui.Enc(kv.Value));
                }
                response.ExecuteScript("BookStayJs.invalid();");
                return response;
            }
            if (sized && lb != null && SizeFromLb(lb.Value) != ls.Size)
            {
                string right = SizeFromLb(lb.Value);
                RunType? fit = Runs(site).FirstOrDefault(r => Fits(r, ls.Sp, right));
                St fix = await Norm(e, Ui.Ymd(ls.From!.Value), ls.In, Ui.Ymd(ls.To!.Value), "1", ls.Sp, right, Fits(ls.Run, ls.Sp, right) ? ls.Run.Key : string.Empty, string.Join(",", ls.Adds), "1");
                response.SetElementContents("bs-flow", await Flow(e, fix));
                response.SetElementContents("bs-sum", Summary(e, fix));
                response.ExecuteScript("BookStayJs.after();");
                string msg = pname + " weighs " + lb.Value.ToString("0.#", Ui.Inv) + " lb, so " + (sex == "f" ? "she" : "he") + " needs a " + SizeName(right).ToLowerInvariant() + " " + site.SpeciesName(ls.Sp).ToLowerInvariant() + " room" + (fit != null ? " (" + fit.Name + ")" : string.Empty) + ". Please choose the room again; your details are kept.";
                response.ExecuteScript("BookStayJs.notice('" + Ui.Js(msg) + "');");
                return response;
            }
            RunType run = ls.Run;
            DateTime from = ls.From!.Value;
            DateTime to = ls.To!.Value;
            DateTime start = from.Add(Ui.ParseHm(ls.In) ?? TimeSpan.FromHours(9));
            DateTime end = to.Add(OutBy(site));
            int nights = ls.Nights;
            string digits = Ui.Digits(phone);
            if (!Take("try:" + ip, 10, TimeSpan.FromHours(1)) || !Take("tryemail:" + email, 5, TimeSpan.FromHours(1)))
            {
                await App.Log("web", "book stay online", "too many tries from " + ip, false);
                response.SetElementContents("bs-e-form", FormErr("We have had a lot of booking attempts from you in the last hour. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help."));
                return response;
            }
            string capErr = await Caps(site, email, digits);
            if (capErr != string.Empty)
            {
                await App.Log("web", "book stay online", "daily cap reached", false);
                response.SetElementContents("bs-e-form", capErr);
                return response;
            }
            var (cands, cerr) = await Helper.GetListT<BsClient>("SELECT CLIENT_ID AS ClientId, PHONE AS Phone, PHONE2 AS Phone2 FROM CLIENT WHERE ACTIVE = 1 AND LOWER(EMAIL) = @email ORDER BY CREATED_UTC", new List<SqlParameter> { App.P("email", email, 120) });
            if (cerr != string.Empty || cands == null)
            {
                response.SetElementContents("bs-e-form", FormErr("We could not take this booking online. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help."));
                return response;
            }
            BsClient? found = cands.FirstOrDefault(c => SamePhone(c.Phone, digits) || SamePhone(c.Phone2, digits));
            bool newClient = found == null;
            string cid = found?.ClientId ?? App.NewId();
            BsPet? pet = null;
            if (found != null)
            {
                var (prow, perr) = await Helper.GetOneT<BsPet>("SELECT TOP 1 PET_ID AS PetId, PET_NAME AS PetName, BREED AS Breed FROM PET WHERE CLIENT_ID = @cid AND ACTIVE = 1 AND DECEASED_DATE IS NULL AND SPECIES_KEY = @sp AND LOWER(PET_NAME) = @pn ORDER BY CREATED_UTC", new List<SqlParameter> { App.P("cid", cid, 50), App.P("sp", ls.Sp, 20), App.P("pn", pname.ToLowerInvariant(), 60) });
                if (perr != string.Empty)
                {
                    response.SetElementContents("bs-e-form", FormErr("We could not take this booking online. Please call us on " + Ui.Enc(site.Settings.Phone) + " and we will help."));
                    return response;
                }
                pet = prow;
            }
            bool newPet = pet == null;
            string pid = pet?.PetId ?? App.NewId();
            string petName = pet?.PetName ?? pname;
            List<string> missing = await Missing(site, ls.Sp, newPet ? string.Empty : pid, to);
            bool vx = missing.Count > 0;
            List<BsRoom> free = await e.FreeRooms(run, from, to);
            if (free.Count == 0)
            {
                await Gone(response, e, ls, "Sorry, that room was just taken for these dates.");
                return response;
            }
            decimal price = PriceOf(e, run, nights, ls.Adds, ls.Sp, ls.Size);
            string fullNote = Ui.Clip((feeding != string.Empty ? "Feeding: " + feeding + (note != string.Empty ? "\n" : string.Empty) : string.Empty) + note, 600);
            DateTime? birth = ageY != string.Empty || ageM != string.Empty ? e.Today.AddYears(-years).AddMonths(-months) : null;
            string size = sized ? (lb != null ? SizeFromLb(lb.Value) : ls.Size) : string.Empty;
            string aid = App.NewId();
            string code = string.Empty;
            string result = string.Empty;
            BsRoom? room = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                room = free.Count > 0 ? free[0] : null;
                code = await NewCode();
                if (room == null || code == string.Empty)
                {
                    break;
                }
                List<SqlParameter> ps = new List<SqlParameter>
                {
                    App.P("aid", aid, 50), App.P("code", code, 20), App.P("cid", cid, 50), App.P("pid", pid, 50), App.P("rt", run.Key, 50), App.P("room", room.RoomId, 50),
                    App.PDate("st", start), App.PDate("en", end), App.PDate("din", from.Date), App.PDate("dout", to.Date), App.PDate("today", e.Today),
                    App.P("ign", aid, 50), App.P("ivis", string.Empty, 50), App.PBit("inst", false), App.P("adds", string.Join(",", ls.Adds), 400), App.PDec("price", price), App.PBit("vx", vx),
                    App.P("note", fullNote, 600), App.PBit("nc", newClient || isNew), App.P("lid", App.NewId(), 50),
                    App.P("first", first, 60), App.P("last", last, 60), App.P("phone", phone, 30), App.P("email", email, 120), App.P("pref", contact, 10),
                    App.P("pname", pname, 60), App.P("sp", ls.Sp, 20), App.P("breed", breed, 60), App.P("sex", sex, 1), App.PBit("fixed", isFixed), new SqlParameter("birth", SqlDbType.Date) { Value = birth.HasValue ? birth.Value : DBNull.Value }, App.P("size", size, 10),
                    App.P("wid", App.NewId(), 50), App.PDec("lb", lb ?? 0m), App.PDate("now", e.Now)
                };
                List<string> sql = new List<string>();
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
                }
                sql.Add("UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room");
                sql.Add(RoomGuard);
                sql.Add(PetGuard);
                if (lb != null)
                {
                    sql.Add("INSERT INTO PETWEIGHT (WEIGHT_ID, PET_ID, WEIGHED_AT, LB, VISIT_ID, STAFF_ID) VALUES (@wid, @pid, @now, @lb, N'', N'web')");
                }
                sql.Add("INSERT INTO APPOINTMENT (APPT_ID, APPT_CODE, CLIENT_ID, PET_ID, BOOKTYPE_ID, VISIT_TYPE, STAFF_ID, ROOMTYPE_ID, ROOM_ID, START_AT, END_AT, MINUTES, PROC_MINUTES, ADDONS, EST_PRICE, SOURCE, STATUS, NEW_CLIENT, VAX_PENDING, NOTE, CREATED_UTC, CREATED_BY) VALUES (@aid, @code, @cid, @pid, N'', N'boarding', N'', @rt, @room, @st, @en, 0, 0, @adds, @price, N'web', N'booked', @nc, @vx, @note, SYSUTCDATETIME(), N'web')");
                sql.Add("INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), N'web', N'Booked online')");
                result = await Helper.PutData(sql, ps);
                if (!Helper.IsError(result))
                {
                    break;
                }
                if (result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    free.Remove(room);
                    continue;
                }
                if (!(result.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || result.Contains("unique", StringComparison.OrdinalIgnoreCase) || result.Contains("UX_", StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }
            }
            if (room == null || code == string.Empty || Helper.IsError(result))
            {
                await App.Log("web", "book stay online", petName + " · " + run.Name + " · failed", false);
                if (room == null || result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase) || result.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    await Gone(response, e, ls, result.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase) ? "Those dates aren't available online. Please pick other dates or call us." : "Sorry, that room was just taken for these dates.");
                    return response;
                }
                response.SetElementContents("bs-e-form", Call(site));
                return response;
            }
            await App.Log("web", "book stay online", petName + " · " + run.Name + " · " + code, true);
            string petLine = petName + " (" + (breed != string.Empty ? breed : pet != null && pet.Breed != string.Empty ? pet.Breed : site.SpeciesName(ls.Sp)) + ")";
            response.SetElementContents("bs-flow", Booked(site, run, ls, code, petName, petLine, first, price, vx, missing));
            response.SetElementContents("bs-sum", "<h2>What happens next</h2><ol class=\"bs-nextl\"><li>We hold this room for " + Ui.Enc(petName) + ".</li><li>The front desk may call or text to confirm.</li><li>Need to change? Use your code on <a href=\"MyAppointment?c=" + Uri.EscapeDataString(code) + "\">My booking</a>.</li></ol>");
            response.ExecuteScript("BookStayJs.booked();");
            return response;
        }

        private static string Booked(SiteInfo site, RunType run, St st, string code, string petName, string petLine, string first, decimal price, bool vx, List<string> missing)
        {
            List<AddOn> adds = AddsFor(site, st.Sp).Where(x => st.Adds.Contains(x.Key)).ToList();
            VaxRule? rule = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == st.Sp);
            StringBuilder sb = new StringBuilder("<section class=\"bs-booked\" id=\"bs-booked\" tabindex=\"-1\"" + IcsAttrs(site, st, code, petName, run) + "><div class=\"bs-tick\" aria-hidden=\"true\"></div><h2>" + Ui.Enc(petName) + "'s stay is booked, " + Ui.Enc(first) + "</h2><p>Keep your booking code. You need it with your email or phone to change or cancel.</p><div class=\"bs-code\">" + Ui.Enc(code) + "</div><dl class=\"bs-sumdl\">");
            sb.Append("<div><dt>Pet</dt><dd>" + Ui.Enc(petLine) + "</dd></div><div><dt>Room</dt><dd>" + Ui.Enc(run.Name) + "</dd></div>");
            sb.Append("<div><dt>Check in</dt><dd>" + LongDay(st.From!.Value) + " at " + HmText(site, st.In) + "</dd></div><div><dt>Check out</dt><dd>" + LongDay(st.To!.Value) + " by " + HmText(site, site.Settings.CheckOutBy) + "</dd></div><div><dt>Nights</dt><dd>" + st.Nights + "</dd></div>");
            sb.Append("<div><dt>Extras</dt><dd>" + (adds.Count > 0 ? Ui.Enc(string.Join(", ", adds.Select(x => x.Name))) : "None") + "</dd></div>");
            if (site.Settings.ShowPrices)
            {
                sb.Append("<div><dt>Price</dt><dd>" + Cash(price) + "</dd></div>");
            }
            sb.Append("</dl>");
            if (vx)
            {
                sb.Append("<p class=\"bs-warn-box\">Please bring proof of these vaccines at check-in: " + Ui.Enc(string.Join(", ", missing)) + ". They must be valid until " + Day(st.To.Value, false) + ".</p>");
            }
            else if (rule != null && rule.Required.Count > 0)
            {
                sb.Append("<p class=\"bs-ok-box\">Vaccines on file are valid for the whole stay.</p>");
            }
            sb.Append("<p class=\"bs-info-box\">Bring your pet's food in a labelled bag, any medicines, and a toy or blanket that smells of home.</p>");
            sb.Append("<div class=\"bs-bacts\"><button type=\"button\" class=\"bs-btn bs-btn-accent\" onclick=\"BookStayJs.ics('bs-booked')\">Add to calendar</button><a class=\"bs-btn bs-btn-ghost\" href=\"MyAppointment?c=" + Uri.EscapeDataString(code) + "\">Manage booking</a><a class=\"bs-btn bs-btn-ghost\" href=\"BookStay\">Book another stay</a></div>");
            if (App.Demo)
            {
                sb.Append("<p class=\"bs-hint bs-demo-note\">Demo data: this stay is in the shared demo, which resets every day. <a href=\"AdminLogin\">Sign in to the admin</a> to see it on the boarding board.</p>");
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

        private static string IcsAttrs(SiteInfo site, St st, string code, string petName, RunType run)
        {
            DateTime cin = st.From!.Value.Date.Add(Ui.ParseHm(st.In) ?? TimeSpan.FromHours(9));
            DateTime outBy = st.To!.Value.Date.Add(OutBy(site));
            List<Dictionary<string, string>> ev = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { ["s"] = Utc(site, cin), ["e"] = Utc(site, cin.AddMinutes(30)), ["t"] = "Check in " + petName + " for boarding | " + site.Settings.Name, ["d"] = "Booking code " + code + ". " + run.Name, ["u"] = code + "-in" },
                new Dictionary<string, string> { ["s"] = Utc(site, outBy.AddMinutes(-30)), ["e"] = Utc(site, outBy), ["t"] = "Pick up " + petName + " from boarding | " + site.Settings.Name, ["d"] = "Booking code " + code + ". Check-out by " + site.Settings.CheckOutBy + ".", ["u"] = code + "-out" }
            };
            return " data-code=\"" + Ui.Attr(code) + "\" data-loc=\"" + Ui.Attr(site.Settings.Name + ", " + site.Settings.Address) + "\" data-ev=\"" + Ui.Attr(JsonSerializer.Serialize(ev)) + "\"";
        }
    }
}
