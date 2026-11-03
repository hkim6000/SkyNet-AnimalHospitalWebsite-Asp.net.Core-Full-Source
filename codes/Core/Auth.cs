using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public sealed class Ctx
    {
        public StaffRow U { get; set; } = new StaffRow();
        public string Token { get; set; } = string.Empty;
        public List<MenuRow> Menus { get; set; } = new List<MenuRow>();
        public Dictionary<string, string> Rights { get; set; } = new Dictionary<string, string>();
        public bool Manager => U.RoleId == "manager";

        public bool Can(string menuKey, char letter)
        {
            return Rights.TryGetValue(menuKey, out string? r) && r.Contains(letter);
        }

        public bool Mine(string staffId)
        {
            return !U.OwnOnly || staffId == U.StaffId;
        }
    }

    public static class Auth
    {
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();
        private const string StaffCols = "S.STAFF_ID AS StaffId, S.LOGIN AS Login, S.STAFF_NAME AS StaffName, S.TITLE AS Title, S.ROLE_ID AS RoleId, ISNULL(R.ROLE_NAME, N'') AS RoleName, S.PROVIDER_TYPE AS ProviderType, S.PHOTO AS Photo, S.COLOR AS Color, S.OWN_ONLY AS OwnOnly, S.PASS_HASH AS PassHash, S.MUST_CHANGE AS MustChange, S.IS_DEMO AS IsDemo, S.ACTIVE AS Active";

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool VerifySha(string password, string hex)
        {
            string hash = (hex ?? string.Empty).ToLowerInvariant();
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
            {
                return false;
            }
            string given = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(hash));
        }

        public static string Weak(string next, string again)
        {
            if (next.Length < 8)
            {
                return "Use at least 8 characters for the new password.";
            }
            if (next != again)
            {
                return "The two new passwords are not the same.";
            }
            return string.Empty;
        }

        public static bool Locked(string key)
        {
            lock (Fails)
            {
                string k = key.ToLowerInvariant();
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(f => f < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        private static DateTime lastPrune = DateTime.MinValue;

        public static void Fail(string key)
        {
            lock (Fails)
            {
                string k = key.ToLowerInvariant();
                if (Fails.Count > 2000 && DateTime.UtcNow > lastPrune.AddSeconds(30))
                {
                    lastPrune = DateTime.UtcNow;
                    DateTime cut = DateTime.UtcNow.AddMinutes(-5);
                    foreach (string old in Fails.Where(f => f.Value.Count == 0 || f.Value.Max() < cut).Select(f => f.Key).ToList())
                    {
                        Fails.Remove(old);
                    }
                    if (Fails.Count > 20000)
                    {
                        foreach (string old in Fails.Where(f => !f.Key.StartsWith("login:", StringComparison.Ordinal) && !f.Key.StartsWith("acct:", StringComparison.Ordinal)).OrderBy(f => f.Value.Count == 0 ? DateTime.MinValue : f.Value.Max()).Take(Fails.Count - 10000).Select(f => f.Key).ToList())
                        {
                            Fails.Remove(old);
                        }
                    }
                }
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[k] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static async Task<StaffRow?> StaffByLogin(string login)
        {
            var (row, emsg) = await Helper.GetOneT<StaffRow>("SELECT " + StaffCols + " FROM STAFF S LEFT JOIN ROLE R ON R.ROLE_ID = S.ROLE_ID WHERE S.LOGIN = @login", new List<SqlParameter> { App.P("login", login, 40) });
            return emsg == string.Empty ? row : null;
        }

        public static async Task<string> Open(string staffId)
        {
            string tok = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            await Helper.PutData(new List<string>
            {
                "DELETE FROM STAFFSESSION WHERE EXPIRES_UTC < SYSUTCDATETIME()",
                "INSERT INTO STAFFSESSION (TOKEN, STAFF_ID, EXPIRES_UTC, CREATED_UTC, IP) VALUES (@tok, @sid, DATEADD(HOUR, 8, SYSUTCDATETIME()), SYSUTCDATETIME(), @ip)",
                "UPDATE STAFF SET LAST_LOGIN = SYSUTCDATETIME() WHERE STAFF_ID = @sid"
            }, new List<SqlParameter> { App.P("tok", tok, 64), App.P("sid", staffId, 50), App.P("ip", App.Ip(), 60) });
            return tok;
        }

        public static async Task Close(string? tok)
        {
            string t = Clean(tok);
            if (t == string.Empty)
            {
                return;
            }
            await Helper.PutData(new List<string> { "DELETE FROM STAFFSESSION WHERE TOKEN = @tok" }, new List<SqlParameter> { App.P("tok", t, 64) });
        }

        public static async Task CloseOthers(string staffId, string keep)
        {
            await Helper.PutData(new List<string> { "DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid AND TOKEN <> @tok" }, new List<SqlParameter> { App.P("sid", staffId, 50), App.P("tok", keep, 64) });
        }

        private static string Clean(string? tok)
        {
            string t = (tok ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(t, "^[a-f0-9]{48}$") ? t : string.Empty;
        }

        public static async Task<List<MenuRow>> Menus()
        {
            var (list, emsg) = await Helper.GetListT<MenuRow>("SELECT MENU_KEY AS MenuKey, SECTION_KEY AS SectionKey, SECTION_NAME AS SectionName, SECTION_ORDER AS SectionOrder, MENU_NAME AS MenuName, PAGE_NAME AS PageName, MENU_ORDER AS MenuOrder, RIGHTS AS Rights, EXTRA_NAME AS ExtraName, HINT AS Hint, SHOW_MENU AS ShowMenu FROM MENU WHERE ACTIVE = 1 ORDER BY SECTION_ORDER, MENU_ORDER");
            return emsg == string.Empty && list != null ? list : new List<MenuRow>();
        }

        public static string CleanRights(string allowed, string given)
        {
            string g = (given ?? string.Empty).ToUpperInvariant();
            string r = new string("VAEDX".Where(ch => allowed.Contains(ch) && g.Contains(ch)).ToArray());
            if (r.Length > 0 && !r.Contains('V'))
            {
                r = "V" + r;
            }
            return r;
        }

        public static async Task<Ctx?> Check(string? tok)
        {
            await App.DemoDaily();
            string t = Clean(tok);
            if (t == string.Empty)
            {
                return null;
            }
            var (row, emsg) = await Helper.GetOneT<StaffRow>("SELECT " + StaffCols + " FROM STAFFSESSION X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID LEFT JOIN ROLE R ON R.ROLE_ID = S.ROLE_ID WHERE X.TOKEN = @tok AND X.EXPIRES_UTC > SYSUTCDATETIME() AND S.ACTIVE = 1 AND S.MUST_CHANGE = 0", new List<SqlParameter> { App.P("tok", t, 64) });
            if (emsg != string.Empty || row == null)
            {
                return null;
            }
            if (!App.Demo && row.IsDemo)
            {
                return null;
            }
            await Helper.PutData(new List<string> { "UPDATE STAFFSESSION SET EXPIRES_UTC = DATEADD(HOUR, 8, SYSUTCDATETIME()) WHERE TOKEN = @tok AND EXPIRES_UTC < DATEADD(HOUR, 7, SYSUTCDATETIME())" }, new List<SqlParameter> { App.P("tok", t, 64) });
            Ctx c = new Ctx { U = row, Token = t, Menus = await Menus() };
            if (c.Manager)
            {
                foreach (MenuRow m in c.Menus)
                {
                    c.Rights[m.MenuKey] = CleanRights(m.Rights, m.Rights);
                }
            }
            else
            {
                var (perms, perr) = await Helper.GetListT<PermRow>("SELECT MENU_KEY AS MenuKey, RIGHTS AS Rights FROM STAFFPERM WHERE STAFF_ID = @sid", new List<SqlParameter> { App.P("sid", row.StaffId, 50) });
                foreach (PermRow p in perr == string.Empty && perms != null ? perms : new List<PermRow>())
                {
                    MenuRow? m = c.Menus.FirstOrDefault(x => x.MenuKey == p.MenuKey);
                    string r = m == null ? string.Empty : CleanRights(m.Rights, p.Rights);
                    if (r.Length > 0)
                    {
                        c.Rights[p.MenuKey] = r;
                    }
                }
            }
            return c;
        }

        public static string Menu(string p, string js, string page, Ctx c)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<a class=\"" + p + "-mi " + p + "-mtop" + (page == "Admin" ? " " + p + "-on" : string.Empty) + "\" href=\"Admin\">" + Icon(p, "home") + "<span>Dashboard</span></a>");
            foreach (IGrouping<string, MenuRow> g in c.Menus.Where(m => m.ShowMenu && c.Can(m.MenuKey, 'V')).GroupBy(m => m.SectionKey))
            {
                List<MenuRow> items = g.OrderBy(m => m.MenuOrder).ToList();
                bool here = items.Any(m => m.PageName == page);
                string sec = Ui.Attr(g.Key);
                sb.Append("<div class=\"" + p + "-sec" + (here ? " " + p + "-sopen " + p + "-shere" : string.Empty) + "\" data-sec=\"" + sec + "\"><button type=\"button\" class=\"" + p + "-sech\" aria-expanded=\"" + (here ? "true" : "false") + "\" onclick=\"" + js + ".sec(this)\">" + Icon(p, g.Key) + "<span>" + Ui.Enc(items[0].SectionName) + "</span><i class=\"" + p + "-chev\" aria-hidden=\"true\"></i></button><div class=\"" + p + "-secb\">");
                foreach (MenuRow m in items)
                {
                    sb.Append("<a class=\"" + p + "-mi" + (m.PageName == page ? " " + p + "-on" : string.Empty) + "\" href=\"" + Ui.Attr(m.PageName) + "\">" + Ui.Enc(m.MenuName) + "</a>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("<a class=\"" + p + "-mi " + p + "-mtop" + (page == "AdminAccount" ? " " + p + "-on" : string.Empty) + "\" href=\"AdminAccount\">" + Icon(p, "account") + "<span>My account</span></a>");
            return sb.ToString();
        }

        public static string Icon(string p, string key)
        {
            string d = key switch
            {
                "home" => "M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z",
                "front" => "M4 5h16v14H4zM4 9h16M9 13h6",
                "clinical" => "M12 4v16M4 12h16",
                "boarding" => "M3 20V9l9-5 9 5v11M8 20v-6h8v6",
                "people" => "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM2 21a7 7 0 0 1 14 0M17 11a3 3 0 1 0 0-6M22 21a6 6 0 0 0-4-5.6",
                "money" => "M3 6h18v12H3zM3 10h18M7 15h3",
                "catalog" => "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z",
                "reports" => "M4 20V10M10 20V4M16 20v-8M22 20H2",
                "website" => "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zM3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18",
                "admin" => "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19 12l2-1-1-3-2 .5-1.5-1.5L17 5l-3-1-1 2h-2l-1-2-3 1 .5 2L6 8.5 4 8l-1 3 2 1v0l-2 1 1 3 2-.5 1.5 1.5L7 19l3 1 1-2h2l1 2 3-1-.5-2 1.5-1.5 2 .5 1-3z",
                "account" => "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0",
                _ => "M5 5h14v14H5z"
            };
            return "<svg class=\"" + p + "-ico\" viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"" + d + "\"/></svg>";
        }

        public static string Who(string p, Ctx c)
        {
            return Ui.Avatar(p, c.U.StaffId, c.U.StaffName, c.U.Photo) + "<span class=\"" + p + "-who-t\"><b>" + Ui.Enc(c.U.StaffName) + "</b><small>" + Ui.Enc(c.U.Title != string.Empty ? c.U.Title : c.U.RoleName) + "</small></span>";
        }

        public static string DemoPill(string p)
        {
            return App.Demo ? "<span class=\"" + p + "-dlong\">Demo data &middot; shared with other visitors</span><span class=\"" + p + "-dshort\">Demo</span>" : string.Empty;
        }
    }
}
