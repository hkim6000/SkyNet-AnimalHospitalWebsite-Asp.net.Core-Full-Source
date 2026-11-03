using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminSchedule : WebPage
    {
        private const double Ppm = 1.6;
        private static readonly string[] States = { "booked", "confirmed", "checkedin", "noshow", "cancelled" };

        private sealed class SchedCol
        {
            public string StaffId { get; set; } = string.Empty;
            public DateTime Day { get; set; }
            public string Head { get; set; } = string.Empty;
            public string OffText { get; set; } = string.Empty;
            public string Who { get; set; } = string.Empty;
            public List<Window> Wins { get; set; } = new List<Window>();
            public List<SchedAppt> Appts { get; set; } = new List<SchedAppt>();
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Schedule | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            DateTime? qd = Ui.ParseDay(QueryValue("d"));
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--sc-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("sc"))
                .Replace("{plhd_qd}", qd != null ? Ui.Ymd(qd.Value) : string.Empty)
                .Replace("{plhd_qs}", Ui.Attr(CleanId(QueryValue("s"))));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_.-]{1,50}$") ? t : string.Empty;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminScheduleJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("sc-menu", Auth.Menu("sc", "AdminScheduleJs", "AdminSchedule", c));
            response.SetElementContents("sc-who", Auth.Who("sc", c));
            response.ExecuteScript("AdminScheduleJs.secs();");
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminScheduleJs.out();");
            return response;
        }

        private static string Denied()
        {
            return "<div class=\"sc-ptop\"><div><h1>Schedule</h1></div></div><div class=\"sc-panel sc-denied\"><h2>You don't have access to this page</h2><p>The schedule is not part of your areas yet. Ask a manager to add it for you in Staff and permissions.</p><a class=\"sc-btn sc-btn-ghost sc-btn-sm\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            bool quiet = (GetDataValue("quiet") ?? string.Empty).Trim() == "1";
            if (!quiet)
            {
                Shell(response, c);
            }
            if (!c.Can("schedule", 'V'))
            {
                response.SetElementContents("sc-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            DateTime day = Ui.ParseDay(GetDataValue("day")) ?? now.Date;
            if (day < now.Date.AddYears(-3) || day > now.Date.AddYears(3))
            {
                day = now.Date;
            }
            string staff = CleanId(GetDataValue("staff"));
            bool everyone = (GetDataValue("everyone") ?? string.Empty).Trim() == "1";
            bool showCx = (GetDataValue("cancelled") ?? string.Empty).Trim() == "1";
            List<StaffPick> pool = await Pool(c);
            if (c.U.OwnOnly && staff != string.Empty)
            {
                staff = c.U.StaffId;
            }
            if (staff != string.Empty && !pool.Any(p => p.StaffId == staff))
            {
                staff = string.Empty;
            }
            string def = c.U.OwnOnly || pool.Any(p => p.StaffId == c.U.StaffId) ? c.U.StaffId : string.Empty;
            string html;
            if (staff == string.Empty)
            {
                (string h, string first) = await DayView(c, s, now, day, pool, everyone, showCx);
                html = h;
                if (def == string.Empty)
                {
                    def = first != string.Empty ? first : pool.Count > 0 ? pool[0].StaffId : string.Empty;
                }
            }
            else
            {
                html = await WeekView(c, s, now, day, pool.First(p => p.StaffId == staff), pool, showCx);
            }
            response.SetElementContents("sc-main", html);
            response.ExecuteScript("AdminScheduleJs.ready('" + Ui.Ymd(day) + "','" + Ui.Js(staff) + "','" + Ui.Ymd(now.Date) + "','" + Ui.Js(def) + "');");
            return response;
        }

        private static async Task<List<StaffPick>> Pool(Ctx c)
        {
            List<StaffPick> provs = await Flow.Providers();
            if (!c.U.OwnOnly)
            {
                return provs;
            }
            List<StaffPick> mine = provs.Where(p => p.StaffId == c.U.StaffId).ToList();
            if (mine.Count == 0)
            {
                mine.Add(new StaffPick { StaffId = c.U.StaffId, StaffName = c.U.StaffName, ProviderType = c.U.ProviderType, Color = c.U.Color, Photo = c.U.Photo, OwnOnly = true });
            }
            return mine;
        }

        private static async Task<List<SchedAppt>> Appts(Ctx c, DateTime d0, DateTime d1, string staffId)
        {
            List<SqlParameter> ps = new List<SqlParameter> { App.PDate("d0", d0), App.PDate("d1", d1), App.PBit("own", c.U.OwnOnly), App.P("me", c.U.StaffId, 50), App.P("one", staffId, 50) };
            string sql = "SELECT A.APPT_ID AS ApptId, A.STAFF_ID AS StaffId, A.START_AT AS StartAt, A.END_AT AS EndAt, A.PROC_AT AS ProcAt, A.PROC_MINUTES AS ProcMinutes, A.PICKUP_AT AS PickupAt, A.STATUS AS Status, A.VISIT_TYPE AS VisitType, A.NOTE AS Note, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(SP.SPECIES_NAME, ISNULL(P.SPECIES_KEY, N'')) AS SpeciesName, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS ClientName, ISNULL(V.VISIT_ID, N'') AS VisitId, ISNULL(V.STATUS, N'') AS VisitStatus, ISNULL(R.ROOM_NAME, N'') AS RoomName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = A.VISIT_ID OUTER APPLY (SELECT TOP 1 RM.ROOM_NAME FROM VISITROOM VR JOIN ROOM RM ON RM.ROOM_ID = VR.ROOM_ID WHERE VR.VISIT_ID = V.VISIT_ID AND VR.OUT_AT IS NULL ORDER BY VR.IN_AT DESC) R WHERE A.START_AT >= @d0 AND A.START_AT < @d1 AND A.VISIT_TYPE <> N'boarding' AND (@own = 0 OR A.STAFF_ID = @me) AND (@one = N'' OR A.STAFF_ID = @one) ORDER BY A.START_AT";
            var (list, emsg) = await Helper.GetListT<SchedAppt>(sql, ps);
            return emsg == string.Empty && list != null ? list.GroupBy(a => a.ApptId).Select(g => g.First()).ToList() : new List<SchedAppt>();
        }

        private static async Task<List<SchedOff>> TimeOff(DateTime d0, DateTime d1)
        {
            var (list, emsg) = await Helper.GetListT<SchedOff>("SELECT STAFF_ID AS StaffId, FROM_DATE AS FromDate, TO_DATE AS ToDate, NOTE AS Note FROM TIMEOFF WHERE FROM_DATE <= @d1 AND TO_DATE >= @d0", new List<SqlParameter> { App.PDate("d0", d0), App.PDate("d1", d1) });
            return emsg == string.Empty && list != null ? list : new List<SchedOff>();
        }

        private static string OffText(string holiday, bool open, List<SchedOff> offs, string staffId, DateTime day)
        {
            if (holiday != string.Empty)
            {
                return "Closed · " + holiday;
            }
            if (!open)
            {
                return "Clinic closed";
            }
            SchedOff? t = offs.FirstOrDefault(o => o.StaffId == staffId && o.FromDate.Date <= day && o.ToDate.Date >= day);
            if (t != null)
            {
                return t.Note != string.Empty ? "Time off · " + t.Note : "Time off";
            }
            return "Not working";
        }

        private static (DateTime From, DateTime To) Span(SchedAppt a)
        {
            if (a.ProcAt != null && a.ProcMinutes > 0)
            {
                return (a.ProcAt.Value, a.ProcAt.Value.AddMinutes(a.ProcMinutes));
            }
            return (a.StartAt, a.EndAt > a.StartAt ? a.EndAt : a.StartAt.AddMinutes(15));
        }

        private static bool Busy(SchedAppt a)
        {
            return a.Status == "booked" || a.Status == "confirmed" || a.Status == "checkedin";
        }

        private static string Hrs(int minutes)
        {
            int h = minutes / 60;
            int m = minutes % 60;
            if (h == 0)
            {
                return m + "m";
            }
            return m == 0 ? h + "h" : h + "h " + m + "m";
        }

        private static string Summary(List<SchedAppt> appts)
        {
            List<SchedAppt> live = appts.Where(a => a.Status != "cancelled").ToList();
            if (live.Count == 0)
            {
                return "No appointments";
            }
            int mins = (int)appts.Where(Busy).Sum(a => (Span(a).To - Span(a).From).TotalMinutes);
            return Ui.Plural(live.Count, "appt", "appts") + " · " + Hrs(mins);
        }

        private static string Px(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture) + "px";
        }

        private static string Pct(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture) + "%";
        }

        private static string Color(string v)
        {
            return Ui.SafeColor(v) ? v : "#3F7D6B";
        }

        private static string Role(string providerType)
        {
            return providerType switch
            {
                "vet" => "Veterinarian",
                "tech" => "Technician",
                "groomer" => "Groomer",
                _ => "Staff"
            };
        }

        private static string StatusText(string st)
        {
            return st switch
            {
                "confirmed" => "Confirmed",
                "checkedin" => "Checked in",
                "noshow" => "No-show",
                "cancelled" => "Cancelled",
                _ => "Booked"
            };
        }

        private static string VisitText(SchedAppt a)
        {
            return a.VisitStatus switch
            {
                "waiting" => "Waiting",
                "inroom" => a.RoomName != string.Empty ? "In " + a.RoomName : "In room",
                "checkout" => "At checkout",
                "closed" => "Visit done",
                _ => string.Empty
            };
        }

        private static (int From, int To) Range(List<SchedCol> cols, IEnumerable<(bool Open, TimeSpan F, TimeSpan T)> hours)
        {
            double a = double.MaxValue;
            double z = double.MinValue;
            foreach (var h in hours.Where(x => x.Open))
            {
                a = Math.Min(a, h.F.TotalMinutes);
                z = Math.Max(z, h.T.TotalMinutes);
            }
            foreach (SchedCol col in cols)
            {
                foreach (Window w in col.Wins)
                {
                    a = Math.Min(a, w.From.TotalMinutes);
                    z = Math.Max(z, w.To.TotalMinutes);
                }
                foreach (SchedAppt ap in col.Appts)
                {
                    var (f, t) = Span(ap);
                    a = Math.Min(a, (f - col.Day).TotalMinutes);
                    z = Math.Max(z, (t - col.Day).TotalMinutes);
                }
            }
            if (a == double.MaxValue)
            {
                return (0, 0);
            }
            int from = Math.Clamp((int)Math.Floor(a / 60) * 60, 0, 1380);
            int to = Math.Clamp((int)Math.Ceiling(z / 60) * 60, from + 60, 1440);
            return (from, to);
        }

        private static string Bar(Ctx c, DateTime day, string staff, List<StaffPick> pool, bool everyone, bool showCx, int cxCount)
        {
            bool week = staff != string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sc-bar\">");
            sb.Append("<div class=\"sc-seg\" role=\"group\" aria-label=\"View\"><button type=\"button\" class=\"" + (week ? string.Empty : "sc-son") + "\" aria-pressed=\"" + (week ? "false" : "true") + "\" onclick=\"AdminScheduleJs.mode('day')\">Day</button><button type=\"button\" class=\"" + (week ? "sc-son" : string.Empty) + "\" aria-pressed=\"" + (week ? "true" : "false") + "\" onclick=\"AdminScheduleJs.mode('week')\">Week</button></div>");
            sb.Append("<div class=\"sc-step\"><button type=\"button\" class=\"sc-ibtn sc-sbtn\" aria-label=\"" + (week ? "Previous week" : "Previous day") + "\" onclick=\"AdminScheduleJs.step(-1)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 6l-6 6 6 6\"/></svg></button><button type=\"button\" class=\"sc-btn sc-btn-ghost sc-btn-sm\" onclick=\"AdminScheduleJs.today()\">Today</button><button type=\"button\" class=\"sc-ibtn sc-sbtn\" aria-label=\"" + (week ? "Next week" : "Next day") + "\" onclick=\"AdminScheduleJs.step(1)\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg></button></div>");
            sb.Append("<label class=\"sc-field sc-fdate\"><span class=\"sc-sr\">Date</span><input type=\"date\" id=\"sc-date\" value=\"" + Ui.Ymd(day) + "\" onchange=\"AdminScheduleJs.pick()\"></label>");
            if (!c.U.OwnOnly && pool.Count > 0)
            {
                List<(string, string)> opts = new List<(string, string)> { (string.Empty, "Everyone (day)") };
                opts.AddRange(pool.Select(p => (p.StaffId, p.StaffName + " (week)")));
                sb.Append("<label class=\"sc-field sc-fstaff\"><span class=\"sc-sr\">Staff member</span><select id=\"sc-staff\" onchange=\"AdminScheduleJs.who()\">" + Ui.Opts(opts, staff) + "</select></label>");
            }
            sb.Append("<span class=\"sc-grow\"></span><div class=\"sc-checks\">");
            if (!week && !c.U.OwnOnly)
            {
                sb.Append("<label class=\"sc-chk\"><input type=\"checkbox\" id=\"sc-all\"" + (everyone ? " checked" : string.Empty) + " onchange=\"AdminScheduleJs.toggles()\"><span>Show everyone</span></label>");
            }
            sb.Append("<label class=\"sc-chk\"><input type=\"checkbox\" id=\"sc-cxl\"" + (showCx ? " checked" : string.Empty) + " onchange=\"AdminScheduleJs.toggles()\"><span>Show cancelled" + (cxCount > 0 ? " (" + cxCount + ")" : string.Empty) + "</span></label>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Legend()
        {
            StringBuilder sb = new StringBuilder("<ul class=\"sc-legend\" aria-label=\"Colour key\">");
            foreach (string st in States)
            {
                sb.Append("<li><i class=\"sc-key sc-b-" + st + "\"></i>" + StatusText(st) + "</li>");
            }
            sb.Append("<li><i class=\"sc-key sc-key-off\"></i>Not working</li></ul>");
            return sb.ToString();
        }

        private async Task<(string Html, string First)> DayView(Ctx c, Dictionary<string, string> s, DateTime now, DateTime day, List<StaffPick> pool, bool everyone, bool showCx)
        {
            var hours = Slots.Hours(s, day);
            string holiday = await Slots.Holiday(day);
            bool open = hours.Open && holiday == string.Empty;
            List<Window> wins = await Slots.Windows(s, day, pool.Select(p => p.StaffId));
            List<SchedAppt> all = await Appts(c, day, day.AddDays(1), string.Empty);
            List<SchedOff> offs = await TimeOff(day, day);
            int cxCount = all.Count(a => a.Status == "cancelled");
            List<SchedAppt> shown = all.Where(a => showCx || a.Status != "cancelled").ToList();
            List<SchedCol> cols = new List<SchedCol>();
            foreach (StaffPick p in pool)
            {
                List<Window> w = wins.Where(x => x.StaffId == p.StaffId).OrderBy(x => x.From).ToList();
                List<SchedAppt> ap = shown.Where(a => a.StaffId == p.StaffId).ToList();
                if (!c.U.OwnOnly && !everyone && w.Count == 0 && ap.Count == 0)
                {
                    continue;
                }
                string accent = Color(p.Color);
                string head = "<a class=\"sc-ch\" style=\"--sc-c:" + accent + "\" href=\"AdminSchedule?d=" + Ui.Ymd(day) + "&amp;s=" + Ui.Attr(Uri.EscapeDataString(p.StaffId)) + "\" title=\"See " + Ui.Attr(p.StaffName) + "'s week\">" + Ui.Avatar("sc", p.StaffId, p.StaffName, p.Photo) + "<span class=\"sc-cht\"><b>" + Ui.Enc(p.StaffName) + "</b><small>" + Role(p.ProviderType) + "</small><em>" + Summary(all.Where(a => a.StaffId == p.StaffId).ToList()) + "</em></span></a>";
                cols.Add(new SchedCol { StaffId = p.StaffId, Day = day, Head = head, Wins = w, Appts = ap, OffText = OffText(holiday, hours.Open, offs, p.StaffId, day), Who = p.StaffName });
            }
            if (!c.U.OwnOnly)
            {
                HashSet<string> ids = new HashSet<string>(pool.Select(p => p.StaffId));
                List<SchedAppt> other = shown.Where(a => !ids.Contains(a.StaffId)).ToList();
                if (other.Count > 0)
                {
                    string head = "<div class=\"sc-ch\" style=\"--sc-c:#8A8F8C\"><span class=\"sc-av sc-av-none\" aria-hidden=\"true\">?</span><span class=\"sc-cht\"><b>Not assigned</b><small>No provider set</small><em>" + Summary(all.Where(a => !ids.Contains(a.StaffId)).ToList()) + "</em></span></div>";
                    cols.Add(new SchedCol { StaffId = string.Empty, Day = day, Head = head, Appts = other, OffText = string.Empty, Who = string.Empty });
                }
            }
            StringBuilder sb = new StringBuilder();
            int working = cols.Count(x => x.Wins.Count > 0);
            string sub = Ui.LongDay(day) + (day == now.Date ? " · Today" : string.Empty);
            if (open && !c.U.OwnOnly)
            {
                sub += " · " + Ui.Plural(working, "person working", "people working");
            }
            sb.Append("<div class=\"sc-ptop\"><div><h1>Schedule</h1><p>" + Ui.Enc(sub) + "</p></div></div>");
            sb.Append(Bar(c, day, string.Empty, pool, everyone, showCx, cxCount));
            if (!open)
            {
                string why = holiday != string.Empty ? "The clinic is closed on " + Ui.LongDay(day) + " for " + holiday + "." : "The clinic is closed on " + Ui.DayNames[(int)day.DayOfWeek] + "s.";
                sb.Append("<div class=\"sc-panel sc-closed\"><svg viewBox=\"0 0 24 24\" width=\"30\" height=\"30\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 9h16v11H4zM8 9V6a4 4 0 0 1 8 0v3\"/></svg><div><h2>Clinic closed</h2><p>" + Ui.Enc(why) + (shown.Count > 0 ? " Some appointments are still on the books for this day, shown below." : " Nothing is booked. Use the arrows to move to an open day.") + "</p></div></div>");
                if (shown.Count == 0)
                {
                    return (sb.ToString(), string.Empty);
                }
                cols = cols.Where(x => x.Appts.Count > 0).ToList();
            }
            if (cols.Count == 0)
            {
                sb.Append("<div class=\"sc-panel sc-closed\"><div><h2>Nobody on the schedule</h2><p>No one is working on this day and nothing is booked." + (c.U.OwnOnly ? string.Empty : " Tick “Show everyone” to see all staff.") + "</p></div></div>");
                return (sb.ToString(), string.Empty);
            }
            var (from, to) = Range(cols, new[] { (open, hours.From, hours.To) });
            sb.Append(Grid(c, cols, from, to, now, false));
            sb.Append(Legend());
            return (sb.ToString(), cols.FirstOrDefault(x => x.StaffId != string.Empty)?.StaffId ?? string.Empty);
        }

        private async Task<string> WeekView(Ctx c, Dictionary<string, string> s, DateTime now, DateTime day, StaffPick p, List<StaffPick> pool, bool showCx)
        {
            DateTime mon = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            List<SchedAppt> all = await Appts(c, mon, mon.AddDays(7), p.StaffId);
            List<SchedOff> offs = await TimeOff(mon, mon.AddDays(6));
            int cxCount = all.Count(a => a.Status == "cancelled");
            List<SchedAppt> shown = all.Where(a => showCx || a.Status != "cancelled").ToList();
            List<SchedCol> cols = new List<SchedCol>();
            List<(bool, TimeSpan, TimeSpan)> hours = new List<(bool, TimeSpan, TimeSpan)>();
            string accent = Color(p.Color);
            for (int i = 0; i < 7; i++)
            {
                DateTime d = mon.AddDays(i);
                var h = Slots.Hours(s, d);
                string holiday = await Slots.Holiday(d);
                hours.Add((h.Open && holiday == string.Empty, h.From, h.To));
                List<Window> w = (await Slots.Windows(s, d, new[] { p.StaffId })).OrderBy(x => x.From).ToList();
                List<SchedAppt> dayAll = all.Where(a => a.StartAt.Date == d).ToList();
                string head = "<a class=\"sc-ch sc-chd" + (d == now.Date ? " sc-chtoday" : string.Empty) + "\" style=\"--sc-c:" + accent + "\" href=\"AdminSchedule?d=" + Ui.Ymd(d) + "\" title=\"See everyone on " + Ui.Attr(Ui.Day(d, false)) + "\"><span class=\"sc-cht\"><b>" + d.ToString("ddd", Ui.Us) + " <span>" + d.ToString("MMM d", Ui.Us) + "</span></b><small>" + (d == now.Date ? "Today" : w.Count > 0 ? Ui.Hm(d.Add(w[0].From)) + " – " + Ui.Hm(d.Add(w[^1].To)) : "Off") + "</small><em>" + Summary(dayAll) + "</em></span></a>";
                cols.Add(new SchedCol { StaffId = p.StaffId, Day = d, Head = head, Wins = w, Appts = shown.Where(a => a.StartAt.Date == d).ToList(), OffText = OffText(holiday, h.Open, offs, p.StaffId, d), Who = p.StaffName });
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sc-ptop\"><div class=\"sc-wtop\" style=\"--sc-c:" + accent + "\">" + Ui.Avatar("sc", p.StaffId, p.StaffName, p.Photo, "lg") + "<div><h1>" + Ui.Enc(p.StaffName) + "</h1><p>" + Role(p.ProviderType) + " · Week of " + Ui.Enc(mon.ToString("MMM d", Ui.Us) + " – " + mon.AddDays(6).ToString("MMM d, yyyy", Ui.Us)) + " · " + Summary(all) + "</p></div></div></div>");
            sb.Append(Bar(c, day, p.StaffId, pool, false, showCx, cxCount));
            var (from, to) = Range(cols, hours);
            if (to <= from)
            {
                sb.Append("<div class=\"sc-panel sc-closed\"><div><h2>Closed all week</h2><p>The clinic has no open hours in this week.</p></div></div>");
                return sb.ToString();
            }
            sb.Append(Grid(c, cols, from, to, now, true));
            sb.Append(Legend());
            return sb.ToString();
        }

        private static string Grid(Ctx c, List<SchedCol> cols, int from, int to, DateTime now, bool week)
        {
            double height = (to - from) * Ppm;
            bool live = cols.Any(x => x.Day == now.Date);
            double nowMin = (now - now.Date).TotalMinutes;
            double focus;
            if (!week && live && nowMin >= from && nowMin <= to)
            {
                focus = (nowMin - from - 60) * Ppm;
            }
            else
            {
                double first = double.MaxValue;
                foreach (SchedCol col in cols)
                {
                    foreach (Window w in col.Wins)
                    {
                        first = Math.Min(first, w.From.TotalMinutes);
                    }
                    foreach (SchedAppt a in col.Appts)
                    {
                        first = Math.Min(first, (Span(a).From - col.Day).TotalMinutes);
                    }
                }
                focus = first == double.MaxValue ? 0 : (first - from - 15) * Ppm;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sc-wrap\" id=\"sc-wrap\" data-focus=\"" + Math.Max(0, (int)focus) + "\" tabindex=\"0\" aria-label=\"Schedule grid\">");
            sb.Append("<div class=\"sc-grid" + (week ? " sc-week" : string.Empty) + "\" id=\"sc-grid\" style=\"--sc-n:" + cols.Count + "\" data-from=\"" + from + "\" data-to=\"" + to + "\" data-ppm=\"" + Ppm.ToString(CultureInfo.InvariantCulture) + "\" data-now=\"" + nowMin.ToString("0.##", CultureInfo.InvariantCulture) + "\" data-live=\"" + (live ? "1" : "0") + "\">");
            sb.Append("<div class=\"sc-corner\"></div>");
            foreach (SchedCol col in cols)
            {
                sb.Append(col.Head);
            }
            sb.Append("<div class=\"sc-times\" style=\"height:" + Px(height) + "\">");
            for (int m = from; m < to; m += 60)
            {
                sb.Append("<span style=\"top:" + Px((m - from) * Ppm) + "\">" + DateTime.Today.AddMinutes(m).ToString("h tt", Ui.Us) + "</span>");
            }
            if (live)
            {
                sb.Append("<i class=\"sc-now sc-nowdot\" style=\"top:" + Px((nowMin - from) * Ppm) + "\"" + (nowMin < from || nowMin > to ? " hidden" : string.Empty) + "></i>");
            }
            sb.Append("</div>");
            bool canAdd = c.Can("appts", 'A');
            foreach (SchedCol col in cols)
            {
                sb.Append(Column(c, col, from, to, now, canAdd, height));
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Off(int a, int z, int from, string label, bool full)
        {
            if (z <= a)
            {
                return string.Empty;
            }
            double h = (z - a) * Ppm;
            return "<div class=\"sc-off" + (full ? " sc-offall" : string.Empty) + "\" style=\"top:" + Px((a - from) * Ppm) + ";height:" + Px(h) + "\">" + (label != string.Empty && h >= 22 ? "<span>" + Ui.Enc(label) + "</span>" : string.Empty) + "</div>";
        }

        private static string Column(Ctx c, SchedCol col, int from, int to, DateTime now, bool canAdd, double height)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sc-col" + (col.Day == now.Date ? " sc-ctoday" : string.Empty) + "\" style=\"height:" + Px(height) + "\">");
            List<(int F, int T)> work = col.Wins.Select(w => ((int)w.From.TotalMinutes, (int)w.To.TotalMinutes)).OrderBy(x => x.Item1).ToList();
            if (col.StaffId != string.Empty)
            {
                if (work.Count == 0)
                {
                    sb.Append(Off(from, to, from, col.OffText, true));
                }
                else
                {
                    int cur = from;
                    foreach (var w in work)
                    {
                        sb.Append(Off(cur, w.F, from, cur == from ? string.Empty : "Break", false));
                        cur = Math.Max(cur, w.T);
                    }
                    sb.Append(Off(cur, to, from, string.Empty, false));
                }
            }
            List<(SchedAppt A, double F, double T)> items = col.Appts.Select(a =>
            {
                var (f, t) = Span(a);
                double fm = Math.Clamp((f - col.Day).TotalMinutes, from, to);
                double tm = Math.Clamp((t - col.Day).TotalMinutes, from, to);
                return (a, fm, Math.Max(tm, fm + 15));
            }).OrderBy(x => x.Item2).ThenByDescending(x => x.Item3 - x.Item2).ToList();
            if (canAdd && col.StaffId != string.Empty)
            {
                foreach (var w in work)
                {
                    for (int m = (int)Math.Ceiling(w.F / 15.0) * 15; m + 15 <= w.T; m += 15)
                    {
                        DateTime at = col.Day.AddMinutes(m);
                        if (at <= now.AddMinutes(-15) || items.Any(x => x.F < m + 15 && x.T > m))
                        {
                            continue;
                        }
                        string hm = at.ToString("HH:mm", CultureInfo.InvariantCulture);
                        sb.Append("<a class=\"sc-slot\" style=\"top:" + Px((m - from) * Ppm) + "\" href=\"AdminAppts?s=" + Ui.Attr(Uri.EscapeDataString(col.StaffId)) + "&amp;d=" + Ui.Ymd(col.Day) + "&amp;t=" + hm + "\" aria-label=\"Book " + Ui.Attr(col.Who) + " on " + Ui.Attr(Ui.Day(col.Day, false)) + " at " + Ui.Hm(at) + "\"><span>+ " + Ui.Hm(at) + "</span></a>");
                    }
                }
            }
            int i = 0;
            while (i < items.Count)
            {
                int j = i;
                double end = items[i].T;
                List<double> lanes = new List<double>();
                List<int> laneOf = new List<int>();
                while (j < items.Count && (j == i || items[j].F < end))
                {
                    int lane = lanes.FindIndex(x => x <= items[j].F);
                    if (lane < 0)
                    {
                        lanes.Add(items[j].T);
                        lane = lanes.Count - 1;
                    }
                    else
                    {
                        lanes[lane] = items[j].T;
                    }
                    laneOf.Add(lane);
                    end = Math.Max(end, items[j].T);
                    j++;
                }
                double width = 100.0 / lanes.Count;
                for (int k = i; k < j; k++)
                {
                    sb.Append(Block(c, items[k].A, items[k].F, items[k].T, from, laneOf[k - i] * width, width));
                }
                i = j;
            }
            if (col.Day == now.Date)
            {
                double nm = (now - now.Date).TotalMinutes;
                sb.Append("<i class=\"sc-now\" style=\"top:" + Px((nm - from) * Ppm) + "\"" + (nm < from || nm > to ? " hidden" : string.Empty) + "></i>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Block(Ctx c, SchedAppt a, double f, double t, int from, double left, double width)
        {
            string st = States.Contains(a.Status) ? a.Status : "booked";
            bool drop = a.ProcAt != null && a.ProcMinutes > 0;
            var (sf, stt) = Span(a);
            string pet = a.PetName + (a.SpeciesName != string.Empty ? " (" + a.SpeciesName + ")" : string.Empty);
            string kind = drop ? "Surgery · " + a.BookName : a.BookName;
            string drops = drop ? "Drop-off " + Ui.Hm(a.StartAt) + (a.PickupAt != null ? " · Pick-up " + Ui.Hm(a.PickupAt.Value) : string.Empty) : string.Empty;
            string vt = a.VisitStatus != "cancelled" ? VisitText(a) : string.Empty;
            List<string> tip = new List<string> { Ui.Hm(sf) + " – " + Ui.Hm(stt) + " · " + StatusText(st), pet, kind };
            if (drops != string.Empty)
            {
                tip.Add(drops);
            }
            if (a.ClientName.Trim() != string.Empty)
            {
                tip.Add("Owner: " + a.ClientName.Trim());
            }
            if (vt != string.Empty)
            {
                tip.Add("Visit: " + vt);
            }
            if (a.Note.Trim() != string.Empty)
            {
                tip.Add("Note: " + Ui.OneLine(a.Note, 140));
            }
            double h = Math.Max(22, (t - f) * Ppm - 2);
            string cls = "sc-blk sc-b-" + st + (drop ? " sc-bdrop" : string.Empty) + (h < 40 ? " sc-bshort" : h >= 58 ? " sc-btall" : string.Empty) + (width < 99 ? " sc-bnarrow" : string.Empty);
            string style = "top:" + Px((f - from) * Ppm + 1) + ";--sc-bh:" + Px(h) + ";left:calc(" + Pct(left) + " + 3px);width:calc(" + Pct(width) + " - 6px)";
            StringBuilder inner = new StringBuilder();
            inner.Append("<span class=\"sc-bl1\"><span class=\"sc-btime\">" + Ui.Hm(sf) + "</span> <b>" + Ui.Enc(a.PetName) + "</b>" + (a.SpeciesName != string.Empty ? " <small>" + Ui.Enc(a.SpeciesName) + "</small>" : string.Empty) + "</span>");
            string tags = ((st == "noshow" || st == "cancelled" || st == "checkedin") && vt == string.Empty ? "<i class=\"sc-tg sc-tg-" + st + "\">" + StatusText(st) + "</i>" : string.Empty) + (vt != string.Empty ? "<i class=\"sc-tg sc-tv-" + Ui.Attr(a.VisitStatus) + "\">" + Ui.Enc(vt) + "</i>" : string.Empty);
            inner.Append("<span class=\"sc-brow\"><span class=\"sc-bl2\">" + Ui.Enc(kind) + "</span>" + (tags != string.Empty ? "<span class=\"sc-btags\">" + tags + "</span>" : string.Empty) + "</span>");
            if (drops != string.Empty)
            {
                inner.Append("<span class=\"sc-bl3\">" + Ui.Enc(drops) + "</span>");
            }
            string title = Ui.Attr(string.Join("\n", tip));
            if (c.Can("appts", 'V'))
            {
                return "<a class=\"" + cls + "\" style=\"" + style + "\" href=\"AdminAppts?a=" + Ui.Attr(Uri.EscapeDataString(a.ApptId)) + "\" title=\"" + title + "\">" + inner + "</a>";
            }
            if (a.VisitId != string.Empty && c.Can("visits", 'V'))
            {
                return "<a class=\"" + cls + "\" style=\"" + style + "\" href=\"AdminVisit?v=" + Ui.Attr(Uri.EscapeDataString(a.VisitId)) + "\" title=\"" + title + "\">" + inner + "</a>";
            }
            return "<div class=\"" + cls + "\" style=\"" + style + "\" title=\"" + title + "\" tabindex=\"0\">" + inner + "</div>";
        }
    }
}
