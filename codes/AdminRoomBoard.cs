using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminRoomBoard : WebPage
    {
        private const string RoomSql = "SELECT R.ROOM_ID AS RoomId, R.ROOM_NAME AS RoomName, R.NOTE AS Note, T.ROOMTYPE_ID AS RoomtypeId, T.TYPE_NAME AS TypeName, T.PURPOSE AS Purpose, T.COLOR AS Color, T.TYPE_ORDER AS TypeOrder, R.ROOM_ORDER AS RoomOrder, ISNULL(X.VISIT_ID, N'') AS VisitId, X.IN_AT AS InAt, ISNULL(X.STAFF_ID, N'') AS RoomStaffId, ISNULL(RS.STAFF_NAME, N'') AS RoomStaffName, ISNULL(V.VISIT_TYPE, N'') AS VisitType, ISNULL(V.STAFF_ID, N'') AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.CHECKIN_AT AS CheckinAt, ISNULL(V.REASON, N'') AS Reason, ISNULL(P.PET_ID, N'') AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(SP.SPECIES_NAME, ISNULL(P.SPECIES_KEY, N'')) AS Species, ISNULL(P.ALERT, N'') AS Alert, ISNULL(C.CLIENT_ID, N'') AS ClientId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS ClientName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(B.BOOK_NAME, N'') AS BookName FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID LEFT JOIN VISITROOM X ON X.ROOM_ID = R.ROOM_ID AND X.OUT_AT IS NULL LEFT JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN STAFF RS ON RS.STAFF_ID = X.STAFF_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID AND V.APPT_ID <> N'' LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE R.ACTIVE = 1 AND T.ACTIVE = 1 AND T.PURPOSE <> N'boarding' ORDER BY T.TYPE_ORDER, R.ROOM_ORDER, R.ROOM_NAME";
        private const string WaitSql = "SELECT V.VISIT_ID AS VisitId, V.VISIT_TYPE AS VisitType, V.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.CHECKIN_AT AS CheckinAt, V.REASON AS Reason, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS Species, P.ALERT AS Alert, C.FIRST_NAME + N' ' + C.LAST_NAME AS ClientName, ISNULL(A.ROOMTYPE_ID, N'') AS RoomtypeId, ISNULL(B.BOOK_NAME, N'') AS BookName FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID AND V.APPT_ID <> N'' LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE V.STATUS = N'waiting' AND V.VISIT_TYPE <> N'boarding' ORDER BY V.CHECKIN_AT";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Room board | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--rb-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("rb"));
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRoomBoardJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("rb-menu", Auth.Menu("rb", "AdminRoomBoardJs", "AdminRoomBoard", c));
            response.SetElementContents("rb-who", Auth.Who("rb", c));
            response.ExecuteScript("AdminRoomBoardJs.secs();");
        }

        private static string Denied()
        {
            return "<div class=\"rb-panel rb-deny\"><h1>You don't have access to this page</h1><p>The room board is for the front desk and clinical team. Ask a manager to open it for you in Staff and permissions.</p><a class=\"rb-btn rb-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminRoomBoardJs.out();");
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
            if (!c.Can("roomboard", 'V'))
            {
                response.SetElementContents("rb-main", Denied());
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rb-ptop\"><div><h1>Room board</h1><p>Every room right now. The board updates by itself every 30 seconds. <button type=\"button\" class=\"rb-link\" onclick=\"AdminRoomBoardJs.refresh()\">Refresh now</button></p></div><div class=\"rb-acts\">");
            if (c.Can("today", 'V'))
            {
                sb.Append("<a class=\"rb-btn rb-btn-ghost\" href=\"AdminToday\">Today's board</a>");
            }
            sb.Append("</div></div><div id=\"rb-board\">" + await Build(c) + "</div>");
            response.SetElementContents("rb-main", sb.ToString());
            response.ExecuteScript("AdminRoomBoardJs.ready();");
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
            if (!c.Can("roomboard", 'V'))
            {
                response.SetElementContents("rb-main", Denied());
                return response;
            }
            await Redraw(response, c, string.Empty, false);
            return response;
        }

        private static async Task Redraw(ApiResponse response, Ctx c, string message, bool close)
        {
            response.SetElementContents("rb-board", await Build(c));
            response.ExecuteScript((close ? "AdminRoomBoardJs.closeModal();" : string.Empty) + "AdminRoomBoardJs.drawn();" + (message != string.Empty ? "AdminRoomBoardJs.toast('" + Ui.Js(message) + "');" : string.Empty));
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
            return "<span class=\"rb-mins\" data-min=\"" + m + "\">" + Ui.Enc(MinText(m)) + "</span>";
        }

        private static string Kind(string visitType)
        {
            return visitType switch { "surgery" => "Surgery", "grooming" => "Grooming", _ => "Clinic visit" };
        }

        private static string Chip(RbRoom r)
        {
            if (r.VisitId == string.Empty)
            {
                return "<span class=\"rb-st rb-st-on\">Free</span>";
            }
            return r.Purpose switch
            {
                "surgery" => "<span class=\"rb-st rb-st-insurgery\">In surgery</span>",
                "recovery" => "<span class=\"rb-st rb-st-recovering\">Recovering</span>",
                "grooming" => "<span class=\"rb-st rb-st-inroom\">Grooming</span>",
                "emergency" => "<span class=\"rb-st rb-st-noshow\">Emergency</span>",
                _ => "<span class=\"rb-st rb-st-inroom\">In use</span>"
            };
        }

        private static async Task<List<RbRoom>> Rooms()
        {
            var (list, emsg) = await Helper.GetListT<RbRoom>(RoomSql);
            return emsg == string.Empty && list != null ? list.GroupBy(x => x.RoomId).Select(g => g.First()).ToList() : new List<RbRoom>();
        }

        private static async Task<List<RbWait>> Waiting()
        {
            var (list, emsg) = await Helper.GetListT<RbWait>(WaitSql);
            return emsg == string.Empty && list != null ? list : new List<RbWait>();
        }

        private static async Task<string> Build(Ctx c)
        {
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            List<RbRoom> rooms = await Rooms();
            List<RbWait> waiting = (await Waiting()).Where(w => c.Mine(w.StaffId)).ToList();
            bool canE = c.Can("roomboard", 'E');
            bool visitV = c.Can("visits", 'V');
            bool canStart = canE && waiting.Any(w => c.Mine(w.StaffId));
            int used = rooms.Count(r => r.VisitId != string.Empty);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rb-sum\"><span class=\"rb-chip rb-chip-use\"><b>" + used + "</b> in use</span><span class=\"rb-chip rb-chip-free\"><b>" + (rooms.Count - used) + "</b> free</span><span class=\"rb-chip rb-chip-wait\"><b>" + waiting.Count + "</b> waiting</span></div>");
            sb.Append("<div class=\"rb-layout\"><aside class=\"rb-wait\" aria-label=\"Waiting patients\"><header class=\"rb-wh\"><h2>Waiting</h2><span class=\"rb-n\">" + waiting.Count + "</span></header>");
            if (waiting.Count == 0)
            {
                sb.Append("<p class=\"rb-empty\">No one is waiting.</p>");
            }
            else
            {
                sb.Append("<ul class=\"rb-wlist\">");
                foreach (RbWait w in waiting)
                {
                    sb.Append("<li><div class=\"rb-wtop\"><b>" + Ui.Enc(w.PetName) + "</b> <small>" + Ui.Enc(w.Species) + "</small><span class=\"rb-wmin\">" + Mins(w.CheckinAt, now) + "</span></div>" + (w.Alert.Trim() != string.Empty ? "<p class=\"rb-alert\"><i aria-hidden=\"true\">!</i><span>" + Ui.Enc(w.Alert) + "</span></p>" : string.Empty) + "<p>" + Ui.Enc(w.ClientName) + "</p><p class=\"rb-meta\">" + Ui.Enc(w.Reason != string.Empty ? w.Reason : Kind(w.VisitType)) + " &middot; " + (w.StaffName != string.Empty ? Ui.Enc(w.StaffName) : "Any staff") + "</p></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</aside><div class=\"rb-groups\">");
            foreach (IGrouping<string, RbRoom> g in rooms.GroupBy(x => x.RoomtypeId).OrderBy(g => g.First().TypeOrder))
            {
                RbRoom first = g.First();
                int free = g.Count(x => x.VisitId == string.Empty);
                string color = " style=\"--rb-n:" + Math.Min(g.Count(), 4) + (Ui.SafeColor(first.Color) ? ";--rb-c:" + first.Color : string.Empty) + "\"";
                sb.Append("<section class=\"rb-grp\"" + color + "><header class=\"rb-gh\"><h2><i aria-hidden=\"true\"></i>" + Ui.Enc(first.TypeName) + "</h2><span>" + free + " of " + g.Count() + " free</span></header><div class=\"rb-rooms\">");
                foreach (RbRoom r in g.OrderBy(x => x.RoomOrder).ThenBy(x => x.RoomName))
                {
                    bool busy = r.VisitId != string.Empty;
                    sb.Append("<article class=\"rb-room" + (busy ? " rb-busy" : " rb-free") + "\"><div class=\"rb-rtop\"><h3>" + Ui.Enc(r.RoomName) + "</h3>" + Chip(r) + "</div>");
                    StringBuilder acts = new StringBuilder();
                    if (!busy)
                    {
                        sb.Append("<p class=\"rb-meta\">" + Ui.Enc(r.Note != string.Empty ? r.Note : "Ready for the next patient") + "</p>");
                        if (canStart)
                        {
                            acts.Append("<button type=\"button\" class=\"rb-btn rb-btn-sm rb-btn-accent\" onclick=\"AdminRoomBoardJs.startHere('" + Ui.Js(r.RoomId) + "')\">Start here</button>");
                        }
                    }
                    else if (!c.Mine(r.StaffId))
                    {
                        sb.Append("<p class=\"rb-meta\">In use" + (r.StaffName != string.Empty ? " &middot; " + Ui.Enc(r.StaffName) : string.Empty) + "</p>");
                    }
                    else
                    {
                        string pet = c.Can("pets", 'V') ? "<a href=\"AdminPets?p=" + Uri.EscapeDataString(r.PetId) + "\">" + Ui.Enc(r.PetName) + "</a>" : Ui.Enc(r.PetName);
                        sb.Append("<p class=\"rb-pet\"><b>" + pet + "</b>" + (r.Species != string.Empty ? " <small>" + Ui.Enc(r.Species) + "</small>" : string.Empty) + "</p>");
                        if (r.Alert.Trim() != string.Empty)
                        {
                            sb.Append("<p class=\"rb-alert\"><i aria-hidden=\"true\">!</i><span>" + Ui.Enc(r.Alert) + "</span></p>");
                        }
                        sb.Append("<p class=\"rb-own\">" + Ui.Enc(r.ClientName) + "</p>");
                        string who = r.StaffName != string.Empty ? Ui.Enc(r.StaffName) : "Any staff";
                        if (r.RoomStaffName != string.Empty && r.RoomStaffId != r.StaffId)
                        {
                            who += " &middot; with " + Ui.Enc(r.RoomStaffName);
                        }
                        sb.Append("<p class=\"rb-meta\">" + Ui.Enc(r.BookName != string.Empty ? r.BookName : Kind(r.VisitType)) + " &middot; " + who + "</p>");
                        if (r.InAt.HasValue)
                        {
                            sb.Append("<p class=\"rb-time\">In since " + Ui.Hm(r.InAt.Value) + " &middot; " + Mins(r.InAt.Value, now) + "</p>");
                        }
                        if (r.CheckinAt.HasValue)
                        {
                            sb.Append("<p class=\"rb-meta\">Visit total " + Mins(r.CheckinAt.Value, now) + "</p>");
                        }
                        string id = Ui.Js(r.VisitId);
                        if (canE && c.Mine(r.StaffId))
                        {
                            acts.Append("<button type=\"button\" class=\"rb-btn rb-btn-sm rb-btn-ghost\" onclick=\"AdminRoomBoardJs.move('" + id + "')\">Move</button>");
                            acts.Append("<button type=\"button\" class=\"rb-btn rb-btn-sm rb-btn-accent\" onclick=\"AdminRoomBoardJs.toCheckout('" + id + "','" + Ui.Js(r.PetName) + "')\">Ready for checkout</button>");
                        }
                        if (visitV)
                        {
                            acts.Append("<a class=\"rb-btn rb-btn-sm rb-btn-ghost\" href=\"AdminVisit?v=" + Uri.EscapeDataString(r.VisitId) + "\">Open visit</a>");
                        }
                    }
                    if (acts.Length > 0)
                    {
                        sb.Append("<div class=\"rb-racts\">" + acts + "</div>");
                    }
                    sb.Append("</article>");
                }
                sb.Append("</div></section>");
            }
            if (rooms.Count == 0)
            {
                sb.Append("<div class=\"rb-panel\"><p>No rooms are set up yet. A manager can add them in Rooms and services.</p></div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private async Task<Ctx?> Editor(ApiResponse response, string what)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return null;
            }
            if (!c.Can("roomboard", 'V') || !c.Can("roomboard", 'E'))
            {
                await App.Log(c.U.StaffId, what, "not allowed", false);
                response.ExecuteScript("AdminRoomBoardJs.closeModal();AdminRoomBoardJs.toast('You can look at the room board but not change it.');");
                return null;
            }
            return c;
        }

        private static List<StaffPick> Choosable(Ctx c, List<StaffPick> staff)
        {
            return c.U.OwnOnly ? staff.Where(x => x.StaffId == c.U.StaffId).ToList() : staff;
        }

        private static string StaffOptions(List<StaffPick> staff, string cur)
        {
            StringBuilder sb = new StringBuilder();
            if (!staff.Any(x => x.StaffId == cur))
            {
                sb.Append("<option value=\"\">Pick a person</option>");
            }
            foreach (StaffPick p in staff)
            {
                string role = p.ProviderType switch { "vet" => "Vet", "tech" => "Technician", "groomer" => "Groomer", _ => string.Empty };
                sb.Append("<option value=\"" + Ui.Attr(p.StaffId) + "\"" + (p.StaffId == cur ? " selected" : string.Empty) + ">" + Ui.Enc(p.StaffName) + (role != string.Empty ? " (" + role + ")" : string.Empty) + "</option>");
            }
            return sb.ToString();
        }

        private static async Task<string> StartModal(Ctx c, RbRoom room, string pickVisit, string pickStaff, string err)
        {
            DateTime now = App.Now(await App.Settings());
            List<RbWait> waiting = (await Waiting()).Where(w => c.Mine(w.StaffId)).ToList();
            List<StaffPick> staff = Choosable(c, await Flow.Providers());
            string vid = pickVisit != string.Empty && waiting.Any(w => w.VisitId == pickVisit) ? pickVisit : waiting.Count == 1 ? waiting[0].VisitId : string.Empty;
            RbWait? chosen = waiting.FirstOrDefault(w => w.VisitId == vid);
            string cur = pickStaff;
            if (cur == string.Empty && chosen != null && staff.Any(x => x.StaffId == chosen.StaffId))
            {
                cur = chosen.StaffId;
            }
            if (cur == string.Empty)
            {
                StaffPick? home = staff.FirstOrDefault(x => x.HomeRoomId == room.RoomId);
                cur = home != null ? home.StaffId : staff.Any(x => x.StaffId == c.U.StaffId) ? c.U.StaffId : string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Start a visit in " + Ui.Enc(room.RoomName) + "</h2><p class=\"rb-mlead\">" + Ui.Enc(room.TypeName) + " &middot; pick the waiting patient and who sees them.</p>");
            sb.Append("<em class=\"rb-err rb-merr\" id=\"rb-e-modal\">" + Ui.Enc(err) + "</em>");
            if (waiting.Count == 0)
            {
                sb.Append("<p class=\"rb-empty\">No one is waiting right now.</p><div class=\"rb-mfoot\"><button type=\"button\" class=\"rb-btn rb-btn-ghost\" onclick=\"AdminRoomBoardJs.closeModal()\">Close</button></div>");
                return sb.ToString();
            }
            sb.Append("<h3>Waiting patient</h3><div class=\"rb-plist\">");
            foreach (RbWait w in waiting)
            {
                bool match = w.RoomtypeId != string.Empty && w.RoomtypeId == room.RoomtypeId;
                sb.Append("<label class=\"rb-opt\"><input type=\"radio\" name=\"rb-wv\" value=\"" + Ui.Attr(w.VisitId) + "\" data-staff=\"" + Ui.Attr(w.StaffId) + "\" onchange=\"AdminRoomBoardJs.wvPick(this)\"" + (w.VisitId == vid ? " checked" : string.Empty) + "><span><b>" + Ui.Enc(w.PetName) + " <small>" + Ui.Enc(w.Species) + "</small></b><small>" + Ui.Enc(w.ClientName) + " &middot; " + Ui.Enc(w.Reason != string.Empty ? w.Reason : Kind(w.VisitType)) + " &middot; " + (w.StaffName != string.Empty ? Ui.Enc(w.StaffName) : "Any staff") + " &middot; waiting " + Ui.Enc(MinText(Math.Max(0, (int)(now - w.CheckinAt).TotalMinutes))) + "</small>" + (match ? "<em class=\"rb-tag\">Booked for this room type</em>" : string.Empty) + (w.Alert.Trim() != string.Empty ? "<em class=\"rb-tag rb-tag-warn\">" + Ui.Enc(w.Alert) + "</em>" : string.Empty) + "</span></label>");
            }
            sb.Append("</div><label class=\"rb-field rb-mstaff\"><span>Who sees the patient</span><select id=\"rb-mstaff\">" + StaffOptions(staff, cur) + "</select></label>");
            sb.Append("<div class=\"rb-mfoot\"><button type=\"button\" class=\"rb-btn rb-btn-ghost\" onclick=\"AdminRoomBoardJs.closeModal()\">Cancel</button><button type=\"button\" class=\"rb-btn rb-btn-accent\" onclick=\"AdminRoomBoardJs.startGo('" + Ui.Js(room.RoomId) + "')\">Start visit</button></div>");
            return sb.ToString();
        }

        private static async Task<string> MoveModal(Ctx c, RbRoom from, string pickRoom, string pickStaff, string err)
        {
            List<RbRoom> rooms = await Rooms();
            List<StaffPick> staff = Choosable(c, await Flow.Providers());
            string cur = pickStaff != string.Empty ? pickStaff : from.StaffId;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Move " + Ui.Enc(from.PetName) + "</h2><p class=\"rb-mlead\">Now in " + Ui.Enc(from.RoomName) + " with " + (from.StaffName != string.Empty ? Ui.Enc(from.StaffName) : "no one yet") + ".</p>");
            sb.Append("<em class=\"rb-err rb-merr\" id=\"rb-e-modal\">" + Ui.Enc(err) + "</em>");
            sb.Append("<label class=\"rb-field rb-mstaff\"><span>Who looks after the patient</span><select id=\"rb-mstaff\">" + StaffOptions(staff, cur) + "</select></label>");
            sb.Append("<h3>Move to</h3><div class=\"rb-rgroups\">");
            foreach (IGrouping<string, RbRoom> g in rooms.GroupBy(x => x.RoomtypeId).OrderBy(g => g.First().TypeOrder))
            {
                sb.Append("<fieldset class=\"rb-rg\"><legend>" + Ui.Enc(g.First().TypeName) + "</legend><div class=\"rb-rlist\">");
                foreach (RbRoom x in g.OrderBy(x => x.RoomOrder).ThenBy(x => x.RoomName))
                {
                    bool here = x.VisitId == from.VisitId;
                    bool busy = x.VisitId != string.Empty;
                    string note = here ? "Here now" : busy ? x.PetName + (x.RoomStaffName != string.Empty ? " with " + x.RoomStaffName : string.Empty) : "Free";
                    sb.Append("<label class=\"rb-opt rb-ropt" + (busy ? " rb-dis" : string.Empty) + "\"><input type=\"radio\" name=\"rb-mroom\" value=\"" + Ui.Attr(x.RoomId) + "\"" + (busy ? " disabled" : string.Empty) + (!busy && x.RoomId == pickRoom ? " checked" : string.Empty) + "><span><b>" + Ui.Enc(x.RoomName) + "</b><small>" + Ui.Enc(note) + "</small></span></label>");
                }
                sb.Append("</div></fieldset>");
            }
            sb.Append("</div><div class=\"rb-mfoot\"><button type=\"button\" class=\"rb-btn rb-btn-ghost\" onclick=\"AdminRoomBoardJs.closeModal()\">Cancel</button><button type=\"button\" class=\"rb-btn rb-btn-accent\" onclick=\"AdminRoomBoardJs.moveGo('" + Ui.Js(from.VisitId) + "')\">Move patient</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> StartPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "start visit");
            if (c == null)
            {
                return response;
            }
            RbRoom? room = (await Rooms()).FirstOrDefault(x => x.RoomId == Val("roomid", 50));
            if (room == null || room.VisitId != string.Empty)
            {
                await Redraw(response, c, room == null ? "This room was not found." : room.RoomName + " is in use now.", true);
                return response;
            }
            response.SetElementContents("rb-mbody", await StartModal(c, room, string.Empty, string.Empty, string.Empty));
            response.ExecuteScript("AdminRoomBoardJs.shown();");
            return response;
        }

        public async Task<ApiResponse> StartGo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "start visit");
            if (c == null)
            {
                return response;
            }
            string roomId = Val("roomid", 50);
            string visitId = Val("visitid", 50);
            string staffId = c.U.OwnOnly ? c.U.StaffId : Val("staffid", 50);
            FlowVisit? v = await Flow.Visit(visitId);
            string err;
            if (v == null || v.VisitType == "boarding")
            {
                err = "Pick a waiting patient.";
            }
            else if (!c.Mine(v.StaffId))
            {
                await App.Log(c.U.StaffId, "start visit", v.PetName + " · not your patient", false);
                err = "You can only start your own patients.";
            }
            else
            {
                err = staffId == string.Empty ? "Pick who sees the patient." : await Flow.Start(c, v.VisitId, roomId, staffId, await App.Settings());
            }
            if (err == string.Empty)
            {
                StaffPick? st = (await Flow.Providers()).FirstOrDefault(x => x.StaffId == staffId);
                RbRoom? r = (await Rooms()).FirstOrDefault(x => x.RoomId == roomId);
                await Redraw(response, c, v!.PetName + " is in " + (r != null ? r.RoomName : "the room") + (st != null ? " with " + st.StaffName : string.Empty) + ".", true);
                return response;
            }
            RbRoom? room = (await Rooms()).FirstOrDefault(x => x.RoomId == roomId);
            if (room == null || room.VisitId != string.Empty)
            {
                await Redraw(response, c, room == null ? "This room was not found." : room.RoomName + " was just taken. Pick another room.", true);
                return response;
            }
            response.SetElementContents("rb-mbody", await StartModal(c, room, visitId, staffId, err));
            await Redraw(response, c, string.Empty, false);
            return response;
        }

        public async Task<ApiResponse> MovePick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "move room");
            if (c == null)
            {
                return response;
            }
            string visitId = Val("visitid", 50);
            RbRoom? from = (await Rooms()).FirstOrDefault(x => x.VisitId == visitId && visitId != string.Empty);
            if (from == null)
            {
                await Redraw(response, c, "This patient is not in a room any more.", true);
                return response;
            }
            if (!c.Mine(from.StaffId))
            {
                await Redraw(response, c, "You can only move your own patients.", true);
                return response;
            }
            response.SetElementContents("rb-mbody", await MoveModal(c, from, string.Empty, string.Empty, string.Empty));
            response.ExecuteScript("AdminRoomBoardJs.shown();");
            return response;
        }

        public async Task<ApiResponse> MoveGo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Editor(response, "move room");
            if (c == null)
            {
                return response;
            }
            string visitId = Val("visitid", 50);
            string roomId = Val("roomid", 50);
            string staffId = c.U.OwnOnly ? c.U.StaffId : Val("staffid", 50);
            RbRoom? from = (await Rooms()).FirstOrDefault(x => x.VisitId == visitId && visitId != string.Empty);
            if (from == null)
            {
                await Redraw(response, c, "This patient is not in a room any more.", true);
                return response;
            }
            if (!c.Mine(from.StaffId))
            {
                await App.Log(c.U.StaffId, "move room", from.PetName + " · not your patient", false);
                await Redraw(response, c, "You can only move your own patients.", true);
                return response;
            }
            string err = roomId == string.Empty ? "Pick a room." : staffId == string.Empty ? "Pick who looks after the patient." : await Flow.Move(c, visitId, roomId, staffId, await App.Settings());
            if (err == string.Empty)
            {
                RbRoom? to = (await Rooms()).FirstOrDefault(x => x.RoomId == roomId);
                await Redraw(response, c, from.PetName + " moved to " + (to != null ? to.RoomName : "the new room") + ".", true);
                return response;
            }
            RbRoom? still = (await Rooms()).FirstOrDefault(x => x.VisitId == visitId);
            if (still == null)
            {
                await Redraw(response, c, err, true);
                return response;
            }
            response.SetElementContents("rb-mbody", await MoveModal(c, still, roomId, staffId, err));
            await Redraw(response, c, string.Empty, false);
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
    }
}
