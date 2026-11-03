using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminHours : WebPage
    {
        private const string Key = "hours";
        private const string Me = "AdminHoursJs";
        private static readonly int[] Order = { 1, 2, 3, 4, 5, 6, 0 };
        private static readonly string[] Short = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Hours and time off | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            string tab = (QueryValue("t") ?? string.Empty).Trim();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--hr-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("hr"))
                .Replace("{plhd_qt}", tab == "off" || tab == "hol" ? tab : string.Empty);
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private static string Hex(string? v)
        {
            string t = (v ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(t, "^[a-f0-9]{32}$") ? t : string.Empty;
        }

        private static SqlParameter PDay(string name, DateTime d)
        {
            return new SqlParameter(name, SqlDbType.Date) { Value = d.Date };
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript(Me + ".expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            if (msg != string.Empty)
            {
                response.ExecuteScript(Me + ".toast('" + Ui.Js(msg) + "');");
            }
        }

        private static string Denied()
        {
            return "<div class=\"hr-panel hr-deny\"><h1>Hours and time off</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"hr-btn hr-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static string Hm(string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? hm : Clock(t.Value);
        }

        private static string Clock(TimeSpan t)
        {
            return Ui.Hm(DateTime.Today.Add(t)).Replace(":00 ", " ");
        }

        private static string BreakText(string a, string b)
        {
            TimeSpan? x = Ui.ParseHm(a);
            TimeSpan? y = Ui.ParseHm(b);
            return x == null || y == null || y <= x ? string.Empty : "Break " + Clock(x.Value) + " · " + (int)(y.Value - x.Value).TotalMinutes + " min";
        }

        private static string Dates(DateTime a, DateTime b)
        {
            if (a.Date == b.Date)
            {
                return Ui.Day(a);
            }
            return a.Year == b.Year ? Ui.Day(a, false) + " – " + Ui.Day(b) : Ui.Day(a) + " – " + Ui.Day(b);
        }

        private static async Task<List<HrPerson>> Providers()
        {
            var (rows, emsg) = await Helper.GetListT<HrPerson>("SELECT STAFF_ID AS StaffId, STAFF_NAME AS StaffName, TITLE AS Title, PROVIDER_TYPE AS ProviderType, PHOTO AS Photo, COLOR AS Color FROM STAFF WHERE ACTIVE = 1 AND PROVIDER_TYPE <> N'' ORDER BY CASE PROVIDER_TYPE WHEN N'vet' THEN 1 WHEN N'tech' THEN 2 ELSE 3 END, STAFF_NAME");
            return emsg == string.Empty && rows != null ? rows : new List<HrPerson>();
        }

        private static async Task<List<HrPerson>> Everyone(string keep)
        {
            var (rows, emsg) = await Helper.GetListT<HrPerson>("SELECT STAFF_ID AS StaffId, STAFF_NAME AS StaffName, TITLE AS Title, PROVIDER_TYPE AS ProviderType, PHOTO AS Photo, COLOR AS Color FROM STAFF WHERE ACTIVE = 1 OR STAFF_ID = @keep ORDER BY CASE PROVIDER_TYPE WHEN N'vet' THEN 1 WHEN N'tech' THEN 2 WHEN N'groomer' THEN 3 ELSE 4 END, STAFF_NAME", new List<SqlParameter> { App.P("keep", keep, 50) });
            return emsg == string.Empty && rows != null ? rows : new List<HrPerson>();
        }

        private static async Task<List<HrDay>> Days(string staffId)
        {
            string where = staffId == string.Empty ? string.Empty : " WHERE STAFF_ID = @sid";
            var (rows, emsg) = await Helper.GetListT<HrDay>("SELECT STAFF_ID AS StaffId, DAY_NO AS DayNo, IS_ON AS IsOn, FROM_HM AS FromHm, TO_HM AS ToHm, BREAK_FROM AS BreakFrom, BREAK_TO AS BreakTo FROM STAFFHOURS" + where, new List<SqlParameter> { App.P("sid", staffId, 50) });
            return emsg == string.Empty && rows != null ? rows : new List<HrDay>();
        }

        private static string ClinicStrip(Dictionary<string, string> s, Ctx c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"hr-clinic\" aria-label=\"Clinic opening hours\"><div class=\"hr-clh\"><b>Clinic opening hours</b><span>Staff hours only count for bookings inside these.</span>" + (c.Can("settings", 'V') ? "<a class=\"hr-alink\" href=\"AdminSettings\">Change in Settings</a>" : string.Empty) + "</div><ul class=\"hr-cdays\">");
            DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            foreach (int d in Order)
            {
                var (open, from, to) = Slots.Hours(s, monday.AddDays((d + 6) % 7));
                sb.Append("<li class=\"" + (open ? string.Empty : "hr-closed") + "\"><b>" + Short[d] + "</b><span>" + (open ? Ui.Enc(Clock(from) + " – " + Clock(to)) : "Closed") + "</span></li>");
            }
            sb.Append("</ul></section>");
            return sb.ToString();
        }

        private static async Task<string> Body(Ctx c, Dictionary<string, string> s)
        {
            DateTime today = App.Now(s).Date;
            StringBuilder sb = new StringBuilder();
            bool off = c.Can(Key, 'X');
            sb.Append("<div class=\"hr-ptop\"><div><h1>Hours and time off</h1><p>When each vet, technician and groomer works, their days off, and the days the whole clinic is closed. Online booking and the schedule follow these.</p></div></div>");
            sb.Append(ClinicStrip(s, c));
            sb.Append("<nav class=\"hr-tabs\" aria-label=\"Sections\"><button type=\"button\" class=\"hr-tab\" data-tab=\"week\" onclick=\"" + Me + ".tab('week')\">Weekly hours</button>" + (off ? "<button type=\"button\" class=\"hr-tab\" data-tab=\"off\" onclick=\"" + Me + ".tab('off')\">Time off</button>" : string.Empty) + "<button type=\"button\" class=\"hr-tab\" data-tab=\"hol\" onclick=\"" + Me + ".tab('hol')\">Holidays</button></nav>");
            List<HrOff> offs = off ? await Offs(c, today) : new List<HrOff>();
            sb.Append("<section class=\"hr-sect\" id=\"hr-s-week\" data-tab=\"week\">" + await WeekHtml(c, s, offs.Where(o => o.ToDate >= today && o.FromDate <= today.AddDays(30)).ToList()) + "</section>");
            if (off)
            {
                sb.Append("<section class=\"hr-sect\" id=\"hr-s-off\" data-tab=\"off\" hidden>" + OffHtml(c, today, offs) + "</section>");
            }
            sb.Append("<section class=\"hr-sect\" id=\"hr-s-hol\" data-tab=\"hol\" hidden>" + await HolHtml(c, today) + "</section>");
            return sb.ToString();
        }

        private static async Task<string> WeekHtml(Ctx c, Dictionary<string, string> s, List<HrOff> soon)
        {
            List<HrPerson> people = await Providers();
            List<HrDay> days = await Days(string.Empty);
            DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"hr-shead\"><h2>Weekly hours</h2><span class=\"hr-sub\">The usual week for every active provider. " + (c.Can(Key, 'E') ? "Click a row to change it." : string.Empty) + "</span></div>");
            if (people.Count == 0)
            {
                sb.Append("<div class=\"hr-tw\"><p class=\"hr-empty\">No active vets, technicians or groomers yet. Add them in Staff and permissions.</p></div>");
                return sb.ToString();
            }
            bool edit = c.Can(Key, 'E');
            sb.Append("<div class=\"hr-tw\"><table class=\"hr-table hr-wtable\"><thead><tr><th>Person</th>" + string.Concat(Order.Select(d => "<th>" + Short[d] + "</th>")) + "<th class=\"hr-num\">Hours</th></tr></thead><tbody>");
            foreach (HrPerson p in people)
            {
                string go = Me + ".week('" + Ui.Js(p.StaffId) + "')";
                sb.Append("<tr class=\"" + (edit ? "hr-row" : string.Empty) + "\"" + (edit ? " tabindex=\"0\" onclick=\"" + go + "\" onkeydown=\"if(event.key==='Enter')" + go + "\"" : string.Empty) + ">");
                List<HrOff> mine = soon.Where(o => o.StaffId == p.StaffId).ToList();
                sb.Append("<td class=\"hr-cname\"><div class=\"hr-pn\">" + Ui.Avatar("hr", p.StaffId, p.StaffName, p.Photo) + "<div><b>" + Ui.Enc(p.StaffName) + "</b><small>" + Ui.Enc(p.Title) + "</small>" + string.Concat(mine.Select(o => "<small class=\"hr-offsoon\">Off " + Ui.Enc(Dates(o.FromDate, o.ToDate)) + "</small>")) + "</div></div></td>");
                double total = 0;
                foreach (int d in Order)
                {
                    HrDay? h = days.FirstOrDefault(x => x.StaffId == p.StaffId && x.DayNo == d);
                    if (h == null || !h.IsOn)
                    {
                        sb.Append("<td data-l=\"" + Short[d] + "\" class=\"hr-cell hr-coff\">Off</td>");
                        continue;
                    }
                    TimeSpan? a = Ui.ParseHm(h.FromHm);
                    TimeSpan? z = Ui.ParseHm(h.ToHm);
                    TimeSpan? ba = Ui.ParseHm(h.BreakFrom);
                    TimeSpan? bz = Ui.ParseHm(h.BreakTo);
                    if (a != null && z != null && z > a)
                    {
                        total += (z.Value - a.Value).TotalHours - (ba != null && bz != null && bz > ba ? (bz.Value - ba.Value).TotalHours : 0);
                    }
                    var (open, cf, ct) = Slots.Hours(s, monday.AddDays((d + 6) % 7));
                    bool outside = a != null && z != null && (!open || a < cf || z > ct);
                    sb.Append("<td data-l=\"" + Short[d] + "\" class=\"hr-cell\"><span class=\"hr-span\">" + Ui.Enc(Hm(h.FromHm)) + " –</span> <span class=\"hr-span\">" + Ui.Enc(Hm(h.ToHm)) + "</span>" + (h.BreakFrom != string.Empty ? "<small>" + Ui.Enc(BreakText(h.BreakFrom, h.BreakTo)) + "</small>" : string.Empty) + (outside ? "<small class=\"hr-out\">" + (open ? "Outside clinic hours" : "Clinic closed") + "</small>" : string.Empty) + "</td>");
                }
                sb.Append("<td data-l=\"Hours a week\" class=\"hr-num\">" + total.ToString("0.#", CultureInfo.InvariantCulture) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static async Task<List<HrOff>> Offs(Ctx c, DateTime today)
        {
            const string cols = "T.TIMEOFF_ID AS TimeoffId, T.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'Removed staff') AS StaffName, ISNULL(S.PHOTO, N'') AS Photo, T.FROM_DATE AS FromDate, T.TO_DATE AS ToDate, T.NOTE AS Note, (SELECT COUNT(*) FROM APPOINTMENT A WHERE A.STAFF_ID = T.STAFF_ID AND A.START_AT >= T.FROM_DATE AND A.START_AT < DATEADD(DAY, 1, T.TO_DATE) AND A.STATUS <> N'cancelled' AND A.VISIT_TYPE <> N'boarding') AS Clash FROM TIMEOFF T LEFT JOIN STAFF S ON S.STAFF_ID = T.STAFF_ID";
            var (up, uerr) = await Helper.GetListT<HrOff>("SELECT " + cols + " WHERE T.TO_DATE >= @today ORDER BY T.FROM_DATE, S.STAFF_NAME", new List<SqlParameter> { PDay("today", today) });
            var (past, perr) = await Helper.GetListT<HrOff>("SELECT " + cols + " WHERE T.TO_DATE < @today ORDER BY T.TO_DATE DESC OFFSET 0 ROWS FETCH NEXT 40 ROWS ONLY", new List<SqlParameter> { PDay("today", today) });
            List<HrOff> list = uerr == string.Empty && up != null ? up : new List<HrOff>();
            list.AddRange(perr == string.Empty && past != null ? past : new List<HrOff>());
            return list;
        }

        private static string OffHtml(Ctx c, DateTime today, List<HrOff> offs)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"hr-shead\"><h2>Time off</h2><span class=\"hr-sub\">Days a person is away. They can't be booked on these days.</span><button type=\"button\" class=\"hr-btn hr-btn-accent\" onclick=\"" + Me + ".offForm('', this)\">Add time off</button></div>");
            List<(string, string)> names = offs.GroupBy(o => o.StaffId).Select(g => (g.Key, g.First().StaffName)).OrderBy(x => x.Item2).ToList();
            if (names.Count > 1)
            {
                sb.Append("<div class=\"hr-bar\"><label class=\"hr-field\"><span>Person</span><select id=\"hr-offwho\" onchange=\"" + Me + ".offFilter()\"><option value=\"\">Everyone</option>" + Ui.Opts(names, string.Empty) + "</select></label></div>");
            }
            if (offs.Count == 0)
            {
                sb.Append("<div class=\"hr-tw\"><p class=\"hr-empty\">No time off planned.</p></div>");
                return sb.ToString();
            }
            sb.Append("<div class=\"hr-tw\"><table class=\"hr-table hr-otable\"><thead><tr><th>Person</th><th>Dates</th><th>Days</th><th>Note</th><th>Appointments</th><th></th></tr></thead><tbody id=\"hr-offrows\">");
            bool pastHead = false;
            foreach (HrOff o in offs)
            {
                bool past = o.ToDate.Date < today;
                if (past && !pastHead)
                {
                    pastHead = true;
                    sb.Append("<tr class=\"hr-sep\"><td colspan=\"6\">Past</td></tr>");
                }
                int n = (o.ToDate.Date - o.FromDate.Date).Days + 1;
                sb.Append("<tr class=\"" + (past ? "hr-off" : string.Empty) + "\" data-staff=\"" + Ui.Attr(o.StaffId) + "\">");
                sb.Append("<td class=\"hr-cname\"><div class=\"hr-pn\">" + Ui.Avatar("hr", o.StaffId, o.StaffName, o.Photo) + "<div><b>" + Ui.Enc(o.StaffName) + "</b><small class=\"hr-mob\">" + Ui.Enc(Dates(o.FromDate, o.ToDate)) + "</small></div></div></td>");
                sb.Append("<td data-l=\"Dates\" class=\"hr-desk hr-nw\">" + Ui.Enc(Dates(o.FromDate, o.ToDate)) + (!past && o.FromDate.Date <= today ? "<small class=\"hr-blk hr-now\">Away now</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Days\">" + Ui.Plural(n, "day", "days") + "</td>");
                sb.Append("<td data-l=\"Note\">" + (o.Note != string.Empty ? Ui.Enc(o.Note) : "<span class=\"hr-dim\">-</span>") + "</td>");
                sb.Append("<td data-l=\"Appointments\">" + (past ? "<span class=\"hr-dim\">-</span>" : o.Clash > 0 ? "<button type=\"button\" class=\"hr-tag hr-tag-warn hr-tagb\" onclick=\"" + Me + ".clashes('" + Ui.Js(o.TimeoffId) + "')\">" + Ui.Plural(o.Clash, "to move", "to move") + "</button>" : "<span class=\"hr-tag hr-tag-ok\">None booked</span>") + "</td>");
                sb.Append("<td class=\"hr-tacts\"><button type=\"button\" class=\"hr-btn hr-btn-sm hr-btn-ghost\" onclick=\"" + Me + ".offForm('" + Ui.Js(o.TimeoffId) + "', this)\">Edit</button><button type=\"button\" class=\"hr-btn hr-btn-sm hr-btn-danger\" onclick=\"" + Me + ".offAsk('" + Ui.Js(o.TimeoffId) + "', '" + Ui.Js(o.StaffName + " · " + Dates(o.FromDate, o.ToDate)) + "')\">Delete</button></td></tr>");
            }
            sb.Append("</tbody></table></div>");
            if (offs.Any(o => o.ToDate.Date < today))
            {
                sb.Append("<p class=\"hr-foot\">Past time off shows the latest 40.</p>");
            }
            return sb.ToString();
        }

        private static async Task<string> HolHtml(Ctx c, DateTime today)
        {
            var (rows, emsg) = await Helper.GetListT<HrHoliday>("SELECT H.HOLIDAY_DATE AS HolidayDate, H.HOLIDAY_NAME AS HolidayName, (SELECT COUNT(*) FROM APPOINTMENT A WHERE A.START_AT >= H.HOLIDAY_DATE AND A.START_AT < DATEADD(DAY, 1, H.HOLIDAY_DATE) AND A.STATUS IN (N'booked', N'confirmed')) AS Booked FROM HOLIDAY H WHERE H.HOLIDAY_DATE >= @from ORDER BY H.HOLIDAY_DATE", new List<SqlParameter> { PDay("from", today.AddDays(-120)) });
            List<HrHoliday> list = emsg == string.Empty && rows != null ? rows : new List<HrHoliday>();
            bool edit = c.Can(Key, 'E');
            bool appts = c.Can("appts", 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"hr-shead\"><h2>Holidays</h2><span class=\"hr-sub\">Days the whole clinic is closed. Nobody can be booked, online or at the desk.</span></div>");
            if (edit)
            {
                sb.Append("<form class=\"hr-panel hr-hform\" onsubmit=\"return " + Me + ".holSave()\" novalidate><label class=\"hr-field\"><span>Day</span><input type=\"date\" id=\"hr-h-date\" min=\"" + Ui.Ymd(today) + "\" onchange=\"" + Me + ".holCheck()\"><em class=\"hr-err\" id=\"hr-e-hdate\"></em></label><label class=\"hr-field hr-grow2\"><span>Name</span><input type=\"text\" id=\"hr-h-name\" maxlength=\"60\" placeholder=\"For example: Thanksgiving\" autocomplete=\"off\"><em class=\"hr-err\" id=\"hr-e-hname\"></em></label><button type=\"submit\" class=\"hr-btn hr-btn-accent\" id=\"hr-h-go\">Add holiday</button><div class=\"hr-hwarn\" id=\"hr-h-warn\"></div></form>");
            }
            if (list.Count == 0)
            {
                sb.Append("<div class=\"hr-tw\"><p class=\"hr-empty\">No holidays coming up.</p></div>");
                return sb.ToString();
            }
            sb.Append("<ul class=\"hr-hols\">");
            foreach (HrHoliday h in list)
            {
                bool past = h.HolidayDate.Date < today;
                string day = Ui.Ymd(h.HolidayDate);
                sb.Append("<li class=\"hr-hol" + (past ? " hr-off" : string.Empty) + "\"><div class=\"hr-hd\"><b>" + h.HolidayDate.ToString("MMM", Ui.Us) + "</b><span>" + h.HolidayDate.Day + "</span></div><div class=\"hr-hb\"><b>" + Ui.Enc(h.HolidayName) + "</b><small>" + Ui.Enc(Ui.LongDay(h.HolidayDate)) + (past ? " · past" : string.Empty) + "</small>");
                if (!past && h.Booked > 0)
                {
                    string label = Ui.Plural(h.Booked, "appointment is", "appointments are") + " still booked that day";
                    sb.Append(appts ? "<a class=\"hr-tag hr-tag-warn\" href=\"AdminAppts?d=" + day + "\">" + label + "</a>" : "<span class=\"hr-tag hr-tag-warn\">" + label + "</span>");
                }
                sb.Append("</div>");
                if (edit && !past)
                {
                    sb.Append("<button type=\"button\" class=\"hr-btn hr-btn-sm hr-btn-danger\" onclick=\"" + Me + ".holAsk('" + day + "', '" + Ui.Js(h.HolidayName) + "')\">Delete</button>");
                }
                sb.Append("</li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private async Task Draw(ApiResponse response, Ctx c, string tab, string toast)
        {
            Dictionary<string, string> s = await App.Settings();
            response.SetElementContents("hr-main", await Body(c, s));
            response.ExecuteScript(Me + ".ready('" + Ui.Js(tab) + "');");
            Toast(response, toast);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("hr-menu", Auth.Menu("hr", Me, "AdminHours", c));
            response.SetElementContents("hr-who", Auth.Who("hr", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("hr-main", Denied());
                return response;
            }
            string tab = (GetDataValue("opentab") ?? string.Empty).Trim();
            await Draw(response, c, tab == "off" && c.Can(Key, 'X') ? "off" : tab == "hol" ? "hol" : "week", string.Empty);
            return response;
        }

        public async Task<ApiResponse> WeekForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change working hours.");
                return response;
            }
            HrPerson? p = (await Providers()).FirstOrDefault(x => x.StaffId == CleanId(GetDataValue("staffid")));
            if (p == null)
            {
                Toast(response, "This person was not found.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<HrDay> days = await Days(p.StaffId);
            DateTime monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Weekly hours · " + Ui.Enc(p.StaffName) + "</h2><p class=\"hr-mlead\">Untick a day they don't work. A break is optional; leave both break times empty for none.</p>");
            sb.Append("<form class=\"hr-form\" onsubmit=\"return " + Me + ".weekSave()\" novalidate><input type=\"hidden\" id=\"hr-w-id\" value=\"" + Ui.Attr(p.StaffId) + "\"><div class=\"hr-wk\"><div class=\"hr-wkh\" aria-hidden=\"true\"><span>Day</span><span>Works</span><span>From</span><span>To</span><span>Break from</span><span>Break to</span></div>");
            foreach (int d in Order)
            {
                HrDay h = days.FirstOrDefault(x => x.DayNo == d) ?? new HrDay { DayNo = d, IsOn = false, FromHm = "09:00", ToHm = "17:00" };
                var (open, cf, ct) = Slots.Hours(s, monday.AddDays((d + 6) % 7));
                string dn = Ui.DayNames[d];
                sb.Append("<div class=\"hr-wr" + (h.IsOn ? string.Empty : " hr-wr-off") + "\" id=\"hr-wr-" + d + "\" data-d=\"" + d + "\"><div class=\"hr-wd\"><b>" + dn + "</b><small>" + (open ? "Clinic " + Ui.Enc(Clock(cf) + " – " + Clock(ct)) : "Clinic closed") + "</small></div>");
                sb.Append("<label class=\"hr-check hr-won\"><input type=\"checkbox\" id=\"hr-w-on-" + d + "\"" + (h.IsOn ? " checked" : string.Empty) + " onchange=\"" + Me + ".dayOn(" + d + ")\"><span>Works</span></label>");
                sb.Append("<label class=\"hr-field hr-wt\"><span>From</span><input type=\"time\" step=\"300\" id=\"hr-w-from-" + d + "\" value=\"" + Ui.Attr(h.FromHm) + "\" aria-label=\"" + dn + " from\"></label>");
                sb.Append("<label class=\"hr-field hr-wt\"><span>To</span><input type=\"time\" step=\"300\" id=\"hr-w-to-" + d + "\" value=\"" + Ui.Attr(h.ToHm) + "\" aria-label=\"" + dn + " to\"></label>");
                sb.Append("<label class=\"hr-field hr-wt\"><span>Break from</span><input type=\"time\" step=\"300\" id=\"hr-w-bf-" + d + "\" value=\"" + Ui.Attr(h.BreakFrom) + "\" aria-label=\"" + dn + " break from\"></label>");
                sb.Append("<label class=\"hr-field hr-wt\"><span>Break to</span><input type=\"time\" step=\"300\" id=\"hr-w-bt-" + d + "\" value=\"" + Ui.Attr(h.BreakTo) + "\" aria-label=\"" + dn + " break to\"></label>");
                sb.Append("<em class=\"hr-err hr-werr\" id=\"hr-e-d" + d + "\"></em></div>");
            }
            sb.Append("</div><div class=\"hr-mfoot hr-mfoot-sp\"><button type=\"button\" class=\"hr-btn hr-btn-sm hr-btn-ghost\" onclick=\"" + Me + ".copyMon()\">Copy Monday to Tuesday – Friday</button><span class=\"hr-grow\"></span><button type=\"button\" class=\"hr-btn hr-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"hr-btn hr-btn-accent\" id=\"hr-w-go\">Save hours</button></div><em class=\"hr-err hr-merr\" id=\"hr-e-week\"></em></form>");
            response.SetElementContents("hr-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('');");
            return response;
        }

        public async Task<ApiResponse> WeekSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "weekly hours", "not allowed", false);
                Toast(response, "You can't change working hours.");
                return response;
            }
            HrPerson? p = (await Providers()).FirstOrDefault(x => x.StaffId == CleanId(GetDataValue("staffid")));
            if (p == null)
            {
                Toast(response, "This person was not found or is not an active provider.");
                return response;
            }
            Dictionary<int, HrDay> next = new Dictionary<int, HrDay>();
            Dictionary<int, string> errs = new Dictionary<int, string>();
            foreach (string part in (GetDataValue("week") ?? string.Empty).Split(';'))
            {
                string[] f = part.Split('|');
                if (f.Length != 6 || !int.TryParse(f[0], out int d) || d < 0 || d > 6 || next.ContainsKey(d))
                {
                    continue;
                }
                bool on = f[1] == "1";
                string a = f[2].Trim();
                string z = f[3].Trim();
                string ba = f[4].Trim();
                string bz = f[5].Trim();
                TimeSpan? ta = Ui.ParseHm(a);
                TimeSpan? tz = Ui.ParseHm(z);
                TimeSpan? tba = Ui.ParseHm(ba);
                TimeSpan? tbz = Ui.ParseHm(bz);
                string e = string.Empty;
                if (on)
                {
                    if (ta == null || tz == null)
                    {
                        e = "Enter both start and end times.";
                    }
                    else if (tz <= ta)
                    {
                        e = "The end time must be after the start time.";
                    }
                    else if ((ba == string.Empty) != (bz == string.Empty))
                    {
                        e = "Enter both break times, or leave both empty.";
                    }
                    else if (ba != string.Empty && (tba == null || tbz == null))
                    {
                        e = "The break times don't look right.";
                    }
                    else if (tba != null && tbz != null && tbz <= tba)
                    {
                        e = "The break must end after it starts.";
                    }
                    else if (tba != null && tbz != null && (tba <= ta || tbz >= tz))
                    {
                        e = "The break must sit inside the working day.";
                    }
                }
                errs[d] = e;
                next[d] = new HrDay
                {
                    DayNo = d,
                    IsOn = on,
                    FromHm = ta != null ? a : "09:00",
                    ToHm = tz != null && (ta == null || tz > ta) ? z : "17:00",
                    BreakFrom = on && tba != null && tbz != null ? ba : string.Empty,
                    BreakTo = on && tba != null && tbz != null ? bz : string.Empty
                };
            }
            for (int d = 0; d < 7; d++)
            {
                response.SetElementContents("hr-e-d" + d, errs.TryGetValue(d, out string? e) ? e : string.Empty);
            }
            if (next.Count != 7)
            {
                response.SetElementContents("hr-e-week", "Something went wrong. Close the form and try again.");
                return response;
            }
            response.SetElementContents("hr-e-week", string.Empty);
            if (errs.Values.Any(x => x != string.Empty))
            {
                return response;
            }
            List<HrDay> before = await Days(p.StaffId);
            List<string> sql = new List<string> { "DELETE FROM STAFFHOURS WHERE STAFF_ID = @sid" };
            List<SqlParameter> ps = new List<SqlParameter> { App.P("sid", p.StaffId, 50) };
            List<string> changed = new List<string>();
            foreach (HrDay h in next.Values.OrderBy(x => x.DayNo))
            {
                int d = h.DayNo;
                ps.Add(App.PInt("d" + d, d));
                ps.Add(App.PBit("on" + d, h.IsOn));
                ps.Add(App.P("a" + d, h.FromHm, 5));
                ps.Add(App.P("z" + d, h.ToHm, 5));
                ps.Add(App.P("ba" + d, h.BreakFrom, 5));
                ps.Add(App.P("bz" + d, h.BreakTo, 5));
                sql.Add("INSERT INTO STAFFHOURS (STAFF_ID, DAY_NO, IS_ON, FROM_HM, TO_HM, BREAK_FROM, BREAK_TO) VALUES (@sid, @d" + d + ", @on" + d + ", @a" + d + ", @z" + d + ", @ba" + d + ", @bz" + d + ")");
                HrDay? o = before.FirstOrDefault(x => x.DayNo == d);
                string was = o == null || !o.IsOn ? "off" : o.FromHm + "-" + o.ToHm + (o.BreakFrom != string.Empty ? " (" + o.BreakFrom + "-" + o.BreakTo + ")" : string.Empty);
                string now = !h.IsOn ? "off" : h.FromHm + "-" + h.ToHm + (h.BreakFrom != string.Empty ? " (" + h.BreakFrom + "-" + h.BreakTo + ")" : string.Empty);
                if (was != now)
                {
                    changed.Add(Short[d] + " " + was + " → " + now);
                }
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("hr-e-week", "The hours could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "weekly hours", p.StaffName + " · " + (changed.Count > 0 ? string.Join(", ", changed) : "no change"), true);
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, "week", "Hours saved for " + p.StaffName + ".");
            return response;
        }

        private static async Task<List<HrAppt>> Inside(string staffId, DateTime from, DateTime to)
        {
            var (rows, emsg) = await Helper.GetListT<HrAppt>("SELECT A.APPT_ID AS ApptId, A.START_AT AS StartAt, A.STATUS AS Status, A.VISIT_TYPE AS VisitType, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID WHERE A.STAFF_ID = @sid AND A.START_AT >= @d0 AND A.START_AT < @d1 AND A.STATUS <> N'cancelled' AND A.VISIT_TYPE <> N'boarding' ORDER BY A.START_AT", new List<SqlParameter> { App.P("sid", staffId, 50), App.PDate("d0", from.Date), App.PDate("d1", to.Date.AddDays(1)) });
            return emsg == string.Empty && rows != null ? rows : new List<HrAppt>();
        }

        private static string StatusName(string st)
        {
            return st switch
            {
                "booked" => "Booked",
                "confirmed" => "Confirmed",
                "checkedin" => "Checked in",
                "noshow" => "No-show",
                _ => st
            };
        }

        private static string ApptList(Ctx c, List<HrAppt> list)
        {
            bool link = c.Can("appts", 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<ul class=\"hr-alist\">");
            foreach (HrAppt a in list)
            {
                string inner = "<span class=\"hr-aw\">" + Ui.Enc(Ui.Day(a.StartAt, false) + " · " + Ui.Hm(a.StartAt)) + "</span><span class=\"hr-at\"><b>" + Ui.Enc(a.PetName != string.Empty ? a.PetName : "No pet") + "</b>" + (a.OwnerName.Trim() != string.Empty ? " (" + Ui.Enc(a.OwnerName.Trim()) + ")" : string.Empty) + " · " + Ui.Enc(a.BookName) + "</span><span class=\"hr-st hr-st-" + Ui.Attr(a.Status) + "\">" + Ui.Enc(StatusName(a.Status)) + "</span>";
                sb.Append("<li>" + (link ? "<a class=\"hr-al\" href=\"AdminAppts?a=" + Uri.EscapeDataString(a.ApptId) + "\" target=\"_blank\" rel=\"noopener\">" + inner + "<span class=\"hr-ago\">Open</span></a>" : "<div class=\"hr-al\">" + inner + "</div>") + "</li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string ClashHtml(Ctx c, string name, List<HrAppt> list)
        {
            if (list.Count == 0)
            {
                return "<p class=\"hr-okline\">No appointments are booked with " + Ui.Enc(name) + " on these days.</p>";
            }
            return "<div class=\"hr-warn\"><b>" + Ui.Plural(list.Count, "appointment", "appointments") + " with " + Ui.Enc(name) + " on these days.</b><p>Saving does not move them. " + (c.Can("appts", 'V') ? "Open each one to move it to someone else or another day." : "Ask the front desk to move them.") + "</p>" + ApptList(c, list) + "</div>";
        }

        private async Task<(Ctx? C, Dictionary<string, string> S)> OffGuard(ApiResponse response)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, new Dictionary<string, string>());
            }
            if (!c.Can(Key, 'X'))
            {
                await App.Log(c.U.StaffId, "time off", "not allowed", false);
                Toast(response, "You can't manage time off.");
                return (null, new Dictionary<string, string>());
            }
            return (c, await App.Settings());
        }

        private static async Task<HrOff?> OneOff(string id)
        {
            var (row, emsg) = await Helper.GetOneT<HrOff>("SELECT T.TIMEOFF_ID AS TimeoffId, T.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'Removed staff') AS StaffName, ISNULL(S.PHOTO, N'') AS Photo, T.FROM_DATE AS FromDate, T.TO_DATE AS ToDate, T.NOTE AS Note, 0 AS Clash FROM TIMEOFF T LEFT JOIN STAFF S ON S.STAFF_ID = T.STAFF_ID WHERE T.TIMEOFF_ID = @id", new List<SqlParameter> { App.P("id", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        public async Task<ApiResponse> OffForm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, s) = await OffGuard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("offid"));
            HrOff o = new HrOff { FromDate = App.Now(s).Date.AddDays(1), ToDate = App.Now(s).Date.AddDays(1) };
            if (id != string.Empty)
            {
                HrOff? found = await OneOff(id);
                if (found == null)
                {
                    Toast(response, "This time off was not found. It may have been deleted.");
                    return response;
                }
                o = found;
            }
            List<HrPerson> people = await Everyone(o.StaffId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (id == string.Empty ? "Add time off" : "Change time off") + "</h2><form class=\"hr-form\" onsubmit=\"return " + Me + ".offSave()\" novalidate><input type=\"hidden\" id=\"hr-o-id\" value=\"" + Ui.Attr(id) + "\"><div class=\"hr-fgrid\">");
            sb.Append("<label class=\"hr-field hr-span2\"><span>Who</span><select id=\"hr-o-who\" onchange=\"" + Me + ".offCheck()\"><option value=\"\">Pick a person</option>" + Ui.Opts(people.Select(p => (p.StaffId, p.StaffName + (p.ProviderType == string.Empty ? " (office)" : string.Empty))), o.StaffId) + "</select><em class=\"hr-err\" id=\"hr-e-owho\"></em></label>");
            sb.Append("<label class=\"hr-field\"><span>First day off</span><input type=\"date\" id=\"hr-o-from\" value=\"" + Ui.Ymd(o.FromDate) + "\" onchange=\"" + Me + ".offCheck()\"><em class=\"hr-err\" id=\"hr-e-ofrom\"></em></label>");
            sb.Append("<label class=\"hr-field\"><span>Last day off</span><input type=\"date\" id=\"hr-o-to\" value=\"" + Ui.Ymd(o.ToDate) + "\" onchange=\"" + Me + ".offCheck()\"><em class=\"hr-err\" id=\"hr-e-oto\"></em></label>");
            sb.Append("<label class=\"hr-field hr-span2\"><span>Note <small>(optional, for example: conference)</small></span><input type=\"text\" id=\"hr-o-note\" maxlength=\"200\" value=\"" + Ui.Attr(o.Note) + "\" autocomplete=\"off\"></label></div>");
            sb.Append("<div id=\"hr-clash\" class=\"hr-clash\">");
            if (o.StaffId != string.Empty)
            {
                sb.Append(ClashHtml(c, o.StaffName, await Inside(o.StaffId, o.FromDate, o.ToDate)));
            }
            sb.Append("</div><em class=\"hr-err hr-merr\" id=\"hr-e-off\"></em><div class=\"hr-mfoot\"><button type=\"button\" class=\"hr-btn hr-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"hr-btn hr-btn-accent\" id=\"hr-o-go\">" + (id == string.Empty ? "Add time off" : "Save") + "</button></div></form>");
            response.SetElementContents("hr-mbody", sb.ToString());
            response.ExecuteScript(Me + ".offShown();");
            return response;
        }

        public async Task<ApiResponse> OffCheck()
        {
            ApiResponse response = new ApiResponse();
            var (c, s) = await OffGuard(response);
            if (c == null)
            {
                return response;
            }
            string sid = CleanId(GetDataValue("offstaff"));
            DateTime? from = Ui.ParseDay(GetDataValue("ofrom"));
            DateTime? to = Ui.ParseDay(GetDataValue("oto"));
            HrPerson? p = sid == string.Empty ? null : (await Everyone(sid)).FirstOrDefault(x => x.StaffId == sid);
            if (p == null || from == null || to == null || to < from || (to.Value - from.Value).TotalDays > 366)
            {
                response.SetElementContents("hr-clash", string.Empty);
                return response;
            }
            response.SetElementContents("hr-clash", ClashHtml(c, p.StaffName, await Inside(p.StaffId, from.Value, to.Value)));
            return response;
        }

        public async Task<ApiResponse> Clashes()
        {
            ApiResponse response = new ApiResponse();
            var (c, s) = await OffGuard(response);
            if (c == null)
            {
                return response;
            }
            HrOff? o = await OneOff(CleanId(GetDataValue("offid")));
            if (o == null)
            {
                Toast(response, "This time off was not found.");
                return response;
            }
            response.SetElementContents("hr-mbody", "<h2>" + Ui.Enc(o.StaffName) + " · " + Ui.Enc(Dates(o.FromDate, o.ToDate)) + "</h2>" + ClashHtml(c, o.StaffName, await Inside(o.StaffId, o.FromDate, o.ToDate)) + "<div class=\"hr-mfoot\"><button type=\"button\" class=\"hr-btn hr-btn-accent\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            response.ExecuteScript(Me + ".shown('');");
            return response;
        }

        public async Task<ApiResponse> OffSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, s) = await OffGuard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("offid"));
            bool add = id == string.Empty;
            HrOff? old = null;
            if (add)
            {
                id = Hex(GetDataValue("newid"));
                if (id == string.Empty)
                {
                    response.SetElementContents("hr-e-off", "Something went wrong. Close the form and try again.");
                    return response;
                }
            }
            else
            {
                old = await OneOff(id);
                if (old == null)
                {
                    response.SetElementContents("hr-e-off", "This time off was not found. It may have been deleted.");
                    return response;
                }
            }
            DateTime today = App.Now(s).Date;
            string sid = CleanId(GetDataValue("offstaff"));
            DateTime? from = Ui.ParseDay(GetDataValue("ofrom"));
            DateTime? to = Ui.ParseDay(GetDataValue("oto"));
            string note = Ui.OneLine(GetDataValue("onote"), 200);
            HrPerson? p = sid == string.Empty ? null : (await Everyone(old?.StaffId ?? string.Empty)).FirstOrDefault(x => x.StaffId == sid);
            string ew = p == null ? "Pick who is away." : string.Empty;
            string ef = from == null ? "Pick the first day." : from < today.AddDays(-366) ? "Pick a day within the last year." : string.Empty;
            string et = to == null ? "Pick the last day." : from != null && to < from ? "The last day can't be before the first day." : from != null && (to.Value - from.Value).TotalDays > 365 ? "Time off can be at most a year. Add it in parts." : string.Empty;
            if (ew == string.Empty && ef == string.Empty && et == string.Empty)
            {
                var (ov, overr) = await Helper.GetListT<HrOff>("SELECT TIMEOFF_ID AS TimeoffId, STAFF_ID AS StaffId, N'' AS StaffName, N'' AS Photo, FROM_DATE AS FromDate, TO_DATE AS ToDate, NOTE AS Note, 0 AS Clash FROM TIMEOFF WHERE STAFF_ID = @sid AND TIMEOFF_ID <> @id AND FROM_DATE <= @to AND TO_DATE >= @from", new List<SqlParameter> { App.P("sid", sid, 50), App.P("id", id, 50), PDay("from", from!.Value), PDay("to", to!.Value) });
                HrOff? hit = overr == string.Empty && ov != null ? ov.FirstOrDefault() : null;
                if (hit != null)
                {
                    et = "This overlaps their time off " + Dates(hit.FromDate, hit.ToDate) + ". Change that one instead.";
                }
            }
            response.SetElementContents("hr-e-owho", ew);
            response.SetElementContents("hr-e-ofrom", ef);
            response.SetElementContents("hr-e-oto", Ui.Enc(et));
            response.SetElementContents("hr-e-off", string.Empty);
            if (ew != string.Empty || ef != string.Empty || et != string.Empty)
            {
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("id", id, 50), App.P("sid", sid, 50), PDay("from", from!.Value), PDay("to", to!.Value), App.P("note", note, 200) };
            string sql = add
                ? "IF NOT EXISTS (SELECT 1 FROM TIMEOFF WHERE TIMEOFF_ID = @id) INSERT INTO TIMEOFF (TIMEOFF_ID, STAFF_ID, FROM_DATE, TO_DATE, NOTE) VALUES (@id, @sid, @from, @to, @note)"
                : "UPDATE TIMEOFF SET STAFF_ID = @sid, FROM_DATE = @from, TO_DATE = @to, NOTE = @note, SYSDTE = GETDATE() WHERE TIMEOFF_ID = @id; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";
            string result = await Helper.PutData(new List<string> { sql }, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("hr-e-off", result.Contains("STATECHANGED") ? "This time off was deleted by someone else." : "The time off could not be saved. Try again.");
                return response;
            }
            string label = p!.StaffName + " · " + Dates(from.Value, to.Value) + (note != string.Empty ? " · " + note : string.Empty);
            await App.Log(c.U.StaffId, add ? "add time off" : "edit time off", old != null && (old.FromDate != from.Value || old.ToDate != to.Value || old.StaffId != sid) ? old.StaffName + " · " + Dates(old.FromDate, old.ToDate) + " → " + label : label, true);
            List<HrAppt> clash = await Inside(sid, from.Value, to.Value);
            await Draw(response, c, "off", clash.Count == 0 ? "Time off saved." : string.Empty);
            if (clash.Count > 0)
            {
                response.SetElementContents("hr-mbody", "<h2>Time off saved</h2>" + ClashHtml(c, p.StaffName, clash) + "<div class=\"hr-mfoot\"><button type=\"button\" class=\"hr-btn hr-btn-accent\" onclick=\"" + Me + ".closeModal()\">Done</button></div>");
                response.ExecuteScript(Me + ".shown('');");
            }
            else
            {
                response.ExecuteScript(Me + ".closeModal();");
            }
            return response;
        }

        public async Task<ApiResponse> OffDelete()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, s) = await OffGuard(response);
            if (c == null)
            {
                return response;
            }
            HrOff? o = await OneOff(CleanId(GetDataValue("offid")));
            response.ExecuteScript(Me + ".closeModal();");
            if (o == null)
            {
                await Draw(response, c, "off", "This time off was already deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "DELETE FROM TIMEOFF WHERE TIMEOFF_ID = @id" }, new List<SqlParameter> { App.P("id", o.TimeoffId, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, "The time off could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete time off", o.StaffName + " · " + Dates(o.FromDate, o.ToDate), true);
            await Draw(response, c, "off", "Time off deleted.");
            return response;
        }

        private static async Task<int> Booked(DateTime day)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT COUNT(*) FROM APPOINTMENT WHERE START_AT >= @d0 AND START_AT < @d1 AND STATUS IN (N'booked', N'confirmed')", new List<SqlParameter> { App.PDate("d0", day.Date), App.PDate("d1", day.Date.AddDays(1)) });
            return emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        private static string BookedWarn(Ctx c, DateTime day, int n)
        {
            if (n == 0)
            {
                return "<p class=\"hr-okline\">Nothing is booked on " + Ui.Enc(Ui.LongDay(day)) + ".</p>";
            }
            return "<div class=\"hr-warn\"><b>" + Ui.Plural(n, "appointment is", "appointments are") + " booked on " + Ui.Enc(Ui.LongDay(day)) + ".</b> Adding the holiday does not cancel or move them." + (c.Can("appts", 'V') ? " <a class=\"hr-alink\" href=\"AdminAppts?d=" + Ui.Ymd(day) + "\" target=\"_blank\" rel=\"noopener\">See that day's appointments</a>" : string.Empty) + "</div>";
        }

        private async Task<(Ctx? C, Dictionary<string, string> S)> HolGuard(ApiResponse response)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, new Dictionary<string, string>());
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "holiday", "not allowed", false);
                Toast(response, "You can't change holidays.");
                return (null, new Dictionary<string, string>());
            }
            return (c, await App.Settings());
        }

        public async Task<ApiResponse> HolCheck()
        {
            ApiResponse response = new ApiResponse();
            var (c, s) = await HolGuard(response);
            if (c == null)
            {
                return response;
            }
            DateTime? day = Ui.ParseDay(GetDataValue("hday"));
            if (day == null || day < App.Now(s).Date)
            {
                response.SetElementContents("hr-h-warn", string.Empty);
                return response;
            }
            response.SetElementContents("hr-h-warn", BookedWarn(c, day.Value, await Booked(day.Value)));
            return response;
        }

        public async Task<ApiResponse> HolSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, s) = await HolGuard(response);
            if (c == null)
            {
                return response;
            }
            DateTime? day = Ui.ParseDay(GetDataValue("hday"));
            string name = Ui.OneLine(GetDataValue("hname"), 60);
            DateTime today = App.Now(s).Date;
            string ed = day == null ? "Pick the day." : day < today ? "Pick today or a later day." : day > today.AddYears(3) ? "Pick a day within the next three years." : string.Empty;
            string en = name == string.Empty ? "Give the holiday a name." : string.Empty;
            if (ed == string.Empty)
            {
                string have = await Slots.Holiday(day!.Value);
                if (have != string.Empty)
                {
                    ed = "That day is already a holiday: " + have + ".";
                }
            }
            response.SetElementContents("hr-e-hdate", Ui.Enc(ed));
            response.SetElementContents("hr-e-hname", en);
            if (ed != string.Empty || en != string.Empty)
            {
                return response;
            }
            string result = await Helper.PutData(new List<string> { "IF NOT EXISTS (SELECT 1 FROM HOLIDAY WHERE HOLIDAY_DATE = @d) INSERT INTO HOLIDAY (HOLIDAY_DATE, HOLIDAY_NAME) VALUES (@d, @name)" }, new List<SqlParameter> { PDay("d", day!.Value), App.P("name", name, 60) });
            if (Helper.IsError(result))
            {
                response.SetElementContents("hr-e-hdate", "The holiday could not be saved. Try again.");
                return response;
            }
            int n = await Booked(day.Value);
            await App.Log(c.U.StaffId, "add holiday", Ui.Ymd(day.Value) + " · " + name + (n > 0 ? " · " + Ui.Plural(n, "appointment", "appointments") + " booked" : string.Empty), true);
            await Draw(response, c, "hol", n > 0 ? "Holiday added. " + Ui.Plural(n, "appointment is", "appointments are") + " still booked that day." : "Holiday added.");
            return response;
        }

        public async Task<ApiResponse> HolDelete()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, s) = await HolGuard(response);
            if (c == null)
            {
                return response;
            }
            DateTime? day = Ui.ParseDay(GetDataValue("hday"));
            response.ExecuteScript(Me + ".closeModal();");
            string have = day == null ? string.Empty : await Slots.Holiday(day.Value);
            if (day == null || have == string.Empty)
            {
                await Draw(response, c, "hol", "This holiday was already deleted.");
                return response;
            }
            if (day.Value.Date < App.Now(s).Date)
            {
                Toast(response, "Past holidays stay for the record.");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "DELETE FROM HOLIDAY WHERE HOLIDAY_DATE = @d" }, new List<SqlParameter> { PDay("d", day.Value) });
            if (Helper.IsError(result))
            {
                Toast(response, "The holiday could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete holiday", Ui.Ymd(day.Value) + " · " + have, true);
            await Draw(response, c, "hol", "Holiday deleted. The clinic is open that day again.");
            return response;
        }
    }
}
