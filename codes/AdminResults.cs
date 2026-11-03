using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminResults : WebPage
    {
        private const string Key = "results";
        private const string LineSql = "SELECT O.LINE_ID AS LineId, O.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.STATUS AS VisitStatus, V.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, P.PET_ID AS PetId, P.PET_NAME AS PetName, P.PHOTO AS Photo, P.SPECIES_KEY AS SpeciesKey, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.ALERT AS Alert, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, O.ITEM_ID AS ItemId, O.ITEM_NAME AS ItemName, O.ITEM_TYPE AS ItemType, O.STATUS AS Status, ISNULL(OB.STAFF_NAME, N'') AS OrderedName, O.ORDERED_UTC AS OrderedUtc " +
            "FROM ORDERLINE O JOIN VISIT V ON V.VISIT_ID = O.VISIT_ID JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN STAFF OB ON OB.STAFF_ID = O.ORDERED_BY ";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Pending results | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--rs-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("rs"))
                .Replace("{plhd_open}", string.Empty);
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private static SqlParameter Dec(string name, decimal? value)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = 12, Scale = 3, Value = value.HasValue ? value.Value : DBNull.Value };
        }

        private static string Num3(decimal v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminResultsJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminResultsJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"rs-panel rs-deny\"><h1>You don't have access to this page</h1><p>Pending results are for the clinical team. Ask a manager to open them for you in Staff and permissions.</p><a class=\"rs-btn rs-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminResultsJs.out();");
            return response;
        }

        private static string VisitChip(string st)
        {
            string t = st switch
            {
                "waiting" => "Waiting",
                "inroom" => "In room",
                "checkout" => "Ready for checkout",
                "closed" => "Closed",
                "cancelled" => "Cancelled",
                _ => st
            };
            return "<span class=\"rs-st rs-st-" + Ui.Attr(st) + "\">" + Ui.Enc(t) + "</span>";
        }

        private static async Task<int> Count(string sql, List<SqlParameter> ps)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, ps);
            return emsg == string.Empty && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        private static async Task<string> CountsHtml(Ctx c)
        {
            DateTime since = DateTime.UtcNow.AddDays(-7);
            List<SqlParameter> Ps() => new List<SqlParameter> { App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50), App.PDate("since", since) };
            int pend = await Count("SELECT COUNT(*) FROM ORDERLINE O JOIN VISIT V ON V.VISIT_ID = O.VISIT_ID WHERE O.ITEM_TYPE = N'test' AND O.STATUS = N'ordered' AND V.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid)", Ps());
            int flagged = await Count("SELECT COUNT(DISTINCT R.LINE_ID) FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID JOIN VISIT V ON V.VISIT_ID = R.VISIT_ID WHERE R.ENTERED_UTC >= @since AND R.FLAG <> N'' AND O.STATUS <> N'cancelled' AND V.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid)", Ps());
            int done = await Count("SELECT COUNT(DISTINCT R.LINE_ID) FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID JOIN VISIT V ON V.VISIT_ID = R.VISIT_ID WHERE R.ENTERED_UTC >= @since AND O.STATUS <> N'cancelled' AND V.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid)", Ps());
            return "<div class=\"rs-tile" + (pend > 0 ? " rs-hot" : string.Empty) + "\"><small>Waiting for results</small><b>" + pend.ToString("#,##0", CultureInfo.InvariantCulture) + "</b><span>" + (c.U.OwnOnly ? "Your patients" : "All patients") + "</span></div>" +
                "<div class=\"rs-tile" + (flagged > 0 ? " rs-warn2" : string.Empty) + "\"><small>Flagged this week</small><b>" + flagged.ToString("#,##0", CultureInfo.InvariantCulture) + "</b><span>Tests with a value outside the normal range</span></div>" +
                "<div class=\"rs-tile\"><small>Entered this week</small><b>" + done.ToString("#,##0", CultureInfo.InvariantCulture) + "</b><span>Tests with results in the last 7 days</span></div>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("rs-menu", Auth.Menu("rs", "AdminResultsJs", "AdminResults", c));
            response.SetElementContents("rs-who", Auth.Who("rs", c));
            response.ExecuteScript("AdminResultsJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rs-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            var (names, _) = await Helper.GetDataTable("SELECT ITEM_ID, ITEM_NAME FROM ITEM WHERE ITEM_TYPE = N'test' ORDER BY ITEM_ORDER, ITEM_NAME");
            List<(string, string)> testOpts = new List<(string, string)>();
            foreach (DataRow r in names.Rows)
            {
                testOpts.Add((Convert.ToString(r["ITEM_ID"]) ?? string.Empty, Convert.ToString(r["ITEM_NAME"]) ?? string.Empty));
            }
            List<StaffPick> staff = await Flow.Providers();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rs-ptop\"><div><h1>Pending results</h1><p>" + (c.U.OwnOnly ? "Tests for your patients that are waiting for results, and what came back this week." : "Tests waiting for results, and what came back this week.") + "</p></div></div>");
            sb.Append("<div class=\"rs-tiles\" id=\"rs-counts\">" + await CountsHtml(c) + "</div>");
            sb.Append("<section class=\"rs-panel\"><div class=\"rs-shead\"><h2>Waiting for results</h2></div><div class=\"rs-bar\"><label class=\"rs-field\"><span>Test</span><select id=\"rs-ftest\" onchange=\"AdminResultsJs.filter()\"><option value=\"\">All tests</option>" + Ui.Opts(testOpts, string.Empty) + "</select></label>");
            if (!c.U.OwnOnly)
            {
                sb.Append("<label class=\"rs-field\"><span>Vet or groomer</span><select id=\"rs-fstaff\" onchange=\"AdminResultsJs.filter()\"><option value=\"\">Anyone</option>" + Ui.Opts(staff.Select(x => (x.StaffId, x.StaffName)), string.Empty) + "</select></label>");
            }
            sb.Append("</div><div id=\"rs-pend\">" + await PendingHtml(c, s, string.Empty, string.Empty) + "</div></section>");
            sb.Append("<section class=\"rs-panel\"><div class=\"rs-shead\"><h2>Entered in the last 7 days</h2><label class=\"rs-check\"><input type=\"checkbox\" id=\"rs-fflag\" onchange=\"AdminResultsJs.recent()\"><span>Only tests with flagged values</span></label></div><div id=\"rs-recent\">" + await RecentHtml(c, false) + "</div></section>");
            response.SetElementContents("rs-main", sb.ToString());
            response.ExecuteScript("AdminResultsJs.ready();");
            return response;
        }

        private static async Task<string> PendingHtml(Ctx c, Dictionary<string, string> s, string test, string sf)
        {
            var (list, emsg) = await Helper.GetListT<RsLine>(LineSql + "WHERE O.ITEM_TYPE = N'test' AND O.STATUS = N'ordered' AND V.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid) AND (@it = N'' OR O.ITEM_ID = @it) AND (@sf = N'' OR V.STAFF_ID = @sf) ORDER BY O.ORDERED_UTC DESC", new List<SqlParameter> { App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50), App.P("it", test, 50), App.P("sf", sf, 50) });
            if (emsg != string.Empty || list == null)
            {
                return "<p class=\"rs-empty\">The list could not be loaded. Try again.</p>";
            }
            if (list.Count == 0)
            {
                return "<p class=\"rs-empty\">" + (test != string.Empty || sf != string.Empty ? "No tests match these filters." : "Nothing is waiting. All results are in.") + "</p>";
            }
            DateTime today = App.Now(s).Date;
            DateTime cut = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(today, DateTimeKind.Unspecified), App.Zone(App.S(s, "timezone")));
            StringBuilder sb = new StringBuilder();
            foreach ((string title, List<RsLine> rows) in new[] { ("Ordered today", list.Where(x => x.OrderedUtc >= cut).ToList()), ("Older", list.Where(x => x.OrderedUtc < cut).ToList()) })
            {
                if (rows.Count == 0)
                {
                    continue;
                }
                sb.Append("<h3 class=\"rs-h3\">" + Ui.Enc(title) + " <span class=\"rs-cnt\">" + rows.Count + "</span></h3><div class=\"rs-tw\"><table class=\"rs-table rs-ptable\"><thead><tr><th>Pet</th><th>Test</th><th>Visit</th><th>Ordered</th>" + (c.Can(Key, 'E') ? "<th><span class=\"rs-sr\">Action</span></th>" : string.Empty) + "</tr></thead><tbody>");
                foreach (RsLine r in rows)
                {
                    sb.Append("<tr><td class=\"rs-cname\"><div class=\"rs-pn\">" + Ui.Avatar("rs", r.PetId, r.PetName, r.Photo) + "<div><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName + " · " + r.OwnerName) + "</small></div></div></td>");
                    sb.Append("<td data-l=\"Test\"><b>" + Ui.Enc(r.ItemName) + "</b></td>");
                    string vno = c.Can("visits", 'V') ? "<a class=\"rs-link\" href=\"AdminVisit?v=" + Uri.EscapeDataString(r.VisitId) + "\">" + Ui.Enc(r.VisitNo) + "</a>" : Ui.Enc(r.VisitNo);
                    sb.Append("<td data-l=\"Visit\"><span class=\"rs-vc\"><span>" + vno + (r.StaffName != string.Empty ? "<small class=\"rs-blk\">" + Ui.Enc(r.StaffName) + "</small>" : string.Empty) + "</span>" + VisitChip(r.VisitStatus) + "</span></td>");
                    sb.Append("<td data-l=\"Ordered\"><span>" + Ui.Enc(r.OrderedName) + "<small class=\"rs-blk\">" + Ui.Time(r.OrderedUtc, "ago") + "</small></span></td>");
                    if (c.Can(Key, 'E'))
                    {
                        sb.Append("<td class=\"rs-ract\"><button type=\"button\" class=\"rs-btn rs-btn-sm rs-btn-accent\" onclick=\"AdminResultsJs.enter('" + Ui.Js(r.LineId) + "')\">Enter results</button></td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            return sb.ToString();
        }

        private static async Task<string> RecentHtml(Ctx c, bool onlyFlagged)
        {
            DateTime since = DateTime.UtcNow.AddDays(-7);
            var (list, emsg) = await Helper.GetListT<RsRecent>("SELECT TOP 50 O.LINE_ID AS LineId, O.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, P.PET_ID AS PetId, P.PET_NAME AS PetName, P.PHOTO AS Photo, ISNULL(MAX(C.FIRST_NAME + N' ' + C.LAST_NAME), N'') AS OwnerName, O.ITEM_NAME AS ItemName, MAX(R.ENTERED_UTC) AS EnteredUtc, ISNULL(MAX(S.STAFF_NAME), N'') AS EnteredName, SUM(CASE WHEN R.FLAG <> N'' THEN 1 ELSE 0 END) AS Flagged FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID JOIN VISIT V ON V.VISIT_ID = O.VISIT_ID JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = R.ENTERED_BY WHERE R.ENTERED_UTC >= @since AND O.STATUS <> N'cancelled' AND V.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid) GROUP BY O.LINE_ID, O.VISIT_ID, V.VISIT_NO, P.PET_ID, P.PET_NAME, P.PHOTO, O.ITEM_NAME HAVING (@fo = 0 OR SUM(CASE WHEN R.FLAG <> N'' THEN 1 ELSE 0 END) > 0) ORDER BY MAX(R.ENTERED_UTC) DESC", new List<SqlParameter> { App.PDate("since", since), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50), App.PBit("fo", onlyFlagged) });
            if (emsg != string.Empty || list == null)
            {
                return "<p class=\"rs-empty\">The list could not be loaded. Try again.</p>";
            }
            if (list.Count == 0)
            {
                return "<p class=\"rs-empty\">" + (onlyFlagged ? "No flagged results in the last 7 days." : "No results were entered in the last 7 days.") + "</p>";
            }
            var (flags, _) = await Helper.GetListT<RsFlag>("SELECT R.LINE_ID AS LineId, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.FLAG AS Flag FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID JOIN VISIT V ON V.VISIT_ID = R.VISIT_ID LEFT JOIN TESTFIELD T ON T.FIELD_ID = R.FIELD_ID WHERE R.ENTERED_UTC >= @since AND R.FLAG <> N'' AND O.STATUS <> N'cancelled' AND (@own = 0 OR V.STAFF_ID = @sid) ORDER BY ISNULL(T.FIELD_ORDER, 999), R.FIELD_NAME", new List<SqlParameter> { App.PDate("since", since), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) });
            ILookup<string, RsFlag> byLine = (flags ?? new List<RsFlag>()).ToLookup(x => x.LineId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rs-tw\"><table class=\"rs-table\"><thead><tr><th>Pet</th><th>Test</th><th>Flagged values</th><th>Entered</th><th>Visit</th></tr></thead><tbody>");
            foreach (RsRecent r in list)
            {
                sb.Append("<tr" + (r.Flagged > 0 ? " class=\"rs-rflag\"" : string.Empty) + "><td class=\"rs-cname\"><div class=\"rs-pn\">" + Ui.Avatar("rs", r.PetId, r.PetName, r.Photo) + "<div><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.OwnerName) + "</small></div></div></td>");
                sb.Append("<td data-l=\"Test\"><b>" + Ui.Enc(r.ItemName) + "</b></td><td data-l=\"Flagged\">");
                List<RsFlag> fl = byLine[r.LineId].ToList();
                if (fl.Count == 0)
                {
                    sb.Append("<span class=\"rs-okv\">All normal</span>");
                }
                else
                {
                    sb.Append("<span class=\"rs-fvals\">");
                    foreach (RsFlag f in fl)
                    {
                        string v = f.ValueNum.HasValue ? Num3(f.ValueNum.Value) : f.ValueText;
                        sb.Append("<span class=\"rs-fv " + (f.Flag == "H" ? "rs-hi" : "rs-lo") + "\">" + Ui.Enc(f.FieldName) + " <b>" + Ui.Enc(v) + "</b>" + (f.Unit != string.Empty ? " " + Ui.Enc(f.Unit) : string.Empty) + " <i>" + (f.Flag == "H" ? "High" : "Low") + "</i></span>");
                    }
                    sb.Append("</span>");
                }
                sb.Append("</td><td data-l=\"Entered\"><span>" + Ui.Enc(r.EnteredName) + "<small class=\"rs-blk\">" + Ui.Time(r.EnteredUtc, "ago") + "</small></span></td>");
                sb.Append("<td data-l=\"Visit\">" + (c.Can("visits", 'V') ? "<a class=\"rs-link\" href=\"AdminVisit?v=" + Uri.EscapeDataString(r.VisitId) + "\">" + Ui.Enc(r.VisitNo) + "</a>" : Ui.Enc(r.VisitNo)) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
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
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rs-main", Denied());
                return response;
            }
            string test = CleanId(Val("ftest", 50));
            string sf = c.U.OwnOnly ? string.Empty : CleanId(Val("fstaff", 50));
            response.SetElementContents("rs-pend", await PendingHtml(c, await App.Settings(), test, sf));
            response.ExecuteScript("AdminResultsJs.ready();");
            return response;
        }

        public async Task<ApiResponse> Recent()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rs-main", Denied());
                return response;
            }
            response.SetElementContents("rs-recent", await RecentHtml(c, Val("fflag", 2) == "1"));
            response.ExecuteScript("AdminResultsJs.ready();");
            return response;
        }

        private static async Task<RsLine?> OneLine(string lineId)
        {
            if (lineId == string.Empty)
            {
                return null;
            }
            var (row, emsg) = await Helper.GetOneT<RsLine>(LineSql + "WHERE O.LINE_ID = @lid AND O.ITEM_TYPE = N'test'", new List<SqlParameter> { App.P("lid", lineId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static bool Open(string st)
        {
            return st == "waiting" || st == "inroom" || st == "checkout";
        }

        private static bool CanEnter(Ctx c, RsLine l)
        {
            return c.Can(Key, 'E') && l.ItemType == "test" && l.Status != "cancelled" && l.VisitStatus != "cancelled" && (Open(l.VisitStatus) || l.Status == "ordered");
        }

        private static async Task<List<RsField>> Fields(string itemId, string species)
        {
            var (list, emsg) = await Helper.GetListT<RsField>("SELECT FIELD_ID AS FieldId, FIELD_NAME AS FieldName, UNIT AS Unit, SPECIES_KEY AS SpeciesKey, LOW_VALUE AS LowValue, HIGH_VALUE AS HighValue, DECIMALS AS Decimals, IS_TEXT AS IsText, FIELD_ORDER AS FieldOrder FROM TESTFIELD WHERE ITEM_ID = @iid AND (SPECIES_KEY = N'' OR SPECIES_KEY = @sp) ORDER BY FIELD_ORDER, FIELD_NAME", new List<SqlParameter> { App.P("iid", itemId, 50), App.P("sp", species, 20) });
            List<RsField> raw = emsg == string.Empty && list != null ? list : new List<RsField>();
            List<RsField> pick = raw.GroupBy(f => f.FieldName.Trim().ToLowerInvariant()).Select(g => g.FirstOrDefault(f => f.SpeciesKey != string.Empty) ?? g.First()).OrderBy(f => f.FieldOrder).ThenBy(f => f.FieldName).ToList();
            if (pick.Count == 0)
            {
                pick.Add(new RsField { FieldId = string.Empty, FieldName = "Result", IsText = true });
            }
            return pick;
        }

        private static string FieldKey(RsField f)
        {
            return f.FieldId != string.Empty ? f.FieldId : "n:" + f.FieldName;
        }

        private static string Range(decimal? lo, decimal? hi)
        {
            if (lo.HasValue && hi.HasValue)
            {
                return Num3(lo.Value) + "&ndash;" + Num3(hi.Value);
            }
            if (lo.HasValue)
            {
                return "at least " + Num3(lo.Value);
            }
            if (hi.HasValue)
            {
                return "up to " + Num3(hi.Value);
            }
            return string.Empty;
        }

        private static async Task<string> FormHtml(RsLine l, string err)
        {
            List<RsField> fields = await Fields(l.ItemId, l.SpeciesKey);
            var (have, _) = await Helper.GetListT<RsResult>("SELECT FIELD_ID AS FieldId, FIELD_NAME AS FieldName, VALUE_NUM AS ValueNum, VALUE_TEXT AS ValueText, UNIT AS Unit, FLAG AS Flag, ENTERED_UTC AS EnteredUtc FROM RESULT WHERE LINE_ID = @lid", new List<SqlParameter> { App.P("lid", l.LineId, 50) });
            have ??= new List<RsResult>();
            var (prevList, _) = await Helper.GetListT<RsResult>("SELECT TOP 300 R.FIELD_ID AS FieldId, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.FLAG AS Flag, R.ENTERED_UTC AS EnteredUtc FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID WHERE R.PET_ID = @pid AND O.ITEM_ID = @iid AND R.LINE_ID <> @lid AND O.STATUS <> N'cancelled' AND O.ORDERED_UTC < @at ORDER BY R.ENTERED_UTC DESC", new List<SqlParameter> { App.P("pid", l.PetId, 50), App.P("iid", l.ItemId, 50), App.P("lid", l.LineId, 50), App.PDate("at", l.OrderedUtc) });
            Dictionary<string, RsResult> prev = new Dictionary<string, RsResult>();
            foreach (RsResult p in prevList ?? new List<RsResult>())
            {
                string k = p.FieldName.Trim().ToLowerInvariant();
                if (!prev.ContainsKey(k))
                {
                    prev[k] = p;
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + Ui.Enc(l.ItemName) + " for " + Ui.Enc(l.PetName) + "</h2><p class=\"rs-mlead\">" + Ui.Enc(l.SpeciesName + " · " + l.OwnerName + " · visit " + l.VisitNo) + " &middot; ordered by " + Ui.Enc(l.OrderedName) + " " + Ui.Time(l.OrderedUtc, "ago") + "</p>");
            if (l.Alert.Trim() != string.Empty)
            {
                sb.Append("<div class=\"rs-alert\"><b>Alert</b><span>" + Ui.Enc(l.Alert) + "</span></div>");
            }
            sb.Append("<div class=\"rs-rform\" id=\"rs-rbox\">");
            int i = 0;
            foreach (RsField f in fields)
            {
                RsResult? h = have.FirstOrDefault(r => (f.FieldId != string.Empty && r.FieldId == f.FieldId) || r.FieldName.Equals(f.FieldName, StringComparison.OrdinalIgnoreCase));
                prev.TryGetValue(f.FieldName.Trim().ToLowerInvariant(), out RsResult? pv);
                string iid = "rs-ri-" + i;
                string rng = Range(f.LowValue, f.HighValue);
                sb.Append("<div class=\"rs-rrow\"><label for=\"" + iid + "\"><b>" + Ui.Enc(f.FieldName) + "</b>" + (f.Unit != string.Empty ? "<small>" + Ui.Enc(f.Unit) + "</small>" : string.Empty) + "</label>");
                if (f.IsText)
                {
                    sb.Append("<input type=\"text\" class=\"rs-rin rs-rtext\" id=\"" + iid + "\" data-k=\"" + Ui.Attr(FieldKey(f)) + "\" maxlength=\"400\" value=\"" + Ui.Attr(h?.ValueText) + "\">");
                }
                else
                {
                    string fl = h != null ? h.Flag : string.Empty;
                    sb.Append("<input type=\"text\" inputmode=\"decimal\" class=\"rs-rin" + (fl == "H" ? " rs-fh" : fl == "L" ? " rs-fl" : string.Empty) + "\" id=\"" + iid + "\" data-k=\"" + Ui.Attr(FieldKey(f)) + "\" data-lo=\"" + (f.LowValue.HasValue ? Num3(f.LowValue.Value) : string.Empty) + "\" data-hi=\"" + (f.HighValue.HasValue ? Num3(f.HighValue.Value) : string.Empty) + "\" maxlength=\"14\" value=\"" + (h?.ValueNum != null ? Num3(h.ValueNum.Value) : string.Empty) + "\" oninput=\"AdminResultsJs.flag(this)\">");
                }
                string hint = string.Empty;
                if (pv != null)
                {
                    string pval = pv.ValueNum.HasValue ? Num3(pv.ValueNum.Value) + (pv.Unit != string.Empty ? " " + pv.Unit : string.Empty) + (pv.Flag != string.Empty ? " (" + (pv.Flag == "H" ? "high" : "low") + ")" : string.Empty) : pv.ValueText;
                    hint = "<span class=\"rs-rprev\">Last time " + Ui.Enc(Ui.Clip(pval, 60)) + " &middot; " + Ui.Time(pv.EnteredUtc, "day") + "</span>";
                }
                sb.Append("<span class=\"rs-rmeta\">" + (rng != string.Empty ? "<span>Normal " + rng + "</span>" : string.Empty) + hint + "</span></div>");
                i++;
            }
            sb.Append("</div><em class=\"rs-err rs-merr\" id=\"rs-e-res\">" + Ui.Enc(err) + "</em><div class=\"rs-mfoot\"><button type=\"button\" class=\"rs-btn rs-btn-ghost\" onclick=\"AdminResultsJs.closeModal()\">Cancel</button><button type=\"button\" class=\"rs-btn rs-btn-accent\" id=\"rs-rsave\" onclick=\"AdminResultsJs.save('" + Ui.Js(l.LineId) + "')\">Save results</button></div>");
            return sb.ToString();
        }

        private async Task<(Ctx?, RsLine?)> Reach(ApiResponse response, string action)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null);
            }
            if (!c.Can(Key, 'V'))
            {
                response.ExecuteScript("AdminResultsJs.closeModal();");
                response.SetElementContents("rs-main", Denied());
                return (null, null);
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, action, "results · no right", false);
                Toast(response, "You can look at results but not enter them.");
                return (null, null);
            }
            RsLine? l = await OneLine(CleanId(Val("lineid", 50)));
            if (l == null)
            {
                Toast(response, "This test was not found.");
                return (null, null);
            }
            if (!c.Mine(l.StaffId))
            {
                await App.Log(c.U.StaffId, action, l.PetName + " · " + l.ItemName + " · not one of yours", false);
                Toast(response, "Not one of yours. You can only enter results for your own visits.");
                return (null, null);
            }
            return (c, l);
        }

        private static async Task Redraw(ApiResponse response, Ctx c, string test, string sf, bool onlyFlagged)
        {
            response.SetElementContents("rs-counts", await CountsHtml(c));
            response.SetElementContents("rs-pend", await PendingHtml(c, await App.Settings(), test, sf));
            response.SetElementContents("rs-recent", await RecentHtml(c, onlyFlagged));
            response.ExecuteScript("AdminResultsJs.ready();");
        }

        public async Task<ApiResponse> ResultForm()
        {
            ApiResponse response = new ApiResponse();
            var (c, l) = await Reach(response, "results");
            if (c == null || l == null)
            {
                return response;
            }
            if (!CanEnter(c, l))
            {
                await Redraw(response, c, CleanId(Val("ftest", 50)), c.U.OwnOnly ? string.Empty : CleanId(Val("fstaff", 50)), Val("fflag", 2) == "1");
                Toast(response, "Results for this test can't be entered any more.");
                return response;
            }
            response.SetElementContents("rs-mbody", await FormHtml(l, string.Empty));
            response.ExecuteScript("AdminResultsJs.shown();");
            return response;
        }

        public async Task<ApiResponse> ResultSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminResultsJs.unlock();");
            var (c, l) = await Reach(response, "enter results");
            if (c == null || l == null)
            {
                return response;
            }
            string test = CleanId(Val("ftest", 50));
            string sf = c.U.OwnOnly ? string.Empty : CleanId(Val("fstaff", 50));
            if (!CanEnter(c, l))
            {
                response.ExecuteScript("AdminResultsJs.closeModal();");
                await Redraw(response, c, test, sf, Val("fflag", 2) == "1");
                Toast(response, "Results for this test can't be entered any more.");
                return response;
            }
            List<RsRv> vals;
            try
            {
                vals = JsonSerializer.Deserialize<List<RsRv>>(Val("rvals", 20000), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<RsRv>();
            }
            catch (JsonException)
            {
                vals = new List<RsRv>();
            }
            List<RsField> fields = await Fields(l.ItemId, l.SpeciesKey);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", l.VisitId, 50), App.P("lid", l.LineId, 50), App.P("pid", l.PetId, 50), App.P("me", c.U.StaffId, 50), App.P("vst", l.VisitStatus, 20) };
            List<string> sql = new List<string>
            {
                "UPDATE VISIT SET SYSDTE = SYSDTE WHERE VISIT_ID = @vid",
                "UPDATE ORDERLINE SET STATUS = N'done', DONE_BY = @me, DONE_UTC = SYSUTCDATETIME(), SYSDTE = GETDATE() WHERE LINE_ID = @lid AND VISIT_ID = @vid AND ITEM_TYPE = N'test' AND STATUS " + (Open(l.VisitStatus) ? "IN (N'ordered', N'done')" : "= N'ordered'") + " AND EXISTS (SELECT 1 FROM VISIT WHERE VISIT_ID = @vid AND STATUS = @vst); IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;",
                "DELETE FROM RESULT WHERE LINE_ID = @lid"
            };
            string err = string.Empty;
            int n = 0;
            int flagged = 0;
            foreach (RsField f in fields)
            {
                RsRv? rv = vals.FirstOrDefault(x => x.K == FieldKey(f));
                string raw = (rv?.V ?? string.Empty).Trim();
                if (raw == string.Empty)
                {
                    continue;
                }
                decimal? num = null;
                string text = string.Empty;
                string flag = string.Empty;
                if (f.IsText)
                {
                    text = Ui.OneLine(raw, 400);
                }
                else
                {
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal x) || Math.Abs(x) >= 1000000000m)
                    {
                        err = "Use a number for " + f.FieldName + ", for example 12.5.";
                        break;
                    }
                    x = Math.Round(x, Math.Clamp(f.Decimals, 0, 3), MidpointRounding.AwayFromZero);
                    num = x;
                    flag = f.LowValue.HasValue && x < f.LowValue.Value ? "L" : f.HighValue.HasValue && x > f.HighValue.Value ? "H" : string.Empty;
                    if (flag != string.Empty)
                    {
                        flagged++;
                    }
                }
                ps.Add(App.P("r" + n, App.NewId(), 50));
                ps.Add(App.P("f" + n, f.FieldId, 50));
                ps.Add(App.P("n" + n, Ui.Clip(f.FieldName, 60), 60));
                ps.Add(Dec("x" + n, num));
                ps.Add(App.P("t" + n, text, 400));
                ps.Add(App.P("u" + n, Ui.Clip(f.Unit, 20), 20));
                ps.Add(Dec("lo" + n, f.LowValue));
                ps.Add(Dec("hi" + n, f.HighValue));
                ps.Add(App.P("g" + n, flag, 2));
                sql.Add("INSERT INTO RESULT (RESULT_ID, LINE_ID, VISIT_ID, PET_ID, FIELD_ID, FIELD_NAME, VALUE_NUM, VALUE_TEXT, UNIT, LOW_VALUE, HIGH_VALUE, FLAG, ENTERED_BY, ENTERED_UTC) VALUES (@r" + n + ", @lid, @vid, @pid, @f" + n + ", @n" + n + ", @x" + n + ", @t" + n + ", @u" + n + ", @lo" + n + ", @hi" + n + ", @g" + n + ", @me, SYSUTCDATETIME())");
                n++;
            }
            if (err == string.Empty && n == 0)
            {
                err = "Enter at least one result.";
            }
            if (err != string.Empty)
            {
                response.SetElementContents("rs-e-res", Ui.Enc(err));
                return response;
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                if (result.Contains(Flow.Moved, StringComparison.OrdinalIgnoreCase))
                {
                    response.ExecuteScript("AdminResultsJs.closeModal();");
                    await Redraw(response, c, test, sf, Val("fflag", 2) == "1");
                    Toast(response, "Someone else just changed this test. The list is refreshed.");
                    return response;
                }
                response.SetElementContents("rs-e-res", "This could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "enter results", l.PetName + " · " + l.ItemName + (flagged > 0 ? " · " + flagged + " flagged" : string.Empty), true);
            response.ExecuteScript("AdminResultsJs.closeModal();");
            await Redraw(response, c, test, sf, Val("fflag", 2) == "1");
            Toast(response, "Results saved for " + l.PetName + "'s " + l.ItemName + "." + (flagged > 0 ? " " + Ui.Plural(flagged, "value is", "values are") + " outside the normal range." : string.Empty));
            return response;
        }
    }
}
