using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminBoarding : WebPage
    {
        private const string Key = "boarding";
        private const string Me = "AdminBoardingJs";
        private const int Days = 14;
        private const int PageSize = 30;
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string Changed = "IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";
        private static readonly string[] Tags = { "Ate well", "Ate some", "Did not eat", "Walked", "Played", "Medicine given", "Bathroom normal" };
        private static readonly string[] States = { "upcoming", "booked", "confirmed", "inhouse", "ready", "done", "noshow", "cancelled" };
        private const string PhoneDigits = "REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), N'+', N'')";

        private const string StateCase = "CASE WHEN A.STATUS = N'checkedin' THEN CASE WHEN V.STATUS = N'inroom' THEN N'inhouse' WHEN V.STATUS = N'checkout' THEN N'ready' ELSE N'done' END ELSE A.STATUS END";

        private const string StayCols = "A.APPT_ID AS ApptId, A.APPT_CODE AS ApptCode, A.CLIENT_ID AS ClientId, A.PET_ID AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(P.SPECIES_KEY, N'') AS SpeciesKey, ISNULL(SP.SPECIES_NAME, ISNULL(P.SPECIES_KEY, N'')) AS SpeciesName, ISNULL(P.BREED, N'') AS Breed, ISNULL(P.SIZE, N'') AS Size, ISNULL(P.ALERT, N'') AS PetAlert, ISNULL(P.PHOTO, N'') AS Photo, " +
            "ISNULL(C.FIRST_NAME, N'') AS OwnerFirst, ISNULL(C.LAST_NAME, N'') AS OwnerLast, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.EMAIL, N'') AS Email, ISNULL(C.ALERT, N'') AS ClientAlert, A.ROOMTYPE_ID AS RoomtypeId, ISNULL(T.TYPE_NAME, N'') AS TypeName, A.ROOM_ID AS RoomId, ISNULL(R.ROOM_NAME, N'') AS RoomName, A.START_AT AS StartAt, A.END_AT AS EndAt, A.ADDONS AS Addons, A.EST_PRICE AS EstPrice, A.VAX_PENDING AS VaxPending, " +
            "A.NOTE AS Note, A.DESK_NOTE AS DeskNote, A.STATUS AS Status, A.SOURCE AS Source, A.CANCEL_REASON AS CancelReason, A.CREATED_UTC AS CreatedUtc, A.VISIT_ID AS VisitId, ISNULL(V.VISIT_NO, N'') AS VisitNo, ISNULL(V.STATUS, N'') AS VisitStatus, V.CHECKIN_AT AS CheckinAt, V.PLAN_OUT AS PlanOut, V.READY_AT AS ReadyAt, ISNULL(X.ROOM_ID, N'') AS CurRoomId, ISNULL(XR.ROOM_NAME, N'') AS CurRoomName, " + StateCase + " AS State";

        private const string StayFrom = " FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN ROOMTYPE T ON T.ROOMTYPE_ID = A.ROOMTYPE_ID LEFT JOIN ROOM R ON R.ROOM_ID = A.ROOM_ID LEFT JOIN VISIT V ON V.VISIT_ID = A.VISIT_ID AND A.VISIT_ID <> N'' LEFT JOIN VISITROOM X ON X.VISIT_ID = V.VISIT_ID AND X.OUT_AT IS NULL LEFT JOIN ROOM XR ON XR.ROOM_ID = X.ROOM_ID WHERE A.VISIT_TYPE = N'boarding'";

        private const string PetCols = "SELECT P.PET_ID AS PetId, P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(X.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SIZE AS Size, P.ALERT AS Alert, P.PHOTO AS Photo, P.CLIENT_ID AS ClientId, C.FIRST_NAME + N' ' + C.LAST_NAME AS OwnerName, C.PHONE AS Phone, C.ALERT AS ClientAlert FROM PET P JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES X ON X.SPECIES_KEY = P.SPECIES_KEY WHERE P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL AND C.ACTIVE = 1 AND P.SPECIES_KEY IN (N'dog', N'cat')";

        private const string BusyCond = "SELECT A.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND A.ROOM_ID <> N'' AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din " +
            "UNION ALL SELECT X.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))";

        private const string RoomGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT A WHERE A.ROOM_ID = @room AND A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID WHERE X.ROOM_ID = @room AND X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))) THROW 50001, N'ROOMBUSY', 1;";

        private const string PetGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND APPT_ID <> @ign AND CAST(START_AT AS DATE) < @dout AND CAST(END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom' AND VISIT_ID <> @ivis AND (CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) > @din OR CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) < @today)) THROW 50005, N'PETBUSY', 1;";

        private const string PetLock = "UPDATE PET SET SYSDTE = SYSDTE WHERE PET_ID = @pid";

        private const string LogSql = "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), @me, @ltext)";

        private sealed class Rules
        {
            public TimeSpan InFrom { get; set; }
            public TimeSpan InTo { get; set; }
            public TimeSpan OutBy { get; set; }
            public int MaxNights { get; set; }
            public int CancelHours { get; set; }
        }

        private sealed class Bar
        {
            public string RoomId { get; set; } = string.Empty;
            public double L { get; set; }
            public double R { get; set; }
            public int N0 { get; set; }
            public int N1 { get; set; }
            public BdStay St { get; set; } = new BdStay();
            public string Cls { get; set; } = string.Empty;
            public bool Vx { get; set; }
            public bool CutL { get; set; }
            public bool CutR { get; set; }
        }

        private sealed class QLine
        {
            public BdItem Item { get; set; } = new BdItem();
            public decimal Qty { get; set; }
            public decimal Price { get; set; }
            public decimal Total => Qty * Price;
        }

        private sealed class VaxLine
        {
            public string Name { get; set; } = string.Empty;
            public DateTime? Due { get; set; }
            public bool Ok { get; set; }
        }

        private sealed class Ask
        {
            public DateTime In { get; set; }
            public DateTime Out { get; set; }
            public string Error { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Boarding | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            DateTime? qd = Ui.ParseDay(QueryValue("d"));
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--bd-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("bd"))
                .Replace("{plhd_qa}", Ui.Attr(Id(QueryValue("a"))))
                .Replace("{plhd_qp}", Ui.Attr(Id(QueryValue("p"))))
                .Replace("{plhd_qd}", qd.HasValue ? Ui.Ymd(qd.Value) : string.Empty);
        }

        private static string Id(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private string V(string key, int max = 200)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private string Text(string key, int max)
        {
            string t = (GetDataValue(key) ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }

        private string NewKey()
        {
            string k = V("newid", 40);
            return Regex.IsMatch(k, "^[a-f0-9]{32}$") ? k : string.Empty;
        }

        private static string Like(string term)
        {
            return "%" + term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private static string Hm24(TimeSpan t)
        {
            return t.ToString("hh\\:mm", Ui.Inv);
        }

        private static string Clock(TimeSpan t)
        {
            return Ui.Hm(DateTime.Today.Add(t));
        }

        private static string When(DateTime d)
        {
            return Ui.Day(d, false) + ", " + Ui.Hm(d);
        }

        private static string Num(double v)
        {
            return v.ToString("0.###", Ui.Inv);
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

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("bd-menu", Auth.Menu("bd", Me, "AdminBoarding", c));
            response.SetElementContents("bd-who", Auth.Who("bd", c));
            response.ExecuteScript(Me + ".secs();");
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript(Me + ".toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"bd-panel bd-deny\"><h1>You don't have access to this page</h1><p>The boarding board is for the front desk and the kennel team. Ask a manager to open it for you in Staff and permissions.</p><a class=\"bd-btn bd-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static Rules Rule(Dictionary<string, string> s)
        {
            TimeSpan f = Ui.ParseHm(App.S(s, "stay.checkin.from")) ?? new TimeSpan(7, 30, 0);
            TimeSpan t = Ui.ParseHm(App.S(s, "stay.checkin.to")) ?? new TimeSpan(18, 0, 0);
            TimeSpan o = Ui.ParseHm(App.S(s, "stay.checkout.by")) ?? new TimeSpan(11, 0, 0);
            if (t <= f)
            {
                t = f.Add(TimeSpan.FromHours(1));
            }
            if (o < f)
            {
                o = f;
            }
            return new Rules { InFrom = f, InTo = t, OutBy = o, MaxNights = Math.Clamp(App.SInt(s, "stay.max.nights", 30), 1, 365), CancelHours = Math.Max(0, App.SInt(s, "stay.cancel.hours", 48)) };
        }

        private static List<TimeSpan> Steps(TimeSpan a, TimeSpan z)
        {
            List<TimeSpan> list = new List<TimeSpan>();
            TimeSpan t = TimeSpan.FromMinutes(Math.Ceiling(a.TotalMinutes / 30) * 30);
            for (; t <= z; t = t.Add(TimeSpan.FromMinutes(30)))
            {
                list.Add(t);
            }
            if (list.Count == 0)
            {
                list.Add(a);
            }
            return list;
        }

        private static string TimeOpts(List<TimeSpan> steps, TimeSpan cur)
        {
            List<TimeSpan> all = steps.ToList();
            if (!all.Contains(cur))
            {
                all.Add(cur);
                all.Sort();
            }
            return Ui.Opts(all.Select(t => (Hm24(t), Clock(t))), Hm24(cur));
        }

        private static async Task<string> Closed(Dictionary<string, string> s, DateTime day)
        {
            string hol = await Slots.Holiday(day);
            if (hol != string.Empty)
            {
                return "the clinic is closed on " + Ui.Day(day, false) + " (" + hol + ")";
            }
            if (!Slots.Hours(s, day).Open)
            {
                return "the clinic is closed on " + Ui.DayNames[(int)day.DayOfWeek] + "s";
            }
            return string.Empty;
        }

        private static decimal PriceFor(BdItem i, string size)
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

        private static string SpeciesWord(string key)
        {
            return key == "dog" ? "Dog" : key == "cat" ? "Cat" : key;
        }

        private static string SizeWord(string size)
        {
            return size switch { "small" => "Small", "medium" => "Medium", "large" => "Large", _ => string.Empty };
        }

        private static string StateText(string st)
        {
            return st switch
            {
                "booked" => "Booked",
                "confirmed" => "Confirmed",
                "inhouse" => "In house",
                "ready" => "Ready for checkout",
                "done" => "Checked out",
                "noshow" => "No-show",
                "cancelled" => "Cancelled",
                "upcoming" => "Coming up",
                "overdue" => "Past check-out",
                _ => st
            };
        }

        private static string Chip(string st)
        {
            return "<span class=\"bd-st bd-st-" + Ui.Attr(st) + "\">" + Ui.Enc(StateText(st)) + "</span>";
        }

        private static string Sp(string key)
        {
            return "<i class=\"bd-sp bd-sp-" + (key == "cat" ? "cat" : "dog") + "\" title=\"" + Ui.Attr(SpeciesWord(key)) + "\">" + (key == "cat" ? "C" : "D") + "</i>";
        }

        private static DateTime PlanEnd(BdStay st)
        {
            return st.PlanOut ?? st.EndAt;
        }

        private static DateTime InDay(BdStay st)
        {
            return (st.CheckinAt ?? st.StartAt).Date;
        }

        private static int Nights(DateTime a, DateTime b)
        {
            return Math.Max(0, (b.Date - a.Date).Days);
        }

        private static List<string> Csv(string v)
        {
            return (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        }

        private static async Task<List<BdRun>> Runs()
        {
            var (list, emsg) = await Helper.GetListT<BdRun>("SELECT R.ROOM_ID AS RoomId, R.ROOM_NAME AS RoomName, R.ROOMTYPE_ID AS RoomtypeId, T.TYPE_NAME AS TypeName, T.SPECIES AS Species, T.SIZES AS Sizes, T.NIGHT_ITEM_ID AS NightItemId, T.TYPE_ORDER AS TypeOrder, R.ROOM_ORDER AS RoomOrder, CAST(CASE WHEN T.ACTIVE = 1 AND R.ACTIVE = 1 THEN 1 ELSE 0 END AS BIT) AS Live FROM ROOM R JOIN ROOMTYPE T ON T.ROOMTYPE_ID = R.ROOMTYPE_ID WHERE T.PURPOSE = N'boarding' AND ((T.ACTIVE = 1 AND R.ACTIVE = 1) OR EXISTS (SELECT 1 FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID WHERE X.ROOM_ID = R.ROOM_ID AND X.OUT_AT IS NULL AND V.STATUS = N'inroom')) ORDER BY T.TYPE_ORDER, T.TYPE_NAME, R.ROOM_ORDER, R.ROOM_NAME");
            return emsg == string.Empty && list != null ? list : new List<BdRun>();
        }

        private static async Task<List<BdItem>> Items()
        {
            var (list, emsg) = await Helper.GetListT<BdItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, ITEM_TYPE AS ItemType, UNIT AS Unit, PRICE AS Price, PRICE_M AS PriceM, PRICE_L AS PriceL, TAXABLE AS Taxable, DOSE AS Dose, INSTRUCTIONS AS Instructions, SPECIES AS Species, ACTIVE AS Active, ITEM_ORDER AS ItemOrder FROM ITEM WHERE ITEM_TYPE = N'boarding' ORDER BY ITEM_ORDER, ITEM_NAME");
            return emsg == string.Empty && list != null ? list : new List<BdItem>();
        }

        private static async Task<Dictionary<string, string>> TypeNights()
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT ROOMTYPE_ID, NIGHT_ITEM_ID FROM ROOMTYPE WHERE PURPOSE = N'boarding'");
            Dictionary<string, string> d = new Dictionary<string, string>();
            if (emsg == string.Empty)
            {
                foreach (DataRow r in dt.Rows)
                {
                    d[Convert.ToString(r[0]) ?? string.Empty] = Convert.ToString(r[1]) ?? string.Empty;
                }
            }
            return d;
        }

        private static BdItem? NightItem(Dictionary<string, string> nights, List<BdItem> items, string roomtypeId)
        {
            return nights.TryGetValue(roomtypeId ?? string.Empty, out string? id) && id != string.Empty ? items.FirstOrDefault(i => i.ItemId == id) : null;
        }

        private static List<BdItem> Extras(List<BdItem> items, Dictionary<string, string> nights, string species)
        {
            HashSet<string> ids = nights.Values.Where(x => x != string.Empty).ToHashSet();
            return items.Where(i => i.Active && !ids.Contains(i.ItemId) && Slots.ForSpecies(i.Species, species)).ToList();
        }

        private static bool PerNight(BdItem i)
        {
            return i.Unit == "day" || i.Unit == "night";
        }

        private static bool Fits(BdRun r, BdPet p)
        {
            if (!r.Live || !Slots.ForSpecies(r.Species, p.SpeciesKey))
            {
                return false;
            }
            if (p.Size == string.Empty || r.Sizes.Trim() == string.Empty)
            {
                return true;
            }
            return Csv(r.Sizes).Contains(p.Size);
        }

        private static async Task<BdStay?> StayById(string id)
        {
            if (id == string.Empty)
            {
                return null;
            }
            var (row, emsg) = await Helper.GetOneT<BdStay>("SELECT " + StayCols + StayFrom + " AND A.APPT_ID = @aid", new List<SqlParameter> { App.P("aid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<BdPet?> PetById(string id)
        {
            if (id == string.Empty)
            {
                return null;
            }
            var (row, emsg) = await Helper.GetOneT<BdPet>(PetCols + " AND P.PET_ID = @pid", new List<SqlParameter> { App.P("pid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<BdVaxRow>> VaxRows(List<string> petIds)
        {
            if (petIds.Count == 0)
            {
                return new List<BdVaxRow>();
            }
            List<SqlParameter> ps = new List<SqlParameter>();
            string inList = Slots.InList(petIds, "pv", ps);
            var (list, emsg) = await Helper.GetListT<BdVaxRow>("SELECT PET_ID AS PetId, VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE PET_ID IN (" + inList + ")", ps);
            return emsg == string.Empty && list != null ? list : new List<BdVaxRow>();
        }

        private static List<VaxLine> VaxCheck(Dictionary<string, string> s, string species, string petId, List<BdVaxRow> rows, DateTime outDay)
        {
            List<VaxLine> list = new List<VaxLine>();
            foreach (string name in Csv(App.S(s, "stay.vax." + species)))
            {
                BdVaxRow? last = rows.Where(r => r.PetId == petId && r.VaxName.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.GivenDate).ThenByDescending(r => r.DueDate).FirstOrDefault();
                list.Add(new VaxLine { Name = name, Due = last?.DueDate.Date, Ok = last != null && last.DueDate.Date >= outDay.Date });
            }
            return list;
        }

        private static async Task<List<BdBusy>> BusyRooms(DateTime din, DateTime dout, DateTime today, string ignAppt, string ignVisit, bool instant)
        {
            var (list, emsg) = await Helper.GetListT<BdBusy>(BusyCond, new List<SqlParameter> { App.PDate("din", din.Date), App.PDate("dout", dout.Date), App.PDate("today", today.Date), App.P("ign", ignAppt, 50), App.P("ivis", ignVisit, 50), App.PBit("inst", instant) });
            return emsg == string.Empty && list != null ? list : new List<BdBusy>();
        }

        private static List<QLine> Charge(BdItem? night, List<BdItem> adds, int nights, string size)
        {
            List<QLine> list = new List<QLine>();
            if (night != null)
            {
                list.Add(new QLine { Item = night, Qty = nights, Price = PriceFor(night, size) });
            }
            foreach (BdItem a in adds)
            {
                list.Add(new QLine { Item = a, Qty = PerNight(a) ? nights : 1, Price = PriceFor(a, size) });
            }
            return list;
        }

        private static string UnitWord(BdItem i)
        {
            return PerNight(i) ? "per night" : "once";
        }

        private static string Lines(List<QLine> lines, string title, string foot)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-est\"><b class=\"bd-esth\">" + Ui.Enc(title) + "</b><ul class=\"bd-estl\">");
            foreach (QLine l in lines)
            {
                sb.Append("<li><span>" + Ui.Enc(l.Item.ItemName) + "<small>" + Ui.Num(l.Qty) + " &times; " + Ui.Money(l.Price) + "</small></span><b>" + Ui.Money(l.Total) + "</b></li>");
            }
            sb.Append("</ul><div class=\"bd-estt\"><span>Total</span><b>" + Ui.Money(lines.Sum(x => x.Total)) + "</b></div>");
            if (foot != string.Empty)
            {
                sb.Append("<p class=\"bd-dim\">" + Ui.Enc(foot) + "</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static async Task<string> NewCode()
        {
            for (int i = 0; i < 12; i++)
            {
                StringBuilder sb = new StringBuilder("CAH-S");
                for (int k = 0; k < 5; k++)
                {
                    sb.Append(CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]);
                }
                string code = sb.ToString();
                var (dt, emsg) = await Helper.GetDataTable("SELECT COUNT(*) FROM APPOINTMENT WHERE APPT_CODE = @code", new List<SqlParameter> { App.P("code", code, 20) });
                if (emsg == string.Empty && Convert.ToInt32(dt.Rows[0][0]) == 0)
                {
                    return code;
                }
            }
            return string.Empty;
        }

        private static string Why(string r)
        {
            if (r.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase))
            {
                return "That run was just taken for these nights. Pick another run.";
            }
            if (r.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase))
            {
                return "This pet already has a stay on these nights.";
            }
            if (r.Contains("STATECHANGED", StringComparison.OrdinalIgnoreCase))
            {
                return "Someone else just changed this stay. The board is refreshed.";
            }
            return "This could not be saved. Try again.";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private DateTime Start(DateTime today)
        {
            DateTime? d = Ui.ParseDay(V("bdstart", 10));
            if (d == null || d.Value < today.AddDays(-400) || d.Value > today.AddDays(800))
            {
                return today;
            }
            return d.Value;
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
                response.SetElementContents("bd-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            DateTime start = Start(today);
            var (strip, sum) = await TodayHtml(c, s);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-ptop\"><div><h1>Boarding</h1><p id=\"bd-sum\">" + sum + "</p></div><div class=\"bd-acts\">");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-accent\" onclick=\"AdminBoardingJs.newStay('', '', '')\">New stay</button>");
            }
            sb.Append("</div></div>");
            sb.Append("<div class=\"bd-tabs\" role=\"tablist\"><button type=\"button\" role=\"tab\" class=\"bd-tab bd-on\" id=\"bd-tb-board\" aria-selected=\"true\" onclick=\"AdminBoardingJs.tab('board')\">Board</button><button type=\"button\" role=\"tab\" class=\"bd-tab\" id=\"bd-tb-list\" aria-selected=\"false\" onclick=\"AdminBoardingJs.tab('list')\">All stays</button></div>");
            sb.Append("<section id=\"bd-vboard\"><div id=\"bd-today\">" + strip + "</div>");
            sb.Append("<div class=\"bd-panel bd-bpanel\"><div class=\"bd-bctl\"><div class=\"bd-nav\"><button type=\"button\" class=\"bd-btn bd-btn-ghost bd-btn-sm bd-arrow\" aria-label=\"Previous week\" onclick=\"AdminBoardingJs.prev()\">&larr;</button><button type=\"button\" class=\"bd-btn bd-btn-ghost bd-btn-sm\" onclick=\"AdminBoardingJs.goToday()\">Today</button><button type=\"button\" class=\"bd-btn bd-btn-ghost bd-btn-sm bd-arrow\" aria-label=\"Next week\" onclick=\"AdminBoardingJs.next()\">&rarr;</button></div>");
            sb.Append("<label class=\"bd-field bd-dpick\"><span class=\"bd-sr\">Board starts on</span><input type=\"date\" id=\"bd-bday\" value=\"" + Ui.Ymd(start) + "\" onchange=\"AdminBoardingJs.pickDay()\"></label><span class=\"bd-brange\" id=\"bd-brange\">" + Ui.Enc(Ui.Day(start, false) + " – " + Ui.Day(start.AddDays(Days - 1), start.Year != start.AddDays(Days - 1).Year)) + "</span></div>");
            sb.Append("<div id=\"bd-board\">" + await BoardHtml(c, s, start) + "</div>");
            sb.Append(Legend(c));
            sb.Append("</div></section>");
            sb.Append("<section id=\"bd-vlist\" hidden><div class=\"bd-bar\"><label class=\"bd-field bd-grow2\"><span>Search</span><input type=\"search\" id=\"bd-lterm\" placeholder=\"Pet, owner, phone or stay code\" autocomplete=\"off\" oninput=\"AdminBoardingJs.typed()\"></label>");
            sb.Append("<label class=\"bd-field\"><span>Status</span><select id=\"bd-lstat\" onchange=\"AdminBoardingJs.find(1)\"><option value=\"\">All</option>" + Ui.Opts(States.Select(x => (x, StateText(x))), string.Empty) + "</select></label>");
            sb.Append("<label class=\"bd-field\"><span>From</span><input type=\"date\" id=\"bd-lfrom\" value=\"" + Ui.Ymd(today) + "\" onchange=\"AdminBoardingJs.find(1)\"></label><label class=\"bd-field\"><span>To</span><input type=\"date\" id=\"bd-lto\" onchange=\"AdminBoardingJs.find(1)\"></label>");
            sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.clearList()\">Clear</button></div><div id=\"bd-list\"><p class=\"bd-dim\">Loading&hellip;</p></div></section>");
            response.SetElementContents("bd-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('" + Ui.Ymd(start) + "', '" + Ui.Ymd(today) + "', " + (c.Can(Key, 'A') ? "1" : "0") + ");");
            string openA = Id(V("openstay", 50));
            string newPet = Id(V("newpet", 50));
            if (openA != string.Empty)
            {
                BdStay? st = await StayById(openA);
                if (st == null)
                {
                    Toast(response, "That stay was not found.");
                }
                else
                {
                    response.SetElementContents("bd-mbody", await StayHtml(c, s, st));
                    response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
                }
            }
            else if (newPet != string.Empty)
            {
                if (!c.Can(Key, 'A'))
                {
                    Toast(response, "You don't have the right to book stays.");
                }
                else
                {
                    await OpenForm(response, c, s, newPet, string.Empty, null);
                }
            }
            return response;
        }

        private static string Legend(Ctx c)
        {
            return "<div class=\"bd-legend\"><span><i class=\"bd-lg bd-b-booked\"></i>Booked</span><span><i class=\"bd-lg bd-b-confirmed\"></i>Confirmed</span><span><i class=\"bd-lg bd-b-inhouse\"></i>In house</span><span><i class=\"bd-lg bd-b-overdue\"></i>Past check-out</span><span><i class=\"bd-lg bd-b-ready\"></i>Ready for checkout</span><span><i class=\"bd-lg bd-b-done\"></i>Checked out</span><span><i class=\"bd-lg bd-b-confirmed bd-vx\"></i>Vaccines missing</span>" + (c.Can(Key, 'A') ? "<span class=\"bd-lhint\">Click an empty day to book that run.</span>" : string.Empty) + "</div>";
        }

        private async Task Paint(ApiResponse response, Ctx c, Dictionary<string, string> s)
        {
            DateTime today = App.Now(s).Date;
            DateTime start = Start(today);
            var (strip, sum) = await TodayHtml(c, s);
            response.SetElementContents("bd-today", strip);
            response.SetElementContents("bd-sum", sum);
            response.SetElementContents("bd-board", await BoardHtml(c, s, start));
            response.SetElementContents("bd-brange", Ui.Enc(Ui.Day(start, false) + " – " + Ui.Day(start.AddDays(Days - 1), start.Year != start.AddDays(Days - 1).Year)));
            response.ExecuteScript(Me + ".boardAt('" + Ui.Ymd(start) + "', '" + Ui.Ymd(today) + "');");
        }

        public async Task<ApiResponse> Board()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bd-main", Denied());
                return response;
            }
            await Paint(response, c, await App.Settings());
            return response;
        }

        private async Task Refresh(ApiResponse response, Ctx c, Dictionary<string, string> s, string apptId, string msg)
        {
            await Paint(response, c, s);
            if (apptId != string.Empty)
            {
                BdStay? st = await StayById(apptId);
                if (st != null)
                {
                    response.SetElementContents("bd-mbody", await StayHtml(c, s, st));
                    response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
                }
            }
            if (msg != string.Empty)
            {
                Toast(response, msg);
            }
            response.ExecuteScript(Me + ".after();");
        }

        private static async Task<List<BdStay>> Window(DateTime w0, DateTime w1)
        {
            var (list, emsg) = await Helper.GetListT<BdStay>("SELECT " + StayCols + StayFrom + " AND A.STATUS NOT IN (N'cancelled', N'noshow') AND A.START_AT < @w1 AND (A.END_AT >= @w0 OR V.PLAN_OUT >= @w0 OR V.READY_AT >= @w0 OR V.STATUS = N'inroom') ORDER BY A.START_AT", new List<SqlParameter> { App.PDate("w0", w0), App.PDate("w1", w1) });
            return emsg == string.Empty && list != null ? list : new List<BdStay>();
        }

        private static async Task<List<BdSeg>> Segs(List<string> visitIds)
        {
            if (visitIds.Count == 0)
            {
                return new List<BdSeg>();
            }
            List<SqlParameter> ps = new List<SqlParameter>();
            string inList = Slots.InList(visitIds, "sv", ps);
            var (list, emsg) = await Helper.GetListT<BdSeg>("SELECT X.VISIT_ID AS VisitId, X.ROOM_ID AS RoomId, ISNULL(R.ROOM_NAME, N'') AS RoomName, ISNULL(R.ROOMTYPE_ID, N'') AS RoomtypeId, X.IN_AT AS InAt, X.OUT_AT AS OutAt FROM VISITROOM X LEFT JOIN ROOM R ON R.ROOM_ID = X.ROOM_ID WHERE X.VISIT_ID IN (" + inList + ") ORDER BY X.IN_AT", ps);
            return emsg == string.Empty && list != null ? list : new List<BdSeg>();
        }

        private static string BarState(BdStay st, DateTime today)
        {
            if (st.State == "inhouse")
            {
                return PlanEnd(st).Date < today ? "overdue" : "inhouse";
            }
            return st.State;
        }

        private static DateTime EffEnd(BdStay st, DateTime today)
        {
            DateTime p = PlanEnd(st).Date;
            if (st.State == "inhouse")
            {
                return p < today ? today.AddDays(2) : p;
            }
            if (st.ReadyAt.HasValue && st.State != "booked" && st.State != "confirmed")
            {
                return st.ReadyAt.Value.Date;
            }
            return p;
        }

        private static List<Bar> Bars(Dictionary<string, string> s, List<BdStay> stays, List<BdSeg> segs, List<BdVaxRow> vax, DateTime start, DateTime today)
        {
            List<Bar> bars = new List<Bar>();
            void Add(BdStay st, string room, DateTime from, DateTime to, string cls, bool vx)
            {
                int o0 = (from.Date - start).Days;
                int o1 = (to.Date - start).Days;
                double l = o0 + 0.5;
                double r = o1 > o0 ? o1 + 0.5 : l + 0.5;
                if (r <= 0 || l >= Days)
                {
                    return;
                }
                bars.Add(new Bar { RoomId = room, L = Math.Max(l, 0), R = Math.Min(r, Days), CutL = l < 0, CutR = r > Days, N0 = o0, N1 = o1, St = st, Cls = cls, Vx = vx });
            }
            foreach (BdStay st in stays)
            {
                if (st.State == "booked" || st.State == "confirmed")
                {
                    bool vx = VaxCheck(s, st.SpeciesKey, st.PetId, vax, st.EndAt).Any(x => !x.Ok);
                    Add(st, st.RoomId, st.StartAt, st.EndAt, st.State, vx);
                    continue;
                }
                List<BdSeg> mine = segs.Where(x => x.VisitId == st.VisitId && st.VisitId != string.Empty).OrderBy(x => x.InAt).ToList();
                string cls = BarState(st, today);
                if (mine.Count == 0)
                {
                    Add(st, st.CurRoomId != string.Empty ? st.CurRoomId : st.RoomId, InDay(st), EffEnd(st, today), cls, false);
                    continue;
                }
                for (int i = 0; i < mine.Count; i++)
                {
                    BdSeg g = mine[i];
                    bool last = i == mine.Count - 1;
                    DateTime from = g.InAt.Date;
                    DateTime to = g.OutAt.HasValue ? g.OutAt.Value.Date : EffEnd(st, today);
                    if (to <= from && !last)
                    {
                        continue;
                    }
                    Add(st, g.RoomId, from, to, g.OutAt.HasValue && st.State == "inhouse" ? "moved" : cls, false);
                }
            }
            return bars;
        }

        private static async Task<Dictionary<DateTime, string>> Holidays(DateTime a, DateTime b)
        {
            var (list, emsg) = await Helper.GetListT<BdDay>("SELECT HOLIDAY_DATE AS HolidayDate, HOLIDAY_NAME AS HolidayName FROM HOLIDAY WHERE HOLIDAY_DATE >= @a AND HOLIDAY_DATE < @b", new List<SqlParameter> { App.PDate("a", a), App.PDate("b", b) });
            Dictionary<DateTime, string> d = new Dictionary<DateTime, string>();
            foreach (BdDay h in emsg == string.Empty && list != null ? list : new List<BdDay>())
            {
                d[h.HolidayDate.Date] = h.HolidayName;
            }
            return d;
        }

        private async Task<string> BoardHtml(Ctx c, Dictionary<string, string> s, DateTime start)
        {
            DateTime today = App.Now(s).Date;
            DateTime end = start.AddDays(Days);
            List<BdRun> runs = await Runs();
            if (runs.Count == 0)
            {
                return "<p class=\"bd-empty\">No boarding runs are set up yet.</p>";
            }
            List<BdStay> stays = await Window(start, end);
            List<BdSeg> segs = await Segs(stays.Where(x => x.VisitId != string.Empty).Select(x => x.VisitId).Distinct().ToList());
            Dictionary<DateTime, string> hols = await Holidays(start, end);
            List<BdVaxRow> vax = await VaxRows(stays.Where(x => x.State == "booked" || x.State == "confirmed").Select(x => x.PetId).Distinct().ToList());
            List<Bar> bars = Bars(s, stays, segs, vax, start, today);
            List<BdItem> items = await Items();
            bool add = c.Can(Key, 'A');
            string[] cls = new string[Days];
            bool[] open = new bool[Days];
            for (int i = 0; i < Days; i++)
            {
                DateTime d = start.AddDays(i);
                bool shut = hols.ContainsKey(d) || !Slots.Hours(s, d).Open;
                open[i] = !shut && d >= today;
                List<string> k = new List<string>();
                if (d == today)
                {
                    k.Add("bd-td");
                }
                if (shut)
                {
                    k.Add("bd-cx");
                }
                else if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                {
                    k.Add("bd-we");
                }
                if (d < today)
                {
                    k.Add("bd-past");
                }
                cls[i] = string.Join(" ", k);
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-bwrap\"><div class=\"bd-grid\" onclick=\"AdminBoardingJs.hit(event)\"><div class=\"bd-r bd-hd\"><div class=\"bd-rn\"><b>" + Ui.Enc(start.AddDays(Days - 1).Month != start.Month ? start.ToString("MMM", Ui.Us) + " – " + start.AddDays(Days - 1).ToString("MMM yyyy", Ui.Us) : start.ToString("MMMM yyyy", Ui.Us)) + "</b></div>");
            for (int i = 0; i < Days; i++)
            {
                DateTime d = start.AddDays(i);
                string title = hols.TryGetValue(d, out string? hn) ? hn : !Slots.Hours(s, d).Open ? "Clinic closed" : string.Empty;
                sb.Append("<div class=\"bd-c bd-dh " + cls[i] + "\"" + (title != string.Empty ? " title=\"" + Ui.Attr(title) + "\"" : string.Empty) + "><small>" + d.ToString("ddd", Ui.Us) + "</small><b>" + d.Day.ToString(Ui.Inv) + "</b>" + (i > 0 && d.Day == 1 ? "<i>" + d.ToString("MMM", Ui.Us) + "</i>" : string.Empty) + "</div>");
            }
            sb.Append("</div>");
            foreach (IGrouping<string, BdRun> g in runs.GroupBy(r => r.RoomtypeId))
            {
                List<BdRun> list = g.ToList();
                BdItem? night = items.FirstOrDefault(x => x.ItemId == list[0].NightItemId);
                HashSet<string> ids = list.Select(x => x.RoomId).ToHashSet();
                sb.Append("<div class=\"bd-r bd-th\"><div class=\"bd-rn\"><b>" + Ui.Enc(list[0].TypeName) + "</b><small>" + Ui.Plural(list.Count, "run", "runs") + (night != null ? " &middot; $" + Ui.Num(night.Price) : string.Empty) + "</small></div>");
                for (int i = 0; i < Days; i++)
                {
                    int used = bars.Where(b => ids.Contains(b.RoomId) && b.N0 <= i && b.N1 > i).Select(b => b.RoomId).Distinct().Count();
                    int free = list.Count - used;
                    sb.Append("<div class=\"bd-c bd-occ " + cls[i] + (free <= 0 ? " bd-full" : used == 0 ? " bd-free" : string.Empty) + "\" title=\"" + used + " of " + list.Count + " taken\"><b>" + (free <= 0 ? "Full" : free.ToString(Ui.Inv)) + "</b>" + (free > 0 ? "<small>free</small>" : string.Empty) + "</div>");
                }
                sb.Append("</div>");
                foreach (BdRun r in list)
                {
                    sb.Append("<div class=\"bd-r bd-run\"><div class=\"bd-rn\"><b>" + Ui.Enc(r.RoomName) + "</b></div>");
                    for (int i = 0; i < Days; i++)
                    {
                        bool can = add && open[i] && r.Live;
                        sb.Append("<div class=\"bd-c " + cls[i] + (can ? " bd-add" : string.Empty) + "\"" + (can ? " data-r=\"" + Ui.Attr(r.RoomId) + "\" data-d=\"" + Ui.Ymd(start.AddDays(i)) + "\" title=\"Book " + Ui.Attr(r.RoomName) + " from " + Ui.Attr(Ui.Day(start.AddDays(i), false)) + "\"" : string.Empty) + "></div>");
                    }
                    sb.Append("<div class=\"bd-trk\">");
                    foreach (Bar b in bars.Where(x => x.RoomId == r.RoomId).OrderBy(x => x.L))
                    {
                        BdStay st = b.St;
                        string range = Ui.Day(st.State == "booked" || st.State == "confirmed" ? st.StartAt : InDay(st), false) + " – " + Ui.Day(st.State == "booked" || st.State == "confirmed" ? st.EndAt : EffEnd(st, today), false);
                        string tip = st.PetName + " (" + SpeciesWord(st.SpeciesKey) + ") · " + st.OwnerFirst + " " + st.OwnerLast + " · " + range + " · " + (b.Cls == "overdue" ? "Past check-out" : b.Cls == "moved" ? "Moved to another run" : StateText(st.State)) + (b.Vx ? " · Vaccines missing" : string.Empty);
                        sb.Append("<button type=\"button\" class=\"bd-sb bd-b-" + b.Cls + (b.Vx ? " bd-vx" : string.Empty) + (b.CutL ? " bd-cl" : string.Empty) + (b.CutR ? " bd-cr" : string.Empty) + "\" style=\"--bd-l:" + Num(b.L) + ";--bd-w:" + Num(b.R - b.L) + "\" data-a=\"" + Ui.Attr(st.ApptId) + "\" title=\"" + Ui.Attr(tip) + "\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.OwnerLast) + "</small></button>");
                    }
                    sb.Append("</div></div>");
                }
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private async Task<(string Html, string Sum)> TodayHtml(Ctx c, Dictionary<string, string> s)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            var (all, emsg) = await Helper.GetListT<BdStay>("SELECT " + StayCols + StayFrom + " AND ((A.STATUS IN (N'booked', N'confirmed') AND A.START_AT < @d1 AND A.START_AT >= @old) OR (A.STATUS = N'checkedin' AND V.STATUS IN (N'inroom', N'checkout'))) ORDER BY A.START_AT, P.PET_NAME", new List<SqlParameter> { App.PDate("d1", today.AddDays(1)), App.PDate("old", today.AddDays(-14)) });
            List<BdStay> live = emsg == string.Empty && all != null ? all : new List<BdStay>();
            List<BdStay> arrive = live.Where(x => x.State == "booked" || x.State == "confirmed").ToList();
            List<BdStay> house = live.Where(x => x.State == "inhouse").OrderBy(x => x.CurRoomName).ToList();
            List<BdStay> leave = house.Where(x => PlanEnd(x).Date == today).OrderBy(x => PlanEnd(x)).ToList();
            List<BdStay> late = house.Where(x => PlanEnd(x).Date < today).OrderBy(x => PlanEnd(x)).ToList();
            List<BdStay> ready = live.Where(x => x.State == "ready").OrderBy(x => x.ReadyAt).ToList();
            List<BdVaxRow> vax = await VaxRows(arrive.Select(x => x.PetId).Distinct().ToList());
            bool edit = c.Can(Key, 'E');
            bool pay = c.Can("checkout", 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-strip\">");
            sb.Append("<div class=\"bd-col\"><div class=\"bd-colh\"><h2>Arriving today</h2><span class=\"bd-n\">" + arrive.Count + "</span></div>");
            if (arrive.Count == 0)
            {
                sb.Append("<p class=\"bd-none\">No more arrivals today.</p>");
            }
            foreach (BdStay st in arrive)
            {
                List<VaxLine> vl = VaxCheck(s, st.SpeciesKey, st.PetId, vax, st.EndAt);
                List<string> miss = vl.Where(x => !x.Ok).Select(x => x.Name).ToList();
                bool lateIn = st.StartAt.Date < today;
                sb.Append("<div class=\"bd-ti\"><button type=\"button\" class=\"bd-tmain\" onclick=\"AdminBoardingJs.open('" + Ui.Js(st.ApptId) + "')\"><span class=\"bd-tn\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.OwnerLast) + "</small></span><span class=\"bd-tm\">" + (lateIn ? "<em class=\"bd-late\">Was due " + Ui.Enc(Ui.Day(st.StartAt, false)) + "</em>" : Ui.Enc(Ui.Hm(st.StartAt))) + " &middot; " + Ui.Enc(st.RoomName) + " &middot; " + Ui.Plural(Nights(st.StartAt, st.EndAt), "night", "nights") + "</span>");
                sb.Append(miss.Count == 0 ? "<span class=\"bd-tag bd-tag-ok\">Vaccines OK</span>" : "<span class=\"bd-tag bd-tag-warn\">Missing: " + Ui.Enc(string.Join(", ", miss)) + "</span>");
                sb.Append("</button>");
                if (edit && st.EndAt.Date <= today)
                {
                    sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-sm bd-btn-danger\" onclick=\"AdminBoardingJs.noShowFor('" + Ui.Js(st.ApptId) + "')\">No-show</button>");
                }
                else if (edit)
                {
                    sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-sm bd-btn-accent\" onclick=\"AdminBoardingJs.checkIn('" + Ui.Js(st.ApptId) + "')\">Check in</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"bd-col\"><div class=\"bd-colh\"><h2>Going home today</h2><span class=\"bd-n\">" + (leave.Count + ready.Count) + "</span></div>");
            if (leave.Count == 0 && ready.Count == 0)
            {
                sb.Append("<p class=\"bd-none\">No one is due to go home today.</p>");
            }
            foreach (BdStay st in leave)
            {
                sb.Append("<div class=\"bd-ti\"><button type=\"button\" class=\"bd-tmain\" onclick=\"AdminBoardingJs.open('" + Ui.Js(st.ApptId) + "')\"><span class=\"bd-tn\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.OwnerLast) + "</small></span><span class=\"bd-tm\">By " + Ui.Enc(Ui.Hm(PlanEnd(st))) + " &middot; " + Ui.Enc(st.CurRoomName) + "</span></button>");
                if (edit)
                {
                    sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-sm\" onclick=\"AdminBoardingJs.checkOut('" + Ui.Js(st.ApptId) + "')\">Check out</button>");
                }
                sb.Append("</div>");
            }
            foreach (BdStay st in ready)
            {
                sb.Append("<div class=\"bd-ti\"><button type=\"button\" class=\"bd-tmain\" onclick=\"AdminBoardingJs.open('" + Ui.Js(st.ApptId) + "')\"><span class=\"bd-tn\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.OwnerLast) + "</small></span><span class=\"bd-tm\">" + Chip("ready") + "</span></button>");
                if (pay)
                {
                    sb.Append("<a class=\"bd-btn bd-btn-sm bd-btn-ghost\" href=\"AdminCheckout?v=" + Ui.Attr(Uri.EscapeDataString(st.VisitId)) + "\">Take payment</a>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"bd-col\"><div class=\"bd-colh\"><h2>In house now</h2><span class=\"bd-n\">" + house.Count + "</span></div>");
            if (house.Count == 0)
            {
                sb.Append("<p class=\"bd-none\">No pets are staying with us right now.</p>");
            }
            else
            {
                sb.Append("<div class=\"bd-chips\">");
                foreach (BdStay st in house)
                {
                    sb.Append("<button type=\"button\" class=\"bd-hchip\" onclick=\"AdminBoardingJs.open('" + Ui.Js(st.ApptId) + "')\" title=\"" + Ui.Attr(st.PetName + " · " + st.OwnerFirst + " " + st.OwnerLast + " · until " + Ui.Day(PlanEnd(st), false)) + "\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.CurRoomName) + "</small></button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"bd-col" + (late.Count > 0 ? " bd-col-late" : string.Empty) + "\"><div class=\"bd-colh\"><h2>Past check-out</h2><span class=\"bd-n\">" + late.Count + "</span></div>");
            if (late.Count == 0)
            {
                sb.Append("<p class=\"bd-none\">No one is past their check-out day.</p>");
            }
            foreach (BdStay st in late)
            {
                sb.Append("<div class=\"bd-ti\"><button type=\"button\" class=\"bd-tmain\" onclick=\"AdminBoardingJs.open('" + Ui.Js(st.ApptId) + "')\"><span class=\"bd-tn\">" + Sp(st.SpeciesKey) + "<b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(st.OwnerLast) + "</small></span><span class=\"bd-tm\"><em class=\"bd-late\">Was due " + Ui.Enc(Ui.Day(PlanEnd(st), false)) + "</em> &middot; " + Ui.Enc(st.CurRoomName) + "</span></button>");
                if (edit)
                {
                    sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-sm\" onclick=\"AdminBoardingJs.checkOut('" + Ui.Js(st.ApptId) + "')\">Check out</button>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div></div>");
            List<string> parts = new List<string> { Ui.Enc(Ui.LongDay(today)), Ui.Plural(house.Count, "pet in house", "pets in house"), arrive.Count + " arriving", (leave.Count + ready.Count) + " going home" };
            if (late.Count > 0)
            {
                parts.Add("<b class=\"bd-late\">" + late.Count + " past check-out</b>");
            }
            return (sb.ToString(), string.Join(" &middot; ", parts));
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
                response.SetElementContents("bd-main", Denied());
                return response;
            }
            string term = Ui.OneLine(V("lterm", 60), 60);
            string st = V("lstat", 20);
            st = States.Contains(st) ? st : string.Empty;
            DateTime? from = Ui.ParseDay(V("lfrom", 10));
            DateTime? to = Ui.ParseDay(V("lto", 10));
            string digits = Ui.Digits(term);
            string where = " AND (@q = N'' OR P.PET_NAME LIKE @ql OR C.FIRST_NAME LIKE @ql OR C.LAST_NAME LIKE @ql OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @ql OR A.APPT_CODE LIKE @ql OR (@qd <> N'' AND " + PhoneDigits + " LIKE @qdl)) AND (@hf = 0 OR ISNULL(V.PLAN_OUT, A.END_AT) >= @from) AND (@ht = 0 OR A.START_AT < @to)";
            string outer = " WHERE (@st = N'' OR Z.State = @st OR (@st = N'upcoming' AND Z.State IN (N'booked', N'confirmed')))";
            List<SqlParameter> Ps()
            {
                return new List<SqlParameter>
                {
                    App.P("q", term, 60), App.P("ql", Like(term), 200), App.P("qd", digits.Length >= 3 ? digits : string.Empty, 30), App.P("qdl", Like(digits), 60),
                    App.PBit("hf", from.HasValue), App.PDate("from", from ?? DateTime.Today), App.PBit("ht", to.HasValue), App.PDate("to", (to ?? DateTime.Today).AddDays(1)), App.P("st", st, 20)
                };
            }
            string inner = "SELECT " + StayCols + StayFrom + where;
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM (" + inner + ") Z" + outer, Ps());
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            int page = Ui.PageOf(V("pg", 6), PageSize, total);
            List<SqlParameter> ps = Ps();
            ps.Add(App.PInt("off", (page - 1) * PageSize));
            ps.Add(App.PInt("n", PageSize));
            var (list, emsg) = await Helper.GetListT<BdStay>("SELECT * FROM (" + inner + ") Z" + outer + " ORDER BY Z.StartAt, Z.PetName OFFSET @off ROWS FETCH NEXT @n ROWS ONLY", ps);
            if (emsg != string.Empty || list == null)
            {
                response.SetElementContents("bd-list", "<p class=\"bd-empty\">The list could not be loaded. Try again.</p>");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"bd-panel bd-empty\">No stays match.</div>");
            }
            else
            {
                sb.Append("<div class=\"bd-tw\"><table class=\"bd-table\"><thead><tr><th>Dates</th><th>Nights</th><th>Run</th><th>Pet</th><th>Owner</th><th>Status</th><th class=\"bd-num\">Estimate</th></tr></thead><tbody>");
                foreach (BdStay x in list)
                {
                    bool ins = x.State == "inhouse" || x.State == "ready" || x.State == "done";
                    DateTime a = ins ? InDay(x) : x.StartAt;
                    DateTime z = ins ? (x.State == "inhouse" ? PlanEnd(x) : x.ReadyAt ?? PlanEnd(x)) : x.EndAt;
                    string run = x.CurRoomName != string.Empty ? x.CurRoomName : x.RoomName;
                    sb.Append("<tr class=\"bd-row\" tabindex=\"0\" onclick=\"AdminBoardingJs.open('" + Ui.Js(x.ApptId) + "')\" onkeydown=\"if(event.key==='Enter')AdminBoardingJs.open('" + Ui.Js(x.ApptId) + "')\">");
                    sb.Append("<td class=\"bd-cname\"><b>" + Ui.Enc(Ui.Day(a, false) + " – " + Ui.Day(z, false)) + "</b><small>" + Ui.Enc(x.ApptCode) + "</small></td>");
                    sb.Append("<td data-l=\"Nights\">" + Math.Max(1, Nights(a, z)) + "</td>");
                    sb.Append("<td data-l=\"Run\">" + Ui.Enc(run) + "<small class=\"bd-dim bd-blk\">" + Ui.Enc(x.TypeName) + "</small></td>");
                    sb.Append("<td data-l=\"Pet\"><span class=\"bd-nw\">" + Sp(x.SpeciesKey) + " " + Ui.Enc(x.PetName) + "</span>" + (x.PetAlert != string.Empty ? "<span class=\"bd-tag bd-tag-alert\">" + Ui.Enc(x.PetAlert) + "</span>" : string.Empty) + "</td>");
                    sb.Append("<td data-l=\"Owner\">" + Ui.Enc(x.OwnerFirst + " " + x.OwnerLast) + "<small class=\"bd-dim bd-blk\">" + Ui.Enc(x.Phone) + "</small></td>");
                    sb.Append("<td data-l=\"Status\">" + Chip(x.State) + (x.VaxPending && (x.State == "booked" || x.State == "confirmed") ? "<span class=\"bd-tag bd-tag-warn\">Vaccines pending</span>" : string.Empty) + "</td>");
                    sb.Append("<td data-l=\"Estimate\" class=\"bd-num\">" + Ui.Money(x.EstPrice) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            sb.Append(Ui.Pager("bd", page, PageSize, total, "stay", "stays", Me + ".find"));
            response.SetElementContents("bd-list", sb.ToString());
            response.ExecuteScript(Me + ".listed(" + page + ");");
            return response;
        }

        private async Task<BdStay?> Reach(ApiResponse response, Ctx c, char right, string action)
        {
            if (!c.Can(Key, 'V'))
            {
                response.ExecuteScript(Me + ".closeModal();");
                response.SetElementContents("bd-main", Denied());
                return null;
            }
            if (right != 'V' && !c.Can(Key, right))
            {
                await App.Log(c.U.StaffId, action, "boarding · no right", false);
                Toast(response, right == 'X' ? "You don't have the right to cancel stays." : right == 'A' ? "You don't have the right to book stays." : "You can look at stays but not change them.");
                return null;
            }
            BdStay? st = await StayById(Id(V("apptid", 50)));
            if (st == null)
            {
                Toast(response, "This stay was not found.");
                return null;
            }
            return st;
        }

        public async Task<ApiResponse> Stay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'V', "open stay");
            if (st == null)
            {
                return response;
            }
            response.SetElementContents("bd-mbody", await StayHtml(c, await App.Settings(), st));
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
            return response;
        }

        private async Task<string> StayHtml(Ctx c, Dictionary<string, string> s, BdStay st)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            Rules r = Rule(s);
            bool edit = c.Can(Key, 'E');
            bool open = st.State == "booked" || st.State == "confirmed";
            bool inh = st.State == "inhouse";
            List<BdItem> items = await Items();
            List<BdRun> runs = await Runs();
            List<BdVaxRow> vax = await VaxRows(new List<string> { st.PetId });
            DateTime outAt = inh ? PlanEnd(st) : st.EndAt;
            List<VaxLine> vl = VaxCheck(s, st.SpeciesKey, st.PetId, vax, outAt);
            List<BdSeg> segs = st.VisitId != string.Empty ? await Segs(new List<string> { st.VisitId }) : new List<BdSeg>();
            StringBuilder sb = new StringBuilder();
            string room = st.CurRoomName != string.Empty ? st.CurRoomName : segs.Count > 0 ? segs[^1].RoomName : st.RoomName;
            sb.Append("<div class=\"bd-dhead\"><div><h2>" + Ui.Enc(st.PetName) + " <small class=\"bd-code\">" + Ui.Enc(st.ApptCode) + "</small></h2><p class=\"bd-sub\">" + Ui.Enc(st.TypeName + " · " + room) + "</p></div><div class=\"bd-acts\">" + Chip(BarState(st, today) == "overdue" ? "overdue" : st.State) + (open && vl.Any(x => !x.Ok) ? "<span class=\"bd-tag bd-tag-warn\">Vaccines missing</span>" : string.Empty) + "</div></div>");
            if (st.PetAlert != string.Empty)
            {
                sb.Append("<div class=\"bd-alert\"><b>Pet alert</b><span>" + Ui.Enc(st.PetAlert) + "</span></div>");
            }
            if (st.ClientAlert != string.Empty)
            {
                sb.Append("<div class=\"bd-alert\"><b>Owner</b><span>" + Ui.Enc(st.ClientAlert) + "</span></div>");
            }
            List<string> acts = new List<string>();
            if (open && edit)
            {
                if (st.StartAt.Date <= today && st.EndAt.Date > today)
                {
                    acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-accent\" onclick=\"AdminBoardingJs.checkIn('" + Ui.Js(st.ApptId) + "')\">Check in</button>");
                }
                if (st.Status == "booked")
                {
                    acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" id=\"bd-xconf\" onclick=\"AdminBoardingJs.confirmStay(this)\">Confirm</button>");
                }
                acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.edit()\">Change</button>");
                if (today > st.StartAt.Date)
                {
                    acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.noShow()\">No-show</button>");
                }
            }
            if (open && c.Can(Key, 'X'))
            {
                acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-danger\" onclick=\"AdminBoardingJs.cancelForm()\">Cancel stay</button>");
            }
            if (inh && edit)
            {
                acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-accent\" onclick=\"AdminBoardingJs.checkOut('" + Ui.Js(st.ApptId) + "')\">Check out</button>");
                acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.extend()\">Change check-out</button>");
                acts.Add("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.move()\">Move run</button>");
            }
            if (st.State == "ready" && c.Can("checkout", 'V'))
            {
                acts.Add("<a class=\"bd-btn bd-btn-accent\" href=\"AdminCheckout?v=" + Ui.Attr(Uri.EscapeDataString(st.VisitId)) + "\">Take payment</a>");
            }
            if (acts.Count > 0)
            {
                sb.Append("<div class=\"bd-acts bd-dacts\">" + string.Join(string.Empty, acts) + "</div>");
            }
            sb.Append("<div class=\"bd-dgrid\">");
            sb.Append("<div class=\"bd-box\"><h3>Pet</h3><div class=\"bd-pet\">" + Ui.Avatar("bd", st.PetId, st.PetName, st.Photo, "lg") + "<div><b>" + Ui.Enc(st.PetName) + "</b><small>" + Ui.Enc(string.Join(" · ", new[] { SpeciesWord(st.SpeciesKey), st.Breed, SizeWord(st.Size) }.Where(x => x != string.Empty))) + "</small></div></div><div class=\"bd-links\">");
            if (c.Can("pets", 'V'))
            {
                sb.Append("<a href=\"AdminPets?p=" + Ui.Attr(Uri.EscapeDataString(st.PetId)) + "\">Pet record</a>");
            }
            if (c.Can("charts", 'V'))
            {
                sb.Append("<a href=\"AdminChart?p=" + Ui.Attr(Uri.EscapeDataString(st.PetId)) + "\">Patient chart</a>");
            }
            sb.Append("</div></div>");
            sb.Append("<div class=\"bd-box\"><h3>Owner</h3><dl class=\"bd-dl\"><dt>Name</dt><dd>" + (c.Can("clients", 'V') ? "<a href=\"AdminClients?c=" + Ui.Attr(Uri.EscapeDataString(st.ClientId)) + "\">" + Ui.Enc(st.OwnerFirst + " " + st.OwnerLast) + "</a>" : Ui.Enc(st.OwnerFirst + " " + st.OwnerLast)) + "</dd>");
            if (st.Phone != string.Empty)
            {
                sb.Append("<dt>Phone</dt><dd><a href=\"tel:" + Ui.Attr(Ui.CleanPhone(st.Phone)) + "\">" + Ui.Enc(st.Phone) + "</a></dd>");
            }
            if (st.Email != string.Empty)
            {
                sb.Append("<dt>Email</dt><dd class=\"bd-mail\">" + Ui.Enc(st.Email) + "</dd>");
            }
            sb.Append("<dt>Booked</dt><dd>" + Ui.Enc(st.Source == "web" ? "Online" : st.Source == "desk" ? "At the desk" : "By phone") + " &middot; " + Ui.Time(st.CreatedUtc, "day") + "</dd></dl></div>");
            sb.Append("</div>");
            sb.Append("<div class=\"bd-dgrid\"><div class=\"bd-box\"><h3>Stay</h3><dl class=\"bd-dl\">");
            if (open)
            {
                sb.Append("<dt>Check-in</dt><dd>" + Ui.Enc(When(st.StartAt)) + "</dd><dt>Check-out</dt><dd>" + Ui.Enc(When(st.EndAt)) + "</dd><dt>Nights</dt><dd>" + Nights(st.StartAt, st.EndAt) + "</dd><dt>Run</dt><dd>" + Ui.Enc(st.RoomName) + "</dd>");
            }
            else if (st.State == "noshow" || st.State == "cancelled")
            {
                sb.Append("<dt>Was booked</dt><dd>" + Ui.Enc(When(st.StartAt) + " to " + When(st.EndAt)) + "</dd><dt>Run</dt><dd>" + Ui.Enc(st.RoomName) + "</dd>");
                if (st.CancelReason != string.Empty)
                {
                    sb.Append("<dt>Reason</dt><dd>" + Ui.Enc(st.CancelReason) + "</dd>");
                }
            }
            else
            {
                sb.Append("<dt>Checked in</dt><dd>" + Ui.Enc(When(st.CheckinAt ?? st.StartAt)) + "</dd>");
                if (inh)
                {
                    bool late = PlanEnd(st).Date < today;
                    sb.Append("<dt>Going home</dt><dd" + (late ? " class=\"bd-late\"" : string.Empty) + ">" + Ui.Enc(When(PlanEnd(st))) + (late ? " (past due)" : string.Empty) + "</dd><dt>Nights</dt><dd>" + Nights(InDay(st), PlanEnd(st)) + " planned</dd>");
                }
                else
                {
                    sb.Append("<dt>Checked out</dt><dd>" + Ui.Enc(st.ReadyAt.HasValue ? When(st.ReadyAt.Value) : "-") + "</dd>");
                }
                sb.Append("<dt>Run</dt><dd>" + Ui.Enc(room));
                if (segs.Count > 1)
                {
                    sb.Append("<small class=\"bd-dim bd-blk\">" + Ui.Enc(string.Join(" → ", segs.Select(g => g.RoomName + " (" + Ui.Day(g.InAt, false) + ")"))) + "</small>");
                }
                sb.Append("</dd>");
            }
            List<BdItem> adds = Csv(st.Addons).Select(id => items.FirstOrDefault(i => i.ItemId == id)).Where(i => i != null).Select(i => i!).ToList();
            sb.Append("<dt>Extras</dt><dd>" + (adds.Count == 0 ? "<span class=\"bd-dim\">None</span>" : Ui.Enc(string.Join(", ", adds.Select(a => a.ItemName)))) + "</dd></dl>");
            sb.Append("</div><div class=\"bd-box\"><h3>Vaccines</h3>");
            if (vl.Count == 0)
            {
                sb.Append("<p class=\"bd-dim\">No vaccines are required for this species.</p>");
            }
            else
            {
                sb.Append("<ul class=\"bd-vax\">");
                foreach (VaxLine v in vl)
                {
                    sb.Append("<li class=\"" + (v.Ok ? "bd-vok" : "bd-vno") + "\"><b>" + Ui.Enc(v.Name) + "</b><span>" + (v.Due == null ? "Not on file" : (v.Ok ? "Good until " : "Expired or due ") + Ui.Enc(Ui.Day(v.Due.Value))) + "</span></li>");
                }
                sb.Append("</ul><p class=\"bd-dim\">Checked against the check-out day, " + Ui.Enc(Ui.Day(outAt, false)) + ".</p>");
            }
            sb.Append("</div></div>");
            if (st.State == "ready" || st.State == "done")
            {
                var (ol, oerr) = await Helper.GetDataTable("SELECT ITEM_NAME, QTY, UNIT_PRICE FROM ORDERLINE WHERE VISIT_ID = @vid AND STATUS <> N'cancelled' ORDER BY LINE_ORDER", new List<SqlParameter> { App.P("vid", st.VisitId, 50) });
                if (oerr == string.Empty && ol.Rows.Count > 0)
                {
                    List<QLine> charged = ol.Rows.Cast<DataRow>().Select(x => new QLine { Item = new BdItem { ItemName = Convert.ToString(x[0]) ?? string.Empty }, Qty = Convert.ToDecimal(x[1]), Price = Convert.ToDecimal(x[2]) }).ToList();
                    sb.Append("<div class=\"bd-box\">" + Lines(charged, "Charges sent to the front desk", string.Empty) + "</div>");
                }
            }
            else if (st.State != "noshow" && st.State != "cancelled")
            {
                BdItem? night = NightItem(await TypeNights(), items, st.RoomtypeId);
                int n = inh ? Math.Max(1, Nights(InDay(st), PlanEnd(st))) : Math.Max(1, Nights(st.StartAt, st.EndAt));
                sb.Append("<div class=\"bd-box\">" + Lines(Charge(night, adds, n, st.Size), "Estimate", "Final charges are worked out at check-out from the nights actually stayed.") + "</div>");
            }
            sb.Append("<div class=\"bd-dgrid\"><div class=\"bd-box\"><h3>Care notes from the owner</h3>" + (st.Note != string.Empty ? "<p class=\"bd-pre\">" + Ui.Enc(st.Note) + "</p>" : "<p class=\"bd-dim\">No notes.</p>") + "</div>");
            sb.Append("<div class=\"bd-box\"><h3>Desk note</h3>");
            if (edit)
            {
                sb.Append("<label class=\"bd-field\"><span class=\"bd-sr\">Desk note</span><textarea id=\"bd-dnote\" rows=\"3\" maxlength=\"600\" placeholder=\"Only staff see this\">" + Ui.Enc(st.DeskNote) + "</textarea></label><div class=\"bd-acts bd-mt\"><button type=\"button\" class=\"bd-btn bd-btn-sm bd-btn-ghost\" id=\"bd-dsave\" onclick=\"AdminBoardingJs.deskSave()\">Save desk note</button></div>");
            }
            else
            {
                sb.Append(st.DeskNote != string.Empty ? "<p class=\"bd-pre\">" + Ui.Enc(st.DeskNote) + "</p>" : "<p class=\"bd-dim\">No desk note.</p>");
            }
            sb.Append("</div></div>");
            if (st.VisitId != string.Empty)
            {
                var (notes, nerr) = await Helper.GetListT<BdNote>("SELECT N.NOTE_ID AS NoteId, ISNULL(S.STAFF_NAME, N'') AS StaffName, N.CREATED_UTC AS CreatedUtc, N.SUBJECTIVE AS Subjective, N.OBJECTIVE AS Objective FROM VISITNOTE N LEFT JOIN STAFF S ON S.STAFF_ID = N.STAFF_ID WHERE N.VISIT_ID = @vid AND N.NOTE_KIND = N'care' ORDER BY N.CREATED_UTC DESC", new List<SqlParameter> { App.P("vid", st.VisitId, 50) });
                sb.Append("<div class=\"bd-box\" id=\"bd-carebox\"><h3>Care log</h3>");
                if (inh && edit)
                {
                    sb.Append("<div class=\"bd-tags\" role=\"group\" aria-label=\"Quick notes\">");
                    foreach (string t in Tags)
                    {
                        sb.Append("<button type=\"button\" class=\"bd-qt\" aria-pressed=\"false\" data-t=\"" + Ui.Attr(t) + "\" onclick=\"AdminBoardingJs.tag(this)\">" + Ui.Enc(t) + "</button>");
                    }
                    sb.Append("</div><label class=\"bd-field\"><span class=\"bd-sr\">Care note</span><textarea id=\"bd-ctext\" rows=\"2\" maxlength=\"1000\" placeholder=\"How was the day? Food, walks, medicines, mood\"></textarea></label><em class=\"bd-err\" id=\"bd-e-care\"></em><div class=\"bd-acts bd-mt\"><button type=\"button\" class=\"bd-btn bd-btn-sm bd-btn-accent\" id=\"bd-csave\" onclick=\"AdminBoardingJs.careSave()\">Add to care log</button></div>");
                }
                List<BdNote> nl = nerr == string.Empty && notes != null ? notes : new List<BdNote>();
                if (nl.Count == 0)
                {
                    sb.Append("<p class=\"bd-dim bd-mt\">Nothing logged yet.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"bd-care\">");
                    foreach (BdNote n in nl)
                    {
                        sb.Append("<li><div class=\"bd-carem\">" + Ui.Time(n.CreatedUtc, "full") + " &middot; " + Ui.Enc(n.StaffName) + "</div>");
                        if (n.Objective != string.Empty)
                        {
                            sb.Append("<div>" + string.Join(string.Empty, Csv(n.Objective).Select(t => "<span class=\"bd-tag bd-tag-ok\">" + Ui.Enc(t) + "</span>")) + "</div>");
                        }
                        if (n.Subjective != string.Empty)
                        {
                            sb.Append("<p class=\"bd-pre\">" + Ui.Enc(n.Subjective) + "</p>");
                        }
                        sb.Append("</li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("</div>");
            }
            var (logs, lerr) = await Helper.GetListT<BdLog>("SELECT L.LOG_UTC AS LogUtc, ISNULL(S.STAFF_NAME, N'') AS StaffName, L.LOG_TEXT AS LogText FROM APPTLOG L LEFT JOIN STAFF S ON S.STAFF_ID = L.STAFF_ID WHERE L.APPT_ID = @aid ORDER BY L.LOG_UTC DESC", new List<SqlParameter> { App.P("aid", st.ApptId, 50) });
            if (lerr == string.Empty && logs != null && logs.Count > 0)
            {
                sb.Append("<div class=\"bd-box\"><h3>History</h3><ul class=\"bd-rows\">");
                foreach (BdLog l in logs)
                {
                    sb.Append("<li class=\"bd-rl\"><span class=\"bd-rw\">" + Ui.Time(l.LogUtc, "full") + "</span><span class=\"bd-rt\">" + Ui.Enc(l.LogText) + (l.StaffName != string.Empty ? " <small>" + Ui.Enc(l.StaffName) + "</small>" : string.Empty) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            return sb.ToString();
        }

        private Ask ReadDates(Rules r)
        {
            DateTime? din = Ui.ParseDay(V("fdin", 10));
            DateTime? dout = Ui.ParseDay(V("fdout", 10));
            TimeSpan? tin = Ui.ParseHm(V("ftin", 5));
            TimeSpan? tout = Ui.ParseHm(V("ftout", 5));
            if (din == null || dout == null)
            {
                return new Ask { Error = "Pick the check-in and check-out days." };
            }
            DateTime a = din.Value.Add(tin ?? r.InFrom);
            DateTime z = dout.Value.Add(tout ?? r.OutBy);
            return new Ask { In = a, Out = z };
        }

        private static async Task<string> DateError(Dictionary<string, string> s, Rules r, DateTime a, DateTime z, DateTime today, bool checkPast)
        {
            int n = Nights(a, z);
            if (checkPast && a.Date < today)
            {
                return "Check-in can't be in the past.";
            }
            if (n < 1)
            {
                return "Check-out must be at least one day after check-in.";
            }
            if (n > r.MaxNights)
            {
                return "A stay can be at most " + Ui.Plural(r.MaxNights, "night", "nights") + ".";
            }
            if (checkPast && (a.TimeOfDay < r.InFrom || a.TimeOfDay > r.InTo))
            {
                return "Check-in is between " + Clock(r.InFrom) + " and " + Clock(r.InTo) + ".";
            }
            if (z.TimeOfDay < r.InFrom || z.TimeOfDay > r.OutBy)
            {
                return "Check-out is by " + Clock(r.OutBy) + ".";
            }
            string ca = checkPast ? await Closed(s, a.Date) : string.Empty;
            if (ca != string.Empty)
            {
                return "No check-in that day: " + ca + ".";
            }
            string cz = await Closed(s, z.Date);
            if (cz != string.Empty)
            {
                return "No check-out that day: " + cz + ".";
            }
            return string.Empty;
        }

        private async Task OpenForm(ApiResponse response, Ctx c, Dictionary<string, string> s, string petId, string runHint, DateTime? dayHint, BdStay? edit = null)
        {
            Rules r = Rule(s);
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            BdPet? p = edit != null ? await PetById(edit.PetId) : petId != string.Empty ? await PetById(petId) : null;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (edit != null ? "Change stay &middot; " + Ui.Enc(edit.PetName) : "New stay") + "</h2><div id=\"bd-f-pet\">");
            if (edit != null && p == null)
            {
                response.SetElementContents("bd-mbody", "<h2>Change stay</h2><p class=\"bd-lead\">This pet is no longer active, so the stay can't be changed. Cancel it instead.</p><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button></div>");
                response.ExecuteScript(Me + ".shown('" + Ui.Js(edit.ApptId) + "');");
                return;
            }
            if (p == null)
            {
                if (petId != string.Empty)
                {
                    Toast(response, "That pet was not found or is not a dog or cat. Search below.");
                }
                sb.Append(SearchPart());
                sb.Append("</div><div id=\"bd-f-rest\"></div>");
            }
            else
            {
                sb.Append(PetPart(p, edit == null));
                sb.Append("</div><div id=\"bd-f-rest\">" + await RestPart(c, s, r, p, edit, runHint, dayHint, today, now) + "</div>");
            }
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".formShown('" + Ui.Js(p?.PetId ?? string.Empty) + "', '" + Ui.Js(edit?.ApptId ?? string.Empty) + "', '" + Ui.Js(runHint) + "', '" + (dayHint.HasValue ? Ui.Ymd(dayHint.Value) : string.Empty) + "');");
        }

        private static string SearchPart()
        {
            return "<label class=\"bd-field\"><span>Find the pet</span><input type=\"search\" id=\"bd-fterm\" placeholder=\"Pet name, owner or phone\" autocomplete=\"off\" oninput=\"AdminBoardingJs.petType()\"></label><div id=\"bd-f-results\" class=\"bd-picks\"><p class=\"bd-dim\">Dogs and cats only. Type at least 2 letters or numbers.</p></div>";
        }

        private static string PetPart(BdPet p, bool canChange)
        {
            return "<div class=\"bd-fpet\">" + Ui.Avatar("bd", p.PetId, p.PetName, p.Photo) + "<div><b>" + Ui.Enc(p.PetName) + "</b><small>" + Ui.Enc(string.Join(" · ", new[] { SpeciesWord(p.SpeciesKey), p.Breed, SizeWord(p.Size) }.Where(x => x != string.Empty))) + "</small><small>" + Ui.Enc(p.OwnerName + (p.Phone != string.Empty ? " · " + p.Phone : string.Empty)) + "</small></div>" + (canChange ? "<button type=\"button\" class=\"bd-btn bd-btn-sm bd-btn-ghost\" onclick=\"AdminBoardingJs.changePet()\">Change</button>" : string.Empty) + "</div>" + (p.Alert != string.Empty ? "<div class=\"bd-alert\"><b>Pet alert</b><span>" + Ui.Enc(p.Alert) + "</span></div>" : string.Empty);
        }

        private async Task<string> RestPart(Ctx c, Dictionary<string, string> s, Rules r, BdPet p, BdStay? edit, string runHint, DateTime? dayHint, DateTime today, DateTime now)
        {
            DateTime din;
            TimeSpan tin;
            DateTime dout;
            TimeSpan tout;
            List<string> picked;
            string note;
            if (edit != null)
            {
                din = edit.StartAt.Date;
                tin = edit.StartAt.TimeOfDay;
                dout = edit.EndAt.Date;
                tout = edit.EndAt.TimeOfDay;
                picked = Csv(edit.Addons);
                note = edit.Note;
                runHint = edit.RoomId;
            }
            else
            {
                din = dayHint.HasValue && dayHint.Value >= today ? dayHint.Value : today;
                if (!dayHint.HasValue && now.TimeOfDay >= r.InTo)
                {
                    din = today.AddDays(1);
                }
                if (!dayHint.HasValue)
                {
                    for (int i = 0; i < 10 && await Closed(s, din) != string.Empty; i++)
                    {
                        din = din.AddDays(1);
                    }
                }
                List<TimeSpan> ins = Steps(r.InFrom, r.InTo);
                tin = ins.Contains(new TimeSpan(9, 0, 0)) ? new TimeSpan(9, 0, 0) : ins[0];
                if (din == today)
                {
                    TimeSpan next = ins.FirstOrDefault(t => t > now.TimeOfDay);
                    tin = next > TimeSpan.Zero ? next : ins[^1];
                }
                dout = din.AddDays(1);
                for (int i = 0; i < 10 && await Closed(s, dout) != string.Empty; i++)
                {
                    dout = dout.AddDays(1);
                }
                tout = r.OutBy;
                picked = new List<string>();
                note = string.Empty;
            }
            List<BdRun> runs = await Runs();
            List<BdItem> items = await Items();
            List<BdItem> extras = Extras(items, await TypeNights(), p.SpeciesKey);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-fgrid bd-mt\"><div class=\"bd-fpair\"><label class=\"bd-field\"><span>Check-in day</span><input type=\"date\" id=\"bd-fdin\" value=\"" + Ui.Ymd(din) + "\" min=\"" + Ui.Ymd(edit != null && din < today ? din : today) + "\" onchange=\"AdminBoardingJs.dinChanged()\"></label><label class=\"bd-field\"><span>Time</span><select id=\"bd-ftin\" onchange=\"AdminBoardingJs.quote()\">" + TimeOpts(Steps(r.InFrom, r.InTo), tin) + "</select></label></div>");
            sb.Append("<div class=\"bd-fpair\"><label class=\"bd-field\"><span>Check-out day</span><input type=\"date\" id=\"bd-fdout\" value=\"" + Ui.Ymd(dout) + "\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" onchange=\"AdminBoardingJs.quote()\"></label><label class=\"bd-field\"><span>Time</span><select id=\"bd-ftout\" onchange=\"AdminBoardingJs.quote()\">" + TimeOpts(Steps(r.InFrom, r.OutBy), tout) + "</select></label></div></div>");
            sb.Append("<p class=\"bd-dim bd-hint\">Check-in " + Clock(r.InFrom) + " – " + Clock(r.InTo) + ". Check-out by " + Clock(r.OutBy) + ". Up to " + Ui.Plural(r.MaxNights, "night", "nights") + ". No check-in or check-out on days the clinic is closed.</p>");
            sb.Append("<em class=\"bd-err\" id=\"bd-e-dates\"></em>");
            var (runsHtml, chosen) = await RunsPart(s, r, p, runs, items, din.Add(tin), dout.Add(tout), runHint, edit?.ApptId ?? string.Empty, today, edit != null);
            sb.Append("<h3>Run</h3><div id=\"bd-f-runs\">" + runsHtml + "</div>");
            if (extras.Count > 0)
            {
                sb.Append("<h3>Extras</h3><div class=\"bd-adds\">");
                foreach (BdItem x in extras)
                {
                    sb.Append("<label class=\"bd-add1\"><input type=\"checkbox\" class=\"bd-fadd\" id=\"bd-fa-" + Ui.Attr(x.ItemId) + "\" value=\"" + Ui.Attr(x.ItemId) + "\"" + (picked.Contains(x.ItemId) ? " checked" : string.Empty) + " onchange=\"AdminBoardingJs.quote()\"><span><b>" + Ui.Enc(x.ItemName) + "</b><small>" + Ui.Money(PriceFor(x, p.Size)) + " " + Ui.Enc(UnitWord(x)) + "</small></span></label>");
                }
                sb.Append("</div>");
            }
            sb.Append("<h3>Care notes from the owner</h3><label class=\"bd-field\"><span class=\"bd-sr\">Care notes</span><textarea id=\"bd-fnote\" rows=\"3\" maxlength=\"600\" placeholder=\"Food, medicines, habits, who to call\">" + Ui.Enc(note) + "</textarea></label>");
            if (edit == null)
            {
                sb.Append("<div class=\"bd-fgrid bd-mt\"><label class=\"bd-field\"><span>Booked</span><select id=\"bd-fsrc\"><option value=\"phone\">By phone</option><option value=\"desk\">At the desk</option></select></label></div>");
            }
            List<BdItem> adds = extras.Where(x => picked.Contains(x.ItemId)).ToList();
            sb.Append("<div class=\"bd-dgrid bd-mt\"><div id=\"bd-f-vax\">" + await VaxPart(s, p, dout) + "</div><div id=\"bd-f-est\">" + EstPart(runs, items, chosen, adds, Nights(din, dout), p.Size) + "</div></div>");
            sb.Append("<em class=\"bd-err\" id=\"bd-e-save\"></em><div class=\"bd-mfoot\">" + (edit != null ? "<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button>" : "<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.closeModal()\">Close</button>") + "<button type=\"button\" class=\"bd-btn bd-btn-accent\" id=\"bd-fsave\" onclick=\"AdminBoardingJs.save()\">" + (edit != null ? "Save changes" : "Book the stay") + "</button></div>");
            return sb.ToString();
        }

        private static async Task<(string Html, string Chosen)> RunsPart(Dictionary<string, string> s, Rules r, BdPet p, List<BdRun> runs, List<BdItem> items, DateTime a, DateTime z, string want, string ignAppt, DateTime today, bool pastOk)
        {
            List<BdRun> fit = runs.Where(x => Fits(x, p)).ToList();
            if (fit.Count == 0)
            {
                return ("<p class=\"bd-none\">There are no runs for this pet. Check the pet's species and size.</p>", string.Empty);
            }
            if (Nights(a, z) < 1 || (!pastOk && a.Date < today))
            {
                return ("<p class=\"bd-none\">Pick the dates to see which runs are free.</p>", string.Empty);
            }
            HashSet<string> busy = (await BusyRooms(a, z, today, ignAppt, string.Empty, false)).Select(x => x.RoomId).ToHashSet();
            string chosen = fit.Any(x => x.RoomId == want && !busy.Contains(x.RoomId)) ? want : string.Empty;
            if (chosen == string.Empty)
            {
                BdRun? same = fit.FirstOrDefault(x => !busy.Contains(x.RoomId) && x.RoomtypeId == runs.FirstOrDefault(y => y.RoomId == want)?.RoomtypeId);
                chosen = same?.RoomId ?? fit.FirstOrDefault(x => !busy.Contains(x.RoomId))?.RoomId ?? string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            if (want != string.Empty && chosen != want && runs.Any(x => x.RoomId == want))
            {
                BdRun w = runs.First(x => x.RoomId == want);
                sb.Append("<p class=\"bd-note\">" + Ui.Enc(w.RoomName) + (fit.Any(x => x.RoomId == want) ? " is not free for these nights." : " does not suit this pet.") + (chosen != string.Empty ? " Another run is picked." : string.Empty) + "</p>");
            }
            foreach (IGrouping<string, BdRun> g in fit.GroupBy(x => x.RoomtypeId))
            {
                List<BdRun> list = g.ToList();
                BdItem? night = items.FirstOrDefault(i => i.ItemId == list[0].NightItemId);
                int free = list.Count(x => !busy.Contains(x.RoomId));
                sb.Append("<div class=\"bd-rtype\"><div class=\"bd-rth\"><b>" + Ui.Enc(list[0].TypeName) + "</b><small>" + (night != null ? Ui.Money(PriceFor(night, p.Size)) + " per night &middot; " : string.Empty) + (free == 0 ? "none free" : free + " free") + "</small></div><div class=\"bd-rchips\">");
                foreach (BdRun x in list)
                {
                    bool taken = busy.Contains(x.RoomId);
                    sb.Append("<label class=\"bd-rchip" + (taken ? " bd-taken" : string.Empty) + "\"><input type=\"radio\" name=\"bd-frun\" id=\"bd-fr-" + Ui.Attr(x.RoomId) + "\" value=\"" + Ui.Attr(x.RoomId) + "\"" + (x.RoomId == chosen ? " checked" : string.Empty) + (taken ? " disabled" : string.Empty) + " onchange=\"AdminBoardingJs.quote()\"><span>" + Ui.Enc(x.RoomName) + (taken ? "<small>taken</small>" : string.Empty) + "</span></label>");
                }
                sb.Append("</div></div>");
            }
            if (chosen == string.Empty)
            {
                sb.Append("<p class=\"bd-none\">No run that suits this pet is free for every night. Try other dates.</p>");
            }
            return (sb.ToString(), chosen);
        }

        private static string EstPart(List<BdRun> runs, List<BdItem> items, string runId, List<BdItem> adds, int nights, string size)
        {
            BdRun? run = runs.FirstOrDefault(x => x.RoomId == runId);
            if (run == null || nights < 1)
            {
                return "<div class=\"bd-est\"><b class=\"bd-esth\">Estimate</b><p class=\"bd-dim\">Pick dates and a run to see the estimate.</p></div>";
            }
            BdItem? night = items.FirstOrDefault(i => i.ItemId == run.NightItemId);
            return Lines(Charge(night, adds, nights, size), "Estimate · " + Ui.Plural(nights, "night", "nights"), string.Empty);
        }

        private static async Task<string> VaxPart(Dictionary<string, string> s, BdPet p, DateTime outDay)
        {
            List<VaxLine> vl = VaxCheck(s, p.SpeciesKey, p.PetId, await VaxRows(new List<string> { p.PetId }), outDay);
            if (vl.Count == 0)
            {
                return "<div class=\"bd-vbox\"><b class=\"bd-esth\">Vaccines</b><p class=\"bd-dim\">None required.</p></div>";
            }
            List<VaxLine> miss = vl.Where(x => !x.Ok).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-vbox" + (miss.Count > 0 ? " bd-vwarn" : string.Empty) + "\"><b class=\"bd-esth\">Vaccines</b><ul class=\"bd-vax\">");
            foreach (VaxLine v in vl)
            {
                sb.Append("<li class=\"" + (v.Ok ? "bd-vok" : "bd-vno") + "\"><b>" + Ui.Enc(v.Name) + "</b><span>" + (v.Due == null ? "Not on file" : (v.Ok ? "Good until " : "Due ") + Ui.Enc(Ui.Day(v.Due.Value))) + "</span></li>");
            }
            sb.Append("</ul>");
            if (miss.Count > 0)
            {
                sb.Append("<p class=\"bd-vmsg\">You can still book. The stay is marked <b>vaccines pending</b>: ask the owner to bring proof or book the shots before check-in.</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> NewStay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'A'))
            {
                Toast(response, "You don't have the right to book stays.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            await OpenForm(response, c, s, Id(V("petid", 50)), Id(V("runid", 50)), Ui.ParseDay(V("inday", 10)));
            return response;
        }

        public async Task<ApiResponse> PetFind()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !c.Can(Key, 'A'))
            {
                return response;
            }
            string term = Ui.OneLine(V("petterm", 60), 60);
            if (term.Length < 2)
            {
                response.SetElementContents("bd-f-results", "<p class=\"bd-dim\">Dogs and cats only. Type at least 2 letters or numbers.</p>");
                return response;
            }
            string digits = Ui.Digits(term);
            var (list, emsg) = await Helper.GetListT<BdPet>(PetCols.Replace("SELECT ", "SELECT TOP 20 ") + " AND (P.PET_NAME LIKE @ql OR C.FIRST_NAME LIKE @ql OR C.LAST_NAME LIKE @ql OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @ql OR (@qd <> N'' AND " + PhoneDigits + " LIKE @qdl)) ORDER BY P.PET_NAME, C.LAST_NAME", new List<SqlParameter> { App.P("ql", Like(term), 200), App.P("qd", digits.Length >= 3 ? digits : string.Empty, 30), App.P("qdl", Like(digits), 60) });
            if (emsg != string.Empty || list == null)
            {
                response.SetElementContents("bd-f-results", "<p class=\"bd-dim\">The search did not work. Try again.</p>");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<p class=\"bd-dim\">No dog or cat matches &ldquo;" + Ui.Enc(term) + "&rdquo;.</p>");
            }
            foreach (BdPet p in list)
            {
                sb.Append("<button type=\"button\" class=\"bd-pick\" onclick=\"AdminBoardingJs.pickPet('" + Ui.Js(p.PetId) + "')\">" + Sp(p.SpeciesKey) + "<span><b>" + Ui.Enc(p.PetName) + "</b><small>" + Ui.Enc(string.Join(" · ", new[] { p.Breed, SizeWord(p.Size) }.Where(x => x != string.Empty))) + "</small><small>" + Ui.Enc(p.OwnerName + (p.Phone != string.Empty ? " · " + p.Phone : string.Empty)) + "</small>" + (p.Alert != string.Empty ? "<em class=\"bd-tag bd-tag-alert\">" + Ui.Enc(p.Alert) + "</em>" : string.Empty) + "</span></button>");
            }
            response.SetElementContents("bd-f-results", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Quote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !c.Can(Key, 'V'))
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Rules r = Rule(s);
            DateTime today = App.Now(s).Date;
            string apptId = Id(V("apptid", 50));
            BdStay? edit = apptId != string.Empty ? await StayById(apptId) : null;
            if ((edit == null && !c.Can(Key, 'A')) || (edit != null && !c.Can(Key, 'E')))
            {
                return response;
            }
            BdPet? p = await PetById(edit != null ? edit.PetId : Id(V("petid", 50)));
            if (p == null)
            {
                return response;
            }
            Ask ask = ReadDates(r);
            if (ask.Error != string.Empty)
            {
                response.SetElementContents("bd-e-dates", Ui.Enc(ask.Error));
                return response;
            }
            bool moved = edit == null || edit.StartAt != ask.In;
            string err = await DateError(s, r, ask.In, ask.Out, today, moved);
            response.SetElementContents("bd-e-dates", Ui.Enc(err));
            List<BdRun> runs = await Runs();
            List<BdItem> items = await Items();
            var (html, chosen) = await RunsPart(s, r, p, runs, items, ask.In, ask.Out, Id(V("frun", 50)), apptId, today, !moved);
            response.SetElementContents("bd-f-runs", html);
            List<string> picked = Csv(V("fadds", 600));
            List<BdItem> adds = Extras(items, await TypeNights(), p.SpeciesKey).Where(x => picked.Contains(x.ItemId)).ToList();
            response.SetElementContents("bd-f-est", EstPart(runs, items, chosen, adds, Nights(ask.In, ask.Out), p.Size));
            response.SetElementContents("bd-f-vax", await VaxPart(s, p, ask.Out));
            return response;
        }

        public async Task<ApiResponse> PetPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A'))
            {
                Toast(response, "You don't have the right to book stays.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            await OpenForm(response, c, s, Id(V("petid", 50)), Id(V("runid", 50)), Ui.ParseDay(V("inday", 10)));
            return response;
        }

        public async Task<ApiResponse> EditStay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "change stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            if (st.State != "booked" && st.State != "confirmed")
            {
                await Refresh(response, c, s, st.ApptId, "Only booked or confirmed stays can be changed.");
                return response;
            }
            await OpenForm(response, c, s, st.PetId, st.RoomId, null, st);
            return response;
        }

        public async Task<ApiResponse> SaveStay()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string apptId = Id(V("apptid", 50));
            bool isNew = apptId == string.Empty;
            if (!c.Can(Key, 'V') || !c.Can(Key, isNew ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, isNew ? "book stay" : "change stay", "boarding · no right", false);
                Toast(response, isNew ? "You don't have the right to book stays." : "You don't have the right to change stays.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Rules r = Rule(s);
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            BdStay? old = null;
            string newId = string.Empty;
            if (!isNew)
            {
                old = await StayById(apptId);
                if (old == null || (old.State != "booked" && old.State != "confirmed"))
                {
                    await Refresh(response, c, s, old?.ApptId ?? string.Empty, "This stay can't be changed any more.");
                    return response;
                }
            }
            else
            {
                newId = NewKey();
                if (newId == string.Empty)
                {
                    response.SetElementContents("bd-e-save", "This form is out of date. Close it and try again.");
                    return response;
                }
                BdStay? dup = await StayById(newId);
                if (dup != null)
                {
                    await Refresh(response, c, s, dup.ApptId, "Booked.");
                    return response;
                }
            }
            BdPet? p = await PetById(old != null ? old.PetId : Id(V("petid", 50)));
            if (p == null)
            {
                response.SetElementContents("bd-e-save", "Pick a dog or cat first.");
                return response;
            }
            Ask ask = ReadDates(r);
            if (ask.Error != string.Empty)
            {
                response.SetElementContents("bd-e-save", Ui.Enc(ask.Error));
                return response;
            }
            bool moved = old == null || old.StartAt != ask.In || old.EndAt != ask.Out;
            string err = await DateError(s, r, ask.In, ask.Out, today, old == null || old.StartAt != ask.In);
            if (err != string.Empty)
            {
                response.SetElementContents("bd-e-save", Ui.Enc(err));
                response.SetElementContents("bd-e-dates", Ui.Enc(err));
                return response;
            }
            List<BdRun> runs = await Runs();
            List<BdItem> items = await Items();
            BdRun? run = runs.FirstOrDefault(x => x.RoomId == Id(V("frun", 50)));
            if (run == null || !Fits(run, p))
            {
                response.SetElementContents("bd-e-save", "Pick a free run.");
                return response;
            }
            BdItem? night = items.FirstOrDefault(i => i.ItemId == run.NightItemId);
            if (night == null)
            {
                response.SetElementContents("bd-e-save", "This kind of run has no nightly price set. Ask a manager to set it up in the catalog.");
                return response;
            }
            List<BdBusy> busy = await BusyRooms(ask.In, ask.Out, today, apptId, string.Empty, false);
            if (busy.Any(x => x.RoomId == run.RoomId))
            {
                response.SetElementContents("bd-e-save", Ui.Enc(run.RoomName + " is not free for every night. Pick another run."));
                response.ExecuteScript(Me + ".quote();");
                return response;
            }
            List<string> picked = Csv(V("fadds", 600));
            List<BdItem> extras = Extras(items, await TypeNights(), p.SpeciesKey);
            List<BdItem> adds = extras.Where(x => picked.Contains(x.ItemId)).ToList();
            if (old != null)
            {
                foreach (string keep in Csv(old.Addons).Where(id => picked.Contains(id) && !adds.Any(a => a.ItemId == id)))
                {
                    BdItem? it = items.FirstOrDefault(i => i.ItemId == keep);
                    if (it != null)
                    {
                        adds.Add(it);
                    }
                }
            }
            int nights = Nights(ask.In, ask.Out);
            decimal price = Charge(night, adds, nights, p.Size).Sum(x => x.Total);
            List<VaxLine> vl = VaxCheck(s, p.SpeciesKey, p.PetId, await VaxRows(new List<string> { p.PetId }), ask.Out);
            bool vx = vl.Any(x => !x.Ok);
            string note = Text("fnote", 600);
            string addCsv = string.Join(",", adds.Select(a => a.ItemId));
            string aid = isNew ? newId : apptId;
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", aid, 50), App.P("pid", p.PetId, 50), App.P("cid", p.ClientId, 50), App.P("rt", run.RoomtypeId, 50), App.P("room", run.RoomId, 50),
                App.PDate("st", ask.In), App.PDate("en", ask.Out), App.PDate("din", ask.In.Date), App.PDate("dout", ask.Out.Date), App.PDate("today", today),
                App.P("ign", aid, 50), App.P("ivis", string.Empty, 50), App.PBit("inst", false), App.P("adds", addCsv, 400), App.PDec("price", price), App.PBit("vx", vx),
                App.P("note", note, 600), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50)
            };
            List<string> sql = new List<string> { PetLock, "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room", RoomGuard, PetGuard };
            string code = string.Empty;
            string src = V("fsrc", 10) == "desk" ? "desk" : "phone";
            if (isNew)
            {
                code = await NewCode();
                if (code == string.Empty)
                {
                    response.SetElementContents("bd-e-save", "A stay code could not be made. Try again.");
                    return response;
                }
                ps.Add(App.P("code", code, 20));
                ps.Add(App.P("src", src, 10));
                ps.Add(App.P("ltext", src == "desk" ? "Booked at the desk" : "Booked by phone", 300));
                sql.Add("INSERT INTO APPOINTMENT (APPT_ID, APPT_CODE, CLIENT_ID, PET_ID, BOOKTYPE_ID, VISIT_TYPE, STAFF_ID, ROOMTYPE_ID, ROOM_ID, START_AT, END_AT, MINUTES, PROC_MINUTES, ADDONS, EST_PRICE, SOURCE, STATUS, VAX_PENDING, NOTE, CREATED_UTC, CREATED_BY) VALUES (@aid, @code, @cid, @pid, N'', N'boarding', N'', @rt, @room, @st, @en, 0, 0, @adds, @price, @src, N'confirmed', @vx, @note, SYSUTCDATETIME(), @me)");
            }
            else
            {
                List<string> what = new List<string>();
                if (moved)
                {
                    what.Add("dates now " + When(ask.In) + " to " + When(ask.Out));
                }
                if (old!.RoomId != run.RoomId)
                {
                    what.Add("run " + (old.RoomName != string.Empty ? old.RoomName : "?") + " → " + run.RoomName);
                }
                if (string.Join(",", Csv(old.Addons).OrderBy(x => x)) != string.Join(",", adds.Select(a => a.ItemId).OrderBy(x => x)))
                {
                    what.Add("extras " + (adds.Count == 0 ? "none" : string.Join(", ", adds.Select(a => a.ItemName))));
                }
                if (old.Note != note)
                {
                    what.Add("care notes");
                }
                ps.Add(App.P("ltext", Ui.Clip("Changed: " + (what.Count == 0 ? "nothing" : string.Join("; ", what)), 300), 300));
                sql.Add("UPDATE APPOINTMENT SET ROOMTYPE_ID = @rt, ROOM_ID = @room, START_AT = @st, END_AT = @en, ADDONS = @adds, EST_PRICE = @price, VAX_PENDING = @vx, NOTE = @note, SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; " + Changed);
            }
            sql.Add(LogSql);
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, isNew ? "book stay" : "change stay", p.PetName + " · failed", false);
                response.SetElementContents("bd-e-save", Ui.Enc(Why(result)));
                if (result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    response.ExecuteScript(Me + ".quote();");
                }
                return response;
            }
            await App.Log(c.U.StaffId, isNew ? "book stay" : "change stay", p.PetName + " · " + run.RoomName + " · " + (isNew ? code : old!.ApptCode), true);
            if (!isNew)
            {
                await Refresh(response, c, s, aid, "Stay changed.");
                return response;
            }
            await Paint(response, c, s);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-donebox\"><span class=\"bd-st bd-st-confirmed\">Confirmed</span><h2>Stay booked for " + Ui.Enc(p.PetName) + "</h2><p class=\"bd-donecode\">" + Ui.Enc(code) + "</p><p>" + Ui.Enc(run.TypeName + " · " + run.RoomName) + "</p><p>" + Ui.Enc(When(ask.In) + " to " + When(ask.Out)) + " &middot; " + Ui.Plural(nights, "night", "nights") + "</p><p class=\"bd-dim\">Estimate " + Ui.Money(price) + ". Give the owner the stay code.</p>");
            if (vx)
            {
                sb.Append("<p class=\"bd-vmsg\">Vaccines pending: " + Ui.Enc(string.Join(", ", vl.Where(x => !x.Ok).Select(x => x.Name))) + ". Ask the owner to bring proof by check-in.</p>");
            }
            sb.Append("<div class=\"bd-acts\"><button type=\"button\" class=\"bd-btn bd-btn-accent\" onclick=\"AdminBoardingJs.open('" + Ui.Js(aid) + "')\">Open the stay</button><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.newStay('', '', '')\">Book another</button><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(aid) + "');");
            response.ExecuteScript(Me + ".after();");
            return response;
        }

        private async Task<string> Simple(Ctx c, BdStay st, string sql, string logText, string action, List<SqlParameter> extra)
        {
            List<SqlParameter> ps = new List<SqlParameter> { App.P("aid", st.ApptId, 50), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(logText, 300), 300) };
            ps.AddRange(extra);
            string result = await Helper.PutData(new List<string> { sql, LogSql }, ps);
            await App.Log(c.U.StaffId, action, st.PetName + " · " + st.ApptCode, !Helper.IsError(result));
            return result;
        }

        public async Task<ApiResponse> Confirm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "confirm stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string r = await Simple(c, st, "UPDATE APPOINTMENT SET STATUS = N'confirmed', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS = N'booked'; " + Changed, "Confirmed with the owner", "confirm stay", new List<SqlParameter>());
            await Refresh(response, c, s, st.ApptId, Helper.IsError(r) ? Why(r) : "Stay confirmed.");
            return response;
        }

        public async Task<ApiResponse> NoShow()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "no-show stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            if (App.Now(s).Date <= st.StartAt.Date)
            {
                await Refresh(response, c, s, st.ApptId, "A stay can be marked as a no-show only after its check-in day.");
                return response;
            }
            string r = await Simple(c, st, "UPDATE APPOINTMENT SET STATUS = N'noshow', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; " + Changed, "Marked as no-show", "no-show stay", new List<SqlParameter>());
            await Refresh(response, c, s, st.ApptId, Helper.IsError(r) ? Why(r) : "Marked as a no-show. The run is free again.");
            return response;
        }

        public async Task<ApiResponse> CancelForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'X', "cancel stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            if (st.State != "booked" && st.State != "confirmed")
            {
                await Refresh(response, c, s, st.ApptId, "Only booked or confirmed stays can be cancelled.");
                return response;
            }
            Rules r = Rule(s);
            double hours = (st.StartAt - App.Now(s)).TotalHours;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Cancel " + Ui.Enc(st.PetName) + "'s stay?</h2><p class=\"bd-lead\">" + Ui.Enc(When(st.StartAt) + " to " + When(st.EndAt) + " · " + st.RoomName) + "</p>");
            if (hours < r.CancelHours)
            {
                sb.Append("<div class=\"bd-warn\">This is a late cancellation: check-in is less than " + r.CancelHours + " hours away. It will be noted in the history.</div>");
            }
            sb.Append("<label class=\"bd-field bd-mt\"><span>Reason</span><input type=\"text\" id=\"bd-cwhy\" maxlength=\"200\" placeholder=\"For example: owner's plans changed\"></label><em class=\"bd-err\" id=\"bd-e-cancel\"></em><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Keep the stay</button><button type=\"button\" class=\"bd-btn bd-btn-danger\" id=\"bd-cgo\" onclick=\"AdminBoardingJs.cancelGo()\">Cancel the stay</button></div>");
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "', 'bd-cwhy');");
            return response;
        }

        public async Task<ApiResponse> CancelGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'X', "cancel stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string why = Ui.OneLine(V("cwhy", 200), 200);
            if (why.Length < 2)
            {
                response.SetElementContents("bd-e-cancel", "Say why the stay is cancelled.");
                return response;
            }
            Rules rules = Rule(s);
            bool late = (st.StartAt - App.Now(s)).TotalHours < rules.CancelHours;
            string text = "Cancelled" + (late ? " late (less than " + rules.CancelHours + " hours before check-in)" : string.Empty) + ": " + why;
            string r = await Simple(c, st, "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = @why, SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; " + Changed, text, "cancel stay", new List<SqlParameter> { App.P("why", Ui.Clip((late ? "Late: " : string.Empty) + why, 200), 200) });
            await Refresh(response, c, s, st.ApptId, Helper.IsError(r) ? Why(r) : "Stay cancelled. The run is free again.");
            return response;
        }

        public async Task<ApiResponse> CheckInForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "check in stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string block = await CheckInBlock(s, st);
            if (block != string.Empty)
            {
                await Refresh(response, c, s, st.ApptId, block);
                return response;
            }
            List<VaxLine> vl = VaxCheck(s, st.SpeciesKey, st.PetId, await VaxRows(new List<string> { st.PetId }), st.EndAt);
            List<BdBusy> inRun = await RunOccupant(st.RoomId, string.Empty);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Check in " + Ui.Enc(st.PetName) + "</h2><p class=\"bd-lead\">" + Ui.Enc(st.TypeName + " · " + st.RoomName) + " &middot; going home " + Ui.Enc(When(st.EndAt)) + "</p>");
            if (st.PetAlert != string.Empty)
            {
                sb.Append("<div class=\"bd-alert\"><b>Pet alert</b><span>" + Ui.Enc(st.PetAlert) + "</span></div>");
            }
            if (inRun.Count > 0)
            {
                sb.Append("<div class=\"bd-warn\"><b>" + Ui.Enc(st.RoomName) + " still has " + Ui.Enc(inRun[0].PetName) + " in it.</b> Check that pet out or move it first, or change this stay to another run.</div><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button><button type=\"button\" class=\"bd-btn\" onclick=\"AdminBoardingJs.edit()\">Change the run</button></div>");
                response.SetElementContents("bd-mbody", sb.ToString());
                response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
                return response;
            }
            List<VaxLine> miss = vl.Where(x => !x.Ok).ToList();
            sb.Append("<div class=\"bd-vbox" + (miss.Count > 0 ? " bd-vwarn" : string.Empty) + " bd-mt\"><b class=\"bd-esth\">Vaccines</b><ul class=\"bd-vax\">");
            foreach (VaxLine v in vl)
            {
                sb.Append("<li class=\"" + (v.Ok ? "bd-vok" : "bd-vno") + "\"><b>" + Ui.Enc(v.Name) + "</b><span>" + (v.Due == null ? "Not on file" : (v.Ok ? "Good until " : "Due ") + Ui.Enc(Ui.Day(v.Due.Value))) + "</span></li>");
            }
            if (vl.Count == 0)
            {
                sb.Append("<li class=\"bd-vok\"><b>None required</b><span></span></li>");
            }
            sb.Append("</ul></div>");
            if (miss.Count > 0)
            {
                sb.Append("<label class=\"bd-field bd-mt\"><span>Vaccines are missing. Why is it OK to check in anyway?</span><textarea id=\"bd-ovr\" rows=\"2\" maxlength=\"200\" placeholder=\"For example: owner showed proof from another clinic, copy to follow\"></textarea></label>");
            }
            if (st.Note != string.Empty)
            {
                sb.Append("<div class=\"bd-note\"><b>Care notes from the owner</b>" + Ui.Enc(st.Note) + "</div>");
            }
            sb.Append("<em class=\"bd-err\" id=\"bd-e-cin\"></em><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button><button type=\"button\" class=\"bd-btn bd-btn-accent\" id=\"bd-cingo\" onclick=\"AdminBoardingJs.checkInGo()\">" + (miss.Count > 0 ? "Check in anyway" : "Check in") + "</button></div>");
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "', '" + (miss.Count > 0 ? "bd-ovr" : string.Empty) + "');");
            return response;
        }

        private static async Task<string> CheckInBlock(Dictionary<string, string> s, BdStay st)
        {
            DateTime today = App.Now(s).Date;
            if (st.State != "booked" && st.State != "confirmed")
            {
                return st.State == "inhouse" ? "This pet is already checked in." : "Only booked or confirmed stays can be checked in.";
            }
            if (today < st.StartAt.Date)
            {
                return "Check-in opens on " + Ui.Day(st.StartAt, false) + ".";
            }
            if (today >= st.EndAt.Date)
            {
                return "The check-out day has passed. Change the dates first.";
            }
            var (dt, emsg) = await Helper.GetDataTable("SELECT COUNT(*) FROM VISIT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom'", new List<SqlParameter> { App.P("pid", st.PetId, 50) });
            if (emsg == string.Empty && Convert.ToInt32(dt.Rows[0][0]) > 0)
            {
                return "This pet is already staying with us on another booking.";
            }
            return string.Empty;
        }

        private static async Task<List<BdBusy>> RunOccupant(string roomId, string ignVisit)
        {
            var (list, emsg) = await Helper.GetListT<BdBusy>("SELECT X.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.ROOM_ID = @room AND X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis", new List<SqlParameter> { App.P("room", roomId, 50), App.P("ivis", ignVisit, 50) });
            return emsg == string.Empty && list != null ? list : new List<BdBusy>();
        }

        public async Task<ApiResponse> CheckInGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "check in stay");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string vid = NewKey();
            if (vid == string.Empty)
            {
                response.SetElementContents("bd-e-cin", "This form is out of date. Close it and try again.");
                return response;
            }
            if (st.VisitId == vid)
            {
                await Refresh(response, c, s, st.ApptId, st.PetName + " is checked in.");
                return response;
            }
            string block = await CheckInBlock(s, st);
            if (block != string.Empty)
            {
                await Refresh(response, c, s, st.ApptId, block);
                return response;
            }
            List<VaxLine> vl = VaxCheck(s, st.SpeciesKey, st.PetId, await VaxRows(new List<string> { st.PetId }), st.EndAt);
            List<string> miss = vl.Where(x => !x.Ok).Select(x => x.Name).ToList();
            string ovr = Ui.OneLine(V("ovr", 200), 200);
            if (miss.Count > 0 && ovr.Length < 3)
            {
                response.SetElementContents("bd-e-cin", "Vaccines are missing. Write why it is OK to check in anyway.");
                return response;
            }
            List<BdBusy> inRun = await RunOccupant(st.RoomId, string.Empty);
            if (inRun.Count > 0)
            {
                response.SetElementContents("bd-e-cin", Ui.Enc(st.RoomName + " still has " + inRun[0].PetName + " in it. Change the run first."));
                return response;
            }
            string vno = await Flow.NextNo("VIS", "V");
            if (vno == string.Empty)
            {
                response.SetElementContents("bd-e-cin", "A visit number could not be made. Try again.");
                return response;
            }
            DateTime now = App.Now(s);
            string overrideLine = miss.Count > 0 ? Ui.Clip("Vaccine override at check-in (" + string.Join(", ", miss) + " missing): " + ovr + " — " + c.U.StaffName + ", " + Ui.Day(now, false), 300) : string.Empty;
            string ltext = "Checked in to " + st.RoomName + (miss.Count > 0 ? ". " + overrideLine : string.Empty);
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", st.ApptId, 50), App.P("vid", vid, 50), App.P("vno", vno, 20), App.P("cid", st.ClientId, 50), App.P("pid", st.PetId, 50), App.P("room", st.RoomId, 50),
                App.PDate("now", now), App.PDate("plan", st.EndAt), App.P("me", c.U.StaffId, 50), App.P("vr", App.NewId(), 50), App.PBit("vx", miss.Count > 0), App.P("ovr", overrideLine, 300),
                App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(ltext, 300), 300)
            };
            string result = await Helper.PutData(new List<string>
            {
                PetLock,
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                "IF EXISTS (SELECT 1 FROM VISITROOM WHERE ROOM_ID = @room AND OUT_AT IS NULL) THROW 50001, N'ROOMBUSY', 1;",
                "IF EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom') THROW 50005, N'PETBUSY', 1;",
                "UPDATE APPOINTMENT SET STATUS = N'checkedin', VISIT_ID = @vid, VAX_PENDING = @vx, DESK_NOTE = LEFT(CASE WHEN @ovr = N'' THEN DESK_NOTE WHEN DESK_NOTE = N'' THEN @ovr ELSE DESK_NOTE + NCHAR(10) + @ovr END, 600), SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; " + Changed,
                "INSERT INTO VISIT (VISIT_ID, VISIT_NO, APPT_ID, CLIENT_ID, PET_ID, VISIT_TYPE, STATUS, STAFF_ID, REASON, CHECKIN_AT, START_AT, PLAN_OUT, CREATED_BY) VALUES (@vid, @vno, @aid, @cid, @pid, N'boarding', N'inroom', @me, N'Boarding stay', @now, @now, @plan, @me)",
                "INSERT INTO VISITROOM (VR_ID, VISIT_ID, ROOM_ID, STAFF_ID, PURPOSE, IN_AT) VALUES (@vr, @vid, @room, @me, N'boarding', @now)",
                LogSql
            }, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "check in stay", st.PetName + " · failed", false);
                if (result.Contains("STATECHANGED", StringComparison.OrdinalIgnoreCase))
                {
                    await Refresh(response, c, s, st.ApptId, Why(result));
                    return response;
                }
                response.SetElementContents("bd-e-cin", Ui.Enc(result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase) ? st.RoomName + " is in use. Change the run first." : Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, "check in stay", st.PetName + " · " + st.RoomName + (miss.Count > 0 ? " · vaccine override" : string.Empty), true);
            await Refresh(response, c, s, st.ApptId, st.PetName + " is checked in to " + st.RoomName + ".");
            return response;
        }

        private async Task<BdStay?> InHouse(ApiResponse response, Ctx c, Dictionary<string, string> s, string action)
        {
            BdStay? st = await Reach(response, c, 'E', action);
            if (st == null)
            {
                return null;
            }
            if (st.State != "inhouse" || st.CurRoomId == string.Empty)
            {
                await Refresh(response, c, s, st.ApptId, "This pet is not in house any more.");
                return null;
            }
            return st;
        }

        public async Task<ApiResponse> MoveForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "move run");
            if (st == null)
            {
                return response;
            }
            DateTime today = App.Now(s).Date;
            DateTime end = PlanEnd(st).Date > today ? PlanEnd(st).Date : today.AddDays(2);
            BdPet p = new BdPet { PetId = st.PetId, SpeciesKey = st.SpeciesKey, Size = st.Size };
            List<BdRun> fit = (await Runs()).Where(x => Fits(x, p) && x.RoomId != st.CurRoomId).ToList();
            HashSet<string> busy = (await BusyRooms(today, end, today, st.ApptId, st.VisitId, true)).Select(x => x.RoomId).ToHashSet();
            List<BdRun> free = fit.Where(x => !busy.Contains(x.RoomId)).ToList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Move " + Ui.Enc(st.PetName) + "</h2><p class=\"bd-lead\">Now in <b>" + Ui.Enc(st.CurRoomName) + "</b>. Runs free from today until " + Ui.Enc(Ui.Day(end, false)) + ":</p>");
            if (free.Count == 0)
            {
                sb.Append("<p class=\"bd-none\">No other run that suits this pet is free for the rest of the stay.</p><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button></div>");
            }
            else
            {
                foreach (IGrouping<string, BdRun> g in free.GroupBy(x => x.RoomtypeId))
                {
                    sb.Append("<div class=\"bd-rtype\"><div class=\"bd-rth\"><b>" + Ui.Enc(g.First().TypeName) + "</b></div><div class=\"bd-rchips\">");
                    foreach (BdRun x in g)
                    {
                        sb.Append("<label class=\"bd-rchip\"><input type=\"radio\" name=\"bd-mrun\" id=\"bd-mr-" + Ui.Attr(x.RoomId) + "\" value=\"" + Ui.Attr(x.RoomId) + "\"" + (x.RoomId == free[0].RoomId ? " checked" : string.Empty) + "><span>" + Ui.Enc(x.RoomName) + "</span></label>");
                    }
                    sb.Append("</div></div>");
                }
                sb.Append("<em class=\"bd-err\" id=\"bd-e-move\"></em><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button><button type=\"button\" class=\"bd-btn bd-btn-accent\" id=\"bd-mgo\" onclick=\"AdminBoardingJs.moveGo()\">Move</button></div>");
            }
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
            return response;
        }

        public async Task<ApiResponse> MoveGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "move run");
            if (st == null)
            {
                return response;
            }
            string target = Id(V("runid", 50));
            if (target == st.CurRoomId)
            {
                await Refresh(response, c, s, st.ApptId, st.PetName + " is in " + st.CurRoomName + ".");
                return response;
            }
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            DateTime end = PlanEnd(st).Date > today ? PlanEnd(st).Date : today.AddDays(2);
            BdPet p = new BdPet { PetId = st.PetId, SpeciesKey = st.SpeciesKey, Size = st.Size };
            BdRun? run = (await Runs()).FirstOrDefault(x => x.RoomId == target && Fits(x, p));
            if (run == null)
            {
                response.SetElementContents("bd-e-move", "Pick a run.");
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", st.ApptId, 50), App.P("vid", st.VisitId, 50), App.P("room", run.RoomId, 50), App.P("rt", run.RoomtypeId, 50), App.PDate("din", today), App.PDate("dout", end), App.PDate("today", today),
                App.P("ign", st.ApptId, 50), App.P("ivis", st.VisitId, 50), App.PBit("inst", true), App.PDate("now", now), App.P("vr", App.NewId(), 50), App.P("me", c.U.StaffId, 50),
                App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip("Moved from " + st.CurRoomName + " to " + run.RoomName, 300), 300)
            };
            string result = await Helper.PutData(new List<string>
            {
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                RoomGuard,
                "UPDATE VISIT SET SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'inroom'; " + Changed,
                "UPDATE VISITROOM SET OUT_AT = @now WHERE VISIT_ID = @vid AND OUT_AT IS NULL",
                "INSERT INTO VISITROOM (VR_ID, VISIT_ID, ROOM_ID, STAFF_ID, PURPOSE, IN_AT) VALUES (@vr, @vid, @room, @me, N'boarding', @now)",
                "UPDATE APPOINTMENT SET ROOM_ID = @room, ROOMTYPE_ID = @rt, SYSDTE = GETDATE() WHERE APPT_ID = @aid",
                LogSql
            }, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "move run", st.PetName + " · failed", false);
                if (result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    response.SetElementContents("bd-e-move", Ui.Enc(run.RoomName + " was just taken. Pick another run."));
                    return response;
                }
                await Refresh(response, c, s, st.ApptId, Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "move run", st.PetName + " · " + st.CurRoomName + " → " + run.RoomName, true);
            await Refresh(response, c, s, st.ApptId, st.PetName + " moved to " + run.RoomName + ".");
            return response;
        }

        public async Task<ApiResponse> ExtendForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "change check-out");
            if (st == null)
            {
                return response;
            }
            Rules r = Rule(s);
            DateTime today = App.Now(s).Date;
            DateTime min = InDay(st).AddDays(1) > today ? InDay(st).AddDays(1) : today;
            DateTime max = InDay(st).AddDays(r.MaxNights);
            DateTime cur = PlanEnd(st);
            TimeSpan t = cur.TimeOfDay >= r.InFrom && cur.TimeOfDay <= r.OutBy ? cur.TimeOfDay : r.OutBy;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Change check-out for " + Ui.Enc(st.PetName) + "</h2><p class=\"bd-lead\">Checked in " + Ui.Enc(When(st.CheckinAt ?? st.StartAt)) + ". Going home now: <b>" + Ui.Enc(When(cur)) + "</b>.</p>");
            sb.Append("<div class=\"bd-fpair bd-mt\"><label class=\"bd-field\"><span>New check-out day</span><input type=\"date\" id=\"bd-xdout\" value=\"" + Ui.Ymd(cur.Date < min ? min : cur.Date) + "\" min=\"" + Ui.Ymd(min) + "\" max=\"" + Ui.Ymd(max) + "\"></label><label class=\"bd-field\"><span>Time</span><select id=\"bd-xtout\">" + TimeOpts(Steps(r.InFrom, r.OutBy), t) + "</select></label></div>");
            sb.Append("<p class=\"bd-dim bd-hint\">The pet stays in " + Ui.Enc(st.CurRoomName) + ". If another booking needs that run, move the pet first.</p><em class=\"bd-err\" id=\"bd-e-ext\"></em><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button><button type=\"button\" class=\"bd-btn bd-btn-accent\" id=\"bd-xgo\" onclick=\"AdminBoardingJs.extendGo()\">Save new check-out</button></div>");
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "', 'bd-xdout');");
            return response;
        }

        public async Task<ApiResponse> ExtendGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "change check-out");
            if (st == null)
            {
                return response;
            }
            Rules r = Rule(s);
            DateTime today = App.Now(s).Date;
            DateTime? d = Ui.ParseDay(V("xdout", 10));
            TimeSpan? t = Ui.ParseHm(V("xtout", 5));
            if (d == null || t == null)
            {
                response.SetElementContents("bd-e-ext", "Pick the new check-out day.");
                return response;
            }
            DateTime outAt = d.Value.Add(t.Value);
            DateTime inDay = InDay(st);
            string err = string.Empty;
            if (d.Value < today)
            {
                err = "The new check-out can't be in the past.";
            }
            else if (Nights(inDay, d.Value) < 1)
            {
                err = "Check-out must be at least one day after check-in.";
            }
            else if (Nights(inDay, d.Value) > r.MaxNights)
            {
                err = "A stay can be at most " + Ui.Plural(r.MaxNights, "night", "nights") + ".";
            }
            else if (t.Value < r.InFrom || t.Value > r.OutBy)
            {
                err = "Check-out is by " + Clock(r.OutBy) + ".";
            }
            else
            {
                string closed = await Closed(s, d.Value);
                if (closed != string.Empty)
                {
                    err = "No check-out that day: " + closed + ".";
                }
            }
            if (err != string.Empty)
            {
                response.SetElementContents("bd-e-ext", Ui.Enc(err));
                return response;
            }
            if (outAt == PlanEnd(st))
            {
                await Refresh(response, c, s, st.ApptId, "Check-out stays the same.");
                return response;
            }
            List<BdBusy> busy = (await BusyRooms(today, d.Value, today, st.ApptId, st.VisitId, false)).Where(x => x.RoomId == st.CurRoomId).ToList();
            if (busy.Count > 0)
            {
                response.SetElementContents("bd-e-ext", Ui.Enc(st.CurRoomName + " is booked for " + busy[0].PetName + " in those nights. Move " + st.PetName + " to another run first, or pick an earlier day."));
                return response;
            }
            List<BdItem> items = await Items();
            BdItem? night = NightItem(await TypeNights(), items, st.RoomtypeId);
            List<BdItem> adds = Csv(st.Addons).Select(id => items.FirstOrDefault(i => i.ItemId == id)).Where(i => i != null).Select(i => i!).ToList();
            decimal price = Charge(night, adds, Math.Max(1, Nights(inDay, d.Value)), st.Size).Sum(x => x.Total);
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", st.ApptId, 50), App.P("vid", st.VisitId, 50), App.P("room", st.CurRoomId, 50), App.PDate("din", today), App.PDate("dout", d.Value), App.PDate("today", today),
                App.P("ign", st.ApptId, 50), App.P("ivis", st.VisitId, 50), App.PBit("inst", false), App.P("pid", st.PetId, 50), App.PDate("out", outAt), App.PDec("price", price), App.P("me", c.U.StaffId, 50),
                App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip("Check-out changed from " + When(PlanEnd(st)) + " to " + When(outAt), 300), 300)
            };
            string result = await Helper.PutData(new List<string>
            {
                PetLock,
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                RoomGuard,
                PetGuard,
                "UPDATE VISIT SET PLAN_OUT = @out, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'inroom'; " + Changed,
                "UPDATE APPOINTMENT SET END_AT = @out, EST_PRICE = @price, SYSDTE = GETDATE() WHERE APPT_ID = @aid",
                LogSql
            }, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "change check-out", st.PetName + " · failed", false);
                if (result.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    response.SetElementContents("bd-e-ext", Ui.Enc(st.CurRoomName + " was just booked for those nights. Move the pet first, or pick an earlier day."));
                    return response;
                }
                if (result.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase))
                {
                    response.SetElementContents("bd-e-ext", Ui.Enc(st.PetName + " already has another stay booked in those nights. Pick an earlier day or change that booking first."));
                    return response;
                }
                await Refresh(response, c, s, st.ApptId, Why(result));
                return response;
            }
            await App.Log(c.U.StaffId, "change check-out", st.PetName + " · " + Ui.Day(outAt, false), true);
            await Refresh(response, c, s, st.ApptId, "Check-out is now " + When(outAt) + ".");
            return response;
        }

        public async Task<ApiResponse> CareSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "care note");
            if (st == null)
            {
                return response;
            }
            string nid = NewKey();
            if (nid == string.Empty)
            {
                response.SetElementContents("bd-e-care", "This form is out of date. Close it and try again.");
                return response;
            }
            string text = Text("ctext", 1000);
            List<string> tags = Csv(V("ctags", 400)).Where(t => Tags.Contains(t)).ToList();
            if (text == string.Empty && tags.Count == 0)
            {
                response.SetElementContents("bd-e-care", "Pick a quick note or write something.");
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("nid", nid, 50), App.P("vid", st.VisitId, 50), App.P("me", c.U.StaffId, 50), App.P("txt", text, -1), App.P("tags", string.Join(", ", tags), -1) };
            string result = await Helper.PutData(new List<string>
            {
                "IF NOT EXISTS (SELECT 1 FROM VISIT WHERE VISIT_ID = @vid AND STATUS = N'inroom') THROW 50002, N'STATECHANGED', 1;",
                "IF NOT EXISTS (SELECT 1 FROM VISITNOTE WHERE NOTE_ID = @nid) INSERT INTO VISITNOTE (NOTE_ID, VISIT_ID, STAFF_ID, NOTE_KIND, SUBJECTIVE, OBJECTIVE, CREATED_UTC, SIGNED_UTC, SIGNED_BY) VALUES (@nid, @vid, @me, N'care', @txt, @tags, SYSUTCDATETIME(), SYSUTCDATETIME(), @me)"
            }, ps);
            if (Helper.IsError(result))
            {
                response.SetElementContents("bd-e-care", Ui.Enc(Why(result)));
                return response;
            }
            await App.Log(c.U.StaffId, "care note", st.PetName + " · " + (tags.Count > 0 ? string.Join(", ", tags) : Ui.OneLine(text, 60)), true);
            await Refresh(response, c, s, st.ApptId, "Added to the care log.");
            response.ExecuteScript(Me + ".toCare();");
            return response;
        }

        public async Task<ApiResponse> DeskSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            BdStay? st = await Reach(response, c, 'E', "desk note");
            if (st == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string note = Text("dnote", 600);
            string result = await Helper.PutData(new List<string> { "UPDATE APPOINTMENT SET DESK_NOTE = @note, SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding'" }, new List<SqlParameter> { App.P("note", note, 600), App.P("aid", st.ApptId, 50) });
            await App.Log(c.U.StaffId, "desk note", st.PetName + " · " + st.ApptCode, !Helper.IsError(result));
            Toast(response, Helper.IsError(result) ? Why(result) : "Desk note saved.");
            return response;
        }

        private sealed class Bill
        {
            public int Nights { get; set; }
            public List<QLine> Lines { get; set; } = new List<QLine>();
            public string Error { get; set; } = string.Empty;
        }

        private static async Task<Bill> Billing(BdStay st, DateTime today)
        {
            DateTime inDay = InDay(st);
            int nights = Math.Max(1, Nights(inDay, today));
            List<BdItem> items = await Items();
            Dictionary<string, string> typeNights = await TypeNights();
            List<BdSeg> segs = (await Segs(new List<string> { st.VisitId })).OrderBy(x => x.InAt).ToList();
            List<QLine> lines = new List<QLine>();
            for (int k = 0; k < nights; k++)
            {
                DateTime d = inDay.AddDays(k);
                BdSeg? g = segs.LastOrDefault(x => x.InAt.Date <= d) ?? segs.FirstOrDefault();
                string rt = g != null && g.RoomtypeId != string.Empty ? g.RoomtypeId : st.RoomtypeId;
                BdItem? night = NightItem(typeNights, items, rt);
                if (night == null)
                {
                    return new Bill { Nights = nights, Error = "This kind of run has no nightly price set. Ask a manager to set it up in the catalog." };
                }
                QLine? line = lines.FirstOrDefault(x => x.Item.ItemId == night.ItemId);
                if (line == null)
                {
                    lines.Add(new QLine { Item = night, Qty = 1, Price = PriceFor(night, st.Size) });
                }
                else
                {
                    line.Qty += 1;
                }
            }
            List<BdItem> adds = Csv(st.Addons).Select(id => items.FirstOrDefault(i => i.ItemId == id)).Where(i => i != null).Select(i => i!).ToList();
            lines.AddRange(Charge(null, adds, nights, st.Size));
            return new Bill { Nights = nights, Lines = lines };
        }

        public async Task<ApiResponse> CheckOutForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            BdStay? st = await InHouse(response, c, s, "check out stay");
            if (st == null)
            {
                return response;
            }
            DateTime today = App.Now(s).Date;
            Bill b = await Billing(st, today);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Check out " + Ui.Enc(st.PetName) + "</h2><p class=\"bd-lead\">Checked in " + Ui.Enc(When(st.CheckinAt ?? st.StartAt)) + " &middot; " + Ui.Enc(st.CurRoomName) + "</p>");
            if (PlanEnd(st).Date != today)
            {
                sb.Append("<div class=\"bd-warn\">The planned check-out was " + Ui.Enc(When(PlanEnd(st))) + ". " + Ui.Plural(b.Nights, "night is", "nights are") + " charged, counted from the check-in day to today.</div>");
            }
            if (b.Error != string.Empty)
            {
                sb.Append("<p class=\"bd-none\">" + Ui.Enc(b.Error) + "</p><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button></div>");
            }
            else
            {
                sb.Append("<div class=\"bd-box\">" + Lines(b.Lines, "Charges for " + Ui.Plural(b.Nights, "night", "nights"), "These lines go to the front desk, which makes the invoice and takes payment.") + "</div>");
                sb.Append("<em class=\"bd-err\" id=\"bd-e-cout\"></em><div class=\"bd-mfoot\"><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Back</button><button type=\"button\" class=\"bd-btn bd-btn-accent\" id=\"bd-cogo\" onclick=\"AdminBoardingJs.checkOutGo()\">Check out and send to the front desk</button></div>");
            }
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
            return response;
        }

        public async Task<ApiResponse> CheckOutGo()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string key = NewKey();
            BdStay? peek = await Reach(response, c, 'E', "check out stay");
            if (peek == null)
            {
                return response;
            }
            if (key != string.Empty && peek.State != "inhouse")
            {
                var (had, herr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ORDERLINE WHERE LINE_ID = @l", new List<SqlParameter> { App.P("l", key + "-1", 50) });
                if (herr == string.Empty && Convert.ToInt32(had.Rows[0][0]) > 0)
                {
                    await Refresh(response, c, s, peek.ApptId, peek.PetName + " is checked out.");
                    return response;
                }
            }
            BdStay? st = await InHouse(response, c, s, "check out stay");
            if (st == null)
            {
                return response;
            }
            if (key == string.Empty)
            {
                response.SetElementContents("bd-e-cout", "This form is out of date. Close it and try again.");
                return response;
            }
            DateTime now = App.Now(s);
            Bill b = await Billing(st, now.Date);
            if (b.Error != string.Empty)
            {
                response.SetElementContents("bd-e-cout", Ui.Enc(b.Error));
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", st.ApptId, 50), App.P("vid", st.VisitId, 50), App.PDate("now", now), App.P("me", c.U.StaffId, 50), App.P("lid", App.NewId(), 50),
                App.P("ltext", Ui.Clip("Checked out after " + Ui.Plural(b.Nights, "night", "nights") + " from " + st.CurRoomName + ", sent to the front desk", 300), 300),
                App.P("lnote", Ui.Clip(Ui.Plural(b.Nights, "night", "nights") + " · " + Ui.Day(InDay(st), false) + " to " + Ui.Day(now, false), 300), 300)
            };
            List<string> sql = new List<string>
            {
                "UPDATE VISIT SET STATUS = N'checkout', READY_AT = @now, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom'; " + Changed,
                "UPDATE VISITROOM SET OUT_AT = @now WHERE VISIT_ID = @vid AND OUT_AT IS NULL"
            };
            for (int i = 0; i < b.Lines.Count; i++)
            {
                QLine l = b.Lines[i];
                string n = i.ToString(Ui.Inv);
                ps.Add(App.P("l" + n, key + "-" + (i + 1).ToString(Ui.Inv), 50));
                ps.Add(App.P("i" + n, l.Item.ItemId, 50));
                ps.Add(App.P("n" + n, l.Item.ItemName, 120));
                ps.Add(App.P("t" + n, l.Item.ItemType, 20));
                ps.Add(App.PDec("q" + n, l.Qty));
                ps.Add(App.P("u" + n, l.Item.Unit, 20));
                ps.Add(App.PDec("p" + n, l.Price));
                ps.Add(App.PBit("x" + n, l.Item.Taxable));
                ps.Add(App.P("ds" + n, l.Item.Dose, 120));
                ps.Add(App.P("in" + n, l.Item.Instructions, 600));
                sql.Add("INSERT INTO ORDERLINE (LINE_ID, VISIT_ID, ITEM_ID, ITEM_NAME, ITEM_TYPE, QTY, UNIT, UNIT_PRICE, TAXABLE, DOSE, INSTRUCTIONS, DISPENSE, STATUS, NOTE, ORDERED_BY, ORDERED_UTC, DONE_BY, DONE_UTC, LINE_ORDER) SELECT @l" + n + ", @vid, @i" + n + ", @n" + n + ", @t" + n + ", @q" + n + ", @u" + n + ", @p" + n + ", @x" + n + ", @ds" + n + ", @in" + n + ", N'clinic', N'done', " + (i == 0 ? "@lnote" : "N''") + ", @me, SYSUTCDATETIME(), @me, SYSUTCDATETIME(), ISNULL(MAX(LINE_ORDER), 0) + 1 FROM ORDERLINE WHERE VISIT_ID = @vid");
            }
            sql.Add(LogSql);
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "check out stay", st.PetName + " · failed", false);
                await Refresh(response, c, s, st.ApptId, Why(result));
                return response;
            }
            decimal total = b.Lines.Sum(x => x.Total);
            await App.Log(c.U.StaffId, "check out stay", st.PetName + " · " + Ui.Plural(b.Nights, "night", "nights") + " · " + total.ToString("0.00", Ui.Inv), true);
            await Paint(response, c, s);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bd-donebox\"><span class=\"bd-st bd-st-ready\">Ready for checkout</span><h2>" + Ui.Enc(st.PetName) + " is checked out</h2><p>" + Ui.Plural(b.Nights, "night", "nights") + " &middot; " + Ui.Money(total) + " sent to the front desk. " + Ui.Enc(st.CurRoomName) + " is free.</p><div class=\"bd-acts\">");
            if (c.Can("checkout", 'V'))
            {
                sb.Append("<a class=\"bd-btn bd-btn-accent\" href=\"AdminCheckout?v=" + Ui.Attr(Uri.EscapeDataString(st.VisitId)) + "\">Take payment</a>");
            }
            sb.Append("<button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.back()\">Open the stay</button><button type=\"button\" class=\"bd-btn bd-btn-ghost\" onclick=\"AdminBoardingJs.closeModal()\">Close</button></div></div>");
            response.SetElementContents("bd-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + Ui.Js(st.ApptId) + "');");
            response.ExecuteScript(Me + ".after();");
            return response;
        }
    }
}
