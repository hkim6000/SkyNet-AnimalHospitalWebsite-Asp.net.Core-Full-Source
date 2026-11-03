using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminAppts : WebPage
    {
        private const int PageSize = 40;
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private static readonly string[] StatusKeys = { "booked", "confirmed", "checkedin", "noshow", "cancelled" };
        private const string PhoneDigits = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), N'+', N'')";
        private const string RowCols = "A.APPT_ID AS ApptId, A.APPT_CODE AS ApptCode, A.START_AT AS StartAt, A.END_AT AS EndAt, A.MINUTES AS Minutes, A.PROC_AT AS ProcAt, A.PROC_MINUTES AS ProcMinutes, A.PICKUP_AT AS PickupAt, A.STATUS AS Status, A.SOURCE AS Source, A.EST_PRICE AS EstPrice, A.VISIT_TYPE AS VisitType, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.BOOKTYPE_ID AS BooktypeId, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, A.PET_ID AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(X.SPECIES_NAME, ISNULL(P.SPECIES_KEY, N'')) AS SpeciesName, ISNULL(P.SIZE, N'') AS Size, ISNULL(P.ALERT, N'') AS PetAlert, A.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS Phone";
        private const string RowFrom = " FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN SPECIES X ON X.SPECIES_KEY = P.SPECIES_KEY";
        private const string PetCols = "P.PET_ID AS PetId, P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(X.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SIZE AS Size, P.ALERT AS Alert, P.MICROCHIP AS Microchip, P.CLIENT_ID AS ClientId, C.FIRST_NAME + N' ' + C.LAST_NAME AS OwnerName, C.PHONE AS Phone, C.ALERT AS ClientAlert";
        private const string PetFrom = " FROM PET P JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES X ON X.SPECIES_KEY = P.SPECIES_KEY WHERE P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL AND C.ACTIVE = 1";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Appointments | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            DateTime? qd = Ui.ParseDay(QueryValue("d"));
            TimeSpan? qt = Ui.ParseHm(QueryValue("t"));
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ap-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ap"))
                .Replace("{plhd_qa}", Ui.Attr(Id(QueryValue("a"))))
                .Replace("{plhd_qp}", Ui.Attr(Id(QueryValue("p"))))
                .Replace("{plhd_qs}", Ui.Attr(Id(QueryValue("s"))))
                .Replace("{plhd_qd}", qd.HasValue ? Ui.Ymd(qd.Value) : string.Empty)
                .Replace("{plhd_qt}", qt.HasValue ? Hm24(qt.Value) : string.Empty);
        }

        private string V(string key)
        {
            return (GetDataValue(key) ?? string.Empty).Trim();
        }

        private static string Id(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_\\-]{1,50}$") ? t : string.Empty;
        }

        private static string Hm24(TimeSpan t)
        {
            return ((int)t.TotalHours).ToString("00", Ui.Inv) + ":" + t.Minutes.ToString("00", Ui.Inv);
        }

        private static string Clock(string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t.HasValue ? Ui.Hm(DateTime.Today.Add(t.Value)) : Ui.Enc(hm);
        }

        private static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv);
        }

        private static string Like(string v)
        {
            return "%" + v.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminApptsJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("ap-menu", Auth.Menu("ap", "AdminApptsJs", "AdminAppts", c));
            response.SetElementContents("ap-who", Auth.Who("ap", c));
            response.ExecuteScript("AdminApptsJs.secs();");
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminApptsJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"ap-ptop\"><div><h1>Appointments</h1></div></div><div class=\"ap-panel ap-deny\"><h2>You don't have access to this page</h2><p>Ask a manager to give you the Appointments right in Staff and permissions.</p><a class=\"ap-btn ap-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminApptsJs.out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Shell(response, c);
            if (!c.Can("appts", 'V'))
            {
                response.SetElementContents("ap-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            DateTime from = Ui.ParseDay(V("listday")) ?? today;
            string bookStaff = Id(V("bookstaff"));
            string bookPet = Id(V("bookpet"));
            string bookTime = Ui.ParseHm(V("booktime")).HasValue ? Hm24(Ui.ParseHm(V("booktime"))!.Value) : string.Empty;
            bool wizard = c.Can("appts", 'A') && (bookPet != string.Empty || bookStaff != string.Empty || bookTime != string.Empty);
            if (wizard && from < today)
            {
                from = today;
            }
            response.SetElementContents("ap-main", await Frame(c, from));
            response.ExecuteScript("AdminApptsJs.ready('" + Ui.Ymd(from) + "', '" + Ui.Ymd(from.AddDays(6)) + "');");
            string open = Id(V("openappt"));
            if (open != string.Empty)
            {
                response.SetElementContents("ap-mbody", await DetailHtml(c, s, open, string.Empty));
                response.ExecuteScript("AdminApptsJs.shown();");
            }
            else if (wizard)
            {
                DateTime day = Ui.ParseDay(V("listday")) ?? today;
                await OpenWizard(response, c, s, bookPet, bookStaff, day < today ? today : day, bookTime);
            }
            return response;
        }

        private async Task<string> Frame(Ctx c, DateTime from)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-ptop\"><div><h1>Appointments</h1><p>Book, find, change and cancel visits. Boarding stays have their own board.</p></div>");
            if (c.Can("appts", 'A'))
            {
                sb.Append("<div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-accent\" onclick=\"AdminApptsJs.newBooking()\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M12 5v14M5 12h14\"/></svg>New booking</button></div>");
            }
            sb.Append("</div>");
            sb.Append("<form class=\"ap-panel ap-filt\" onsubmit=\"return AdminApptsJs.go(1)\" novalidate>");
            sb.Append("<label class=\"ap-field\"><span>From</span><input type=\"date\" id=\"ap-from\" value=\"" + Ui.Ymd(from) + "\" onchange=\"AdminApptsJs.go(1)\"></label>");
            sb.Append("<label class=\"ap-field\"><span>To</span><input type=\"date\" id=\"ap-to\" value=\"" + Ui.Ymd(from.AddDays(6)) + "\" onchange=\"AdminApptsJs.go(1)\"></label>");
            List<(string, string)> st = new List<(string, string)> { (string.Empty, "Any status") };
            st.AddRange(StatusKeys.Select(k => (k, StatusName(k))));
            sb.Append("<label class=\"ap-field\"><span>Status</span><select id=\"ap-status\" onchange=\"AdminApptsJs.go(1)\">" + Ui.Opts(st, string.Empty) + "</select></label>");
            if (!c.U.OwnOnly)
            {
                List<(string, string)> people = new List<(string, string)> { (string.Empty, "Anyone") };
                people.AddRange((await Flow.Providers()).Select(p => (p.StaffId, p.StaffName)));
                sb.Append("<label class=\"ap-field\"><span>With</span><select id=\"ap-staff\" onchange=\"AdminApptsJs.go(1)\">" + Ui.Opts(people, string.Empty) + "</select></label>");
            }
            var (books, berr) = await Helper.GetListT<ApBook>("SELECT BOOKTYPE_ID AS BooktypeId, BOOK_NAME AS BookName, VISIT_TYPE AS VisitType FROM BOOKTYPE WHERE VISIT_TYPE <> N'boarding' ORDER BY BOOK_ORDER, BOOK_NAME");
            List<(string, string)> kinds = new List<(string, string)> { (string.Empty, "Any kind") };
            if (berr == string.Empty && books != null)
            {
                kinds.AddRange(books.Select(b => (b.BooktypeId, b.BookName)));
            }
            sb.Append("<label class=\"ap-field\"><span>Kind of visit</span><select id=\"ap-book\" onchange=\"AdminApptsJs.go(1)\">" + Ui.Opts(kinds, string.Empty) + "</select></label>");
            sb.Append("<label class=\"ap-field ap-fsearch\"><span>Search</span><input type=\"search\" id=\"ap-term\" maxlength=\"60\" placeholder=\"Pet, owner, phone or code\" autocomplete=\"off\"></label>");
            sb.Append("<div class=\"ap-fbtns\"><button type=\"submit\" class=\"ap-btn ap-btn-sm\">Show</button><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.week()\">Next 7 days</button></div>");
            sb.Append("</form>");
            sb.Append("<div id=\"ap-list\">" + await ListHtml(c, from, from.AddDays(6), string.Empty, string.Empty, string.Empty, string.Empty, 1) + "</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("appts", 'V'))
            {
                response.SetElementContents("ap-main", Denied());
                return response;
            }
            await RefreshList(response, c);
            return response;
        }

        private async Task RefreshList(ApiResponse response, Ctx c)
        {
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            DateTime from = Ui.ParseDay(V("dfrom")) ?? today;
            DateTime to = Ui.ParseDay(V("dto")) ?? from.AddDays(6);
            if (to < from)
            {
                to = from;
            }
            if ((to - from).TotalDays > 92)
            {
                to = from.AddDays(92);
            }
            string status = StatusKeys.Contains(V("fstatus")) ? V("fstatus") : string.Empty;
            int.TryParse(V("pg"), out int page);
            response.SetElementContents("ap-list", await ListHtml(c, from, to, status, Id(V("fstaff")), Id(V("fbook")), Ui.OneLine(V("term"), 60), Math.Max(1, page)));
            response.ExecuteScript("AdminApptsJs.listed('" + Ui.Ymd(from) + "', '" + Ui.Ymd(to) + "');");
        }

        private static string StatusName(string k)
        {
            return k switch { "booked" => "Booked", "confirmed" => "Confirmed", "checkedin" => "Checked in", "noshow" => "No-show", "cancelled" => "Cancelled", _ => k };
        }

        private static string SourceName(string k)
        {
            return k switch { "web" => "Website", "phone" => "Phone", "desk" => "Desk", _ => k == string.Empty ? "Desk" : k };
        }

        private static string Chip(string status)
        {
            return "<span class=\"ap-st ap-st-" + Ui.Attr(status) + "\">" + Ui.Enc(StatusName(status)) + "</span>";
        }

        private static string SizeName(string size)
        {
            return size switch { "small" => "small", "medium" => "medium", "large" => "large", _ => string.Empty };
        }

        private static string DayHead(DateTime d, DateTime today)
        {
            string rel = d == today ? "Today · " : d == today.AddDays(1) ? "Tomorrow · " : d == today.AddDays(-1) ? "Yesterday · " : string.Empty;
            return rel + Ui.LongDay(d);
        }

        private static async Task<string> ListHtml(Ctx c, DateTime from, DateTime to, string status, string staff, string book, string term, int page)
        {
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            string digits = Ui.Digits(term);
            string where = " WHERE A.VISIT_TYPE <> N'boarding' AND A.START_AT >= @d0 AND A.START_AT < @d1 AND (@st = N'' OR A.STATUS = @st) AND (@sid = N'' OR A.STAFF_ID = @sid) AND (@bt = N'' OR A.BOOKTYPE_ID = @bt)"
                + " AND (@q = N'' OR P.PET_NAME LIKE @ql OR C.FIRST_NAME LIKE @ql OR C.LAST_NAME LIKE @ql OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @ql OR A.APPT_CODE LIKE @ql OR (@qd <> N'' AND " + PhoneDigits + " LIKE @qdl))";
            string sid = c.U.OwnOnly ? c.U.StaffId : staff;
            List<SqlParameter> Ps() => new List<SqlParameter>
            {
                App.PDate("d0", from), App.PDate("d1", to.AddDays(1)), App.P("st", status, 20), App.P("sid", sid, 50), App.P("bt", book, 50),
                App.P("q", term, 60), App.P("ql", Like(term), 200), App.P("qd", digits.Length >= 3 ? digits : string.Empty, 30), App.P("qdl", Like(digits), 60)
            };
            var (cdt, cerr) = await Helper.GetDataTable("SELECT COUNT(*)" + RowFrom + where, Ps());
            if (cerr != string.Empty)
            {
                return "<div class=\"ap-panel ap-empty\"><p>The appointments could not be loaded. Try again.</p></div>";
            }
            int total = Convert.ToInt32(cdt.Rows[0][0]);
            string range = from == to ? Ui.Day(from) : Ui.Day(from, from.Year != to.Year) + " – " + Ui.Day(to);
            StringBuilder sb = new StringBuilder();
            sb.Append("<p class=\"ap-count\"><b>" + Ui.Plural(total, "appointment", "appointments") + "</b> &middot; " + Ui.Enc(range) + (c.U.OwnOnly ? " &middot; yours only" : string.Empty) + "</p>");
            if (total == 0)
            {
                sb.Append("<div class=\"ap-panel ap-empty\"><p>No appointments match. Try other dates or clear the search.</p></div>");
                return sb.ToString();
            }
            int pg = Ui.PageOf(page.ToString(Ui.Inv), PageSize, total);
            List<SqlParameter> ps = Ps();
            ps.Add(App.PInt("off", (pg - 1) * PageSize));
            ps.Add(App.PInt("take", PageSize));
            var (rows, rerr) = await Helper.GetListT<ApRow>("SELECT " + RowCols + RowFrom + where + " ORDER BY A.START_AT, ISNULL(A.PROC_AT, A.START_AT), P.PET_NAME, A.APPT_ID OFFSET @off ROWS FETCH NEXT @take ROWS ONLY", ps);
            if (rerr != string.Empty || rows == null)
            {
                return "<div class=\"ap-panel ap-empty\"><p>The appointments could not be loaded. Try again.</p></div>";
            }
            sb.Append("<div class=\"ap-table\"><div class=\"ap-thead\" aria-hidden=\"true\"><span>Time</span><span>Pet</span><span>Owner</span><span>Visit</span><span>With</span><span>Status</span><span>Booked</span><span class=\"ap-r\">Estimate</span></div>");
            foreach (IGrouping<DateTime, ApRow> g in rows.GroupBy(r => r.StartAt.Date))
            {
                sb.Append("<h2 class=\"ap-day" + (g.Key == today ? " ap-today" : string.Empty) + "\"><span>" + Ui.Enc(DayHead(g.Key, today)) + "</span><small>" + Ui.Plural(g.Count(), "visit", "visits") + "</small></h2>");
                foreach (ApRow r in g)
                {
                    sb.Append(RowHtml(r));
                }
            }
            sb.Append("</div>");
            sb.Append(Ui.Pager("ap", pg, PageSize, total, "appointment", "appointments", "AdminApptsJs.go"));
            return sb.ToString();
        }

        private static string RowHtml(ApRow r)
        {
            bool drop = r.ProcAt.HasValue;
            bool dim = r.Status == "cancelled" || r.Status == "noshow";
            StringBuilder sb = new StringBuilder();
            sb.Append("<a class=\"ap-row" + (dim ? " ap-dim" : string.Empty) + "\" href=\"AdminAppts?a=" + Ui.Attr(r.ApptId) + "\" onclick=\"return AdminApptsJs.open('" + Ui.Js(r.ApptId) + "')\">");
            if (drop)
            {
                sb.Append("<span class=\"ap-c-time\"><b>Drop-off " + Ui.Hm(r.StartAt) + "</b><small>Surgery " + Ui.Hm(r.ProcAt!.Value) + "</small></span>");
            }
            else
            {
                sb.Append("<span class=\"ap-c-time\"><b>" + Ui.Hm(r.StartAt) + "</b><small>" + r.Minutes.ToString(Ui.Inv) + " min</small></span>");
            }
            string sz = SizeName(r.Size);
            sb.Append("<span class=\"ap-c-pet\"><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName + (sz != string.Empty ? " · " + sz : string.Empty)) + "</small>" + (r.PetAlert != string.Empty ? "<em class=\"ap-alert\" title=\"" + Ui.Attr(r.PetAlert) + "\">" + Ui.Enc(r.PetAlert) + "</em>" : string.Empty) + "</span>");
            sb.Append("<span class=\"ap-c-own\"><b>" + Ui.Enc(r.OwnerName) + "</b><small>" + Ui.Enc(r.Phone) + "</small></span>");
            sb.Append("<span class=\"ap-c-type\">" + Ui.Enc(r.BookName) + "<small class=\"ap-code\">" + Ui.Enc(r.ApptCode) + "</small></span>");
            sb.Append("<span class=\"ap-c-staff\">" + Ui.Enc(r.StaffName == string.Empty ? "Anyone" : r.StaffName) + "</span>");
            sb.Append("<span class=\"ap-c-st\">" + Chip(r.Status) + "</span>");
            sb.Append("<span class=\"ap-c-src\">" + Ui.Enc(SourceName(r.Source)) + "</span>");
            sb.Append("<span class=\"ap-c-price\">" + (r.EstPrice > 0 ? Ui.Money(r.EstPrice) : "&ndash;") + "</span>");
            sb.Append("</a>");
            return sb.ToString();
        }

        private static async Task<ApDetail?> Detail(string apptId)
        {
            var (row, emsg) = await Helper.GetOneT<ApDetail>("SELECT A.APPT_ID AS ApptId, A.APPT_CODE AS ApptCode, A.START_AT AS StartAt, A.END_AT AS EndAt, A.MINUTES AS Minutes, A.PROC_AT AS ProcAt, A.PROC_MINUTES AS ProcMinutes, A.PICKUP_AT AS PickupAt, A.STATUS AS Status, A.SOURCE AS Source, A.EST_PRICE AS EstPrice, A.VISIT_TYPE AS VisitType, A.VISIT_ID AS VisitId, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.BOOKTYPE_ID AS BooktypeId, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, ISNULL(T.TYPE_NAME, N'') AS RoomType, A.NOTE AS Note, A.DESK_NOTE AS DeskNote, A.CANCEL_REASON AS CancelReason, A.NEW_CLIENT AS NewClient, A.CREATED_UTC AS CreatedUtc, A.CREATED_BY AS CreatedBy, ISNULL(CB.STAFF_NAME, N'') AS CreatedName, A.PET_ID AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(P.SPECIES_KEY, N'') AS SpeciesKey, ISNULL(X.SPECIES_NAME, ISNULL(P.SPECIES_KEY, N'')) AS SpeciesName, ISNULL(P.BREED, N'') AS Breed, ISNULL(P.SEX, N'') AS Sex, ISNULL(P.SIZE, N'') AS Size, ISNULL(P.ALERT, N'') AS PetAlert, A.CLIENT_ID AS ClientId, ISNULL(C.CLIENT_NO, N'') AS ClientNo, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.PHONE2, N'') AS Phone2, ISNULL(C.EMAIL, N'') AS Email, ISNULL(C.CONTACT_PREF, N'') AS ContactPref, ISNULL(C.ALERT, N'') AS ClientAlert FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID LEFT JOIN STAFF CB ON CB.STAFF_ID = A.CREATED_BY LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN ROOMTYPE T ON T.ROOMTYPE_ID = A.ROOMTYPE_ID LEFT JOIN SPECIES X ON X.SPECIES_KEY = P.SPECIES_KEY WHERE A.APPT_ID = @aid AND A.VISIT_TYPE <> N'boarding'", new List<SqlParameter> { App.P("aid", apptId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static string Info(string label, string html)
        {
            return "<div class=\"ap-kv\"><span>" + Ui.Enc(label) + "</span><div>" + html + "</div></div>";
        }

        private static async Task<string> DetailHtml(Ctx c, Dictionary<string, string> s, string apptId, string notice)
        {
            ApDetail? a = await Detail(apptId);
            if (a == null)
            {
                return "<h2>Appointment not found</h2><p class=\"ap-muted\">It may have been removed, or the link is wrong.</p><div class=\"ap-dacts\"><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminApptsJs.closeModal()\">Close</button></div>";
            }
            if (!c.Mine(a.StaffId))
            {
                return "<h2>Not one of yours</h2><p class=\"ap-muted\">This appointment is with another staff member. You can only open your own appointments.</p><div class=\"ap-dacts\"><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminApptsJs.closeModal()\">Close</button></div>";
            }
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            bool open = a.Status == "booked" || a.Status == "confirmed";
            bool canE = c.Can("appts", 'E');
            bool drop = a.ProcAt.HasValue;
            string jid = Ui.Js(a.ApptId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-dhead\">" + Chip(a.Status) + "<span class=\"ap-code\">" + Ui.Enc(a.ApptCode) + "</span>" + (a.NewClient ? "<span class=\"ap-tag\">New client</span>" : string.Empty) + "</div>");
            sb.Append("<h2>" + Ui.Enc(a.PetName) + " &middot; " + Ui.Enc(a.BookName) + "</h2>");
            if (notice != string.Empty)
            {
                sb.Append("<div class=\"ap-note ap-note-ok\">" + notice + "</div>");
            }
            if (a.PetAlert != string.Empty || a.ClientAlert != string.Empty)
            {
                sb.Append("<div class=\"ap-note ap-note-warn\">" + (a.PetAlert != string.Empty ? "<b>Pet alert:</b> " + Ui.Enc(a.PetAlert) : string.Empty) + (a.PetAlert != string.Empty && a.ClientAlert != string.Empty ? "<br>" : string.Empty) + (a.ClientAlert != string.Empty ? "<b>Client alert:</b> " + Ui.Enc(a.ClientAlert) : string.Empty) + "</div>");
            }
            sb.Append("<div class=\"ap-dgrid\">");
            StringBuilder when = new StringBuilder();
            when.Append(Info("Day", "<b>" + Ui.Enc(Ui.LongDay(a.StartAt)) + "</b>"));
            if (drop)
            {
                when.Append(Info("Drop-off", Ui.Hm(a.StartAt)));
                when.Append(Info("Surgery", Ui.Hm(a.ProcAt!.Value) + " &middot; " + a.ProcMinutes.ToString(Ui.Inv) + " min"));
                when.Append(Info("Pick-up", a.PickupAt.HasValue ? Ui.Hm(a.PickupAt.Value) : "&ndash;"));
            }
            else
            {
                when.Append(Info("Time", Ui.Hm(a.StartAt) + " &ndash; " + Ui.Hm(a.EndAt) + " &middot; " + a.Minutes.ToString(Ui.Inv) + " min"));
            }
            when.Append(Info("With", Ui.Enc(a.StaffName == string.Empty ? "Anyone" : a.StaffName)));
            if (a.RoomType != string.Empty)
            {
                when.Append(Info("Room", Ui.Enc(a.RoomType)));
            }
            sb.Append("<section class=\"ap-dbox\"><h3>When</h3>" + when + "</section>");
            StringBuilder pet = new StringBuilder();
            string petName = c.Can("pets", 'V') ? "<a class=\"ap-link\" href=\"AdminPets?p=" + Ui.Attr(a.PetId) + "\">" + Ui.Enc(a.PetName) + "</a>" : Ui.Enc(a.PetName);
            pet.Append(Info("Name", "<b>" + petName + "</b>"));
            List<string> bits = new List<string> { a.SpeciesName };
            if (a.Breed != string.Empty)
            {
                bits.Add(a.Breed);
            }
            if (SizeName(a.Size) != string.Empty)
            {
                bits.Add(SizeName(a.Size));
            }
            if (a.Sex == "M" || a.Sex == "F")
            {
                bits.Add(a.Sex == "M" ? "male" : "female");
            }
            pet.Append(Info("About", Ui.Enc(string.Join(" · ", bits.Where(b => b != string.Empty)))));
            sb.Append("<section class=\"ap-dbox\"><h3>Pet</h3>" + pet + "</section>");
            StringBuilder own = new StringBuilder();
            string ownName = c.Can("clients", 'V') ? "<a class=\"ap-link\" href=\"AdminClients?c=" + Ui.Attr(a.ClientId) + "\">" + Ui.Enc(a.OwnerName) + "</a>" : Ui.Enc(a.OwnerName);
            own.Append(Info("Name", "<b>" + ownName + "</b>" + (a.ClientNo != string.Empty ? " <small class=\"ap-muted\">" + Ui.Enc(a.ClientNo) + "</small>" : string.Empty)));
            if (a.Phone != string.Empty)
            {
                own.Append(Info("Phone", "<a class=\"ap-link\" href=\"tel:" + Ui.Attr(Ui.Digits(a.Phone)) + "\">" + Ui.Enc(a.Phone) + "</a>" + (a.Phone2 != string.Empty ? "<br><a class=\"ap-link\" href=\"tel:" + Ui.Attr(Ui.Digits(a.Phone2)) + "\">" + Ui.Enc(a.Phone2) + "</a>" : string.Empty)));
            }
            if (a.Email != string.Empty)
            {
                own.Append(Info("Email", "<a class=\"ap-link ap-break\" href=\"mailto:" + Ui.Attr(a.Email) + "\">" + Ui.Enc(a.Email) + "</a>"));
            }
            if (a.ContactPref != string.Empty)
            {
                own.Append(Info("Prefers", Ui.Enc(a.ContactPref == "email" ? "Email" : a.ContactPref == "text" ? "Text message" : "Phone call")));
            }
            sb.Append("<section class=\"ap-dbox\"><h3>Owner</h3>" + own + "</section>");
            StringBuilder bk = new StringBuilder();
            bk.Append(Info("Booked", Ui.Enc(SourceName(a.Source)) + " &middot; " + Ui.Time(a.CreatedUtc, "full") + (a.CreatedName != string.Empty ? "<br><small class=\"ap-muted\">by " + Ui.Enc(a.CreatedName) + "</small>" : string.Empty)));
            bk.Append(Info("Estimate", a.EstPrice > 0 ? Ui.Money(a.EstPrice) : "&ndash;"));
            if (a.VisitId != string.Empty && c.Can("visits", 'V'))
            {
                bk.Append(Info("Visit", "<a class=\"ap-link\" href=\"AdminVisit?v=" + Ui.Attr(a.VisitId) + "\">Open the visit</a>"));
            }
            sb.Append("<section class=\"ap-dbox\"><h3>Booking</h3>" + bk + "</section>");
            sb.Append("</div>");
            if (a.Note != string.Empty)
            {
                sb.Append("<h3>Note to the vet</h3><p class=\"ap-quote\">" + Ui.Enc(a.Note) + "</p>");
            }
            if (a.Status == "cancelled" && a.CancelReason != string.Empty)
            {
                sb.Append("<h3>Why it was cancelled</h3><p class=\"ap-quote\">" + Ui.Enc(a.CancelReason) + "</p>");
            }
            StringBuilder acts = new StringBuilder();
            if (open && canE)
            {
                if (a.Status == "booked")
                {
                    acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm\" onclick=\"AdminApptsJs.setStatus('" + jid + "', 'confirmed')\">Mark as confirmed</button>");
                }
                else
                {
                    acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.setStatus('" + jid + "', 'booked')\">Set back to booked</button>");
                }
                acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.resched('" + jid + "')\">Move to another time</button>");
                if (a.StartAt.Date <= today)
                {
                    acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.noShow('" + jid + "')\">No-show</button>");
                }
            }
            if (open && c.Can("appts", 'X'))
            {
                acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-danger\" onclick=\"AdminApptsJs.cancelForm(true)\">Cancel appointment</button>");
            }
            if (open && a.StartAt.Date == today && c.Can("today", 'E'))
            {
                acts.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-accent\" onclick=\"AdminApptsJs.checkIn('" + jid + "')\">Check in now</button>");
            }
            if (a.Status == "checkedin" && c.Can("today", 'V'))
            {
                acts.Append("<a class=\"ap-btn ap-btn-sm ap-btn-ghost\" href=\"AdminToday\">Open Today's board</a>");
            }
            if (acts.Length > 0)
            {
                sb.Append("<div class=\"ap-dacts\">" + acts + "</div>");
            }
            if (open && c.Can("appts", 'X'))
            {
                sb.Append("<div class=\"ap-cform\" id=\"ap-cform\" hidden><label class=\"ap-field\"><span>Why is it cancelled?</span><textarea id=\"ap-creason\" rows=\"2\" maxlength=\"200\" placeholder=\"For example: owner called, pet is feeling better\"></textarea><em class=\"ap-err\" id=\"ap-e-creason\"></em></label><div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-danger\" onclick=\"AdminApptsJs.cancelAppt('" + jid + "')\">Cancel this appointment</button><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.cancelForm(false)\">Keep it</button></div></div>");
            }
            sb.Append("<h3>Desk note</h3>");
            if (canE)
            {
                sb.Append("<div class=\"ap-dnote\"><label class=\"ap-field\"><span class=\"ap-sr\">Desk note</span><textarea id=\"ap-dnote\" rows=\"2\" maxlength=\"600\" placeholder=\"Only staff see this. For example: owner will be 10 minutes late\">" + Ui.Enc(a.DeskNote) + "</textarea></label><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.saveNote('" + jid + "')\">Save note</button></div>");
            }
            else
            {
                sb.Append("<p class=\"ap-quote\">" + (a.DeskNote != string.Empty ? Ui.Enc(a.DeskNote) : "<span class=\"ap-muted\">No desk note.</span>") + "</p>");
            }
            var (logs, lerr) = await Helper.GetListT<ApLog>("SELECT L.LOG_UTC AS LogUtc, ISNULL(S.STAFF_NAME, CASE WHEN L.STAFF_ID = N'' OR L.STAFF_ID = N'web' THEN N'Website' ELSE L.STAFF_ID END) AS StaffName, L.LOG_TEXT AS LogText FROM APPTLOG L LEFT JOIN STAFF S ON S.STAFF_ID = L.STAFF_ID WHERE L.APPT_ID = @aid ORDER BY L.LOG_UTC DESC", new List<SqlParameter> { App.P("aid", a.ApptId, 50) });
            sb.Append("<h3>History</h3>");
            if (lerr == string.Empty && logs != null && logs.Count > 0)
            {
                sb.Append("<ul class=\"ap-log\">");
                foreach (ApLog l in logs)
                {
                    sb.Append("<li><b>" + Ui.Enc(l.LogText) + "</b><small>" + Ui.Enc(l.StaffName) + " &middot; " + Ui.Time(l.LogUtc, "full") + "</small></li>");
                }
                sb.Append("</ul>");
            }
            else
            {
                sb.Append("<p class=\"ap-muted\">Nothing recorded yet.</p>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> OpenAppt()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("appts", 'V'))
            {
                response.SetElementContents("ap-main", Denied());
                return response;
            }
            response.SetElementContents("ap-mbody", await DetailHtml(c, await App.Settings(), Id(V("apptid")), string.Empty));
            response.ExecuteScript("AdminApptsJs.shown();");
            return response;
        }

        private async Task<(Ctx? C, ApDetail? A)> ForChange(ApiResponse response, char right)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null);
            }
            if (!c.Can("appts", right))
            {
                await App.Log(c.U.StaffId, "appointment change", "not allowed", false);
                Toast(response, right == 'X' ? "You don't have the right to cancel appointments." : "You don't have the right to change appointments.");
                return (null, null);
            }
            ApDetail? a = await Detail(Id(V("apptid")));
            if (a == null)
            {
                Toast(response, "This appointment was not found.");
                return (null, null);
            }
            if (!c.Mine(a.StaffId))
            {
                await App.Log(c.U.StaffId, "appointment change", "not own: " + a.ApptCode, false);
                Toast(response, "You can only change your own appointments.");
                return (null, null);
            }
            return (c, a);
        }

        private async Task Changed(ApiResponse response, Ctx c, string apptId, string notice, string toast)
        {
            response.SetElementContents("ap-mbody", await DetailHtml(c, await App.Settings(), apptId, notice));
            response.ExecuteScript("AdminApptsJs.shown();");
            await RefreshList(response, c);
            if (toast != string.Empty)
            {
                Toast(response, toast);
            }
        }

        public async Task<ApiResponse> SetStatus()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'E');
            if (c == null || a == null)
            {
                return response;
            }
            string to = V("tostatus") == "booked" ? "booked" : "confirmed";
            string r = await Flow.SetApptStatus(c, a.ApptId, to, string.Empty);
            if (r != string.Empty)
            {
                Toast(response, r);
                await Changed(response, c, a.ApptId, string.Empty, string.Empty);
                return response;
            }
            await Changed(response, c, a.ApptId, string.Empty, to == "confirmed" ? "Marked as confirmed." : "Set back to booked.");
            return response;
        }

        public async Task<ApiResponse> NoShow()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'E');
            if (c == null || a == null)
            {
                return response;
            }
            if (a.StartAt.Date > App.Now(await App.Settings()).Date)
            {
                Toast(response, "Only today's or past appointments can be marked as a no-show.");
                return response;
            }
            string r = await Flow.SetApptStatus(c, a.ApptId, "noshow", string.Empty);
            await Changed(response, c, a.ApptId, string.Empty, r != string.Empty ? r : "Marked as a no-show.");
            return response;
        }

        public async Task<ApiResponse> CancelAppt()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'X');
            if (c == null || a == null)
            {
                return response;
            }
            string why = Ui.OneLine(V("reason"), 200);
            if (why.Length < 3)
            {
                response.SetElementContents("ap-e-creason", "Write a short reason, so the team knows why.");
                return response;
            }
            string r = await Flow.SetApptStatus(c, a.ApptId, "cancelled", why);
            await Changed(response, c, a.ApptId, string.Empty, r != string.Empty ? r : "The appointment is cancelled. The time is free again.");
            return response;
        }

        public async Task<ApiResponse> CheckIn()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'E');
            if (c == null || a == null)
            {
                return response;
            }
            if (!c.Can("today", 'E'))
            {
                Toast(response, "You don't have the right to check pets in.");
                return response;
            }
            var (vid, err) = await Flow.CheckIn(c, a.ApptId, await App.Settings());
            if (err != string.Empty)
            {
                Toast(response, err);
                await Changed(response, c, a.ApptId, string.Empty, string.Empty);
                return response;
            }
            string notice = Ui.Enc(a.PetName) + " is checked in and waiting. <a class=\"ap-link\" href=\"AdminToday\">Open Today's board</a>" + (c.Can("visits", 'V') && vid != string.Empty ? " &middot; <a class=\"ap-link\" href=\"AdminVisit?v=" + Ui.Attr(vid) + "\">Open the visit</a>" : string.Empty);
            await Changed(response, c, a.ApptId, notice, "Checked in.");
            return response;
        }

        public async Task<ApiResponse> SaveNote()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'E');
            if (c == null || a == null)
            {
                return response;
            }
            string note = Ui.Clip(V("desknote"), 600);
            if (note == a.DeskNote)
            {
                Toast(response, "Nothing changed.");
                return response;
            }
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE APPOINTMENT SET DESK_NOTE = @note, SYSDTE = GETDATE() WHERE APPT_ID = @aid",
                "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), @me, @ltext)"
            }, new List<SqlParameter> { App.P("note", note, 600), App.P("aid", a.ApptId, 50), App.P("lid", App.NewId(), 50), App.P("me", c.U.StaffId, 50), App.P("ltext", note == string.Empty ? "Desk note removed" : "Desk note: " + Ui.OneLine(note, 280), 300) });
            if (Helper.IsError(r))
            {
                Toast(response, "The note could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "appointment desk note", a.PetName + " · " + a.ApptCode, true);
            await Changed(response, c, a.ApptId, string.Empty, "Desk note saved.");
            return response;
        }

        private static async Task<ApPet?> PetById(string petId)
        {
            var (row, emsg) = await Helper.GetOneT<ApPet>("SELECT " + PetCols + PetFrom + " AND P.PET_ID = @pid", new List<SqlParameter> { App.P("pid", petId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<BookRow?> BookById(string bookId)
        {
            var (row, emsg) = await Helper.GetOneT<BookRow>("SELECT " + Slots.BookCols + " FROM BOOKTYPE B WHERE B.BOOKTYPE_ID = @bt AND B.ACTIVE = 1 AND B.VISIT_TYPE <> N'boarding'", new List<SqlParameter> { App.P("bt", bookId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<ApPerson>> Eligible(Ctx c, string bookId, string species)
        {
            var (list, emsg) = await Helper.GetListT<ApPerson>("SELECT S.STAFF_ID AS StaffId, S.STAFF_NAME AS StaffName FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.BOOKTYPE_ID = @bt AND S.ACTIVE = 1 AND (NOT EXISTS (SELECT 1 FROM STAFFSPECIES Y WHERE Y.STAFF_ID = S.STAFF_ID) OR EXISTS (SELECT 1 FROM STAFFSPECIES Y WHERE Y.STAFF_ID = S.STAFF_ID AND Y.SPECIES_KEY = @sp)) ORDER BY S.STAFF_NAME", new List<SqlParameter> { App.P("bt", bookId, 50), App.P("sp", species, 20) });
            List<ApPerson> r = emsg == string.Empty && list != null ? list : new List<ApPerson>();
            return c.U.OwnOnly ? r.Where(x => x.StaffId == c.U.StaffId).ToList() : r;
        }

        private static async Task<decimal> Price(BookRow b, string size)
        {
            if (b.ItemId == string.Empty)
            {
                return 0m;
            }
            var (dt, emsg) = await Helper.GetDataTable("SELECT PRICE, PRICE_M, PRICE_L FROM ITEM WHERE ITEM_ID = @iid", new List<SqlParameter> { App.P("iid", b.ItemId, 50) });
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                return 0m;
            }
            decimal p = Convert.ToDecimal(dt.Rows[0]["PRICE"]);
            decimal m = Convert.ToDecimal(dt.Rows[0]["PRICE_M"]);
            decimal l = Convert.ToDecimal(dt.Rows[0]["PRICE_L"]);
            if (size == "large" && l > 0)
            {
                return l;
            }
            if (size == "medium" && m > 0)
            {
                return m;
            }
            return p;
        }

        private static string WizardFrame(string mode)
        {
            bool move = mode == "move";
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (move ? "Move appointment" : "New booking") + "</h2>");
            sb.Append("<div class=\"ap-wz\">");
            sb.Append("<section class=\"ap-ws\"><h3><i>1</i>Pet</h3><div id=\"ap-w-pet\"></div></section>");
            sb.Append("<section class=\"ap-ws\" id=\"ap-w2\" hidden><h3><i>2</i>Kind of visit</h3><div id=\"ap-w-type\"></div></section>");
            sb.Append("<section class=\"ap-ws\" id=\"ap-w3\" hidden><h3><i>3</i>Who</h3><div id=\"ap-w-staff\"></div></section>");
            sb.Append("<section class=\"ap-ws\" id=\"ap-w4\" hidden><h3><i>4</i>Day and time</h3><div id=\"ap-w-when\"></div></section>");
            sb.Append("<section class=\"ap-ws\" id=\"ap-w5\" hidden><h3><i>5</i>" + (move ? "Confirm the new time" : "Note and save") + "</h3><p class=\"ap-wsum\" id=\"ap-w-sum\"></p>");
            if (!move)
            {
                sb.Append("<div class=\"ap-wform\"><label class=\"ap-field\"><span>Note to the vet (optional)</span><textarea id=\"ap-w-note\" rows=\"2\" maxlength=\"600\" placeholder=\"What the owner told you, for example: limping on the back left leg since Sunday\"></textarea></label>");
                sb.Append("<label class=\"ap-field\"><span>Booked by</span><select id=\"ap-w-src\"><option value=\"phone\">Phone call</option><option value=\"desk\">At the desk</option></select></label></div>");
            }
            sb.Append("<em class=\"ap-err\" id=\"ap-e-wsave\"></em><div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-accent\" id=\"ap-w-go\" onclick=\"AdminApptsJs.wsave()\">" + (move ? "Move to this time" : "Book this time") + "</button><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminApptsJs.closeModal()\">Close</button></div></section>");
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string SearchPart(Ctx c, string term)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<form class=\"ap-wfind\" onsubmit=\"return AdminApptsJs.findPet()\" novalidate><label class=\"ap-field\"><span>Find the pet</span><input type=\"search\" id=\"ap-w-term\" maxlength=\"60\" value=\"" + Ui.Attr(term) + "\" placeholder=\"Pet or owner name, phone or microchip\" autocomplete=\"off\"></label><button type=\"submit\" class=\"ap-btn\">Find</button></form>");
            sb.Append("<div id=\"ap-w-results\" class=\"ap-wres\"></div>");
            if (c.Can("clients", 'A'))
            {
                sb.Append("<p class=\"ap-muted ap-wnew\">New to the clinic? <a class=\"ap-link\" href=\"AdminClients?n=1\">Add a new client</a> first, then come back.</p>");
            }
            return sb.ToString();
        }

        private static async Task<string> PetPart(Ctx c, ApPet p, DateTime today, bool fixedPet)
        {
            StringBuilder sb = new StringBuilder();
            string sz = SizeName(p.Size);
            sb.Append("<div class=\"ap-wpet\"><div><b>" + Ui.Enc(p.PetName) + "</b><small>" + Ui.Enc(p.SpeciesName + (p.Breed != string.Empty ? " · " + p.Breed : string.Empty) + (sz != string.Empty ? " · " + sz : string.Empty)) + "</small><small>" + Ui.Enc(p.OwnerName) + (p.Phone != string.Empty ? " · " + Ui.Enc(p.Phone) : string.Empty) + "</small></div><div class=\"ap-acts\">");
            if (c.Can("pets", 'V'))
            {
                sb.Append("<a class=\"ap-btn ap-btn-sm ap-btn-ghost\" href=\"AdminPets?p=" + Ui.Attr(p.PetId) + "\">Pet record</a>");
            }
            if (!fixedPet)
            {
                sb.Append("<button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.changePet()\">Change pet</button>");
            }
            sb.Append("</div></div>");
            if (p.Alert != string.Empty || p.ClientAlert != string.Empty)
            {
                sb.Append("<div class=\"ap-note ap-note-warn\">" + (p.Alert != string.Empty ? "<b>Pet alert:</b> " + Ui.Enc(p.Alert) : string.Empty) + (p.Alert != string.Empty && p.ClientAlert != string.Empty ? "<br>" : string.Empty) + (p.ClientAlert != string.Empty ? "<b>Client alert:</b> " + Ui.Enc(p.ClientAlert) : string.Empty) + "</div>");
            }
            var (vax, verr) = await Helper.GetListT<ApVax>("SELECT VAX_NAME AS VaxName, MAX(DUE_DATE) AS DueDate FROM PETVACCINE WHERE PET_ID = @pid GROUP BY VAX_NAME HAVING MAX(DUE_DATE) < @today ORDER BY VAX_NAME", new List<SqlParameter> { App.P("pid", p.PetId, 50), new SqlParameter("today", SqlDbType.Date) { Value = today } });
            if (verr == string.Empty && vax != null && vax.Count > 0)
            {
                sb.Append("<div class=\"ap-note ap-note-info\"><b>Vaccines overdue:</b> " + string.Join(", ", vax.Select(v => Ui.Enc(v.VaxName) + " (due " + Ui.Enc(Ui.Day(v.DueDate, v.DueDate.Year != today.Year)) + ")")) + ". You may want to add a vaccine visit.</div>");
            }
            return sb.ToString();
        }

        private static async Task<string> TypesPart(Ctx c, ApPet p, string staffPref, string bookSel)
        {
            var (books, berr) = await Helper.GetListT<BookRow>("SELECT " + Slots.BookCols + " FROM BOOKTYPE B WHERE B.ACTIVE = 1 AND B.VISIT_TYPE <> N'boarding' ORDER BY CASE B.VISIT_TYPE WHEN N'clinic' THEN 1 WHEN N'surgery' THEN 2 ELSE 3 END, B.BOOK_ORDER, B.BOOK_NAME");
            if (berr != string.Empty || books == null)
            {
                return "<p class=\"ap-muted\">The kinds of visit could not be loaded.</p>";
            }
            var (pairs, perr) = await Helper.GetListT<ApPerson>("SELECT X.BOOKTYPE_ID AS StaffName, S.STAFF_ID AS StaffId FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE S.ACTIVE = 1 AND (NOT EXISTS (SELECT 1 FROM STAFFSPECIES Y WHERE Y.STAFF_ID = S.STAFF_ID) OR EXISTS (SELECT 1 FROM STAFFSPECIES Y WHERE Y.STAFF_ID = S.STAFF_ID AND Y.SPECIES_KEY = @sp))", new List<SqlParameter> { App.P("sp", p.SpeciesKey, 20) });
            List<ApPerson> able = perr == string.Empty && pairs != null ? pairs : new List<ApPerson>();
            string only = c.U.OwnOnly ? c.U.StaffId : staffPref;
            List<BookRow> fit = books.Where(b => Slots.ForSpecies(b.Species, p.SpeciesKey) && able.Any(x => x.StaffName == b.BooktypeId && (only == string.Empty || x.StaffId == only))).ToList();
            StringBuilder sb = new StringBuilder();
            if (staffPref != string.Empty && !c.U.OwnOnly)
            {
                var (sn, serr) = await Helper.GetDataTable("SELECT STAFF_NAME FROM STAFF WHERE STAFF_ID = @sid", new List<SqlParameter> { App.P("sid", staffPref, 50) });
                string name = serr == string.Empty && sn.Rows.Count > 0 ? Convert.ToString(sn.Rows[0][0]) ?? string.Empty : string.Empty;
                sb.Append("<p class=\"ap-muted ap-wonly\">Showing what " + Ui.Enc(name == string.Empty ? "this staff member" : name) + " can do for " + Ui.Enc(p.PetName) + ". <button type=\"button\" class=\"ap-linkbtn\" onclick=\"AdminApptsJs.anyStaff()\">Show every kind</button></p>");
            }
            if (fit.Count == 0)
            {
                sb.Append("<p class=\"ap-muted\">Nothing can be booked for a " + Ui.Enc(p.SpeciesName.ToLowerInvariant()) + (staffPref != string.Empty || c.U.OwnOnly ? " with this staff member" : string.Empty) + ".</p>");
                return sb.ToString();
            }
            foreach (IGrouping<string, BookRow> g in fit.GroupBy(b => b.VisitType))
            {
                string head = g.Key == "surgery" ? "Surgery and dental (drop-off)" : g.Key == "grooming" ? "Grooming" : "Clinic visits";
                sb.Append("<div class=\"ap-tgrp\"><small>" + Ui.Enc(head) + "</small><div class=\"ap-types\">");
                foreach (BookRow b in g)
                {
                    string len = Slots.Dropoff(b) ? "Drop-off" : Slots.Length(b, p.Size).ToString(Ui.Inv) + " min";
                    sb.Append("<button type=\"button\" class=\"ap-type" + (b.BooktypeId == bookSel ? " ap-sel" : string.Empty) + "\" data-book=\"" + Ui.Attr(b.BooktypeId) + "\" onclick=\"AdminApptsJs.pickBook(this)\"><b>" + Ui.Enc(b.BookName) + "</b><small>" + Ui.Enc(len) + "</small></button>");
                }
                sb.Append("</div></div>");
            }
            return sb.ToString();
        }

        private static async Task<string> StaffPart(Ctx c, BookRow b, ApPet p, List<ApPerson> people, string staffSel)
        {
            StringBuilder sb = new StringBuilder();
            if (people.Count == 0)
            {
                return "<p class=\"ap-muted\">Nobody who can do this visit is set up for this kind of pet.</p>";
            }
            List<(string, string)> opts = new List<(string, string)>();
            if (!c.U.OwnOnly && people.Count > 1)
            {
                opts.Add((string.Empty, "Anyone available"));
            }
            opts.AddRange(people.Select(x => (x.StaffId, x.StaffName)));
            sb.Append("<div class=\"ap-wstaff\"><label class=\"ap-field\"><span>Who should see " + Ui.Enc(p.PetName) + "?</span><select id=\"ap-w-staffsel\" onchange=\"AdminApptsJs.pickStaff(this.value)\">" + Ui.Opts(opts, staffSel) + "</select></label>");
            decimal price = await Price(b, p.Size);
            string len = Slots.Dropoff(b) ? "Drop-off " + Clock(b.DropFrom) + "–" + Clock(b.DropTo) + ", pick-up from " + Clock(b.PickupHm) : Slots.Length(b, p.Size).ToString(Ui.Inv) + " minutes" + (SizeName(p.Size) != string.Empty && (b.MinutesM > 0 || b.MinutesL > 0) ? " for a " + SizeName(p.Size) + " " + Ui.Enc(p.SpeciesName.ToLowerInvariant()) : string.Empty);
            sb.Append("<p class=\"ap-wfacts\"><span>" + len + "</span>" + (price > 0 ? "<span>About " + Ui.Money(price) + "</span>" : string.Empty) + "</p></div>");
            return sb.ToString();
        }

        private static async Task<(string Html, DateTime Day)> WhenPart(Dictionary<string, string> s, BookRow b, ApPet p, List<ApPerson> people, string staffSel, DateTime day, string ignore, string timePref, bool seek)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            if (day < today)
            {
                day = today;
            }
            List<string> ids = people.Select(x => x.StaffId).Where(x => staffSel == string.Empty || x == staffSel).ToList();
            List<Opening> opens = new List<Opening>();
            int tries = seek ? 60 : 1;
            for (int i = 0; i < tries; i++)
            {
                opens = ids.Count == 0 ? new List<Opening>() : (await Slots.Find(s, b, staffSel, day, p.Size, ignore, now)).Where(o => ids.Contains(o.StaffId)).ToList();
                if (opens.Count > 0 || !seek)
                {
                    break;
                }
                day = day.AddDays(1);
            }
            bool drop = Slots.Dropoff(b);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-dnav\"><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.wday(-1)\"" + (day <= today ? " disabled" : string.Empty) + ">&larr; Day before</button><input type=\"date\" id=\"ap-w-day\" aria-label=\"Day\" value=\"" + Ui.Ymd(day) + "\" min=\"" + Ui.Ymd(today) + "\" onchange=\"AdminApptsJs.wdayset(this.value)\"><button type=\"button\" class=\"ap-btn ap-btn-sm ap-btn-ghost\" onclick=\"AdminApptsJs.wday(1)\">Next day &rarr;</button></div>");
            sb.Append("<p class=\"ap-dlabel\">" + Ui.Enc(DayHead(day, today)) + "</p>");
            var (same, serr) = await Helper.GetListT<ApSame>("SELECT A.APPT_ID AS ApptId, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, A.START_AT AS StartAt, A.PROC_AT AS ProcAt, A.STATUS AS Status FROM APPOINTMENT A LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE A.PET_ID = @pid AND A.START_AT >= @d0 AND A.START_AT < @d1 AND A.STATUS IN (N'booked', N'confirmed', N'checkedin') AND A.VISIT_TYPE <> N'boarding' AND A.APPT_ID <> @ign ORDER BY A.START_AT", new List<SqlParameter> { App.P("pid", p.PetId, 50), App.PDate("d0", day), App.PDate("d1", day.AddDays(1)), App.P("ign", ignore, 50) });
            if (serr == string.Empty && same != null && same.Count > 0)
            {
                sb.Append("<div class=\"ap-note ap-note-warn\"><b>" + Ui.Enc(p.PetName) + " already has " + (same.Count == 1 ? "a visit" : same.Count.ToString(Ui.Inv) + " visits") + " this day:</b> " + string.Join(", ", same.Select(x => Ui.Enc(x.BookName) + " at " + Ui.Hm(x.ProcAt ?? x.StartAt) + " (" + Ui.Enc(StatusName(x.Status).ToLowerInvariant()) + ")")) + ". You can still book.</div>");
            }
            if (opens.Count == 0)
            {
                var (isOpen, _, _) = Slots.Hours(s, day);
                string hol = await Slots.Holiday(day);
                string why = hol != string.Empty ? "The clinic is closed this day (" + Ui.Enc(hol) + ")." : !isOpen ? "The clinic is closed on " + Ui.Enc(Ui.DayNames[(int)day.DayOfWeek]) + "s." : day == today ? "No free times are left today." : "No free times this day.";
                sb.Append("<div class=\"ap-none\"><p>" + why + "</p><button type=\"button\" class=\"ap-btn ap-btn-sm\" onclick=\"AdminApptsJs.seek()\">Find the next free day</button></div>");
                return (sb.ToString(), day);
            }
            sb.Append("<div class=\"ap-slots\">");
            bool preset = false;
            foreach (ApPerson who in people.Where(x => opens.Any(o => o.StaffId == x.StaffId)))
            {
                List<Opening> mine = opens.Where(o => o.StaffId == who.StaffId).ToList();
                sb.Append("<div class=\"ap-sgrp\"><div class=\"ap-shead\"><b>" + Ui.Enc(who.StaffName) + "</b><small>" + (drop ? "Drop-off " + Ui.Hm(mine[0].Start) + " &middot; choose the surgery time" : Ui.Plural(mine.Count, "free time", "free times")) + "</small></div><div class=\"ap-times\">");
                foreach (Opening o in mine)
                {
                    DateTime key = o.ProcAt ?? o.Start;
                    string label = drop ? "Drop-off " + Ui.Hm(o.Start) + " · surgery " + Ui.Hm(o.ProcAt!.Value) + " · pick-up " + Ui.Hm(o.PickupAt ?? o.End) : Ui.Hm(o.Start) + " – " + Ui.Hm(o.End);
                    bool pre = !preset && timePref != string.Empty && Hm24(key.TimeOfDay) == timePref && (staffSel == string.Empty || staffSel == o.StaffId);
                    preset = preset || pre;
                    sb.Append("<button type=\"button\" class=\"ap-tm\"" + (pre ? " id=\"ap-w-pre\"" : string.Empty) + " data-staff=\"" + Ui.Attr(o.StaffId) + "\" data-start=\"" + Stamp(o.Start) + "\" data-proc=\"" + (o.ProcAt.HasValue ? Stamp(o.ProcAt.Value) : string.Empty) + "\" data-label=\"" + Ui.Attr(Ui.LongDay(day) + " · " + label + " · with " + who.StaffName) + "\" onclick=\"AdminApptsJs.pickSlot(this)\">" + (drop ? "<b>Surgery " + Ui.Hm(o.ProcAt!.Value) + "</b><small>pick-up " + Ui.Hm(o.PickupAt ?? o.End) + "</small>" : "<b>" + Ui.Hm(o.Start) + "</b>") + "</button>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("</div>");
            if (timePref != string.Empty && !preset)
            {
                TimeSpan? want = Ui.ParseHm(timePref);
                sb.Insert(0, "<div class=\"ap-note ap-note-info\">" + (want.HasValue ? Ui.Hm(day.Add(want.Value)) : Ui.Enc(timePref)) + " is not free for this visit. Pick another time.</div>");
            }
            return (sb.ToString(), day);
        }

        private async Task OpenWizard(ApiResponse response, Ctx c, Dictionary<string, string> s, string petId, string staffPref, DateTime day, string timePref)
        {
            DateTime today = App.Now(s).Date;
            response.SetElementContents("ap-mbody", WizardFrame("new"));
            ApPet? p = petId != string.Empty ? await PetById(petId) : null;
            string staff = c.U.OwnOnly ? c.U.StaffId : staffPref;
            response.SetVariableData("apwz", new { mode = "new", pet = p != null ? p.PetId : string.Empty, staff = staff, day = Ui.Ymd(day < today ? today : day), time = timePref, appt = string.Empty });
            if (p != null)
            {
                response.SetElementContents("ap-w-pet", await PetPart(c, p, today, false));
                response.SetElementContents("ap-w-type", await TypesPart(c, p, staff, string.Empty));
                response.ExecuteScript("AdminApptsJs.wzInit(2);");
            }
            else
            {
                if (petId != string.Empty)
                {
                    Toast(response, "That pet was not found. Search for it below.");
                }
                response.SetElementContents("ap-w-pet", SearchPart(c, string.Empty));
                response.ExecuteScript("AdminApptsJs.wzInit(1);");
            }
        }

        private async Task<Ctx?> WizardGuard(ApiResponse response)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return null;
            }
            bool move = Id(V("wappt")) != string.Empty;
            if (!c.Can("appts", move ? 'E' : 'A'))
            {
                Toast(response, move ? "You don't have the right to move appointments." : "You don't have the right to book appointments.");
                return null;
            }
            return c;
        }

        public async Task<ApiResponse> NewBooking()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("appts", 'A'))
            {
                Toast(response, "You don't have the right to book appointments.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            await OpenWizard(response, c, s, Id(V("bookpet")), string.Empty, App.Now(s).Date, string.Empty);
            return response;
        }

        public async Task<ApiResponse> PetSearch()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await WizardGuard(response);
            if (c == null)
            {
                return response;
            }
            string term = Ui.OneLine(V("petterm"), 60);
            if (term.Length < 2)
            {
                response.SetElementContents("ap-w-results", "<p class=\"ap-muted\">Type at least 2 letters or numbers.</p>");
                return response;
            }
            string digits = Ui.Digits(term);
            var (list, emsg) = await Helper.GetListT<ApPet>("SELECT TOP 20 " + PetCols + PetFrom + " AND (P.PET_NAME LIKE @ql OR C.FIRST_NAME LIKE @ql OR C.LAST_NAME LIKE @ql OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @ql OR C.CLIENT_NO LIKE @ql OR (@qd <> N'' AND (" + PhoneDigits + " LIKE @qdl OR P.MICROCHIP LIKE @qdl))) ORDER BY P.PET_NAME, C.LAST_NAME", new List<SqlParameter> { App.P("ql", Like(term), 200), App.P("qd", digits.Length >= 3 ? digits : string.Empty, 30), App.P("qdl", Like(digits), 60) });
            if (emsg != string.Empty || list == null)
            {
                response.SetElementContents("ap-w-results", "<p class=\"ap-muted\">The search did not work. Try again.</p>");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<p class=\"ap-muted\">No pet matches &ldquo;" + Ui.Enc(term) + "&rdquo;.</p>");
            }
            foreach (ApPet p in list)
            {
                string sz = SizeName(p.Size);
                sb.Append("<button type=\"button\" class=\"ap-wpick\" onclick=\"AdminApptsJs.pickPet('" + Ui.Js(p.PetId) + "')\"><b>" + Ui.Enc(p.PetName) + "</b><small>" + Ui.Enc(p.SpeciesName + (p.Breed != string.Empty ? " · " + p.Breed : string.Empty) + (sz != string.Empty ? " · " + sz : string.Empty)) + "</small><small>" + Ui.Enc(p.OwnerName + (p.Phone != string.Empty ? " · " + p.Phone : string.Empty)) + "</small>" + (p.Alert != string.Empty ? "<em class=\"ap-alert\">" + Ui.Enc(p.Alert) + "</em>" : string.Empty) + "</button>");
            }
            if (c.Can("pets", 'A'))
            {
                List<ApPet> owners = list.GroupBy(x => x.ClientId).Select(g => g.First()).Take(3).ToList();
                if (owners.Count > 0)
                {
                    sb.Append("<p class=\"ap-muted ap-wnew\">Pet not listed? Add a new pet for " + string.Join(", ", owners.Select(o => "<a class=\"ap-link\" href=\"AdminPets?c=" + Ui.Attr(o.ClientId) + "&amp;n=1\">" + Ui.Enc(o.OwnerName) + "</a>")) + ".</p>");
                }
            }
            response.SetElementContents("ap-w-results", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> PickPet()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await WizardGuard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            ApPet? p = await PetById(Id(V("wpet")));
            if (p == null)
            {
                Toast(response, "That pet was not found.");
                return response;
            }
            string staff = c.U.OwnOnly ? c.U.StaffId : Id(V("wstaff"));
            response.SetElementContents("ap-w-pet", await PetPart(c, p, App.Now(s).Date, false));
            response.SetElementContents("ap-w-type", await TypesPart(c, p, staff, string.Empty));
            response.ExecuteScript("AdminApptsJs.petPicked('" + Ui.Js(p.PetId) + "');");
            return response;
        }

        public async Task<ApiResponse> ChangePet()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await WizardGuard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ap-w-pet", SearchPart(c, string.Empty));
            response.ExecuteScript("AdminApptsJs.wzInit(1);");
            return response;
        }

        public async Task<ApiResponse> PickBook()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await WizardGuard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            ApPet? p = await PetById(Id(V("wpet")));
            BookRow? b = await BookById(Id(V("wbook")));
            if (p == null || b == null || !Slots.ForSpecies(b.Species, p.SpeciesKey))
            {
                Toast(response, "Pick the pet and the kind of visit again.");
                return response;
            }
            List<ApPerson> people = await Eligible(c, b.BooktypeId, p.SpeciesKey);
            string staff = Id(V("wstaff"));
            if (c.U.OwnOnly)
            {
                staff = c.U.StaffId;
            }
            if (staff != string.Empty && !people.Any(x => x.StaffId == staff))
            {
                staff = string.Empty;
            }
            if (staff == string.Empty && people.Count == 1)
            {
                staff = people[0].StaffId;
            }
            response.SetElementContents("ap-w-staff", await StaffPart(c, b, p, people, staff));
            DateTime day = Ui.ParseDay(V("wday")) ?? App.Now(s).Date;
            var (html, shown) = await WhenPart(s, b, p, people, staff, day, Id(V("wappt")), Ui.ParseHm(V("wtime")).HasValue ? Hm24(Ui.ParseHm(V("wtime"))!.Value) : string.Empty, false);
            response.SetElementContents("ap-w-when", html);
            response.ExecuteScript("AdminApptsJs.bookPicked('" + Ui.Js(b.BooktypeId) + "', '" + Ui.Js(staff) + "', '" + Ui.Ymd(shown) + "');");
            return response;
        }

        public async Task<ApiResponse> Times()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await WizardGuard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            ApPet? p = await PetById(Id(V("wpet")));
            BookRow? b = await BookById(Id(V("wbook")));
            if (p == null || b == null)
            {
                Toast(response, "Pick the pet and the kind of visit again.");
                return response;
            }
            List<ApPerson> people = await Eligible(c, b.BooktypeId, p.SpeciesKey);
            string staff = c.U.OwnOnly ? c.U.StaffId : Id(V("wstaff"));
            if (staff != string.Empty && !people.Any(x => x.StaffId == staff))
            {
                staff = string.Empty;
            }
            DateTime day = Ui.ParseDay(V("wday")) ?? App.Now(s).Date;
            var (html, shown) = await WhenPart(s, b, p, people, staff, day, Id(V("wappt")), Ui.ParseHm(V("wtime")).HasValue ? Hm24(Ui.ParseHm(V("wtime"))!.Value) : string.Empty, V("wseek") == "1");
            response.SetElementContents("ap-w-when", html);
            response.ExecuteScript("AdminApptsJs.timesShown('" + Ui.Ymd(shown) + "');");
            return response;
        }

        private static async Task<string> NewCode()
        {
            for (int i = 0; i < 12; i++)
            {
                StringBuilder sb = new StringBuilder("CAH-");
                for (int k = 0; k < 6; k++)
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

        private sealed class Chosen
        {
            public ApPet Pet { get; set; } = new ApPet();
            public BookRow Book { get; set; } = new BookRow();
            public Opening Slot { get; set; } = new Opening();
            public string StaffName { get; set; } = string.Empty;
        }

        private async Task<(Chosen? Pick, string Error)> CheckSlot(Ctx c, Dictionary<string, string> s, string ignore)
        {
            ApPet? p = await PetById(Id(V("wpet")));
            BookRow? b = await BookById(Id(V("wbook")));
            if (p == null || b == null || !Slots.ForSpecies(b.Species, p.SpeciesKey))
            {
                return (null, "Pick the pet and the kind of visit again.");
            }
            string staff = Id(V("slotstaff"));
            List<ApPerson> people = await Eligible(c, b.BooktypeId, p.SpeciesKey);
            ApPerson? who = people.FirstOrDefault(x => x.StaffId == staff);
            if (who == null)
            {
                return (null, "Pick a time with someone who can do this visit.");
            }
            DateTime? start = Ui.ParseStamp(V("slotstart"));
            DateTime? proc = Ui.ParseStamp(V("slotproc"));
            bool drop = Slots.Dropoff(b);
            if (start == null || (drop && proc == null))
            {
                return (null, "Pick a time first.");
            }
            DateTime now = App.Now(s);
            List<Opening> opens = await Slots.Find(s, b, staff, start.Value.Date, p.Size, ignore, now);
            Opening? hit = opens.FirstOrDefault(o => o.StaffId == staff && (drop ? o.ProcAt == proc : o.Start == start));
            if (hit == null)
            {
                return (null, "That time was just taken or is no longer open. Pick another time.");
            }
            return (new Chosen { Pet = p, Book = b, Slot = hit, StaffName = who.StaffName }, string.Empty);
        }

        private static List<string> OverlapGuard()
        {
            return new List<string>
            {
                "UPDATE STAFF SET SYSDTE = SYSDTE WHERE STAFF_ID = @sid",
                "UPDATE ROOMTYPE SET SYSDTE = SYSDTE WHERE ROOMTYPE_ID = @rt",
                "IF @rt <> N'' AND (SELECT COUNT(*) FROM APPOINTMENT WHERE ROOMTYPE_ID = @rt AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) >= (SELECT COUNT(*) FROM ROOM WHERE ROOMTYPE_ID = @rt AND ACTIVE = 1) THROW 50003, N'SLOTTAKEN', 1;",
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE STAFF_ID = @sid AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) THROW 50003, N'SLOTTAKEN', 1;"
            };
        }

        private static List<SqlParameter> SlotParams(Opening o)
        {
            DateTime ba = o.ProcAt ?? o.Start;
            DateTime bz = o.ProcAt.HasValue ? o.ProcAt.Value.AddMinutes(o.ProcMinutes) : o.End;
            return new List<SqlParameter>
            {
                App.P("sid", o.StaffId, 50), App.PDate("ba", ba), App.PDate("bz", bz), App.PDate("st", o.Start), App.PDate("en", o.End),
                App.PInt("mins", (int)(o.End - o.Start).TotalMinutes), App.PDate("proc", o.ProcAt), App.PInt("pmins", o.ProcAt.HasValue ? o.ProcMinutes : 0), App.PDate("pick", o.PickupAt)
            };
        }

        private static string SlotText(Opening o, string staffName)
        {
            if (o.ProcAt.HasValue)
            {
                return Ui.Day(o.Start, false) + ", drop-off " + Ui.Hm(o.Start) + ", surgery " + Ui.Hm(o.ProcAt.Value) + ", pick-up " + Ui.Hm(o.PickupAt ?? o.End) + " with " + staffName;
            }
            return Ui.Day(o.Start, false) + " " + Ui.Hm(o.Start) + " with " + staffName;
        }

        public async Task<ApiResponse> SaveBooking()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("appts", 'A'))
            {
                await App.Log(c.U.StaffId, "book appointment", "not allowed", false);
                Toast(response, "You don't have the right to book appointments.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            var (pick, err) = await CheckSlot(c, s, string.Empty);
            if (pick == null)
            {
                response.SetElementContents("ap-e-wsave", Ui.Enc(err));
                response.ExecuteScript("AdminApptsJs.slotGone();");
                return response;
            }
            string code = await NewCode();
            if (code == string.Empty)
            {
                response.SetElementContents("ap-e-wsave", "A booking code could not be made. Try again.");
                return response;
            }
            string src = V("srcval") == "desk" ? "desk" : "phone";
            string aid = App.NewId();
            decimal price = await Price(pick.Book, pick.Pet.Size);
            List<SqlParameter> ps = SlotParams(pick.Slot);
            ps.AddRange(new List<SqlParameter>
            {
                App.P("aid", aid, 50), App.P("code", code, 20), App.P("cid", pick.Pet.ClientId, 50), App.P("pid", pick.Pet.PetId, 50), App.P("bt", pick.Book.BooktypeId, 50),
                App.P("vt", pick.Book.VisitType, 20), App.P("rt", pick.Book.RoomtypeId, 50), App.PDec("price", price), App.P("src", src, 10),
                App.P("note", Ui.Clip(V("vetnote"), 600), 600), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", src == "desk" ? "Booked at the desk" : "Booked by phone", 300)
            });
            List<string> sql = OverlapGuard();
            sql.Add("INSERT INTO APPOINTMENT (APPT_ID, APPT_CODE, CLIENT_ID, PET_ID, BOOKTYPE_ID, VISIT_TYPE, STAFF_ID, ROOMTYPE_ID, START_AT, END_AT, MINUTES, PROC_AT, PROC_MINUTES, PICKUP_AT, EST_PRICE, SOURCE, STATUS, NOTE, CREATED_UTC, CREATED_BY) VALUES (@aid, @code, @cid, @pid, @bt, @vt, @sid, @rt, @st, @en, @mins, @proc, @pmins, @pick, @price, @src, N'confirmed', @note, SYSUTCDATETIME(), @me)");
            sql.Add("INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), @me, @ltext)");
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                await App.Log(c.U.StaffId, "book appointment", pick.Pet.PetName + " · failed", false);
                response.SetElementContents("ap-e-wsave", r.Contains("SLOTTAKEN", StringComparison.OrdinalIgnoreCase) ? "That time was just taken. Pick another time." : "The booking could not be saved. Try again.");
                response.ExecuteScript("AdminApptsJs.slotGone();");
                return response;
            }
            await App.Log(c.U.StaffId, "book appointment", pick.Pet.PetName + " · " + pick.Book.BookName + " · " + code, true);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ap-done\"><span class=\"ap-st ap-st-confirmed\">Confirmed</span><h2>Booked for " + Ui.Enc(pick.Pet.PetName) + "</h2><p class=\"ap-donecode\">" + Ui.Enc(code) + "</p><p>" + Ui.Enc(pick.Book.BookName) + " &middot; " + Ui.Enc(SlotText(pick.Slot, pick.StaffName)) + "</p>");
            if (price > 0)
            {
                sb.Append("<p class=\"ap-muted\">Estimate " + Ui.Money(price) + ". Give the owner the booking code.</p>");
            }
            sb.Append("<div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-accent\" onclick=\"AdminApptsJs.open('" + Ui.Js(aid) + "')\">Open the appointment</button><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminApptsJs.newBooking()\">Book another</button><button type=\"button\" class=\"ap-btn ap-btn-ghost\" onclick=\"AdminApptsJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("ap-mbody", sb.ToString());
            response.ExecuteScript("AdminApptsJs.shown();");
            await RefreshList(response, c);
            return response;
        }

        public async Task<ApiResponse> Reschedule()
        {
            ApiResponse response = new ApiResponse();
            var (c, a) = await ForChange(response, 'E');
            if (c == null || a == null)
            {
                return response;
            }
            if (a.Status != "booked" && a.Status != "confirmed")
            {
                Toast(response, "Only booked or confirmed appointments can be moved.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            ApPet? p = await PetById(a.PetId);
            BookRow? b = await BookById(a.BooktypeId);
            if (p == null || b == null)
            {
                Toast(response, "This pet or kind of visit is no longer active, so it cannot be moved. Cancel it and book again.");
                return response;
            }
            DateTime today = App.Now(s).Date;
            List<ApPerson> people = await Eligible(c, b.BooktypeId, p.SpeciesKey);
            string staff = people.Any(x => x.StaffId == a.StaffId) ? a.StaffId : people.Count == 1 ? people[0].StaffId : string.Empty;
            DateTime day = a.StartAt.Date < today ? today : a.StartAt.Date;
            response.SetElementContents("ap-mbody", WizardFrame("move"));
            response.SetElementContents("ap-w-pet", await PetPart(c, p, today, true) + "<p class=\"ap-wnow\">Now: <b>" + Ui.Enc(a.BookName) + "</b> &middot; " + Ui.Enc(Ui.Day(a.StartAt, false)) + " " + (a.ProcAt.HasValue ? "surgery " + Ui.Hm(a.ProcAt.Value) : Ui.Hm(a.StartAt)) + " with " + Ui.Enc(a.StaffName) + " <span class=\"ap-code\">" + Ui.Enc(a.ApptCode) + "</span></p>");
            response.SetElementContents("ap-w-type", "<div class=\"ap-types\"><span class=\"ap-type ap-sel\"><b>" + Ui.Enc(b.BookName) + "</b><small>" + (Slots.Dropoff(b) ? "Drop-off" : Slots.Length(b, p.Size).ToString(Ui.Inv) + " min") + "</small></span></div>");
            response.SetElementContents("ap-w-staff", await StaffPart(c, b, p, people, staff));
            var (html, shown) = await WhenPart(s, b, p, people, staff, day, a.ApptId, string.Empty, false);
            response.SetElementContents("ap-w-when", html);
            response.SetVariableData("apwz", new { mode = "move", pet = p.PetId, staff = staff, day = Ui.Ymd(shown), time = string.Empty, appt = a.ApptId, book = b.BooktypeId });
            response.ExecuteScript("AdminApptsJs.wzInit(4);");
            return response;
        }

        public async Task<ApiResponse> SaveMove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("appts", 'E'))
            {
                await App.Log(c.U.StaffId, "move appointment", "not allowed", false);
                Toast(response, "You don't have the right to move appointments.");
                return response;
            }
            ApDetail? a = await Detail(Id(V("wappt")));
            if (a == null || !c.Mine(a.StaffId))
            {
                Toast(response, "This appointment was not found.");
                return response;
            }
            if (a.Status != "booked" && a.Status != "confirmed")
            {
                response.SetElementContents("ap-e-wsave", "Only booked or confirmed appointments can be moved.");
                return response;
            }
            if (Id(V("wpet")) != a.PetId || Id(V("wbook")) != a.BooktypeId)
            {
                response.SetElementContents("ap-e-wsave", "Moving keeps the same pet and kind of visit.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            var (pick, err) = await CheckSlot(c, s, a.ApptId);
            if (pick == null)
            {
                response.SetElementContents("ap-e-wsave", Ui.Enc(err));
                response.ExecuteScript("AdminApptsJs.slotGone();");
                return response;
            }
            Opening was = new Opening { Start = a.StartAt, End = a.EndAt, ProcAt = a.ProcAt, ProcMinutes = a.ProcMinutes, PickupAt = a.PickupAt };
            string text = "Moved from " + SlotText(was, a.StaffName == string.Empty ? "anyone" : a.StaffName) + " to " + SlotText(pick.Slot, pick.StaffName);
            List<SqlParameter> ps = SlotParams(pick.Slot);
            ps.AddRange(new List<SqlParameter> { App.P("aid", a.ApptId, 50), App.P("rt", pick.Book.RoomtypeId, 50), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(text, 300), 300) });
            List<string> sql = new List<string> { "IF NOT EXISTS (SELECT 1 FROM APPOINTMENT WHERE APPT_ID = @aid AND STATUS IN (N'booked', N'confirmed')) THROW 50002, N'" + Flow.Moved + "', 1;" };
            sql.AddRange(OverlapGuard());
            sql.Add("UPDATE APPOINTMENT SET STAFF_ID = @sid, START_AT = @st, END_AT = @en, MINUTES = @mins, PROC_AT = @proc, PROC_MINUTES = @pmins, PICKUP_AT = @pick, SYSDTE = GETDATE() WHERE APPT_ID = @aid");
            sql.Add("INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), @me, @ltext)");
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                await App.Log(c.U.StaffId, "move appointment", a.PetName + " · " + a.ApptCode + " · failed", false);
                response.SetElementContents("ap-e-wsave", r.Contains("SLOTTAKEN", StringComparison.OrdinalIgnoreCase) ? "That time was just taken. Pick another time." : r.Contains(Flow.Moved, StringComparison.OrdinalIgnoreCase) ? "Someone else just changed this appointment." : "The change could not be saved. Try again.");
                response.ExecuteScript("AdminApptsJs.slotGone();");
                return response;
            }
            await App.Log(c.U.StaffId, "move appointment", a.PetName + " · " + a.ApptCode + " · " + SlotText(pick.Slot, pick.StaffName), true);
            await Changed(response, c, a.ApptId, "Moved to " + Ui.Enc(SlotText(pick.Slot, pick.StaffName)) + ".", "Appointment moved.");
            return response;
        }
    }
}
