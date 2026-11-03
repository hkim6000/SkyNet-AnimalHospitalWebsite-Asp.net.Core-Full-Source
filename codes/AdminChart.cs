using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminChart : WebPage
    {
        private const string Key = "charts";
        private const string PetCols = "P.PET_ID AS PetId, P.CLIENT_ID AS ClientId, P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SEX AS Sex, P.FIXED AS Fixed, P.BIRTH_DATE AS BirthDate, P.COLOR AS Color, P.MICROCHIP AS Microchip, P.ALERT AS Alert, P.PHOTO AS Photo, P.ACTIVE AS Active, P.DECEASED_DATE AS DeceasedDate, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS OwnerPhone, ISNULL(C.ALERT, N'') AS OwnerAlert";
        private const string FindCols = "P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.PHOTO AS Photo, P.ALERT AS Alert, P.MICROCHIP AS Microchip, P.ACTIVE AS Active, P.DECEASED_DATE AS DeceasedDate, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS OwnerPhone";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Patient chart | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ch-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ch"))
                .Replace("{plhd_open}", Ui.Attr(CleanId(QueryValue("p"))));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminChartJs.expired();");
            }
            return c;
        }

        private static string Denied()
        {
            return "<div class=\"ch-panel ch-deny\"><h1>Patient charts</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"ch-btn ch-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminChartJs.out();");
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
            response.SetElementContents("ch-menu", Auth.Menu("ch", "AdminChartJs", "AdminChart", c));
            response.SetElementContents("ch-who", Auth.Who("ch", c));
            response.ExecuteScript("AdminChartJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ch-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string pid = CleanId(GetDataValue("petid"));
            if (pid == string.Empty)
            {
                response.SetElementContents("ch-main", await PickerHtml(s));
                response.ExecuteScript("AdminChartJs.picker();");
                return response;
            }
            response.SetElementContents("ch-main", await ChartHtml(c, s, pid));
            response.ExecuteScript("AdminChartJs.ready();");
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
                response.SetElementContents("ch-main", Denied());
                return response;
            }
            string term = Ui.OneLine(GetDataValue("term"), 80);
            response.SetElementContents("ch-found", await FoundHtml(await App.Settings(), term));
            return response;
        }

        public async Task<ApiResponse> Trend()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ch-main", Denied());
                return response;
            }
            string pid = CleanId(GetDataValue("petid"));
            string fld = Ui.Clip(GetDataValue("fld"), 120);
            List<ChResult> results = await Results(pid);
            response.SetElementContents("ch-trend", TrendHtml(results, fld));
            return response;
        }

        private static string Like(string t)
        {
            return "%" + t.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private static string Dig(string col)
        {
            return "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(" + col + ", N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), N'+', N'')";
        }

        private static string Short(DateTime d)
        {
            return d.ToString("MMM d, yyyy", Ui.Us);
        }

        private static string Lb(decimal v)
        {
            return Ui.Num(Math.Round(v, 2)) + " lb";
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

        private static string SexText(string sex, bool fix)
        {
            return sex switch
            {
                "m" => fix ? "Male, neutered" : "Male",
                "f" => fix ? "Female, spayed" : "Female",
                _ => fix ? "Fixed" : "Sex not known"
            };
        }

        private static string Chip(string status)
        {
            string text = status switch
            {
                "booked" => "Booked",
                "confirmed" => "Confirmed",
                "waiting" => "Waiting",
                "inroom" => "In progress",
                "checkout" => "At checkout",
                "closed" => "Done",
                "active" => "Active",
                "resolved" => "Resolved",
                "ruleout" => "Ruled out",
                "ok" => "Up to date",
                "due" => "Due soon",
                "overdue" => "Overdue",
                "ordered" => "Ordered",
                "done" => "Done",
                _ => status
            };
            return "<span class=\"ch-st ch-st-" + Ui.Attr(status) + "\">" + Ui.Enc(text) + "</span>";
        }

        private static string TypeName(string t)
        {
            return t switch
            {
                "clinic" => "Clinic",
                "surgery" => "Surgery",
                "grooming" => "Grooming",
                "boarding" => "Boarding",
                _ => t
            };
        }

        private static string KindName(string k)
        {
            return k switch
            {
                "exam" => "Exam note",
                "surgery" => "Surgery note",
                "grooming" => "Grooming note",
                "progress" => "Progress note",
                "phone" => "Phone call",
                _ => "Note"
            };
        }

        private static string ItemTypeName(string t)
        {
            return t switch
            {
                "practice" => "Service",
                "medicine" => "Medicine",
                "test" => "Test",
                "vaccine" => "Vaccine",
                "grooming" => "Grooming",
                "boarding" => "Boarding",
                "product" => "Product",
                "fee" => "Fee",
                _ => t
            };
        }

        private static string Val(decimal v, int dec)
        {
            return dec >= 0 && dec <= 4 ? v.ToString("F" + dec.ToString(Ui.Inv), Ui.Inv) : Ui.Num(v);
        }

        private static string Range(ChResult r)
        {
            if (r.LowValue.HasValue && r.HighValue.HasValue)
            {
                return Val(r.LowValue.Value, r.Decimals) + "&ndash;" + Val(r.HighValue.Value, r.Decimals);
            }
            if (r.LowValue.HasValue)
            {
                return "&ge; " + Val(r.LowValue.Value, r.Decimals);
            }
            if (r.HighValue.HasValue)
            {
                return "&le; " + Val(r.HighValue.Value, r.Decimals);
            }
            return string.Empty;
        }

        private static string FlagHtml(string flag)
        {
            return flag == "H" ? "<span class=\"ch-flag ch-flag-h\" title=\"High\">H</span>" : flag == "L" ? "<span class=\"ch-flag ch-flag-l\" title=\"Low\">L</span>" : string.Empty;
        }

        private static string Text(string v)
        {
            return "<span class=\"ch-pre\">" + Ui.Enc(v.Trim()) + "</span>";
        }

        private static string FindRow(ChFind r, string extra)
        {
            bool gone = r.DeceasedDate.HasValue;
            StringBuilder sb = new StringBuilder();
            sb.Append("<li><a class=\"ch-prow" + (r.Active && !gone ? string.Empty : " ch-off") + "\" href=\"AdminChart?p=" + Uri.EscapeDataString(r.PetId) + "\">" + Ui.Avatar("ch", r.PetId, r.PetName, r.Photo) + "<span class=\"ch-pinfo\"><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName + (r.Breed != string.Empty ? " · " + r.Breed : string.Empty)) + "</small>");
            List<string> bits = new List<string>();
            if (r.OwnerName != string.Empty)
            {
                bits.Add(r.OwnerName);
            }
            if (r.OwnerPhone != string.Empty)
            {
                bits.Add(r.OwnerPhone);
            }
            if (r.Microchip != string.Empty)
            {
                bits.Add("Chip " + r.Microchip);
            }
            sb.Append("<small>" + Ui.Enc(string.Join(" · ", bits)) + "</small></span><span class=\"ch-pside\">" + extra);
            if (gone)
            {
                sb.Append("<span class=\"ch-tag\">Passed away</span>");
            }
            else if (!r.Active)
            {
                sb.Append("<span class=\"ch-tag\">Inactive</span>");
            }
            if (r.Alert != string.Empty)
            {
                sb.Append("<span class=\"ch-tag ch-tag-alert\" title=\"" + Ui.Attr(r.Alert) + "\">" + Ui.Enc(r.Alert) + "</span>");
            }
            sb.Append("</span></a></li>");
            return sb.ToString();
        }

        private static async Task<string> PickerHtml(Dictionary<string, string> s)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            bool h24 = App.S(s, "clock24") == "1";
            string sql = "SELECT " + FindCols + ", V.CHECKIN_AT AS SeenAt, V.REASON AS Reason FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY WHERE V.CHECKIN_AT >= @d0 AND V.CHECKIN_AT < @d1 AND V.STATUS <> N'cancelled' AND V.VISIT_TYPE <> N'boarding' AND NOT EXISTS (SELECT 1 FROM VISIT Z WHERE Z.PET_ID = V.PET_ID AND Z.CHECKIN_AT >= @d0 AND Z.CHECKIN_AT < @d1 AND Z.STATUS <> N'cancelled' AND Z.VISIT_TYPE <> N'boarding' AND (Z.CHECKIN_AT > V.CHECKIN_AT OR (Z.CHECKIN_AT = V.CHECKIN_AT AND Z.VISIT_ID > V.VISIT_ID))) ORDER BY V.CHECKIN_AT DESC";
            var (todays, _) = await Helper.GetListT<ChFind>(sql, new List<SqlParameter> { App.PDate("d0", today), App.PDate("d1", today.AddDays(1)) });
            var (weeks, _) = await Helper.GetListT<ChFind>(sql, new List<SqlParameter> { App.PDate("d0", today.AddDays(-6)), App.PDate("d1", today) });
            todays ??= new List<ChFind>();
            weeks ??= new List<ChFind>();
            HashSet<string> seen = new HashSet<string>(todays.Select(x => x.PetId));
            weeks = weeks.Where(x => !seen.Contains(x.PetId)).Take(40).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ch-ptop\"><div><h1>Patient charts</h1><p>Find a pet to see its whole medical history in one place.</p></div></div>");
            sb.Append("<div class=\"ch-panel ch-pick\"><label class=\"ch-field\"><span>Find a pet</span><input type=\"search\" id=\"ch-term\" maxlength=\"80\" placeholder=\"Pet name, owner name, phone or microchip\" autocomplete=\"off\" oninput=\"AdminChartJs.typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();AdminChartJs.find();}\"></label><div id=\"ch-found\"></div></div>");
            sb.Append("<div class=\"ch-recent\"><section class=\"ch-panel\"><h2>Seen today</h2>");
            if (todays.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">No patients seen yet today.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ch-plist\">");
                foreach (ChFind r in todays)
                {
                    sb.Append(FindRow(r, "<span class=\"ch-seen\">" + Ui.Enc(Ui.Hm(r.SeenAt ?? now, h24)) + (r.Reason != string.Empty ? " &middot; " + Ui.Enc(Ui.OneLine(r.Reason, 40)) : string.Empty) + "</span>"));
                }
                sb.Append("</ul>");
            }
            sb.Append("</section><section class=\"ch-panel\"><h2>Seen this week</h2>");
            if (weeks.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">No other patients seen in the last 7 days.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ch-plist\">");
                foreach (ChFind r in weeks)
                {
                    sb.Append(FindRow(r, "<span class=\"ch-seen\">" + Ui.Enc((r.SeenAt ?? now).ToString("ddd MMM d", Ui.Us)) + (r.Reason != string.Empty ? " &middot; " + Ui.Enc(Ui.OneLine(r.Reason, 40)) : string.Empty) + "</span>"));
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div>");
            return sb.ToString();
        }

        private static async Task<string> FoundHtml(Dictionary<string, string> s, string term)
        {
            if (term.Length == 0)
            {
                return string.Empty;
            }
            string digits = Ui.Digits(term);
            string sql = "SELECT TOP 20 " + FindCols + ", NULL AS SeenAt, N'' AS Reason FROM PET P LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY WHERE P.PET_NAME LIKE @tl OR P.MICROCHIP LIKE @tl OR C.FIRST_NAME LIKE @tl OR C.LAST_NAME LIKE @tl OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @tl OR (@dg <> N'' AND (" + Dig("C.PHONE") + " LIKE @dl OR " + Dig("C.PHONE2") + " LIKE @dl)) ORDER BY CASE WHEN P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL THEN 0 ELSE 1 END, P.PET_NAME, C.LAST_NAME, P.PET_ID";
            var (rows, emsg) = await Helper.GetListT<ChFind>(sql, new List<SqlParameter>
            {
                App.P("tl", Like(term), 300),
                App.P("dl", Like(digits), 300),
                App.P("dg", digits.Length >= 3 ? digits : string.Empty, 40)
            });
            if (emsg != string.Empty || rows == null)
            {
                return "<p class=\"ch-dim ch-fmsg\">The search could not be run. Try again in a moment.</p>";
            }
            if (rows.Count == 0)
            {
                return "<p class=\"ch-dim ch-fmsg\">No pet matches &ldquo;" + Ui.Enc(term) + "&rdquo;.</p>";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<ul class=\"ch-plist ch-found\">");
            foreach (ChFind r in rows)
            {
                sb.Append(FindRow(r, string.Empty));
            }
            sb.Append("</ul>");
            if (rows.Count == 20)
            {
                sb.Append("<p class=\"ch-dim ch-fmsg\">Showing the first 20 matches. Type more to narrow it down.</p>");
            }
            return sb.ToString();
        }

        private static async Task<ChPet?> Pet(string id)
        {
            var (row, emsg) = await Helper.GetOneT<ChPet>("SELECT " + PetCols + " FROM PET P LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID WHERE P.PET_ID = @pid", new List<SqlParameter> { App.P("pid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<ChResult>> Results(string pid)
        {
            if (pid == string.Empty)
            {
                return new List<ChResult>();
            }
            var (list, _) = await Helper.GetListT<ChResult>("SELECT R.LINE_ID AS LineId, R.VISIT_ID AS VisitId, ISNULL(O.ITEM_ID, N'') AS ItemId, ISNULL(O.ITEM_NAME, N'Test') AS ItemName, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.LOW_VALUE AS LowValue, R.HIGH_VALUE AS HighValue, R.FLAG AS Flag, ISNULL(F.FIELD_ORDER, 999) AS FieldOrder, ISNULL(F.DECIMALS, -1) AS Decimals, V.CHECKIN_AT AS VisitAt FROM RESULT R JOIN VISIT V ON V.VISIT_ID = R.VISIT_ID LEFT JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID LEFT JOIN TESTFIELD F ON F.FIELD_ID = R.FIELD_ID WHERE R.PET_ID = @pid AND V.STATUS <> N'cancelled' AND ISNULL(O.STATUS, N'') <> N'cancelled' ORDER BY V.CHECKIN_AT, R.LINE_ID, ISNULL(F.FIELD_ORDER, 999), R.FIELD_NAME", new List<SqlParameter> { App.P("pid", pid, 50) });
            return list ?? new List<ChResult>();
        }

        private static string FieldKey(ChResult r)
        {
            return r.ItemId + "|" + r.FieldName;
        }

        private static async Task<string> ChartHtml(Ctx c, Dictionary<string, string> s, string pid)
        {
            ChPet? p = await Pet(pid);
            if (p == null)
            {
                return "<div class=\"ch-panel ch-deny\"><h1>Pet not found</h1><p>This pet may have been deleted, or the link is wrong.</p><a class=\"ch-btn ch-btn-ghost\" href=\"AdminChart\">Find another pet</a></div>";
            }
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            bool h24 = App.S(s, "clock24") == "1";
            List<SqlParameter> P() => new List<SqlParameter> { App.P("pid", p.PetId, 50) };
            var (visits, verr) = await Helper.GetListT<ChVisit>("SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.VISIT_TYPE AS VisitType, V.STATUS AS Status, V.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.REASON AS Reason, ISNULL(B.BOOK_NAME, N'') AS BookName, V.CHECKIN_AT AS CheckinAt, V.CLOSED_AT AS ClosedAt, V.PLAN_OUT AS PlanOut, V.RECHECK_DATE AS RecheckDate, V.SUMMARY AS Summary FROM VISIT V LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE V.PET_ID = @pid AND V.STATUS <> N'cancelled' ORDER BY V.CHECKIN_AT DESC, V.VISIT_NO DESC", P());
            if (verr != string.Empty || visits == null)
            {
                return "<div class=\"ch-panel\"><p>The chart could not be loaded. Try again in a moment.</p></div>";
            }
            var (notes, _) = await Helper.GetListT<ChNote>("SELECT N.NOTE_ID AS NoteId, N.VISIT_ID AS VisitId, ISNULL(S.STAFF_NAME, N'') AS StaffName, N.NOTE_KIND AS NoteKind, N.SUBJECTIVE AS Subjective, N.OBJECTIVE AS Objective, N.ASSESSMENT AS Assessment, N.PLAN_TEXT AS PlanText, N.TEMP_F AS TempF, N.HEART_RATE AS HeartRate, N.RESP_RATE AS RespRate, N.WEIGHT_LB AS WeightLb, N.PARENT_ID AS ParentId, N.CREATED_UTC AS CreatedUtc, N.SIGNED_UTC AS SignedUtc FROM VISITNOTE N JOIN VISIT V ON V.VISIT_ID = N.VISIT_ID LEFT JOIN STAFF S ON S.STAFF_ID = N.STAFF_ID WHERE V.PET_ID = @pid AND V.STATUS <> N'cancelled' ORDER BY N.CREATED_UTC, N.NOTE_ID", P());
            var (dxs, _) = await Helper.GetListT<ChDx>("SELECT D.DX_ID AS DxId, D.VISIT_ID AS VisitId, D.DX_TEXT AS DxText, D.DX_STATUS AS DxStatus, ISNULL(S.STAFF_NAME, N'') AS StaffName, D.CREATED_UTC AS CreatedUtc, V.CHECKIN_AT AS VisitAt FROM DIAGNOSIS D JOIN VISIT V ON V.VISIT_ID = D.VISIT_ID LEFT JOIN STAFF S ON S.STAFF_ID = D.STAFF_ID WHERE D.PET_ID = @pid AND V.STATUS <> N'cancelled' ORDER BY D.CREATED_UTC DESC, D.DX_ID", P());
            var (orders, _) = await Helper.GetListT<ChOrder>("SELECT O.LINE_ID AS LineId, O.VISIT_ID AS VisitId, O.ITEM_NAME AS ItemName, O.ITEM_TYPE AS ItemType, O.QTY AS Qty, O.UNIT AS Unit, O.DOSE AS Dose, O.INSTRUCTIONS AS Instructions, O.DISPENSE AS Dispense, O.STATUS AS Status, O.NOTE AS Note, V.CHECKIN_AT AS VisitAt FROM ORDERLINE O JOIN VISIT V ON V.VISIT_ID = O.VISIT_ID WHERE V.PET_ID = @pid AND V.STATUS <> N'cancelled' AND O.STATUS <> N'cancelled' ORDER BY O.VISIT_ID, O.LINE_ORDER, O.ORDERED_UTC", P());
            var (files, _) = await Helper.GetListT<ChFile>("SELECT F.FILE_ID AS FileId, F.VISIT_ID AS VisitId, F.TITLE AS Title, F.FILE_NAME AS FileName, F.MIME AS Mime, F.BYTES AS Bytes, F.UPLOADED_UTC AS UploadedUtc FROM VISITFILE F WHERE F.PET_ID = @pid ORDER BY F.UPLOADED_UTC", P());
            var (weights, _) = await Helper.GetListT<ChWeight>("SELECT WEIGHED_AT AS WeighedAt, LB AS Lb FROM PETWEIGHT WHERE PET_ID = @pid ORDER BY WEIGHED_AT", P());
            var (vaxes, _) = await Helper.GetListT<ChVax>("SELECT VAX_ID AS VaxId, VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE PET_ID = @pid ORDER BY GIVEN_DATE DESC, VAX_ID DESC", P());
            var (next, _) = await Helper.GetOneT<ChAppt>("SELECT TOP 1 A.APPT_ID AS ApptId, A.START_AT AS StartAt, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.STATUS AS Status FROM APPOINTMENT A LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID WHERE A.PET_ID = @pid AND A.START_AT >= @now AND A.STATUS IN (N'booked', N'confirmed') ORDER BY A.START_AT", new List<SqlParameter> { App.P("pid", p.PetId, 50), App.PDate("now", now) });
            List<ChResult> results = await Results(p.PetId);
            notes ??= new List<ChNote>();
            dxs ??= new List<ChDx>();
            orders ??= new List<ChOrder>();
            files ??= new List<ChFile>();
            weights ??= new List<ChWeight>();
            vaxes ??= new List<ChVax>();
            StringBuilder sb = new StringBuilder();
            sb.Append(HeadHtml(c, s, p, weights, now));
            sb.Append("<div class=\"ch-grid\"><aside class=\"ch-aside\">");
            sb.Append(SummaryHtml(c, s, p, dxs, orders, vaxes, weights, next, now));
            sb.Append("</aside><div class=\"ch-maincol\">");
            List<ChResult> numeric = results.Where(r => r.ValueNum.HasValue).ToList();
            sb.Append("<div class=\"ch-tabs\" role=\"tablist\"><button type=\"button\" class=\"ch-tab ch-on\" role=\"tab\" aria-selected=\"true\" data-tab=\"vis\" onclick=\"AdminChartJs.tab('vis')\">Visit history <span>" + visits.Count.ToString(Ui.Inv) + "</span></button><button type=\"button\" class=\"ch-tab\" role=\"tab\" aria-selected=\"false\" data-tab=\"lab\" onclick=\"AdminChartJs.tab('lab')\">Lab trends <span>" + numeric.Select(r => r.LineId).Distinct().Count().ToString(Ui.Inv) + "</span></button></div>");
            sb.Append("<section class=\"ch-pane\" id=\"ch-pane-vis\">" + TimelineHtml(c, s, visits, notes, dxs, orders, results, files, h24) + "</section>");
            sb.Append("<section class=\"ch-pane ch-pane-off\" id=\"ch-pane-lab\"><h2 class=\"ch-ponly ch-ph2\">Lab trends</h2>" + LabHtml(numeric, results) + "</section>");
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string HeadHtml(Ctx c, Dictionary<string, string> s, ChPet p, List<ChWeight> weights, DateTime now)
        {
            bool gone = p.DeceasedDate.HasValue;
            StringBuilder sb = new StringBuilder();
            sb.Append("<p class=\"ch-ponly ch-pline\">" + Ui.Enc(App.S(s, "name", "Animal Hospital")) + " &middot; Patient chart &middot; printed " + Ui.Enc(now.ToString("MMM d, yyyy h:mm tt", Ui.Us)) + " by " + Ui.Enc(c.U.StaffName) + "</p>");
            sb.Append("<a class=\"ch-back\" href=\"AdminChart\">&larr; Find another pet</a>");
            sb.Append("<div class=\"ch-panel ch-head\"><div class=\"ch-hwho\">" + Ui.Avatar("ch", p.PetId, p.PetName, p.Photo, "lg") + "<div class=\"ch-hinfo\"><h1>" + Ui.Enc(p.PetName));
            if (gone)
            {
                sb.Append(" <span class=\"ch-st ch-st-off\">Passed away " + Short(p.DeceasedDate!.Value) + "</span>");
            }
            else if (!p.Active)
            {
                sb.Append(" <span class=\"ch-st ch-st-off\">Inactive</span>");
            }
            sb.Append("</h1>");
            List<string> bits = new List<string> { p.SpeciesName };
            if (p.Breed != string.Empty)
            {
                bits.Add(p.Breed);
            }
            bits.Add(SexText(p.Sex, p.Fixed));
            string age = Age(p.BirthDate, now.Date, p.DeceasedDate);
            if (age != string.Empty)
            {
                bits.Add(age + (gone ? " when passed" : " old"));
            }
            sb.Append("<p class=\"ch-sub\">" + Ui.Enc(string.Join(" · ", bits)) + "</p><dl class=\"ch-facts\">");
            sb.Append("<div><dt>Colour</dt><dd>" + (p.Color != string.Empty ? Ui.Enc(p.Color) : "<i class=\"ch-dim\">Not noted</i>") + "</dd></div>");
            sb.Append("<div><dt>Microchip</dt><dd>" + (p.Microchip != string.Empty ? Ui.Enc(p.Microchip) : "<i class=\"ch-dim\">None</i>") + "</dd></div>");
            ChWeight? lw = weights.Count > 0 ? weights[^1] : null;
            sb.Append("<div><dt>Weight</dt><dd>" + (lw != null ? "<b>" + Lb(lw.Lb) + "</b> <small class=\"ch-dim\">" + Short(lw.WeighedAt) + "</small>" : "<i class=\"ch-dim\">Not weighed yet</i>") + "</dd></div>");
            string owner = p.OwnerName != string.Empty ? Ui.Enc(p.OwnerName) : "<i class=\"ch-dim\">No owner</i>";
            sb.Append("<div><dt>Owner</dt><dd>" + (c.Can("clients", 'V') && p.OwnerName != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(p.ClientId) + "\">" + owner + "</a>" : owner) + (p.OwnerPhone != string.Empty ? " &middot; <a href=\"tel:" + Ui.Attr(Ui.Digits(p.OwnerPhone)) + "\">" + Ui.Enc(p.OwnerPhone) + "</a>" : string.Empty) + "</dd></div>");
            sb.Append("</dl></div></div><div class=\"ch-acts\">");
            if (c.Can("appts", 'A') && p.Active && !gone)
            {
                sb.Append("<a class=\"ch-btn ch-btn-sm ch-btn-accent\" href=\"AdminAppts?p=" + Uri.EscapeDataString(p.PetId) + "\">Book</a>");
            }
            if (c.Can("pets", 'V'))
            {
                sb.Append("<a class=\"ch-btn ch-btn-sm ch-btn-ghost\" href=\"AdminPets?p=" + Uri.EscapeDataString(p.PetId) + "\">Pet record</a>");
            }
            sb.Append("<button type=\"button\" class=\"ch-btn ch-btn-sm\" onclick=\"AdminChartJs.print()\">Print chart</button></div></div>");
            if (p.Alert != string.Empty)
            {
                sb.Append("<div class=\"ch-alert\" role=\"alert\"><b>Alert</b><span>" + Ui.Enc(p.Alert) + "</span></div>");
            }
            if (p.OwnerAlert != string.Empty)
            {
                sb.Append("<div class=\"ch-alert ch-alert-owner\" role=\"alert\"><b>Owner alert</b><span>" + Ui.Enc(p.OwnerAlert) + "</span></div>");
            }
            return sb.ToString();
        }

        private static string SummaryHtml(Ctx c, Dictionary<string, string> s, ChPet p, List<ChDx> dxs, List<ChOrder> orders, List<ChVax> vaxes, List<ChWeight> weights, ChAppt? next, DateTime now)
        {
            DateTime today = now.Date;
            bool h24 = App.S(s, "clock24") == "1";
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"ch-card\"><h3>Active problems</h3>");
            var problems = dxs.GroupBy(d => d.DxText.Trim().ToLowerInvariant())
                .Select(g => new { Last = g.OrderByDescending(d => d.CreatedUtc).First(), First = g.Min(d => d.VisitAt), Count = g.Count() })
                .Where(x => x.Last.DxStatus == "active")
                .OrderByDescending(x => x.Last.CreatedUtc)
                .ToList();
            if (problems.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">No active problems.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ch-slist\">");
                foreach (var x in problems)
                {
                    string since = x.First.Date < x.Last.VisitAt.Date ? " &middot; since " + Short(x.First) : string.Empty;
                    sb.Append("<li><b>" + Ui.Enc(x.Last.DxText) + "</b><small>Last noted " + Short(x.Last.VisitAt) + since + (x.Last.StaffName != string.Empty ? " &middot; " + Ui.Enc(x.Last.StaffName) : string.Empty) + "</small></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"ch-card\"><h3>Medicines sent home</h3>");
            var meds = orders.Where(o => o.ItemType == "medicine" && o.Dispense == "home" && o.VisitAt >= today.AddMonths(-12))
                .GroupBy(o => o.ItemName.Trim().ToLowerInvariant())
                .Select(g => new { Last = g.OrderByDescending(o => o.VisitAt).First(), Count = g.Count() })
                .OrderByDescending(x => x.Last.VisitAt)
                .ToList();
            if (meds.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">None in the last 12 months.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ch-slist\">");
                foreach (var x in meds)
                {
                    sb.Append("<li><b>" + Ui.Enc(x.Last.ItemName) + "</b>" + (x.Last.Dose != string.Empty ? "<span>" + Ui.Enc(x.Last.Dose) + "</span>" : string.Empty) + (x.Last.Instructions != string.Empty ? "<span class=\"ch-dim\">" + Ui.Enc(x.Last.Instructions) + "</span>" : string.Empty) + "<small>Sent home " + Short(x.Last.VisitAt) + (x.Count > 1 ? " &middot; " + Ui.Plural(x.Count, "time", "times") + " this year" : string.Empty) + "</small></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"ch-card\"><h3>Vaccines</h3>");
            DateTime soon = today.AddDays(App.SInt(s, "remind.days", 30));
            List<ChVax> latest = vaxes.GroupBy(v => v.VaxName.Trim().ToLowerInvariant()).Select(g => g.First()).OrderBy(v => v.DueDate).ThenBy(v => v.VaxName).ToList();
            if (latest.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">No vaccines on file.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ch-vlist\">");
                foreach (ChVax v in latest)
                {
                    string st = v.DueDate.Date < today ? "overdue" : v.DueDate.Date <= soon ? "due" : "ok";
                    sb.Append("<li><span><b>" + Ui.Enc(v.VaxName) + "</b><small>Given " + Short(v.GivenDate) + " &middot; due " + Short(v.DueDate) + "</small></span>" + Chip(st) + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"ch-card\"><h3>Weight</h3>");
            if (weights.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">No weights yet.</p>");
            }
            else
            {
                ChWeight last = weights[^1];
                string trend = string.Empty;
                if (weights.Count > 1)
                {
                    decimal d = last.Lb - weights[^2].Lb;
                    trend = d == 0 ? " <small class=\"ch-dim\">same as before</small>" : " <small class=\"ch-dim\">" + (d > 0 ? "up " : "down ") + Lb(Math.Abs(d)) + " since " + Short(weights[^2].WeighedAt) + "</small>";
                }
                sb.Append("<p class=\"ch-wnow\"><b>" + Lb(last.Lb) + "</b> on " + Short(last.WeighedAt) + trend + "</p>");
                sb.Append(WeightChart(weights));
            }
            sb.Append("</section>");
            sb.Append("<section class=\"ch-card\"><h3>Next appointment</h3>");
            if (next == null)
            {
                sb.Append("<p class=\"ch-dim\">Nothing booked.</p>");
            }
            else
            {
                string inner = "<b>" + Ui.Enc(next.StartAt.ToString("ddd MMM d, yyyy", Ui.Us)) + " &middot; " + Ui.Hm(next.StartAt, h24) + "</b><small>" + Ui.Enc(next.BookName) + (next.StaffName != string.Empty ? " with " + Ui.Enc(next.StaffName) : string.Empty) + "</small>";
                sb.Append(c.Can("appts", 'V') && c.Mine(next.StaffId) ? "<a class=\"ch-next\" href=\"AdminAppts?a=" + Uri.EscapeDataString(next.ApptId) + "\">" + inner + "</a>" : "<div class=\"ch-next\">" + inner + "</div>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string F(double v)
        {
            return v.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static string WeightChart(List<ChWeight> pts)
        {
            if (pts.Count < 2)
            {
                return string.Empty;
            }
            double w = 340, h = 150, l = 40, r = 10, t = 12, b = 28;
            double min = (double)pts.Min(x => x.Lb), max = (double)pts.Max(x => x.Lb);
            double pad = Math.Max((max - min) * 0.15, Math.Max(max * 0.03, 0.2));
            min = Math.Max(0, min - pad);
            max += pad;
            double t0 = pts[0].WeighedAt.Ticks, t1 = pts[^1].WeighedAt.Ticks;
            double X(DateTime d) => t1 > t0 ? l + (d.Ticks - t0) / (t1 - t0) * (w - l - r) : (l + w - r) / 2;
            double Y(decimal v) => t + (max - (double)v) / (max - min) * (h - t - b);
            StringBuilder sb = new StringBuilder();
            sb.Append("<svg class=\"ch-chart\" viewBox=\"0 0 " + F(w) + " " + F(h) + "\" role=\"img\" aria-label=\"Weight from " + Ui.Attr(Lb(pts[0].Lb)) + " to " + Ui.Attr(Lb(pts[^1].Lb)) + "\">");
            foreach (double gv in new[] { min, (min + max) / 2, max })
            {
                double gy = t + (max - gv) / (max - min) * (h - t - b);
                sb.Append("<line class=\"ch-cg\" x1=\"" + F(l) + "\" x2=\"" + F(w - r) + "\" y1=\"" + F(gy) + "\" y2=\"" + F(gy) + "\"/><text class=\"ch-ct\" x=\"" + F(l - 6) + "\" y=\"" + F(gy + 4) + "\" text-anchor=\"end\">" + gv.ToString("0.#", CultureInfo.InvariantCulture) + "</text>");
            }
            sb.Append("<text class=\"ch-ct\" x=\"" + F(l) + "\" y=\"" + F(h - 8) + "\">" + pts[0].WeighedAt.ToString("MMM yyyy", Ui.Us) + "</text><text class=\"ch-ct\" x=\"" + F(w - r) + "\" y=\"" + F(h - 8) + "\" text-anchor=\"end\">" + pts[^1].WeighedAt.ToString("MMM yyyy", Ui.Us) + "</text>");
            sb.Append("<polyline class=\"ch-cl\" points=\"" + string.Join(" ", pts.Select(x => F(X(x.WeighedAt)) + "," + F(Y(x.Lb)))) + "\"/>");
            foreach (ChWeight x in pts)
            {
                sb.Append("<g><circle class=\"ch-chit\" cx=\"" + F(X(x.WeighedAt)) + "\" cy=\"" + F(Y(x.Lb)) + "\" r=\"10\"/><circle class=\"ch-cd\" cx=\"" + F(X(x.WeighedAt)) + "\" cy=\"" + F(Y(x.Lb)) + "\" r=\"4\"/><title>" + Ui.Enc(Short(x.WeighedAt) + ": " + Lb(x.Lb)) + "</title></g>");
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        private static string TimelineHtml(Ctx c, Dictionary<string, string> s, List<ChVisit> visits, List<ChNote> notes, List<ChDx> dxs, List<ChOrder> orders, List<ChResult> results, List<ChFile> files, bool h24)
        {
            StringBuilder sb = new StringBuilder();
            if (visits.Count == 0)
            {
                sb.Append("<div class=\"ch-panel ch-empty\"><p>No visits on this chart yet.</p></div>");
                return sb.ToString();
            }
            List<string> years = visits.Select(v => v.CheckinAt.Year.ToString(Ui.Inv)).Distinct().ToList();
            List<(string, string)> staff = visits.Where(v => v.StaffId != string.Empty && v.StaffName != string.Empty).GroupBy(v => v.StaffId).Select(g => (g.Key, g.First().StaffName)).OrderBy(x => x.Item2).ToList();
            List<string> types = new[] { "clinic", "surgery", "grooming", "boarding" }.Where(t => visits.Any(v => v.VisitType == t)).ToList();
            sb.Append("<div class=\"ch-tools\"><label class=\"ch-field ch-grow\"><span>Search this chart</span><input type=\"search\" id=\"ch-q\" maxlength=\"80\" placeholder=\"Words in notes, diagnoses, orders or results\" autocomplete=\"off\" oninput=\"AdminChartJs.typedq()\"></label>");
            sb.Append("<label class=\"ch-field\"><span>Type</span><select id=\"ch-ft\" onchange=\"AdminChartJs.filt()\"><option value=\"\">All types</option>" + Ui.Opts(types.Select(t => (t, TypeName(t))), string.Empty) + "</select></label>");
            sb.Append("<label class=\"ch-field\"><span>Year</span><select id=\"ch-fy\" onchange=\"AdminChartJs.filt()\"><option value=\"\">All years</option>" + Ui.Opts(years.Select(y => (y, y)), string.Empty) + "</select></label>");
            sb.Append("<label class=\"ch-field\"><span>Staff</span><select id=\"ch-fs\" onchange=\"AdminChartJs.filt()\"><option value=\"\">Everyone</option>" + Ui.Opts(staff, string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"ch-tbar\"><span id=\"ch-count\" aria-live=\"polite\">" + Ui.Plural(visits.Count, "visit", "visits") + "</span><span class=\"ch-acts\"><button type=\"button\" class=\"ch-btn ch-btn-sm ch-btn-ghost\" onclick=\"AdminChartJs.all(true)\">Expand all</button><button type=\"button\" class=\"ch-btn ch-btn-sm ch-btn-ghost\" onclick=\"AdminChartJs.all(false)\">Collapse all</button></span></div>");
            ILookup<string, ChNote> nByVisit = notes.ToLookup(n => n.VisitId);
            ILookup<string, ChDx> dByVisit = dxs.ToLookup(d => d.VisitId);
            ILookup<string, ChOrder> oByVisit = orders.ToLookup(o => o.VisitId);
            ILookup<string, ChResult> rByVisit = results.ToLookup(r => r.VisitId);
            ILookup<string, ChFile> fByVisit = files.ToLookup(f => f.VisitId);
            sb.Append("<div class=\"ch-tl\" id=\"ch-tl\">");
            int opened = 0;
            foreach (ChVisit v in visits)
            {
                string data = " data-type=\"" + Ui.Attr(v.VisitType) + "\" data-year=\"" + v.CheckinAt.Year.ToString(Ui.Inv) + "\" data-staff=\"" + Ui.Attr(v.StaffId) + "\"";
                if (v.VisitType == "boarding")
                {
                    DateTime? end = v.ClosedAt ?? v.PlanOut;
                    string span = v.CheckinAt.ToString("MMM d", Ui.Us) + (end.HasValue ? " – " + end.Value.ToString("MMM d", Ui.Us) : string.Empty);
                    sb.Append("<article class=\"ch-v ch-vlite\"" + data + "><div class=\"ch-vh\"><span class=\"ch-vd\"><b>" + Short(v.CheckinAt) + "</b></span><span class=\"ch-vm\"><span class=\"ch-vt\"><span class=\"ch-ty ch-ty-boarding\">Boarding</span> <b>" + Ui.Enc(span) + "</b></span><small>" + Ui.Enc(v.VisitNo) + (v.Reason != string.Empty && v.Reason != "Boarding stay" ? " &middot; " + Ui.Enc(v.Reason) : string.Empty) + "</small></span>" + Chip(v.Status) + "</div></article>");
                    continue;
                }
                bool open = opened < 1;
                opened++;
                List<ChResult> vr = rByVisit[v.VisitId].ToList();
                int flagged = vr.Count(r => r.Flag == "H" || r.Flag == "L");
                List<ChNote> vn = nByVisit[v.VisitId].ToList();
                bool unsigned = vn.Any(n => !n.SignedUtc.HasValue);
                sb.Append("<article class=\"ch-v" + (open ? " ch-open" : string.Empty) + "\"" + data + " data-o=\"" + (open ? "1" : "0") + "\">");
                sb.Append("<button type=\"button\" class=\"ch-vh\" aria-expanded=\"" + (open ? "true" : "false") + "\" onclick=\"AdminChartJs.tog(this)\"><span class=\"ch-vd\"><b>" + Short(v.CheckinAt) + "</b><small>" + Ui.Hm(v.CheckinAt, h24) + "</small></span>");
                string title = v.Reason != string.Empty ? v.Reason : v.BookName != string.Empty ? v.BookName : TypeName(v.VisitType) + " visit";
                List<string> sub = new List<string> { v.VisitNo };
                if (v.StaffName != string.Empty)
                {
                    sub.Add(v.StaffName);
                }
                if (v.BookName != string.Empty && v.BookName != title)
                {
                    sub.Add(v.BookName);
                }
                sb.Append("<span class=\"ch-vm\"><span class=\"ch-vt\"><span class=\"ch-ty ch-ty-" + Ui.Attr(v.VisitType) + "\">" + Ui.Enc(TypeName(v.VisitType)) + "</span> <b>" + Ui.Enc(title) + "</b></span><small>" + Ui.Enc(string.Join(" · ", sub)) + "</small></span><span class=\"ch-vs\">");
                if (flagged > 0)
                {
                    sb.Append("<span class=\"ch-tag ch-tag-alert\">" + Ui.Plural(flagged, "flagged result", "flagged results") + "</span>");
                }
                if (unsigned)
                {
                    sb.Append("<span class=\"ch-tag ch-tag-warn\">Unsigned note</span>");
                }
                sb.Append(Chip(v.Status) + "</span><i class=\"ch-chev\" aria-hidden=\"true\"></i></button>");
                sb.Append("<div class=\"ch-vb\">" + VisitBody(c, v, vn, dByVisit[v.VisitId].ToList(), oByVisit[v.VisitId].ToList(), vr, fByVisit[v.VisitId].ToList(), h24) + "</div></article>");
            }
            sb.Append("</div><div class=\"ch-panel ch-empty\" id=\"ch-none\" hidden><p>No visit matches these filters.</p></div>");
            return sb.ToString();
        }

        private static string NoteHtml(ChNote n, ILookup<string, ChNote> kids, int depth)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ch-note" + (depth > 0 ? " ch-add" : string.Empty) + "\"><div class=\"ch-nh\"><span class=\"ch-kind\">" + (depth > 0 ? "Addendum" : Ui.Enc(KindName(n.NoteKind))) + "</span>" + (n.StaffName != string.Empty ? "<b>" + Ui.Enc(n.StaffName) + "</b>" : string.Empty) + Ui.Time(n.CreatedUtc, "full"));
            sb.Append(n.SignedUtc.HasValue ? "<span class=\"ch-sig ch-sig-ok\">Signed " + Ui.Time(n.SignedUtc.Value, "day") + "</span>" : "<span class=\"ch-sig ch-sig-no\">Not signed yet</span>");
            sb.Append("</div>");
            List<string> vit = new List<string>();
            if (n.TempF.HasValue)
            {
                vit.Add("<span>Temp <b>" + Ui.Num(n.TempF.Value) + " &deg;F</b></span>");
            }
            if (n.HeartRate.HasValue)
            {
                vit.Add("<span>Heart <b>" + n.HeartRate.Value.ToString(Ui.Inv) + "/min</b></span>");
            }
            if (n.RespRate.HasValue)
            {
                vit.Add("<span>Breathing <b>" + n.RespRate.Value.ToString(Ui.Inv) + "/min</b></span>");
            }
            if (n.WeightLb.HasValue)
            {
                vit.Add("<span>Weight <b>" + Lb(n.WeightLb.Value) + "</b></span>");
            }
            if (vit.Count > 0)
            {
                sb.Append("<div class=\"ch-vit\">" + string.Join(string.Empty, vit) + "</div>");
            }
            StringBuilder soap = new StringBuilder();
            foreach ((string label, string text) in new[] { ("History", n.Subjective), ("Exam findings", n.Objective), ("Assessment", n.Assessment), ("Plan", n.PlanText) })
            {
                if (text.Trim() != string.Empty)
                {
                    soap.Append("<div><dt>" + label + "</dt><dd>" + Text(text) + "</dd></div>");
                }
            }
            if (soap.Length > 0)
            {
                sb.Append("<dl class=\"ch-soap\">" + soap + "</dl>");
            }
            else if (vit.Count == 0)
            {
                sb.Append("<p class=\"ch-dim\">Empty note.</p>");
            }
            if (depth < 4)
            {
                foreach (ChNote k in kids[n.NoteId])
                {
                    sb.Append(NoteHtml(k, kids, depth + 1));
                }
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string VisitBody(Ctx c, ChVisit v, List<ChNote> notes, List<ChDx> dxs, List<ChOrder> orders, List<ChResult> results, List<ChFile> files, bool h24)
        {
            StringBuilder sb = new StringBuilder();
            List<string> meta = new List<string> { "Checked in " + Ui.Enc(v.CheckinAt.ToString("ddd MMM d, yyyy", Ui.Us)) + " " + Ui.Hm(v.CheckinAt, h24) };
            if (v.ClosedAt.HasValue)
            {
                meta.Add("closed " + Ui.Hm(v.ClosedAt.Value, h24) + (v.ClosedAt.Value.Date != v.CheckinAt.Date ? " on " + Ui.Enc(v.ClosedAt.Value.ToString("MMM d", Ui.Us)) : string.Empty));
            }
            sb.Append("<div class=\"ch-vmeta\"><span>" + string.Join(" &middot; ", meta) + "</span>");
            if (c.Can("visits", 'V') && c.Mine(v.StaffId))
            {
                sb.Append("<a class=\"ch-btn ch-btn-sm ch-btn-ghost ch-noprint\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Open visit</a>");
            }
            sb.Append("</div>");
            bool any = false;
            HashSet<string> ids = new HashSet<string>(notes.Select(n => n.NoteId));
            List<ChNote> roots = notes.Where(n => n.ParentId == string.Empty || !ids.Contains(n.ParentId)).ToList();
            ILookup<string, ChNote> kids = notes.Where(n => n.ParentId != string.Empty && ids.Contains(n.ParentId)).ToLookup(n => n.ParentId);
            if (roots.Count > 0)
            {
                any = true;
                sb.Append("<section class=\"ch-blk\"><h4>Notes</h4>");
                foreach (ChNote n in roots)
                {
                    sb.Append(NoteHtml(n, kids, 0));
                }
                sb.Append("</section>");
            }
            if (dxs.Count > 0)
            {
                any = true;
                sb.Append("<section class=\"ch-blk\"><h4>Diagnoses</h4><ul class=\"ch-dx\">");
                foreach (ChDx d in dxs.OrderBy(d => d.CreatedUtc))
                {
                    sb.Append("<li><b>" + Ui.Enc(d.DxText) + "</b>" + Chip(d.DxStatus) + (d.StaffName != string.Empty ? "<small>" + Ui.Enc(d.StaffName) + "</small>" : string.Empty) + "</li>");
                }
                sb.Append("</ul></section>");
            }
            if (orders.Count > 0)
            {
                any = true;
                sb.Append("<section class=\"ch-blk\"><h4>Orders</h4><div class=\"ch-tw\"><table class=\"ch-tbl ch-otbl\"><thead><tr><th>Item</th><th>Type</th><th class=\"ch-num\">Qty</th><th>Dose and instructions</th><th>Status</th></tr></thead><tbody>");
                foreach (ChOrder o in orders)
                {
                    string how = string.Join(" ", new[] { o.Dose != string.Empty ? "<b>" + Ui.Enc(o.Dose) + "</b>" : string.Empty, o.Instructions != string.Empty ? Ui.Enc(o.Instructions) : string.Empty, o.Note != string.Empty ? "<i class=\"ch-dim\">" + Ui.Enc(o.Note) + "</i>" : string.Empty }.Where(x => x != string.Empty));
                    sb.Append("<tr><td data-l=\"Item\" class=\"ch-oname\"><b>" + Ui.Enc(o.ItemName) + "</b>" + (o.Dispense == "home" ? " <span class=\"ch-tag\">Sent home</span>" : string.Empty) + "</td><td data-l=\"Type\">" + Ui.Enc(ItemTypeName(o.ItemType)) + "</td><td data-l=\"Qty\" class=\"ch-num\">" + Ui.Num(o.Qty) + (o.Unit != string.Empty && o.Unit != "each" ? " " + Ui.Enc(o.Unit) : string.Empty) + "</td><td data-l=\"How\"><span>" + (how != string.Empty ? how : "<i class=\"ch-dim\">&ndash;</i>") + "</span></td><td data-l=\"Status\">" + Chip(o.Status) + "</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            if (results.Count > 0)
            {
                any = true;
                sb.Append("<section class=\"ch-blk\"><h4>Test results</h4>");
                foreach (IGrouping<string, ChResult> g in results.GroupBy(r => r.LineId))
                {
                    sb.Append("<div class=\"ch-res\"><b class=\"ch-rname\">" + Ui.Enc(g.First().ItemName) + "</b><div class=\"ch-tw\"><table class=\"ch-tbl ch-rtbl\"><thead><tr><th>Test</th><th class=\"ch-num\">Result</th><th>Normal range</th><th>Flag</th></tr></thead><tbody>");
                    foreach (ChResult r in g.OrderBy(r => r.FieldOrder).ThenBy(r => r.FieldName))
                    {
                        string value = r.ValueNum.HasValue ? Val(r.ValueNum.Value, r.Decimals) + (r.Unit != string.Empty ? " <small>" + Ui.Enc(r.Unit) + "</small>" : string.Empty) : Ui.Enc(r.ValueText);
                        string cls = r.Flag == "H" ? " class=\"ch-hi\"" : r.Flag == "L" ? " class=\"ch-lo\"" : string.Empty;
                        sb.Append("<tr" + cls + "><td data-l=\"Test\">" + Ui.Enc(r.FieldName) + "</td><td data-l=\"Result\" class=\"ch-num\">" + (value != string.Empty ? value : "<i class=\"ch-dim\">&ndash;</i>") + "</td><td data-l=\"Normal\">" + Range(r) + "</td><td data-l=\"Flag\">" + FlagHtml(r.Flag) + "</td></tr>");
                    }
                    sb.Append("</tbody></table></div></div>");
                }
                sb.Append("</section>");
            }
            if (files.Count > 0)
            {
                any = true;
                bool mine = c.Mine(v.StaffId);
                sb.Append("<section class=\"ch-blk\"><h4>Files</h4><ul class=\"ch-files\">");
                foreach (ChFile f in files)
                {
                    string name = f.Title != string.Empty ? f.Title : f.FileName;
                    string size = f.Bytes >= 1048576 ? (f.Bytes / 1048576.0).ToString("0.#", Ui.Inv) + " MB" : Math.Max(1, f.Bytes / 1024).ToString(Ui.Inv) + " KB";
                    string kind = f.Mime == "application/pdf" ? "PDF" : f.Mime.StartsWith("image/", StringComparison.Ordinal) ? "Picture" : "File";
                    sb.Append("<li>" + (mine ? "<a href=\"vfile?id=" + Uri.EscapeDataString(f.FileId) + "\" target=\"_blank\" rel=\"noopener\">" + Ui.Enc(name) + "</a>" : "<b>" + Ui.Enc(name) + "</b>") + "<small>" + kind + " &middot; " + size + (mine ? string.Empty : " &middot; only the visit's provider can open it") + "</small></li>");
                }
                sb.Append("</ul></section>");
            }
            if (v.Summary.Trim() != string.Empty)
            {
                any = true;
                sb.Append("<section class=\"ch-blk ch-take\"><h4>Take-home instructions</h4><p>" + Text(v.Summary) + "</p></section>");
            }
            if (v.RecheckDate.HasValue)
            {
                any = true;
                sb.Append("<p class=\"ch-rc\">Recheck by <b>" + Ui.Enc(v.RecheckDate.Value.ToString("ddd MMM d, yyyy", Ui.Us)) + "</b></p>");
            }
            if (!any)
            {
                sb.Append("<p class=\"ch-dim\">Nothing was recorded for this visit yet.</p>");
            }
            return sb.ToString();
        }

        private static string Label(ChResult r)
        {
            return r.FieldName + " (" + r.ItemName + ")";
        }

        private static string LabHtml(List<ChResult> numeric, List<ChResult> all)
        {
            if (numeric.Count == 0)
            {
                return "<div class=\"ch-panel ch-empty\"><p>No lab results with numbers on this chart yet.</p></div>";
            }
            var fields = numeric.GroupBy(FieldKey)
                .Select(g => new { Key = g.Key, Label = Label(g.First()), Count = g.Count(), Flags = g.Count(r => r.Flag == "H" || r.Flag == "L") })
                .OrderByDescending(x => x.Count).ThenByDescending(x => x.Flags).ThenBy(x => x.Label)
                .ToList();
            string cur = fields[0].Key;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ch-tools ch-noprint\"><label class=\"ch-field ch-grow\"><span>Test to follow</span><select id=\"ch-fld\" onchange=\"AdminChartJs.trend()\">");
            foreach (var x in fields.OrderBy(x => x.Label))
            {
                sb.Append("<option value=\"" + Ui.Attr(x.Key) + "\"" + (x.Key == cur ? " selected" : string.Empty) + ">" + Ui.Enc(x.Label + " · " + Ui.Plural(x.Count, "result", "results") + (x.Flags > 0 ? ", " + x.Flags + " flagged" : string.Empty)) + "</option>");
            }
            sb.Append("</select></label></div><div id=\"ch-trend\">" + TrendHtml(all, cur) + "</div>");
            return sb.ToString();
        }

        private static string TrendHtml(List<ChResult> all, string key)
        {
            List<ChResult> pts = all.Where(r => r.ValueNum.HasValue && FieldKey(r) == key).OrderBy(r => r.VisitAt).ThenBy(r => r.LineId).ToList();
            if (pts.Count == 0)
            {
                return "<div class=\"ch-panel ch-empty\"><p>No results with numbers for this test.</p></div>";
            }
            ChResult last = pts[^1];
            int dec = last.Decimals;
            decimal? lo = pts.LastOrDefault(r => r.LowValue.HasValue)?.LowValue;
            decimal? hi = pts.LastOrDefault(r => r.HighValue.HasValue)?.HighValue;
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ch-panel ch-trend\"><div class=\"ch-trh\"><div><h3>" + Ui.Enc(Label(last)) + "</h3><p class=\"ch-dim\">" + Ui.Plural(pts.Count, "result", "results") + " from " + Short(pts[0].VisitAt) + " to " + Short(last.VisitAt));
            if (lo.HasValue || hi.HasValue)
            {
                sb.Append(" &middot; normal range " + (lo.HasValue ? Val(lo.Value, dec) : "&ndash;") + "&ndash;" + (hi.HasValue ? Val(hi.Value, dec) : "&ndash;") + (last.Unit != string.Empty ? " " + Ui.Enc(last.Unit) : string.Empty));
            }
            sb.Append("</p></div><div class=\"ch-trl\"><small>Latest</small><b class=\"" + (last.Flag == "H" ? "ch-thi" : last.Flag == "L" ? "ch-tlo" : string.Empty) + "\">" + Val(last.ValueNum!.Value, dec) + (last.Unit != string.Empty ? " <small>" + Ui.Enc(last.Unit) + "</small>" : string.Empty) + FlagHtml(last.Flag) + "</b></div></div>");
            sb.Append(TrendSvg(pts, lo, hi, dec));
            sb.Append("<p class=\"ch-legend\"><span><i class=\"ch-lg-band\"></i>Normal range</span><span><i class=\"ch-lg-n\"></i>Normal</span><span><i class=\"ch-lg-h\"></i>High</span><span><i class=\"ch-lg-l\"></i>Low</span></p></div>");
            string itemId = last.ItemId;
            string itemName = last.ItemName;
            List<ChResult> test = all.Where(r => r.ItemId == itemId && r.ItemName == itemName).ToList();
            var cols = test.GroupBy(r => r.LineId).Select(g => new { Line = g.Key, At = g.First().VisitAt }).OrderBy(x => x.At).ThenBy(x => x.Line).ToList();
            var rows = test.GroupBy(r => r.FieldName).Select(g => new { Name = g.Key, Order = g.Min(r => r.FieldOrder), Cells = g.GroupBy(r => r.LineId).ToDictionary(x => x.Key, x => x.First()) }).OrderBy(x => x.Order).ThenBy(x => x.Name).ToList();
            sb.Append("<div class=\"ch-panel ch-gridp\"><h3>" + Ui.Enc(itemName) + " results</h3><div class=\"ch-gw\"><table class=\"ch-rgrid\"><thead><tr><th scope=\"col\">Test</th>");
            foreach (var col in cols)
            {
                sb.Append("<th scope=\"col\" class=\"ch-num\">" + Ui.Enc(col.At.ToString("MMM d", Ui.Us)) + "<small>" + col.At.Year.ToString(Ui.Inv) + "</small></th>");
            }
            sb.Append("</tr></thead><tbody>");
            string chosen = key.Contains('|') ? key.Substring(key.IndexOf('|') + 1) : string.Empty;
            foreach (var row in rows)
            {
                ChResult? any = row.Cells.Values.LastOrDefault();
                string range = any != null ? Range(any) : string.Empty;
                string unit = any != null && any.Unit != string.Empty ? any.Unit : string.Empty;
                sb.Append("<tr" + (row.Name == chosen ? " class=\"ch-gsel\"" : string.Empty) + "><th scope=\"row\">" + Ui.Enc(row.Name) + (unit != string.Empty || range != string.Empty ? "<small>" + Ui.Enc(unit) + (range != string.Empty ? (unit != string.Empty ? " &middot; " : string.Empty) + range : string.Empty) + "</small>" : string.Empty) + "</th>");
                foreach (var col in cols)
                {
                    if (!row.Cells.TryGetValue(col.Line, out ChResult? r))
                    {
                        sb.Append("<td class=\"ch-num ch-dim\">&ndash;</td>");
                        continue;
                    }
                    string v = r.ValueNum.HasValue ? Val(r.ValueNum.Value, r.Decimals) : Ui.Enc(Ui.OneLine(r.ValueText, 40));
                    string cls = r.Flag == "H" ? " ch-hi" : r.Flag == "L" ? " ch-lo" : string.Empty;
                    sb.Append("<td class=\"ch-num" + cls + "\">" + (v != string.Empty ? v : "&ndash;") + FlagHtml(r.Flag) + "</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div></div>");
            return sb.ToString();
        }

        private static string TrendSvg(List<ChResult> pts, decimal? lo, decimal? hi, int dec)
        {
            double w = 680, h = 260, l = 52, r = 20, t = 22, b = 34;
            List<double> vals = pts.Select(x => (double)x.ValueNum!.Value).ToList();
            double min = vals.Min(), max = vals.Max();
            if (lo.HasValue)
            {
                min = Math.Min(min, (double)lo.Value);
                max = Math.Max(max, (double)lo.Value);
            }
            if (hi.HasValue)
            {
                min = Math.Min(min, (double)hi.Value);
                max = Math.Max(max, (double)hi.Value);
            }
            double pad = Math.Max((max - min) * 0.08, Math.Max(Math.Abs(max) * 0.03, 0.05));
            min = min >= 0 ? Math.Max(0, min - pad) : min - pad;
            max += pad;
            double raw = (max - min) / 4;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double step = new[] { 1, 2, 2.5, 5, 10 }.Select(m => m * mag).First(m => m >= raw);
            min = Math.Floor(min / step) * step;
            max = Math.Ceiling(max / step) * step;
            int ticks = (int)Math.Round((max - min) / step);
            int sdec = Math.Max(0, -(int)Math.Floor(Math.Log10(step) + 1e-9)) + (Math.Abs(step / mag - 2.5) < 1e-9 && step < 10 ? 1 : 0);
            double t0 = pts[0].VisitAt.Ticks, t1 = pts[^1].VisitAt.Ticks;
            int n = pts.Count;
            double X(int i, DateTime d) => t1 > t0 ? l + (d.Ticks - t0) / (t1 - t0) * (w - l - r) : n > 1 ? l + i * (w - l - r) / (n - 1) : (l + w - r) / 2;
            double Y(double v) => t + (max - v) / (max - min) * (h - t - b);
            string D(double v) => dec >= 0 && dec <= 4 ? v.ToString("F" + dec.ToString(Ui.Inv), Ui.Inv) : v.ToString("0.##", Ui.Inv);
            StringBuilder sb = new StringBuilder();
            ChResult last = pts[^1];
            sb.Append("<svg class=\"ch-tchart\" viewBox=\"0 0 " + F(w) + " " + F(h) + "\" role=\"img\" aria-label=\"" + Ui.Attr(last.FieldName + " over time, latest " + D(vals[^1]) + " " + last.Unit) + "\">");
            if (lo.HasValue || hi.HasValue)
            {
                double yTop = hi.HasValue ? Y((double)hi.Value) : t;
                double yBot = lo.HasValue ? Y((double)lo.Value) : h - b;
                sb.Append("<rect class=\"ch-band\" x=\"" + F(l) + "\" y=\"" + F(yTop) + "\" width=\"" + F(w - l - r) + "\" height=\"" + F(Math.Max(1, yBot - yTop)) + "\"/>");
                if (hi.HasValue)
                {
                    sb.Append("<line class=\"ch-bl\" x1=\"" + F(l) + "\" x2=\"" + F(w - r) + "\" y1=\"" + F(yTop) + "\" y2=\"" + F(yTop) + "\"/>");
                }
                if (lo.HasValue)
                {
                    sb.Append("<line class=\"ch-bl\" x1=\"" + F(l) + "\" x2=\"" + F(w - r) + "\" y1=\"" + F(yBot) + "\" y2=\"" + F(yBot) + "\"/>");
                }
            }
            for (int i = 0; i <= ticks; i++)
            {
                double gv = min + step * i;
                double gy = Y(gv);
                sb.Append("<line class=\"ch-cg\" x1=\"" + F(l) + "\" x2=\"" + F(w - r) + "\" y1=\"" + F(gy) + "\" y2=\"" + F(gy) + "\"/><text class=\"ch-ct\" x=\"" + F(l - 8) + "\" y=\"" + F(gy + 4) + "\" text-anchor=\"end\">" + Ui.Enc(gv.ToString("F" + sdec.ToString(Ui.Inv), Ui.Inv)) + "</text>");
            }
            sb.Append("<text class=\"ch-ct\" x=\"" + F(l) + "\" y=\"" + F(h - 10) + "\">" + Ui.Enc(pts[0].VisitAt.ToString("MMM d, yyyy", Ui.Us)) + "</text>");
            if (n > 1)
            {
                sb.Append("<text class=\"ch-ct\" x=\"" + F(w - r) + "\" y=\"" + F(h - 10) + "\" text-anchor=\"end\">" + Ui.Enc(last.VisitAt.ToString("MMM d, yyyy", Ui.Us)) + "</text>");
            }
            if (n > 1)
            {
                sb.Append("<polyline class=\"ch-tl2\" points=\"" + string.Join(" ", pts.Select((x, i) => F(X(i, x.VisitAt)) + "," + F(Y(vals[i])))) + "\"/>");
            }
            bool labels = n <= 14;
            bool[] below = new bool[n];
            for (int i = 1; i < n; i++)
            {
                if (X(i, pts[i].VisitAt) - X(i - 1, pts[i - 1].VisitAt) < 34)
                {
                    if (vals[i] < vals[i - 1])
                    {
                        below[i] = true;
                    }
                    else
                    {
                        below[i - 1] = true;
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                ChResult x = pts[i];
                double cx = X(i, x.VisitAt), cy = Y(vals[i]);
                double ly = below[i] || cy - 11 < 10 ? cy + 20 : cy - 11;
                string anchor = n > 1 && i == 0 ? "start" : n > 1 && i == n - 1 ? "end" : "middle";
                double lx = anchor == "start" ? cx + 4 : anchor == "end" ? cx - 4 : cx;
                string cls = x.Flag == "H" ? "ch-pt ch-pt-h" : x.Flag == "L" ? "ch-pt ch-pt-l" : "ch-pt";
                sb.Append("<g><circle class=\"ch-chit\" cx=\"" + F(cx) + "\" cy=\"" + F(cy) + "\" r=\"12\"/><circle class=\"" + cls + "\" cx=\"" + F(cx) + "\" cy=\"" + F(cy) + "\" r=\"5.5\"/>");
                if (labels)
                {
                    sb.Append("<text class=\"ch-pv" + (x.Flag == "H" ? " ch-pv-h" : x.Flag == "L" ? " ch-pv-l" : string.Empty) + "\" x=\"" + F(lx) + "\" y=\"" + F(ly) + "\" text-anchor=\"" + anchor + "\">" + Ui.Enc(D(vals[i])) + "</text>");
                }
                sb.Append("<title>" + Ui.Enc(Short(x.VisitAt) + ": " + D(vals[i]) + " " + x.Unit + (x.Flag == "H" ? " (high)" : x.Flag == "L" ? " (low)" : string.Empty)) + "</title></g>");
            }
            sb.Append("</svg>");
            return sb.ToString();
        }
    }
}
