using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminStaff : WebPage
    {
        private const string Key = "staff";
        private const string Me = "AdminStaffJs";
        private const string Letters = "VAEDX";
        private const string Cols = "S.STAFF_ID AS StaffId, S.LOGIN AS Login, S.STAFF_NAME AS StaffName, S.TITLE AS Title, S.ROLE_ID AS RoleId, ISNULL(R.ROLE_NAME, N'') AS RoleName, S.PROVIDER_TYPE AS ProviderType, S.CREDENTIALS AS Credentials, S.BIO AS Bio, S.EDUCATION AS Education, S.LANGUAGES AS Languages, S.PHOTO AS Photo, S.COLOR AS Color, S.EMAIL AS Email, S.PHONE AS Phone, S.HOME_ROOM_ID AS HomeRoomId, ISNULL(M.ROOM_NAME, N'') AS HomeRoomName, S.OWN_ONLY AS OwnOnly, S.SHOW_WEB AS ShowWeb, S.WEB_ORDER AS WebOrder, S.ACCEPTING AS Accepting, S.IS_DEMO AS IsDemo, S.ACTIVE AS Active, S.MUST_CHANGE AS MustChange, S.LAST_LOGIN AS LastLogin, (SELECT COUNT(*) FROM STAFFSESSION X WHERE X.STAFF_ID = S.STAFF_ID AND X.EXPIRES_UTC > SYSUTCDATETIME()) AS Sessions, CAST(CASE WHEN S.ROLE_ID = N'manager' OR EXISTS (SELECT 1 FROM STAFFPERM Q WHERE Q.STAFF_ID = S.STAFF_ID AND Q.MENU_KEY = N'staff' AND Q.RIGHTS <> N'') THEN 1 ELSE 0 END AS BIT) AS HasStaff";
        private const string From = " FROM STAFF S LEFT JOIN ROLE R ON R.ROLE_ID = S.ROLE_ID LEFT JOIN ROOM M ON M.ROOM_ID = S.HOME_ROOM_ID";
        private const string LastManager = "IF NOT EXISTS (SELECT 1 FROM STAFF WHERE ROLE_ID = N'manager' AND ACTIVE = 1 AND (@demo = 1 OR IS_DEMO = 0)) THROW 50003, N'LASTMANAGER', 1;";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Staff and permissions | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            string tab = (QueryValue("t") ?? string.Empty).Trim();
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--sf-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("sf"))
                .Replace("{plhd_qs}", Ui.Attr(CleanId(QueryValue("s"))))
                .Replace("{plhd_qt}", tab == "roles" ? "roles" : string.Empty);
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

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key), max);
        }

        private bool Flag(string key)
        {
            return (GetDataValue(key) ?? string.Empty).Trim() == "1";
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
            return "<div class=\"sf-panel sf-deny\"><h1>Staff and permissions</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"sf-btn sf-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static string Missing()
        {
            return "<div class=\"sf-panel sf-deny\"><h1>Staff member not found</h1><p>This person is not in the staff list any more.</p><a class=\"sf-btn sf-btn-ghost\" href=\"AdminStaff\">All staff</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static string TypeName(string t)
        {
            return t switch
            {
                "vet" => "Vet",
                "tech" => "Technician",
                "groomer" => "Groomer",
                _ => "None"
            };
        }

        private static string RightWord(char ch, string extra)
        {
            return ch switch
            {
                'V' => "View",
                'A' => "Add",
                'E' => "Edit",
                'D' => "Delete",
                'X' => extra != string.Empty ? extra : "Extra",
                _ => string.Empty
            };
        }

        private static async Task<SfStaff?> Person(string id)
        {
            var (row, emsg) = await Helper.GetOneT<SfStaff>("SELECT " + Cols + From + " WHERE S.STAFF_ID = @sid", new List<SqlParameter> { App.P("sid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<SfStaff>> People()
        {
            var (rows, emsg) = await Helper.GetListT<SfStaff>("SELECT " + Cols + From + " ORDER BY S.ACTIVE DESC, ISNULL(R.ROLE_ORDER, 99), S.STAFF_NAME");
            return emsg == string.Empty && rows != null ? rows : new List<SfStaff>();
        }

        private static async Task<List<SfRole>> Roles()
        {
            var (rows, emsg) = await Helper.GetListT<SfRole>("SELECT R.ROLE_ID AS RoleId, R.ROLE_NAME AS RoleName, R.ROLE_ORDER AS RoleOrder, (SELECT COUNT(*) FROM STAFF S WHERE S.ROLE_ID = R.ROLE_ID) AS Used FROM ROLE R ORDER BY R.ROLE_ORDER, R.ROLE_NAME");
            return emsg == string.Empty && rows != null ? rows : new List<SfRole>();
        }

        private static async Task<Dictionary<string, string>> Rights(string table, string col, string id, List<MenuRow> menus)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            var (rows, emsg) = await Helper.GetListT<PermRow>("SELECT MENU_KEY AS MenuKey, RIGHTS AS Rights FROM " + table + " WHERE " + col + " = @id", new List<SqlParameter> { App.P("id", id, 50) });
            foreach (PermRow p in emsg == string.Empty && rows != null ? rows : new List<PermRow>())
            {
                MenuRow? m = menus.FirstOrDefault(x => x.MenuKey == p.MenuKey);
                string r = m == null ? string.Empty : Auth.CleanRights(m.Rights, p.Rights);
                if (r != string.Empty)
                {
                    d[p.MenuKey] = r;
                }
            }
            return d;
        }

        private static Task<Dictionary<string, string>> StaffRights(string staffId, List<MenuRow> menus)
        {
            return Rights("STAFFPERM", "STAFF_ID", staffId, menus);
        }

        private static Task<Dictionary<string, string>> RoleRights(string roleId, List<MenuRow> menus)
        {
            return Rights("ROLEPERM", "ROLE_ID", roleId, menus);
        }

        private static string R(Dictionary<string, string> d, string k)
        {
            return d.TryGetValue(k, out string? v) ? v : string.Empty;
        }

        private static Dictionary<string, string> ParsePerms(string? raw, List<MenuRow> menus)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (string part in (raw ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2)
                {
                    continue;
                }
                MenuRow? m = menus.FirstOrDefault(x => x.MenuKey == kv[0].Trim());
                if (m == null)
                {
                    continue;
                }
                string r = Auth.CleanRights(m.Rights, kv[1]);
                if (r != string.Empty)
                {
                    d[m.MenuKey] = r;
                }
            }
            return d;
        }

        private static string PermText(Dictionary<string, string> d)
        {
            return string.Join(";", d.Select(x => x.Key + ":" + x.Value));
        }

        private static string Diff(Dictionary<string, string> before, Dictionary<string, string> after, List<MenuRow> menus)
        {
            List<string> ch = new List<string>();
            foreach (MenuRow m in menus)
            {
                string a = R(before, m.MenuKey);
                string b = R(after, m.MenuKey);
                if (a != b)
                {
                    ch.Add(m.MenuKey + " " + (a == string.Empty ? "none" : a) + "→" + (b == string.Empty ? "none" : b));
                }
            }
            return ch.Count == 0 ? "no change" : string.Join(", ", ch);
        }

        private static List<string> Photos()
        {
            List<string> list = new List<string>();
            string dir = Path.Combine(App.Root, "images", "hospital", "people");
            if (!Directory.Exists(dir))
            {
                return list;
            }
            foreach (string f in Directory.GetFiles(dir, "*.webp").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                string rel = "images/hospital/people/" + Path.GetFileName(f);
                if (Ui.SafePath(rel))
                {
                    list.Add(rel);
                }
            }
            return list;
        }

        private static string Matrix(string id, List<MenuRow> menus, Dictionary<string, string> rights, bool all, bool editable, HashSet<string> fixedKeys, Dictionary<string, string>? limit)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sf-perm\" id=\"" + id + "\">");
            foreach (IGrouping<string, MenuRow> g in menus.GroupBy(m => m.SectionKey))
            {
                List<MenuRow> items = g.OrderBy(m => m.MenuOrder).ToList();
                int on = items.Count(m => (all ? Auth.CleanRights(m.Rights, m.Rights) : R(rights, m.MenuKey)) != string.Empty);
                sb.Append("<details class=\"sf-pgrp\" data-sec=\"" + Ui.Attr(g.Key) + "\"><summary class=\"sf-psum\"><i class=\"sf-pchev\" aria-hidden=\"true\"></i><span class=\"sf-h3\">" + Ui.Enc(items[0].SectionName) + "</span><span class=\"sf-pcnt\">" + on + " of " + items.Count + "</span></summary><div class=\"sf-pitems\">");
                foreach (MenuRow m in items)
                {
                    string have = all ? Auth.CleanRights(m.Rights, m.Rights) : R(rights, m.MenuKey);
                    bool locked = !editable || fixedKeys.Contains(m.MenuKey);
                    sb.Append("<div class=\"sf-prow" + (have != string.Empty ? " sf-pyes" : string.Empty) + "\" data-k=\"" + Ui.Attr(m.MenuKey) + "\"><div class=\"sf-pname\"><b>" + Ui.Enc(m.MenuName) + "</b>" + (m.Hint != string.Empty ? "<small>" + Ui.Enc(m.Hint) + "</small>" : string.Empty) + (locked && editable ? "<small class=\"sf-plock\">Only a manager can change this one.</small>" : string.Empty) + "</div><div class=\"sf-pls\">");
                    foreach (char ch in Letters.Where(x => m.Rights.Contains(x)))
                    {
                        bool off = locked || (limit != null && !R(limit, m.MenuKey).Contains(ch));
                        sb.Append("<label class=\"sf-pl\"" + (off && !locked ? " title=\"You don't have this right yourself, so you can't give it.\"" : string.Empty) + "><input type=\"checkbox\" data-l=\"" + ch + "\"" + (have.Contains(ch) ? " checked" : string.Empty) + (off ? " disabled" : " onchange=\"" + Me + ".tick(this)\"") + "><span>" + Ui.Enc(RightWord(ch, m.ExtraName)) + "</span></label>");
                    }
                    sb.Append("</div></div>");
                }
                sb.Append("</div></details>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("sf-menu", Auth.Menu("sf", Me, "AdminStaff", c));
            response.SetElementContents("sf-who", Auth.Who("sf", c));
            response.ExecuteScript(Me + ".secs();");
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
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("sf-main", Denied());
                return response;
            }
            string sid = CleanId(GetDataValue("openstaff"));
            if (sid != string.Empty)
            {
                await PersonView(response, c, sid, string.Empty);
                return response;
            }
            if ((GetDataValue("opentab") ?? string.Empty).Trim() == "roles")
            {
                await RolesView(response, c, string.Empty, string.Empty);
                return response;
            }
            await ListView(response, c, string.Empty);
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
                response.SetElementContents("sf-main", Denied());
                return response;
            }
            await ListView(response, c, string.Empty);
            return response;
        }

        private static string Tabs(string on)
        {
            return "<nav class=\"sf-tabs\" aria-label=\"Staff sections\"><button type=\"button\" class=\"sf-tab" + (on == "staff" ? " sf-tab-on" : string.Empty) + "\" onclick=\"" + Me + ".list()\">Staff</button><button type=\"button\" class=\"sf-tab" + (on == "roles" ? " sf-tab-on" : string.Empty) + "\" onclick=\"" + Me + ".roles('')\">Roles and default rights</button></nav>";
        }

        private static async Task ListView(ApiResponse response, Ctx c, string toast)
        {
            List<SfStaff> people = await People();
            List<SfRole> roles = await Roles();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sf-ptop\"><div><h1>Staff and permissions</h1><p>Everyone who can sign in to the admin, what they do, and which areas they can open.</p></div><div class=\"sf-acts\">");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".form('', this)\">Add staff</button>");
            }
            sb.Append("</div></div>");
            sb.Append(Tabs("staff"));
            sb.Append("<div class=\"sf-bar\"><label class=\"sf-field sf-grow2\"><span>Search</span><input type=\"search\" id=\"sf-term\" maxlength=\"60\" placeholder=\"Name, user name or title\" autocomplete=\"off\" oninput=\"" + Me + ".filter()\"></label>");
            sb.Append("<label class=\"sf-field\"><span>Role</span><select id=\"sf-frole\" onchange=\"" + Me + ".filter()\"><option value=\"\">All roles</option>" + Ui.Opts(roles.Select(r => (r.RoleId, r.RoleName)), string.Empty) + "</select></label>");
            sb.Append("<label class=\"sf-field\"><span>Show</span><select id=\"sf-fact\" onchange=\"" + Me + ".filter()\"><option value=\"1\">Active</option><option value=\"0\">Inactive</option><option value=\"\">Everyone</option></select></label></div>");
            sb.Append("<div class=\"sf-tw\"><table class=\"sf-table sf-stable\"><thead><tr><th>Name</th><th>User name</th><th>Role</th><th>Provider</th><th>Status</th><th>Last sign-in</th><th>Website</th></tr></thead><tbody id=\"sf-rows\">");
            foreach (SfStaff p in people)
            {
                string go = Me + ".person('" + Ui.Js(p.StaffId) + "')";
                string search = (p.StaffName + " " + p.Login + " " + p.Title + " " + p.RoleName).ToLowerInvariant();
                sb.Append("<tr class=\"sf-row" + (p.Active ? string.Empty : " sf-off") + "\" tabindex=\"0\" data-role=\"" + Ui.Attr(p.RoleId) + "\" data-act=\"" + (p.Active ? "1" : "0") + "\" data-search=\"" + Ui.Attr(search) + "\" onclick=\"" + go + "\" onkeydown=\"if(event.key==='Enter')" + go + "\">");
                sb.Append("<td class=\"sf-cname\"><div class=\"sf-pn\">" + Ui.Avatar("sf", p.StaffId, p.StaffName, p.Photo) + "<div><b>" + Ui.Enc(p.StaffName) + "</b><small>" + Ui.Enc(p.Title != string.Empty ? p.Title : p.RoleName) + "</small></div></div></td>");
                sb.Append("<td data-l=\"User name\"><span class=\"sf-mono\">" + Ui.Enc(p.Login) + "</span></td>");
                sb.Append("<td data-l=\"Role\">" + Ui.Enc(p.RoleName != string.Empty ? p.RoleName : "No role") + "</td>");
                sb.Append("<td data-l=\"Provider\">" + (p.ProviderType != string.Empty ? "<span class=\"sf-dot\" style=\"--c:" + Ui.Attr(Ui.SafeColor(p.Color) ? p.Color : "#3F7D6B") + "\"></span>" + Ui.Enc(TypeName(p.ProviderType)) : "<span class=\"sf-dim\">-</span>") + "</td>");
                sb.Append("<td data-l=\"Status\">" + (p.Active ? "<span class=\"sf-st sf-st-on\">Active</span>" : "<span class=\"sf-st sf-st-off\">Inactive</span>") + (p.MustChange && !App.Demo ? "<span class=\"sf-st sf-st-noshow sf-st2\">New password at next sign-in</span>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Last sign-in\" class=\"sf-nw\">" + (p.LastLogin.HasValue ? Ui.Time(p.LastLogin.Value, "ago") : "<span class=\"sf-dim\">Never</span>") + "</td>");
                sb.Append("<td data-l=\"Website\">" + (p.ShowWeb ? "Shown" + (p.ProviderType != string.Empty && !p.Accepting ? "<small class=\"sf-blk sf-dim\">Not taking new clients</small>" : string.Empty) : "<span class=\"sf-dim\">Hidden</span>") + "</td></tr>");
            }
            sb.Append("</tbody></table><p class=\"sf-empty\" id=\"sf-none\" hidden>Nobody matches. Try a different search, role or status.</p></div>");
            sb.Append("<p class=\"sf-foot\" id=\"sf-count\"></p>");
            if (App.Demo)
            {
                sb.Append("<p class=\"sf-info\">Demo: sample accounts keep the password <b>demo</b> and their user name, so every visitor can sign in. Everything else can be changed and resets each day.</p>");
            }
            response.SetElementContents("sf-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('list', '');");
            Toast(response, toast);
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("sf-main", Denied());
                return response;
            }
            await PersonView(response, c, CleanId(GetDataValue("staffid")), string.Empty);
            return response;
        }

        private static List<string> Beyond(Ctx c, Dictionary<string, string> d)
        {
            if (c.Manager)
            {
                return new List<string>();
            }
            return c.Menus.Where(m => R(d, m.MenuKey).Any(ch => !R(c.Rights, m.MenuKey).Contains(ch))).Select(m => m.MenuName).ToList();
        }

        private static string Areas(List<string> names)
        {
            return string.Join(", ", names.Take(3)) + (names.Count > 3 ? " and " + Ui.Plural(names.Count - 3, "more", "more") : string.Empty);
        }

        private static string Grantable(Ctx c, Dictionary<string, string> d)
        {
            if (c.Manager)
            {
                return string.Empty;
            }
            if (R(d, Key) != string.Empty)
            {
                return "include Staff and permissions, and only a manager can give that.";
            }
            List<string> extra = Beyond(c, d);
            return extra.Count > 0 ? "include rights you don't have yourself (" + Areas(extra) + ")." : string.Empty;
        }

        private static async Task<string> Why(Ctx c, SfStaff p)
        {
            if (c.Manager)
            {
                return string.Empty;
            }
            if (p.RoleId == "manager")
            {
                return "Only a manager can change a manager.";
            }
            if (p.StaffId == c.U.StaffId)
            {
                return "Only a manager can change your own staff record. Use My account for your password.";
            }
            if (p.HasStaff)
            {
                return "They can manage staff, so only a manager can change their account.";
            }
            if (c.U.OwnOnly && !p.OwnOnly)
            {
                return "They see everyone's records and you see only your own, so only a manager can change their account.";
            }
            List<string> extra = Beyond(c, await StaffRights(p.StaffId, c.Menus));
            if (extra.Count > 0)
            {
                return "They have rights you don't have (" + Areas(extra) + "), so only a manager can change their account.";
            }
            return string.Empty;
        }

        private static async Task PersonView(ApiResponse response, Ctx c, string id, string toast)
        {
            SfStaff? p = id == string.Empty ? null : await Person(id);
            if (p == null)
            {
                response.SetElementContents("sf-main", Missing());
                response.ExecuteScript(Me + ".ready('missing', '');");
                return;
            }
            List<MenuRow> menus = c.Menus;
            Dictionary<string, string> rights = await StaffRights(p.StaffId, menus);
            var (spec, sperr) = await Helper.GetListT<SfPick>("SELECT X.SPECIES_KEY AS K, ISNULL(P.SPECIES_NAME, X.SPECIES_KEY) AS T FROM STAFFSPECIES X LEFT JOIN SPECIES P ON P.SPECIES_KEY = X.SPECIES_KEY WHERE X.STAFF_ID = @sid ORDER BY ISNULL(P.SPECIES_ORDER, 99)", new List<SqlParameter> { App.P("sid", p.StaffId, 50) });
            List<SfPick> species = sperr == string.Empty && spec != null ? spec : new List<SfPick>();
            bool self = p.StaffId == c.U.StaffId;
            string block = await Why(c, p);
            bool canEdit = c.Can(Key, 'E') && block == string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sf-ptop sf-phead\"><div class=\"sf-pn sf-pbig\">" + Ui.Avatar("sf", p.StaffId, p.StaffName, p.Photo, "lg") + "<div><h1>" + Ui.Enc(p.StaffName) + "</h1><p>" + Ui.Enc((p.Title != string.Empty ? p.Title + " · " : string.Empty) + (p.RoleName != string.Empty ? p.RoleName : "No role")) + " · <span class=\"sf-mono\">" + Ui.Enc(p.Login) + "</span></p></div></div><div class=\"sf-acts\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".list()\">All staff</button>");
            if (canEdit)
            {
                sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".form('" + Ui.Js(p.StaffId) + "', this)\">Edit details</button>");
            }
            if (c.Can("activity", 'V'))
            {
                sb.Append("<a class=\"sf-btn sf-btn-ghost\" href=\"AdminActivity?s=" + Uri.EscapeDataString(p.StaffId) + "\">Their activity</a>");
            }
            sb.Append("</div></div>");
            if (!p.Active)
            {
                sb.Append("<div class=\"sf-warn sf-wtop\"><b>Inactive.</b> " + Ui.Enc(p.StaffName) + " can't sign in and is left out of bookings and schedules.</div>");
            }
            if (block != string.Empty && c.Can(Key, 'E'))
            {
                sb.Append("<p class=\"sf-info sf-wtop\">" + Ui.Enc(block) + "</p>");
            }
            sb.Append("<div class=\"sf-dgrid\"><section class=\"sf-panel\"><h2 class=\"sf-h2\">Details</h2><dl class=\"sf-dl\">");
            sb.Append("<dt>Role</dt><dd>" + Ui.Enc(p.RoleName != string.Empty ? p.RoleName : "No role") + "</dd>");
            sb.Append("<dt>Provider</dt><dd>" + (p.ProviderType != string.Empty ? "<span class=\"sf-dot\" style=\"--c:" + Ui.Attr(Ui.SafeColor(p.Color) ? p.Color : "#3F7D6B") + "\"></span>" + Ui.Enc(TypeName(p.ProviderType)) + (p.Credentials != string.Empty ? ", " + Ui.Enc(p.Credentials) : string.Empty) : "Not a provider (office or other staff)") + "</dd>");
            if (p.ProviderType != string.Empty)
            {
                sb.Append("<dt>Sees</dt><dd>" + (species.Count > 0 ? Ui.Enc(string.Join(", ", species.Select(x => x.T))) : "<span class=\"sf-dim\">Any species</span>") + "</dd>");
                sb.Append("<dt>Home room</dt><dd>" + (p.HomeRoomName != string.Empty ? Ui.Enc(p.HomeRoomName) : "<span class=\"sf-dim\">None</span>") + "</dd>");
            }
            sb.Append("<dt>Records</dt><dd>" + (p.OwnOnly ? "Only their own appointments and visits" : "Everyone's") + "</dd>");
            sb.Append("<dt>Email</dt><dd class=\"sf-mail\">" + (p.Email != string.Empty ? Ui.Enc(p.Email) : "<span class=\"sf-dim\">-</span>") + "</dd>");
            sb.Append("<dt>Phone</dt><dd>" + (p.Phone != string.Empty ? Ui.Enc(p.Phone) : "<span class=\"sf-dim\">-</span>") + "</dd>");
            sb.Append("<dt>Website</dt><dd>" + (p.ShowWeb ? "Shown on the team page (order " + p.WebOrder + ")" + (p.ProviderType != string.Empty ? (p.Accepting ? " · taking new clients" : " · not taking new clients") : string.Empty) : "Not shown") + "</dd>");
            sb.Append("</dl>");
            if (p.Bio != string.Empty || p.Education != string.Empty || p.Languages != string.Empty)
            {
                sb.Append("<div class=\"sf-bio\">");
                if (p.Bio != string.Empty)
                {
                    sb.Append("<p>" + Ui.Enc(p.Bio) + "</p>");
                }
                if (p.Education != string.Empty)
                {
                    sb.Append("<ul class=\"sf-edu\">" + string.Concat(p.Education.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty).Select(x => "<li>" + Ui.Enc(x) + "</li>")) + "</ul>");
                }
                if (p.Languages != string.Empty)
                {
                    sb.Append("<p class=\"sf-sub\">Speaks " + Ui.Enc(p.Languages) + "</p>");
                }
                sb.Append("</div>");
            }
            sb.Append("</section><section class=\"sf-panel\"><h2 class=\"sf-h2\">Sign-in</h2><dl class=\"sf-dl\">");
            sb.Append("<dt>User name</dt><dd><span class=\"sf-mono\">" + Ui.Enc(p.Login) + "</span></dd>");
            sb.Append("<dt>Status</dt><dd>" + (p.Active ? "<span class=\"sf-st sf-st-on\">Active</span>" : "<span class=\"sf-st sf-st-off\">Inactive</span>") + (p.MustChange && !App.Demo ? " <span class=\"sf-st sf-st-noshow\">Chooses a new password at next sign-in</span>" : string.Empty) + "</dd>");
            sb.Append("<dt>Last sign-in</dt><dd>" + (p.LastLogin.HasValue ? Ui.Time(p.LastLogin.Value, "full") : "<span class=\"sf-dim\">Never</span>") + "</dd>");
            sb.Append("<dt>Signed in now</dt><dd>" + (p.Sessions > 0 ? Ui.Plural(p.Sessions, "device or browser", "devices or browsers") : "<span class=\"sf-dim\">No</span>") + "</dd>");
            sb.Append("</dl>");
            if (App.Demo && p.IsDemo)
            {
                sb.Append("<p class=\"sf-info\">This is a sample account: it keeps the password <b>demo</b> and its user name so every visitor can sign in.</p>");
            }
            if (canEdit)
            {
                sb.Append("<div class=\"sf-acts sf-acts-in\">");
                if (!self)
                {
                    sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-ghost\" onclick=\"" + Me + ".passForm()\">Set a new password</button>");
                    if (p.Sessions > 0)
                    {
                        sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-ghost\" onclick=\"" + Me + ".kick(this)\">Sign out everywhere</button>");
                    }
                    sb.Append(p.Active ? "<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-danger\" onclick=\"" + Me + ".retire(0)\">Make inactive</button>" : "<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-ghost\" onclick=\"" + Me + ".retire(1)\">Make active again</button>");
                }
                else
                {
                    sb.Append("<a class=\"sf-btn sf-btn-sm sf-btn-ghost\" href=\"AdminAccount\">Change my password</a>");
                }
                sb.Append("</div>");
            }
            if (c.Can(Key, 'D') && !self && block == string.Empty)
            {
                sb.Append("<p class=\"sf-sub sf-delp\">Added by mistake? <button type=\"button\" class=\"sf-link\" onclick=\"" + Me + ".askRemove()\">Delete " + Ui.Enc(p.StaffName) + "</button> (only if they never did anything).</p>");
            }
            sb.Append("</section></div>");
            sb.Append("<section class=\"sf-panel sf-ppanel\"><div class=\"sf-shead\"><h2 class=\"sf-h2\">Permissions</h2><span class=\"sf-sub\">" + (p.RoleId == "manager" ? "Managers can open every area and do everything." : "Tick what " + Ui.Enc(FirstName(p.StaffName)) + " can do in each area. View comes with any other tick.") + "</span></div>");
            bool permEdit = canEdit && p.RoleId != "manager" && !self;
            HashSet<string> fixedKeys = new HashSet<string>();
            if (!c.Manager)
            {
                fixedKeys.Add("staff");
            }
            if (p.RoleId != "manager" && c.Can(Key, 'E') && self && c.Manager)
            {
                sb.Append("<p class=\"sf-info\">You can't change your own permissions. Ask another manager.</p>");
            }
            else if (permEdit && !c.Manager)
            {
                sb.Append("<p class=\"sf-info\">You can only give rights you have yourself. The others are greyed out.</p>");
            }
            sb.Append(Matrix("sf-pm-person", menus, rights, p.RoleId == "manager", permEdit, fixedKeys, c.Manager ? null : c.Rights));
            if (permEdit)
            {
                sb.Append("<div class=\"sf-pfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".defaults(this)\">Reset to " + Ui.Enc(p.RoleName != string.Empty ? p.RoleName : "role") + " defaults</button><button type=\"button\" class=\"sf-btn sf-btn-accent\" id=\"sf-psave\" onclick=\"" + Me + ".savePerms(this)\">Save permissions</button></div><em class=\"sf-err\" id=\"sf-e-perm\"></em>");
            }
            sb.Append("</section>");
            response.SetElementContents("sf-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('person', '" + Ui.Js(p.StaffId) + "');");
            Toast(response, toast);
        }

        private static string FirstName(string name)
        {
            string[] w = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0)
            {
                return name;
            }
            return w[0] == "Dr." && w.Length > 1 ? "Dr. " + w[w.Length - 1] : w[0];
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("staffid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                Toast(response, add ? "You can't add staff." : "You can't change staff details.");
                return response;
            }
            SfStaff p = new SfStaff { Active = true, Accepting = true, Color = "#3F7D6B", RoleId = "office" };
            List<string> spec = new List<string>();
            if (!add)
            {
                SfStaff? old = await Person(id);
                if (old == null)
                {
                    Toast(response, "This person was not found.");
                    return response;
                }
                string block = await Why(c, old);
                if (block != string.Empty)
                {
                    Toast(response, block);
                    return response;
                }
                p = old;
                var (sp, sperr) = await Helper.GetListT<SfPick>("SELECT SPECIES_KEY AS K, SPECIES_KEY AS T FROM STAFFSPECIES WHERE STAFF_ID = @sid", new List<SqlParameter> { App.P("sid", id, 50) });
                spec = sperr == string.Empty && sp != null ? sp.Select(x => x.K).ToList() : spec;
            }
            List<SfRole> roles = new List<SfRole>();
            foreach (SfRole r in await Roles())
            {
                if (c.Manager || r.RoleId == p.RoleId || (r.RoleId != "manager" && Grantable(c, await RoleRights(r.RoleId, c.Menus)) == string.Empty))
                {
                    roles.Add(r);
                }
            }
            var (rooms, rerr) = await Helper.GetListT<SfPick>("SELECT R.ROOM_ID AS K, R.ROOM_NAME AS T FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID WHERE T.PURPOSE <> N'boarding' AND (R.ACTIVE = 1 OR R.ROOM_ID = @cur) ORDER BY T.TYPE_ORDER, R.ROOM_ORDER", new List<SqlParameter> { App.P("cur", p.HomeRoomId, 50) });
            var (sps, serr) = await Helper.GetListT<SfPick>("SELECT SPECIES_KEY AS K, SPECIES_NAME AS T FROM SPECIES WHERE ACTIVE = 1 OR SPECIES_KEY IN (SELECT SPECIES_KEY FROM STAFFSPECIES WHERE STAFF_ID = @sid) ORDER BY SPECIES_ORDER", new List<SqlParameter> { App.P("sid", id, 50) });
            bool self = !add && id == c.U.StaffId;
            bool lockLogin = !add && App.Demo && p.IsDemo;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "Add staff" : "Edit " + Ui.Enc(p.StaffName)) + "</h2>");
            sb.Append("<form class=\"sf-form\" onsubmit=\"return " + Me + ".save()\" novalidate><input type=\"hidden\" id=\"sf-f-id\" value=\"" + Ui.Attr(add ? string.Empty : p.StaffId) + "\">");
            sb.Append("<h3>Who</h3><div class=\"sf-fgrid\">");
            sb.Append("<label class=\"sf-field\"><span>Full name</span><input type=\"text\" id=\"sf-f-name\" maxlength=\"100\" value=\"" + Ui.Attr(p.StaffName) + "\" autocomplete=\"off\"><em class=\"sf-err\" id=\"sf-e-sname\"></em></label>");
            sb.Append("<label class=\"sf-field\"><span>Job title <small>(shown under their name)</small></span><input type=\"text\" id=\"sf-f-title\" maxlength=\"100\" value=\"" + Ui.Attr(p.Title) + "\" autocomplete=\"off\"><em class=\"sf-err\" id=\"sf-e-title\"></em></label>");
            sb.Append("<label class=\"sf-field\"><span>User name <small>(for signing in)</small></span><input type=\"text\" id=\"sf-f-login\" maxlength=\"40\" value=\"" + Ui.Attr(p.Login) + "\" autocomplete=\"off\" autocapitalize=\"none\" spellcheck=\"false\"" + (lockLogin ? " disabled" : string.Empty) + "><em class=\"sf-err\" id=\"sf-e-login\"></em><small class=\"sf-hint\">" + (lockLogin ? "Sample accounts keep their user name in the demo." : "3 to 40 lowercase letters, numbers, dots, dashes or underscores.") + "</small></label>");
            sb.Append("<label class=\"sf-field\"><span>Role</span><select id=\"sf-f-role\"" + (self ? " disabled" : string.Empty) + ">" + Ui.Opts(roles.Select(r => (r.RoleId, r.RoleName)), p.RoleId) + "</select><em class=\"sf-err\" id=\"sf-e-role\"></em><small class=\"sf-hint\">" + (self ? "You can't change your own role." : add ? "They start with this role's default permissions." : "Changing the role also resets their permissions to that role's defaults.") + "</small></label>");
            sb.Append("<label class=\"sf-field\"><span>Email</span><input type=\"email\" id=\"sf-f-email\" maxlength=\"120\" value=\"" + Ui.Attr(p.Email) + "\" autocomplete=\"off\"><em class=\"sf-err\" id=\"sf-e-email\"></em></label>");
            sb.Append("<label class=\"sf-field\"><span>Phone</span><input type=\"tel\" id=\"sf-f-phone\" maxlength=\"30\" value=\"" + Ui.Attr(p.Phone) + "\" autocomplete=\"off\"><em class=\"sf-err\" id=\"sf-e-phone\"></em></label>");
            sb.Append("</div>");
            if (add)
            {
                sb.Append("<h3>Password</h3><div class=\"sf-fgrid\"><label class=\"sf-field\"><span>Password</span><input type=\"password\" id=\"sf-f-pass\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"sf-err\" id=\"sf-e-pass\"></em></label><label class=\"sf-field\"><span>Password again</span><input type=\"password\" id=\"sf-f-again\" maxlength=\"100\" autocomplete=\"new-password\"></label></div><p class=\"sf-hint\">At least 8 characters. Give it to them in person." + (App.Demo ? " They can change it in My account." : " They must choose their own password the first time they sign in.") + "</p>");
            }
            sb.Append("<h3>Work</h3><div class=\"sf-fgrid\">");
            sb.Append("<label class=\"sf-field\"><span>Provider type</span><select id=\"sf-f-ptype\" onchange=\"" + Me + ".ptype()\">" + Ui.Opts(new[] { ("", "None (office or other staff)"), ("vet", "Vet"), ("tech", "Technician"), ("groomer", "Groomer") }, p.ProviderType) + "</select><em class=\"sf-err\" id=\"sf-e-ptype\"></em><small class=\"sf-hint\">Providers can be booked and get a column on the schedule.</small></label>");
            sb.Append("<label class=\"sf-field sf-prov\"><span>Credentials <small>(for example DVM)</small></span><input type=\"text\" id=\"sf-f-cred\" maxlength=\"60\" value=\"" + Ui.Attr(p.Credentials) + "\" autocomplete=\"off\"></label>");
            sb.Append("<label class=\"sf-field sf-prov\"><span>Home room</span><select id=\"sf-f-room\"><option value=\"\">No home room</option>" + Ui.Opts((rerr == string.Empty && rooms != null ? rooms : new List<SfPick>()).Select(r => (r.K, r.T)), p.HomeRoomId) + "</select><em class=\"sf-err\" id=\"sf-e-room\"></em><small class=\"sf-hint\">Suggested first when they start a visit.</small></label>");
            sb.Append("<label class=\"sf-field sf-prov\"><span>Colour on the schedule</span><input type=\"color\" id=\"sf-f-color\" value=\"" + Ui.Attr(Ui.SafeColor(p.Color) ? p.Color.ToLowerInvariant() : "#3f7d6b") + "\"><em class=\"sf-err\" id=\"sf-e-color\"></em></label>");
            sb.Append("<fieldset class=\"sf-field sf-span2 sf-prov sf-fs\"><legend>Species they see <small>(none ticked = any)</small></legend><div class=\"sf-chks\" id=\"sf-f-species\">");
            foreach (SfPick sp in serr == string.Empty && sps != null ? sps : new List<SfPick>())
            {
                sb.Append("<label class=\"sf-check sf-chk\"><input type=\"checkbox\" data-sp=\"" + Ui.Attr(sp.K) + "\"" + (spec.Contains(sp.K) ? " checked" : string.Empty) + "><span>" + Ui.Enc(sp.T) + "</span></label>");
            }
            sb.Append("</div><em class=\"sf-err\" id=\"sf-e-species\"></em></fieldset>");
            bool ownLock = self || (!c.Manager && c.U.OwnOnly);
            sb.Append("<label class=\"sf-check sf-span2\"><input type=\"checkbox\" id=\"sf-f-own\"" + (p.OwnOnly ? " checked" : string.Empty) + (ownLock ? " disabled" : string.Empty) + "><span>Only their own records <small class=\"sf-dim\">(" + (self ? "you can't change this for yourself" : ownLock ? "only a manager can change this" : "they see only their own appointments and visits") + ")</small></span></label>");
            if (!add)
            {
                sb.Append("<label class=\"sf-check sf-span2\"><input type=\"checkbox\" id=\"sf-f-active\"" + (p.Active ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "><span>Active <small class=\"sf-dim\">(" + (self ? "you can't make yourself inactive" : "inactive staff can't sign in") + ")</small></span></label>");
            }
            sb.Append("</div>");
            sb.Append("<h3>Website team page</h3><div class=\"sf-fgrid\">");
            sb.Append("<label class=\"sf-check\"><input type=\"checkbox\" id=\"sf-f-web\"" + (p.ShowWeb ? " checked" : string.Empty) + "><span>Show on the website</span></label>");
            sb.Append("<label class=\"sf-field\"><span>Order on the page</span><input type=\"number\" id=\"sf-f-order\" min=\"0\" max=\"999\" step=\"1\" value=\"" + p.WebOrder + "\"><em class=\"sf-err\" id=\"sf-e-weborder\"></em></label>");
            sb.Append("<label class=\"sf-check sf-span2 sf-prov\"><input type=\"checkbox\" id=\"sf-f-accept\"" + (p.Accepting ? " checked" : string.Empty) + "><span>Taking new clients</span></label>");
            sb.Append("<fieldset class=\"sf-field sf-span2 sf-fs\"><legend>Photo</legend><div class=\"sf-photos\" id=\"sf-f-photos\">");
            sb.Append("<label class=\"sf-ph\"><input type=\"radio\" name=\"sf-photo\" value=\"\"" + (p.Photo == string.Empty ? " checked" : string.Empty) + "><span class=\"sf-phn\">" + Ui.Enc(Ui.Initials(p.StaffName != string.Empty ? p.StaffName : "New Staff")) + "</span><small>No photo</small></label>");
            List<string> photos = Photos();
            if (p.Photo != string.Empty && !photos.Contains(p.Photo) && Ui.SafePath(p.Photo))
            {
                photos.Insert(0, p.Photo);
            }
            foreach (string ph in photos)
            {
                sb.Append("<label class=\"sf-ph\"><input type=\"radio\" name=\"sf-photo\" value=\"" + Ui.Attr(ph) + "\"" + (ph == p.Photo ? " checked" : string.Empty) + "><img src=\"" + Ui.Attr(ph) + "\" alt=\"\" loading=\"lazy\" width=\"64\" height=\"64\"><small>" + Ui.Enc(Path.GetFileNameWithoutExtension(ph)) + "</small></label>");
            }
            sb.Append("</div><em class=\"sf-err\" id=\"sf-e-photo\"></em></fieldset>");
            sb.Append("<label class=\"sf-field sf-span2\"><span>About them <small>(a short paragraph for the website)</small></span><textarea id=\"sf-f-bio\" rows=\"3\" maxlength=\"2000\">" + Ui.Enc(p.Bio) + "</textarea></label>");
            sb.Append("<label class=\"sf-field\"><span>Education <small>(one per line)</small></span><textarea id=\"sf-f-edu\" rows=\"3\" maxlength=\"1000\">" + Ui.Enc(p.Education) + "</textarea></label>");
            sb.Append("<label class=\"sf-field\"><span>Languages <small>(for example English, Spanish)</small></span><input type=\"text\" id=\"sf-f-langs\" maxlength=\"200\" value=\"" + Ui.Attr(p.Languages) + "\" autocomplete=\"off\"></label>");
            sb.Append("</div><em class=\"sf-err sf-merr\" id=\"sf-e-form\"></em>");
            sb.Append("<div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"sf-btn sf-btn-accent\" id=\"sf-f-go\">" + (add ? "Add staff" : "Save changes") + "</button></div></form>");
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript(Me + ".formShown();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("staffid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, add ? "add staff" : "edit staff", "not allowed", false);
                Toast(response, add ? "You can't add staff." : "You can't change staff details.");
                return response;
            }
            SfStaff? old = null;
            if (add)
            {
                id = Hex(GetDataValue("newid"));
                if (id == string.Empty)
                {
                    Toast(response, "Something went wrong. Close the form and try again.");
                    return response;
                }
                SfStaff? dup = await Person(id);
                if (dup != null)
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await PersonView(response, c, id, "Saved.");
                    return response;
                }
            }
            else
            {
                old = await Person(id);
                if (old == null)
                {
                    Toast(response, "This person was not found. They may have been deleted.");
                    return response;
                }
            }
            if (old != null)
            {
                string block = await Why(c, old);
                if (block != string.Empty)
                {
                    await App.Log(c.U.StaffId, "edit staff", old.StaffName + " · refused, " + block, false);
                    response.SetElementContents("sf-e-form", Ui.Enc(block));
                    Toast(response, block);
                    return response;
                }
            }
            bool self = old != null && old.StaffId == c.U.StaffId;
            string login = Val("login", 60).ToLowerInvariant();
            if (old != null && App.Demo && old.IsDemo)
            {
                login = old.Login;
            }
            string name = Ui.OneLine(GetDataValue("sname"), 100);
            string title = Ui.OneLine(GetDataValue("title"), 100);
            string role = CleanId(GetDataValue("role"));
            if (self && old != null)
            {
                role = old.RoleId;
            }
            string ptype = Val("ptype", 20);
            string cred = Ui.OneLine(GetDataValue("cred"), 60);
            string bio = Val("bio", 2000).Replace("\r", string.Empty);
            string edu = string.Join("\n", Val("edu", 1000).Replace("\r", string.Empty).Split('\n').Select(x => Ui.OneLine(x, 200)).Where(x => x != string.Empty));
            string langs = Ui.OneLine(GetDataValue("langs"), 200);
            string photo = Val("photo", 300);
            string color = Val("color", 7);
            string email = Val("email", 120);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string room = CleanId(GetDataValue("room"));
            List<string> spec = (GetDataValue("species") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            bool own = self && old != null ? old.OwnOnly : Flag("ownonly");
            bool web = Flag("showweb");
            bool accepting = Flag("accepting");
            bool active = add || Flag("active");
            string orderRaw = Val("weborder", 6);
            string pass = Ui.Clip(GetDataValue("pass"), 100);
            string again = Ui.Clip(GetDataValue("again"), 100);
            Dictionary<string, string> errs = new Dictionary<string, string> { { "login", "" }, { "sname", "" }, { "title", "" }, { "role", "" }, { "ptype", "" }, { "photo", "" }, { "color", "" }, { "email", "" }, { "phone", "" }, { "room", "" }, { "species", "" }, { "weborder", "" }, { "pass", "" }, { "form", "" } };
            if (!Regex.IsMatch(login, "^[a-z0-9._-]{3,40}$"))
            {
                errs["login"] = "Use 3 to 40 lowercase letters, numbers, dots, dashes or underscores.";
            }
            if (name == string.Empty)
            {
                errs["sname"] = "Enter their name.";
            }
            List<SfRole> roles = await Roles();
            SfRole? rrow = roles.FirstOrDefault(r => r.RoleId == role);
            if (rrow == null)
            {
                errs["role"] = "Pick a role.";
            }
            if (!new[] { "", "vet", "tech", "groomer" }.Contains(ptype))
            {
                errs["ptype"] = "Pick a provider type.";
            }
            if (photo != string.Empty && !(Photos().Contains(photo) || (old != null && photo == old.Photo && Ui.SafePath(photo))))
            {
                errs["photo"] = "Pick one of the photos shown.";
            }
            if (!Ui.SafeColor(color))
            {
                errs["color"] = "Pick a colour.";
            }
            if (email != string.Empty && !Ui.Email(email))
            {
                errs["email"] = "This email address doesn't look right.";
            }
            if (phone != string.Empty && Ui.Digits(phone).Length < 7)
            {
                errs["phone"] = "Enter the full phone number.";
            }
            if (!int.TryParse(orderRaw == string.Empty ? "0" : orderRaw, out int order) || order < 0 || order > 999)
            {
                errs["weborder"] = "Use a number from 0 to 999.";
                order = 0;
            }
            if (ptype == string.Empty)
            {
                room = string.Empty;
                spec.Clear();
                cred = string.Empty;
                accepting = old?.Accepting ?? true;
            }
            if (room != string.Empty)
            {
                var (rt, rterr) = await Helper.GetDataTable("SELECT 1 FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID WHERE R.ROOM_ID = @room AND T.PURPOSE <> N'boarding' AND (R.ACTIVE = 1 OR R.ROOM_ID = @cur)", new List<SqlParameter> { App.P("room", room, 50), App.P("cur", old?.HomeRoomId ?? string.Empty, 50) });
                if (rterr != string.Empty || rt.Rows.Count == 0)
                {
                    errs["room"] = "Pick a room from the list.";
                }
            }
            if (spec.Count > 0)
            {
                var (sk, skerr) = await Helper.GetListT<SfPick>("SELECT SPECIES_KEY AS K, SPECIES_NAME AS T FROM SPECIES");
                HashSet<string> known = new HashSet<string>(skerr == string.Empty && sk != null ? sk.Select(x => x.K) : new List<string>());
                if (spec.Any(x => !known.Contains(x)))
                {
                    errs["species"] = "Pick species from the list.";
                }
            }
            if (add)
            {
                string weak = Auth.Weak(pass, again);
                if (weak != string.Empty)
                {
                    errs["pass"] = weak.Replace("new password", "password").Replace("two new passwords", "two passwords");
                }
            }
            bool roleChanged = add || (old != null && old.RoleId != role);
            List<MenuRow> menus = c.Menus;
            if (rrow != null)
            {
                bool wasManager = old != null && old.RoleId == "manager";
                if (!c.Manager && (wasManager || role == "manager"))
                {
                    errs["role"] = "Only a manager can add, change or appoint a manager.";
                }
                else if (self && old != null && role != old.RoleId)
                {
                    errs["role"] = "You can't change your own role.";
                }
                else if (roleChanged && !c.Manager)
                {
                    string g = Grantable(c, await RoleRights(role, menus));
                    if (g != string.Empty)
                    {
                        errs["role"] = "This role's default permissions " + g + " Pick another role or ask a manager.";
                    }
                }
            }
            if (!c.Manager && !self && c.U.OwnOnly && (old == null ? own : own != old.OwnOnly))
            {
                errs["form"] = "Only a manager can change who sees only their own records.";
            }
            if (self && !active)
            {
                errs["form"] = "You can't make yourself inactive.";
            }
            if (errs["login"] == string.Empty)
            {
                var (lt, lterr) = await Helper.GetDataTable("SELECT 1 FROM STAFF WHERE LOGIN = @login AND STAFF_ID <> @sid", new List<SqlParameter> { App.P("login", login, 40), App.P("sid", id, 50) });
                if (lterr != string.Empty || lt.Rows.Count > 0)
                {
                    errs["login"] = "Someone already uses this user name. Pick another.";
                }
            }
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("sf-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("sid", id, 50), App.P("login", login, 40), App.P("name", name, 100), App.P("title", title, 100), App.P("role", role, 50),
                App.P("ptype", ptype, 20), App.P("cred", cred, 60), App.P("bio", bio, 2000), App.P("edu", edu, 1000), App.P("langs", langs, 200),
                App.P("photo", photo, 300), App.P("color", color.ToUpperInvariant(), 7), App.P("email", email, 120), App.P("phone", phone, 30), App.P("room", room, 50),
                App.PBit("own", own), App.PBit("web", web), App.PInt("ord", order), App.PBit("acc", accepting), App.PBit("act", active), App.PBit("demo", App.Demo)
            };
            List<string> sql = new List<string>();
            string resetPerms = "DELETE FROM STAFFPERM WHERE STAFF_ID = @sid; INSERT INTO STAFFPERM (STAFF_ID, MENU_KEY, RIGHTS) SELECT @sid, P.MENU_KEY, P.RIGHTS FROM ROLEPERM P JOIN MENU M ON M.MENU_KEY = P.MENU_KEY WHERE P.ROLE_ID = @role AND P.RIGHTS <> N'';";
            if (add)
            {
                ps.Add(App.P("hash", Auth.HashPassword(pass), 200));
                ps.Add(App.PBit("must", !App.Demo));
                sql.Add("IF NOT EXISTS (SELECT 1 FROM STAFF WHERE STAFF_ID = @sid) BEGIN INSERT INTO STAFF (STAFF_ID, LOGIN, STAFF_NAME, TITLE, ROLE_ID, PROVIDER_TYPE, CREDENTIALS, BIO, EDUCATION, LANGUAGES, PHOTO, COLOR, EMAIL, PHONE, HOME_ROOM_ID, OWN_ONLY, SHOW_WEB, WEB_ORDER, ACCEPTING, PASS_HASH, MUST_CHANGE, IS_DEMO, ACTIVE) VALUES (@sid, @login, @name, @title, @role, @ptype, @cred, @bio, @edu, @langs, @photo, @color, @email, @phone, @room, @own, @web, @ord, @acc, @hash, @must, 0, 1); " + resetPerms + " END");
            }
            else
            {
                sql.Add("UPDATE STAFF SET LOGIN = @login, STAFF_NAME = @name, TITLE = @title, ROLE_ID = @role, PROVIDER_TYPE = @ptype, CREDENTIALS = @cred, BIO = @bio, EDUCATION = @edu, LANGUAGES = @langs, PHOTO = @photo, COLOR = @color, EMAIL = @email, PHONE = @phone, HOME_ROOM_ID = @room, OWN_ONLY = @own, SHOW_WEB = @web, WEB_ORDER = @ord, ACCEPTING = @acc, ACTIVE = @act, SYSDTE = GETDATE() WHERE STAFF_ID = @sid");
                sql.Add(LastManager);
                if (roleChanged)
                {
                    sql.Add(resetPerms);
                }
                if (!active)
                {
                    sql.Add("DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid");
                }
            }
            sql.Add("DELETE FROM STAFFSPECIES WHERE STAFF_ID = @sid");
            for (int i = 0; i < spec.Count; i++)
            {
                ps.Add(App.P("sp" + i, spec[i], 20));
                sql.Add("INSERT INTO STAFFSPECIES (STAFF_ID, SPECIES_KEY) VALUES (@sid, @sp" + i + ")");
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                if (result.Contains("LASTMANAGER"))
                {
                    response.SetElementContents("sf-e-form", "At least one active manager must stay. Make someone else a manager first.");
                }
                else if (result.Contains("UX_STAFF_LOGIN", StringComparison.OrdinalIgnoreCase) || result.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    response.SetElementContents("sf-e-login", "Someone already uses this user name. Pick another.");
                }
                else
                {
                    response.SetElementContents("sf-e-form", "The changes could not be saved. Try again.");
                }
                return response;
            }
            string target;
            if (add)
            {
                target = name + " (" + login + ") · " + (rrow?.RoleName ?? role);
            }
            else
            {
                List<string> ch = new List<string>();
                void Note(bool changed, string text)
                {
                    if (changed)
                    {
                        ch.Add(text);
                    }
                }
                SfStaff o = old!;
                Note(o.Login != login, "user name " + o.Login + " → " + login);
                Note(o.StaffName != name, "name");
                Note(o.Title != title, "title");
                Note(o.RoleId != role, "role " + o.RoleName + " → " + (rrow?.RoleName ?? role) + " (permissions reset)");
                Note(o.ProviderType != ptype, "provider " + TypeName(o.ProviderType) + " → " + TypeName(ptype));
                Note(o.Active != active, active ? "made active" : "made inactive");
                Note(o.Photo != photo, "photo");
                Note(o.ShowWeb != web || o.WebOrder != order || o.Accepting != accepting, "website");
                Note(o.HomeRoomId != room, "home room");
                Note(o.OwnOnly != own, own ? "own records only" : "all records");
                Note(o.Bio != bio || o.Education != edu || o.Languages != langs || o.Credentials != cred, "profile");
                Note(o.Email != email || o.Phone != phone, "contact");
                Note(!string.Equals(o.Color, color, StringComparison.OrdinalIgnoreCase), "colour");
                target = name + " · " + (ch.Count > 0 ? string.Join(", ", ch) : "details");
            }
            await App.Log(c.U.StaffId, add ? "add staff" : "edit staff", target, true);
            response.ExecuteScript(Me + ".closeModal();");
            await PersonView(response, c, id, add ? (ptype != string.Empty ? "Added. Set their weekly hours in Hours and time off." : "Added.") : "Saved.");
            return response;
        }

        private async Task<(Ctx? C, SfStaff? P)> Target(ApiResponse response, char right, bool notSelf)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null);
            }
            if (!c.Can(Key, right))
            {
                Toast(response, "You don't have the right to do that.");
                return (null, null);
            }
            SfStaff? p = await Person(CleanId(GetDataValue("staffid")));
            if (p == null)
            {
                Toast(response, "This person was not found.");
                return (null, null);
            }
            string block = await Why(c, p);
            if (block != string.Empty)
            {
                await App.Log(c.U.StaffId, "staff change", p.StaffName + " · refused, manager only", false);
                Toast(response, block);
                return (null, null);
            }
            if (notSelf && p.StaffId == c.U.StaffId)
            {
                Toast(response, "You can't do that to your own account.");
                return (null, null);
            }
            return (c, p);
        }

        public async Task<ApiResponse> PassForm()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await Target(response, 'E', true);
            if (c == null || p == null)
            {
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>New password for " + Ui.Enc(p.StaffName) + "</h2>");
            if (App.Demo && p.IsDemo)
            {
                sb.Append("<p class=\"sf-mlead\">Sample accounts keep the password <b>demo</b> so every visitor of the demo can sign in. Add a new staff member to try a password change.</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".closeModal()\">OK</button></div>");
            }
            else
            {
                sb.Append("<p class=\"sf-mlead\">They are signed out everywhere. Tell them the new password in person." + (App.Demo ? string.Empty : " They must choose their own password the next time they sign in.") + "</p><form class=\"sf-form\" onsubmit=\"return " + Me + ".setPass()\" novalidate><div class=\"sf-fgrid\"><label class=\"sf-field\"><span>New password</span><input type=\"password\" id=\"sf-p-new\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"sf-err\" id=\"sf-e-pnew\"></em></label><label class=\"sf-field\"><span>New password again</span><input type=\"password\" id=\"sf-p-again\" maxlength=\"100\" autocomplete=\"new-password\"></label></div><p class=\"sf-hint\">At least 8 characters.</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"sf-btn sf-btn-accent\" id=\"sf-p-go\">Save password</button></div></form>");
            }
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript(Me + ".passShown();");
            return response;
        }

        public async Task<ApiResponse> SetPass()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'E', true);
            if (c == null || p == null)
            {
                return response;
            }
            if (App.Demo && p.IsDemo)
            {
                Toast(response, "Sample accounts keep the password demo.");
                return response;
            }
            string next = Ui.Clip(GetDataValue("next"), 100);
            string again = Ui.Clip(GetDataValue("again"), 100);
            string weak = Auth.Weak(next, again);
            response.SetElementContents("sf-e-pnew", weak);
            if (weak != string.Empty)
            {
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "UPDATE STAFF SET PASS_HASH = @hash, MUST_CHANGE = @must, SYSDTE = GETDATE() WHERE STAFF_ID = @sid",
                "DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid"
            }, new List<SqlParameter> { App.P("hash", Auth.HashPassword(next), 200), App.PBit("must", !App.Demo), App.P("sid", p.StaffId, 50) });
            if (Helper.IsError(result))
            {
                response.SetElementContents("sf-e-pnew", "The password could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "set password", p.StaffName + " (" + p.Login + ") · signed out everywhere", true);
            response.ExecuteScript(Me + ".closeModal();");
            await PersonView(response, c, p.StaffId, "New password saved. " + FirstName(p.StaffName) + " was signed out everywhere.");
            return response;
        }

        public async Task<ApiResponse> Kick()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'E', true);
            if (c == null || p == null)
            {
                return response;
            }
            string result = await Helper.PutData(new List<string> { "DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid" }, new List<SqlParameter> { App.P("sid", p.StaffId, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, "That did not work. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "sign out staff", p.StaffName + " (" + p.Login + ")", true);
            await PersonView(response, c, p.StaffId, FirstName(p.StaffName) + " was signed out everywhere.");
            return response;
        }

        public async Task<ApiResponse> Retire()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'E', true);
            if (c == null || p == null)
            {
                return response;
            }
            bool on = Flag("makeactive");
            List<string> sql = new List<string> { "UPDATE STAFF SET ACTIVE = @act, SYSDTE = GETDATE() WHERE STAFF_ID = @sid" };
            if (!on)
            {
                sql.Add(LastManager);
                sql.Add("DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid");
            }
            string result = await Helper.PutData(sql, new List<SqlParameter> { App.PBit("act", on), App.P("sid", p.StaffId, 50), App.PBit("demo", App.Demo) });
            response.ExecuteScript(Me + ".closeModal();");
            if (Helper.IsError(result))
            {
                Toast(response, result.Contains("LASTMANAGER") ? "At least one active manager must stay. Make someone else a manager first." : "That did not work. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, on ? "activate staff" : "deactivate staff", p.StaffName + " (" + p.Login + ")" + (on ? string.Empty : " · signed out"), true);
            await PersonView(response, c, p.StaffId, on ? FirstName(p.StaffName) + " is active again." : FirstName(p.StaffName) + " is inactive and was signed out.");
            return response;
        }

        private static async Task<List<string>> Uses(string sid)
        {
            string[,] refs =
            {
                { "ACTIVITY", "STAFF_ID", "sign-ins or other activity" },
                { "APPOINTMENT", "STAFF_ID", "appointments" },
                { "APPOINTMENT", "CREATED_BY", "appointments they booked" },
                { "APPTLOG", "STAFF_ID", "appointment history" },
                { "VISIT", "STAFF_ID", "visits" },
                { "VISIT", "CREATED_BY", "check-ins" },
                { "VISITROOM", "STAFF_ID", "room history" },
                { "VISITNOTE", "STAFF_ID", "medical notes" },
                { "VISITNOTE", "SIGNED_BY", "signed notes" },
                { "DIAGNOSIS", "STAFF_ID", "diagnoses" },
                { "ORDERLINE", "ORDERED_BY", "orders" },
                { "ORDERLINE", "DONE_BY", "orders they completed" },
                { "RESULT", "ENTERED_BY", "test results" },
                { "VISITFILE", "UPLOADED_BY", "files" },
                { "INVOICE", "CREATED_BY", "invoices" },
                { "PAYMENT", "STAFF_ID", "payments" },
                { "MAILLOG", "STAFF_ID", "emails" },
                { "REMINDER", "STAFF_ID", "reminders" },
                { "PETWEIGHT", "STAFF_ID", "weights" },
                { "PETVACCINE", "STAFF_ID", "vaccines" },
                { "CLIENT", "CREATED_BY", "clients they added" },
                { "PET", "CREATED_BY", "pets they added" },
                { "PAGEDOC", "SAVED_BY", "website pages" },
                { "PAGEVER", "SAVED_BY", "website page versions" },
                { "MEDIA", "ADDED_BY", "media" },
                { "ALBUM", "ADDED_BY", "albums" },
                { "MESSAGE", "HANDLED_BY", "messages" }
            };
            List<string> parts = new List<string>();
            for (int i = 0; i < refs.GetLength(0); i++)
            {
                parts.Add("SELECT N'" + refs[i, 2] + "' AS K, N'' AS T WHERE EXISTS (SELECT 1 FROM " + refs[i, 0] + " WHERE " + refs[i, 1] + " = @sid)");
            }
            var (rows, emsg) = await Helper.GetListT<SfPick>(string.Join(" UNION ALL ", parts), new List<SqlParameter> { App.P("sid", sid, 50) });
            if (emsg != string.Empty || rows == null)
            {
                return new List<string> { "records (they could not be checked)" };
            }
            return rows.Select(r => r.K).Distinct().ToList();
        }

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await Target(response, 'D', true);
            if (c == null || p == null)
            {
                return response;
            }
            List<string> uses = await Uses(p.StaffId);
            StringBuilder sb = new StringBuilder();
            if (uses.Count > 0)
            {
                sb.Append("<h2>" + Ui.Enc(p.StaffName) + " can't be deleted</h2><p class=\"sf-mlead\">They have " + Ui.Enc(string.Join(", ", uses.Take(6))) + (uses.Count > 6 ? " and more" : string.Empty) + " in the records, which must keep their name.</p>");
                if (p.Active && c.Can(Key, 'E'))
                {
                    sb.Append("<p>Make them inactive instead: they can't sign in, are left out of bookings, and the records keep their name.</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Not now</button><button type=\"button\" class=\"sf-btn sf-btn-danger\" onclick=\"" + Me + ".retireGo(0, this)\">Make inactive</button></div>");
                }
                else
                {
                    sb.Append("<div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".closeModal()\">OK</button></div>");
                }
            }
            else
            {
                sb.Append("<h2>Delete " + Ui.Enc(p.StaffName) + "?</h2><p class=\"sf-mlead\">They never did anything in the system, so they can be removed completely with their hours, time off and permissions. This can't be undone.</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Not now</button><button type=\"button\" class=\"sf-btn sf-btn-danger\" onclick=\"" + Me + ".remove(this)\">Delete</button></div>");
            }
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown();");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'D', true);
            if (c == null || p == null)
            {
                return response;
            }
            List<string> uses = await Uses(p.StaffId);
            if (uses.Count > 0)
            {
                response.ExecuteScript(Me + ".closeModal();");
                Toast(response, "They have records now, so they can't be deleted. Make them inactive instead.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "DELETE FROM STAFF WHERE STAFF_ID = @sid AND NOT EXISTS (SELECT 1 FROM ACTIVITY WHERE STAFF_ID = @sid) AND NOT EXISTS (SELECT 1 FROM APPOINTMENT WHERE STAFF_ID = @sid) AND NOT EXISTS (SELECT 1 FROM VISIT WHERE STAFF_ID = @sid); IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;",
                LastManager,
                "DELETE FROM STAFFPERM WHERE STAFF_ID = @sid",
                "DELETE FROM STAFFSPECIES WHERE STAFF_ID = @sid",
                "DELETE FROM STAFFHOURS WHERE STAFF_ID = @sid",
                "DELETE FROM TIMEOFF WHERE STAFF_ID = @sid",
                "DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid",
                "DELETE FROM BOOKSTAFF WHERE STAFF_ID = @sid"
            }, new List<SqlParameter> { App.P("sid", p.StaffId, 50), App.PBit("demo", App.Demo) });
            response.ExecuteScript(Me + ".closeModal();");
            if (Helper.IsError(result))
            {
                Toast(response, result.Contains("LASTMANAGER") ? "At least one active manager must stay." : "They could not be deleted. Make them inactive instead.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete staff", p.StaffName + " (" + p.Login + ")", true);
            await ListView(response, c, p.StaffName + " was deleted.");
            return response;
        }

        public async Task<ApiResponse> Defaults()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'E', true);
            if (c == null || p == null)
            {
                return response;
            }
            Dictionary<string, string> defs = await RoleRights(p.RoleId, c.Menus);
            string g = Grantable(c, defs);
            if (g != string.Empty)
            {
                Toast(response, "The " + (p.RoleName != string.Empty ? p.RoleName : "role") + " defaults " + g + " Only a manager can apply them.");
                return response;
            }
            response.ExecuteScript(Me + ".fill('sf-pm-person', '" + Ui.Js(PermText(defs)) + "');");
            Toast(response, "The " + (p.RoleName != string.Empty ? p.RoleName : "role") + " defaults are ticked. Save to keep them.");
            return response;
        }

        public async Task<ApiResponse> Perms()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            var (c, p) = await Target(response, 'E', false);
            if (c == null || p == null)
            {
                return response;
            }
            response.SetElementContents("sf-e-perm", string.Empty);
            if (p.RoleId == "manager")
            {
                response.SetElementContents("sf-e-perm", "Managers always have every right.");
                return response;
            }
            if (p.StaffId == c.U.StaffId)
            {
                await App.Log(c.U.StaffId, "permissions", p.StaffName + " · refused, own account", false);
                response.SetElementContents("sf-e-perm", "You can't change your own permissions.");
                return response;
            }
            List<MenuRow> menus = c.Menus;
            Dictionary<string, string> before = await StaffRights(p.StaffId, menus);
            Dictionary<string, string> after = ParsePerms(GetDataValue("perms"), menus);
            if (!c.Manager && (R(before, Key) != string.Empty || R(after, Key) != string.Empty))
            {
                await App.Log(c.U.StaffId, "permissions", p.StaffName + " · refused, staff right is manager only", false);
                response.SetElementContents("sf-e-perm", R(before, Key) != string.Empty ? "They can manage staff, so only a manager can change their permissions." : "Only a manager can give someone the Staff and permissions right.");
                return response;
            }
            List<string> extra = Beyond(c, after);
            if (extra.Count > 0)
            {
                await App.Log(c.U.StaffId, "permissions", p.StaffName + " · refused, rights the caller lacks", false);
                response.SetElementContents("sf-e-perm", Ui.Enc("You can only give rights you have yourself. Untick: " + Areas(extra) + "."));
                return response;
            }
            List<string> sql = new List<string> { "DELETE FROM STAFFPERM WHERE STAFF_ID = @sid" };
            List<SqlParameter> ps = new List<SqlParameter> { App.P("sid", p.StaffId, 50) };
            int i = 0;
            foreach (KeyValuePair<string, string> kv in after)
            {
                ps.Add(App.P("k" + i, kv.Key, 50));
                ps.Add(App.P("r" + i, kv.Value, 10));
                sql.Add("INSERT INTO STAFFPERM (STAFF_ID, MENU_KEY, RIGHTS) VALUES (@sid, @k" + i + ", @r" + i + ")");
                i++;
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("sf-e-perm", "The permissions could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "permissions", p.StaffName + " · " + Diff(before, after, menus), true);
            await PersonView(response, c, p.StaffId, "Permissions saved. They apply the next time " + FirstName(p.StaffName) + " opens a page.");
            return response;
        }

        public async Task<ApiResponse> RoleTab()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("sf-main", Denied());
                return response;
            }
            await RolesView(response, c, CleanId(GetDataValue("roleid")), string.Empty);
            return response;
        }

        private static async Task RolesView(ApiResponse response, Ctx c, string roleId, string toast)
        {
            List<SfRole> roles = await Roles();
            SfRole? on = roles.FirstOrDefault(r => r.RoleId == roleId) ?? roles.FirstOrDefault(r => r.RoleId != "manager") ?? roles.FirstOrDefault();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"sf-ptop\"><div><h1>Staff and permissions</h1><p>Roles group staff, and each role has default permissions that new staff start with.</p></div><div class=\"sf-acts\">");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".roleForm('', this)\">Add role</button>");
            }
            sb.Append("</div></div>");
            sb.Append(Tabs("roles"));
            sb.Append("<div class=\"sf-rgrid\"><section class=\"sf-panel sf-rlist\"><h2 class=\"sf-h2\">Roles</h2><ul class=\"sf-rolel\">");
            foreach (SfRole r in roles)
            {
                bool here = on != null && r.RoleId == on.RoleId;
                sb.Append("<li><button type=\"button\" class=\"sf-rb" + (here ? " sf-rb-on" : string.Empty) + "\" onclick=\"" + Me + ".roles('" + Ui.Js(r.RoleId) + "')\"" + (here ? " aria-current=\"true\"" : string.Empty) + "><b>" + Ui.Enc(r.RoleName) + "</b><small>" + (r.Used == 0 ? "Nobody yet" : Ui.Plural(r.Used, "person", "people")) + " · order " + r.RoleOrder + "</small></button></li>");
            }
            sb.Append("</ul></section><section class=\"sf-panel sf-rmat\">");
            if (on == null)
            {
                sb.Append("<p class=\"sf-empty\">No roles yet.</p>");
            }
            else
            {
                bool mgr = on.RoleId == "manager";
                bool canEdit = c.Can(Key, 'E') && (!mgr || c.Manager);
                sb.Append("<div class=\"sf-shead\"><h2 class=\"sf-h2\">" + Ui.Enc(on.RoleName) + "</h2><span class=\"sf-sub\">" + (mgr ? "Managers always have every right; there is nothing to set." : "Default permissions. New staff with this role start with these, and Reset to role defaults copies them.") + "</span><div class=\"sf-acts\">");
                if (canEdit)
                {
                    sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-ghost\" onclick=\"" + Me + ".roleForm('" + Ui.Js(on.RoleId) + "', this)\">Rename</button>");
                }
                if (c.Can(Key, 'D') && !mgr)
                {
                    sb.Append("<button type=\"button\" class=\"sf-btn sf-btn-sm sf-btn-danger\" onclick=\"" + Me + ".roleAsk('" + Ui.Js(on.RoleId) + "')\">Delete</button>");
                }
                sb.Append("</div></div>");
                Dictionary<string, string> rights = mgr ? new Dictionary<string, string>() : await RoleRights(on.RoleId, c.Menus);
                HashSet<string> fixedKeys = new HashSet<string>();
                if (!c.Manager)
                {
                    fixedKeys.Add(Key);
                }
                string g = mgr ? string.Empty : Grantable(c, rights);
                bool permEdit = canEdit && !mgr && g == string.Empty;
                if (canEdit && !mgr && g != string.Empty)
                {
                    sb.Append("<p class=\"sf-info\">" + Ui.Enc("These defaults " + g + " Only a manager can change them.") + "</p>");
                }
                else if (permEdit && !c.Manager)
                {
                    sb.Append("<p class=\"sf-info\">You can only give rights you have yourself. The others are greyed out.</p>");
                }
                sb.Append(Matrix("sf-pm-role", c.Menus, rights, mgr, permEdit, fixedKeys, c.Manager ? null : c.Rights));
                if (permEdit)
                {
                    sb.Append("<div class=\"sf-pfoot\"><button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".saveRole('" + Ui.Js(on.RoleId) + "', this)\">Save " + Ui.Enc(on.RoleName) + " defaults</button></div><em class=\"sf-err\" id=\"sf-e-rperm\"></em><p class=\"sf-hint\">Saving changes the defaults only. People who already have this role keep their own permissions until you reset them.</p>");
                }
            }
            sb.Append("</section></div>");
            response.SetElementContents("sf-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('roles', '" + Ui.Js(on?.RoleId ?? string.Empty) + "');");
            Toast(response, toast);
        }

        public async Task<ApiResponse> RoleForm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string rid = CleanId(GetDataValue("roleid"));
            bool add = rid == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            SfRole r = new SfRole();
            List<SfRole> roles = await Roles();
            if (!add)
            {
                SfRole? found = roles.FirstOrDefault(x => x.RoleId == rid);
                if (found == null)
                {
                    Toast(response, "This role was not found.");
                    return response;
                }
                if (found.RoleId == "manager" && !c.Manager)
                {
                    Toast(response, "Only a manager can change the manager role.");
                    return response;
                }
                r = found;
            }
            else
            {
                r.RoleOrder = roles.Count == 0 ? 1 : roles.Max(x => x.RoleOrder) + 1;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "Add role" : "Rename " + Ui.Enc(r.RoleName)) + "</h2><form class=\"sf-form\" onsubmit=\"return " + Me + ".roleSave()\" novalidate><input type=\"hidden\" id=\"sf-r-id\" value=\"" + Ui.Attr(r.RoleId) + "\"><div class=\"sf-fgrid\">");
            sb.Append("<label class=\"sf-field\"><span>Role name</span><input type=\"text\" id=\"sf-r-name\" maxlength=\"60\" value=\"" + Ui.Attr(r.RoleName) + "\" autocomplete=\"off\"><em class=\"sf-err\" id=\"sf-e-rname\"></em></label>");
            sb.Append("<label class=\"sf-field\"><span>Order in lists</span><input type=\"number\" id=\"sf-r-order\" min=\"0\" max=\"999\" step=\"1\" value=\"" + r.RoleOrder + "\"><em class=\"sf-err\" id=\"sf-e-rorder\"></em></label></div>");
            if (add)
            {
                sb.Append("<p class=\"sf-hint\">A new role starts with no permissions. Tick its defaults after saving.</p>");
            }
            sb.Append("<div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"sf-btn sf-btn-accent\" id=\"sf-r-go\">" + (add ? "Add role" : "Save") + "</button></div></form>");
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript(Me + ".roleShown();");
            return response;
        }

        public async Task<ApiResponse> RoleSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string rid = CleanId(GetDataValue("roleid"));
            bool add = rid == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            List<SfRole> roles = await Roles();
            SfRole? old = null;
            if (add)
            {
                rid = Hex(GetDataValue("newid"));
                if (rid == string.Empty)
                {
                    Toast(response, "Something went wrong. Close the form and try again.");
                    return response;
                }
                if (roles.Any(x => x.RoleId == rid))
                {
                    response.ExecuteScript(Me + ".closeModal();");
                    await RolesView(response, c, rid, "Saved.");
                    return response;
                }
            }
            else
            {
                old = roles.FirstOrDefault(x => x.RoleId == rid);
                if (old == null)
                {
                    Toast(response, "This role was not found.");
                    return response;
                }
                if (old.RoleId == "manager" && !c.Manager)
                {
                    Toast(response, "Only a manager can change the manager role.");
                    return response;
                }
            }
            string name = Ui.OneLine(GetDataValue("rname"), 60);
            string orderRaw = Val("rorder", 6);
            string en = name == string.Empty ? "Enter a name for the role." : roles.Any(x => x.RoleId != rid && string.Equals(x.RoleName, name, StringComparison.OrdinalIgnoreCase)) ? "There is already a role with this name." : string.Empty;
            string eo = int.TryParse(orderRaw == string.Empty ? "0" : orderRaw, out int order) && order >= 0 && order <= 999 ? string.Empty : "Use a number from 0 to 999.";
            response.SetElementContents("sf-e-rname", en);
            response.SetElementContents("sf-e-rorder", eo);
            if (en != string.Empty || eo != string.Empty)
            {
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("rid", rid, 50), App.P("name", name, 60), App.PInt("ord", order) };
            string result = await Helper.PutData(new List<string> { add ? "IF NOT EXISTS (SELECT 1 FROM ROLE WHERE ROLE_ID = @rid) INSERT INTO ROLE (ROLE_ID, ROLE_NAME, ROLE_ORDER) VALUES (@rid, @name, @ord)" : "UPDATE ROLE SET ROLE_NAME = @name, ROLE_ORDER = @ord, SYSDTE = GETDATE() WHERE ROLE_ID = @rid" }, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("sf-e-rname", "The role could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, add ? "add role" : "edit role", add ? name : (old!.RoleName != name ? old.RoleName + " → " + name : name) + (old != null && old.RoleOrder != order ? " · order " + old.RoleOrder + " → " + order : string.Empty), true);
            response.ExecuteScript(Me + ".closeModal();");
            await RolesView(response, c, rid, add ? "Role added. Tick its default permissions." : "Saved.");
            return response;
        }

        public async Task<ApiResponse> RolePerms()
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
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            string rid = CleanId(GetDataValue("roleid"));
            SfRole? role = (await Roles()).FirstOrDefault(x => x.RoleId == rid);
            if (role == null)
            {
                Toast(response, "This role was not found.");
                return response;
            }
            response.SetElementContents("sf-e-rperm", string.Empty);
            if (role.RoleId == "manager")
            {
                response.SetElementContents("sf-e-rperm", "Managers always have every right.");
                return response;
            }
            List<MenuRow> menus = c.Menus;
            Dictionary<string, string> before = await RoleRights(rid, menus);
            Dictionary<string, string> after = ParsePerms(GetDataValue("perms"), menus);
            string gb = Grantable(c, before);
            string ga = Grantable(c, after);
            if (gb != string.Empty || ga != string.Empty)
            {
                await App.Log(c.U.StaffId, "role defaults", role.RoleName + " · refused, rights the caller lacks", false);
                response.SetElementContents("sf-e-rperm", Ui.Enc(gb != string.Empty ? "This role's defaults " + gb + " Only a manager can change them." : "The new defaults would " + ga.Replace("include", "include") + " Only a manager can give them."));
                return response;
            }
            List<string> sql = new List<string> { "DELETE FROM ROLEPERM WHERE ROLE_ID = @rid" };
            List<SqlParameter> ps = new List<SqlParameter> { App.P("rid", rid, 50) };
            int i = 0;
            foreach (KeyValuePair<string, string> kv in after)
            {
                ps.Add(App.P("k" + i, kv.Key, 50));
                ps.Add(App.P("r" + i, kv.Value, 10));
                sql.Add("INSERT INTO ROLEPERM (ROLE_ID, MENU_KEY, RIGHTS) VALUES (@rid, @k" + i + ", @r" + i + ")");
                i++;
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("sf-e-rperm", "The defaults could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "role defaults", role.RoleName + " · " + Diff(before, after, menus), true);
            await RolesView(response, c, rid, role.RoleName + " defaults saved.");
            return response;
        }

        public async Task<ApiResponse> RoleAsk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            SfRole? role = (await Roles()).FirstOrDefault(x => x.RoleId == CleanId(GetDataValue("roleid")));
            if (role == null)
            {
                Toast(response, "This role was not found.");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            if (role.RoleId == "manager" || role.Used > 0)
            {
                sb.Append("<h2>" + Ui.Enc(role.RoleName) + " can't be deleted</h2><p class=\"sf-mlead\">" + (role.RoleId == "manager" ? "The manager role is always needed." : Ui.Plural(role.Used, "person has", "people have") + " this role (active or inactive). Give them another role first.") + "</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-accent\" onclick=\"" + Me + ".closeModal()\">OK</button></div>");
            }
            else
            {
                sb.Append("<h2>Delete " + Ui.Enc(role.RoleName) + "?</h2><p class=\"sf-mlead\">Nobody has this role. Its default permissions are removed too.</p><div class=\"sf-mfoot\"><button type=\"button\" class=\"sf-btn sf-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Not now</button><button type=\"button\" class=\"sf-btn sf-btn-danger\" onclick=\"" + Me + ".roleRemove('" + Ui.Js(role.RoleId) + "', this)\">Delete role</button></div>");
            }
            response.SetElementContents("sf-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown();");
            return response;
        }

        public async Task<ApiResponse> RoleRemove()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            SfRole? role = (await Roles()).FirstOrDefault(x => x.RoleId == CleanId(GetDataValue("roleid")));
            response.ExecuteScript(Me + ".closeModal();");
            if (role == null || role.RoleId == "manager")
            {
                Toast(response, role == null ? "This role was not found." : "The manager role can't be deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "IF EXISTS (SELECT 1 FROM STAFF WHERE ROLE_ID = @rid) THROW 50004, N'INUSE', 1;",
                "DELETE FROM ROLEPERM WHERE ROLE_ID = @rid",
                "DELETE FROM ROLE WHERE ROLE_ID = @rid AND ROLE_ID <> N'manager'"
            }, new List<SqlParameter> { App.P("rid", role.RoleId, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, result.Contains("INUSE") ? "Someone has this role now, so it can't be deleted." : "The role could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete role", role.RoleName, true);
            await RolesView(response, c, string.Empty, role.RoleName + " was deleted.");
            return response;
        }
    }
}
