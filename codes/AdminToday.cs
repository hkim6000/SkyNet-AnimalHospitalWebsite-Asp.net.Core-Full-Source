using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminToday : WebPage
    {
        private const string ApptSql = "SELECT A.APPT_ID AS ApptId, A.START_AT AS StartAt, A.PROC_AT AS ProcAt, A.VISIT_TYPE AS VisitType, A.STATUS AS Status, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.ROOMTYPE_ID AS RoomtypeId, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, A.NEW_CLIENT AS NewClient, P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS Species, P.ALERT AS Alert, C.CLIENT_ID AS ClientId, C.FIRST_NAME + N' ' + C.LAST_NAME AS ClientName, C.PHONE AS Phone FROM APPOINTMENT A JOIN PET P ON P.PET_ID = A.PET_ID JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID";
        private const string VisitSql = "SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.APPT_ID AS ApptId, V.VISIT_TYPE AS VisitType, V.STATUS AS Status, V.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.REASON AS Reason, V.CHECKIN_AT AS CheckinAt, V.START_AT AS StartAt, V.READY_AT AS ReadyAt, V.CLOSED_AT AS ClosedAt, P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS Species, P.ALERT AS Alert, C.CLIENT_ID AS ClientId, C.FIRST_NAME + N' ' + C.LAST_NAME AS ClientName, C.PHONE AS Phone, ISNULL(A.ROOMTYPE_ID, N'') AS RoomtypeId, ISNULL(B.BOOK_NAME, N'') AS BookName, ISNULL(X.ROOM_ID, N'') AS RoomId, ISNULL(R.ROOM_NAME, N'') AS RoomName, ISNULL(X.PURPOSE, N'') AS Purpose, X.IN_AT AS RoomSince FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID AND V.APPT_ID <> N'' LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID OUTER APPLY (SELECT TOP 1 VR.ROOM_ID, VR.PURPOSE, VR.IN_AT FROM VISITROOM VR WHERE VR.VISIT_ID = V.VISIT_ID AND VR.OUT_AT IS NULL ORDER BY VR.IN_AT DESC) X LEFT JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID";
        private const string RoomSql = "SELECT R.ROOM_ID AS RoomId, R.ROOM_NAME AS RoomName, R.ROOMTYPE_ID AS RoomtypeId, T.TYPE_NAME AS TypeName, T.TYPE_ORDER AS TypeOrder, R.ROOM_ORDER AS RoomOrder, ISNULL(X.VISIT_ID, N'') AS BusyVisit, ISNULL(P.PET_NAME, N'') AS BusyPet, ISNULL(S.STAFF_NAME, N'') AS BusyStaff FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID LEFT JOIN VISITROOM X ON X.ROOM_ID = R.ROOM_ID AND X.OUT_AT IS NULL LEFT JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE R.ACTIVE = 1 AND T.ACTIVE = 1 AND T.PURPOSE <> N'boarding' ORDER BY T.TYPE_ORDER, R.ROOM_ORDER, R.ROOM_NAME";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Today | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--td-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("td"));
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminTodayJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("td-menu", Auth.Menu("td", "AdminTodayJs", "AdminToday", c));
            response.SetElementContents("td-who", Auth.Who("td", c));
            response.ExecuteScript("AdminTodayJs.secs();");
        }

        private static string Denied()
        {
            return "<div class=\"td-panel td-deny\"><h1>You don't have access to this page</h1><p>Today's board is for the front desk and clinical team. Ask a manager to open it for you in Staff and permissions.</p><a class=\"td-btn td-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminTodayJs.out();");
            return response;
        }

        private string Filter(Ctx c, List<StaffPick> staff)
        {
            if (c.U.OwnOnly)
            {
                return c.U.StaffId;
            }
            string f = Val("filt", 50);
            return staff.Any(x => x.StaffId == f) ? f : string.Empty;
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
            if (!c.Can("today", 'V'))
            {
                response.SetElementContents("td-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<StaffPick> staff = await Flow.Providers();
            string filt = Filter(c, staff);
            var (sum, board) = await Build(c, s, filt);
            response.SetElementContents("td-main", Head(c, s, staff, filt, sum) + "<div id=\"td-board\">" + board + "</div>");
            response.ExecuteScript("AdminTodayJs.ready();");
            return response;
        }

        public async Task<ApiResponse> Board()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("today", 'V'))
            {
                response.SetElementContents("td-main", Denied());
                return response;
            }
            await Redraw(response, c, string.Empty, false);
            return response;
        }

        private async Task Redraw(ApiResponse response, Ctx c, string message, bool close)
        {
            Dictionary<string, string> s = await App.Settings();
            List<StaffPick> staff = await Flow.Providers();
            var (sum, board) = await Build(c, s, Filter(c, staff));
            response.SetElementContents("td-sum", sum);
            response.SetElementContents("td-board", board);
            response.ExecuteScript((close ? "AdminTodayJs.closeModal();" : string.Empty) + "AdminTodayJs.drawn();" + (message != string.Empty ? "AdminTodayJs.toast('" + Ui.Js(message) + "');" : string.Empty));
        }

        private static string Head(Ctx c, Dictionary<string, string> s, List<StaffPick> staff, string filt, string sum)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"td-ptop\"><div><h1>Today</h1><p>" + Ui.LongDay(App.Now(s)) + " <span class=\"td-sum\" id=\"td-sum\">" + sum + "</span></p></div><div class=\"td-acts\">");
            if (c.Can("today", 'E'))
            {
                sb.Append("<button type=\"button\" class=\"td-btn td-btn-accent\" onclick=\"AdminTodayJs.walkIn()\">Walk-in</button>");
            }
            if (c.Can("appts", 'A'))
            {
                sb.Append("<a class=\"td-btn\" href=\"AdminAppts\">New appointment</a>");
            }
            if (c.Can("roomboard", 'V'))
            {
                sb.Append("<a class=\"td-btn td-btn-ghost\" href=\"AdminRoomBoard\">Room board</a>");
            }
            sb.Append("</div></div><div class=\"td-bar\"><label class=\"td-field td-filt\"><span>Show</span><select id=\"td-fstaff\" onchange=\"AdminTodayJs.filter()\"" + (c.U.OwnOnly ? " disabled" : string.Empty) + ">");
            if (c.U.OwnOnly)
            {
                sb.Append("<option value=\"" + Ui.Attr(c.U.StaffId) + "\" selected>Only my patients</option>");
            }
            else
            {
                sb.Append("<option value=\"\">All staff</option>");
                foreach (StaffPick p in staff)
                {
                    sb.Append("<option value=\"" + Ui.Attr(p.StaffId) + "\"" + (p.StaffId == filt ? " selected" : string.Empty) + ">" + Ui.Enc(p.StaffName) + "</option>");
                }
            }
            sb.Append("</select></label><p class=\"td-live\">The board updates by itself every 45 seconds. <button type=\"button\" class=\"td-link\" onclick=\"AdminTodayJs.refresh()\">Refresh now</button></p></div>");
            return sb.ToString();
        }

        private static async Task<int> Count(string sql, List<SqlParameter> ps)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, ps);
            return emsg == string.Empty && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        private static string MinText(int m)
        {
            if (m < 1)
            {
                return "<1 min";
            }
            if (m < 60)
            {
                return m == 1 ? "1 min" : m + " mins";
            }
            return (m / 60) + " hr" + (m % 60 > 0 ? " " + (m % 60) + " min" : string.Empty);
        }

        private static string Mins(DateTime from, DateTime now)
        {
            int m = Math.Max(0, (int)Math.Floor((now - from).TotalMinutes));
            return "<span class=\"td-mins\" data-min=\"" + m + "\">" + Ui.Enc(MinText(m)) + "</span>";
        }

        private static string Kind(string visitType)
        {
            return visitType switch { "surgery" => "Surgery", "grooming" => "Grooming", _ => "Clinic visit" };
        }

        private static string StatusChip(string status)
        {
            return status switch
            {
                "confirmed" => "<span class=\"td-st td-st-confirmed\">Confirmed</span>",
                "booked" => "<span class=\"td-st td-st-booked\">Booked</span>",
                _ => "<span class=\"td-st\">" + Ui.Enc(status) + "</span>"
            };
        }

        private static string RoomChip(string purpose)
        {
            return purpose switch
            {
                "surgery" => "<span class=\"td-st td-st-insurgery\">In surgery</span>",
                "recovery" => "<span class=\"td-st td-st-recovering\">Recovering</span>",
                "grooming" => "<span class=\"td-st td-st-inroom\">Grooming</span>",
                "emergency" => "<span class=\"td-st td-st-noshow\">Emergency</span>",
                _ => "<span class=\"td-st td-st-inroom\">In room</span>"
            };
        }

        private static string PetLine(Ctx c, string petId, string petName, string species, string alert)
        {
            string name = c.Can("pets", 'V') ? "<a href=\"AdminPets?p=" + Uri.EscapeDataString(petId) + "\">" + Ui.Enc(petName) + "</a>" : Ui.Enc(petName);
            string sb = "<p class=\"td-pet\"><b>" + name + "</b>" + (species != string.Empty ? " <small>" + Ui.Enc(species) + "</small>" : string.Empty) + "</p>";
            if (alert.Trim() != string.Empty)
            {
                sb += "<p class=\"td-alert\"><i aria-hidden=\"true\">!</i><span>" + Ui.Enc(alert) + "</span></p>";
            }
            return sb;
        }

        private static string OwnerLine(Ctx c, string clientId, string clientName, string phone)
        {
            string name = c.Can("clients", 'V') ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(clientId) + "\">" + Ui.Enc(clientName) + "</a>" : Ui.Enc(clientName);
            string digits = Ui.Digits(phone);
            return "<p class=\"td-own\">" + name + (phone != string.Empty ? " &middot; " + (digits != string.Empty ? "<a href=\"tel:" + Ui.Attr(digits) + "\">" + Ui.Enc(phone) + "</a>" : Ui.Enc(phone)) : string.Empty) + "</p>";
        }

        private static string Who(string staffName)
        {
            return staffName != string.Empty ? Ui.Enc(staffName) : "Any staff";
        }

        private static string Col(string key, string title, int n, string body, string empty)
        {
            return "<section class=\"td-col td-col-" + key + "\"><header class=\"td-colh\"><h2>" + Ui.Enc(title) + "</h2><span class=\"td-n\">" + n + "</span></header><div class=\"td-list\">" + (body != string.Empty ? body : "<p class=\"td-empty\">" + Ui.Enc(empty) + "</p>") + "</div></section>";
        }

        private static async Task<(string Sum, string Board)> Build(Ctx c, Dictionary<string, string> s, string filt)
        {
            DateTime now = App.Now(s);
            DateTime d0 = now.Date;
            List<SqlParameter> Ps() => new List<SqlParameter> { App.PDate("d0", d0), App.PDate("d1", d0.AddDays(1)), App.P("filt", filt, 50) };
            var (appts, aerr) = await Helper.GetListT<TdAppt>(ApptSql + " WHERE A.START_AT >= @d0 AND A.START_AT < @d1 AND A.VISIT_TYPE <> N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.VISIT_ID = N'' AND (@filt = N'' OR A.STAFF_ID = @filt) ORDER BY A.START_AT, P.PET_NAME", Ps());
            var (visits, verr) = await Helper.GetListT<TdVisit>(VisitSql + " WHERE V.VISIT_TYPE <> N'boarding' AND (V.STATUS IN (N'waiting', N'inroom', N'checkout') OR (V.STATUS = N'closed' AND V.CLOSED_AT >= @d0 AND V.CLOSED_AT < @d1)) AND (@filt = N'' OR V.STAFF_ID = @filt)", Ps());
            if (aerr != string.Empty || verr != string.Empty || appts == null || visits == null)
            {
                return (string.Empty, "<div class=\"td-panel\"><p>The board could not be loaded right now. It tries again in a moment.</p></div>");
            }
            int noshow = await Count("SELECT COUNT(*) FROM APPOINTMENT WHERE START_AT >= @d0 AND START_AT < @d1 AND VISIT_TYPE <> N'boarding' AND STATUS = N'noshow' AND (@filt = N'' OR STAFF_ID = @filt)", Ps());
            int cancelled = await Count("SELECT COUNT(*) FROM APPOINTMENT WHERE START_AT >= @d0 AND START_AT < @d1 AND VISIT_TYPE <> N'boarding' AND STATUS = N'cancelled' AND (@filt = N'' OR STAFF_ID = @filt)", Ps());
            List<TdVisit> waiting = visits.Where(v => v.Status == "waiting").OrderBy(v => v.CheckinAt).ToList();
            List<TdVisit> inroom = visits.Where(v => v.Status == "inroom").OrderBy(v => v.RoomSince ?? v.StartAt ?? v.CheckinAt).ToList();
            List<TdVisit> ready = visits.Where(v => v.Status == "checkout").OrderBy(v => v.ReadyAt ?? v.CheckinAt).ToList();
            List<TdVisit> done = visits.Where(v => v.Status == "closed").OrderByDescending(v => v.ClosedAt).ToList();
            bool canE = c.Can("today", 'E');
            bool visitV = c.Can("visits", 'V');
            StringBuilder b = new StringBuilder();
            if (c.Can("boarding", 'V'))
            {
                int arrive = await Count("SELECT COUNT(*) FROM APPOINTMENT WHERE VISIT_TYPE = N'boarding' AND START_AT >= @d0 AND START_AT < @d1 AND STATUS IN (N'booked', N'confirmed')", Ps());
                int leave = await Count("SELECT COUNT(*) FROM VISIT WHERE VISIT_TYPE = N'boarding' AND STATUS = N'inroom' AND PLAN_OUT >= @d0 AND PLAN_OUT < @d1", Ps());
                if (arrive + leave > 0)
                {
                    List<string> parts = new List<string>();
                    if (arrive > 0)
                    {
                        parts.Add(Ui.Plural(arrive, "arrival", "arrivals"));
                    }
                    if (leave > 0)
                    {
                        parts.Add(Ui.Plural(leave, "departure", "departures"));
                    }
                    b.Append("<a class=\"td-boarding\" href=\"AdminBoarding\"><b>Boarding</b><span>" + string.Join(" and ", parts) + " today</span><em>Open the boarding board &rarr;</em></a>");
                }
            }
            StringBuilder e = new StringBuilder();
            foreach (TdAppt a in appts)
            {
                bool late = a.StartAt < now;
                bool act = canE && c.Mine(a.StaffId);
                e.Append("<article class=\"td-card" + (late ? " td-late" : string.Empty) + "\"><div class=\"td-ctop\"><span class=\"td-time\">" + (a.VisitType == "surgery" ? "Drop-off " : string.Empty) + Ui.Hm(a.StartAt) + "</span>" + (late ? "<span class=\"td-tag td-tag-late\">Late</span>" : string.Empty) + (a.NewClient ? "<span class=\"td-tag\">New client</span>" : string.Empty) + StatusChip(a.Status) + "</div>");
                if (a.VisitType == "surgery" && a.ProcAt.HasValue)
                {
                    e.Append("<p class=\"td-sub\">Procedure at " + Ui.Hm(a.ProcAt.Value) + "</p>");
                }
                e.Append(PetLine(c, a.PetId, a.PetName, a.Species, a.Alert));
                e.Append(OwnerLine(c, a.ClientId, a.ClientName, a.Phone));
                e.Append("<p class=\"td-meta\">" + Ui.Enc(a.BookName) + " &middot; " + Who(a.StaffName) + "</p>");
                StringBuilder acts = new StringBuilder();
                string id = Ui.Js(a.ApptId);
                string pet = Ui.Js(a.PetName);
                if (act)
                {
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-accent\" onclick=\"AdminTodayJs.checkIn('" + id + "')\">Check in</button>");
                    if (a.Status == "booked")
                    {
                        acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-ghost\" onclick=\"AdminTodayJs.confirmAppt('" + id + "')\">Confirm</button>");
                    }
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-danger\" onclick=\"AdminTodayJs.noShow('" + id + "','" + pet + "')\">No-show</button>");
                }
                if (c.Can("appts", 'V'))
                {
                    acts.Append("<a class=\"td-btn td-btn-sm td-btn-ghost\" href=\"AdminAppts?a=" + Uri.EscapeDataString(a.ApptId) + "\">Details</a>");
                }
                if (acts.Length > 0)
                {
                    e.Append("<div class=\"td-cacts\">" + acts + "</div>");
                }
                e.Append("</article>");
            }
            StringBuilder w = new StringBuilder();
            foreach (TdVisit v in waiting)
            {
                bool act = canE && c.Mine(v.StaffId);
                w.Append("<article class=\"td-card\"><div class=\"td-ctop\"><span class=\"td-time\">Waiting " + Mins(v.CheckinAt, now) + "</span><span class=\"td-st td-st-checkedin\">" + Ui.Enc(Kind(v.VisitType)) + "</span></div>");
                w.Append(PetLine(c, v.PetId, v.PetName, v.Species, v.Alert));
                w.Append(OwnerLine(c, v.ClientId, v.ClientName, v.Phone));
                w.Append("<p class=\"td-meta\">" + Ui.Enc(v.Reason != string.Empty ? v.Reason : v.BookName) + " &middot; " + Who(v.StaffName) + "</p>");
                StringBuilder acts = new StringBuilder();
                string id = Ui.Js(v.VisitId);
                if (act)
                {
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-accent\" onclick=\"AdminTodayJs.start('" + id + "')\">Start</button>");
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-ghost\" onclick=\"AdminTodayJs.undo('" + id + "','" + Ui.Js(v.PetName) + "')\">Undo check-in</button>");
                }
                if (visitV)
                {
                    acts.Append("<a class=\"td-btn td-btn-sm td-btn-ghost\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Open visit</a>");
                }
                if (acts.Length > 0)
                {
                    w.Append("<div class=\"td-cacts\">" + acts + "</div>");
                }
                w.Append("</article>");
            }
            StringBuilder r = new StringBuilder();
            foreach (TdVisit v in inroom)
            {
                bool act = canE && c.Mine(v.StaffId);
                DateTime since = v.RoomSince ?? v.StartAt ?? v.CheckinAt;
                r.Append("<article class=\"td-card\"><div class=\"td-ctop\"><span class=\"td-time\">" + Ui.Enc(v.RoomName != string.Empty ? v.RoomName : "No room") + "</span>" + RoomChip(v.Purpose) + "</div>");
                r.Append(PetLine(c, v.PetId, v.PetName, v.Species, v.Alert));
                r.Append(OwnerLine(c, v.ClientId, v.ClientName, v.Phone));
                r.Append("<p class=\"td-meta\">" + Who(v.StaffName) + " &middot; since " + Ui.Hm(since) + " (" + Mins(since, now) + ")</p>");
                StringBuilder acts = new StringBuilder();
                string id = Ui.Js(v.VisitId);
                if (act)
                {
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-ghost\" onclick=\"AdminTodayJs.move('" + id + "')\">Move</button>");
                    acts.Append("<button type=\"button\" class=\"td-btn td-btn-sm td-btn-accent\" onclick=\"AdminTodayJs.toCheckout('" + id + "','" + Ui.Js(v.PetName) + "')\">Ready for checkout</button>");
                }
                if (visitV)
                {
                    acts.Append("<a class=\"td-btn td-btn-sm td-btn-ghost\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Open visit</a>");
                }
                if (acts.Length > 0)
                {
                    r.Append("<div class=\"td-cacts\">" + acts + "</div>");
                }
                r.Append("</article>");
            }
            StringBuilder k = new StringBuilder();
            foreach (TdVisit v in ready)
            {
                k.Append("<article class=\"td-card\"><div class=\"td-ctop\"><span class=\"td-time\">Ready " + Mins(v.ReadyAt ?? v.CheckinAt, now) + "</span><span class=\"td-st td-st-ready\">Checkout</span></div>");
                k.Append(PetLine(c, v.PetId, v.PetName, v.Species, v.Alert));
                k.Append(OwnerLine(c, v.ClientId, v.ClientName, v.Phone));
                k.Append("<p class=\"td-meta\">" + Ui.Enc(v.BookName != string.Empty ? v.BookName : Kind(v.VisitType)) + " &middot; " + Who(v.StaffName) + "</p>");
                StringBuilder acts = new StringBuilder();
                if (c.Can("checkout", 'V'))
                {
                    acts.Append("<a class=\"td-btn td-btn-sm td-btn-accent\" href=\"AdminCheckout?v=" + Uri.EscapeDataString(v.VisitId) + "\">Checkout</a>");
                }
                if (visitV)
                {
                    acts.Append("<a class=\"td-btn td-btn-sm td-btn-ghost\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Open visit</a>");
                }
                if (acts.Length > 0)
                {
                    k.Append("<div class=\"td-cacts\">" + acts + "</div>");
                }
                k.Append("</article>");
            }
            b.Append("<div class=\"td-cols\">");
            b.Append(Col("exp", "Expected", appts.Count, e.ToString(), "No one else is expected today."));
            b.Append(Col("wait", "Waiting", waiting.Count, w.ToString(), "No one is waiting."));
            b.Append(Col("room", "In rooms", inroom.Count, r.ToString(), "All rooms are free."));
            b.Append(Col("ready", "Ready for checkout", ready.Count, k.ToString(), "No one is waiting to pay."));
            b.Append("</div>");
            List<string> lost = new List<string>();
            if (noshow > 0)
            {
                lost.Add(Ui.Plural(noshow, "no-show", "no-shows"));
            }
            if (cancelled > 0)
            {
                lost.Add(cancelled + " cancelled");
            }
            b.Append("<details class=\"td-done\" id=\"td-done\" ontoggle=\"AdminTodayJs.doneToggle(this)\"><summary><b>Done today</b><span class=\"td-n\">" + done.Count + "</span>" + (lost.Count > 0 ? "<small>" + string.Join(" &middot; ", lost) + "</small>" : string.Empty) + "</summary>");
            if (done.Count == 0)
            {
                b.Append("<p class=\"td-empty\">No visits are finished yet today.</p>");
            }
            else
            {
                b.Append("<ul class=\"td-dlist\">");
                foreach (TdVisit v in done)
                {
                    b.Append("<li><span class=\"td-dt\">" + (v.ClosedAt.HasValue ? Ui.Hm(v.ClosedAt.Value) : string.Empty) + "</span><b>" + Ui.Enc(v.PetName) + "</b><span>" + Ui.Enc(v.ClientName) + "</span><span>" + Ui.Enc(v.BookName != string.Empty ? v.BookName : Kind(v.VisitType)) + " &middot; " + Who(v.StaffName) + "</span>" + (visitV ? "<a href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Open</a>" : string.Empty) + "</li>");
                }
                b.Append("</ul>");
            }
            b.Append("</details>");
            string sum = "&middot; " + appts.Count + " expected &middot; " + waiting.Count + " waiting &middot; " + inroom.Count + " in rooms &middot; " + ready.Count + " ready &middot; " + done.Count + " done";
            return (sum, b.ToString());
        }

        private async Task<Ctx?> Editor(ApiResponse response, string what)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return null;
            }
            if (!c.Can("today", 'V') || !c.Can("today", 'E'))
            {
                await App.Log(c.U.StaffId, what, "not allowed", false);
                response.ExecuteScript("AdminTodayJs.closeModal();AdminTodayJs.toast('You can look at the board but not change it.');");
                return null;
            }
            return c;
        }

        private static async Task<TdAppt?> ApptInfo(string apptId)
        {
            var (row, emsg) = await Helper.GetOneT<TdAppt>("SELECT A.APPT_ID AS ApptId, A.STAFF_ID AS StaffId, A.STATUS AS Status, A.VISIT_TYPE AS VisitType, ISNULL(P.PET_NAME, N'') AS PetName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.APPT_ID = @aid", new List<SqlParameter> { App.P("aid", apptId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<TdVisit?> VisitInfo(string visitId)
        {
            var (row, emsg) = await Helper.GetOneT<TdVisit>(VisitSql + " WHERE V.VISIT_ID = @vid", new List<SqlParameter> { App.P("vid", visitId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private async Task<ApiResponse> ApptAction(string what, string status)
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, what);
            if (c == null)
            {
                return response;
            }
            TdAppt? a = await ApptInfo(Val("apptid", 50));
            if (a == null || a.VisitType == "boarding")
            {
                await Redraw(response, c, "This appointment was not found.", true);
                return response;
            }
            if (!c.Mine(a.StaffId))
            {
                await App.Log(c.U.StaffId, what, a.PetName + " · not your patient", false);
                await Redraw(response, c, "You can only change your own patients.", true);
                return response;
            }
            string err;
            string ok;
            if (status == "checkin")
            {
                var (_, cerr) = await Flow.CheckIn(c, a.ApptId, await App.Settings());
                err = cerr;
                ok = a.PetName + " is checked in and waiting.";
            }
            else
            {
                err = await Flow.SetApptStatus(c, a.ApptId, status, string.Empty);
                ok = status == "noshow" ? a.PetName + " is marked as a no-show." : a.PetName + "'s appointment is confirmed.";
            }
            await Redraw(response, c, err != string.Empty ? err : ok, true);
            return response;
        }

        public async Task<ApiResponse> CheckIn()
        {
            return await ApptAction("check in", "checkin");
        }

        public async Task<ApiResponse> NoShow()
        {
            return await ApptAction("no-show", "noshow");
        }

        public async Task<ApiResponse> Confirm()
        {
            return await ApptAction("confirm appointment", "confirmed");
        }

        public async Task<ApiResponse> Undo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "undo check in");
            if (c == null)
            {
                return response;
            }
            FlowVisit? v = await Flow.Visit(Val("visitid", 50));
            if (v == null || v.VisitType == "boarding")
            {
                await Redraw(response, c, "This visit was not found.", true);
                return response;
            }
            if (!c.Mine(v.StaffId))
            {
                await App.Log(c.U.StaffId, "undo check in", v.PetName + " · not your patient", false);
                await Redraw(response, c, "You can only change your own patients.", true);
                return response;
            }
            string err = await Flow.UndoCheckIn(c, v.VisitId);
            await Redraw(response, c, err != string.Empty ? err : "Check-in undone for " + v.PetName + ".", true);
            return response;
        }

        public async Task<ApiResponse> Ready()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "ready for checkout");
            if (c == null)
            {
                return response;
            }
            FlowVisit? v = await Flow.Visit(Val("visitid", 50));
            if (v == null || v.VisitType == "boarding")
            {
                await Redraw(response, c, "This visit was not found.", true);
                return response;
            }
            if (!c.Mine(v.StaffId))
            {
                await App.Log(c.U.StaffId, "ready for checkout", v.PetName + " · not your patient", false);
                await Redraw(response, c, "You can only change your own patients.", true);
                return response;
            }
            string err = await Flow.Ready(c, v.VisitId, await App.Settings());
            await Redraw(response, c, err != string.Empty ? err : v.PetName + " is ready for checkout.", true);
            return response;
        }

        private static List<StaffPick> Choosable(Ctx c, List<StaffPick> staff)
        {
            return c.U.OwnOnly ? staff.Where(x => x.StaffId == c.U.StaffId).ToList() : staff;
        }

        private static string StaffOptions(List<StaffPick> staff, string cur, string blank)
        {
            StringBuilder sb = new StringBuilder();
            if (blank != string.Empty)
            {
                sb.Append("<option value=\"\" data-home=\"\">" + Ui.Enc(blank) + "</option>");
            }
            foreach (StaffPick p in staff)
            {
                string role = p.ProviderType switch { "vet" => "Vet", "tech" => "Technician", "groomer" => "Groomer", _ => string.Empty };
                sb.Append("<option value=\"" + Ui.Attr(p.StaffId) + "\" data-home=\"" + Ui.Attr(p.HomeRoomId) + "\"" + (p.StaffId == cur ? " selected" : string.Empty) + ">" + Ui.Enc(p.StaffName) + (role != string.Empty ? " (" + role + ")" : string.Empty) + "</option>");
            }
            return sb.ToString();
        }

        private static async Task<List<TdRoom>> Rooms()
        {
            var (list, emsg) = await Helper.GetListT<TdRoom>(RoomSql);
            return emsg == string.Empty && list != null ? list.GroupBy(x => x.RoomId).Select(g => g.First()).ToList() : new List<TdRoom>();
        }

        private static async Task<string> RoomModal(Ctx c, TdVisit v, bool move, string pickRoom, string pickStaff, string err)
        {
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            List<TdRoom> rooms = await Rooms();
            List<StaffPick> staff = Choosable(c, await Flow.Providers());
            string cur = pickStaff != string.Empty && staff.Any(x => x.StaffId == pickStaff) ? pickStaff : staff.Any(x => x.StaffId == v.StaffId) ? v.StaffId : staff.Any(x => x.StaffId == c.U.StaffId) ? c.U.StaffId : string.Empty;
            string room = pickRoom;
            if (room == string.Empty && !move)
            {
                string home = staff.FirstOrDefault(x => x.StaffId == cur)?.HomeRoomId ?? string.Empty;
                TdRoom? h = rooms.FirstOrDefault(x => x.RoomId == home && x.BusyVisit == string.Empty);
                if (h != null && (v.RoomtypeId == string.Empty || h.RoomtypeId == v.RoomtypeId))
                {
                    room = h.RoomId;
                }
                else
                {
                    TdRoom? t = rooms.FirstOrDefault(x => x.RoomtypeId == v.RoomtypeId && v.RoomtypeId != string.Empty && x.BusyVisit == string.Empty);
                    room = t != null ? t.RoomId : h != null ? h.RoomId : string.Empty;
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (move ? "Move " : "Start visit &middot; ") + Ui.Enc(v.PetName) + "</h2>");
            string lead = move
                ? "Now in " + Ui.Enc(v.RoomName != string.Empty ? v.RoomName : "no room") + " with " + Who(v.StaffName) + "."
                : Ui.Enc(v.Species) + " &middot; " + Ui.Enc(v.Reason != string.Empty ? v.Reason : Kind(v.VisitType)) + " &middot; waiting " + Ui.Enc(MinText(Math.Max(0, (int)(now - v.CheckinAt).TotalMinutes)));
            sb.Append("<p class=\"td-mlead\">" + lead + "</p>");
            if (v.Alert.Trim() != string.Empty)
            {
                sb.Append("<p class=\"td-alert\"><i aria-hidden=\"true\">!</i><span>" + Ui.Enc(v.Alert) + "</span></p>");
            }
            sb.Append("<em class=\"td-err td-merr\" id=\"td-e-room\">" + Ui.Enc(err) + "</em>");
            sb.Append("<label class=\"td-field td-mstaff\"><span>" + (move ? "Who looks after the patient" : "Who sees the patient") + "</span><select id=\"td-mstaff\" onchange=\"AdminTodayJs.staffPick()\">" + StaffOptions(staff, cur, cur == string.Empty ? "Pick a person" : string.Empty) + "</select></label>");
            sb.Append("<h3>Room</h3><div class=\"td-rgroups\">");
            foreach (IGrouping<string, TdRoom> g in rooms.GroupBy(x => x.RoomtypeId).OrderBy(g => g.First().TypeOrder))
            {
                sb.Append("<fieldset class=\"td-rg\"><legend>" + Ui.Enc(g.First().TypeName) + "</legend><div class=\"td-rlist\">");
                foreach (TdRoom x in g.OrderBy(x => x.RoomOrder).ThenBy(x => x.RoomName))
                {
                    bool here = x.BusyVisit == v.VisitId;
                    bool busy = x.BusyVisit != string.Empty;
                    string note = here ? "Here now" : busy ? x.BusyPet + (x.BusyStaff != string.Empty ? " with " + x.BusyStaff : string.Empty) : x.RoomtypeId == v.RoomtypeId && v.RoomtypeId != string.Empty ? "Free · booked room type" : "Free";
                    sb.Append("<label class=\"td-room" + (busy ? " td-busy" : string.Empty) + "\"><input type=\"radio\" name=\"td-room\" value=\"" + Ui.Attr(x.RoomId) + "\" onchange=\"AdminTodayJs.roomPick()\"" + (busy ? " disabled" : string.Empty) + (!busy && x.RoomId == room ? " checked" : string.Empty) + "><span><b>" + Ui.Enc(x.RoomName) + "</b><small>" + Ui.Enc(note) + "</small></span></label>");
                }
                sb.Append("</div></fieldset>");
            }
            sb.Append("</div><div class=\"td-mfoot\"><button type=\"button\" class=\"td-btn td-btn-ghost\" onclick=\"AdminTodayJs.closeModal()\">Cancel</button><button type=\"button\" class=\"td-btn td-btn-accent\" onclick=\"AdminTodayJs." + (move ? "moveGo" : "startGo") + "('" + Ui.Js(v.VisitId) + "')\">" + (move ? "Move patient" : "Start visit") + "</button></div>");
            return sb.ToString();
        }

        private async Task<ApiResponse> Pick(bool move)
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, move ? "move room" : "start visit");
            if (c == null)
            {
                return response;
            }
            TdVisit? v = await VisitInfo(Val("visitid", 50));
            if (v == null || v.VisitType == "boarding" || v.Status != (move ? "inroom" : "waiting"))
            {
                await Redraw(response, c, move ? "Only a visit in a room can be moved." : "Only a waiting visit can be started.", true);
                return response;
            }
            if (!c.Mine(v.StaffId))
            {
                await Redraw(response, c, "You can only change your own patients.", true);
                return response;
            }
            response.SetElementContents("td-mbody", await RoomModal(c, v, move, string.Empty, string.Empty, string.Empty));
            response.ExecuteScript("AdminTodayJs.shown();");
            return response;
        }

        public async Task<ApiResponse> StartPick()
        {
            return await Pick(false);
        }

        public async Task<ApiResponse> MovePick()
        {
            return await Pick(true);
        }

        private async Task<ApiResponse> Go(bool move)
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, move ? "move room" : "start visit");
            if (c == null)
            {
                return response;
            }
            TdVisit? v = await VisitInfo(Val("visitid", 50));
            if (v == null || v.VisitType == "boarding")
            {
                await Redraw(response, c, "This visit was not found.", true);
                return response;
            }
            if (!c.Mine(v.StaffId))
            {
                await App.Log(c.U.StaffId, move ? "move room" : "start visit", v.PetName + " · not your patient", false);
                await Redraw(response, c, "You can only change your own patients.", true);
                return response;
            }
            string roomId = Val("roomid", 50);
            string staffId = Val("staffid", 50);
            if (c.U.OwnOnly && staffId != c.U.StaffId)
            {
                staffId = c.U.StaffId;
            }
            string err = roomId == string.Empty ? "Pick a room." : staffId == string.Empty ? "Pick who looks after the patient." : string.Empty;
            if (err == string.Empty)
            {
                Dictionary<string, string> s = await App.Settings();
                err = move ? await Flow.Move(c, v.VisitId, roomId, staffId, s) : await Flow.Start(c, v.VisitId, roomId, staffId, s);
            }
            if (err != string.Empty)
            {
                TdVisit? fresh = await VisitInfo(v.VisitId);
                if (fresh != null && fresh.Status == (move ? "inroom" : "waiting"))
                {
                    response.SetElementContents("td-mbody", await RoomModal(c, fresh, move, roomId, staffId, err));
                    await Redraw(response, c, string.Empty, false);
                    return response;
                }
                await Redraw(response, c, err, true);
                return response;
            }
            TdVisit? after = await VisitInfo(v.VisitId);
            string where = after != null && after.RoomName != string.Empty ? after.RoomName : "the room";
            await Redraw(response, c, v.PetName + (move ? " moved to " : " is in ") + where + (after != null && after.StaffName != string.Empty ? " with " + after.StaffName : string.Empty) + ".", true);
            return response;
        }

        public async Task<ApiResponse> StartGo()
        {
            return await Go(false);
        }

        public async Task<ApiResponse> MoveGo()
        {
            return await Go(true);
        }

        public async Task<ApiResponse> WalkOpen()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "walk-in");
            if (c == null)
            {
                return response;
            }
            List<StaffPick> staff = Choosable(c, await Flow.Providers());
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Walk-in</h2><p class=\"td-mlead\">Find the pet by its name, the owner's name or phone number.</p>");
            sb.Append("<label class=\"td-field\"><span>Search</span><input type=\"search\" id=\"td-wq\" maxlength=\"60\" autocomplete=\"off\" placeholder=\"Pet name, owner or phone\" oninput=\"AdminTodayJs.find()\"></label>");
            sb.Append("<div class=\"td-wres\" id=\"td-wres\"><p class=\"td-hint\">Type at least 2 letters or 3 digits.</p></div>");
            if (c.Can("clients", 'A'))
            {
                sb.Append("<p class=\"td-hint\">Not in the system yet? <a class=\"td-link\" href=\"AdminClients?n=1\">Add a new client</a> first.</p>");
            }
            sb.Append("<div class=\"td-wpick\" id=\"td-wpick\" hidden></div>");
            sb.Append("<div class=\"td-wgrid\"><label class=\"td-field\"><span>Kind of visit</span><select id=\"td-wkind\"><option value=\"clinic\">Clinic visit</option><option value=\"surgery\">Surgery</option><option value=\"grooming\">Grooming</option></select></label>");
            sb.Append("<label class=\"td-field\"><span>Who sees the pet</span><select id=\"td-wstaff\">" + StaffOptions(staff, c.U.OwnOnly ? c.U.StaffId : string.Empty, c.U.OwnOnly ? string.Empty : "Anyone (decide later)") + "</select></label></div>");
            sb.Append("<label class=\"td-field\"><span>Reason</span><input type=\"text\" id=\"td-wwhy\" maxlength=\"300\" placeholder=\"For example: limping since this morning\"></label>");
            sb.Append("<em class=\"td-err td-merr\" id=\"td-e-walk\"></em>");
            sb.Append("<div class=\"td-mfoot\"><button type=\"button\" class=\"td-btn td-btn-ghost\" onclick=\"AdminTodayJs.closeModal()\">Cancel</button><button type=\"button\" class=\"td-btn td-btn-accent\" onclick=\"AdminTodayJs.walkGo()\">Check in walk-in</button></div>");
            response.SetElementContents("td-mbody", sb.ToString());
            response.ExecuteScript("AdminTodayJs.shown();AdminTodayJs.walkReady();");
            return response;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "walk-in search");
            if (c == null)
            {
                return response;
            }
            string term = Ui.OneLine(GetDataValue("term"), 60);
            string dig = Ui.Digits(term);
            if (term.Length < 2 && dig.Length < 3)
            {
                response.SetElementContents("td-wres", "<p class=\"td-hint\">Type at least 2 letters or 3 digits.</p>");
                return response;
            }
            string like = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
            string phone = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N'')";
            var (list, emsg) = await Helper.GetListT<TdPet>("SELECT TOP 15 P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS Species, P.BREED AS Breed, P.ALERT AS Alert, C.FIRST_NAME + N' ' + C.LAST_NAME AS ClientName, C.PHONE AS Phone, (SELECT COUNT(*) FROM VISIT V WHERE V.PET_ID = P.PET_ID AND V.STATUS IN (N'waiting', N'inroom', N'checkout') AND V.VISIT_TYPE <> N'boarding') AS OpenVisits FROM PET P JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY WHERE P.ACTIVE = 1 AND C.ACTIVE = 1 AND P.DECEASED_DATE IS NULL AND (P.PET_NAME LIKE @q ESCAPE N'\\' OR C.FIRST_NAME LIKE @q ESCAPE N'\\' OR C.LAST_NAME LIKE @q ESCAPE N'\\' OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @q ESCAPE N'\\' OR (@dig <> N'' AND " + phone + " LIKE @dig)) ORDER BY P.PET_NAME, C.LAST_NAME, C.FIRST_NAME", new List<SqlParameter> { App.P("q", like, 200), App.P("dig", dig.Length >= 3 ? "%" + dig + "%" : string.Empty, 40) });
            if (emsg != string.Empty || list == null)
            {
                response.SetElementContents("td-wres", "<p class=\"td-hint\">The search did not work. Try again.</p>");
                return response;
            }
            if (list.Count == 0)
            {
                response.SetElementContents("td-wres", "<p class=\"td-hint\">No pets match &ldquo;" + Ui.Enc(term) + "&rdquo;.</p>");
                return response;
            }
            StringBuilder sb = new StringBuilder("<ul class=\"td-plist\">");
            foreach (TdPet p in list)
            {
                string label = p.PetName + " (" + p.Species + ") · " + p.ClientName;
                bool here = p.OpenVisits > 0;
                sb.Append("<li><button type=\"button\" class=\"td-pbtn\"" + (here ? " disabled" : " onclick=\"AdminTodayJs.pickPet('" + Ui.Js(p.PetId) + "','" + Ui.Js(label) + "')\"") + "><b>" + Ui.Enc(p.PetName) + "</b><small>" + Ui.Enc(p.Species + (p.Breed != string.Empty ? " · " + p.Breed : string.Empty)) + "</small><span>" + Ui.Enc(p.ClientName) + (p.Phone != string.Empty ? " · " + Ui.Enc(p.Phone) : string.Empty) + "</span>" + (here ? "<em class=\"td-tag\">Already here</em>" : string.Empty) + (p.Alert.Trim() != string.Empty ? "<em class=\"td-tag td-tag-late\">" + Ui.Enc(p.Alert) + "</em>" : string.Empty) + "</button></li>");
            }
            sb.Append("</ul>");
            response.SetElementContents("td-wres", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> WalkGo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "walk-in");
            if (c == null)
            {
                return response;
            }
            string petId = Val("petid", 50);
            string kind = Val("kind", 20);
            string staffId = Val("staffid", 50);
            string reason = Ui.OneLine(GetDataValue("reason"), 300);
            if (c.U.OwnOnly)
            {
                staffId = c.U.StaffId;
            }
            if (staffId != string.Empty && !(await Flow.Providers()).Any(x => x.StaffId == staffId))
            {
                staffId = string.Empty;
            }
            if (petId == string.Empty)
            {
                response.SetElementContents("td-e-walk", "Pick a pet from the search results first.");
                return response;
            }
            var (vid, err) = await Flow.WalkIn(c, petId, kind, staffId, reason, await App.Settings());
            if (err != string.Empty)
            {
                response.SetElementContents("td-e-walk", Ui.Enc(err));
                return response;
            }
            FlowVisit? v = await Flow.Visit(vid);
            await Redraw(response, c, (v != null ? v.PetName : "The pet") + " is checked in as a walk-in.", true);
            return response;
        }
    }
}
