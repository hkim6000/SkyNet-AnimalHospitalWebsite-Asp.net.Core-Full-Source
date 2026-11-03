using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminBookTypes : WebPage
    {
        private const string Key = "booktypes";
        private const string Me = "AdminBookTypesJs";
        private const string Cols = "B.BOOKTYPE_ID AS BooktypeId, B.BOOK_NAME AS BookName, B.TEAM_KEY AS TeamKey, B.VISIT_TYPE AS VisitType, B.SHORT_TEXT AS ShortText, B.LONG_TEXT AS LongText, B.EXPECT AS Expect, B.MINUTES AS Minutes, B.MINUTES_M AS MinutesM, B.MINUTES_L AS MinutesL, B.ITEM_ID AS ItemId, B.ROOMTYPE_ID AS RoomtypeId, B.SPECIES AS Species, B.DROP_FROM AS DropFrom, B.DROP_TO AS DropTo, B.PICKUP_HM AS PickupHm, B.PREOP AS Preop, B.IMAGE AS Image, B.ONLINE AS Online, B.NEW_OK AS NewOk, B.BOOK_ORDER AS BookOrder, B.ACTIVE AS Active, ISNULL(I.ITEM_NAME, N'') AS ItemName, I.PRICE AS Price, I.PRICE_M AS PriceM, I.PRICE_L AS PriceL, ISNULL(T.TYPE_NAME, N'') AS RoomName, (SELECT COUNT(*) FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.BOOKTYPE_ID = B.BOOKTYPE_ID AND S.ACTIVE = 1) AS StaffCount, ISNULL((SELECT STRING_AGG(S.STAFF_NAME, N', ') FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.BOOKTYPE_ID = B.BOOKTYPE_ID AND S.ACTIVE = 1), N'') AS StaffNames";
        private const string From = " FROM BOOKTYPE B LEFT JOIN ITEM I ON I.ITEM_ID = B.ITEM_ID LEFT JOIN ROOMTYPE T ON T.ROOMTYPE_ID = B.ROOMTYPE_ID";

        private static readonly (string Key, string Name)[] VisitTypes = { ("clinic", "Clinic visit"), ("surgery", "Surgery or dental (drop-off)"), ("grooming", "Grooming") };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Booking types | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--bt-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("bt"));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key), max);
        }

        private string NewKey()
        {
            string k = Val("newid", 40);
            return Regex.IsMatch(k, "^[a-f0-9]{32}$") ? k : string.Empty;
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
            response.ExecuteScript(Me + ".unlock();" + Me + ".toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"bt-panel bt-deny\"><h1>Booking types</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"bt-btn bt-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static string VtName(string key)
        {
            return key switch { "surgery" => "Surgery", "grooming" => "Grooming", _ => "Clinic" };
        }

        private static string Clock(string hm, Dictionary<string, string> s)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? hm : Ui.Hm(DateTime.Today.Add(t.Value), App.S(s, "clock24") == "1");
        }

        private static List<string> ItemTypesFor(string vt)
        {
            return vt switch
            {
                "surgery" => new List<string> { "practice" },
                "grooming" => new List<string> { "grooming" },
                _ => new List<string> { "practice", "test", "vaccine" }
            };
        }

        private static List<string> Images()
        {
            List<string> list = new List<string>();
            try
            {
                string dir = Path.Combine(App.Root, "images", "hospital", "full");
                if (Directory.Exists(dir))
                {
                    foreach (string f in Directory.GetFiles(dir, "*.webp").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        string rel = "images/hospital/full/" + Path.GetFileName(f);
                        if (Ui.SafePath(rel))
                        {
                            list.Add(rel);
                        }
                    }
                }
            }
            catch (IOException)
            {
                return list;
            }
            catch (UnauthorizedAccessException)
            {
                return list;
            }
            return list;
        }

        private static string ImageName(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path).Replace('-', ' ');
            return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n.Substring(1) : n;
        }

        private static async Task<List<BktTeam>> Teams()
        {
            var (list, emsg) = await Helper.GetListT<BktTeam>("SELECT TEAM_KEY AS TeamKey, TEAM_NAME AS TeamName, ACTIVE AS Active FROM TEAM ORDER BY TEAM_ORDER, TEAM_NAME");
            return emsg == string.Empty && list != null ? list : new List<BktTeam>();
        }

        private static async Task<List<BktSpecies>> SpeciesList()
        {
            var (list, emsg) = await Helper.GetListT<BktSpecies>("SELECT SPECIES_KEY AS SpeciesKey, SPECIES_NAME AS SpeciesName, PLURAL_NAME AS PluralName FROM SPECIES WHERE ACTIVE = 1 ORDER BY SPECIES_ORDER, SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<BktSpecies>();
        }

        private static async Task<List<BktStaff>> Providers()
        {
            var (list, emsg) = await Helper.GetListT<BktStaff>("SELECT S.STAFF_ID AS StaffId, S.STAFF_NAME AS StaffName, S.PROVIDER_TYPE AS ProviderType, ISNULL((SELECT STRING_AGG(X.SPECIES_KEY, N',') FROM STAFFSPECIES X WHERE X.STAFF_ID = S.STAFF_ID), N'') AS Species FROM STAFF S WHERE S.ACTIVE = 1 AND S.PROVIDER_TYPE <> N'' ORDER BY CASE S.PROVIDER_TYPE WHEN N'vet' THEN 1 WHEN N'tech' THEN 2 WHEN N'groomer' THEN 3 ELSE 4 END, S.STAFF_NAME");
            return emsg == string.Empty && list != null ? list : new List<BktStaff>();
        }

        private static async Task<BktRow?> Book(string id)
        {
            var (row, emsg) = await Helper.GetOneT<BktRow>("SELECT " + Cols + From + " WHERE B.BOOKTYPE_ID = @bid", new List<SqlParameter> { App.P("bid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static string SpeciesText(string csv, List<BktSpecies> sp, bool plural)
        {
            List<string> keys = (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (keys.Count == 0)
            {
                return "All species";
            }
            return string.Join(", ", keys.Select(k =>
            {
                BktSpecies? x = sp.FirstOrDefault(y => y.SpeciesKey == k);
                return x == null ? k : plural && x.PluralName != string.Empty ? x.PluralName : x.SpeciesName;
            }));
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("bt-menu", Auth.Menu("bt", Me, "AdminBookTypes", c));
            response.SetElementContents("bt-who", Auth.Who("bt", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bt-main", Denied());
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bt-ptop\"><div><h1>Booking types</h1><p>What can be booked, how long it takes, who can do it and what it costs. The desk uses these now, and the website will use the ones marked online.</p></div>");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<div class=\"bt-acts\"><button type=\"button\" class=\"bt-btn bt-btn-accent\" onclick=\"" + Me + ".add(this)\">Add booking type</button></div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"bt-bar\"><label class=\"bt-field bt-grow2\"><span>Search</span><input type=\"search\" id=\"bt-term\" maxlength=\"80\" placeholder=\"Name\" autocomplete=\"off\" oninput=\"" + Me + ".typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();" + Me + ".find();}\"></label>");
            sb.Append("<label class=\"bt-field\"><span>Visit type</span><select id=\"bt-vt\" onchange=\"" + Me + ".find()\"><option value=\"\">All visit types</option>" + Ui.Opts(VisitTypes.Select(v => (v.Key, VtName(v.Key))), string.Empty) + "</select></label>");
            sb.Append("<label class=\"bt-field\"><span>Show</span><select id=\"bt-show\" onchange=\"" + Me + ".find()\"><option value=\"active\">Active booking types</option><option value=\"inactive\">Inactive booking types</option><option value=\"all\">All booking types</option></select></label></div>");
            sb.Append("<div id=\"bt-list\">" + await ListHtml(c, string.Empty, string.Empty, "active") + "</div>");
            response.SetElementContents("bt-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> List()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bt-main", Denied());
                return response;
            }
            string vt = Val("fvt", 20);
            string show = Val("show", 10);
            response.SetElementContents("bt-list", await ListHtml(c, Ui.OneLine(GetDataValue("term"), 80), VisitTypes.Any(v => v.Key == vt) ? vt : string.Empty, show == "inactive" || show == "all" ? show : "active"));
            return response;
        }

        private static string Like(string t)
        {
            return "%" + t.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private static string Minutes(BktRow r, Dictionary<string, string> s)
        {
            if (r.VisitType == "surgery")
            {
                string main = "<b>" + r.Minutes + " min</b> <span class=\"bt-dim\">surgery</span>";
                if (Ui.ParseHm(r.DropFrom) != null && Ui.ParseHm(r.PickupHm) != null)
                {
                    main += "<small class=\"bt-pp\">Drop-off " + Ui.Enc(Clock(r.DropFrom, s)) + "&ndash;" + Ui.Enc(Clock(r.DropTo, s)) + ", pick-up " + Ui.Enc(Clock(r.PickupHm, s)) + "</small>";
                }
                return main;
            }
            if (r.MinutesM > 0 || r.MinutesL > 0)
            {
                return "<b>" + r.Minutes + " min</b><small class=\"bt-pp\">M " + (r.MinutesM > 0 ? r.MinutesM : r.Minutes) + " &middot; L " + (r.MinutesL > 0 ? r.MinutesL : r.Minutes) + " min</small>";
            }
            return "<b>" + r.Minutes + " min</b>";
        }

        private static string PriceText(BktRow r)
        {
            if (r.ItemId == string.Empty || r.Price == null)
            {
                return "<span class=\"bt-dim\">No price</span>";
            }
            List<string> more = new List<string>();
            if ((r.PriceM ?? 0) > 0)
            {
                more.Add("M " + Ui.Money(r.PriceM ?? 0));
            }
            if ((r.PriceL ?? 0) > 0)
            {
                more.Add("L " + Ui.Money(r.PriceL ?? 0));
            }
            return "<b>" + Ui.Money(r.Price.Value) + "</b>" + (more.Count > 0 ? "<small class=\"bt-pp\">" + string.Join(" &middot; ", more) + "</small>" : string.Empty);
        }

        private static async Task<string> ListHtml(Ctx c, string term, string vt, string show)
        {
            string where = " WHERE (@t = N'' OR B.BOOK_NAME LIKE @tl) AND (@vt = N'' OR B.VISIT_TYPE = @vt)" + (show == "active" ? " AND B.ACTIVE = 1" : show == "inactive" ? " AND B.ACTIVE = 0" : string.Empty);
            var (rows, emsg) = await Helper.GetListT<BktRow>("SELECT " + Cols + From + where + " ORDER BY B.BOOK_ORDER, B.BOOK_NAME", new List<SqlParameter> { App.P("t", term, 80), App.P("tl", Like(term), 300), App.P("vt", vt, 20) });
            if (emsg != string.Empty || rows == null)
            {
                return "<div class=\"bt-panel\"><p>The booking types could not be loaded. Try again in a moment.</p></div>";
            }
            if (rows.Count == 0)
            {
                return "<div class=\"bt-panel bt-empty\"><p>" + (term != string.Empty ? "No booking type matches &ldquo;" + Ui.Enc(term) + "&rdquo;." : show == "inactive" ? "There are no inactive booking types." : "There are no booking types here yet.") + "</p></div>";
            }
            Dictionary<string, string> s = await App.Settings();
            List<BktTeam> teams = await Teams();
            List<(string Key, string Name)> groups = teams.Select(t => (t.TeamKey, t.TeamName)).ToList();
            if (rows.Any(r => !teams.Any(t => t.TeamKey == r.TeamKey)))
            {
                groups.Add(("~none", "No team"));
            }
            StringBuilder sb = new StringBuilder();
            foreach ((string key, string name) in groups)
            {
                List<BktRow> list = key == "~none" ? rows.Where(r => !teams.Any(t => t.TeamKey == r.TeamKey)).ToList() : rows.Where(r => r.TeamKey == key).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<section class=\"bt-grp\"><div class=\"bt-shead\"><h2>" + Ui.Enc(name) + "</h2><span class=\"bt-sub\">" + Ui.Plural(list.Count, "booking type", "booking types") + "</span></div>");
                sb.Append("<div class=\"bt-tw\"><table class=\"bt-table\"><thead><tr><th>Name</th><th>Visit type</th><th>Time</th><th class=\"bt-num\">Price</th><th>Room</th><th>Who</th><th>Website</th></tr></thead><tbody>");
                foreach (BktRow r in list)
                {
                    string js = Me + ".open('" + Ui.Js(r.BooktypeId) + "')";
                    sb.Append("<tr class=\"bt-row" + (r.Active ? string.Empty : " bt-off") + "\" tabindex=\"0\" onclick=\"" + js + "\" onkeydown=\"if(event.key==='Enter'){" + js + ";}\">");
                    sb.Append("<td class=\"bt-cname\"><b>" + Ui.Enc(r.BookName) + (r.Active ? string.Empty : " <span class=\"bt-st bt-st-off\">Inactive</span>") + "</b>" + (r.ShortText != string.Empty ? "<small>" + Ui.Enc(r.ShortText) + "</small>" : string.Empty) + "</td>");
                    sb.Append("<td data-l=\"Visit type\">" + VtName(r.VisitType) + "</td>");
                    sb.Append("<td data-l=\"Time\" class=\"bt-nw\"><span class=\"bt-v\">" + Minutes(r, s) + "</span></td>");
                    sb.Append("<td data-l=\"Price\" class=\"bt-num\"><span class=\"bt-v\">" + PriceText(r) + "</span></td>");
                    sb.Append("<td data-l=\"Room\">" + (r.RoomName != string.Empty ? Ui.Enc(r.RoomName) : "<span class=\"bt-dim\">Any</span>") + "</td>");
                    sb.Append("<td data-l=\"Who\"><span title=\"" + Ui.Attr(r.StaffNames) + "\">" + (r.StaffCount > 0 ? Ui.Plural(r.StaffCount, "person", "people") : "<b class=\"bt-late\">Nobody</b>") + "</span></td>");
                    sb.Append("<td data-l=\"Website\"><span class=\"bt-v\">" + (r.Online ? "<span class=\"bt-st bt-st-on\">Online</span>" : "<span class=\"bt-dim\">Desk only</span>") + "<small class=\"bt-pp\">" + (r.NewOk ? "New clients welcome" : "Existing clients only") + "</small></span></td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("bookid"));
            if (!c.Can(Key, 'V') || (id == string.Empty && !c.Can(Key, 'A')))
            {
                Toast(response, "You can't add booking types.");
                return response;
            }
            BktRow? b = null;
            int appts = 0;
            if (id != string.Empty)
            {
                b = await Book(id);
                if (b == null)
                {
                    Toast(response, "This booking type was not found. It may have been deleted.");
                    return response;
                }
                appts = await Appts(id);
            }
            response.SetElementContents("bt-mbody", await FormHtml(c, b, appts));
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(" + (b == null ? "true" : "false") + ");");
            return response;
        }

        private static async Task<int> Appts(string id)
        {
            var (dt, _) = await Helper.GetDataTable("SELECT COUNT(*) FROM APPOINTMENT WHERE BOOKTYPE_ID = @bid", new List<SqlParameter> { App.P("bid", id, 50) });
            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 1;
        }

        private static string Field(string id, string label, string value, int max, string type = "text", string extra = "", string hint = "", string attrs = "")
        {
            return "<label class=\"bt-field" + (extra != string.Empty ? " " + extra : string.Empty) + "\"><span>" + label + (hint != string.Empty ? " <small>" + hint + "</small>" : string.Empty) + "</span><input type=\"" + type + "\" id=\"bt-f-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\" autocomplete=\"off\"" + attrs + "><em class=\"bt-err\" id=\"bt-e-" + id + "\"></em></label>";
        }

        private static string Area(string id, string label, string value, int max, int rows, string extra = "", string hint = "", string ph = "")
        {
            return "<label class=\"bt-field" + (extra != string.Empty ? " " + extra : string.Empty) + "\"><span>" + label + (hint != string.Empty ? " <small>" + hint + "</small>" : string.Empty) + "</span><textarea id=\"bt-f-" + id + "\" rows=\"" + rows + "\" maxlength=\"" + max + "\"" + (ph != string.Empty ? " placeholder=\"" + Ui.Attr(ph) + "\"" : string.Empty) + ">" + Ui.Enc(value) + "</textarea><em class=\"bt-err\" id=\"bt-e-" + id + "\"></em></label>";
        }

        private static string ProvName(string t)
        {
            return t switch { "vet" => "Vet", "tech" => "Technician", "groomer" => "Groomer", _ => t };
        }

        private static async Task<string> FormHtml(Ctx c, BktRow? b, int appts)
        {
            bool add = b == null;
            bool can = add ? c.Can(Key, 'A') : c.Can(Key, 'E');
            BktRow v = b ?? new BktRow { VisitType = "clinic", Minutes = 30, Online = true, NewOk = true, Active = true };
            List<BktTeam> teams = await Teams();
            List<BktSpecies> sp = await SpeciesList();
            List<BktStaff> staff = await Providers();
            var (items, _) = await Helper.GetListT<BktItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, ITEM_TYPE AS ItemType, PRICE AS Price, ACTIVE AS Active FROM ITEM WHERE ITEM_TYPE IN (N'practice', N'test', N'vaccine', N'grooming') AND (ACTIVE = 1 OR ITEM_ID = @iid) ORDER BY ITEM_TYPE, ITEM_ORDER, ITEM_NAME", new List<SqlParameter> { App.P("iid", v.ItemId, 50) });
            var (rooms, _) = await Helper.GetListT<BktRoom>("SELECT ROOMTYPE_ID AS RoomtypeId, TYPE_NAME AS TypeName, PURPOSE AS Purpose, ACTIVE AS Active FROM ROOMTYPE WHERE PURPOSE <> N'boarding' AND (ACTIVE = 1 OR ROOMTYPE_ID = @rt) ORDER BY TYPE_ORDER, TYPE_NAME", new List<SqlParameter> { App.P("rt", v.RoomtypeId, 50) });
            var (picked, _) = await Helper.GetDataTable("SELECT STAFF_ID FROM BOOKSTAFF WHERE BOOKTYPE_ID = @bid", new List<SqlParameter> { App.P("bid", v.BooktypeId, 50) });
            HashSet<string> who = new HashSet<string>(picked.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToString(r[0]) ?? string.Empty));
            HashSet<string> mine = new HashSet<string>((v.Species ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            List<string> images = Images();
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "New booking type" : Ui.Enc(v.BookName)) + "</h2>");
            if (!add)
            {
                sb.Append("<p class=\"bt-mlead\">" + VtName(v.VisitType) + " &middot; " + (appts > 0 ? Ui.Plural(appts, "appointment", "appointments") + " booked with it so far" : "not booked yet") + (v.Active ? string.Empty : " &middot; <b>inactive</b>") + "</p>");
            }
            if (!can)
            {
                sb.Append("<p class=\"bt-info\">You can look at this booking type. Ask a manager if something needs to change.</p>");
            }
            sb.Append("<form class=\"bt-form\" onsubmit=\"return " + Me + ".save()\" novalidate><input type=\"hidden\" id=\"bt-f-id\" value=\"" + Ui.Attr(v.BooktypeId) + "\"><fieldset class=\"bt-fs\"" + (can ? string.Empty : " disabled") + ">");
            sb.Append("<div class=\"bt-fgrid\">");
            sb.Append(Field("name", "Name", v.BookName, 100, "text", "bt-span2", "what owners see, like Wellness exam"));
            List<(string, string)> teamOpts = teams.Where(t => (t.Active && t.TeamKey != "boarding") || t.TeamKey == v.TeamKey).Select(t => (t.TeamKey, t.TeamName)).ToList();
            if (add || !teamOpts.Any(t => t.Item1 == v.TeamKey))
            {
                teamOpts.Insert(0, (string.Empty, "Pick a team"));
            }
            sb.Append("<label class=\"bt-field\"><span>Team <small>groups it on the website</small></span><select id=\"bt-f-team\">" + Ui.Opts(teamOpts, v.TeamKey) + "</select><em class=\"bt-err\" id=\"bt-e-team\"></em></label>");
            sb.Append("<label class=\"bt-field\"><span>Visit type</span><select id=\"bt-f-vt\" onchange=\"" + Me + ".vtChg()\">" + Ui.Opts(VisitTypes.Select(x => (x.Key, x.Name)), v.VisitType) + "</select><em class=\"bt-err\" id=\"bt-e-vt\"></em></label>");
            sb.Append(Field("short", "Short text", v.ShortText, 200, "text", "bt-span2", "one line under the name"));
            sb.Append(Area("long", "Long text", v.LongText, 2000, 3, "bt-span2", "the full description on the website"));
            sb.Append(Area("expect", "What to expect", v.Expect, 1000, 3, "bt-span2", "one point per line", "Full nose-to-tail check\nWeight and body condition"));
            sb.Append("<h3 class=\"bt-span2 bt-sec2\">Time and price</h3>");
            sb.Append("<div class=\"bt-span2 bt-trio\">");
            sb.Append(Field("min", "<span id=\"bt-minl\">" + (v.VisitType == "surgery" ? "Surgery length (minutes)" : v.VisitType == "grooming" ? "Minutes, small pet" : "Minutes") + "</span>", v.Minutes.ToString(Ui.Inv), 3, "text", string.Empty, string.Empty, " inputmode=\"numeric\""));
            sb.Append("<div class=\"bt-fsp\" id=\"bt-sizew\"" + (v.VisitType == "grooming" ? string.Empty : " hidden") + ">" + Field("minm", "Minutes, medium dog", v.MinutesM > 0 ? v.MinutesM.ToString(Ui.Inv) : string.Empty, 3, "text", string.Empty, "blank = same", " inputmode=\"numeric\"") + "</div>");
            sb.Append("<div class=\"bt-fsp\" id=\"bt-sizew2\"" + (v.VisitType == "grooming" ? string.Empty : " hidden") + ">" + Field("minl", "Minutes, large dog", v.MinutesL > 0 ? v.MinutesL.ToString(Ui.Inv) : string.Empty, 3, "text", string.Empty, "blank = same", " inputmode=\"numeric\"") + "</div>");
            sb.Append("</div>");
            sb.Append("<label class=\"bt-field\"><span>Item sold <small>sets the price</small></span><select id=\"bt-f-item\"><option value=\"\" data-vt=\"clinic surgery grooming\">No item (no price shown)</option>");
            foreach (BktItem it in items ?? new List<BktItem>())
            {
                string vts = string.Join(" ", VisitTypes.Where(x => ItemTypesFor(x.Key).Contains(it.ItemType)).Select(x => x.Key));
                sb.Append("<option value=\"" + Ui.Attr(it.ItemId) + "\" data-vt=\"" + vts + "\"" + (it.ItemId == v.ItemId ? " selected" : string.Empty) + ">" + Ui.Enc(it.ItemName + " · " + M2(it.Price) + (it.Active ? string.Empty : " (inactive)")) + "</option>");
            }
            sb.Append("</select><em class=\"bt-err\" id=\"bt-e-item\"></em></label>");
            sb.Append("<label class=\"bt-field\"><span>Room type <small>a free room of this type is needed</small></span><select id=\"bt-f-room\"><option value=\"\">Any room</option>");
            foreach (BktRoom r in rooms ?? new List<BktRoom>())
            {
                sb.Append("<option value=\"" + Ui.Attr(r.RoomtypeId) + "\"" + (r.RoomtypeId == v.RoomtypeId ? " selected" : string.Empty) + ">" + Ui.Enc(r.TypeName + (r.Active ? string.Empty : " (inactive)")) + "</option>");
            }
            sb.Append("</select><em class=\"bt-err\" id=\"bt-e-room\"></em></label>");
            sb.Append("<div class=\"bt-span2 bt-fgrid bt-surg\" id=\"bt-surgw\"" + (v.VisitType == "surgery" ? string.Empty : " hidden") + ">");
            sb.Append("<h3 class=\"bt-span2 bt-sec2\">Drop-off day</h3><p class=\"bt-span2 bt-hint\">Pets are dropped off in the morning window, the vet operates later in the day, and the owner picks up at the pick-up time. All three times are needed. Pick-up must leave room for the surgery and at least an hour of recovery after drop-off ends.</p>");
            sb.Append("<div class=\"bt-span2 bt-trio\">");
            sb.Append(Field("dfrom", "Drop-off from", v.DropFrom, 5, "time"));
            sb.Append(Field("dto", "Drop-off until", v.DropTo, 5, "time"));
            sb.Append(Field("pick", "Pick-up from", v.PickupHm, 5, "time"));
            sb.Append("</div>");
            sb.Append(Area("preop", "Before surgery instructions", v.Preop, 1000, 4, "bt-span2", "one per line, sent to the owner", "No food after 10 pm the night before"));
            sb.Append("</div>");
            sb.Append("<h3 class=\"bt-span2 bt-sec2\">Pets and people</h3>");
            sb.Append("<div class=\"bt-field bt-span2\"><span>For which species <small>leave all unticked for every species</small></span><div class=\"bt-checks\">");
            foreach (BktSpecies s in sp)
            {
                sb.Append("<label class=\"bt-check\"><input type=\"checkbox\" name=\"bt-f-sp\" value=\"" + Ui.Attr(s.SpeciesKey) + "\"" + (mine.Contains(s.SpeciesKey) ? " checked" : string.Empty) + "><span>" + Ui.Enc(s.SpeciesName) + "</span></label>");
            }
            sb.Append("</div><em class=\"bt-err\" id=\"bt-e-sp\"></em></div>");
            sb.Append("<div class=\"bt-field bt-span2\"><span>Who can do it</span><div class=\"bt-who2\">");
            foreach (BktStaff p in staff)
            {
                sb.Append("<label class=\"bt-pick\"><input type=\"checkbox\" name=\"bt-f-staff\" value=\"" + Ui.Attr(p.StaffId) + "\"" + (who.Contains(p.StaffId) ? " checked" : string.Empty) + "><span><b>" + Ui.Enc(p.StaffName) + "</b><small>" + ProvName(p.ProviderType) + " &middot; " + Ui.Enc(p.Species == string.Empty ? "all species" : SpeciesText(p.Species, sp, true).ToLowerInvariant()) + "</small></span></label>");
            }
            sb.Append("</div><em class=\"bt-err\" id=\"bt-e-staff\"></em></div>");
            sb.Append("<h3 class=\"bt-span2 bt-sec2\">Website</h3>");
            sb.Append("<div class=\"bt-span2 bt-imgrow\"><label class=\"bt-field\"><span>Picture</span><select id=\"bt-f-img\" onchange=\"" + Me + ".img()\"><option value=\"\">No picture</option>");
            foreach (string img in images)
            {
                sb.Append("<option value=\"" + Ui.Attr(img) + "\"" + (img == v.Image ? " selected" : string.Empty) + ">" + Ui.Enc(ImageName(img)) + "</option>");
            }
            if (v.Image != string.Empty && !images.Contains(v.Image))
            {
                sb.Append("<option value=\"\" selected>Missing picture (" + Ui.Enc(v.Image) + ")</option>");
            }
            sb.Append("</select><em class=\"bt-err\" id=\"bt-e-img\"></em></label><div class=\"bt-imgp\" id=\"bt-imgp\">" + (images.Contains(v.Image) ? "<img src=\"" + Ui.Attr(v.Image) + "\" alt=\"\">" : "<span>No picture</span>") + "</div></div>");
            sb.Append("<label class=\"bt-check\"><input type=\"checkbox\" id=\"bt-f-online\"" + (v.Online ? " checked" : string.Empty) + "><span>Bookable on the website</span></label>");
            sb.Append("<label class=\"bt-check\"><input type=\"checkbox\" id=\"bt-f-newok\"" + (v.NewOk ? " checked" : string.Empty) + "><span>New clients can book it</span></label>");
            sb.Append(Field("order", "Sort order", v.BookOrder.ToString(Ui.Inv), 5, "text", string.Empty, "lower comes first", " inputmode=\"numeric\""));
            if (!add)
            {
                sb.Append("<label class=\"bt-check bt-selfend\"><input type=\"checkbox\" id=\"bt-f-active\"" + (v.Active ? " checked" : string.Empty) + "><span>Active <small class=\"bt-hint\">inactive types can't be booked</small></span></label>");
            }
            sb.Append("</div></fieldset><em class=\"bt-err\" id=\"bt-e-form\"></em><div class=\"bt-mfoot\">");
            if (!add && c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"bt-btn bt-btn-danger bt-mleft\" onclick=\"" + Me + ".ask('" + Ui.Js(v.BooktypeId) + "', this)\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"bt-btn bt-btn-ghost\" onclick=\"" + Me + ".closeModal()\">" + (can ? "Cancel" : "Close") + "</button>");
            if (can)
            {
                sb.Append("<button type=\"submit\" class=\"bt-btn bt-btn-accent\" id=\"bt-fsave\">" + (add ? "Add booking type" : "Save changes") + "</button>");
            }
            sb.Append("</div></form>");
            return sb.ToString();
        }

        private static string M2(decimal v)
        {
            return "$" + v.ToString("#,##0.00", Ui.Inv);
        }

        private static string Lines(string? text, int maxLine, int maxAll)
        {
            List<string> lines = (text ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(l => Ui.OneLine(l, maxLine)).Where(l => l != string.Empty).ToList();
            string all = string.Join("\n", lines);
            return all.Length > maxAll ? all.Substring(0, maxAll).TrimEnd() : all;
        }

        private static (bool Ok, int Value) Mins(string v, bool allowBlank)
        {
            if (v == string.Empty || (allowBlank && v == "0"))
            {
                return (allowBlank, 0);
            }
            return int.TryParse(v, NumberStyles.None, Ui.Inv, out int n) && n >= 5 && n <= 600 ? (true, n) : (false, 0);
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("bookid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, add ? "add booking type" : "edit booking type", "not allowed", false);
                Toast(response, add ? "You can't add booking types." : "You can't change booking types.");
                return response;
            }
            BktRow? old = null;
            if (add)
            {
                id = NewKey();
                if (id == string.Empty)
                {
                    Toast(response, "Please close this window and try again.");
                    return response;
                }
                BktRow? done = await Book(id);
                if (done != null)
                {
                    response.ExecuteScript(Me + ".saved('" + Ui.Js(done.BookName + " was added.") + "');");
                    return response;
                }
            }
            else
            {
                old = await Book(id);
                if (old == null)
                {
                    Toast(response, "This booking type was not found. It may have been deleted.");
                    return response;
                }
            }
            string name = Ui.OneLine(GetDataValue("bname"), 100);
            string team = Val("team", 30);
            string vt = Val("vt", 20);
            string shortText = Ui.OneLine(GetDataValue("short"), 200);
            string longText = Ui.Clip((GetDataValue("long") ?? string.Empty).Replace("\r", string.Empty), 2000);
            string expect = Lines(GetDataValue("expect"), 200, 1000);
            string item = CleanId(GetDataValue("item"));
            string room = CleanId(GetDataValue("room"));
            string dfrom = Val("dfrom", 5);
            string dto = Val("dto", 5);
            string pick = Val("pick", 5);
            string preop = Lines(GetDataValue("preop"), 300, 1000);
            string image = Val("img", 300);
            bool online = Val("online", 2) == "1";
            bool newOk = Val("newok", 2) == "1";
            bool active = add || Val("active", 2) == "1";
            string orderText = Val("order", 8);
            List<string> spPicked = (GetDataValue("species") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            List<string> staffPicked = (GetDataValue("staff") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            Dictionary<string, string> errs = new Dictionary<string, string> { { "name", "" }, { "team", "" }, { "vt", "" }, { "min", "" }, { "minm", "" }, { "minl", "" }, { "item", "" }, { "room", "" }, { "dfrom", "" }, { "dto", "" }, { "pick", "" }, { "sp", "" }, { "staff", "" }, { "img", "" }, { "order", "" }, { "form", "" } };
            List<BktTeam> teams = await Teams();
            List<BktSpecies> species = await SpeciesList();
            List<BktStaff> staff = await Providers();
            if (name == string.Empty)
            {
                errs["name"] = "Enter the name.";
            }
            else
            {
                var (dup, _) = await Helper.GetDataTable("SELECT 1 FROM BOOKTYPE WHERE LOWER(BOOK_NAME) = LOWER(@name) AND BOOKTYPE_ID <> @bid", new List<SqlParameter> { App.P("name", name, 100), App.P("bid", id, 50) });
                if (dup.Rows.Count > 0)
                {
                    errs["name"] = "Another booking type already has this name.";
                }
            }
            if (!teams.Any(t => t.TeamKey == team))
            {
                errs["team"] = "Pick a team.";
            }
            if (!VisitTypes.Any(x => x.Key == vt))
            {
                errs["vt"] = "Pick a visit type.";
                vt = "clinic";
            }
            (bool minOk, int min) = Mins(Val("min", 5), false);
            if (!minOk)
            {
                errs["min"] = "Enter 5 to 600 minutes.";
            }
            int minM = 0;
            int minL = 0;
            if (vt == "grooming")
            {
                (bool okM, minM) = Mins(Val("minm", 5), true);
                (bool okL, minL) = Mins(Val("minl", 5), true);
                if (!okM)
                {
                    errs["minm"] = "Enter 5 to 600 minutes, or leave it blank.";
                }
                if (!okL)
                {
                    errs["minl"] = "Enter 5 to 600 minutes, or leave it blank.";
                }
            }
            if (item != string.Empty)
            {
                var (it, _) = await Helper.GetOneT<BktItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, ITEM_TYPE AS ItemType, PRICE AS Price, ACTIVE AS Active FROM ITEM WHERE ITEM_ID = @iid", new List<SqlParameter> { App.P("iid", item, 50) });
                if (it == null)
                {
                    errs["item"] = "This item was not found.";
                }
                else if (!ItemTypesFor(vt).Contains(it.ItemType))
                {
                    errs["item"] = "This item doesn't fit a " + VtName(vt).ToLowerInvariant() + " booking. Pick another one.";
                }
                else if (!it.Active && (old == null || old.ItemId != item))
                {
                    errs["item"] = "This item is inactive. Pick an active one.";
                }
            }
            if (room != string.Empty)
            {
                var (rt, _) = await Helper.GetOneT<BktRoom>("SELECT ROOMTYPE_ID AS RoomtypeId, TYPE_NAME AS TypeName, PURPOSE AS Purpose, ACTIVE AS Active FROM ROOMTYPE WHERE ROOMTYPE_ID = @rt", new List<SqlParameter> { App.P("rt", room, 50) });
                if (rt == null || rt.Purpose == "boarding")
                {
                    errs["room"] = "Pick a room type from the list.";
                }
            }
            if (vt == "surgery")
            {
                TimeSpan? a = Ui.ParseHm(dfrom);
                TimeSpan? z = Ui.ParseHm(dto);
                TimeSpan? p = Ui.ParseHm(pick);
                if (a == null)
                {
                    errs["dfrom"] = dfrom == string.Empty ? "Enter when drop-off starts, like 07:30." : "Enter a time like 07:30.";
                }
                if (z == null)
                {
                    errs["dto"] = dto == string.Empty ? "Enter when drop-off ends, like 08:30." : "Enter a time like 08:30.";
                }
                if (p == null)
                {
                    errs["pick"] = pick == string.Empty ? "Enter when pick-up starts, like 15:00." : "Enter a time like 15:00.";
                }
                if (a != null && z != null && z <= a)
                {
                    errs["dto"] = "Must be later than the drop-off start.";
                }
                if (z != null && p != null && minOk && errs["dto"] == string.Empty)
                {
                    TimeSpan earliest = z.Value + TimeSpan.FromMinutes(min + 60);
                    if (p < earliest)
                    {
                        Dictionary<string, string> st = await App.Settings();
                        errs["pick"] = earliest >= TimeSpan.FromHours(24) ? "The surgery and an hour of recovery don't fit in the day after drop-off ends. Shorten the surgery or end drop-off earlier." : "Too early. The surgery starts after drop-off ends (" + Ui.Enc(Clock(dto, st)) + "), takes " + min + " minutes, and the pet needs at least 60 minutes to wake up, so pick-up can start at " + Ui.Enc(Clock(earliest.ToString("hh\\:mm", Ui.Inv), st)) + " at the earliest.";
                    }
                }
            }
            else
            {
                dfrom = string.Empty;
                dto = string.Empty;
                pick = string.Empty;
                preop = string.Empty;
            }
            if (spPicked.Any(k => !species.Any(x => x.SpeciesKey == k)))
            {
                errs["sp"] = "Pick species from the list.";
            }
            if (staffPicked.Any(k => !staff.Any(x => x.StaffId == k)))
            {
                errs["staff"] = "Pick people from the list.";
            }
            else if (active && staffPicked.Count == 0)
            {
                errs["staff"] = "Tick at least one person who can do it.";
            }
            if (image != string.Empty && !Images().Contains(image))
            {
                errs["img"] = "Pick a picture from the list.";
            }
            int order = 0;
            if (orderText != string.Empty && (!int.TryParse(orderText, NumberStyles.None, Ui.Inv, out order) || order > 99999))
            {
                errs["order"] = "Enter a whole number.";
            }
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("bt-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                response.ExecuteScript(Me + ".unlock();" + Me + ".bad();");
                return response;
            }
            string spCsv = string.Join(",", species.Where(x => spPicked.Contains(x.SpeciesKey)).Select(x => x.SpeciesKey));
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("bid", id, 50), App.P("name", name, 100), App.P("team", team, 30), App.P("vt", vt, 20), App.P("short", shortText, 200), App.P("long", longText, 2000),
                App.P("expect", expect, 1000), App.PInt("min", min), App.PInt("minm", minM), App.PInt("minl", minL), App.P("item", item, 50), App.P("room", room, 50),
                App.P("sp", spCsv, 100), App.P("dfrom", dfrom, 5), App.P("dto", dto, 5), App.P("pick", pick, 5), App.P("preop", preop, 1000), App.P("img", image, 300),
                App.PBit("online", online), App.PBit("newok", newOk), App.PInt("ord", order), App.PBit("active", active)
            };
            List<string> sql = new List<string>();
            if (add)
            {
                sql.Add("IF EXISTS (SELECT 1 FROM BOOKTYPE WHERE BOOKTYPE_ID = @bid) THROW 50003, N'DONE', 1;");
                sql.Add("INSERT INTO BOOKTYPE (BOOKTYPE_ID, BOOK_NAME, TEAM_KEY, VISIT_TYPE, SHORT_TEXT, LONG_TEXT, EXPECT, MINUTES, MINUTES_M, MINUTES_L, ITEM_ID, ROOMTYPE_ID, SPECIES, DROP_FROM, DROP_TO, PICKUP_HM, PREOP, IMAGE, ONLINE, NEW_OK, BOOK_ORDER, ACTIVE, SYSDTE) VALUES (@bid, @name, @team, @vt, @short, @long, @expect, @min, @minm, @minl, @item, @room, @sp, @dfrom, @dto, @pick, @preop, @img, @online, @newok, @ord, 1, GETDATE())");
            }
            else
            {
                sql.Add("UPDATE BOOKTYPE SET BOOK_NAME = @name, TEAM_KEY = @team, VISIT_TYPE = @vt, SHORT_TEXT = @short, LONG_TEXT = @long, EXPECT = @expect, MINUTES = @min, MINUTES_M = @minm, MINUTES_L = @minl, ITEM_ID = @item, ROOMTYPE_ID = @room, SPECIES = @sp, DROP_FROM = @dfrom, DROP_TO = @dto, PICKUP_HM = @pick, PREOP = @preop, IMAGE = @img, ONLINE = @online, NEW_OK = @newok, BOOK_ORDER = @ord, ACTIVE = @active, SYSDTE = GETDATE() WHERE BOOKTYPE_ID = @bid; IF @@ROWCOUNT = 0 THROW 50002, N'GONE', 1;");
            }
            sql.Add("DELETE FROM BOOKSTAFF WHERE BOOKTYPE_ID = @bid");
            for (int i = 0; i < staffPicked.Count; i++)
            {
                sql.Add("INSERT INTO BOOKSTAFF (BOOKTYPE_ID, STAFF_ID) VALUES (@bid, @s" + i + ")");
                ps.Add(App.P("s" + i, staffPicked[i], 50));
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                if (add && (result.Contains("DONE") || await Book(id) != null))
                {
                    response.ExecuteScript(Me + ".saved('" + Ui.Js(name + " was added.") + "');");
                    return response;
                }
                await App.Log(c.U.StaffId, add ? "add booking type" : "edit booking type", name + " failed", false);
                response.SetElementContents("bt-e-form", result.Contains("GONE") ? "This booking type was deleted by someone else." : "The booking type could not be saved. Try again.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            string target = name;
            if (add)
            {
                target += " · " + VtName(vt) + " · " + min + " min · " + Ui.Plural(staffPicked.Count, "person", "people");
            }
            else if (old != null)
            {
                List<string> ch = new List<string>();
                if (old.BookName != name)
                {
                    ch.Add("was " + old.BookName);
                }
                if (old.Minutes != min || old.MinutesM != minM || old.MinutesL != minL)
                {
                    ch.Add("minutes " + old.Minutes + (old.MinutesM > 0 || old.MinutesL > 0 ? "/" + old.MinutesM + "/" + old.MinutesL : string.Empty) + " → " + min + (minM > 0 || minL > 0 ? "/" + minM + "/" + minL : string.Empty));
                }
                if (old.ItemId != item)
                {
                    ch.Add("item changed");
                }
                if (old.Online != online)
                {
                    ch.Add(online ? "now online" : "now desk only");
                }
                if (old.Active != active)
                {
                    ch.Add(active ? "made active" : "made inactive");
                }
                if (old.StaffCount != staffPicked.Count)
                {
                    ch.Add(Ui.Plural(staffPicked.Count, "person", "people"));
                }
                target += ch.Count > 0 ? " · " + string.Join("; ", ch) : " · details";
            }
            await App.Log(c.U.StaffId, add ? "added booking type" : "edited booking type", target, true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(name + (add ? " was added." : " was saved.")) + "');");
            return response;
        }

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                Toast(response, "You can't delete booking types.");
                return response;
            }
            string id = CleanId(GetDataValue("bookid"));
            BktRow? b = id == string.Empty ? null : await Book(id);
            if (b == null)
            {
                Toast(response, "This booking type was not found. It may have been deleted.");
                return response;
            }
            int n = await Appts(id);
            string bid = Ui.Js(id);
            StringBuilder sb = new StringBuilder();
            if (n > 0)
            {
                sb.Append("<h2>" + Ui.Enc(b.BookName) + " can't be deleted</h2><p class=\"bt-mlead\">" + Ui.Plural(n, "appointment was", "appointments were") + " booked with it, and they must keep pointing to it.</p>");
                if (b.Active && c.Can(Key, 'E'))
                {
                    sb.Append("<p>Make it inactive instead. It can't be booked any more, but old appointments keep it.</p><div class=\"bt-mfoot\"><button type=\"button\" class=\"bt-btn bt-btn-ghost\" onclick=\"" + Me + ".open('" + bid + "')\">Back</button><button type=\"button\" class=\"bt-btn bt-btn-accent\" onclick=\"" + Me + ".retire('" + bid + "', this)\">Make inactive</button></div>");
                }
                else
                {
                    sb.Append("<div class=\"bt-mfoot\"><button type=\"button\" class=\"bt-btn bt-btn-ghost\" onclick=\"" + Me + ".open('" + bid + "')\">Back</button></div>");
                }
            }
            else
            {
                sb.Append("<h2>Delete " + Ui.Enc(b.BookName) + "?</h2><p class=\"bt-mlead\">Nothing was booked with it yet, so it can be removed for good. This can't be undone.</p>");
                sb.Append("<div class=\"bt-mfoot\"><button type=\"button\" class=\"bt-btn bt-btn-ghost\" onclick=\"" + Me + ".open('" + bid + "')\">Keep it</button><button type=\"button\" class=\"bt-btn bt-btn-danger\" onclick=\"" + Me + ".remove('" + bid + "', this)\">Delete for good</button></div>");
            }
            response.SetElementContents("bt-mbody", sb.ToString());
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(false);");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                await App.Log(c.U.StaffId, "delete booking type", "not allowed", false);
                Toast(response, "You can't delete booking types.");
                return response;
            }
            string id = CleanId(GetDataValue("bookid"));
            BktRow? b = id == string.Empty ? null : await Book(id);
            if (b == null)
            {
                Toast(response, "This booking type was not found. It may have been deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE BOOKTYPE_ID = @bid) THROW 50001, N'BOOKUSED', 1;",
                "DELETE FROM BOOKSTAFF WHERE BOOKTYPE_ID = @bid",
                "DELETE FROM BOOKTYPE WHERE BOOKTYPE_ID = @bid"
            }, new List<SqlParameter> { App.P("bid", id, 50) });
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "delete booking type", b.BookName + " failed", false);
                Toast(response, result.Contains("BOOKUSED") ? "It was just booked and can't be deleted. Make it inactive instead." : "The booking type could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "deleted booking type", b.BookName, true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(b.BookName + " was deleted.") + "');");
            return response;
        }

        public async Task<ApiResponse> Retire()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "make booking type inactive", "not allowed", false);
                Toast(response, "You can't change booking types.");
                return response;
            }
            string id = CleanId(GetDataValue("bookid"));
            BktRow? b = id == string.Empty ? null : await Book(id);
            if (b == null)
            {
                Toast(response, "This booking type was not found. It may have been deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "UPDATE BOOKTYPE SET ACTIVE = 0, SYSDTE = GETDATE() WHERE BOOKTYPE_ID = @bid" }, new List<SqlParameter> { App.P("bid", id, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, "The booking type could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "edited booking type", b.BookName + " · made inactive", true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(b.BookName + " is now inactive.") + "');");
            return response;
        }
    }
}
