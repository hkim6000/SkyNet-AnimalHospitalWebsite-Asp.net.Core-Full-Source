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
    public class AdminRoomSetup : WebPage
    {
        private const string Key = "roomsetup";
        private const string Me = "AdminRoomSetupJs";
        private static readonly string[] Purposes = { "visit", "special", "emergency", "surgery", "recovery", "grooming", "boarding" };
        private static readonly string[] SizeKeys = { "small", "medium", "large" };
        private const string Changed = "IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";

        private const string TypeSql = "SELECT T.ROOMTYPE_ID AS TypeId, T.TYPE_NAME AS TypeName, T.PURPOSE AS Purpose, T.COLOR AS Color, T.SPECIES AS Species, T.SIZES AS Sizes, T.NIGHT_ITEM_ID AS NightItemId, ISNULL(I.ITEM_NAME, N'') AS NightName, ISNULL(I.PRICE, 0) AS NightPrice, T.SHORT_TEXT AS ShortText, T.LONG_TEXT AS LongText, T.IMAGE AS Image, T.ONLINE AS Online, T.TYPE_ORDER AS TypeOrder, T.ACTIVE AS Active, (SELECT COUNT(*) FROM ROOM R WHERE R.ROOMTYPE_ID = T.ROOMTYPE_ID) AS RoomCnt, (SELECT COUNT(*) FROM ROOM R WHERE R.ROOMTYPE_ID = T.ROOMTYPE_ID AND R.ACTIVE = 1) AS ActiveCnt FROM ROOMTYPE T LEFT JOIN ITEM I ON I.ITEM_ID = T.NIGHT_ITEM_ID AND T.NIGHT_ITEM_ID <> N''";

        private const string RoomSql = "SELECT R.ROOM_ID AS RoomId, R.ROOMTYPE_ID AS TypeId, R.ROOM_NAME AS RoomName, R.NOTE AS Note, R.ROOM_ORDER AS RoomOrder, R.ACTIVE AS Active, ISNULL(U.N, 0) AS BusyCnt, ISNULL(U.PetName, N'') AS BusyPet FROM ROOM R LEFT JOIN (SELECT X.ROOM_ID, COUNT(*) AS N, MIN(ISNULL(P.PET_NAME, N'')) AS PetName FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL GROUP BY X.ROOM_ID) U ON U.ROOM_ID = R.ROOM_ID";

        private const string TypeBusy = "EXISTS (SELECT 1 FROM VISITROOM X JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID WHERE X.OUT_AT IS NULL AND R.ROOMTYPE_ID = @tid) OR EXISTS (SELECT 1 FROM APPOINTMENT A WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND (A.ROOMTYPE_ID = @tid OR A.ROOM_ID IN (SELECT R.ROOM_ID FROM ROOM R WHERE R.ROOMTYPE_ID = @tid)))";

        private const string RoomBusy = "EXISTS (SELECT 1 FROM VISITROOM X WHERE X.OUT_AT IS NULL AND X.ROOM_ID = @rid) OR EXISTS (SELECT 1 FROM APPOINTMENT A WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND A.ROOM_ID = @rid)";

        private const string TypeUseSql = "SELECT TOP 12 Kind, PetName, RoomName, VisitType, At FROM (SELECT N'in' AS Kind, ISNULL(P.PET_NAME, N'') AS PetName, R.ROOM_NAME AS RoomName, V.VISIT_TYPE AS VisitType, X.IN_AT AS At FROM VISITROOM X JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL AND R.ROOMTYPE_ID = @tid UNION ALL SELECT N'appt' AS Kind, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(R.ROOM_NAME, N'') AS RoomName, A.VISIT_TYPE AS VisitType, A.START_AT AS At FROM APPOINTMENT A LEFT JOIN ROOM R ON R.ROOM_ID = A.ROOM_ID AND A.ROOM_ID <> N'' LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND (A.ROOMTYPE_ID = @tid OR R.ROOMTYPE_ID = @tid)) Z ORDER BY CASE WHEN Kind = N'in' THEN 0 ELSE 1 END, At";

        private const string RoomUseSql = "SELECT TOP 12 Kind, PetName, RoomName, VisitType, At FROM (SELECT N'in' AS Kind, ISNULL(P.PET_NAME, N'') AS PetName, R.ROOM_NAME AS RoomName, V.VISIT_TYPE AS VisitType, X.IN_AT AS At FROM VISITROOM X JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL AND X.ROOM_ID = @rid UNION ALL SELECT N'appt' AS Kind, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(R.ROOM_NAME, N'') AS RoomName, A.VISIT_TYPE AS VisitType, A.START_AT AS At FROM APPOINTMENT A LEFT JOIN ROOM R ON R.ROOM_ID = A.ROOM_ID LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND A.ROOM_ID = @rid) Z ORDER BY CASE WHEN Kind = N'in' THEN 0 ELSE 1 END, At";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Rooms and room types | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ro-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ro"));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private static string NewKey(string? v)
        {
            string t = (v ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(t, "^[a-f0-9]{32}$") ? t : string.Empty;
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key), max);
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
            return "<div class=\"ro-panel ro-deny\"><h1>Rooms and room types</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"ro-btn ro-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        public static string PurposeName(string p)
        {
            return p switch
            {
                "visit" => "Exam room",
                "special" => "Special room",
                "emergency" => "Emergency",
                "surgery" => "Surgery",
                "recovery" => "Recovery ward",
                "grooming" => "Grooming",
                "boarding" => "Boarding",
                _ => p
            };
        }

        private static string SizeName(string k)
        {
            return k switch
            {
                "small" => "Small",
                "medium" => "Medium",
                "large" => "Large",
                _ => k
            };
        }

        private static string VisitWord(string t)
        {
            return t switch
            {
                "boarding" => "boarding stay",
                "surgery" => "surgery",
                "grooming" => "grooming",
                _ => "appointment"
            };
        }

        private static List<string> Csv(string v)
        {
            return (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        }

        private string Show()
        {
            string v = Val("rshow", 10);
            return v == "inactive" || v == "all" ? v : "active";
        }

        private static async Task<List<RoType>> Types()
        {
            var (list, emsg) = await Helper.GetListT<RoType>(TypeSql + " ORDER BY T.TYPE_ORDER, T.TYPE_NAME");
            return emsg == string.Empty && list != null ? list : new List<RoType>();
        }

        private static async Task<RoType?> TypeOne(string id)
        {
            var (row, emsg) = await Helper.GetOneT<RoType>(TypeSql + " WHERE T.ROOMTYPE_ID = @tid", new List<SqlParameter> { App.P("tid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<RoRoom>> Rooms()
        {
            var (list, emsg) = await Helper.GetListT<RoRoom>(RoomSql + " ORDER BY R.ROOM_ORDER, R.ROOM_NAME");
            return emsg == string.Empty && list != null ? list : new List<RoRoom>();
        }

        private static async Task<RoRoom?> RoomOne(string id)
        {
            var (row, emsg) = await Helper.GetOneT<RoRoom>(RoomSql + " WHERE R.ROOM_ID = @rid", new List<SqlParameter> { App.P("rid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<RoSpecies>> SpeciesList()
        {
            var (list, emsg) = await Helper.GetListT<RoSpecies>("SELECT SPECIES_KEY AS SpeciesKey, SPECIES_NAME AS SpeciesName, PLURAL_NAME AS PluralName, ACTIVE AS Active FROM SPECIES ORDER BY SPECIES_ORDER, SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<RoSpecies>();
        }

        private static async Task<List<RoItem>> NightItems()
        {
            var (list, emsg) = await Helper.GetListT<RoItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, PRICE AS Price, SPECIES AS Species, ACTIVE AS Active FROM ITEM WHERE ITEM_TYPE = N'boarding' ORDER BY ITEM_ORDER, ITEM_NAME");
            return emsg == string.Empty && list != null ? list : new List<RoItem>();
        }

        private static List<string> Images()
        {
            List<string> list = new List<string>();
            string dir = Path.Combine(App.Root, "images", "hospital", "full");
            if (!Directory.Exists(dir))
            {
                return list;
            }
            foreach (string f in Directory.GetFiles(dir, "*.webp").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string rel = "images/hospital/full/" + Path.GetFileName(f);
                if (Ui.SafePath(rel))
                {
                    list.Add(rel);
                }
            }
            return list;
        }

        private static string ImageName(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).Replace('-', ' ').Replace('_', ' ');
            return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : path;
        }

        private static string SpeciesText(string csv, List<RoSpecies> sp)
        {
            List<string> keys = Csv(csv);
            if (keys.Count == 0)
            {
                return "All animals";
            }
            return string.Join(", ", keys.Select(k => sp.FirstOrDefault(x => x.SpeciesKey == k)?.PluralName is string p && p != string.Empty ? p : sp.FirstOrDefault(x => x.SpeciesKey == k)?.SpeciesName ?? k));
        }

        private static string Chev(bool up)
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"" + (up ? "M6 15l6-6 6 6" : "M6 9l6 6 6-6") + "\"/></svg>";
        }

        private static string Status(bool active)
        {
            return active ? "<span class=\"ro-st ro-st-on\">Active</span>" : "<span class=\"ro-st ro-st-off\">Inactive</span>";
        }

        private static async Task<string> Body(Ctx c, Dictionary<string, string> s, string show)
        {
            List<RoType> types = await Types();
            List<RoRoom> rooms = await Rooms();
            List<RoSpecies> sp = await SpeciesList();
            bool canE = c.Can(Key, 'E');
            bool canA = c.Can(Key, 'A');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ro-ptop\"><div><h1>Rooms and room types</h1><p>Room types say what a room is for, its colour on the boards and which animals it suits. Rooms are the real exam rooms, tables, wards and runs.</p></div><div class=\"ro-acts\">");
            if (canA)
            {
                sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".typeForm('')\">Add a room type</button><button type=\"button\" class=\"ro-btn ro-btn-accent\" onclick=\"" + Me + ".roomForm('', '')\">Add a room</button>");
            }
            sb.Append("</div></div>");
            sb.Append("<div class=\"ro-bar\"><label class=\"ro-field\"><span>Show</span><select id=\"ro-show\" onchange=\"" + Me + ".refresh()\">" + Ui.Opts(new[] { ("active", "Active only"), ("inactive", "Inactive only"), ("all", "Everything") }, show) + "</select></label>");
            int inUse = rooms.Count(r => r.BusyCnt > 0);
            sb.Append("<p class=\"ro-sum\">" + Ui.Plural(types.Count(t => t.Active), "active room type", "active room types") + " &middot; " + Ui.Plural(rooms.Count(r => r.Active), "active room", "active rooms") + " &middot; " + Ui.Plural(inUse, "room in use now", "rooms in use now") + "</p></div>");

            List<RoType> tshow = types.Where(t => show == "all" || (show == "active" ? t.Active : !t.Active)).ToList();
            sb.Append("<section class=\"ro-sect\"><h2 class=\"ro-h2\">Room types</h2>");
            if (tshow.Count == 0)
            {
                sb.Append("<div class=\"ro-tw\"><p class=\"ro-empty\">" + (show == "inactive" ? "No inactive room types." : "No room types yet.") + "</p></div>");
            }
            else
            {
                sb.Append("<div class=\"ro-tw\"><table class=\"ro-table ro-ttable\"><thead><tr><th>Order</th><th>Room type</th><th>Colour</th><th>Suits</th><th class=\"ro-num\">Rooms</th><th>Website</th><th>Status</th><th><span class=\"ro-sr\">Actions</span></th></tr></thead><tbody>");
                foreach (RoType t in tshow)
                {
                    int i = types.IndexOf(t);
                    string tid = Ui.Js(t.TypeId);
                    sb.Append("<tr class=\"" + (t.Active ? string.Empty : "ro-off") + "\">");
                    sb.Append("<td data-l=\"Order\" class=\"ro-ordc\"><span class=\"ro-ordn\">" + t.TypeOrder.ToString(Ui.Inv) + "</span>");
                    if (canE)
                    {
                        sb.Append("<span class=\"ro-mv\"><button type=\"button\" class=\"ro-mbtn\" aria-label=\"Move " + Ui.Attr(t.TypeName) + " up\"" + (i == 0 ? " disabled" : string.Empty) + " onclick=\"" + Me + ".moveType('" + tid + "', 'up', this)\">" + Chev(true) + "</button><button type=\"button\" class=\"ro-mbtn\" aria-label=\"Move " + Ui.Attr(t.TypeName) + " down\"" + (i == types.Count - 1 ? " disabled" : string.Empty) + " onclick=\"" + Me + ".moveType('" + tid + "', 'down', this)\">" + Chev(false) + "</button></span>");
                    }
                    sb.Append("</td>");
                    sb.Append("<td class=\"ro-cname\"><b>" + Ui.Enc(t.TypeName) + "</b><small>" + Ui.Enc(PurposeName(t.Purpose)) + (t.Purpose == "boarding" && t.NightName != string.Empty ? " &middot; " + Ui.Money(t.NightPrice) + " a night" : string.Empty) + "</small></td>");
                    sb.Append("<td data-l=\"Colour\"><span class=\"ro-colc\"><span class=\"ro-sw\" style=\"background:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\" aria-hidden=\"true\"></span><span class=\"ro-hex\">" + Ui.Enc(t.Color) + "</span></span></td>");
                    sb.Append("<td data-l=\"Suits\">" + Ui.Enc(SpeciesText(t.Species, sp)) + (t.Purpose == "boarding" && t.Sizes != string.Empty ? "<small class=\"ro-blk ro-dim\">" + Ui.Enc(string.Join(", ", Csv(t.Sizes).Select(SizeName))) + " size</small>" : string.Empty) + "</td>");
                    sb.Append("<td data-l=\"Rooms\" class=\"ro-num\">" + t.ActiveCnt.ToString(Ui.Inv) + (t.RoomCnt > t.ActiveCnt ? "<small class=\"ro-blk ro-dim\">+" + (t.RoomCnt - t.ActiveCnt).ToString(Ui.Inv) + " inactive</small>" : string.Empty) + "</td>");
                    sb.Append("<td data-l=\"Website\">" + (t.Purpose == "boarding" ? (t.Online ? "<span class=\"ro-st ro-st-web\">Shown</span>" : "<span class=\"ro-dim\">Hidden</span>") : "<span class=\"ro-dim\">&ndash;</span>") + "</td>");
                    sb.Append("<td data-l=\"Status\">" + Status(t.Active) + "</td><td class=\"ro-acell\">");
                    if (canE)
                    {
                        sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-sm ro-btn-ghost\" onclick=\"" + Me + ".typeForm('" + tid + "')\">Edit</button>");
                        if (!t.Active)
                        {
                            sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-sm ro-btn-ghost\" onclick=\"" + Me + ".typeActive('" + tid + "', 1, this)\">Make active</button>");
                        }
                    }
                    sb.Append("</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            sb.Append("</section>");

            sb.Append("<section class=\"ro-sect\"><h2 class=\"ro-h2\">Rooms</h2><div class=\"ro-groups\">");
            int shown = 0;
            foreach (RoType t in types)
            {
                List<RoRoom> all = rooms.Where(r => r.TypeId == t.TypeId).ToList();
                List<RoRoom> list = all.Where(r => show == "all" || (show == "active" ? r.Active : !r.Active)).ToList();
                bool typeShown = show == "all" || (show == "active" ? t.Active : !t.Active);
                if (list.Count == 0 && !(typeShown && show != "inactive"))
                {
                    continue;
                }
                if (show == "active" && !t.Active)
                {
                    continue;
                }
                shown++;
                string tid = Ui.Js(t.TypeId);
                sb.Append("<div class=\"ro-panel ro-group" + (t.Active ? string.Empty : " ro-off") + "\"><div class=\"ro-ghead\"><span class=\"ro-sw ro-sw-l\" style=\"background:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\" aria-hidden=\"true\"></span><div class=\"ro-gt\"><h3>" + Ui.Enc(t.TypeName) + "</h3><small>" + Ui.Enc(PurposeName(t.Purpose)) + " &middot; " + Ui.Plural(all.Count(r => r.Active), "active room", "active rooms") + (t.Active ? string.Empty : " &middot; type is inactive") + "</small></div>");
                if (canA && t.Active)
                {
                    sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-sm ro-btn-ghost\" onclick=\"" + Me + ".roomForm('', '" + tid + "')\">Add a room here</button>");
                }
                sb.Append("</div>");
                if (list.Count == 0)
                {
                    sb.Append("<p class=\"ro-sub ro-gempty\">" + (all.Count == 0 ? "No rooms of this type yet." : "No rooms to show with this filter.") + "</p></div>");
                    continue;
                }
                sb.Append("<ul class=\"ro-rooms\">");
                for (int i = 0; i < list.Count; i++)
                {
                    RoRoom r = list[i];
                    int pos = all.IndexOf(r);
                    string rid = Ui.Js(r.RoomId);
                    sb.Append("<li class=\"ro-room" + (r.Active ? string.Empty : " ro-off") + "\"><span class=\"ro-ordn\" title=\"Order\">" + r.RoomOrder.ToString(Ui.Inv) + "</span><div class=\"ro-rt\"><b>" + Ui.Enc(r.RoomName) + "</b>" + (r.Note != string.Empty ? "<small>" + Ui.Enc(r.Note) + "</small>" : string.Empty) + "</div><div class=\"ro-rtags\">");
                    if (r.BusyCnt > 0)
                    {
                        sb.Append("<span class=\"ro-st ro-st-busy\">In use &middot; " + Ui.Enc(r.BusyPet != string.Empty ? r.BusyPet : "a patient") + (r.BusyCnt > 1 ? " +" + (r.BusyCnt - 1).ToString(Ui.Inv) : string.Empty) + "</span>");
                    }
                    else if (r.Active)
                    {
                        sb.Append("<span class=\"ro-st ro-st-free\">Free</span>");
                    }
                    if (!r.Active)
                    {
                        sb.Append(Status(false));
                    }
                    sb.Append("</div><div class=\"ro-racts\">");
                    if (canE)
                    {
                        sb.Append("<span class=\"ro-mv\"><button type=\"button\" class=\"ro-mbtn\" aria-label=\"Move " + Ui.Attr(r.RoomName) + " up\"" + (pos == 0 ? " disabled" : string.Empty) + " onclick=\"" + Me + ".moveRoom('" + rid + "', 'up', this)\">" + Chev(true) + "</button><button type=\"button\" class=\"ro-mbtn\" aria-label=\"Move " + Ui.Attr(r.RoomName) + " down\"" + (pos == all.Count - 1 ? " disabled" : string.Empty) + " onclick=\"" + Me + ".moveRoom('" + rid + "', 'down', this)\">" + Chev(false) + "</button></span>");
                        sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-sm ro-btn-ghost\" onclick=\"" + Me + ".roomForm('" + rid + "', '')\">Edit</button>");
                        if (!r.Active)
                        {
                            sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-sm ro-btn-ghost\" onclick=\"" + Me + ".roomActive('" + rid + "', 1, this)\">Make active</button>");
                        }
                    }
                    sb.Append("</div></li>");
                }
                sb.Append("</ul></div>");
            }
            if (shown == 0)
            {
                sb.Append("<div class=\"ro-tw\"><p class=\"ro-empty\">" + (show == "inactive" ? "No inactive rooms." : "No rooms yet.") + "</p></div>");
            }
            sb.Append("</div></section>");
            return sb.ToString();
        }

        private static async Task Draw(ApiResponse response, Ctx c, string show, string toast)
        {
            Dictionary<string, string> s = await App.Settings();
            response.SetElementContents("ro-main", await Body(c, s, show));
            response.ExecuteScript(Me + ".ready();");
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
            response.SetElementContents("ro-menu", Auth.Menu("ro", Me, "AdminRoomSetup", c));
            response.SetElementContents("ro-who", Auth.Who("ro", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ro-main", Denied());
                return response;
            }
            await Draw(response, c, "active", string.Empty);
            return response;
        }

        public async Task<ApiResponse> Refresh()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ro-main", Denied());
                return response;
            }
            await Draw(response, c, Show(), string.Empty);
            return response;
        }

        private async Task<Ctx?> Reach(ApiResponse response, char right)
        {
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return null;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, right))
            {
                Toast(response, "You don't have the right to do that.");
                return null;
            }
            return c;
        }

        private static string Field(string label, string inner, string err, string cls = "")
        {
            return "<label class=\"ro-field" + (cls != string.Empty ? " " + cls : string.Empty) + "\"><span>" + label + "</span>" + inner + "</label>" + (err != string.Empty ? "<em class=\"ro-err\" id=\"ro-e-" + err + "\"></em>" : string.Empty);
        }

        public async Task<ApiResponse> TypeForm()
        {
            ApiResponse response = new ApiResponse();
            string id = CleanId(GetDataValue("typeid"));
            Ctx? c = await Reach(response, id == string.Empty ? 'A' : 'E');
            if (c == null)
            {
                return response;
            }
            RoType t = new RoType { Purpose = "visit", Color = "#3F7D6B", Active = true };
            if (id != string.Empty)
            {
                RoType? row = await TypeOne(id);
                if (row == null)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await Draw(response, c, Show(), "This room type does not exist any more.");
                    return response;
                }
                t = row;
            }
            List<RoSpecies> sp = await SpeciesList();
            List<RoItem> items = await NightItems();
            List<string> imgs = Images();
            List<string> mySp = Csv(t.Species);
            List<string> mySz = Csv(t.Sizes);
            bool board = t.Purpose == "boarding";
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (id == string.Empty ? "Add a room type" : "Edit room type") + "</h2>");
            if (id != string.Empty)
            {
                sb.Append("<p class=\"ro-mlead\">" + Ui.Plural(t.RoomCnt, "room", "rooms") + " of this type. Changes show on the boards and booking pages straight away.</p>");
            }
            sb.Append("<div class=\"ro-fgrid\" data-typeid=\"" + Ui.Attr(t.TypeId) + "\" id=\"ro-tform\">");
            sb.Append("<div>" + Field("Name", "<input id=\"ro-tname\" type=\"text\" maxlength=\"60\" value=\"" + Ui.Attr(t.TypeName) + "\" autocomplete=\"off\">", "tname") + "</div>");
            sb.Append("<div>" + Field("What it is for", "<select id=\"ro-tpur\" onchange=\"" + Me + ".purpose()\">" + Ui.Opts(Purposes.Select(p => (p, PurposeName(p))), t.Purpose) + "</select>", "tpur") + "</div>");
            string col = Ui.SafeColor(t.Color) ? t.Color.ToLowerInvariant() : "#3f7d6b";
            sb.Append("<div>" + Field("Colour on the boards", "<span class=\"ro-colrow\"><input id=\"ro-tcol\" type=\"color\" value=\"" + col + "\" oninput=\"" + Me + ".colour()\"><b class=\"ro-colv\" id=\"ro-tcolv\">" + Ui.Enc(col.ToUpperInvariant()) + "</b></span>", "tcol") + "</div>");
            sb.Append("<div>" + Field("Order <small>(lower comes first; empty = last)</small>", "<input id=\"ro-tord\" type=\"number\" inputmode=\"numeric\" min=\"0\" max=\"9999\" value=\"" + (id == string.Empty ? string.Empty : t.TypeOrder.ToString(Ui.Inv)) + "\">", "tord") + "</div>");
            sb.Append("<fieldset class=\"ro-fs ro-span2\"><legend>Which animals it suits <small>(none ticked = all animals)</small></legend><div class=\"ro-checks\">");
            foreach (RoSpecies x in sp.Where(x => x.Active || mySp.Contains(x.SpeciesKey)))
            {
                sb.Append("<label class=\"ro-check\"><input type=\"checkbox\" class=\"ro-tsp\" id=\"ro-tsp-" + Ui.Attr(x.SpeciesKey) + "\" value=\"" + Ui.Attr(x.SpeciesKey) + "\"" + (mySp.Contains(x.SpeciesKey) ? " checked" : string.Empty) + ">" + Ui.Enc(x.PluralName != string.Empty ? x.PluralName : x.SpeciesName) + "</label>");
            }
            sb.Append("</div><em class=\"ro-err\" id=\"ro-e-tsp\"></em></fieldset>");
            sb.Append("<fieldset class=\"ro-fs ro-span2 ro-bonly\"" + (board ? string.Empty : " hidden") + "><legend>Dog sizes for this run <small>(none ticked = any size)</small></legend><div class=\"ro-checks\">");
            foreach (string k in SizeKeys)
            {
                sb.Append("<label class=\"ro-check\"><input type=\"checkbox\" class=\"ro-tsz\" id=\"ro-tsz-" + k + "\" value=\"" + k + "\"" + (mySz.Contains(k) ? " checked" : string.Empty) + ">" + SizeName(k) + "</label>");
            }
            sb.Append("</div><em class=\"ro-err\" id=\"ro-e-tsize\"></em></fieldset>");
            StringBuilder io = new StringBuilder("<option value=\"\">Choose the nightly price item</option>");
            foreach (RoItem it in items.Where(x => x.Active || x.ItemId == t.NightItemId))
            {
                io.Append("<option value=\"" + Ui.Attr(it.ItemId) + "\"" + (it.ItemId == t.NightItemId ? " selected" : string.Empty) + ">" + Ui.Enc(it.ItemName + " · " + Ui.Money(it.Price).Replace("&minus;", "-") + (it.Active ? string.Empty : " (inactive)")) + "</option>");
            }
            sb.Append("<div class=\"ro-span2 ro-bonly\"" + (board ? string.Empty : " hidden") + ">" + Field("Price for one night", "<select id=\"ro-titem\">" + io + "</select>", "titem") + "<p class=\"ro-hint\">Boarding items come from Items and prices. The owner is charged this item once for every night.</p></div>");
            sb.Append("<div class=\"ro-span2\">" + Field("Short text for the website <small>(one line)</small>", "<input id=\"ro-tshort\" type=\"text\" maxlength=\"200\" value=\"" + Ui.Attr(t.ShortText) + "\">", "tshort") + "</div>");
            sb.Append("<div class=\"ro-span2\">" + Field("Longer text for the website", "<textarea id=\"ro-tlong\" rows=\"3\" maxlength=\"1000\">" + Ui.Enc(t.LongText) + "</textarea>", "tlong") + "</div>");
            StringBuilder im = new StringBuilder("<option value=\"\">No picture</option>");
            foreach (string p in imgs)
            {
                im.Append("<option value=\"" + Ui.Attr(p) + "\"" + (p == t.Image ? " selected" : string.Empty) + ">" + Ui.Enc(ImageName(p)) + "</option>");
            }
            if (t.Image != string.Empty && !imgs.Contains(t.Image))
            {
                im.Append("<option value=\"" + Ui.Attr(t.Image) + "\" selected>" + Ui.Enc(ImageName(t.Image) + " (missing)") + "</option>");
            }
            bool hasImg = t.Image != string.Empty && imgs.Contains(t.Image);
            sb.Append("<div class=\"ro-span2 ro-imgrow\"><div class=\"ro-imgf\">" + Field("Picture", "<select id=\"ro-timg\" onchange=\"" + Me + ".picture()\">" + im + "</select>", "timg") + "<p class=\"ro-hint\">Pictures from the hospital photo folder (images/hospital/full).</p></div><div class=\"ro-imgpv\" id=\"ro-timgpv\">" + (hasImg ? "<img src=\"" + Ui.Attr(t.Image) + "\" alt=\"\" loading=\"lazy\">" : "<span>No picture</span>") + "</div></div>");
            sb.Append("<div class=\"ro-span2 ro-bonly\"" + (board ? string.Empty : " hidden") + "><label class=\"ro-check\"><input type=\"checkbox\" id=\"ro-tonline\"" + (t.Online ? " checked" : string.Empty) + ">Show this run type on the website (boarding page and online stay booking)</label></div>");
            sb.Append("<div class=\"ro-span2\"><label class=\"ro-check\"><input type=\"checkbox\" id=\"ro-tact\"" + (t.Active ? " checked" : string.Empty) + ">Active <small class=\"ro-dim\">(inactive types are hidden from boards and booking)</small></label><em class=\"ro-err\" id=\"ro-e-tact\"></em></div>");
            sb.Append("</div><div id=\"ro-tuse\"></div><em class=\"ro-err ro-merr\" id=\"ro-e-type\"></em><div class=\"ro-mfoot\">");
            if (id != string.Empty && c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-danger ro-mleft\" onclick=\"" + Me + ".delType('" + Ui.Js(t.TypeId) + "', this)\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"ro-btn ro-btn-accent\" id=\"ro-tgo\" onclick=\"" + Me + ".saveType()\">" + (id == string.Empty ? "Add room type" : "Save changes") + "</button></div>");
            response.SetElementContents("ro-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('ro-tname');");
            return response;
        }

        private static async Task<(int InRoom, int Booked, List<RoUse> List)> TypeUse(string tid, DateTime now)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT (SELECT COUNT(*) FROM VISITROOM X JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID WHERE X.OUT_AT IS NULL AND R.ROOMTYPE_ID = @tid), (SELECT COUNT(*) FROM APPOINTMENT A LEFT JOIN ROOM R ON R.ROOM_ID = A.ROOM_ID AND A.ROOM_ID <> N'' WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND (A.ROOMTYPE_ID = @tid OR R.ROOMTYPE_ID = @tid))", new List<SqlParameter> { App.P("tid", tid, 50), App.PDate("now", now) });
            int a = emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            int b = emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][1]) : 0;
            if (a + b == 0)
            {
                return (0, 0, new List<RoUse>());
            }
            var (list, lerr) = await Helper.GetListT<RoUse>(TypeUseSql, new List<SqlParameter> { App.P("tid", tid, 50), App.PDate("now", now) });
            return (a, b, lerr == string.Empty && list != null ? list : new List<RoUse>());
        }

        private static async Task<(int InRoom, int Booked, List<RoUse> List)> RoomUse(string rid, DateTime now)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT (SELECT COUNT(*) FROM VISITROOM X WHERE X.OUT_AT IS NULL AND X.ROOM_ID = @rid), (SELECT COUNT(*) FROM APPOINTMENT A WHERE A.STATUS IN (N'booked', N'confirmed') AND A.END_AT >= @now AND A.ROOM_ID = @rid)", new List<SqlParameter> { App.P("rid", rid, 50), App.PDate("now", now) });
            int a = emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            int b = emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][1]) : 0;
            if (a + b == 0)
            {
                return (0, 0, new List<RoUse>());
            }
            var (list, lerr) = await Helper.GetListT<RoUse>(RoomUseSql, new List<SqlParameter> { App.P("rid", rid, 50), App.PDate("now", now) });
            return (a, b, lerr == string.Empty && list != null ? list : new List<RoUse>());
        }

        private static string UseHtml(string lead, int inRoom, int booked, List<RoUse> list, string tail, bool clock24)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ro-warn\"><b>" + Ui.Enc(lead) + "</b><ul>");
            foreach (RoUse u in list)
            {
                string pet = u.PetName != string.Empty ? u.PetName : "A patient";
                if (u.Kind == "in")
                {
                    sb.Append("<li>" + Ui.Enc(pet + " is in " + u.RoomName + " now (since " + Ui.Day(u.At, false) + " " + Ui.Hm(u.At, clock24) + ")") + "</li>");
                }
                else
                {
                    sb.Append("<li>" + Ui.Enc(pet + " has a " + VisitWord(u.VisitType) + " booked " + Ui.Day(u.At, false) + " " + Ui.Hm(u.At, clock24) + (u.RoomName != string.Empty ? " in " + u.RoomName : string.Empty)) + "</li>");
                }
            }
            sb.Append("</ul>");
            int more = inRoom + booked - list.Count;
            if (more > 0)
            {
                sb.Append("<p>&hellip; and " + Ui.Plural(more, "more", "more") + ".</p>");
            }
            sb.Append("<p>" + Ui.Enc(tail) + "</p></div>");
            return sb.ToString();
        }

        private static string Diff(List<string> changes, string label, string before, string after)
        {
            if (before != after)
            {
                changes.Add(label + " " + (before == string.Empty ? "none" : Ui.OneLine(before, 40)) + " → " + (after == string.Empty ? "none" : Ui.OneLine(after, 40)));
            }
            return after;
        }

        private static void ClearTypeErrors(ApiResponse response)
        {
            foreach (string e in new[] { "tname", "tpur", "tcol", "tsp", "tsize", "titem", "tshort", "tlong", "timg", "tord", "tact", "type" })
            {
                response.SetElementContents("ro-e-" + e, string.Empty);
            }
            response.SetElementContents("ro-tuse", string.Empty);
        }

        public async Task<ApiResponse> SaveType()
        {
            ApiResponse response = new ApiResponse();
            string id = CleanId(GetDataValue("typeid"));
            bool isNew = id == string.Empty;
            Ctx? c = await Reach(response, isNew ? 'A' : 'E');
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            ClearTypeErrors(response);
            RoType? cur = null;
            if (!isNew)
            {
                cur = await TypeOne(id);
                if (cur == null)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await Draw(response, c, Show(), "This room type does not exist any more.");
                    return response;
                }
            }
            else
            {
                id = NewKey(GetDataValue("newid"));
                if (id == string.Empty)
                {
                    response.SetElementContents("ro-e-type", "Something went wrong. Close the form and try again.");
                    return response;
                }
            }
            List<RoSpecies> sp = await SpeciesList();
            List<RoItem> items = await NightItems();
            List<string> imgs = Images();
            bool bad = false;
            string name = Ui.OneLine(GetDataValue("tname"), 60);
            if (name == string.Empty)
            {
                response.SetElementContents("ro-e-tname", "Write a name for this room type.");
                bad = true;
            }
            string purpose = Val("tpur", 20);
            if (!Purposes.Contains(purpose))
            {
                response.SetElementContents("ro-e-tpur", "Choose what this room type is for.");
                bad = true;
            }
            bool board = purpose == "boarding";
            string col = Val("tcol", 10).ToUpperInvariant();
            if (!Ui.SafeColor(col))
            {
                response.SetElementContents("ro-e-tcol", "Pick a colour.");
                bad = true;
            }
            List<string> given = Csv(Val("tspecies", 300));
            List<string> curSp = Csv(cur?.Species ?? string.Empty);
            List<string> species = sp.Where(x => given.Contains(x.SpeciesKey) && (x.Active || curSp.Contains(x.SpeciesKey))).Select(x => x.SpeciesKey).ToList();
            if (given.Any(g => !species.Contains(g)))
            {
                response.SetElementContents("ro-e-tsp", "One of the animals is not on the species list any more. Tick them again.");
                bad = true;
            }
            string speciesCsv = string.Join(",", species);
            if (speciesCsv.Length > 100)
            {
                response.SetElementContents("ro-e-tsp", "Too many animals ticked.");
                bad = true;
            }
            List<string> sz = Csv(Val("tsizes", 60));
            if (sz.Any(x => !SizeKeys.Contains(x)))
            {
                response.SetElementContents("ro-e-tsize", "Choose small, medium or large.");
                bad = true;
            }
            string sizes = board ? string.Join(",", SizeKeys.Where(sz.Contains)) : string.Empty;
            string item = board ? CleanId(GetDataValue("titem")) : string.Empty;
            if (board)
            {
                RoItem? it = items.FirstOrDefault(x => x.ItemId == item);
                if (item == string.Empty)
                {
                    response.SetElementContents("ro-e-titem", "Boarding needs a price for one night. Choose the item.");
                    bad = true;
                }
                else if (it == null || (!it.Active && item != (cur?.NightItemId ?? string.Empty)))
                {
                    response.SetElementContents("ro-e-titem", "That price item is not a boarding item any more. Choose another.");
                    bad = true;
                }
            }
            string rawShort = GetDataValue("tshort") ?? string.Empty;
            string shortText = Ui.OneLine(rawShort, 400);
            if (shortText.Length > 200)
            {
                response.SetElementContents("ro-e-tshort", "Keep the short text under 200 characters.");
                bad = true;
            }
            string longText = (GetDataValue("tlong") ?? string.Empty).Replace("\r", string.Empty).Trim();
            if (longText.Length > 1000)
            {
                response.SetElementContents("ro-e-tlong", "Keep the longer text under 1,000 characters.");
                bad = true;
            }
            string img = Val("timg", 300);
            if (img != string.Empty && !(Ui.SafePath(img) && imgs.Contains(img)) && img != (cur?.Image ?? string.Empty))
            {
                response.SetElementContents("ro-e-timg", "Choose a picture from the list.");
                bad = true;
            }
            bool online = board && Val("tonline", 2) == "1";
            string ordText = Val("tord", 10);
            int order = 0;
            bool autoOrder = ordText == string.Empty;
            if (!autoOrder && (!int.TryParse(ordText, NumberStyles.Integer, Ui.Inv, out order) || order < 0 || order > 9999))
            {
                response.SetElementContents("ro-e-tord", "Write a whole number from 0 to 9999, or leave it empty.");
                bad = true;
            }
            bool active = Val("tact", 2) == "1";
            if (bad)
            {
                response.SetElementContents("ro-e-type", "Check the fields marked in red.");
                return response;
            }
            var (dup, derr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ROOMTYPE WHERE LOWER(TYPE_NAME) = LOWER(@name) AND ROOMTYPE_ID <> @tid", new List<SqlParameter> { App.P("name", name, 60), App.P("tid", id, 50) });
            if (derr == string.Empty && dup.Rows.Count > 0 && Convert.ToInt32(dup.Rows[0][0]) > 0)
            {
                response.SetElementContents("ro-e-tname", "Another room type already has this name.");
                return response;
            }
            bool cross = cur != null && cur.Purpose != purpose && (cur.Purpose == "boarding" || board);
            bool guard = cur != null && ((cur.Active && !active) || cross);
            if (cross)
            {
                var (bt, berr) = await Helper.GetDataTable("SELECT BOOK_NAME FROM BOOKTYPE WHERE ROOMTYPE_ID = @tid ORDER BY BOOK_ORDER, BOOK_NAME", new List<SqlParameter> { App.P("tid", id, 50) });
                List<string> books = berr == string.Empty ? bt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]) ?? string.Empty).ToList() : new List<string> { "a booking type" };
                if (books.Count > 0)
                {
                    response.SetElementContents("ro-tuse", "<div class=\"ro-warn\"><b>What this room type is for can't change between boarding and other use while booking types use it:</b><ul>" + string.Concat(books.Take(12).Select(b => "<li>" + Ui.Enc(b) + "</li>")) + "</ul>" + (books.Count > 12 ? "<p>&hellip; and " + Ui.Plural(books.Count - 12, "more", "more") + ".</p>" : string.Empty) + "<p>Change the room type of these booking types in Booking types first.</p></div>");
                    response.SetElementContents("ro-e-type", "Not saved.");
                    return response;
                }
            }
            if (guard)
            {
                var (inRoom, booked, use) = await TypeUse(id, now);
                if (inRoom + booked > 0)
                {
                    string why = cur!.Active && !active ? "This room type can't be made inactive yet:" : "What this room type is for can't change between boarding and other use yet:";
                    response.SetElementContents("ro-tuse", UseHtml(why, inRoom, booked, use, "Wait until these patients have left, or move or cancel the bookings first.", App.S(s, "clock24") == "1"));
                    response.SetElementContents("ro-e-type", "Not saved.");
                    return response;
                }
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("tid", id, 50), App.P("name", name, 60), App.P("pur", purpose, 20), App.P("col", col, 7), App.P("sp", speciesCsv, 100), App.P("sz", sizes, 40),
                App.P("item", item, 50), App.P("short", shortText, 200), App.P("long", longText, 1000), App.P("img", img, 300), App.PBit("online", online),
                App.PInt("ord", order), App.PBit("auto", autoOrder), App.PBit("act", active), App.PDate("now", now), App.PBit("guard", guard), App.PBit("cross", cross)
            };
            List<string> sql = new List<string>();
            string dupGuard = "IF EXISTS (SELECT 1 FROM ROOMTYPE WHERE LOWER(TYPE_NAME) = LOWER(@name) AND ROOMTYPE_ID <> @tid) THROW 50003, N'DUPNAME', 1;";
            if (isNew)
            {
                sql.Add(dupGuard + " IF NOT EXISTS (SELECT 1 FROM ROOMTYPE WHERE ROOMTYPE_ID = @tid) INSERT INTO ROOMTYPE (ROOMTYPE_ID, TYPE_NAME, PURPOSE, COLOR, SPECIES, SIZES, NIGHT_ITEM_ID, SHORT_TEXT, LONG_TEXT, IMAGE, ONLINE, TYPE_ORDER, ACTIVE, SYSDTE) VALUES (@tid, @name, @pur, @col, @sp, @sz, @item, @short, @long, @img, @online, CASE WHEN @auto = 1 THEN ISNULL((SELECT MAX(TYPE_ORDER) FROM ROOMTYPE), 0) + 1 ELSE @ord END, @act, GETDATE())");
            }
            else
            {
                sql.Add(dupGuard + " IF @cross = 1 AND EXISTS (SELECT 1 FROM BOOKTYPE WHERE ROOMTYPE_ID = @tid) THROW 50007, N'BOOKTYPES', 1; IF @guard = 1 AND (" + TypeBusy + ") THROW 50004, N'INUSE', 1; UPDATE ROOMTYPE SET TYPE_NAME = @name, PURPOSE = @pur, COLOR = @col, SPECIES = @sp, SIZES = @sz, NIGHT_ITEM_ID = @item, SHORT_TEXT = @short, LONG_TEXT = @long, IMAGE = @img, ONLINE = @online, TYPE_ORDER = CASE WHEN @auto = 1 THEN TYPE_ORDER ELSE @ord END, ACTIVE = @act, SYSDTE = GETDATE() WHERE ROOMTYPE_ID = @tid; " + Changed);
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, isNew ? "add room type" : "edit room type", name + " failed", false);
                response.SetElementContents(result.Contains("DUPNAME") ? "ro-e-tname" : "ro-e-type", result.Contains("DUPNAME") ? "Another room type already has this name." : result.Contains("INUSE") ? "A patient or booking just started using this room type. Try again to see which." : result.Contains("BOOKTYPES") ? "A booking type now uses this room type, so it can't change between boarding and other use. Try again to see which." : result.Contains("STATECHANGED") ? "This room type was just changed or deleted by someone else. Close the form and try again." : "The room type could not be saved. Try again.");
                return response;
            }
            if (isNew)
            {
                await App.Log(c.U.StaffId, "add room type", name + " · " + PurposeName(purpose), true);
            }
            else
            {
                List<string> ch = new List<string>();
                Diff(ch, "name", cur!.TypeName, name);
                Diff(ch, "purpose", PurposeName(cur.Purpose), PurposeName(purpose));
                Diff(ch, "colour", cur.Color.ToUpperInvariant(), col);
                Diff(ch, "species", cur.Species, speciesCsv);
                Diff(ch, "sizes", cur.Sizes, sizes);
                Diff(ch, "night item", cur.NightItemId, item);
                Diff(ch, "short text", cur.ShortText, shortText);
                Diff(ch, "long text", cur.LongText, longText);
                Diff(ch, "picture", cur.Image, img);
                Diff(ch, "website", cur.Online ? "shown" : "hidden", online ? "shown" : "hidden");
                if (!autoOrder)
                {
                    Diff(ch, "order", cur.TypeOrder.ToString(Ui.Inv), order.ToString(Ui.Inv));
                }
                Diff(ch, "status", cur.Active ? "active" : "inactive", active ? "active" : "inactive");
                await App.Log(c.U.StaffId, "edit room type", cur.TypeName + " · " + (ch.Count == 0 ? "no changes" : string.Join(", ", ch)), true);
            }
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), isNew ? name + " added." : "Room type saved.");
            return response;
        }

        public async Task<ApiResponse> TypeActive()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'E');
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            string id = CleanId(GetDataValue("typeid"));
            bool active = Val("tact", 2) == "1";
            RoType? cur = id == string.Empty ? null : await TypeOne(id);
            if (cur == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), "This room type does not exist any more.");
                return response;
            }
            if (cur.Active == active)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), active ? "It is already active." : "It is already inactive.");
                return response;
            }
            if (!active)
            {
                var (inRoom, booked, use) = await TypeUse(id, now);
                if (inRoom + booked > 0)
                {
                    response.SetElementContents("ro-mbody", "<h2>" + Ui.Enc(cur.TypeName) + " can't be made inactive yet</h2>" + UseHtml("These patients and bookings still use it:", inRoom, booked, use, "Wait until these patients have left, or move or cancel the bookings first.", App.S(s, "clock24") == "1") + "<div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-accent\" onclick=\"" + Me + ".closeModal()\">OK</button></div>");
                    response.ExecuteScript(Me + ".shown('');");
                    return response;
                }
            }
            string result = await Helper.PutData(new List<string> { (active ? string.Empty : "IF " + TypeBusy + " THROW 50004, N'INUSE', 1; ") + "UPDATE ROOMTYPE SET ACTIVE = @act, SYSDTE = GETDATE() WHERE ROOMTYPE_ID = @tid; " + Changed }, new List<SqlParameter> { App.P("tid", id, 50), App.PBit("act", active), App.PDate("now", now) });
            if (Helper.IsError(result))
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), result.Contains("INUSE") ? "A patient or booking just started using it. Try again to see which." : "It could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, active ? "room type active" : "room type inactive", cur.TypeName, true);
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), cur.TypeName + (active ? " is active again." : " is now inactive."));
            return response;
        }

        private static string RefText(RoRefs r)
        {
            List<string> parts = new List<string>();
            if (r.Rooms > 0)
            {
                parts.Add(Ui.Plural(r.Rooms, "room", "rooms"));
            }
            if (r.BookTypes > 0)
            {
                parts.Add(Ui.Plural(r.BookTypes, "booking type", "booking types"));
            }
            if (r.Appts > 0)
            {
                parts.Add(Ui.Plural(r.Appts, "appointment", "appointments"));
            }
            if (r.Visits > 0)
            {
                parts.Add(Ui.Plural(r.Visits, "visit", "visits"));
            }
            if (r.Staff > 0)
            {
                parts.Add(Ui.Plural(r.Staff, "staff member's home room", "staff members' home room"));
            }
            return string.Join(", ", parts);
        }

        private static async Task<RoRefs> TypeRefs(string id)
        {
            var (row, emsg) = await Helper.GetOneT<RoRefs>("SELECT (SELECT COUNT(*) FROM ROOM WHERE ROOMTYPE_ID = @tid) AS Rooms, (SELECT COUNT(*) FROM BOOKTYPE WHERE ROOMTYPE_ID = @tid) AS BookTypes, (SELECT COUNT(*) FROM APPOINTMENT WHERE ROOMTYPE_ID = @tid) AS Appts, 0 AS Visits, 0 AS Staff", new List<SqlParameter> { App.P("tid", id, 50) });
            return emsg == string.Empty && row != null ? row : new RoRefs { Rooms = 1 };
        }

        private static async Task<RoRefs> RoomRefs(string id)
        {
            var (row, emsg) = await Helper.GetOneT<RoRefs>("SELECT 0 AS Rooms, 0 AS BookTypes, (SELECT COUNT(*) FROM APPOINTMENT WHERE ROOM_ID = @rid) AS Appts, (SELECT COUNT(*) FROM VISITROOM WHERE ROOM_ID = @rid) AS Visits, (SELECT COUNT(*) FROM STAFF WHERE HOME_ROOM_ID = @rid) AS Staff", new List<SqlParameter> { App.P("rid", id, 50) });
            return emsg == string.Empty && row != null ? row : new RoRefs { Visits = 1 };
        }

        public async Task<ApiResponse> DelType()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'D');
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("typeid"));
            RoType? cur = id == string.Empty ? null : await TypeOne(id);
            if (cur == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), "This room type does not exist any more.");
                return response;
            }
            RoRefs r = await TypeRefs(id);
            bool go = Val("sure", 2) == "1";
            string tid = Ui.Js(id);
            if (r.Rooms + r.BookTypes + r.Appts > 0)
            {
                StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(cur.TypeName) + " can't be deleted</h2><p class=\"ro-mlead\">It is still used by " + Ui.Enc(RefText(r)) + ". Records that point to it would break.</p>");
                sb.Append(cur.Active ? "<p class=\"ro-mlead\">Make it inactive instead: it stays in the records but is hidden from the boards and booking.</p>" : "<p class=\"ro-mlead\">It is already inactive, so it is hidden from the boards and booking.</p>");
                sb.Append("<div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button>" + (cur.Active && c.Can(Key, 'E') ? "<button type=\"button\" class=\"ro-btn ro-btn-accent\" onclick=\"" + Me + ".typeActive('" + tid + "', 0, this)\">Make inactive</button>" : string.Empty) + "</div>");
                response.SetElementContents("ro-mbody", sb.ToString());
                response.ExecuteScript(Me + ".shown('');");
                return response;
            }
            if (!go)
            {
                response.SetElementContents("ro-mbody", "<h2>Delete " + Ui.Enc(cur.TypeName) + "?</h2><p class=\"ro-mlead\">Nothing uses this room type, so it can be deleted for good. This can't be undone.</p><div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Keep it</button><button type=\"button\" class=\"ro-btn ro-btn-danger\" onclick=\"" + Me + ".delTypeGo('" + tid + "', this)\">Delete for good</button></div>");
                response.ExecuteScript(Me + ".shown('');");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "IF EXISTS (SELECT 1 FROM ROOM WHERE ROOMTYPE_ID = @tid) OR EXISTS (SELECT 1 FROM BOOKTYPE WHERE ROOMTYPE_ID = @tid) OR EXISTS (SELECT 1 FROM APPOINTMENT WHERE ROOMTYPE_ID = @tid) THROW 50006, N'USED', 1; DELETE FROM ROOMTYPE WHERE ROOMTYPE_ID = @tid" }, new List<SqlParameter> { App.P("tid", id, 50) });
            if (Helper.IsError(result))
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), result.Contains("USED") ? "Something started using this room type. It was not deleted." : "It could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete room type", cur.TypeName, true);
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), cur.TypeName + " was deleted.");
            return response;
        }

        public async Task<ApiResponse> MoveType()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'E');
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("typeid"));
            bool up = Val("mdir", 5) == "up";
            List<RoType> types = await Types();
            int i = types.FindIndex(t => t.TypeId == id);
            int j = up ? i - 1 : i + 1;
            if (i < 0 || j < 0 || j >= types.Count)
            {
                await Draw(response, c, Show(), string.Empty);
                return response;
            }
            List<string> ids = types.Select(t => t.TypeId).ToList();
            (ids[i], ids[j]) = (ids[j], ids[i]);
            List<SqlParameter> ps = new List<SqlParameter>();
            StringBuilder cs = new StringBuilder("UPDATE ROOMTYPE SET TYPE_ORDER = CASE ROOMTYPE_ID");
            for (int k = 0; k < ids.Count; k++)
            {
                cs.Append(" WHEN @t" + k + " THEN " + (k + 1).ToString(Ui.Inv));
                ps.Add(App.P("t" + k, ids[k], 50));
            }
            cs.Append(" ELSE TYPE_ORDER END, SYSDTE = GETDATE() WHERE ROOMTYPE_ID IN (" + string.Join(", ", Enumerable.Range(0, ids.Count).Select(k => "@t" + k)) + ")");
            string result = await Helper.PutData(new List<string> { cs.ToString() }, ps);
            if (!Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "reorder room types", types[i].TypeName + (up ? " moved up" : " moved down"), true);
            }
            await Draw(response, c, Show(), Helper.IsError(result) ? "The order could not be changed. Try again." : string.Empty);
            return response;
        }

        public async Task<ApiResponse> RoomForm()
        {
            ApiResponse response = new ApiResponse();
            string id = CleanId(GetDataValue("roomid"));
            Ctx? c = await Reach(response, id == string.Empty ? 'A' : 'E');
            if (c == null)
            {
                return response;
            }
            RoRoom r = new RoRoom { Active = true, TypeId = CleanId(GetDataValue("rtypeid")) };
            if (id != string.Empty)
            {
                RoRoom? row = await RoomOne(id);
                if (row == null)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await Draw(response, c, Show(), "This room does not exist any more.");
                    return response;
                }
                r = row;
            }
            List<RoType> types = await Types();
            StringBuilder to = new StringBuilder(r.TypeId == string.Empty || types.All(t => t.TypeId != r.TypeId) ? "<option value=\"\">Choose the room type</option>" : string.Empty);
            foreach (RoType t in types.Where(t => t.Active || t.TypeId == r.TypeId))
            {
                to.Append("<option value=\"" + Ui.Attr(t.TypeId) + "\"" + (t.TypeId == r.TypeId ? " selected" : string.Empty) + ">" + Ui.Enc(t.TypeName + " · " + PurposeName(t.Purpose) + (t.Active ? string.Empty : " (inactive)")) + "</option>");
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (id == string.Empty ? "Add a room" : "Edit room") + "</h2>");
            if (r.BusyCnt > 0)
            {
                sb.Append("<p class=\"ro-info\">" + Ui.Enc((r.BusyPet != string.Empty ? r.BusyPet : "A patient") + (r.BusyCnt > 1 ? " and " + Ui.Plural(r.BusyCnt - 1, "other patient", "other patients") : string.Empty) + " " + (r.BusyCnt > 1 ? "are" : "is") + " in this room now.") + "</p>");
            }
            sb.Append("<div class=\"ro-fgrid\" id=\"ro-rform\" data-roomid=\"" + Ui.Attr(r.RoomId) + "\">");
            sb.Append("<div>" + Field("Name", "<input id=\"ro-rname\" type=\"text\" maxlength=\"60\" value=\"" + Ui.Attr(r.RoomName) + "\" autocomplete=\"off\">", "rname") + "</div>");
            sb.Append("<div>" + Field("Room type", "<select id=\"ro-rtype\">" + to + "</select>", "rtype") + "</div>");
            sb.Append("<div class=\"ro-span2\">" + Field("Note <small>(for staff, for example: scale by the door)</small>", "<input id=\"ro-rnote\" type=\"text\" maxlength=\"200\" value=\"" + Ui.Attr(r.Note) + "\">", "rnote") + "</div>");
            sb.Append("<div>" + Field("Order <small>(lower comes first; empty = last)</small>", "<input id=\"ro-rord\" type=\"number\" inputmode=\"numeric\" min=\"0\" max=\"9999\" value=\"" + (id == string.Empty ? string.Empty : r.RoomOrder.ToString(Ui.Inv)) + "\">", "rord") + "</div>");
            sb.Append("<div class=\"ro-cbox\"><label class=\"ro-check\"><input type=\"checkbox\" id=\"ro-ract\"" + (r.Active ? " checked" : string.Empty) + ">Active</label><em class=\"ro-err\" id=\"ro-e-ract\"></em></div>");
            sb.Append("</div><div id=\"ro-ruse\"></div><em class=\"ro-err ro-merr\" id=\"ro-e-room\"></em><div class=\"ro-mfoot\">");
            if (id != string.Empty && c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-danger ro-mleft\" onclick=\"" + Me + ".delRoom('" + Ui.Js(r.RoomId) + "', this)\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"ro-btn ro-btn-accent\" id=\"ro-rgo\" onclick=\"" + Me + ".saveRoom()\">" + (id == string.Empty ? "Add room" : "Save changes") + "</button></div>");
            response.SetElementContents("ro-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('ro-rname');");
            return response;
        }

        public async Task<ApiResponse> SaveRoom()
        {
            ApiResponse response = new ApiResponse();
            string id = CleanId(GetDataValue("roomid"));
            bool isNew = id == string.Empty;
            Ctx? c = await Reach(response, isNew ? 'A' : 'E');
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            foreach (string e in new[] { "rname", "rtype", "rnote", "rord", "ract", "room" })
            {
                response.SetElementContents("ro-e-" + e, string.Empty);
            }
            response.SetElementContents("ro-ruse", string.Empty);
            RoRoom? cur = null;
            if (!isNew)
            {
                cur = await RoomOne(id);
                if (cur == null)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await Draw(response, c, Show(), "This room does not exist any more.");
                    return response;
                }
            }
            else
            {
                id = NewKey(GetDataValue("newid"));
                if (id == string.Empty)
                {
                    response.SetElementContents("ro-e-room", "Something went wrong. Close the form and try again.");
                    return response;
                }
            }
            bool bad = false;
            string name = Ui.OneLine(GetDataValue("rname"), 60);
            if (name == string.Empty)
            {
                response.SetElementContents("ro-e-rname", "Write a name for the room.");
                bad = true;
            }
            string tid = CleanId(GetDataValue("rtype"));
            RoType? type = tid == string.Empty ? null : await TypeOne(tid);
            if (type == null || (!type.Active && tid != (cur?.TypeId ?? string.Empty)))
            {
                response.SetElementContents("ro-e-rtype", "Choose an active room type.");
                bad = true;
            }
            string note = Ui.OneLine(GetDataValue("rnote"), 400);
            if (note.Length > 200)
            {
                response.SetElementContents("ro-e-rnote", "Keep the note under 200 characters.");
                bad = true;
            }
            string ordText = Val("rord", 10);
            int order = 0;
            bool autoOrder = ordText == string.Empty;
            if (!autoOrder && (!int.TryParse(ordText, NumberStyles.Integer, Ui.Inv, out order) || order < 0 || order > 9999))
            {
                response.SetElementContents("ro-e-rord", "Write a whole number from 0 to 9999, or leave it empty.");
                bad = true;
            }
            bool active = Val("ract", 2) == "1";
            if (bad)
            {
                response.SetElementContents("ro-e-room", "Check the fields marked in red.");
                return response;
            }
            var (dup, derr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ROOM WHERE LOWER(ROOM_NAME) = LOWER(@name) AND ROOM_ID <> @rid", new List<SqlParameter> { App.P("name", name, 60), App.P("rid", id, 50) });
            if (derr == string.Empty && dup.Rows.Count > 0 && Convert.ToInt32(dup.Rows[0][0]) > 0)
            {
                response.SetElementContents("ro-e-rname", "Another room already has this name.");
                return response;
            }
            bool guard = cur != null && ((cur.Active && !active) || cur.TypeId != tid);
            if (guard)
            {
                var (inRoom, booked, use) = await RoomUse(id, now);
                if (inRoom + booked > 0)
                {
                    string why = cur!.Active && !active ? "This room can't be made inactive yet:" : "This room can't move to another room type yet:";
                    response.SetElementContents("ro-ruse", UseHtml(why, inRoom, booked, use, "Wait until the patient has left, or move the bookings to another room first.", App.S(s, "clock24") == "1"));
                    response.SetElementContents("ro-e-room", "Not saved.");
                    return response;
                }
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("rid", id, 50), App.P("tid", tid, 50), App.P("name", name, 60), App.P("note", note, 200), App.PInt("ord", order), App.PBit("auto", autoOrder), App.PBit("act", active), App.PDate("now", now), App.PBit("guard", guard)
            };
            string dupGuard = "IF EXISTS (SELECT 1 FROM ROOM WHERE LOWER(ROOM_NAME) = LOWER(@name) AND ROOM_ID <> @rid) THROW 50003, N'DUPNAME', 1;";
            string sql = isNew
                ? dupGuard + " IF NOT EXISTS (SELECT 1 FROM ROOM WHERE ROOM_ID = @rid) INSERT INTO ROOM (ROOM_ID, ROOMTYPE_ID, ROOM_NAME, NOTE, ROOM_ORDER, ACTIVE, SYSDTE) VALUES (@rid, @tid, @name, @note, CASE WHEN @auto = 1 THEN ISNULL((SELECT MAX(ROOM_ORDER) FROM ROOM WHERE ROOMTYPE_ID = @tid), ISNULL((SELECT MAX(ROOM_ORDER) FROM ROOM), 0)) + 1 ELSE @ord END, @act, GETDATE())"
                : dupGuard + " IF @guard = 1 AND (" + RoomBusy + ") THROW 50004, N'INUSE', 1; UPDATE ROOM SET ROOMTYPE_ID = @tid, ROOM_NAME = @name, NOTE = @note, ROOM_ORDER = CASE WHEN @auto = 1 THEN ROOM_ORDER ELSE @ord END, ACTIVE = @act, SYSDTE = GETDATE() WHERE ROOM_ID = @rid; " + Changed;
            string result = await Helper.PutData(new List<string> { sql }, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, isNew ? "add room" : "edit room", name + " failed", false);
                response.SetElementContents(result.Contains("DUPNAME") ? "ro-e-rname" : "ro-e-room", result.Contains("DUPNAME") ? "Another room already has this name." : result.Contains("INUSE") ? "A patient or booking just started using this room. Try again to see which." : result.Contains("STATECHANGED") ? "This room was just changed or deleted by someone else. Close the form and try again." : "The room could not be saved. Try again.");
                return response;
            }
            if (isNew)
            {
                await App.Log(c.U.StaffId, "add room", name + " · " + (type?.TypeName ?? string.Empty), true);
            }
            else
            {
                List<string> ch = new List<string>();
                Diff(ch, "name", cur!.RoomName, name);
                Diff(ch, "type", cur.TypeId, tid);
                Diff(ch, "note", cur.Note, note);
                if (!autoOrder)
                {
                    Diff(ch, "order", cur.RoomOrder.ToString(Ui.Inv), order.ToString(Ui.Inv));
                }
                Diff(ch, "status", cur.Active ? "active" : "inactive", active ? "active" : "inactive");
                await App.Log(c.U.StaffId, "edit room", cur.RoomName + " · " + (ch.Count == 0 ? "no changes" : string.Join(", ", ch)), true);
            }
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), isNew ? name + " added." : "Room saved.");
            return response;
        }

        public async Task<ApiResponse> RoomActive()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'E');
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            string id = CleanId(GetDataValue("roomid"));
            bool active = Val("ract", 2) == "1";
            RoRoom? cur = id == string.Empty ? null : await RoomOne(id);
            if (cur == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), "This room does not exist any more.");
                return response;
            }
            if (cur.Active == active)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), active ? "It is already active." : "It is already inactive.");
                return response;
            }
            if (active)
            {
                RoType? t = await TypeOne(cur.TypeId);
                if (t == null || !t.Active)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await Draw(response, c, Show(), "Its room type is inactive. Make the room type active first, or move the room to another type.");
                    return response;
                }
            }
            else
            {
                var (inRoom, booked, use) = await RoomUse(id, now);
                if (inRoom + booked > 0)
                {
                    response.SetElementContents("ro-mbody", "<h2>" + Ui.Enc(cur.RoomName) + " can't be made inactive yet</h2>" + UseHtml("It is still in use:", inRoom, booked, use, "Wait until the patient has left, or move the bookings to another room first.", App.S(s, "clock24") == "1") + "<div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-accent\" onclick=\"" + Me + ".closeModal()\">OK</button></div>");
                    response.ExecuteScript(Me + ".shown('');");
                    return response;
                }
            }
            string result = await Helper.PutData(new List<string> { (active ? string.Empty : "IF " + RoomBusy + " THROW 50004, N'INUSE', 1; ") + "UPDATE ROOM SET ACTIVE = @act, SYSDTE = GETDATE() WHERE ROOM_ID = @rid; " + Changed }, new List<SqlParameter> { App.P("rid", id, 50), App.PBit("act", active), App.PDate("now", now) });
            if (Helper.IsError(result))
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), result.Contains("INUSE") ? "A patient or booking just started using it. Try again to see which." : "It could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, active ? "room active" : "room inactive", cur.RoomName, true);
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), cur.RoomName + (active ? " is active again." : " is now inactive."));
            return response;
        }

        public async Task<ApiResponse> DelRoom()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'D');
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("roomid"));
            RoRoom? cur = id == string.Empty ? null : await RoomOne(id);
            if (cur == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), "This room does not exist any more.");
                return response;
            }
            RoRefs r = await RoomRefs(id);
            string rid = Ui.Js(id);
            if (r.Appts + r.Visits + r.Staff > 0)
            {
                StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(cur.RoomName) + " can't be deleted</h2><p class=\"ro-mlead\">It is in the records of " + Ui.Enc(RefText(r)) + ".</p>");
                sb.Append(cur.Active ? "<p class=\"ro-mlead\">Make it inactive instead: it stays in the history but can't be picked any more.</p>" : "<p class=\"ro-mlead\">It is already inactive, so it can't be picked any more.</p>");
                sb.Append("<div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button>" + (cur.Active && c.Can(Key, 'E') ? "<button type=\"button\" class=\"ro-btn ro-btn-accent\" onclick=\"" + Me + ".roomActive('" + rid + "', 0, this)\">Make inactive</button>" : string.Empty) + "</div>");
                response.SetElementContents("ro-mbody", sb.ToString());
                response.ExecuteScript(Me + ".shown('');");
                return response;
            }
            if (Val("sure", 2) != "1")
            {
                response.SetElementContents("ro-mbody", "<h2>Delete " + Ui.Enc(cur.RoomName) + "?</h2><p class=\"ro-mlead\">This room has never been used, so it can be deleted for good. This can't be undone.</p><div class=\"ro-mfoot\"><button type=\"button\" class=\"ro-btn ro-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Keep it</button><button type=\"button\" class=\"ro-btn ro-btn-danger\" onclick=\"" + Me + ".delRoomGo('" + rid + "', this)\">Delete for good</button></div>");
                response.ExecuteScript(Me + ".shown('');");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "IF EXISTS (SELECT 1 FROM VISITROOM WHERE ROOM_ID = @rid) OR EXISTS (SELECT 1 FROM APPOINTMENT WHERE ROOM_ID = @rid) OR EXISTS (SELECT 1 FROM STAFF WHERE HOME_ROOM_ID = @rid) THROW 50006, N'USED', 1; DELETE FROM ROOM WHERE ROOM_ID = @rid" }, new List<SqlParameter> { App.P("rid", id, 50) });
            if (Helper.IsError(result))
            {
                response.ExecuteScript(Me + ".closeModal();");
                await Draw(response, c, Show(), result.Contains("USED") ? "Something started using this room. It was not deleted." : "It could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete room", cur.RoomName, true);
            response.ExecuteScript(Me + ".closeModal();");
            await Draw(response, c, Show(), cur.RoomName + " was deleted.");
            return response;
        }

        public async Task<ApiResponse> MoveRoom()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Reach(response, 'E');
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("roomid"));
            bool up = Val("mdir", 5) == "up";
            RoRoom? cur = id == string.Empty ? null : await RoomOne(id);
            if (cur == null)
            {
                await Draw(response, c, Show(), "This room does not exist any more.");
                return response;
            }
            List<RoRoom> all = (await Rooms()).Where(r => r.TypeId == cur.TypeId).ToList();
            int i = all.FindIndex(r => r.RoomId == id);
            int j = up ? i - 1 : i + 1;
            if (i < 0 || j < 0 || j >= all.Count)
            {
                await Draw(response, c, Show(), string.Empty);
                return response;
            }
            List<int> vals = all.Select(r => r.RoomOrder).OrderBy(v => v).ToList();
            if (vals.Distinct().Count() != vals.Count)
            {
                int b = vals[0];
                vals = Enumerable.Range(0, all.Count).Select(k => b + k).ToList();
            }
            List<string> ids = all.Select(r => r.RoomId).ToList();
            (ids[i], ids[j]) = (ids[j], ids[i]);
            List<SqlParameter> ps = new List<SqlParameter>();
            StringBuilder cs = new StringBuilder("UPDATE ROOM SET ROOM_ORDER = CASE ROOM_ID");
            for (int k = 0; k < ids.Count; k++)
            {
                cs.Append(" WHEN @r" + k + " THEN " + vals[k].ToString(Ui.Inv));
                ps.Add(App.P("r" + k, ids[k], 50));
            }
            cs.Append(" ELSE ROOM_ORDER END, SYSDTE = GETDATE() WHERE ROOM_ID IN (" + string.Join(", ", Enumerable.Range(0, ids.Count).Select(k => "@r" + k)) + ")");
            string result = await Helper.PutData(new List<string> { cs.ToString() }, ps);
            if (!Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "reorder rooms", cur.RoomName + (up ? " moved up" : " moved down"), true);
            }
            await Draw(response, c, Show(), Helper.IsError(result) ? "The order could not be changed. Try again." : string.Empty);
            return response;
        }
    }
}
