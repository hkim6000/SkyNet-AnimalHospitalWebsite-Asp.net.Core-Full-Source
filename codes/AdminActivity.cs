using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminActivity : WebPage
    {
        private const string Key = "activity";
        private const string Me = "AdminActivityJs";
        private const int Size = 50;
        private const int Cap = 5000;
        private const string Cols = "A.ACT_ID AS ActId, A.ACT_UTC AS ActUtc, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.ACTION AS Action, A.TARGET AS Target, A.OK AS Ok, A.IP AS Ip";
        private const string From = " FROM ACTIVITY A LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Activity log | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--al-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("al"))
                .Replace("{plhd_qs}", Ui.Attr(CleanId(QueryValue("s"))));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return t == "-" || Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
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

        private static string Denied()
        {
            return "<div class=\"al-panel al-deny\"><h1>Activity log</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"al-btn al-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private sealed class Filter
        {
            public string StaffId = string.Empty;
            public DateTime? From;
            public DateTime? To;
            public string Term = string.Empty;
            public bool Failed;
            public int Page = 1;
        }

        private Filter Read()
        {
            Filter f = new Filter
            {
                StaffId = CleanId(GetDataValue("who")),
                From = Ui.ParseDay(GetDataValue("dfrom")),
                To = Ui.ParseDay(GetDataValue("dto")),
                Term = Ui.OneLine(GetDataValue("term"), 60),
                Failed = (GetDataValue("failed") ?? string.Empty).Trim() == "1",
                Page = int.TryParse(GetDataValue("pg"), out int p) ? Math.Max(1, p) : 1
            };
            if (f.From.HasValue && f.To.HasValue && f.To < f.From)
            {
                (f.From, f.To) = (f.To, f.From);
            }
            return f;
        }

        private static (string Where, Func<List<SqlParameter>> Ps) Where(Filter f, Dictionary<string, string> s)
        {
            TimeZoneInfo z = App.Zone(App.S(s, "timezone"));
            DateTime? fu = f.From.HasValue ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(f.From.Value.Date, DateTimeKind.Unspecified), z) : null;
            DateTime? tu = f.To.HasValue ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(f.To.Value.Date.AddDays(1), DateTimeKind.Unspecified), z) : null;
            string like = f.Term == string.Empty ? string.Empty : "%" + f.Term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
            string where = " WHERE (@who = N'' OR (@who = N'-' AND A.STAFF_ID = N'') OR A.STAFF_ID = @who) AND (@fu IS NULL OR A.ACT_UTC >= @fu) AND (@tu IS NULL OR A.ACT_UTC < @tu) AND (@q = N'' OR A.ACTION LIKE @q ESCAPE N'\\' OR A.TARGET LIKE @q ESCAPE N'\\') AND (@bad = 0 OR A.OK = 0)";
            List<SqlParameter> Ps() => new List<SqlParameter> { App.P("who", f.StaffId, 50), App.PDate("fu", fu), App.PDate("tu", tu), App.P("q", like, 200), App.PBit("bad", f.Failed) };
            return (where, Ps);
        }

        private static string Who(AlRow r)
        {
            if (r.StaffId == string.Empty)
            {
                return "Website";
            }
            return r.StaffName != string.Empty ? r.StaffName : "Removed staff";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("al-menu", Auth.Menu("al", Me, "AdminActivity", c));
            response.SetElementContents("al-who", Auth.Who("al", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("al-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Filter f = new Filter { StaffId = CleanId(GetDataValue("openstaff")) };
            var (people, perr) = await Helper.GetListT<StaffPick>("SELECT STAFF_ID AS StaffId, STAFF_NAME + CASE WHEN ACTIVE = 0 THEN N' (inactive)' ELSE N'' END AS StaffName, PROVIDER_TYPE AS ProviderType, N'' AS HomeRoomId, COLOR AS Color, PHOTO AS Photo, OWN_ONLY AS OwnOnly FROM STAFF ORDER BY ACTIVE DESC, STAFF_NAME");
            List<StaffPick> staff = perr == string.Empty && people != null ? people : new List<StaffPick>();
            if (f.StaffId != string.Empty && f.StaffId != "-" && !staff.Any(x => x.StaffId == f.StaffId))
            {
                f.StaffId = string.Empty;
            }
            var (acts, aerr) = await Helper.GetListT<StaffPick>("SELECT DISTINCT ACTION AS StaffName, N'' AS StaffId, N'' AS ProviderType, N'' AS HomeRoomId, N'' AS Color, N'' AS Photo, CAST(0 AS BIT) AS OwnOnly FROM ACTIVITY ORDER BY ACTION");
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"al-ptop\"><div><h1>Activity log</h1><p>Sign-ins and important changes, newest first: who did what, to what, and from where.</p></div><div class=\"al-acts\"><button type=\"button\" class=\"al-btn al-btn-ghost\" id=\"al-csvb\" onclick=\"" + Me + ".csv(this)\">Download list (CSV)</button></div></div>");
            sb.Append("<div class=\"al-bar\"><label class=\"al-field\"><span>Who</span><select id=\"al-fwho\" onchange=\"" + Me + ".find(1)\"><option value=\"\">Everyone</option><option value=\"-\"" + (f.StaffId == "-" ? " selected" : string.Empty) + ">Website (not signed in)</option>" + Ui.Opts(staff.Select(x => (x.StaffId, x.StaffName)), f.StaffId) + "</select></label>");
            sb.Append("<label class=\"al-field al-dfield\"><span>From</span><input type=\"date\" id=\"al-dfrom\" onchange=\"" + Me + ".find(1)\"></label><label class=\"al-field al-dfield\"><span>To</span><input type=\"date\" id=\"al-dto\" onchange=\"" + Me + ".find(1)\"></label>");
            sb.Append("<label class=\"al-field al-grow2\"><span>Action or target</span><input type=\"search\" id=\"al-term\" maxlength=\"60\" list=\"al-actlist\" placeholder=\"For example: signin, permissions, Bella\" autocomplete=\"off\" oninput=\"" + Me + ".typed()\"></label>");
            sb.Append("<label class=\"al-check al-fcheck\"><input type=\"checkbox\" id=\"al-ffail\" onchange=\"" + Me + ".find(1)\"><span>Failed only</span></label></div>");
            sb.Append("<datalist id=\"al-actlist\">" + string.Concat((aerr == string.Empty && acts != null ? acts : new List<StaffPick>()).Select(a => "<option value=\"" + Ui.Attr(a.StaffName) + "\"></option>")) + "</datalist>");
            sb.Append("<div id=\"al-list\">" + await ListHtml(f, s) + "</div><div id=\"al-csvall\" hidden></div>");
            response.SetElementContents("al-main", sb.ToString());
            response.ExecuteScript(Me + ".ready();");
            return response;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("al-main", Denied());
                return response;
            }
            response.SetElementContents("al-list", await ListHtml(Read(), await App.Settings()));
            response.ExecuteScript(Me + ".ready();");
            return response;
        }

        private static async Task<string> ListHtml(Filter f, Dictionary<string, string> s)
        {
            var (where, ps) = Where(f, s);
            var (dt, cerr) = await Helper.GetDataTable("SELECT COUNT(*)" + From + where, ps());
            if (cerr != string.Empty)
            {
                return "<div class=\"al-tw\"><p class=\"al-empty\">The activity could not be loaded. Try again.</p></div>";
            }
            int total = dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            if (total == 0)
            {
                return "<div class=\"al-tw\"><p class=\"al-empty\">Nothing matches. Try other dates, a different person or fewer words.</p></div>";
            }
            int page = Math.Clamp(f.Page, 1, Math.Max(1, (total + Size - 1) / Size));
            List<SqlParameter> lp = ps();
            lp.Add(App.PInt("off", (page - 1) * Size));
            lp.Add(App.PInt("take", Size));
            var (rows, rerr) = await Helper.GetListT<AlRow>("SELECT " + Cols + From + where + " ORDER BY A.ACT_UTC DESC, A.ACT_ID DESC OFFSET @off ROWS FETCH NEXT @take ROWS ONLY", lp);
            List<AlRow> list = rerr == string.Empty && rows != null ? rows : new List<AlRow>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"al-tw\"><table class=\"al-table al-atable\"><thead><tr><th>When</th><th>Who</th><th>Action</th><th>Target</th><th>Result</th><th>IP address</th></tr></thead><tbody>");
            foreach (AlRow r in list)
            {
                sb.Append("<tr class=\"" + (r.Ok ? string.Empty : "al-bad") + "\">");
                sb.Append("<td class=\"al-cname al-nw\"><b>" + Ui.Time(r.ActUtc, "full") + "</b><small class=\"al-mob\">" + Ui.Enc(Who(r)) + " · " + Ui.Enc(r.Action) + "</small></td>");
                sb.Append("<td data-l=\"Who\" class=\"al-desk\">" + (r.StaffId == string.Empty ? "<span class=\"al-dim\">Website</span>" : Ui.Enc(Who(r))) + "</td>");
                sb.Append("<td data-l=\"Action\" class=\"al-desk\"><span class=\"al-act\">" + Ui.Enc(r.Action) + "</span></td>");
                sb.Append("<td data-l=\"Target\" class=\"al-tgt\">" + (r.Target != string.Empty ? Ui.Enc(r.Target) : "<span class=\"al-dim\">-</span>") + "</td>");
                sb.Append("<td data-l=\"Result\">" + (r.Ok ? "<span class=\"al-tag al-tag-ok\">OK</span>" : "<span class=\"al-tag al-tag-alert\">Failed</span>") + "</td>");
                sb.Append("<td data-l=\"IP address\">" + (r.Ip != string.Empty ? "<span class=\"al-ip\">" + Ui.Enc(r.Ip) + "</span>" : "<span class=\"al-dim\">-</span>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("al", page, Size, total, "entry", "entries", Me + ".find"));
            return sb.ToString();
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Filter f = Read();
            var (where, ps) = Where(f, s);
            List<SqlParameter> lp = ps();
            lp.Add(App.PInt("take", Cap));
            var (rows, rerr) = await Helper.GetListT<AlRow>("SELECT " + Cols + From + where + " ORDER BY A.ACT_UTC DESC, A.ACT_ID DESC OFFSET 0 ROWS FETCH NEXT @take ROWS ONLY", lp);
            List<AlRow> list = rerr == string.Empty && rows != null ? rows : new List<AlRow>();
            StringBuilder sb = new StringBuilder();
            foreach (AlRow r in list)
            {
                DateTime local = App.Local(s, r.ActUtc);
                string[] cells = { local.ToString("yyyy-MM-dd HH:mm:ss", Ui.Inv), Ui.Iso(r.ActUtc), Who(r), r.Action, r.Target, r.Ok ? "OK" : "Failed", r.Ip };
                sb.Append("<i data-csv=\"" + Ui.Attr(string.Join(",", cells.Select(Ui.Csv))) + "\"></i>");
            }
            response.SetElementContents("al-csvall", sb.ToString());
            response.ExecuteScript(Me + ".csvGo(" + (list.Count >= Cap ? "true" : "false") + ");");
            await App.Log(c.U.StaffId, "download activity", Ui.Plural(list.Count, "row", "rows"), true);
            return response;
        }
    }
}
