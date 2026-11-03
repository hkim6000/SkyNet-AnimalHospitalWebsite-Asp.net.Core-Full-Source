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
    public class AdminVisit : WebPage
    {
        private const string Key = "visits";
        private const int Size = 25;
        private const int ChunkBytes = 1048576;
        private const string OpenSql = "IF NOT EXISTS (SELECT 1 FROM VISIT WHERE VISIT_ID = @vid AND STATUS IN (N'waiting', N'inroom', N'checkout')) THROW 50003, N'VISITCLOSED', 1;";
        private const string LockSql = "IF EXISTS (SELECT 1 FROM VISIT WHERE VISIT_ID = @vid AND STATUS NOT IN (N'waiting', N'inroom', N'checkout')) OR EXISTS (SELECT 1 FROM INVOICE WHERE VISIT_ID = @vid AND STATUS <> N'open') THROW 50003, N'ORDERSLOCKED', 1;";
        private const string HoldSql = "UPDATE VISIT SET SYSDTE = SYSDTE WHERE VISIT_ID = @vid";
        private const string Changed = "IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";
        private static readonly string[] Kinds = { "exam", "surgery", "grooming", "progress", "phone" };
        private static readonly string[] Types = { "practice", "medicine", "test", "vaccine", "grooming", "product", "fee" };
        private static readonly string[] DxStates = { "active", "resolved", "ruleout" };
        private static readonly Dictionary<string, string> Exts = new Dictionary<string, string>
        {
            [".pdf"] = "application/pdf",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv"
        };

        private const string VisitSql = "SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.APPT_ID AS ApptId, V.CLIENT_ID AS ClientId, V.PET_ID AS PetId, V.VISIT_TYPE AS VisitType, V.STATUS AS Status, V.STAFF_ID AS StaffId, V.REASON AS Reason, V.CHECKIN_AT AS CheckinAt, V.START_AT AS StartAt, V.READY_AT AS ReadyAt, V.CLOSED_AT AS ClosedAt, V.RECHECK_DATE AS RecheckDate, V.SUMMARY AS Summary, " +
            "P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SEX AS Sex, P.FIXED AS Fixed, P.BIRTH_DATE AS BirthDate, P.SIZE AS Size, P.ALERT AS Alert, P.PHOTO AS Photo, P.DECEASED_DATE AS DeceasedDate, " +
            "ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.FIRST_NAME, N'') AS OwnerFirst, ISNULL(C.PHONE, N'') AS OwnerPhone, ISNULL(C.EMAIL, N'') AS OwnerEmail, ISNULL(C.ALERT, N'') AS OwnerAlert, ISNULL(S.STAFF_NAME, N'') AS StaffName, ISNULL(A.APPT_CODE, N'') AS ApptCode, ISNULL(R.ROOM_NAME, N'') AS RoomName, X.IN_AT AS RoomSince, " +
            "(SELECT TOP 1 W.LB FROM PETWEIGHT W WHERE W.PET_ID = V.PET_ID ORDER BY W.WEIGHED_AT DESC) AS WeightLb, (SELECT TOP 1 W.WEIGHED_AT FROM PETWEIGHT W WHERE W.PET_ID = V.PET_ID ORDER BY W.WEIGHED_AT DESC) AS WeighedAt, " +
            "CASE WHEN EXISTS (SELECT 1 FROM INVOICE I WHERE I.VISIT_ID = V.VISIT_ID AND I.STATUS <> N'open') THEN 1 ELSE 0 END AS Billed " +
            "FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID AND V.APPT_ID <> N'' LEFT JOIN VISITROOM X ON X.VISIT_ID = V.VISIT_ID AND X.OUT_AT IS NULL LEFT JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID " +
            "WHERE V.VISIT_ID = @vid AND V.VISIT_TYPE <> N'boarding'";

        private const string CardSql = "SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.STATUS AS Status, V.VISIT_TYPE AS VisitType, V.CHECKIN_AT AS CheckinAt, V.START_AT AS StartAt, V.READY_AT AS ReadyAt, V.REASON AS Reason, P.PET_ID AS PetId, P.PET_NAME AS PetName, P.PHOTO AS Photo, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.ALERT AS Alert, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(S.STAFF_NAME, N'') AS StaffName, ISNULL(R.ROOM_NAME, N'') AS RoomName, X.IN_AT AS RoomSince " +
            "FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN VISITROOM X ON X.VISIT_ID = V.VISIT_ID AND X.OUT_AT IS NULL LEFT JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID ";

        private const string LineSql = "SELECT O.LINE_ID AS LineId, O.ITEM_ID AS ItemId, O.ITEM_NAME AS ItemName, O.ITEM_TYPE AS ItemType, O.QTY AS Qty, O.UNIT AS Unit, O.UNIT_PRICE AS UnitPrice, O.DOSE AS Dose, O.INSTRUCTIONS AS Instructions, O.DISPENSE AS Dispense, O.STATUS AS Status, O.NOTE AS Note, ISNULL(S.STAFF_NAME, N'') AS OrderedName, O.ORDERED_UTC AS OrderedUtc, ISNULL(D.STAFF_NAME, N'') AS DoneName, O.DONE_UTC AS DoneUtc, O.LINE_ORDER AS LineOrder, (SELECT COUNT(*) FROM RESULT R WHERE R.LINE_ID = O.LINE_ID) AS ResultCount FROM ORDERLINE O LEFT JOIN STAFF S ON S.STAFF_ID = O.ORDERED_BY LEFT JOIN STAFF D ON D.STAFF_ID = O.DONE_BY WHERE O.VISIT_ID = @vid ORDER BY O.LINE_ORDER, O.ORDERED_UTC";

        private const string ItemCols = "ITEM_ID AS ItemId, ITEM_CODE AS ItemCode, ITEM_NAME AS ItemName, ITEM_TYPE AS ItemType, CATEGORY AS Category, UNIT AS Unit, PRICE AS Price, PRICE_M AS PriceM, PRICE_L AS PriceL, TAXABLE AS Taxable, DOSE AS Dose, INSTRUCTIONS AS Instructions, VAX_MONTHS AS VaxMonths, SPECIES AS Species, NOTE AS Note";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Visits | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--vs-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("vs"))
                .Replace("{plhd_open}", Ui.Attr(CleanId(QueryValue("v"))));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private static string Safe(string id)
        {
            return Regex.Replace(id ?? string.Empty, "[^A-Za-z0-9_-]", string.Empty);
        }

        private string NewKey()
        {
            string k = Val("newid", 40);
            return Regex.IsMatch(k, "^[a-f0-9]{32}$") ? k : string.Empty;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (path != string.Empty && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private string Text(string key, int max)
        {
            string t = (GetDataValue(key) ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }

        private static SqlParameter Dec(string name, decimal? value, int scale = 2)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = 12, Scale = (byte)scale, Value = value.HasValue ? value.Value : DBNull.Value };
        }

        private static SqlParameter IntN(string name, int? value)
        {
            return new SqlParameter(name, SqlDbType.Int) { Value = value.HasValue ? value.Value : DBNull.Value };
        }

        private static bool TryNum(string v, out decimal d)
        {
            return decimal.TryParse((v ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out d);
        }

        private static string Like(string term)
        {
            return term == string.Empty ? string.Empty : "%" + term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminVisitJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("vs-menu", Auth.Menu("vs", "AdminVisitJs", "AdminVisit", c));
            response.SetElementContents("vs-who", Auth.Who("vs", c));
            response.ExecuteScript("AdminVisitJs.secs();");
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminVisitJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"vs-panel vs-deny\"><h1>You don't have access to this page</h1><p>Visits are for the clinical team. Ask a manager to open them for you in Staff and permissions.</p><a class=\"vs-btn vs-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static string Missing(string title, string text)
        {
            return "<div class=\"vs-panel vs-deny\"><h1>" + Ui.Enc(title) + "</h1><p>" + Ui.Enc(text) + "</p><a class=\"vs-btn vs-btn-ghost\" href=\"AdminVisit\">Back to visits</a></div>";
        }

        private static string Why(string r)
        {
            if (r.Contains("ORDERSLOCKED", StringComparison.OrdinalIgnoreCase))
            {
                return "Orders are locked for this visit. It is closed or the invoice was already sent.";
            }
            if (r.Contains("VISITCLOSED", StringComparison.OrdinalIgnoreCase))
            {
                return "This visit is closed. Only addenda and signing are possible now.";
            }
            if (r.Contains(Flow.Moved, StringComparison.OrdinalIgnoreCase))
            {
                return "Someone else just changed this. The page is refreshed.";
            }
            return "This could not be saved. Try again.";
        }

        private static bool IsOpen(VsVisit v)
        {
            return v.Status == "waiting" || v.Status == "inroom" || v.Status == "checkout";
        }

        private static bool OrdersLocked(VsVisit v)
        {
            return !IsOpen(v) || v.Billed == 1;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminVisitJs.out();");
            return response;
        }

        private static async Task<VsVisit?> One(string vid)
        {
            if (vid == string.Empty)
            {
                return null;
            }
            var (row, emsg) = await Helper.GetOneT<VsVisit>(VisitSql, new List<SqlParameter> { App.P("vid", vid, 50) });
            return emsg == string.Empty ? row : null;
        }

        private async Task<VsVisit?> Reach(ApiResponse response, Ctx c, char right, string action)
        {
            if (!c.Can(Key, 'V'))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                response.SetElementContents("vs-main", Denied());
                return null;
            }
            if (right != 'V' && !c.Can(Key, right))
            {
                await App.Log(c.U.StaffId, action, "visit · no right", false);
                Toast(response, right == 'X' ? "Signing needs the Sign records right." : "You can look at visits but not change them.");
                return null;
            }
            VsVisit? v = await One(CleanId(GetDataValue("visitid")));
            if (v == null)
            {
                Toast(response, "This visit was not found.");
                return null;
            }
            if (!c.Mine(v.StaffId))
            {
                await App.Log(c.U.StaffId, action, v.PetName + " · " + v.VisitNo + " · not one of yours", false);
                Toast(response, "Not one of yours. You can only work on your own visits.");
                return null;
            }
            return v;
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
                response.SetElementContents("vs-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string vid = CleanId(GetDataValue("openv"));
            if (vid != string.Empty)
            {
                VsVisit? v = await One(vid);
                if (v == null)
                {
                    response.SetElementContents("vs-main", Missing("Visit not found", "This visit does not exist or it is a boarding stay."));
                    return response;
                }
                if (!c.Mine(v.StaffId))
                {
                    await App.Log(c.U.StaffId, "open visit", v.PetName + " · " + v.VisitNo + " · not one of yours", false);
                    response.SetElementContents("vs-main", Missing("Not one of yours", "This visit belongs to another team member. You can only open your own visits."));
                    return response;
                }
                response.SetElementContents("vs-main", await Workspace(c, v, s));
                response.ExecuteScript("AdminVisitJs.drawn();");
                return response;
            }
            response.SetElementContents("vs-main", await ListPage(c, s));
            response.ExecuteScript("AdminVisitJs.ready();");
            return response;
        }

        private static string StatusChip(string st)
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
            return "<span class=\"vs-st vs-st-" + Ui.Attr(st) + "\">" + Ui.Enc(t) + "</span>";
        }

        private static string TypeName(string t)
        {
            return t switch
            {
                "clinic" => "Clinic visit",
                "surgery" => "Surgery or dental",
                "grooming" => "Grooming",
                _ => t
            };
        }

        private static string KindName(string k)
        {
            return k switch
            {
                "exam" => "Exam",
                "surgery" => "Surgery",
                "grooming" => "Grooming",
                "progress" => "Progress",
                "phone" => "Phone call",
                _ => k
            };
        }

        private static string GroupName(string t)
        {
            return t switch
            {
                "practice" => "Exams and procedures",
                "medicine" => "Medicines",
                "test" => "Tests and imaging",
                "vaccine" => "Vaccines",
                "grooming" => "Grooming",
                "product" => "Products",
                "fee" => "Fees",
                _ => "Other"
            };
        }

        private static string TypeLabel(string t)
        {
            return t switch
            {
                "practice" => "Procedure",
                "medicine" => "Medicine",
                "test" => "Test",
                "vaccine" => "Vaccine",
                "grooming" => "Grooming",
                "product" => "Product",
                "fee" => "Fee",
                _ => t
            };
        }

        private static string SexText(string sex, bool fix)
        {
            return (sex ?? string.Empty).ToLowerInvariant() switch
            {
                "m" => fix ? "Male, neutered" : "Male",
                "f" => fix ? "Female, spayed" : "Female",
                _ => fix ? "Fixed" : "Sex not known"
            };
        }

        private static string Age(DateTime? birth, DateTime today, DateTime? died)
        {
            if (birth == null)
            {
                return string.Empty;
            }
            DateTime end = died ?? today;
            DateTime b = birth.Value.Date;
            if (b > end)
            {
                return string.Empty;
            }
            int months = (end.Year - b.Year) * 12 + end.Month - b.Month - (end.Day < b.Day ? 1 : 0);
            if (months >= 24)
            {
                return (months / 12) + " yrs";
            }
            if (months >= 12)
            {
                return "1 yr" + (months % 12 > 0 ? " " + (months % 12) + " mo" : string.Empty);
            }
            if (months >= 2)
            {
                return months + " mos";
            }
            int weeks = (int)((end - b).TotalDays / 7);
            return weeks <= 1 ? "1 wk" : weeks + " wks";
        }

        private static string When(DateTime d, DateTime today)
        {
            return d.Date == today ? "Today " + Ui.Hm(d) : Ui.Day(d, d.Year == today.Year ? false : true) + " " + Ui.Hm(d);
        }

        private static string Num3(decimal v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
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

        private static async Task<List<VsCard>> OpenCards(Ctx c)
        {
            var (list, emsg) = await Helper.GetListT<VsCard>(CardSql + "WHERE V.STATUS IN (N'waiting', N'inroom', N'checkout') AND V.VISIT_TYPE <> N'boarding' AND (@own = 0 OR V.STAFF_ID = @sid) ORDER BY CASE V.STATUS WHEN N'inroom' THEN 1 WHEN N'waiting' THEN 2 ELSE 3 END, V.CHECKIN_AT", new List<SqlParameter> { App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) });
            return emsg == string.Empty && list != null ? list : new List<VsCard>();
        }

        private static string Card(VsCard r, DateTime today)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<a class=\"vs-card vs-card-" + Ui.Attr(r.Status) + "\" href=\"AdminVisit?v=" + Uri.EscapeDataString(r.VisitId) + "\"><div class=\"vs-ctop\">" + Ui.Avatar("vs", r.PetId, r.PetName, r.Photo) + "<div class=\"vs-cname\"><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName + (r.Breed != string.Empty ? " · " + r.Breed : string.Empty)) + "</small></div>" + StatusChip(r.Status) + "</div>");
            if (r.Alert.Trim() != string.Empty)
            {
                sb.Append("<p class=\"vs-calert\"><i aria-hidden=\"true\">!</i><span>" + Ui.Enc(r.Alert) + "</span></p>");
            }
            sb.Append("<p class=\"vs-cwhy\">" + Ui.Enc(r.Reason != string.Empty ? r.Reason : TypeName(r.VisitType)) + "</p><dl class=\"vs-cdl\"><dt>Owner</dt><dd>" + Ui.Enc(r.OwnerName) + "</dd><dt>With</dt><dd>" + Ui.Enc(r.StaffName != string.Empty ? r.StaffName : "Not set") + "</dd>");
            if (r.RoomName != string.Empty)
            {
                sb.Append("<dt>Room</dt><dd>" + Ui.Enc(r.RoomName) + (r.RoomSince.HasValue ? " <span class=\"vs-dim\">since " + Ui.Hm(r.RoomSince.Value) + "</span>" : string.Empty) + "</dd>");
            }
            string time = r.Status == "checkout" && r.ReadyAt.HasValue ? "Ready " + When(r.ReadyAt.Value, today) : "Checked in " + When(r.CheckinAt, today);
            sb.Append("<dt>Time</dt><dd>" + Ui.Enc(time) + "</dd></dl><span class=\"vs-cno\">" + Ui.Enc(r.VisitNo) + "</span></a>");
            return sb.ToString();
        }

        private static async Task<string> ListPage(Ctx c, Dictionary<string, string> s)
        {
            DateTime today = App.Now(s).Date;
            List<VsCard> open = await OpenCards(c);
            List<StaffPick> staff = await Flow.Providers();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-ptop\"><div><h1>Visits</h1><p>" + (c.U.OwnOnly ? "Your open visits right now, and your past visits." : "Open visits right now, and every past visit.") + "</p></div></div>");
            int w = open.Count(x => x.Status == "waiting");
            int ir = open.Count(x => x.Status == "inroom");
            int rd = open.Count(x => x.Status == "checkout");
            sb.Append("<section class=\"vs-panel\"><div class=\"vs-shead\"><h2>Open visits now</h2><p class=\"vs-sub\">" + Ui.Plural(w, "waiting", "waiting") + " &middot; " + Ui.Plural(ir, "in a room", "in rooms") + " &middot; " + Ui.Plural(rd, "ready for checkout", "ready for checkout") + "</p></div>");
            if (open.Count == 0)
            {
                sb.Append("<p class=\"vs-empty\">" + (c.U.OwnOnly ? "None of your patients are here right now." : "No patients are here right now.") + "</p>");
            }
            else
            {
                sb.Append("<div class=\"vs-cards\">");
                foreach (VsCard r in open)
                {
                    sb.Append(Card(r, today));
                }
                sb.Append("</div>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"vs-panel\"><div class=\"vs-shead\"><h2>Find a visit</h2></div><div class=\"vs-bar\">");
            sb.Append("<label class=\"vs-field vs-grow2\"><span>Search</span><input type=\"search\" id=\"vs-term\" maxlength=\"80\" placeholder=\"Pet name, owner name or visit number\" autocomplete=\"off\" oninput=\"AdminVisitJs.typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();AdminVisitJs.find(1);}\"></label>");
            sb.Append("<label class=\"vs-field vs-fdate\"><span>From</span><input type=\"date\" id=\"vs-dfrom\" value=\"" + Ui.Ymd(today.AddDays(-6)) + "\" onchange=\"AdminVisitJs.find(1)\"></label>");
            sb.Append("<label class=\"vs-field vs-fdate\"><span>To</span><input type=\"date\" id=\"vs-dto\" value=\"" + Ui.Ymd(today) + "\" onchange=\"AdminVisitJs.find(1)\"></label>");
            sb.Append("<label class=\"vs-field\"><span>Status</span><select id=\"vs-vstat\" onchange=\"AdminVisitJs.find(1)\"><option value=\"\">Any status</option>" + Ui.Opts(new[] { ("waiting", "Waiting"), ("inroom", "In room"), ("checkout", "Ready for checkout"), ("closed", "Closed"), ("cancelled", "Cancelled") }, string.Empty) + "</select></label>");
            sb.Append("<label class=\"vs-field\"><span>Type</span><select id=\"vs-vtype\" onchange=\"AdminVisitJs.find(1)\"><option value=\"\">Any type</option>" + Ui.Opts(new[] { ("clinic", "Clinic visit"), ("surgery", "Surgery or dental"), ("grooming", "Grooming") }, string.Empty) + "</select></label>");
            if (!c.U.OwnOnly)
            {
                sb.Append("<label class=\"vs-field\"><span>Seen by</span><select id=\"vs-vstaff\" onchange=\"AdminVisitJs.find(1)\"><option value=\"\">Anyone</option>" + Ui.Opts(staff.Select(x => (x.StaffId, x.StaffName)), string.Empty) + "</select></label>");
            }
            sb.Append("</div><div id=\"vs-list\">" + await ListHtml(c, s, string.Empty, today.AddDays(-6), today, string.Empty, string.Empty, string.Empty, 1) + "</div></section>");
            return sb.ToString();
        }

        private static async Task<string> ListHtml(Ctx c, Dictionary<string, string> s, string term, DateTime from, DateTime to, string st, string ty, string sf, int pg)
        {
            DateTime today = App.Now(s).Date;
            string where = "WHERE V.VISIT_TYPE <> N'boarding' AND V.CHECKIN_AT >= @d0 AND V.CHECKIN_AT < @d1 AND (@own = 0 OR V.STAFF_ID = @sid) AND (@st = N'' OR V.STATUS = @st) AND (@ty = N'' OR V.VISIT_TYPE = @ty) AND (@sf = N'' OR V.STAFF_ID = @sf) AND (@t = N'' OR P.PET_NAME LIKE @t OR C.LAST_NAME LIKE @t OR C.FIRST_NAME LIKE @t OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @t OR V.VISIT_NO LIKE @t)";
            List<SqlParameter> Ps() => new List<SqlParameter> { App.PDate("d0", from), App.PDate("d1", to.AddDays(1)), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50), App.P("st", st, 20), App.P("ty", ty, 20), App.P("sf", sf, 50), App.P("t", Like(term), 200) };
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID " + where, Ps());
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            int page = Ui.PageOf(pg.ToString(CultureInfo.InvariantCulture), Size, total);
            List<SqlParameter> ps = Ps();
            ps.Add(App.PInt("off", (page - 1) * Size));
            ps.Add(App.PInt("n", Size));
            var (rows, emsg) = await Helper.GetListT<VsCard>(CardSql + where + " ORDER BY V.CHECKIN_AT DESC, V.VISIT_NO DESC OFFSET @off ROWS FETCH NEXT @n ROWS ONLY", ps);
            if (emsg != string.Empty || rows == null)
            {
                return "<p class=\"vs-empty\">The visits could not be loaded. Try again.</p>";
            }
            if (rows.Count == 0)
            {
                return "<p class=\"vs-empty\">No visits match. Try a wider date range or clear the search.</p>";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-tw\"><table class=\"vs-table\"><thead><tr><th>Date</th><th>Pet</th><th>Visit</th><th>Reason</th><th>Seen by</th><th>Status</th></tr></thead><tbody>");
            foreach (VsCard r in rows)
            {
                string id = Ui.Js(r.VisitId);
                sb.Append("<tr class=\"vs-row\" tabindex=\"0\" onclick=\"AdminVisitJs.go('" + id + "')\" onkeydown=\"if(event.key==='Enter')AdminVisitJs.go('" + id + "')\">");
                sb.Append("<td class=\"vs-nw\" data-l=\"Date\">" + Ui.Enc(When(r.CheckinAt, today)) + "</td>");
                sb.Append("<td class=\"vs-cname\"><div class=\"vs-pn\">" + Ui.Avatar("vs", r.PetId, r.PetName, r.Photo) + "<div><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.OwnerName) + "</small></div></div></td>");
                sb.Append("<td data-l=\"Visit\"><span><span class=\"vs-nw\">" + Ui.Enc(r.VisitNo) + "</span><small class=\"vs-blk\">" + Ui.Enc(TypeName(r.VisitType)) + "</small></span></td>");
                sb.Append("<td data-l=\"Reason\">" + Ui.Enc(r.Reason) + "</td>");
                sb.Append("<td data-l=\"Seen by\">" + Ui.Enc(r.StaffName) + "</td>");
                sb.Append("<td data-l=\"Status\">" + StatusChip(r.Status) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("vs", page, Size, total, "visit", "visits", "AdminVisitJs.find"));
            return sb.ToString();
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
                response.SetElementContents("vs-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            DateTime from = Ui.ParseDay(Val("dfrom", 10)) ?? today.AddDays(-6);
            DateTime to = Ui.ParseDay(Val("dto", 10)) ?? today;
            if (from > to)
            {
                (from, to) = (to, from);
            }
            string st = Val("vstat", 20);
            st = new[] { "waiting", "inroom", "checkout", "closed", "cancelled" }.Contains(st) ? st : string.Empty;
            string ty = Val("vtype", 20);
            ty = new[] { "clinic", "surgery", "grooming" }.Contains(ty) ? ty : string.Empty;
            string sf = c.U.OwnOnly ? string.Empty : CleanId(Val("vstaff", 50));
            int.TryParse(Val("pg", 6), out int pg);
            response.SetElementContents("vs-list", await ListHtml(c, s, Ui.OneLine(Val("term", 80), 80), from, to, st, ty, sf, Math.Max(pg, 1)));
            response.ExecuteScript("AdminVisitJs.ready();");
            return response;
        }

        private static async Task<string> Workspace(Ctx c, VsVisit v, Dictionary<string, string> s)
        {
            List<VsLine> lines = await Lines(v.VisitId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-wtop\"><a class=\"vs-back\" href=\"AdminVisit\">&larr; All visits</a><nav class=\"vs-jump\" aria-label=\"Sections\"><a href=\"#vs-s-notes\">Notes</a><a href=\"#vs-s-dx\">Diagnoses</a><a href=\"#vs-s-orders\">Orders</a><a href=\"#vs-s-results\">Results</a><a href=\"#vs-s-files\">Files</a><a href=\"#vs-s-take\">Take-home</a></nav></div>");
            sb.Append("<section class=\"vs-panel vs-head\" id=\"vs-head\">" + await HeadHtml(c, v, s) + "</section>");
            sb.Append("<div class=\"vs-grid\"><section class=\"vs-panel vs-wsec\" id=\"vs-s-notes\"><div id=\"vs-notes\">" + await NotesHtml(c, v) + "</div></section>");
            sb.Append("<div class=\"vs-wside\"><section class=\"vs-panel vs-wsec\" id=\"vs-s-dx\"><div id=\"vs-dx\">" + await DxHtml(c, v) + "</div></section><section class=\"vs-panel vs-wsec\"><div id=\"vs-prev\">" + await PrevHtml(c, v, s) + "</div></section></div></div>");
            sb.Append("<section class=\"vs-panel vs-wsec\" id=\"vs-s-orders\"><div id=\"vs-orders\">" + OrdersHtml(c, v, lines) + "</div></section>");
            sb.Append("<section class=\"vs-panel vs-wsec\" id=\"vs-s-results\"><div id=\"vs-results\">" + await ResultsHtml(c, v, lines) + "</div></section>");
            sb.Append("<div class=\"vs-grid2\"><section class=\"vs-panel vs-wsec\" id=\"vs-s-files\"><div class=\"vs-shead\"><h2>Files</h2></div><div id=\"vs-fform\">" + FileFormHtml(c, v, lines, s) + "</div><div id=\"vs-flist\">" + await FileListHtml(c, v, lines) + "</div></section>");
            sb.Append("<section class=\"vs-panel vs-wsec\" id=\"vs-s-take\"><div class=\"vs-shead\"><h2>Take-home summary</h2></div><div id=\"vs-takef\">" + TakeFormHtml(c, v) + "</div><div id=\"vs-takep\">" + await TakePrevHtml(v, lines, s) + "</div></section></div>");
            return sb.ToString();
        }

        private async Task Draw(ApiResponse response, Ctx c, string vid, string parts, string msg)
        {
            Dictionary<string, string> s = await App.Settings();
            VsVisit? v = await One(vid);
            if (v == null)
            {
                response.SetElementContents("vs-main", Missing("Visit not found", "This visit does not exist any more."));
                return;
            }
            List<VsLine>? lines = null;
            foreach (string p in parts.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (p == "orders" || p == "results" || p == "takep" || p == "flist" || p == "fform")
                {
                    lines ??= await Lines(v.VisitId);
                }
                switch (p)
                {
                    case "head":
                        response.SetElementContents("vs-head", await HeadHtml(c, v, s));
                        break;
                    case "notes":
                        response.SetElementContents("vs-notes", await NotesHtml(c, v));
                        break;
                    case "dx":
                        response.SetElementContents("vs-dx", await DxHtml(c, v));
                        break;
                    case "orders":
                        response.SetElementContents("vs-orders", OrdersHtml(c, v, lines!));
                        break;
                    case "results":
                        response.SetElementContents("vs-results", await ResultsHtml(c, v, lines!));
                        break;
                    case "fform":
                        response.SetElementContents("vs-fform", FileFormHtml(c, v, lines!, s));
                        break;
                    case "flist":
                        response.SetElementContents("vs-flist", await FileListHtml(c, v, lines!));
                        break;
                    case "takef":
                        response.SetElementContents("vs-takef", TakeFormHtml(c, v));
                        break;
                    case "takep":
                        response.SetElementContents("vs-takep", await TakePrevHtml(v, lines!, s));
                        break;
                }
            }
            if (msg != string.Empty)
            {
                Toast(response, msg);
            }
            response.ExecuteScript("AdminVisitJs.drawn();");
        }

        private static async Task<string> HeadHtml(Ctx c, VsVisit v, Dictionary<string, string> s)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            List<string> bits = new List<string> { v.SpeciesName };
            if (v.Breed != string.Empty)
            {
                bits.Add(v.Breed);
            }
            bits.Add(SexText(v.Sex, v.Fixed));
            string age = Age(v.BirthDate, today, v.DeceasedDate);
            if (age != string.Empty)
            {
                bits.Add(age);
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-hrow\"><div class=\"vs-whob\">" + Ui.Avatar("vs", v.PetId, v.PetName, v.Photo, "lg") + "<div><h1>" + Ui.Enc(v.PetName) + "</h1><p class=\"vs-sub\">" + Ui.Enc(string.Join(" · ", bits)) + "</p>");
            if (v.WeightLb.HasValue)
            {
                sb.Append("<p class=\"vs-wt\"><b>" + Ui.Num(v.WeightLb.Value) + " lb</b>" + (v.WeighedAt.HasValue ? " <span class=\"vs-dim\">weighed " + Ui.Enc(v.WeighedAt.Value.Date == today ? "today" : Ui.Day(v.WeighedAt.Value)) + "</span>" : string.Empty) + "</p>");
            }
            string owner = v.OwnerName != string.Empty ? Ui.Enc(v.OwnerName) : "No owner";
            sb.Append("<p class=\"vs-owner\">Owner: " + (c.Can("clients", 'V') && v.OwnerName != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(v.ClientId) + "\">" + owner + "</a>" : owner) + (v.OwnerPhone != string.Empty ? " &middot; <a href=\"tel:" + Ui.Attr(Ui.Digits(v.OwnerPhone)) + "\">" + Ui.Enc(v.OwnerPhone) + "</a>" : string.Empty) + "</p></div></div><div class=\"vs-acts\">");
            if (c.Can("charts", 'V'))
            {
                sb.Append("<a class=\"vs-btn vs-btn-sm vs-btn-ghost\" href=\"AdminChart?p=" + Uri.EscapeDataString(v.PetId) + "\">Patient chart</a>");
            }
            if (c.Can(Key, 'E') && v.Status == "waiting")
            {
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" onclick=\"AdminVisitJs.startAsk()\">Start visit</button>");
            }
            if (c.Can(Key, 'E') && v.Status == "inroom")
            {
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.move()\">Move room</button>");
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" onclick=\"AdminVisitJs.toCheckout('" + Ui.Js(v.PetName) + "')\">Ready for checkout</button>");
            }
            if (c.Can("checkout", 'V') && v.Status == "checkout")
            {
                sb.Append("<a class=\"vs-btn vs-btn-sm vs-btn-accent\" href=\"AdminCheckout?v=" + Uri.EscapeDataString(v.VisitId) + "\">Checkout</a>");
            }
            sb.Append("</div></div>");
            if (v.Alert.Trim() != string.Empty)
            {
                sb.Append("<div class=\"vs-alert\" role=\"alert\"><b>Alert</b><span>" + Ui.Enc(v.Alert) + "</span></div>");
            }
            if (v.OwnerAlert.Trim() != string.Empty)
            {
                sb.Append("<div class=\"vs-alert vs-alert-o\"><b>Owner note</b><span>" + Ui.Enc(v.OwnerAlert) + "</span></div>");
            }
            if (v.DeceasedDate.HasValue)
            {
                sb.Append("<div class=\"vs-alert vs-alert-o\"><b>Passed away</b><span>" + Ui.Enc(Ui.Day(v.DeceasedDate.Value)) + "</span></div>");
            }
            sb.Append("<div class=\"vs-facts\">");
            sb.Append(Fact("Visit", Ui.Enc(v.VisitNo)));
            sb.Append(Fact("Type", Ui.Enc(TypeName(v.VisitType))));
            sb.Append(Fact("Status", StatusChip(v.Status)));
            if (v.RoomName != string.Empty)
            {
                sb.Append(Fact("Room", Ui.Enc(v.RoomName) + (v.RoomSince.HasValue ? " <span class=\"vs-dim\">since " + Ui.Hm(v.RoomSince.Value) + "</span>" : string.Empty)));
            }
            sb.Append(Fact(v.VisitType == "grooming" ? "Groomer" : "Seen by", Ui.Enc(v.StaffName != string.Empty ? v.StaffName : "Not set")));
            sb.Append(Fact("Checked in", Ui.Enc(When(v.CheckinAt, today))));
            if (v.StartAt.HasValue)
            {
                sb.Append(Fact("Started", Ui.Enc(When(v.StartAt.Value, today))));
            }
            if (v.ReadyAt.HasValue)
            {
                sb.Append(Fact("Ready", Ui.Enc(When(v.ReadyAt.Value, today))));
            }
            if (v.ClosedAt.HasValue)
            {
                sb.Append(Fact(v.Status == "cancelled" ? "Cancelled" : "Closed", Ui.Enc(When(v.ClosedAt.Value, today))));
            }
            if (v.ApptCode != string.Empty)
            {
                sb.Append(Fact("Appointment", Ui.Enc(v.ApptCode)));
            }
            sb.Append("</div>");
            if (v.Reason != string.Empty)
            {
                sb.Append("<p class=\"vs-why\"><b>Reason</b>" + Ui.Enc(v.Reason) + "</p>");
            }
            if (!IsOpen(v))
            {
                sb.Append("<p class=\"vs-info\">This visit is " + (v.Status == "cancelled" ? "cancelled" : "closed") + ". The record is read-only; you can still sign notes and add addenda.</p>");
            }
            return sb.ToString();
        }

        private static string Fact(string label, string html)
        {
            return "<div class=\"vs-fact\"><small>" + Ui.Enc(label) + "</small><span>" + html + "</span></div>";
        }

        private static async Task<string> PrevHtml(Ctx c, VsVisit v, Dictionary<string, string> s)
        {
            DateTime today = App.Now(s).Date;
            var (list, _) = await Helper.GetListT<VsPrevVisit>("SELECT TOP 5 V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.CHECKIN_AT AS CheckinAt, V.REASON AS Reason, V.STATUS AS Status, V.VISIT_TYPE AS VisitType, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM VISIT V LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID WHERE V.PET_ID = @pid AND V.VISIT_ID <> @vid AND V.VISIT_TYPE <> N'boarding' AND V.CHECKIN_AT < @at AND (@own = 0 OR V.STAFF_ID = @sid) ORDER BY V.CHECKIN_AT DESC", new List<SqlParameter> { App.P("pid", v.PetId, 50), App.P("vid", v.VisitId, 50), App.PDate("at", v.CheckinAt), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) });
            list ??= new List<VsPrevVisit>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-shead\"><h2>Previous visits</h2></div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"vs-empty vs-empty-s\">No earlier visits" + (c.U.OwnOnly ? " with you." : ".") + "</p>");
                return sb.ToString();
            }
            sb.Append("<ul class=\"vs-plist\">");
            foreach (VsPrevVisit p in list)
            {
                sb.Append("<li><a href=\"AdminVisit?v=" + Uri.EscapeDataString(p.VisitId) + "\"><span class=\"vs-pd\">" + Ui.Enc(Ui.Day(p.CheckinAt, p.CheckinAt.Year != today.Year)) + "</span><span class=\"vs-pt\"><b>" + Ui.Enc(p.Reason != string.Empty ? p.Reason : TypeName(p.VisitType)) + "</b><small>" + Ui.Enc(p.VisitNo + (p.StaffName != string.Empty ? " · " + p.StaffName : string.Empty)) + "</small></span></a></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static async Task<List<VsNote>> Notes(string vid)
        {
            var (list, emsg) = await Helper.GetListT<VsNote>("SELECT N.NOTE_ID AS NoteId, N.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, N.NOTE_KIND AS NoteKind, N.SUBJECTIVE AS Subjective, N.OBJECTIVE AS Objective, N.ASSESSMENT AS Assessment, N.PLAN_TEXT AS PlanText, N.TEMP_F AS TempF, N.HEART_RATE AS HeartRate, N.RESP_RATE AS RespRate, N.WEIGHT_LB AS WeightLb, N.PARENT_ID AS ParentId, N.CREATED_UTC AS CreatedUtc, N.SIGNED_UTC AS SignedUtc, ISNULL(G.STAFF_NAME, N'') AS SignedName FROM VISITNOTE N LEFT JOIN STAFF S ON S.STAFF_ID = N.STAFF_ID LEFT JOIN STAFF G ON G.STAFF_ID = N.SIGNED_BY WHERE N.VISIT_ID = @vid ORDER BY N.CREATED_UTC, N.NOTE_ID", new List<SqlParameter> { App.P("vid", vid, 50) });
            return emsg == string.Empty && list != null ? list : new List<VsNote>();
        }

        private static string Vitals(VsNote n)
        {
            List<string> b = new List<string>();
            if (n.TempF.HasValue)
            {
                b.Add("Temp " + n.TempF.Value.ToString("0.0", CultureInfo.InvariantCulture) + " °F");
            }
            if (n.HeartRate.HasValue)
            {
                b.Add("Heart " + n.HeartRate.Value + "/min");
            }
            if (n.RespRate.HasValue)
            {
                b.Add("Breathing " + n.RespRate.Value + "/min");
            }
            if (n.WeightLb.HasValue)
            {
                b.Add("Weight " + Ui.Num(n.WeightLb.Value) + " lb");
            }
            return b.Count == 0 ? string.Empty : "<p class=\"vs-vit\">" + Ui.Enc(string.Join(" · ", b)) + "</p>";
        }

        private static string Soap(VsNote n)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string label, string text) in new[] { ("What the owner says", n.Subjective), ("What we found", n.Objective), ("Assessment", n.Assessment), ("Plan", n.PlanText) })
            {
                if ((text ?? string.Empty).Trim() != string.Empty)
                {
                    sb.Append("<dt>" + label + "</dt><dd>" + Ui.Enc(text) + "</dd>");
                }
            }
            return sb.Length == 0 ? string.Empty : "<dl class=\"vs-soap\">" + sb + "</dl>";
        }

        private static string NoteCard(Ctx c, VsVisit v, VsNote n, List<VsNote> all)
        {
            bool open = IsOpen(v);
            bool add = n.ParentId != string.Empty;
            string nid = Ui.Js(n.NoteId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<article class=\"vs-ncard" + (add ? " vs-addn" : string.Empty) + (n.SignedUtc.HasValue ? string.Empty : " vs-unsigned") + "\"><header class=\"vs-nh\"><span class=\"vs-kind\">" + (add ? "Addendum" : Ui.Enc(KindName(n.NoteKind))) + "</span><b>" + Ui.Enc(n.StaffName) + "</b><span class=\"vs-dim\">" + Ui.Time(n.CreatedUtc, "full") + "</span>");
            sb.Append(n.SignedUtc.HasValue ? "<span class=\"vs-sig\">Signed" + (n.SignedName != string.Empty && n.SignedName != n.StaffName ? " by " + Ui.Enc(n.SignedName) : string.Empty) + " " + Ui.Time(n.SignedUtc.Value, "full") + "</span>" : "<span class=\"vs-nosig\">Not signed</span>");
            sb.Append("</header>");
            sb.Append(Vitals(n));
            sb.Append(Soap(n));
            StringBuilder acts = new StringBuilder();
            if (!n.SignedUtc.HasValue && n.StaffId == c.U.StaffId && c.Can(Key, 'E') && open)
            {
                acts.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.editNote('" + nid + "')\">Edit</button>");
                acts.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-danger\" onclick=\"AdminVisitJs.delNote('" + nid + "')\">Delete</button>");
            }
            if (!n.SignedUtc.HasValue && c.Can(Key, 'X'))
            {
                acts.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm\" onclick=\"AdminVisitJs.sign('" + nid + "')\">" + (n.StaffId == c.U.StaffId ? "Sign" : "Sign for " + Ui.Enc(n.StaffName)) + "</button>");
            }
            if (n.SignedUtc.HasValue && !add && c.Can(Key, 'E') && v.Status != "cancelled")
            {
                acts.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.addendum('" + nid + "')\">Add addendum</button>");
            }
            if (acts.Length > 0)
            {
                sb.Append("<div class=\"vs-nacts\">" + acts + "</div>");
            }
            sb.Append("</article>");
            if (!add)
            {
                List<VsNote> kids = all.Where(x => x.ParentId == n.NoteId).ToList();
                if (kids.Count > 0)
                {
                    sb.Append("<div class=\"vs-kids\">");
                    foreach (VsNote k in kids)
                    {
                        sb.Append(NoteCard(c, v, k, all));
                    }
                    sb.Append("</div>");
                }
            }
            return sb.ToString();
        }

        private static async Task<string> NotesHtml(Ctx c, VsVisit v)
        {
            List<VsNote> all = await Notes(v.VisitId);
            int mine = all.Count(x => x.StaffId == c.U.StaffId && !x.SignedUtc.HasValue);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-shead\"><h2>Notes</h2><div class=\"vs-acts\">");
            if (mine > 0 && c.Can(Key, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.signMine(" + mine + ")\">Sign all my notes (" + mine + ")</button>");
            }
            if (c.Can(Key, 'E') && IsOpen(v))
            {
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" onclick=\"AdminVisitJs.newNote()\">New note</button>");
            }
            sb.Append("</div></div>");
            List<VsNote> tops = all.Where(x => x.ParentId == string.Empty || !all.Any(p => p.NoteId == x.ParentId)).ToList();
            if (tops.Count == 0)
            {
                sb.Append("<p class=\"vs-empty\">No notes yet." + (c.Can(Key, 'E') && IsOpen(v) ? " Start with New note." : string.Empty) + "</p>");
                return sb.ToString();
            }
            foreach (VsNote n in tops)
            {
                sb.Append(NoteCard(c, v, n, all));
            }
            return sb.ToString();
        }

        private static string NoteFormHtml(string mode, VsNote? n, VsNote? parent, string defKind)
        {
            StringBuilder sb = new StringBuilder();
            string title = mode == "add" ? "Addendum to " + (parent != null ? parent.StaffName + "'s " + KindName(parent.NoteKind).ToLowerInvariant() + " note" : "a note") : mode == "edit" ? "Edit note" : "New note";
            sb.Append("<h2>" + Ui.Enc(title) + "</h2>");
            if (mode == "add")
            {
                sb.Append("<p class=\"vs-mlead\">The signed note stays as it is. Your addendum is shown under it with your name and the time.</p>");
            }
            sb.Append("<div class=\"vs-fgrid\">");
            if (mode != "add")
            {
                string kind = n != null ? n.NoteKind : defKind;
                sb.Append("<label class=\"vs-field\"><span>Kind of note</span><select id=\"vs-nkind\">" + Ui.Opts(Kinds.Select(k => (k, KindName(k))), kind) + "</select></label><span class=\"vs-fsp\"></span>");
                sb.Append("<div class=\"vs-span2 vs-vits\">");
                sb.Append("<label class=\"vs-field\"><span>Temperature <small>°F</small></span><input type=\"text\" inputmode=\"decimal\" id=\"vs-ntemp\" maxlength=\"6\" placeholder=\"101.5\" value=\"" + (n?.TempF != null ? n.TempF.Value.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty) + "\"></label>");
                sb.Append("<label class=\"vs-field\"><span>Heart rate <small>/min</small></span><input type=\"text\" inputmode=\"numeric\" id=\"vs-nhr\" maxlength=\"4\" placeholder=\"110\" value=\"" + (n?.HeartRate != null ? n.HeartRate.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) + "\"></label>");
                sb.Append("<label class=\"vs-field\"><span>Breathing <small>/min</small></span><input type=\"text\" inputmode=\"numeric\" id=\"vs-nrr\" maxlength=\"4\" placeholder=\"24\" value=\"" + (n?.RespRate != null ? n.RespRate.Value.ToString(CultureInfo.InvariantCulture) : string.Empty) + "\"></label>");
                sb.Append("<label class=\"vs-field\"><span>Weight <small>lb</small></span><input type=\"text\" inputmode=\"decimal\" id=\"vs-nwt\" maxlength=\"7\" placeholder=\"12.6\" value=\"" + (n?.WeightLb != null ? Num3(n.WeightLb.Value) : string.Empty) + "\"></label>");
                sb.Append("</div>");
            }
            sb.Append("<label class=\"vs-field vs-span2\"><span>What the owner says</span><textarea id=\"vs-ns\" rows=\"3\" maxlength=\"8000\">" + Ui.Enc(n?.Subjective) + "</textarea></label>");
            sb.Append("<label class=\"vs-field vs-span2\"><span>What we found</span><textarea id=\"vs-no\" rows=\"3\" maxlength=\"8000\">" + Ui.Enc(n?.Objective) + "</textarea></label>");
            sb.Append("<label class=\"vs-field vs-span2\"><span>Assessment</span><textarea id=\"vs-na\" rows=\"2\" maxlength=\"8000\">" + Ui.Enc(n?.Assessment) + "</textarea></label>");
            sb.Append("<label class=\"vs-field vs-span2\"><span>Plan</span><textarea id=\"vs-np\" rows=\"3\" maxlength=\"8000\">" + Ui.Enc(n?.PlanText) + "</textarea></label>");
            sb.Append("</div><em class=\"vs-err vs-merr\" id=\"vs-e-note\"></em>");
            string nid = n != null ? Ui.Js(n.NoteId) : string.Empty;
            string pid = parent != null ? Ui.Js(parent.NoteId) : string.Empty;
            sb.Append("<div class=\"vs-mfoot\"><button type=\"button\" class=\"vs-btn vs-btn-ghost\" onclick=\"AdminVisitJs.closeModal()\">Cancel</button><button type=\"button\" class=\"vs-btn vs-btn-accent\" id=\"vs-nsave\" onclick=\"AdminVisitJs.saveNote('" + nid + "','" + pid + "')\">" + (mode == "add" ? "Save addendum" : "Save note") + "</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> NoteForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "note");
            if (v == null)
            {
                return response;
            }
            List<VsNote> all = await Notes(v.VisitId);
            string noteId = CleanId(Val("noteid", 50));
            string parentId = CleanId(Val("parentid", 50));
            if (parentId != string.Empty)
            {
                VsNote? p = all.FirstOrDefault(x => x.NoteId == parentId);
                if (p == null || !p.SignedUtc.HasValue || p.ParentId != string.Empty || v.Status == "cancelled")
                {
                    await Draw(response, c, v.VisitId, "notes", "An addendum can only be added to a signed note.");
                    return response;
                }
                response.SetElementContents("vs-mbody", NoteFormHtml("add", null, p, p.NoteKind));
                response.ExecuteScript("AdminVisitJs.shown('vs-ns');");
                return response;
            }
            if (!IsOpen(v))
            {
                await Draw(response, c, v.VisitId, "notes", "This visit is closed. Only addenda and signing are possible now.");
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (noteId != string.Empty)
            {
                VsNote? n = all.FirstOrDefault(x => x.NoteId == noteId);
                if (n == null || n.SignedUtc.HasValue || n.StaffId != c.U.StaffId)
                {
                    await Draw(response, c, v.VisitId, "notes", "Only your own unsigned notes can be changed.");
                    return response;
                }
                response.SetElementContents("vs-mbody", NoteFormHtml(n.ParentId != string.Empty ? "add" : "edit", n, n.ParentId != string.Empty ? all.FirstOrDefault(x => x.NoteId == n.ParentId) : null, n.NoteKind));
                response.ExecuteScript("AdminVisitJs.shown('vs-ns');");
                return response;
            }
            string def = c.U.ProviderType == "groomer" || v.VisitType == "grooming" ? "grooming" : v.VisitType == "surgery" ? "surgery" : "exam";
            response.SetElementContents("vs-mbody", NoteFormHtml("new", null, null, def));
            response.ExecuteScript("AdminVisitJs.shown('vs-ns');");
            return response;
        }

        public async Task<ApiResponse> NoteSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "save note");
            if (v == null)
            {
                return response;
            }
            List<VsNote> all = await Notes(v.VisitId);
            string noteId = CleanId(Val("noteid", 50));
            string parentId = CleanId(Val("parentid", 50));
            VsNote? edit = noteId != string.Empty ? all.FirstOrDefault(x => x.NoteId == noteId) : null;
            VsNote? parent = parentId != string.Empty ? all.FirstOrDefault(x => x.NoteId == parentId) : edit != null && edit.ParentId != string.Empty ? all.FirstOrDefault(x => x.NoteId == edit.ParentId) : null;
            bool add = parentId != string.Empty || (edit != null && edit.ParentId != string.Empty);
            if (noteId != string.Empty && (edit == null || edit.SignedUtc.HasValue || edit.StaffId != c.U.StaffId))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "notes", "Only your own unsigned notes can be changed.");
                return response;
            }
            if (parentId != string.Empty && (parent == null || !parent.SignedUtc.HasValue || parent.ParentId != string.Empty))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "notes", "An addendum can only be added to a signed note.");
                return response;
            }
            bool needOpen = !(add && edit == null);
            if (needOpen && !IsOpen(v))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "notes", "This visit is closed. Only addenda and signing are possible now.");
                return response;
            }
            if (needOpen && await Waiting(response, c, v))
            {
                return response;
            }
            if (v.Status == "cancelled")
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                Toast(response, "This visit was cancelled.");
                return response;
            }
            string ns = Text("ns", 8000);
            string no = Text("no", 8000);
            string na = Text("na", 8000);
            string np = Text("np", 8000);
            string kind = add ? (parent != null ? parent.NoteKind : "progress") : Val("nkind", 20);
            decimal? temp = null;
            int? hr = null;
            int? rr = null;
            decimal? wt = null;
            string err = string.Empty;
            if (!add)
            {
                if (!Kinds.Contains(kind))
                {
                    err = "Pick the kind of note.";
                }
                string t = Val("ntemp", 10);
                if (err == string.Empty && t != string.Empty)
                {
                    if (TryNum(t, out decimal tv) && tv >= 90 && tv <= 110)
                    {
                        temp = Math.Round(tv, 1);
                    }
                    else
                    {
                        err = "Temperature looks wrong. Use °F, for example 101.5.";
                    }
                }
                string h = Val("nhr", 10);
                if (err == string.Empty && h != string.Empty)
                {
                    if (int.TryParse(h, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hv) && hv >= 10 && hv <= 400)
                    {
                        hr = hv;
                    }
                    else
                    {
                        err = "Heart rate should be beats per minute, for example 110.";
                    }
                }
                string r = Val("nrr", 10);
                if (err == string.Empty && r != string.Empty)
                {
                    if (int.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rv) && rv >= 2 && rv <= 200)
                    {
                        rr = rv;
                    }
                    else
                    {
                        err = "Breathing rate should be breaths per minute, for example 24.";
                    }
                }
                string w = Val("nwt", 10);
                if (err == string.Empty && w != string.Empty)
                {
                    if (TryNum(w, out decimal wv) && wv >= 0.05m && wv <= 400)
                    {
                        wt = Math.Round(wv, 2);
                    }
                    else
                    {
                        err = "Weight should be in pounds, for example 12.6.";
                    }
                }
            }
            if (err == string.Empty && ns == string.Empty && no == string.Empty && na == string.Empty && np == string.Empty && !temp.HasValue && !hr.HasValue && !rr.HasValue && !wt.HasValue)
            {
                err = add ? "Write the addendum in at least one box." : "Write something in at least one box, or enter a vital sign.";
            }
            if (err != string.Empty)
            {
                response.SetElementContents("vs-e-note", Ui.Enc(err));
                return response;
            }
            string newId = NewKey();
            if (edit == null && newId == string.Empty)
            {
                response.SetElementContents("vs-e-note", "This form is out of date. Close it and try again.");
                return response;
            }
            if (edit == null && all.Any(x => x.NoteId == newId))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "notes,head", add ? "Addendum added." : "Note added.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string id = edit != null ? edit.NoteId : newId;
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("nid", id, 50), App.P("vid", v.VisitId, 50), App.P("pid", v.PetId, 50), App.P("me", c.U.StaffId, 50), App.P("kind", kind, 20),
                App.P("ns", ns, -1), App.P("no", no, -1), App.P("na", na, -1), App.P("np", np, -1),
                Dec("temp", temp, 1), IntN("hr", hr), IntN("rr", rr), Dec("wt", wt, 2), App.P("par", add ? (parent != null ? parent.NoteId : string.Empty) : string.Empty, 50), App.PDate("now", App.Now(s))
            };
            List<string> sql = new List<string> { HoldSql };
            if (needOpen)
            {
                sql.Add(OpenSql);
            }
            if (edit != null)
            {
                sql.Add("UPDATE VISITNOTE SET NOTE_KIND = @kind, SUBJECTIVE = @ns, OBJECTIVE = @no, ASSESSMENT = @na, PLAN_TEXT = @np, TEMP_F = @temp, HEART_RATE = @hr, RESP_RATE = @rr, WEIGHT_LB = @wt, SYSDTE = GETDATE() WHERE NOTE_ID = @nid AND VISIT_ID = @vid AND STAFF_ID = @me AND SIGNED_UTC IS NULL; " + Changed);
                sql.Add(wt.HasValue ? "IF EXISTS (SELECT 1 FROM PETWEIGHT WHERE WEIGHT_ID = @nid) UPDATE PETWEIGHT SET LB = @wt, SYSDTE = GETDATE() WHERE WEIGHT_ID = @nid ELSE INSERT INTO PETWEIGHT (WEIGHT_ID, PET_ID, WEIGHED_AT, LB, VISIT_ID, STAFF_ID) VALUES (@nid, @pid, @now, @wt, @vid, @me)" : "DELETE FROM PETWEIGHT WHERE WEIGHT_ID = @nid");
            }
            else
            {
                if (add)
                {
                    sql.Add("IF NOT EXISTS (SELECT 1 FROM VISITNOTE WHERE NOTE_ID = @par AND VISIT_ID = @vid AND SIGNED_UTC IS NOT NULL) THROW 50002, N'STATECHANGED', 1;");
                }
                sql.Add("IF NOT EXISTS (SELECT 1 FROM VISITNOTE WHERE NOTE_ID = @nid) INSERT INTO VISITNOTE (NOTE_ID, VISIT_ID, STAFF_ID, NOTE_KIND, SUBJECTIVE, OBJECTIVE, ASSESSMENT, PLAN_TEXT, TEMP_F, HEART_RATE, RESP_RATE, WEIGHT_LB, PARENT_ID, CREATED_UTC) VALUES (@nid, @vid, @me, @kind, @ns, @no, @na, @np, @temp, @hr, @rr, @wt, @par, SYSUTCDATETIME())");
                if (wt.HasValue)
                {
                    sql.Add("IF NOT EXISTS (SELECT 1 FROM PETWEIGHT WHERE WEIGHT_ID = @nid) INSERT INTO PETWEIGHT (WEIGHT_ID, PET_ID, WEIGHED_AT, LB, VISIT_ID, STAFF_ID) VALUES (@nid, @pid, @now, @wt, @vid, @me)");
                }
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("vs-e-note", Ui.Enc(Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, edit != null ? "edit note" : add ? "add addendum" : "add note", v.PetName + " · " + v.VisitNo, true);
            response.ExecuteScript("AdminVisitJs.closeModal();");
            await Draw(response, c, v.VisitId, wt.HasValue || (edit != null && edit.WeightLb.HasValue) ? "notes,head" : "notes", edit != null ? "Note saved." : add ? "Addendum added." : "Note added.");
            return response;
        }

        public async Task<ApiResponse> NoteDelete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "delete note");
            if (v == null)
            {
                return response;
            }
            string noteId = CleanId(Val("noteid", 50));
            VsNote? n = (await Notes(v.VisitId)).FirstOrDefault(x => x.NoteId == noteId);
            if (n == null || n.SignedUtc.HasValue || n.StaffId != c.U.StaffId || !IsOpen(v))
            {
                await Draw(response, c, v.VisitId, "notes", "Only your own unsigned notes on an open visit can be deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                "DELETE FROM VISITNOTE WHERE NOTE_ID = @nid AND VISIT_ID = @vid AND STAFF_ID = @me AND SIGNED_UTC IS NULL; " + Changed,
                "DELETE FROM PETWEIGHT WHERE WEIGHT_ID = @nid"
            }, new List<SqlParameter> { App.P("nid", n.NoteId, 50), App.P("vid", v.VisitId, 50), App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "notes", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "delete note", v.PetName + " · " + v.VisitNo, true);
            await Draw(response, c, v.VisitId, n.WeightLb.HasValue ? "notes,head" : "notes", "Note deleted.");
            return response;
        }

        public async Task<ApiResponse> NoteSign()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'X', "sign note");
            if (v == null)
            {
                return response;
            }
            string noteId = CleanId(Val("noteid", 50));
            VsNote? n = (await Notes(v.VisitId)).FirstOrDefault(x => x.NoteId == noteId);
            if (n == null || n.SignedUtc.HasValue)
            {
                await Draw(response, c, v.VisitId, "notes", "This note is already signed.");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "UPDATE VISITNOTE SET SIGNED_UTC = SYSUTCDATETIME(), SIGNED_BY = @me, SYSDTE = GETDATE() WHERE NOTE_ID = @nid AND VISIT_ID = @vid AND SIGNED_UTC IS NULL; " + Changed }, new List<SqlParameter> { App.P("nid", n.NoteId, 50), App.P("vid", v.VisitId, 50), App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "notes", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "sign note", v.PetName + " · " + v.VisitNo + (n.StaffId != c.U.StaffId ? " · written by " + n.StaffName : string.Empty), true);
            await Draw(response, c, v.VisitId, "notes", "Note signed. It can't be changed now; add an addendum if needed.");
            return response;
        }

        public async Task<ApiResponse> SignMine()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'X', "sign notes");
            if (v == null)
            {
                return response;
            }
            int n = (await Notes(v.VisitId)).Count(x => x.StaffId == c.U.StaffId && !x.SignedUtc.HasValue);
            if (n == 0)
            {
                await Draw(response, c, v.VisitId, "notes", "You have no unsigned notes on this visit.");
                return response;
            }
            string result = await Helper.PutData(new List<string> { "UPDATE VISITNOTE SET SIGNED_UTC = SYSUTCDATETIME(), SIGNED_BY = @me, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STAFF_ID = @me AND SIGNED_UTC IS NULL" }, new List<SqlParameter> { App.P("vid", v.VisitId, 50), App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "notes", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "sign notes", v.PetName + " · " + v.VisitNo + " · " + n, true);
            await Draw(response, c, v.VisitId, "notes", Ui.Plural(n, "note", "notes") + " signed.");
            return response;
        }

        private static string DxKey(string? text)
        {
            return (text ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static async Task<(List<VsDx> Here, List<VsDx> Other)> Diagnoses(VsVisit v)
        {
            var (list, emsg) = await Helper.GetListT<VsDx>("SELECT D.DX_ID AS DxId, D.VISIT_ID AS VisitId, D.DX_TEXT AS DxText, D.DX_STATUS AS DxStatus, ISNULL(S.STAFF_NAME, N'') AS StaffName, D.CREATED_UTC AS CreatedUtc, ISNULL(X.VISIT_NO, N'') AS VisitNo, X.CHECKIN_AT AS CheckinAt FROM DIAGNOSIS D LEFT JOIN STAFF S ON S.STAFF_ID = D.STAFF_ID LEFT JOIN VISIT X ON X.VISIT_ID = D.VISIT_ID WHERE D.PET_ID = @pid ORDER BY D.CREATED_UTC DESC, D.SYSDTE DESC", new List<SqlParameter> { App.P("pid", v.PetId, 50) });
            List<VsDx> all = emsg == string.Empty && list != null ? list : new List<VsDx>();
            List<VsDx> here = all.Where(x => x.VisitId == v.VisitId).GroupBy(x => DxKey(x.DxText)).Select(g => g.First()).ToList();
            HashSet<string> taken = here.Select(x => DxKey(x.DxText)).ToHashSet();
            List<VsDx> other = all.GroupBy(x => DxKey(x.DxText)).Select(g => g.First()).Where(x => x.VisitId != v.VisitId && !taken.Contains(DxKey(x.DxText)) && (x.DxStatus == "active" || x.DxStatus == "ruleout")).ToList();
            return (here, other);
        }

        private static string DxName(string st)
        {
            return st switch { "active" => "Active", "resolved" => "Resolved", "ruleout" => "Rule out", _ => st };
        }

        private static string DxRow(Ctx c, VsVisit v, VsDx d, bool here)
        {
            bool edit = c.Can(Key, 'E') && IsOpen(v);
            string id = Ui.Js(d.DxId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<li class=\"vs-dxr\"><div class=\"vs-dxt\"><b>" + Ui.Enc(d.DxText) + "</b><small>" + (here ? Ui.Enc(d.StaffName) + " &middot; " + Ui.Time(d.CreatedUtc, "ago") : "From " + Ui.Enc(d.VisitNo) + (d.CheckinAt.HasValue ? " &middot; " + Ui.Enc(Ui.Day(d.CheckinAt.Value)) : string.Empty)) + "</small></div><div class=\"vs-dxa\">");
            if (edit)
            {
                sb.Append("<select class=\"vs-dxs vs-dx-" + Ui.Attr(d.DxStatus) + "\" aria-label=\"Status\" onchange=\"AdminVisitJs.dxStat('" + id + "', this.value)\">" + Ui.Opts(DxStates.Select(x => (x, DxName(x))), d.DxStatus) + "</select>");
                if (here)
                {
                    sb.Append("<button type=\"button\" class=\"vs-ibtn vs-del\" aria-label=\"Delete diagnosis\" title=\"Delete\" onclick=\"AdminVisitJs.dxDel('" + id + "','" + Ui.Js(d.DxText) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button>");
                }
            }
            else
            {
                sb.Append("<span class=\"vs-st vs-dxc-" + Ui.Attr(d.DxStatus) + "\">" + Ui.Enc(DxName(d.DxStatus)) + "</span>");
            }
            sb.Append("</div></li>");
            return sb.ToString();
        }

        private static async Task<string> DxHtml(Ctx c, VsVisit v)
        {
            var (here, other) = await Diagnoses(v);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-shead\"><h2>Diagnoses</h2></div>");
            if (here.Count == 0)
            {
                sb.Append("<p class=\"vs-empty vs-empty-s\">None for this visit yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"vs-dxl\">");
                foreach (VsDx d in here)
                {
                    sb.Append(DxRow(c, v, d, true));
                }
                sb.Append("</ul>");
            }
            if (c.Can(Key, 'E') && IsOpen(v))
            {
                var (sug, _) = await Helper.GetListT<CountRow>("SELECT TOP 300 DX_TEXT AS K, COUNT(*) AS N FROM DIAGNOSIS GROUP BY DX_TEXT ORDER BY COUNT(*) DESC, DX_TEXT");
                sb.Append("<div class=\"vs-dxadd\"><label class=\"vs-field vs-dxin\"><span>Add a diagnosis</span><input type=\"text\" id=\"vs-dxtext\" list=\"vs-dxsug\" maxlength=\"200\" autocomplete=\"off\" placeholder=\"Start typing\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();AdminVisitJs.dxAdd();}\"></label><label class=\"vs-field vs-dxst\"><span>Status</span><select id=\"vs-dxstat\">" + Ui.Opts(DxStates.Select(x => (x, DxName(x))), "active") + "</select></label><button type=\"button\" class=\"vs-btn vs-btn-sm\" id=\"vs-dxgo\" onclick=\"AdminVisitJs.dxAdd()\">Add</button></div><em class=\"vs-err\" id=\"vs-e-dx\"></em><datalist id=\"vs-dxsug\">");
                foreach (CountRow r in sug ?? new List<CountRow>())
                {
                    sb.Append("<option value=\"" + Ui.Attr(r.K) + "\"></option>");
                }
                sb.Append("</datalist>");
            }
            if (other.Count > 0)
            {
                sb.Append("<h3 class=\"vs-h3\">Other open problems</h3>" + (c.Can(Key, 'E') && IsOpen(v) ? "<p class=\"vs-dxhint\">Changing a status here records it on this visit.</p>" : string.Empty) + "<ul class=\"vs-dxl\">");
                foreach (VsDx d in other.GroupBy(x => x.DxText.Trim().ToLowerInvariant()).Select(g => g.First()))
                {
                    sb.Append(DxRow(c, v, d, false));
                }
                sb.Append("</ul>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> DxAdd()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "add diagnosis");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (!IsOpen(v))
            {
                await Draw(response, c, v.VisitId, "dx", "This visit is closed. Diagnoses can't be added any more.");
                return response;
            }
            string newId = NewKey();
            if (newId == string.Empty)
            {
                response.SetElementContents("vs-e-dx", "This page is out of date. Reload it and try again.");
                return response;
            }
            var (seen, _) = await Helper.GetDataTable("SELECT 1 FROM DIAGNOSIS WHERE DX_ID = @did", new List<SqlParameter> { App.P("did", newId, 50) });
            if (seen.Rows.Count > 0)
            {
                await Draw(response, c, v.VisitId, "dx", "Diagnosis added.");
                return response;
            }
            string text = Ui.OneLine(Val("dxtext", 400), 200);
            string st = Val("dxstat", 20);
            string err = text.Length < 2 ? "Write the diagnosis." : !DxStates.Contains(st) ? "Pick a status." : string.Empty;
            if (err == string.Empty && (await Diagnoses(v)).Here.Any(x => DxKey(x.DxText) == DxKey(text)))
            {
                err = "This diagnosis is already on the visit.";
            }
            if (err != string.Empty)
            {
                response.SetElementContents("vs-e-dx", Ui.Enc(err));
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                "IF NOT EXISTS (SELECT 1 FROM DIAGNOSIS WHERE DX_ID = @did) INSERT INTO DIAGNOSIS (DX_ID, VISIT_ID, PET_ID, DX_TEXT, DX_STATUS, STAFF_ID, CREATED_UTC) VALUES (@did, @vid, @pid, @txt, @st, @me, SYSUTCDATETIME())"
            }, new List<SqlParameter> { App.P("did", newId, 50), App.P("vid", v.VisitId, 50), App.P("pid", v.PetId, 50), App.P("txt", text, 200), App.P("st", st, 20), App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                response.SetElementContents("vs-e-dx", Ui.Enc(Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, "add diagnosis", v.PetName + " · " + text, true);
            await Draw(response, c, v.VisitId, "dx", "Diagnosis added.");
            return response;
        }

        public async Task<ApiResponse> DxStatus()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "diagnosis status");
            if (v == null)
            {
                return response;
            }
            string id = CleanId(Val("dxid", 50));
            string st = Val("dxstat", 20);
            var (here, other) = await Diagnoses(v);
            VsDx? d = here.FirstOrDefault(x => x.DxId == id) ?? other.FirstOrDefault(x => x.DxId == id);
            if (!IsOpen(v) || d == null || !DxStates.Contains(st))
            {
                await Draw(response, c, v.VisitId, "dx", !IsOpen(v) ? "This visit is closed." : "This diagnosis was not found.");
                return response;
            }
            bool own = d.VisitId == v.VisitId;
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                own ? "UPDATE DIAGNOSIS SET DX_STATUS = @st, SYSDTE = GETDATE() WHERE DX_ID = @did AND VISIT_ID = @vid; " + Changed
                    : "IF EXISTS (SELECT 1 FROM DIAGNOSIS WHERE VISIT_ID = @vid AND LOWER(LTRIM(RTRIM(DX_TEXT))) = @key) UPDATE DIAGNOSIS SET DX_STATUS = @st, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND LOWER(LTRIM(RTRIM(DX_TEXT))) = @key ELSE INSERT INTO DIAGNOSIS (DX_ID, VISIT_ID, PET_ID, DX_TEXT, DX_STATUS, STAFF_ID, CREATED_UTC) VALUES (@nid, @vid, @pid, @txt, @st, @me, SYSUTCDATETIME())"
            }, new List<SqlParameter> { App.P("did", d.DxId, 50), App.P("vid", v.VisitId, 50), App.P("pid", v.PetId, 50), App.P("st", st, 20), App.P("key", DxKey(d.DxText), 200), App.P("nid", App.NewId(), 50), App.P("txt", Ui.Clip(d.DxText, 200), 200), App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "dx", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "diagnosis status", v.PetName + " · " + d.DxText + " · " + st, true);
            await Draw(response, c, v.VisitId, "dx", d.DxText + ": " + DxName(st).ToLowerInvariant() + (own ? "." : ", recorded on this visit."));
            return response;
        }

        public async Task<ApiResponse> DxDelete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "delete diagnosis");
            if (v == null)
            {
                return response;
            }
            string id = CleanId(Val("dxid", 50));
            VsDx? d = (await Diagnoses(v)).Here.FirstOrDefault(x => x.DxId == id);
            if (!IsOpen(v) || d == null)
            {
                await Draw(response, c, v.VisitId, "dx", "Only a diagnosis added on this open visit can be deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                "DELETE FROM DIAGNOSIS WHERE DX_ID = @did AND VISIT_ID = @vid; " + Changed
            }, new List<SqlParameter> { App.P("did", d.DxId, 50), App.P("vid", v.VisitId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "dx", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "delete diagnosis", v.PetName + " · " + d.DxText, true);
            await Draw(response, c, v.VisitId, "dx", "Diagnosis removed.");
            return response;
        }

        private static async Task<List<VsLine>> Lines(string vid)
        {
            var (list, emsg) = await Helper.GetListT<VsLine>(LineSql, new List<SqlParameter> { App.P("vid", vid, 50) });
            return emsg == string.Empty && list != null ? list : new List<VsLine>();
        }

        private static string LineStatus(string st)
        {
            string t = st switch { "ordered" => "Ordered", "done" => "Done", "cancelled" => "Cancelled", _ => st };
            return "<span class=\"vs-st vs-ls-" + Ui.Attr(st) + "\">" + Ui.Enc(t) + "</span>";
        }

        private static string OrdersHtml(Ctx c, VsVisit v, List<VsLine> lines)
        {
            bool locked = OrdersLocked(v);
            bool edit = c.Can(Key, 'E') && !locked;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-shead\"><h2>Orders</h2><div class=\"vs-acts\">");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" onclick=\"AdminVisitJs.addOrder()\">Add order</button>");
            }
            sb.Append("</div></div>");
            if (locked)
            {
                sb.Append("<p class=\"vs-info\">" + (v.Billed == 1 && IsOpen(v) ? "The invoice for this visit was already sent, so orders are locked." : "This visit is " + (v.Status == "cancelled" ? "cancelled" : "closed") + ", so orders are locked.") + "</p>");
            }
            if (lines.Count == 0)
            {
                sb.Append("<p class=\"vs-empty\">Nothing ordered yet." + (edit ? " Use Add order for exams, medicines, tests, vaccines and more." : string.Empty) + "</p>");
                return sb.ToString();
            }
            List<string> order = Types.Concat(lines.Select(x => x.ItemType).Where(x => !Types.Contains(x)).Distinct()).ToList();
            decimal grand = 0m;
            foreach (string t in order)
            {
                List<VsLine> g = lines.Where(x => x.ItemType == t).ToList();
                if (g.Count == 0)
                {
                    continue;
                }
                decimal sub = g.Where(x => x.Status != "cancelled").Sum(x => Math.Round(x.Qty * x.UnitPrice, 2));
                grand += sub;
                sb.Append("<div class=\"vs-og\"><h3 class=\"vs-h3\">" + Ui.Enc(GroupName(t)) + "</h3><div class=\"vs-tw\"><table class=\"vs-table vs-otable\"><thead><tr><th>Item</th><th class=\"vs-num\">Qty</th><th class=\"vs-num\">Price</th><th class=\"vs-num\">Total</th><th>Status</th>" + (edit ? "<th><span class=\"vs-sr\">Actions</span></th>" : string.Empty) + "</tr></thead><tbody>");
                foreach (VsLine l in g)
                {
                    string lid = Ui.Js(l.LineId);
                    bool off = l.Status == "cancelled";
                    sb.Append("<tr" + (off ? " class=\"vs-off\"" : string.Empty) + "><td class=\"vs-cname vs-oname\"><b>" + Ui.Enc(l.ItemName) + "</b>");
                    List<string> sub2 = new List<string>();
                    if (l.Dose != string.Empty)
                    {
                        sub2.Add(l.Dose);
                    }
                    if (l.Instructions != string.Empty)
                    {
                        sub2.Add(l.Instructions);
                    }
                    if (sub2.Count > 0)
                    {
                        sb.Append("<small>" + Ui.Enc(string.Join(" — ", sub2)) + "</small>");
                    }
                    if (l.Note != string.Empty)
                    {
                        sb.Append("<small class=\"vs-onote\">Note: " + Ui.Enc(l.Note) + "</small>");
                    }
                    sb.Append("<small class=\"vs-dim\">" + (l.Dispense == "home" ? "<span class=\"vs-tag vs-tag-home\">Goes home</span> " : string.Empty) + "Ordered by " + Ui.Enc(l.OrderedName) + " &middot; " + Ui.Time(l.OrderedUtc, "ago") + (l.Status == "done" && l.DoneName != string.Empty ? " &middot; done by " + Ui.Enc(l.DoneName) : string.Empty) + "</small></td>");
                    sb.Append("<td class=\"vs-num\" data-l=\"Qty\"><span>" + Ui.Num(l.Qty) + " <span class=\"vs-dim\">" + Ui.Enc(l.Unit) + "</span></span></td>");
                    sb.Append("<td class=\"vs-num\" data-l=\"Price\">" + Ui.Money(l.UnitPrice) + "</td>");
                    sb.Append("<td class=\"vs-num\" data-l=\"Total\">" + (off ? "<s>" + Ui.Money(Math.Round(l.Qty * l.UnitPrice, 2)) + "</s>" : Ui.Money(Math.Round(l.Qty * l.UnitPrice, 2))) + "</td>");
                    sb.Append("<td data-l=\"Status\">" + LineStatus(l.Status) + "</td>");
                    if (edit)
                    {
                        sb.Append("<td class=\"vs-oacts\">");
                        if (l.Status == "ordered")
                        {
                            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm\" onclick=\"AdminVisitJs.lineSt('" + lid + "','done')\">Done</button>");
                        }
                        if (l.Status == "done")
                        {
                            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.lineSt('" + lid + "','ordered')\">Back to ordered</button>");
                        }
                        if (l.Status != "cancelled")
                        {
                            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.editLine('" + lid + "')\">Edit</button>");
                            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-danger\" onclick=\"AdminVisitJs.cancelLine('" + lid + "','" + Ui.Js(l.ItemName) + "'," + (l.ItemType == "test" ? l.ResultCount : 0) + ")\">Cancel</button>");
                        }
                        else
                        {
                            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.lineSt('" + lid + "','ordered')\">Restore</button>");
                        }
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody></table></div><p class=\"vs-sum\">" + Ui.Enc(GroupName(t)) + ": <b>" + Ui.Money(sub) + "</b></p></div>");
            }
            sb.Append("<p class=\"vs-grand\">Visit total <b>" + Ui.Money(grand) + "</b><small>Before tax and discounts. Cancelled lines are not counted.</small></p>");
            return sb.ToString();
        }

        private static decimal PriceFor(VsItem i, string size)
        {
            if (size == "medium" && i.PriceM > 0)
            {
                return i.PriceM;
            }
            if (size == "large" && i.PriceL > 0)
            {
                return i.PriceL;
            }
            return i.Price;
        }

        private static string SizeName(string size)
        {
            return size switch { "small" => "small", "medium" => "medium", "large" => "large", _ => string.Empty };
        }

        private static async Task<List<VsItem>> CatalogFind(VsVisit v, string term, string type, bool allSp, string itemId)
        {
            List<SqlParameter> ps = new List<SqlParameter> { App.P("ty", type, 20), App.P("t", Like(term), 200), App.PBit("all", allSp), App.P("spl", "%," + v.SpeciesKey.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + ",%", 60), App.P("iid", itemId, 50) };
            var (list, emsg) = await Helper.GetListT<VsItem>("SELECT TOP 40 " + ItemCols + " FROM ITEM WHERE ACTIVE = 1 AND ITEM_TYPE IN (N'practice', N'medicine', N'test', N'vaccine', N'grooming', N'product', N'fee') AND (@iid = N'' OR ITEM_ID = @iid) AND (@ty = N'' OR ITEM_TYPE = @ty) AND (@t = N'' OR ITEM_NAME LIKE @t OR ITEM_CODE LIKE @t OR CATEGORY LIKE @t) AND (@all = 1 OR SPECIES = N'' OR (N',' + REPLACE(SPECIES, N' ', N'') + N',') LIKE @spl) ORDER BY CASE ITEM_TYPE WHEN N'practice' THEN 1 WHEN N'medicine' THEN 2 WHEN N'test' THEN 3 WHEN N'vaccine' THEN 4 WHEN N'grooming' THEN 5 WHEN N'product' THEN 6 ELSE 7 END, ITEM_ORDER, ITEM_NAME", ps);
            return emsg == string.Empty && list != null ? list : new List<VsItem>();
        }

        private static string CatalogList(VsVisit v, List<VsItem> items)
        {
            if (items.Count == 0)
            {
                return "<p class=\"vs-empty vs-empty-s\">Nothing matches. Try another word or type.</p>";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-clist\">");
            foreach (VsItem i in items)
            {
                sb.Append("<button type=\"button\" class=\"vs-ci\" onclick=\"AdminVisitJs.pick('" + Ui.Js(i.ItemId) + "')\"><span class=\"vs-cit\"><b>" + Ui.Enc(i.ItemName) + "</b><small>" + Ui.Enc(TypeLabel(i.ItemType) + " · " + i.ItemCode + (i.Category != string.Empty ? " · " + i.Category : string.Empty)) + "</small></span><span class=\"vs-cip\">" + Ui.Money(PriceFor(i, v.Size)) + "<small>per " + Ui.Enc(i.Unit) + "</small></span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Catalog()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "add order");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (OrdersLocked(v))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "orders", "Orders are locked for this visit.");
                return response;
            }
            string term = Ui.OneLine(Val("cterm", 80), 80);
            string type = Val("ctype", 20);
            type = Types.Contains(type) ? type : string.Empty;
            bool all = Val("callsp", 2) == "1";
            List<VsItem> items = await CatalogFind(v, term, type, all, string.Empty);
            if (Val("cfull", 2) == "1")
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<h2>Add an order for " + Ui.Enc(v.PetName) + "</h2><div class=\"vs-cbar\"><label class=\"vs-field vs-grow2\"><span>Find an item</span><input type=\"search\" id=\"vs-cterm\" maxlength=\"80\" placeholder=\"Name, code or category\" autocomplete=\"off\" oninput=\"AdminVisitJs.cat()\"></label>");
                sb.Append("<label class=\"vs-field\"><span>Type</span><select id=\"vs-ctype\" onchange=\"AdminVisitJs.cat()\"><option value=\"\">All types</option>" + Ui.Opts(Types.Select(x => (x, TypeLabel(x))), string.Empty) + "</select></label>");
                sb.Append("<label class=\"vs-check\"><input type=\"checkbox\" id=\"vs-callsp\" onchange=\"AdminVisitJs.cat()\"><span>Show items for every species</span></label></div>");
                sb.Append("<p class=\"vs-mlead\">Showing items for " + Ui.Enc(v.SpeciesName.ToLowerInvariant()) + (SizeName(v.Size) != string.Empty ? "s, priced for a " + SizeName(v.Size) + " pet" : "s") + ".</p>");
                sb.Append("<div id=\"vs-cat\">" + CatalogList(v, items) + "</div>");
                sb.Append("<div class=\"vs-mfoot\"><button type=\"button\" class=\"vs-btn vs-btn-ghost\" onclick=\"AdminVisitJs.closeModal()\">Close</button></div>");
                response.SetElementContents("vs-mbody", sb.ToString());
                response.ExecuteScript("AdminVisitJs.shown('vs-cterm');");
                return response;
            }
            response.SetElementContents("vs-cat", CatalogList(v, items));
            return response;
        }

        private static string LineFormHtml(VsVisit v, VsItem? item, VsLine? line, string err)
        {
            string type = line != null ? line.ItemType : item!.ItemType;
            string name = line != null ? line.ItemName : item!.ItemName;
            decimal price = line != null ? line.UnitPrice : PriceFor(item!, v.Size);
            string unit = line != null ? line.Unit : item!.Unit;
            bool med = type == "medicine";
            bool canHome = type == "medicine" || type == "product";
            string disp = line != null ? line.Dispense : med && unit != "injection" ? "home" : type == "product" ? "home" : "clinic";
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + Ui.Enc(name) + "</h2><p class=\"vs-mlead\">" + Ui.Enc(TypeLabel(type)) + " &middot; " + Ui.Money(price) + " per " + Ui.Enc(unit) + (line == null && item != null && price != item.Price ? " <span class=\"vs-dim\">(" + Ui.Enc(SizeName(v.Size)) + " pet price)</span>" : string.Empty) + (line != null ? " <span class=\"vs-dim\">(price as ordered)</span>" : string.Empty) + "</p>");
            if (line == null && item != null && item.Note != string.Empty)
            {
                sb.Append("<p class=\"vs-info\">" + Ui.Enc(item.Note) + "</p>");
            }
            sb.Append("<div class=\"vs-fgrid\"><label class=\"vs-field\"><span>Quantity <small>" + Ui.Enc(unit) + "</small></span><input type=\"text\" inputmode=\"decimal\" id=\"vs-lqty\" maxlength=\"8\" value=\"" + (line != null ? Ui.Num(line.Qty) : "1") + "\"></label>");
            if (canHome)
            {
                sb.Append("<label class=\"vs-field\"><span>Given</span><select id=\"vs-ldisp\">" + Ui.Opts(new[] { ("clinic", "Here at the clinic"), ("home", "Goes home with the owner") }, disp) + "</select></label>");
            }
            else
            {
                sb.Append("<span class=\"vs-fsp\"></span>");
            }
            if (med || (line != null && (line.Dose != string.Empty || line.Instructions != string.Empty)))
            {
                sb.Append("<label class=\"vs-field vs-span2\"><span>Dose</span><input type=\"text\" id=\"vs-ldose\" maxlength=\"120\" value=\"" + Ui.Attr(line != null ? line.Dose : item!.Dose) + "\"></label>");
                sb.Append("<label class=\"vs-field vs-span2\"><span>Instructions <small>printed for the owner</small></span><textarea id=\"vs-linstr\" rows=\"3\" maxlength=\"600\">" + Ui.Enc(line != null ? line.Instructions : item!.Instructions) + "</textarea></label>");
            }
            sb.Append("<label class=\"vs-field vs-span2\"><span>Note <small>for the team, optional</small></span><input type=\"text\" id=\"vs-lnote\" maxlength=\"300\" value=\"" + Ui.Attr(line?.Note) + "\"></label></div>");
            sb.Append("<em class=\"vs-err vs-merr\" id=\"vs-e-line\">" + Ui.Enc(err) + "</em><div class=\"vs-mfoot\">");
            sb.Append(line == null ? "<button type=\"button\" class=\"vs-btn vs-btn-ghost\" onclick=\"AdminVisitJs.addOrder()\">&larr; Back to the list</button>" : "<button type=\"button\" class=\"vs-btn vs-btn-ghost\" onclick=\"AdminVisitJs.closeModal()\">Cancel</button>");
            sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-accent\" id=\"vs-lsave\" onclick=\"AdminVisitJs.saveLine('" + Ui.Js(item?.ItemId) + "','" + Ui.Js(line?.LineId) + "')\">" + (line == null ? "Add to orders" : "Save changes") + "</button></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> LineForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "order");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (OrdersLocked(v))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "orders", "Orders are locked for this visit.");
                return response;
            }
            string lineId = CleanId(Val("lineid", 50));
            if (lineId != string.Empty)
            {
                VsLine? l = (await Lines(v.VisitId)).FirstOrDefault(x => x.LineId == lineId);
                if (l == null || l.Status == "cancelled")
                {
                    await Draw(response, c, v.VisitId, "orders", "This order line can't be changed.");
                    return response;
                }
                response.SetElementContents("vs-mbody", LineFormHtml(v, null, l, string.Empty));
                response.ExecuteScript("AdminVisitJs.shown('vs-lqty');");
                return response;
            }
            string itemId = CleanId(Val("itemid", 50));
            VsItem? item = itemId == string.Empty ? null : (await CatalogFind(v, string.Empty, string.Empty, true, itemId)).FirstOrDefault();
            if (item == null)
            {
                Toast(response, "This item is not available any more.");
                return response;
            }
            response.SetElementContents("vs-mbody", LineFormHtml(v, item, null, Slots.ForSpecies(item.Species, v.SpeciesKey) ? string.Empty : "Note: this item is normally for " + item.Species.Replace(",", ", ") + "."));
            response.ExecuteScript("AdminVisitJs.shown('vs-lqty');");
            return response;
        }

        public async Task<ApiResponse> LineSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "save order");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (OrdersLocked(v))
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "orders", "Orders are locked for this visit.");
                return response;
            }
            string lineId = CleanId(Val("lineid", 50));
            VsLine? line = null;
            VsItem? item = null;
            if (lineId != string.Empty)
            {
                line = (await Lines(v.VisitId)).FirstOrDefault(x => x.LineId == lineId);
                if (line == null || line.Status == "cancelled")
                {
                    response.ExecuteScript("AdminVisitJs.closeModal();");
                    await Draw(response, c, v.VisitId, "orders", "This order line can't be changed.");
                    return response;
                }
            }
            else
            {
                string newId = NewKey();
                if (newId == string.Empty)
                {
                    response.SetElementContents("vs-e-line", "This form is out of date. Close it and try again.");
                    return response;
                }
                if ((await Lines(v.VisitId)).Any(x => x.LineId == newId))
                {
                    response.ExecuteScript("AdminVisitJs.closeModal();");
                    await Draw(response, c, v.VisitId, "orders,results,fform,takep", "Order added.");
                    return response;
                }
                lineId = newId;
                string itemId = CleanId(Val("itemid", 50));
                item = itemId == string.Empty ? null : (await CatalogFind(v, string.Empty, string.Empty, true, itemId)).FirstOrDefault();
                if (item == null)
                {
                    response.SetElementContents("vs-e-line", "This item is not available any more.");
                    return response;
                }
            }
            string type = line != null ? line.ItemType : item!.ItemType;
            string qtyText = Val("qty", 12);
            string err = string.Empty;
            if (!TryNum(qtyText, out decimal qty) || qty <= 0 || qty > 9999)
            {
                err = "Enter a quantity, for example 1 or 0.5.";
            }
            qty = Math.Round(qty, 2);
            if (err == string.Empty && qty <= 0)
            {
                err = "Enter a quantity, for example 1 or 0.5.";
            }
            string disp = Val("disp", 10);
            disp = (type == "medicine" || type == "product") && disp == "home" ? "home" : "clinic";
            string dose = Ui.OneLine(Val("dose", 240), 120);
            string instr = Text("instr", 600);
            string note = Ui.OneLine(Val("lnote", 600), 300);
            if (err != string.Empty)
            {
                response.SetElementContents("vs-e-line", Ui.Enc(err));
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", v.VisitId, 50), App.P("lid", lineId, 50), App.PDec("qty", qty), App.P("dose", dose, 120), App.P("instr", instr, 600), App.P("disp", disp, 10), App.P("note", note, 300), App.P("me", c.U.StaffId, 50) };
            List<string> sql = new List<string> { HoldSql, LockSql };
            if (line != null)
            {
                sql.Add("UPDATE ORDERLINE SET QTY = @qty, DOSE = @dose, INSTRUCTIONS = @instr, DISPENSE = @disp, NOTE = @note, SYSDTE = GETDATE() WHERE LINE_ID = @lid AND VISIT_ID = @vid AND STATUS <> N'cancelled'; " + Changed);
            }
            else
            {
                ps.Add(App.P("iid", item!.ItemId, 50));
                ps.Add(App.P("iname", item.ItemName, 120));
                ps.Add(App.P("itype", item.ItemType, 20));
                ps.Add(App.P("unit", item.Unit, 20));
                ps.Add(App.PDec("price", PriceFor(item, v.Size)));
                ps.Add(App.PBit("tax", item.Taxable));
                sql.Add("IF NOT EXISTS (SELECT 1 FROM ORDERLINE WHERE LINE_ID = @lid) INSERT INTO ORDERLINE (LINE_ID, VISIT_ID, ITEM_ID, ITEM_NAME, ITEM_TYPE, QTY, UNIT, UNIT_PRICE, TAXABLE, DOSE, INSTRUCTIONS, DISPENSE, STATUS, NOTE, ORDERED_BY, ORDERED_UTC, LINE_ORDER) SELECT @lid, @vid, @iid, @iname, @itype, @qty, @unit, @price, @tax, @dose, @instr, @disp, N'ordered', @note, @me, SYSUTCDATETIME(), ISNULL(MAX(LINE_ORDER), 0) + 1 FROM ORDERLINE WHERE VISIT_ID = @vid");
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("vs-e-line", Ui.Enc(Why(result)));
                return response;
            }
            string what = line != null ? line.ItemName : item!.ItemName;
            await App.Log(c.U.StaffId, line != null ? "edit order" : "add order", v.PetName + " · " + what, true);
            response.ExecuteScript("AdminVisitJs.closeModal();");
            await Draw(response, c, v.VisitId, type == "test" ? "orders,results,fform,takep" : "orders,takep", line != null ? what + " updated." : what + " added.");
            return response;
        }

        public async Task<ApiResponse> LineSet()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "order status");
            if (v == null)
            {
                return response;
            }
            if (OrdersLocked(v))
            {
                await Draw(response, c, v.VisitId, "orders", "Orders are locked for this visit.");
                return response;
            }
            string lineId = CleanId(Val("lineid", 50));
            string to = Val("lstat", 20);
            VsLine? l = (await Lines(v.VisitId)).FirstOrDefault(x => x.LineId == lineId);
            bool ok = l != null && ((to == "done" && l.Status == "ordered") || (to == "ordered" && (l.Status == "done" || l.Status == "cancelled")) || (to == "cancelled" && (l.Status == "ordered" || l.Status == "done")));
            if (!ok)
            {
                await Draw(response, c, v.VisitId, "orders,results", "This order line was changed by someone else.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", v.VisitId, 50), App.P("lid", l!.LineId, 50), App.P("st", to, 20), App.P("from", l.Status, 20), App.P("me", c.U.StaffId, 50), App.P("pid", v.PetId, 50), App.P("iid", l.ItemId, 50) };
            List<string> sql = new List<string>
            {
                HoldSql,
                LockSql,
                "UPDATE ORDERLINE SET STATUS = @st, DONE_BY = CASE WHEN @st = N'done' THEN @me ELSE N'' END, DONE_UTC = CASE WHEN @st = N'done' THEN SYSUTCDATETIME() ELSE NULL END, SYSDTE = GETDATE() WHERE LINE_ID = @lid AND VISIT_ID = @vid AND STATUS = @from; " + Changed
            };
            string extra = string.Empty;
            if (to == "cancelled" && l.ItemType == "test" && l.ResultCount > 0)
            {
                sql.Add("DELETE FROM RESULT WHERE LINE_ID = @lid");
                extra = " Its results were removed.";
            }
            if (l.ItemType == "vaccine")
            {
                if (to == "done")
                {
                    var (mt, _) = await Helper.GetDataTable("SELECT VAX_MONTHS FROM ITEM WHERE ITEM_ID = @iid", new List<SqlParameter> { App.P("iid", l.ItemId, 50) });
                    int months = mt.Rows.Count > 0 && mt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(mt.Rows[0][0]) : 0;
                    if (months <= 0)
                    {
                        months = 12;
                    }
                    string vname = Ui.Clip(Regex.Replace(l.ItemName, "\\s+vaccine$", string.Empty, RegexOptions.IgnoreCase).Trim(), 60);
                    ps.Add(App.P("vname", vname == string.Empty ? l.ItemName : vname, 60));
                    ps.Add(App.PDate("given", today));
                    ps.Add(App.PDate("due", today.AddMonths(months)));
                    sql.Add("IF NOT EXISTS (SELECT 1 FROM PETVACCINE WHERE VAX_ID = @lid) INSERT INTO PETVACCINE (VAX_ID, PET_ID, ITEM_ID, VAX_NAME, GIVEN_DATE, DUE_DATE, VISIT_ID, STAFF_ID) VALUES (@lid, @pid, @iid, @vname, @given, @due, @vid, @me)");
                    extra = " Vaccine record added, next due " + Ui.Day(today.AddMonths(months)) + ".";
                }
                else
                {
                    sql.Add("DELETE FROM PETVACCINE WHERE VAX_ID = @lid OR (VISIT_ID = @vid AND ITEM_ID = @iid AND NOT EXISTS (SELECT 1 FROM ORDERLINE O WHERE O.VISIT_ID = @vid AND O.ITEM_ID = @iid AND O.STATUS = N'done' AND O.LINE_ID <> @lid))");
                    if (l.Status == "done")
                    {
                        extra = " The vaccine record was removed.";
                    }
                }
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "orders,results", Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, to == "done" ? "order done" : to == "cancelled" ? "cancel order" : "order back to ordered", v.PetName + " · " + l.ItemName, true);
            string msg = l.ItemName + (to == "done" ? " marked done." : to == "cancelled" ? " cancelled." : " is ordered again.") + extra;
            await Draw(response, c, v.VisitId, l.ItemType == "test" ? "orders,results,fform,takep" : "orders,takep", msg);
            return response;
        }

        private static async Task<List<VsField>> Fields(string itemId, string species)
        {
            var (list, emsg) = await Helper.GetListT<VsField>("SELECT FIELD_ID AS FieldId, FIELD_NAME AS FieldName, UNIT AS Unit, SPECIES_KEY AS SpeciesKey, LOW_VALUE AS LowValue, HIGH_VALUE AS HighValue, DECIMALS AS Decimals, IS_TEXT AS IsText, FIELD_ORDER AS FieldOrder FROM TESTFIELD WHERE ITEM_ID = @iid AND (SPECIES_KEY = N'' OR SPECIES_KEY = @sp) ORDER BY FIELD_ORDER, FIELD_NAME", new List<SqlParameter> { App.P("iid", itemId, 50), App.P("sp", species, 20) });
            List<VsField> raw = emsg == string.Empty && list != null ? list : new List<VsField>();
            List<VsField> pick = raw.GroupBy(f => f.FieldName.Trim().ToLowerInvariant()).Select(g => g.FirstOrDefault(f => f.SpeciesKey != string.Empty) ?? g.First()).OrderBy(f => f.FieldOrder).ThenBy(f => f.FieldName).ToList();
            if (pick.Count == 0)
            {
                pick.Add(new VsField { FieldId = string.Empty, FieldName = "Result", IsText = true });
            }
            return pick;
        }

        private static string FieldKey(VsField f)
        {
            return f.FieldId != string.Empty ? f.FieldId : "n:" + f.FieldName;
        }

        private static async Task<List<VsResult>> LineResults(string lineId)
        {
            var (list, emsg) = await Helper.GetListT<VsResult>("SELECT R.FIELD_ID AS FieldId, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.LOW_VALUE AS LowValue, R.HIGH_VALUE AS HighValue, R.FLAG AS Flag, ISNULL(S.STAFF_NAME, N'') AS EnteredName, R.ENTERED_UTC AS EnteredUtc FROM RESULT R LEFT JOIN STAFF S ON S.STAFF_ID = R.ENTERED_BY LEFT JOIN TESTFIELD T ON T.FIELD_ID = R.FIELD_ID WHERE R.LINE_ID = @lid ORDER BY ISNULL(T.FIELD_ORDER, 999), R.FIELD_NAME", new List<SqlParameter> { App.P("lid", lineId, 50) });
            return emsg == string.Empty && list != null ? list : new List<VsResult>();
        }

        private static async Task<Dictionary<string, VsResult>> PrevValues(string petId, VsLine l)
        {
            var (list, emsg) = await Helper.GetListT<VsResult>("SELECT TOP 300 R.FIELD_ID AS FieldId, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.LOW_VALUE AS LowValue, R.HIGH_VALUE AS HighValue, R.FLAG AS Flag, N'' AS EnteredName, R.ENTERED_UTC AS EnteredUtc FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID WHERE R.PET_ID = @pid AND O.ITEM_ID = @iid AND R.LINE_ID <> @lid AND O.STATUS <> N'cancelled' AND O.ORDERED_UTC < @at ORDER BY R.ENTERED_UTC DESC", new List<SqlParameter> { App.P("pid", petId, 50), App.P("iid", l.ItemId, 50), App.P("lid", l.LineId, 50), App.PDate("at", l.OrderedUtc) });
            Dictionary<string, VsResult> d = new Dictionary<string, VsResult>();
            foreach (VsResult r in emsg == string.Empty && list != null ? list : new List<VsResult>())
            {
                string k = r.FieldName.Trim().ToLowerInvariant();
                if (!d.ContainsKey(k))
                {
                    d[k] = r;
                }
            }
            return d;
        }

        private static string ValueHtml(VsResult r)
        {
            if (r.ValueNum.HasValue)
            {
                string cls = r.Flag == "H" ? " vs-hi" : r.Flag == "L" ? " vs-lo" : string.Empty;
                return "<span class=\"vs-val" + cls + "\">" + Num3(r.ValueNum.Value) + (r.Flag != string.Empty ? " <i>" + Ui.Enc(r.Flag == "H" ? "High" : "Low") + "</i>" : string.Empty) + "</span>";
            }
            return "<span class=\"vs-val\">" + Ui.Enc(r.ValueText) + "</span>";
        }

        private static string PrevHint(VsResult? p)
        {
            if (p == null)
            {
                return string.Empty;
            }
            string val = p.ValueNum.HasValue ? Num3(p.ValueNum.Value) + (p.Unit != string.Empty ? " " + p.Unit : string.Empty) + (p.Flag != string.Empty ? " (" + (p.Flag == "H" ? "high" : "low") + ")" : string.Empty) : p.ValueText;
            return "Last time " + Ui.Enc(Ui.Clip(val, 60)) + " &middot; " + Ui.Time(p.EnteredUtc, "day");
        }

        private static bool CanResult(Ctx c, VsVisit v, VsLine l)
        {
            return c.Can(Key, 'E') && l.ItemType == "test" && l.Status != "cancelled" && (IsOpen(v) || (v.Status == "closed" && l.Status == "ordered"));
        }

        private static async Task<string> ResultCard(Ctx c, VsVisit v, VsLine l, bool form)
        {
            List<VsResult> res = await LineResults(l.LineId);
            bool can = CanResult(c, v, l);
            bool showForm = can && (form || l.Status == "ordered");
            string cid = Safe(l.LineId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<header class=\"vs-rh\"><div><b>" + Ui.Enc(l.ItemName) + "</b><small>Ordered by " + Ui.Enc(l.OrderedName) + " &middot; " + Ui.Time(l.OrderedUtc, "ago") + (res.Count > 0 ? " &middot; entered by " + Ui.Enc(res[0].EnteredName) + " " + Ui.Time(res[0].EnteredUtc, "ago") : string.Empty) + "</small></div>" + (l.Status == "done" ? "<span class=\"vs-st vs-ls-done\">" + (res.Count > 0 ? "Results in" : "Done") + "</span>" : "<span class=\"vs-st vs-ls-wait\">Waiting for results</span>") + "</header>");
            if (showForm)
            {
                List<VsField> fields = await Fields(l.ItemId, v.SpeciesKey);
                Dictionary<string, VsResult> prev = await PrevValues(v.PetId, l);
                sb.Append("<div class=\"vs-rform\">");
                int i = 0;
                foreach (VsField f in fields)
                {
                    VsResult? have = res.FirstOrDefault(r => (f.FieldId != string.Empty && r.FieldId == f.FieldId) || r.FieldName.Equals(f.FieldName, StringComparison.OrdinalIgnoreCase));
                    prev.TryGetValue(f.FieldName.Trim().ToLowerInvariant(), out VsResult? pv);
                    string iid = "vs-ri-" + cid + "-" + i;
                    string rng = Range(f.LowValue, f.HighValue);
                    sb.Append("<div class=\"vs-rrow\"><label for=\"" + iid + "\"><b>" + Ui.Enc(f.FieldName) + "</b>" + (f.Unit != string.Empty ? "<small>" + Ui.Enc(f.Unit) + "</small>" : string.Empty) + "</label>");
                    if (f.IsText)
                    {
                        sb.Append("<input type=\"text\" class=\"vs-rin vs-rtext\" id=\"" + iid + "\" data-k=\"" + Ui.Attr(FieldKey(f)) + "\" maxlength=\"400\" value=\"" + Ui.Attr(have?.ValueText) + "\">");
                    }
                    else
                    {
                        string cur = have?.ValueNum != null ? Num3(have.ValueNum.Value) : string.Empty;
                        string fl = have != null ? have.Flag : string.Empty;
                        sb.Append("<input type=\"text\" inputmode=\"decimal\" class=\"vs-rin" + (fl == "H" ? " vs-fh" : fl == "L" ? " vs-fl" : string.Empty) + "\" id=\"" + iid + "\" data-k=\"" + Ui.Attr(FieldKey(f)) + "\" data-lo=\"" + (f.LowValue.HasValue ? Num3(f.LowValue.Value) : string.Empty) + "\" data-hi=\"" + (f.HighValue.HasValue ? Num3(f.HighValue.Value) : string.Empty) + "\" maxlength=\"14\" value=\"" + Ui.Attr(cur) + "\" oninput=\"AdminVisitJs.flag(this)\">");
                    }
                    sb.Append("<span class=\"vs-rmeta\">" + (rng != string.Empty ? "<span>Normal " + rng + "</span>" : string.Empty) + (pv != null ? "<span class=\"vs-rprev\">" + PrevHint(pv) + "</span>" : string.Empty) + "</span></div>");
                    i++;
                }
                sb.Append("</div><em class=\"vs-err\" id=\"vs-e-r-" + cid + "\"></em><div class=\"vs-racts\">");
                if (res.Count > 0 && l.Status == "done")
                {
                    sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.resView('" + Ui.Js(l.LineId) + "')\">Cancel</button>");
                }
                sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" onclick=\"AdminVisitJs.saveRes('" + Ui.Js(l.LineId) + "','" + cid + "',this)\">Save results</button></div>");
                return sb.ToString();
            }
            if (res.Count == 0)
            {
                sb.Append("<p class=\"vs-empty vs-empty-s\">" + (l.Status == "done" ? "Marked done without results." : "No results yet.") + "</p>");
            }
            else
            {
                Dictionary<string, VsResult> prev = await PrevValues(v.PetId, l);
                sb.Append("<div class=\"vs-rtw\"><table class=\"vs-rtable\"><thead><tr><th>Test</th><th>Result</th><th>Normal</th></tr></thead><tbody>");
                foreach (VsResult r in res)
                {
                    prev.TryGetValue(r.FieldName.Trim().ToLowerInvariant(), out VsResult? pv);
                    string rng = Range(r.LowValue, r.HighValue);
                    sb.Append("<tr" + (r.Flag != string.Empty ? " class=\"vs-flagged\"" : string.Empty) + "><td><b>" + Ui.Enc(r.FieldName) + "</b></td><td>" + ValueHtml(r) + (r.Unit != string.Empty ? " <span class=\"vs-dim\">" + Ui.Enc(r.Unit) + "</span>" : string.Empty) + (pv != null ? "<small class=\"vs-rprev vs-blk\">" + PrevHint(pv) + "</small>" : string.Empty) + "</td><td>" + (rng != string.Empty ? rng : "<span class=\"vs-dim\">&mdash;</span>") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            if (can)
            {
                sb.Append("<div class=\"vs-racts\"><button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-ghost\" onclick=\"AdminVisitJs.resForm('" + Ui.Js(l.LineId) + "')\">" + (res.Count > 0 ? "Change results" : "Enter results") + "</button></div>");
            }
            return sb.ToString();
        }

        private static async Task<string> ResultsHtml(Ctx c, VsVisit v, List<VsLine> lines)
        {
            List<VsLine> tests = lines.Where(x => x.ItemType == "test" && x.Status != "cancelled").ToList();
            StringBuilder sb = new StringBuilder();
            int wait = tests.Count(x => x.Status == "ordered");
            sb.Append("<div class=\"vs-shead\"><h2>Test results</h2>" + (tests.Count > 0 ? "<p class=\"vs-sub\">" + (wait > 0 ? Ui.Plural(wait, "test", "tests") + " waiting for results" : "All results are in") + "</p>" : string.Empty) + "</div>");
            if (tests.Count == 0)
            {
                sb.Append("<p class=\"vs-empty\">No tests ordered. Tests added under Orders show their result form here.</p>");
                return sb.ToString();
            }
            sb.Append("<div class=\"vs-rgrid\">");
            foreach (VsLine l in tests)
            {
                sb.Append("<article class=\"vs-rcard\" id=\"vs-rc-" + Safe(l.LineId) + "\">" + await ResultCard(c, v, l, false) + "</article>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> ResultForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            bool view = Val("rview", 2) == "1";
            VsVisit? v = await Reach(response, c, view ? 'V' : 'E', "results");
            if (v == null)
            {
                return response;
            }
            if (!view && await Waiting(response, c, v))
            {
                return response;
            }
            string lineId = CleanId(Val("lineid", 50));
            VsLine? l = (await Lines(v.VisitId)).FirstOrDefault(x => x.LineId == lineId && x.ItemType == "test");
            if (l == null)
            {
                await Draw(response, c, v.VisitId, "results", "This test was not found.");
                return response;
            }
            if (!view && !CanResult(c, v, l))
            {
                await Draw(response, c, v.VisitId, "results", "These results can't be changed any more.");
                return response;
            }
            response.SetElementContents("vs-rc-" + Safe(l.LineId), await ResultCard(c, v, l, !view));
            response.ExecuteScript("AdminVisitJs.drawn();");
            return response;
        }

        public async Task<ApiResponse> ResultSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "enter results");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            string lineId = CleanId(Val("lineid", 50));
            VsLine? l = (await Lines(v.VisitId)).FirstOrDefault(x => x.LineId == lineId && x.ItemType == "test");
            if (l == null || !CanResult(c, v, l))
            {
                await Draw(response, c, v.VisitId, "results,orders", l == null ? "This test was not found." : "These results can't be changed any more.");
                return response;
            }
            string cid = Safe(l.LineId);
            List<VsRv> vals;
            try
            {
                vals = JsonSerializer.Deserialize<List<VsRv>>(Val("rvals", 20000), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<VsRv>();
            }
            catch (JsonException)
            {
                vals = new List<VsRv>();
            }
            List<VsField> fields = await Fields(l.ItemId, v.SpeciesKey);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("vid", v.VisitId, 50), App.P("lid", l.LineId, 50), App.P("pid", v.PetId, 50), App.P("me", c.U.StaffId, 50), App.P("vst", v.Status, 20) };
            List<string> sql = new List<string>
            {
                HoldSql,
                "UPDATE ORDERLINE SET STATUS = N'done', DONE_BY = @me, DONE_UTC = SYSUTCDATETIME(), SYSDTE = GETDATE() WHERE LINE_ID = @lid AND VISIT_ID = @vid AND ITEM_TYPE = N'test' AND STATUS " + (IsOpen(v) ? "IN (N'ordered', N'done')" : "= N'ordered'") + " AND EXISTS (SELECT 1 FROM VISIT WHERE VISIT_ID = @vid AND STATUS = @vst); " + Changed,
                "DELETE FROM RESULT WHERE LINE_ID = @lid"
            };
            string err = string.Empty;
            int n = 0;
            int flagged = 0;
            foreach (VsField f in fields)
            {
                VsRv? rv = vals.FirstOrDefault(x => x.K == FieldKey(f));
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
                    if (!TryNum(raw, out decimal x) || Math.Abs(x) >= 1000000000m)
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
                ps.Add(Dec("x" + n, num, 3));
                ps.Add(App.P("t" + n, text, 400));
                ps.Add(App.P("u" + n, Ui.Clip(f.Unit, 20), 20));
                ps.Add(Dec("lo" + n, f.LowValue, 3));
                ps.Add(Dec("hi" + n, f.HighValue, 3));
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
                response.SetElementContents("vs-e-r-" + cid, Ui.Enc(err));
                return response;
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("vs-e-r-" + cid, Ui.Enc(Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, "enter results", v.PetName + " · " + l.ItemName + (flagged > 0 ? " · " + flagged + " flagged" : string.Empty), true);
            await Draw(response, c, v.VisitId, "orders,results", "Results saved for " + l.ItemName + "." + (flagged > 0 ? " " + Ui.Plural(flagged, "value is", "values are") + " outside the normal range." : string.Empty));
            return response;
        }

        private static async Task<List<VsFile>> Files(string vid)
        {
            var (list, emsg) = await Helper.GetListT<VsFile>("SELECT F.FILE_ID AS FileId, F.LINE_ID AS LineId, F.TITLE AS Title, F.FILE_NAME AS FileName, F.MIME AS Mime, F.BYTES AS Bytes, F.UPLOADED_BY AS UploadedBy, ISNULL(S.STAFF_NAME, N'') AS UploaderName, F.UPLOADED_UTC AS UploadedUtc, F.SEND_OWNER AS SendOwner, F.STORED_NAME AS StoredName FROM VISITFILE F LEFT JOIN STAFF S ON S.STAFF_ID = F.UPLOADED_BY WHERE F.VISIT_ID = @vid ORDER BY F.UPLOADED_UTC DESC", new List<SqlParameter> { App.P("vid", vid, 50) });
            return emsg == string.Empty && list != null ? list : new List<VsFile>();
        }

        private static string Bytes(long b)
        {
            return b >= 1048576 ? (b / 1048576m).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : Math.Max(1, (int)Math.Ceiling(b / 1024m)) + " KB";
        }

        private static string FileFormHtml(Ctx c, VsVisit v, List<VsLine> lines, Dictionary<string, string> s)
        {
            if (!c.Can(Key, 'E') || !IsOpen(v))
            {
                return string.Empty;
            }
            int mb = App.SInt(s, "file.mb", 15);
            List<VsLine> tests = lines.Where(x => x.ItemType == "test" && x.Status != "cancelled").ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-up\"><div class=\"vs-upick\"><input type=\"file\" id=\"vs-ffile\" class=\"vs-sr\" accept=\".pdf,.jpg,.jpeg,.png,.webp,.txt,.csv\" data-max=\"" + (mb * 1048576L).ToString(CultureInfo.InvariantCulture) + "\" onchange=\"AdminVisitJs.pickFile(this)\"><label for=\"vs-ffile\" class=\"vs-btn vs-btn-sm vs-btn-ghost\">Choose a file</label><span id=\"vs-fname\" class=\"vs-dim\">PDF, photo (JPG, PNG, WebP), TXT or CSV &middot; up to " + mb + " MB</span></div>");
            sb.Append("<div class=\"vs-fgrid\"><label class=\"vs-field\"><span>Title</span><input type=\"text\" id=\"vs-ftitle\" maxlength=\"120\" placeholder=\"For example X-ray, left hind leg\"></label>");
            sb.Append("<label class=\"vs-field\"><span>For test <small>optional</small></span><select id=\"vs-fline\"><option value=\"\">Not linked to a test</option>" + Ui.Opts(tests.Select(x => (x.LineId, x.ItemName)), string.Empty) + "</select></label></div>");
            sb.Append("<label class=\"vs-check\"><input type=\"checkbox\" id=\"vs-fsend\"><span>Send to the owner with the take-home summary</span></label>");
            sb.Append("<div class=\"vs-prog\" id=\"vs-fprog\"><i id=\"vs-fbar\"></i></div><em class=\"vs-err\" id=\"vs-e-file\"></em><div class=\"vs-racts\"><button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" id=\"vs-fgo\" onclick=\"AdminVisitJs.upload()\">Upload</button></div></div>");
            return sb.ToString();
        }

        private static async Task<string> FileListHtml(Ctx c, VsVisit v, List<VsLine> lines)
        {
            List<VsFile> files = await Files(v.VisitId);
            if (files.Count == 0)
            {
                return "<p class=\"vs-empty vs-empty-s\">No files on this visit.</p>";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<ul class=\"vs-files\">");
            foreach (VsFile f in files)
            {
                string kind = f.Mime == "application/pdf" ? "PDF" : f.Mime.StartsWith("image/", StringComparison.Ordinal) ? "IMG" : f.Mime == "text/csv" ? "CSV" : "TXT";
                VsLine? l = f.LineId != string.Empty ? lines.FirstOrDefault(x => x.LineId == f.LineId) : null;
                sb.Append("<li class=\"vs-file\"><span class=\"vs-fic vs-fic-" + kind.ToLowerInvariant() + "\" aria-hidden=\"true\">" + kind + "</span><div class=\"vs-fit\"><a href=\"vfile?id=" + Uri.EscapeDataString(f.FileId) + "\" target=\"_blank\" rel=\"noopener\"><b>" + Ui.Enc(f.Title != string.Empty ? f.Title : f.FileName) + "</b></a><small>" + Ui.Enc(f.FileName + " · " + Bytes(f.Bytes)) + " &middot; " + Ui.Enc(f.UploaderName) + " " + Ui.Time(f.UploadedUtc, "ago") + "</small>");
                if (l != null || f.SendOwner)
                {
                    sb.Append("<span class=\"vs-ftags\">" + (l != null ? "<span class=\"vs-tag\">" + Ui.Enc(l.ItemName) + "</span>" : string.Empty) + (f.SendOwner ? "<span class=\"vs-tag vs-tag-home\">Sent to owner</span>" : string.Empty) + "</span>");
                }
                sb.Append("</div><div class=\"vs-fa\"><a class=\"vs-btn vs-btn-sm vs-btn-ghost\" href=\"vfile?id=" + Uri.EscapeDataString(f.FileId) + "\" target=\"_blank\" rel=\"noopener\">View</a>");
                if (c.Can(Key, 'E') && IsOpen(v) && (f.UploadedBy == c.U.StaffId || c.Manager))
                {
                    sb.Append("<button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-danger\" onclick=\"AdminVisitJs.delFile('" + Ui.Js(f.FileId) + "','" + Ui.Js(f.Title != string.Empty ? f.Title : f.FileName) + "')\">Delete</button>");
                }
                sb.Append("</div></li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string PartPath(Ctx c, string uid)
        {
            string dir = Path.Combine(App.VaultDir, "tmp");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, Safe(c.U.StaffId) + "-" + uid + ".part");
        }

        private static void UpFail(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminVisitJs.upFail('" + Ui.Js(msg) + "');");
        }

        private string UploadId()
        {
            string uid = Val("uid", 40);
            return Regex.IsMatch(uid, "^[a-f0-9]{16,40}$") ? uid : string.Empty;
        }

        public async Task<ApiResponse> FilePart()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string uid = UploadId();
            VsVisit? v = await Reach(response, c, 'E', "upload file");
            if (v == null)
            {
                TryDelete(uid != string.Empty ? PartPath(c, uid) : string.Empty);
                UpFail(response, string.Empty);
                return response;
            }
            if (!IsOpen(v))
            {
                TryDelete(uid != string.Empty ? PartPath(c, uid) : string.Empty);
                UpFail(response, "This visit is closed. Files can't be added any more.");
                return response;
            }
            if (v.Status == "waiting")
            {
                TryDelete(uid != string.Empty ? PartPath(c, uid) : string.Empty);
                await Waiting(response, c, v);
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            int mb = App.SInt(s, "file.mb", 15);
            long max = mb * 1048576L;
            int idx = int.TryParse(Val("idx", 6), out int ix) ? ix : -1;
            int total = int.TryParse(Val("total", 6), out int tt) ? tt : 0;
            if (uid == string.Empty || idx < 0 || total < 1 || idx >= total || (long)(total - 1) * ChunkBytes >= max)
            {
                UpFail(response, "Files must be " + mb + " MB or smaller.");
                return response;
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(GetDataValue("part") ?? string.Empty);
            }
            catch (FormatException)
            {
                UpFail(response, "The upload was damaged. Try again.");
                return response;
            }
            string file = PartPath(c, uid);
            if (idx == 0)
            {
                foreach (string stale in Directory.GetFiles(Path.GetDirectoryName(file) ?? App.VaultDir, "*.part"))
                {
                    if (File.GetLastWriteTimeUtc(stale) < DateTime.UtcNow.AddHours(-2))
                    {
                        TryDelete(stale);
                    }
                }
                TryDelete(file);
            }
            long have = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (have != (long)idx * ChunkBytes || bytes.Length == 0 || bytes.Length > ChunkBytes || (idx < total - 1 && bytes.Length != ChunkBytes) || have + bytes.Length > max)
            {
                TryDelete(file);
                UpFail(response, have + bytes.Length > max ? "Files must be " + mb + " MB or smaller." : "The upload was interrupted. Try again.");
                return response;
            }
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Append, FileAccess.Write))
                {
                    await fs.WriteAsync(bytes);
                }
            }
            catch (IOException)
            {
                TryDelete(file);
                UpFail(response, "The file could not be saved. Try again.");
                return response;
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(file);
                UpFail(response, "The file could not be saved. Try again.");
                return response;
            }
            response.ExecuteScript("AdminVisitJs.chunkOk(" + idx + ");");
            return response;
        }

        private static bool Looks(string ext, byte[] h)
        {
            switch (ext)
            {
                case ".pdf":
                    return h.Length >= 4 && h[0] == 0x25 && h[1] == 0x50 && h[2] == 0x44 && h[3] == 0x46;
                case ".png":
                    return h.Length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;
                case ".jpg":
                case ".jpeg":
                    return h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;
                case ".webp":
                    return h.Length >= 12 && h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 && h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50;
                default:
                    return !h.Contains((byte)0);
            }
        }

        public async Task<ApiResponse> FileDone()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string uid = UploadId();
            string part = uid != string.Empty ? PartPath(c, uid) : string.Empty;
            VsVisit? v = await Reach(response, c, 'E', "upload file");
            if (v == null)
            {
                TryDelete(part);
                UpFail(response, string.Empty);
                return response;
            }
            if (part == string.Empty || !File.Exists(part))
            {
                UpFail(response, "The upload was interrupted. Try again.");
                return response;
            }
            if (!IsOpen(v))
            {
                TryDelete(part);
                UpFail(response, "This visit is closed. Files can't be added any more.");
                return response;
            }
            if (v.Status == "waiting")
            {
                TryDelete(part);
                await Waiting(response, c, v);
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            long max = App.SInt(s, "file.mb", 15) * 1048576L;
            string name = Regex.Replace(Path.GetFileName((Val("fname", 300)).Replace('\\', '/')), "[\\x00-\\x1F\"/:*?<>|]", string.Empty).Trim();
            name = Ui.Clip(name == string.Empty ? "file" : name, 200);
            string ext = Path.GetExtension(name).ToLowerInvariant();
            long size;
            byte[] head = new byte[512];
            int got;
            try
            {
                size = new FileInfo(part).Length;
                using (FileStream fs = File.OpenRead(part))
                {
                    got = await fs.ReadAsync(head.AsMemory(0, head.Length));
                }
            }
            catch (IOException)
            {
                TryDelete(part);
                UpFail(response, "The upload could not be read. Try again.");
                return response;
            }
            byte[] h = head.Take(got).ToArray();
            string err = !Exts.ContainsKey(ext) ? "Use a PDF, JPG, PNG, WebP, TXT or CSV file." : size == 0 || size > max ? "Files must be " + (max / 1048576) + " MB or smaller." : !Looks(ext, h) ? "This file does not look like a real " + ext.TrimStart('.').ToUpperInvariant() + " file." : string.Empty;
            if (err != string.Empty)
            {
                TryDelete(part);
                UpFail(response, err);
                return response;
            }
            string title = Ui.OneLine(Val("ftitle", 240), 120);
            if (title == string.Empty)
            {
                title = Ui.Clip(Path.GetFileNameWithoutExtension(name), 120);
            }
            string lineId = CleanId(Val("fline", 50));
            List<VsLine> lines = await Lines(v.VisitId);
            if (lineId != string.Empty && !lines.Any(x => x.LineId == lineId && x.ItemType == "test" && x.Status != "cancelled"))
            {
                lineId = string.Empty;
            }
            bool send = Val("fsend", 2) == "1";
            string fid = App.NewId();
            string stored = fid + ext;
            string dest = Path.Combine(App.VaultDir, stored);
            try
            {
                File.Move(part, dest);
            }
            catch (IOException)
            {
                TryDelete(part);
                UpFail(response, "The file could not be saved. Try again.");
                return response;
            }
            catch (UnauthorizedAccessException)
            {
                TryDelete(part);
                UpFail(response, "The file could not be saved. Try again.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                "INSERT INTO VISITFILE (FILE_ID, VISIT_ID, PET_ID, LINE_ID, TITLE, FILE_NAME, STORED_NAME, MIME, BYTES, UPLOADED_BY, UPLOADED_UTC, SEND_OWNER) VALUES (@fid, @vid, @pid, @lid, @title, @fname, @stored, @mime, @bytes, @me, SYSUTCDATETIME(), @send)"
            }, new List<SqlParameter>
            {
                App.P("fid", fid, 50), App.P("vid", v.VisitId, 50), App.P("pid", v.PetId, 50), App.P("lid", lineId, 50), App.P("title", title, 120), App.P("fname", name, 200), App.P("stored", stored, 100), App.P("mime", Exts[ext], 60),
                new SqlParameter("bytes", SqlDbType.BigInt) { Value = size }, App.P("me", c.U.StaffId, 50), App.PBit("send", send)
            });
            if (Helper.IsError(result))
            {
                TryDelete(dest);
                UpFail(response, Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "upload file", v.PetName + " · " + title, true);
            await Draw(response, c, v.VisitId, "fform,flist,takep", "File added.");
            response.ExecuteScript("AdminVisitJs.upDone();");
            return response;
        }

        public async Task<ApiResponse> FileDelete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "delete file");
            if (v == null)
            {
                return response;
            }
            string fid = CleanId(Val("fileid", 50));
            VsFile? f = (await Files(v.VisitId)).FirstOrDefault(x => x.FileId == fid);
            if (f == null || !IsOpen(v) || (f.UploadedBy != c.U.StaffId && !c.Manager))
            {
                await Draw(response, c, v.VisitId, "flist", f == null ? "This file was not found." : !IsOpen(v) ? "This visit is closed." : "Only the person who added the file or a manager can delete it.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                HoldSql,
                OpenSql,
                "DELETE FROM VISITFILE WHERE FILE_ID = @fid AND VISIT_ID = @vid; " + Changed
            }, new List<SqlParameter> { App.P("fid", f.FileId, 50), App.P("vid", v.VisitId, 50) });
            if (Helper.IsError(result))
            {
                await Draw(response, c, v.VisitId, "flist", Why(result));
                return response;
            }
            string root = Path.GetFullPath(App.VaultDir) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(App.VaultDir, f.StoredName));
            if (f.StoredName != string.Empty && !f.StoredName.Contains("..") && !f.StoredName.Contains('/') && !f.StoredName.Contains('\\') && path.StartsWith(root, StringComparison.Ordinal))
            {
                TryDelete(path);
            }
            await App.Log(c.U.StaffId, "delete file", v.PetName + " · " + (f.Title != string.Empty ? f.Title : f.FileName), true);
            await Draw(response, c, v.VisitId, "flist,takep", "File deleted.");
            return response;
        }

        private static string TakeFormHtml(Ctx c, VsVisit v)
        {
            if (!c.Can(Key, 'E') || !IsOpen(v))
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-fgrid\"><label class=\"vs-field vs-span2\"><span>Instructions for the owner</span><textarea id=\"vs-summ\" rows=\"5\" maxlength=\"2000\" placeholder=\"How to care for the pet at home, what to watch for, when to call us\">" + Ui.Enc(v.Summary) + "</textarea></label>");
            sb.Append("<label class=\"vs-field\"><span>Recheck around <small>optional</small></span><input type=\"date\" id=\"vs-rdate\" value=\"" + (v.RecheckDate.HasValue ? Ui.Ymd(v.RecheckDate.Value) : string.Empty) + "\"></label><div class=\"vs-racts vs-tsave\"><button type=\"button\" class=\"vs-btn vs-btn-sm vs-btn-accent\" id=\"vs-tgo\" onclick=\"AdminVisitJs.saveTake()\">Save summary</button></div></div><em class=\"vs-err\" id=\"vs-e-take\"></em>");
            return sb.ToString();
        }

        private static async Task<string> TakePrevHtml(VsVisit v, List<VsLine> lines, Dictionary<string, string> s)
        {
            List<VsLine> meds = lines.Where(x => x.Dispense == "home" && x.Status != "cancelled" && (x.ItemType == "medicine" || x.ItemType == "product")).ToList();
            var (vax, _) = await Helper.GetListT<VsVax>("SELECT VAX_ID AS VaxId, ITEM_ID AS ItemId, VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE VISIT_ID = @vid ORDER BY VAX_NAME", new List<SqlParameter> { App.P("vid", v.VisitId, 50) });
            vax ??= new List<VsVax>();
            List<VsFile> files = (await Files(v.VisitId)).Where(x => x.SendOwner).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"vs-letter\"><h3 class=\"vs-h3\">What the owner gets</h3><p class=\"vs-sub\">" + (v.OwnerEmail != string.Empty ? "Emailed to " + Ui.Enc(v.OwnerEmail) + " after checkout." : "The owner has no email address on file, so this is printed at checkout.") + "</p><div class=\"vs-paper\">");
            sb.Append("<p class=\"vs-lt\"><b>" + Ui.Enc(v.PetName) + "</b> &middot; " + Ui.Enc(Ui.Day(v.CheckinAt)) + (v.StaffName != string.Empty ? " &middot; " + Ui.Enc(v.StaffName) : string.Empty) + "</p>");
            bool any = false;
            if (meds.Count > 0)
            {
                any = true;
                sb.Append("<h4>Medicines to give at home</h4><ul>");
                foreach (VsLine m in meds)
                {
                    sb.Append("<li><b>" + Ui.Enc(m.ItemName) + "</b> <span class=\"vs-dim\">" + Ui.Num(m.Qty) + " " + Ui.Enc(m.Unit) + "</span>" + (m.Dose != string.Empty || m.Instructions != string.Empty ? "<span class=\"vs-blk\">" + Ui.Enc(string.Join(" — ", new[] { m.Dose, m.Instructions }.Where(x => x != string.Empty))) + "</span>" : string.Empty) + "</li>");
                }
                sb.Append("</ul>");
            }
            if (vax.Count > 0)
            {
                any = true;
                sb.Append("<h4>Vaccines given</h4><ul>");
                foreach (VsVax x in vax)
                {
                    sb.Append("<li><b>" + Ui.Enc(x.VaxName) + "</b> <span class=\"vs-dim\">next due " + Ui.Enc(Ui.Day(x.DueDate)) + "</span></li>");
                }
                sb.Append("</ul>");
            }
            if (v.Summary.Trim() != string.Empty)
            {
                any = true;
                sb.Append("<h4>Instructions</h4><p class=\"vs-pre\">" + Ui.Enc(v.Summary) + "</p>");
            }
            if (v.RecheckDate.HasValue)
            {
                any = true;
                sb.Append("<h4>Next check</h4><p>Please book a recheck around " + Ui.Enc(Ui.LongDay(v.RecheckDate.Value)) + ".</p>");
            }
            if (files.Count > 0)
            {
                any = true;
                sb.Append("<h4>Attached</h4><ul>");
                foreach (VsFile f in files)
                {
                    sb.Append("<li>" + Ui.Enc(f.Title != string.Empty ? f.Title : f.FileName) + "</li>");
                }
                sb.Append("</ul>");
            }
            if (!any)
            {
                sb.Append("<p class=\"vs-dim\">Nothing to send yet. Medicines that go home, vaccines given, the instructions above and files marked for the owner appear here.</p>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> TakeSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "save summary");
            if (v == null)
            {
                return response;
            }
            if (await Waiting(response, c, v))
            {
                return response;
            }
            if (!IsOpen(v))
            {
                await Draw(response, c, v.VisitId, "takef,takep", "This visit is closed.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            string summ = Text("summ", 2000);
            string rd = Val("rdate", 10);
            DateTime? date = Ui.ParseDay(rd);
            string err = rd != string.Empty && date == null ? "Pick a recheck date, or leave it empty." : date.HasValue && (date.Value < today || date.Value > today.AddYears(2)) ? "The recheck date should be between today and two years from now." : string.Empty;
            if (err != string.Empty)
            {
                response.SetElementContents("vs-e-take", Ui.Enc(err));
                return response;
            }
            string result = await Helper.PutData(new List<string> { "UPDATE VISIT SET SUMMARY = @sum, RECHECK_DATE = @rd, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS IN (N'waiting', N'inroom', N'checkout'); IF @@ROWCOUNT = 0 THROW 50003, N'VISITCLOSED', 1;" }, new List<SqlParameter> { App.P("sum", summ, 2000), App.PDate("rd", date), App.P("vid", v.VisitId, 50) });
            if (Helper.IsError(result))
            {
                response.SetElementContents("vs-e-take", Ui.Enc(Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, "save take-home summary", v.PetName + " · " + v.VisitNo, true);
            response.SetElementContents("vs-e-take", string.Empty);
            await Draw(response, c, v.VisitId, "takep", "Take-home summary saved.");
            return response;
        }

        private async Task<bool> Waiting(ApiResponse response, Ctx c, VsVisit v)
        {
            if (v.Status != "waiting")
            {
                return false;
            }
            response.SetElementContents("vs-mbody", await MoveModal(c, v, string.Empty, string.Empty, string.Empty, true));
            response.ExecuteScript("AdminVisitJs.needStart();");
            return true;
        }

        private static async Task<string> MoveModal(Ctx c, VsVisit v, string room, string staffId, string err, bool start = false)
        {
            List<RoomPick> rooms = await Flow.Rooms();
            List<StaffPick> staff = await Flow.Providers();
            string cur = c.U.OwnOnly ? c.U.StaffId : staffId != string.Empty ? staffId : v.StaffId != string.Empty ? v.StaffId : c.U.StaffId;
            StringBuilder sb = new StringBuilder();
            if (start)
            {
                sb.Append("<h2>Start " + Ui.Enc(v.PetName) + "'s visit</h2><p class=\"vs-mlead\">" + Ui.Enc(v.PetName) + " is still waiting. Pick the room and who sees the patient to start the visit, then carry on.</p>");
            }
            else
            {
                sb.Append("<h2>Move " + Ui.Enc(v.PetName) + "</h2><p class=\"vs-mlead\">" + (v.RoomName != string.Empty ? "Now in " + Ui.Enc(v.RoomName) + ". " : string.Empty) + "Pick the new room and who looks after the patient there.</p>");
            }
            sb.Append("<em class=\"vs-err vs-merr\" id=\"vs-e-room\">" + Ui.Enc(err) + "</em>");
            sb.Append("<label class=\"vs-field vs-mstaff\"><span>Who looks after the patient</span><select id=\"vs-mstaff\"" + (c.U.OwnOnly ? " disabled" : string.Empty) + ">");
            foreach (StaffPick p in staff)
            {
                sb.Append("<option value=\"" + Ui.Attr(p.StaffId) + "\"" + (p.StaffId == cur ? " selected" : string.Empty) + ">" + Ui.Enc(p.StaffName) + "</option>");
            }
            sb.Append("</select></label><h3>Room</h3><div class=\"vs-rgroups\">");
            foreach (IGrouping<string, RoomPick> g in rooms.GroupBy(x => x.RoomtypeId).OrderBy(g => g.First().TypeOrder))
            {
                sb.Append("<fieldset class=\"vs-rg\"><legend>" + Ui.Enc(g.First().TypeName) + "</legend><div class=\"vs-rlist\">");
                foreach (RoomPick x in g.OrderBy(x => x.RoomOrder).ThenBy(x => x.RoomName))
                {
                    bool here = x.BusyVisit == v.VisitId;
                    bool busy = x.BusyVisit != string.Empty;
                    string note = here ? "Here now" : busy ? x.BusyPet : "Free";
                    sb.Append("<label class=\"vs-room" + (busy ? " vs-busy" : string.Empty) + "\"><input type=\"radio\" name=\"vs-room\" value=\"" + Ui.Attr(x.RoomId) + "\" onchange=\"AdminVisitJs.roomPick()\"" + (busy ? " disabled" : string.Empty) + (!busy && x.RoomId == room ? " checked" : string.Empty) + "><span><b>" + Ui.Enc(x.RoomName) + "</b><small>" + Ui.Enc(note) + "</small></span></label>");
                }
                sb.Append("</div></fieldset>");
            }
            sb.Append("</div><div class=\"vs-mfoot\"><button type=\"button\" class=\"vs-btn vs-btn-ghost\" onclick=\"AdminVisitJs.closeModal()\">Cancel</button>" + (start ? "<button type=\"button\" class=\"vs-btn vs-btn-accent\" id=\"vs-stgo\" onclick=\"AdminVisitJs.startGo()\">Start visit</button>" : "<button type=\"button\" class=\"vs-btn vs-btn-accent\" onclick=\"AdminVisitJs.moveGo()\">Move patient</button>") + "</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> StartAsk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "start visit");
            if (v == null)
            {
                return response;
            }
            if (!await Waiting(response, c, v))
            {
                await Draw(response, c, v.VisitId, "head", "This visit has already started.");
            }
            return response;
        }

        public async Task<ApiResponse> StartGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript("AdminVisitJs.unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "start visit");
            if (v == null)
            {
                return response;
            }
            if (v.Status != "waiting")
            {
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "head", IsOpen(v) ? "This visit has already started." : "This visit is closed.");
                if (IsOpen(v))
                {
                    response.ExecuteScript("AdminVisitJs.resume();");
                }
                return response;
            }
            string roomId = CleanId(Val("roomid", 50));
            string staffId = c.U.OwnOnly ? c.U.StaffId : CleanId(Val("staffid", 50));
            string err = roomId == string.Empty ? "Pick a room." : staffId == string.Empty ? "Pick who sees the patient." : await Flow.Start(c, v.VisitId, roomId, staffId, await App.Settings());
            if (err != string.Empty)
            {
                VsVisit? fresh = await One(v.VisitId);
                if (fresh != null && fresh.Status == "waiting")
                {
                    response.SetElementContents("vs-mbody", await MoveModal(c, fresh, roomId, staffId, err, true));
                    return response;
                }
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "head", err);
                return response;
            }
            VsVisit? after = await One(v.VisitId);
            response.ExecuteScript("AdminVisitJs.closeModal();");
            await Draw(response, c, v.VisitId, "head", v.PetName + "'s visit started" + (after != null && after.RoomName != string.Empty ? " in " + after.RoomName : string.Empty) + ".");
            response.ExecuteScript("AdminVisitJs.resume();");
            return response;
        }

        public async Task<ApiResponse> MovePick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "move room");
            if (v == null)
            {
                return response;
            }
            if (v.Status != "inroom")
            {
                await Draw(response, c, v.VisitId, "head", "Only a visit in a room can be moved.");
                return response;
            }
            response.SetElementContents("vs-mbody", await MoveModal(c, v, string.Empty, string.Empty, string.Empty));
            response.ExecuteScript("AdminVisitJs.shown('');");
            return response;
        }

        public async Task<ApiResponse> MoveGo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "move room");
            if (v == null)
            {
                return response;
            }
            string roomId = CleanId(Val("roomid", 50));
            string staffId = c.U.OwnOnly ? c.U.StaffId : CleanId(Val("staffid", 50));
            string err = roomId == string.Empty ? "Pick a room." : staffId == string.Empty ? "Pick who looks after the patient." : await Flow.Move(c, v.VisitId, roomId, staffId, await App.Settings());
            if (err != string.Empty)
            {
                VsVisit? fresh = await One(v.VisitId);
                if (fresh != null && fresh.Status == "inroom")
                {
                    response.SetElementContents("vs-mbody", await MoveModal(c, fresh, roomId, staffId, err));
                    return response;
                }
                response.ExecuteScript("AdminVisitJs.closeModal();");
                await Draw(response, c, v.VisitId, "head", err);
                return response;
            }
            VsVisit? after = await One(v.VisitId);
            response.ExecuteScript("AdminVisitJs.closeModal();");
            await Draw(response, c, v.VisitId, "head", v.PetName + " moved to " + (after != null && after.RoomName != string.Empty ? after.RoomName : "the new room") + ".");
            return response;
        }

        public async Task<ApiResponse> Ready()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            VsVisit? v = await Reach(response, c, 'E', "ready for checkout");
            if (v == null)
            {
                return response;
            }
            string err = await Flow.Ready(c, v.VisitId, await App.Settings());
            await Draw(response, c, v.VisitId, "head", err != string.Empty ? err : v.PetName + " is ready for checkout.");
            return response;
        }
    }
}
