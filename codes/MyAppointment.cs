using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class MyAppointment : WebPage
    {
        private static readonly (string Key, string Name)[] Statuses =
        {
            ("booked", "Booked"), ("confirmed", "Confirmed"), ("arrived", "Checked in"), ("inroom", "In room"), ("done", "Completed"), ("droppedoff", "Dropped off"),
            ("insurgery", "In surgery"), ("ready", "Ready for pick-up"), ("discharged", "Home again"), ("checkedin", "Checked in"), ("checkedout", "Checked out"), ("noshow", "No-show"), ("cancelled", "Cancelled")
        };

        private const string Cols = "SELECT A.APPT_ID AS ApptId, A.APPT_CODE AS ApptCode, A.CLIENT_ID AS ClientId, A.PET_ID AS PetId, A.BOOKTYPE_ID AS BooktypeId, ISNULL(B.BOOK_NAME, N'') AS BookName, A.VISIT_TYPE AS VisitType, A.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, A.ROOMTYPE_ID AS RoomtypeId, ISNULL(T.TYPE_NAME, N'') AS TypeName, A.ROOM_ID AS RoomId, A.START_AT AS StartAt, A.END_AT AS EndAt, A.MINUTES AS Minutes, A.PROC_AT AS ProcAt, A.PROC_MINUTES AS ProcMinutes, A.PICKUP_AT AS PickupAt, A.ADDONS AS Addons, A.EST_PRICE AS EstPrice, A.STATUS AS Status, A.VISIT_ID AS VisitId, ISNULL(V.STATUS, N'') AS VisitStatus, A.VAX_PENDING AS VaxPending, A.NOTE AS Note, " +
            "ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(P.SPECIES_KEY, N'') AS SpeciesKey, ISNULL(P.BREED, N'') AS Breed, ISNULL(P.SIZE, N'') AS Size, ISNULL(P.PHOTO, N'') AS Photo, C.FIRST_NAME AS FirstName, C.LAST_NAME AS LastName, C.EMAIL AS Email, C.PHONE AS Phone, C.PHONE2 AS Phone2 " +
            "FROM APPOINTMENT A JOIN CLIENT C ON C.CLIENT_ID = A.CLIENT_ID LEFT JOIN PET P ON P.PET_ID = A.PET_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID LEFT JOIN ROOMTYPE T ON T.ROOMTYPE_ID = A.ROOMTYPE_ID LEFT JOIN VISIT V ON V.VISIT_ID = A.VISIT_ID AND A.VISIT_ID <> N'' WHERE A.APPT_CODE = @code";

        private const string BusyCond = "SELECT A.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM APPOINTMENT A LEFT JOIN PET P ON P.PET_ID = A.PET_ID WHERE A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND A.ROOM_ID <> N'' AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din " +
            "UNION ALL SELECT X.ROOM_ID AS RoomId, ISNULL(P.PET_NAME, N'') AS PetName FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))";

        private const string RoomGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT A WHERE A.ROOM_ID = @room AND A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed') AND A.APPT_ID <> @ign AND CAST(A.START_AT AS DATE) < @dout AND CAST(A.END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISITROOM X JOIN VISIT V ON V.VISIT_ID = X.VISIT_ID WHERE X.ROOM_ID = @room AND X.OUT_AT IS NULL AND X.VISIT_ID <> @ivis AND (@inst = 1 OR V.PLAN_OUT IS NULL OR CAST(V.PLAN_OUT AS DATE) > @din OR (CAST(V.PLAN_OUT AS DATE) < @today AND @din <= DATEADD(DAY, 1, @today)))) THROW 50001, N'ROOMBUSY', 1;";

        private const string PetGuard = "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND APPT_ID <> @ign AND CAST(START_AT AS DATE) < @dout AND CAST(END_AT AS DATE) > @din) OR EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid AND VISIT_TYPE = N'boarding' AND STATUS = N'inroom' AND VISIT_ID <> @ivis AND (CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) > @din OR CAST(ISNULL(PLAN_OUT, CHECKIN_AT) AS DATE) < @today)) THROW 50005, N'PETBUSY', 1;";

        private static readonly List<DateTime> Misses = new List<DateTime>();
        private static readonly Dictionary<string, List<DateTime>> Calls = new Dictionary<string, List<DateTime>>();

        private const string LogSql = "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) VALUES (@lid, @aid, SYSUTCDATETIME(), N'web', @ltext)";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string code = Regex.Replace(Ui.Clip(QueryValue("c"), 20).ToUpperInvariant(), "[^A-Z0-9-]", string.Empty);
            string title = "My booking";
            string desc = "Check, change or cancel a visit or boarding stay at " + site.Settings.Name + ".";
            string full = title + " | " + site.Settings.Name;
            HtmlDoc.SetTitle(Ui.Enc(full));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", Ui.Enc(desc));
            HtmlDoc.AddMetaElement("robots", "noindex");
            HttpContext? h = HttpCurrent.Context;
            string root = h != null ? h.Request.Scheme + "://" + h.Request.Host + h.Request.PathBase + "/" : string.Empty;
            foreach ((string k, string v) in new[] { ("og:type", "website"), ("og:site_name", site.Settings.Name), ("og:title", full), ("og:description", desc), ("og:image", root + "images/hospital/full/reception.webp") })
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Enc(v) } });
            }
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, "ma", "MyAppointmentJs", "MyAppointment", site).Replace("{plhd_main}", Body(site, code));
        }

        private static string Body(SiteInfo site, string code)
        {
            StringBuilder sb = new StringBuilder("<section class=\"ma-phead\"><div class=\"ma-wrap\"><span class=\"ma-kick\">Your booking</span><h1>My booking</h1><p class=\"ma-lead\">Check a visit or a boarding stay, pick a new time or cancel. You need your booking code and the email or phone you booked with.</p></div></section>");
            sb.Append("<div class=\"ma-wrap ma-magrid\"><form class=\"ma-box ma-find\" onsubmit=\"return MyAppointmentJs.find()\" novalidate><h2>Find your booking</h2><label class=\"ma-field\"><span>Booking code</span><input type=\"text\" id=\"ma-code\" maxlength=\"14\" autocomplete=\"off\" autocapitalize=\"characters\" spellcheck=\"false\" placeholder=\"CAH-XXXXXX\" value=\"" + Ui.Attr(code) + "\"></label><label class=\"ma-field\"><span>Email or phone you booked with</span><input type=\"text\" id=\"ma-who\" maxlength=\"120\" autocomplete=\"email\"></label><em class=\"ma-err\" id=\"ma-e-find\" role=\"alert\"></em><button type=\"submit\" class=\"ma-btn ma-btn-accent ma-btn-wide\" id=\"ma-fbtn\">Find my booking</button><p class=\"ma-hint\">Visit codes look like CAH-7KQ2ZD4M. Boarding codes start with CAH-S. Lost your code? Call us on " + Ui.Enc(site.Settings.Phone) + ".</p></form>");
            sb.Append("<div class=\"ma-res\" id=\"ma-res\" aria-live=\"polite\"><div class=\"ma-empty\"><h2>Your booking appears here</h2><p>Visits can be changed or cancelled free up to " + site.Settings.CancelHours + " hours before. Boarding stays up to " + site.Settings.StayCancelHours + " hours before check-in.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-ghost ma-btn-sm\" href=\"Book\">Book a visit</a><a class=\"ma-btn ma-btn-ghost ma-btn-sm\" href=\"BookStay\">Book a stay</a></div></div></div></div>");
            return sb.ToString();
        }

        private static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd d MMM yyyy" : "ddd d MMM", Ui.Inv);
        }

        private static string LongDay(DateTime d)
        {
            return d.ToString("dddd, d MMMM yyyy", Ui.Inv);
        }

        private static string Stamp(DateTime d)
        {
            return d.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv);
        }

        private static string Hm(SiteInfo site, DateTime d)
        {
            return d.ToString(site.Settings.Clock24 ? "HH:mm" : "h:mm tt", Ui.Inv);
        }

        private static string HmText(SiteInfo site, string hm)
        {
            TimeSpan? t = Ui.ParseHm(hm);
            return t == null ? string.Empty : Hm(site, DateTime.Today.Add(t.Value));
        }

        private static string Hm24(TimeSpan t)
        {
            return t.ToString("hh\\:mm", Ui.Inv);
        }

        private static string Cash(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.00", Ui.Inv);
        }

        private static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..") && File.Exists(Path.Combine(App.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string Face(Clinician? v)
        {
            if (v != null && SafeImage(v.Photo))
            {
                return "<img class=\"ma-face\" src=\"" + Ui.Attr(v.Photo) + "\" alt=\"\" loading=\"lazy\" width=\"480\" height=\"600\">";
            }
            return "<span class=\"ma-face ma-noface\" style=\"--h:" + Ui.Hue(v?.Id ?? "x") + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(v?.Name ?? "?")) + "</span>";
        }

        private static string PetPic(MaAppt a, Clinician? p)
        {
            if (SafeImage(a.Photo))
            {
                return "<img class=\"ma-face ma-petpic\" src=\"" + Ui.Attr(a.Photo) + "\" alt=\"\" width=\"72\" height=\"72\">";
            }
            return Face(p);
        }

        private static string PetLine(SiteInfo site, MaAppt a)
        {
            return a.PetName + " (" + (a.Breed != string.Empty ? a.Breed : site.SpeciesName(a.SpeciesKey)) + ")";
        }

        private static string StateOf(MaAppt a)
        {
            if (a.Status != "checkedin")
            {
                return a.Status;
            }
            if (a.VisitType == "boarding")
            {
                return a.VisitStatus == "closed" ? "checkedout" : "checkedin";
            }
            bool drop = a.ProcAt.HasValue;
            return a.VisitStatus switch
            {
                "waiting" => drop ? "droppedoff" : "arrived",
                "inroom" => drop ? "insurgery" : "inroom",
                "checkout" => drop ? "ready" : "done",
                "closed" => drop ? "discharged" : "done",
                "cancelled" => "cancelled",
                _ => "arrived"
            };
        }

        private static string Pill(string state)
        {
            string name = Statuses.Where(x => x.Key == state).Select(x => x.Name).FirstOrDefault() ?? state;
            return "<span class=\"ma-st ma-st-" + Ui.Attr(state) + "\">" + Ui.Enc(name) + "</span>";
        }

        private (string Code, string Who) Keys()
        {
            return (Regex.Replace(Ui.Clip(GetDataValue("code"), 14).ToUpperInvariant(), "[^A-Z0-9-]", string.Empty), Ui.Clip(GetDataValue("who"), 120));
        }

        private static bool Owner(MaAppt a, string who)
        {
            if (who == string.Empty)
            {
                return false;
            }
            if (who.Contains('@'))
            {
                return a.Email != string.Empty && a.Email.Trim().Equals(who.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            string d = Ui.Digits(who);
            if (d.Length < 7)
            {
                return false;
            }
            foreach (string phone in new[] { a.Phone, a.Phone2 })
            {
                string mine = Ui.Digits(phone);
                if (mine.Length < 7)
                {
                    continue;
                }
                if (d == mine || (d.Length >= 10 && mine.Length >= 10 && d.Substring(d.Length - 10) == mine.Substring(mine.Length - 10)))
                {
                    return true;
                }
            }
            return false;
        }

        private static string WhoKey(string who)
        {
            if (who.Contains('@'))
            {
                return "e:" + who.Trim().ToLowerInvariant();
            }
            string d = Ui.Digits(who);
            return "p:" + (d.Length > 10 ? d.Substring(d.Length - 10) : d);
        }

        private static bool Busy()
        {
            lock (Misses)
            {
                Misses.RemoveAll(x => x < DateTime.UtcNow.AddHours(-1));
                return Misses.Count >= 200;
            }
        }

        private static bool Locked(string code, string who)
        {
            return Busy() || Auth.Locked("appt:" + code) || Auth.Locked("apptip:" + App.Ip()) || (who != string.Empty && Auth.Locked("apptwho:" + WhoKey(who)));
        }

        private static void Failed(string code, string who)
        {
            Auth.Fail("appt:" + code);
            Auth.Fail("apptip:" + App.Ip());
            if (who != string.Empty)
            {
                Auth.Fail("apptwho:" + WhoKey(who));
            }
            lock (Misses)
            {
                Misses.Add(DateTime.UtcNow);
                if (Misses.Count > 1000)
                {
                    Misses.RemoveAll(x => x < DateTime.UtcNow.AddHours(-1));
                }
            }
        }

        private static bool Slow(string key, int limit)
        {
            lock (Calls)
            {
                DateTime now = DateTime.UtcNow;
                if (Calls.Count > 5000)
                {
                    foreach (string old in Calls.Where(x => x.Value.Count == 0 || x.Value.Max() < now.AddMinutes(-1)).Select(x => x.Key).ToList())
                    {
                        Calls.Remove(old);
                    }
                    if (Calls.Count > 10000)
                    {
                        Calls.Clear();
                    }
                }
                if (!Calls.TryGetValue(key, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Calls[key] = list;
                }
                list.RemoveAll(x => x < now.AddMinutes(-1));
                if (list.Count >= limit)
                {
                    return true;
                }
                list.Add(now);
                return false;
            }
        }

        private static async Task<MaAppt?> ByCode(string code)
        {
            var (row, emsg) = await Helper.GetOneT<MaAppt>(Cols, new List<SqlParameter> { App.P("code", code, 20) });
            return emsg == string.Empty ? row : null;
        }

        private async Task<MaAppt?> Reach(bool stay)
        {
            (string code, string who) = Keys();
            if (code == string.Empty || who == string.Empty || Locked(code, who))
            {
                return null;
            }
            MaAppt? a = await ByCode(code);
            if (a == null || !Owner(a, who))
            {
                Failed(code, who);
                return null;
            }
            return (a.VisitType == "boarding") == stay ? a : null;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            (string code, string who) = Keys();
            if (code == string.Empty || who == string.Empty)
            {
                response.SetElementContents("ma-e-find", "Enter your booking code and the email or phone you booked with.");
                return response;
            }
            if (Busy())
            {
                response.SetElementContents("ma-e-find", "Online look-ups are busy right now. Please call us and we will help.");
                return response;
            }
            if (Locked(code, who))
            {
                response.SetElementContents("ma-e-find", "Too many tries. Please wait a few minutes or call us.");
                return response;
            }
            MaAppt? a = await ByCode(code);
            if (a == null || !Owner(a, who))
            {
                Failed(code, who);
                await App.Log("web", "booking lookup", code + " · not matched", false);
                response.SetElementContents("ma-e-find", "We couldn't find a booking with those details. Check the code and try the email or phone you used.");
                return response;
            }
            SiteInfo site = await Site.Load();
            response.SetElementContents("ma-res", a.VisitType == "boarding" ? await StayCard(site, a) : await Card(site, a));
            response.ExecuteScript("MyAppointmentJs.found();");
            return response;
        }

        private static string Safe(string text)
        {
            string t = (text ?? string.Empty).Trim();
            if (t == "Booked online" || t == "Cancelled online by the owner" || t.StartsWith("Moved online from ", StringComparison.Ordinal) || t.StartsWith("Dates changed online from ", StringComparison.Ordinal))
            {
                return t;
            }
            if (t.StartsWith("Booked", StringComparison.Ordinal))
            {
                return "Booked with the clinic";
            }
            if (t.StartsWith("Confirmed", StringComparison.Ordinal))
            {
                return "Confirmed by the clinic";
            }
            if (t.StartsWith("Moved from ", StringComparison.Ordinal) || (t.StartsWith("Changed: ", StringComparison.Ordinal) && t.Contains("dates now", StringComparison.Ordinal)))
            {
                return "Time changed by the clinic";
            }
            if (t.StartsWith("Cancelled", StringComparison.Ordinal))
            {
                return "Cancelled by the clinic";
            }
            if (t.StartsWith("Checked in", StringComparison.Ordinal))
            {
                return "Checked in";
            }
            if (t.StartsWith("Marked as no-show", StringComparison.Ordinal))
            {
                return "Marked as a no-show";
            }
            return string.Empty;
        }

        private static async Task<string> History(SiteInfo site, string apptId)
        {
            var (list, emsg) = await Helper.GetListT<MaLog>("SELECT LOG_UTC AS LogUtc, LOG_TEXT AS LogText FROM APPTLOG WHERE APPT_ID = @aid ORDER BY LOG_UTC DESC", new List<SqlParameter> { App.P("aid", apptId, 50) });
            List<(DateTime At, string Text)> rows = emsg == string.Empty && list != null ? list.Select(h => (h.LogUtc, Safe(h.LogText))).Where(x => x.Item2 != string.Empty).ToList() : new List<(DateTime, string)>();
            if (rows.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<details class=\"ma-hist\"><summary>History</summary><ol>");
            foreach ((DateTime at, string text) in rows)
            {
                DateTime l = App.Local(site.S, at);
                sb.Append("<li><time>" + Day(l, false) + " &middot; " + Hm(site, l) + "</time><span>" + Ui.Enc(text) + "</span></li>");
            }
            sb.Append("</ol></details>");
            return sb.ToString();
        }

        private static string PreOp(CareService? sv)
        {
            if (sv == null || sv.PreOp.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<div class=\"ma-preop\"><h3>Before you come</h3><ul>");
            foreach (string line in sv.PreOp)
            {
                sb.Append("<li>" + Ui.Enc(line) + "</li>");
            }
            sb.Append("</ul></div>");
            return sb.ToString();
        }

        private static string DropWindow(SiteInfo site, MaAppt a, CareService? sv)
        {
            TimeSpan? df = Ui.ParseHm(sv?.DropFrom);
            TimeSpan? dt = Ui.ParseHm(sv?.DropTo);
            if (df != null && dt != null && dt > df && a.StartAt.TimeOfDay == df.Value)
            {
                return Hm(site, a.StartAt) + " &ndash; " + Hm(site, a.StartAt.Date.Add(dt.Value));
            }
            return Hm(site, a.StartAt);
        }

        private static bool CanChange(SiteInfo site, MaAppt a)
        {
            return (a.Status is "booked" or "confirmed") && (a.StartAt - App.Now(site.S)).TotalHours >= Math.Max(0, site.Settings.CancelHours);
        }

        private static string Who(MaAppt a)
        {
            return a.FirstName + " " + (a.LastName.Length > 0 ? a.LastName.Substring(0, 1) + "." : string.Empty);
        }

        private static async Task<string> Card(SiteInfo site, MaAppt a)
        {
            CareService? sv = site.Service(a.BooktypeId);
            Clinician? p = site.Provider(a.StaffId);
            string pn = a.PetName != string.Empty ? a.PetName : "your pet";
            bool drop = a.ProcAt.HasValue;
            string state = StateOf(a);
            DateTime now = App.Now(site.S);
            string name = a.BookName != string.Empty ? a.BookName : "Appointment";
            StringBuilder sb = new StringBuilder("<article class=\"ma-box ma-appt\" id=\"ma-appt\" tabindex=\"-1\"" + IcsAttrs(site, a, sv) + "><div class=\"ma-atop\">" + Pill(state) + "<span class=\"ma-acode\">" + Ui.Enc(a.ApptCode) + "</span></div><div class=\"ma-amain\">" + PetPic(a, p) + "<div><h2>" + Ui.Enc(name) + "</h2><p>for " + Ui.Enc(pn) + " with " + Ui.Enc(a.StaffName != string.Empty ? a.StaffName : "our team") + "</p></div></div><dl class=\"ma-sumdl\">");
            sb.Append("<div><dt>Pet</dt><dd>" + Ui.Enc(PetLine(site, a)) + "</dd></div>");
            if (drop)
            {
                sb.Append("<div><dt>Drop off</dt><dd>" + LongDay(a.StartAt) + "<br>" + DropWindow(site, a, sv) + "</dd></div><div><dt>Pick up</dt><dd>" + (a.PickupAt != null ? "From " + Hm(site, a.PickupAt.Value) : "We will call you") + "</dd></div>");
            }
            else
            {
                sb.Append("<div><dt>When</dt><dd>" + LongDay(a.StartAt) + "<br>" + Hm(site, a.StartAt) + " &ndash; " + Hm(site, a.EndAt) + "</dd></div>");
            }
            sb.Append("<div><dt>Where</dt><dd>" + Ui.Enc(site.Settings.Name) + ", " + Ui.Enc(site.Settings.Address) + "</dd></div>");
            sb.Append("<div><dt>Booked by</dt><dd>" + Ui.Enc(Who(a)) + "</dd></div>");
            if (site.Settings.ShowPrices && a.EstPrice > 0)
            {
                sb.Append("<div><dt>Price</dt><dd>" + (a.VisitType == "grooming" ? string.Empty : "From ") + Cash(a.EstPrice) + "</dd></div>");
            }
            sb.Append("</dl>");
            string again = "Book?sv=" + Uri.EscapeDataString(a.BooktypeId) + "&amp;p=" + Uri.EscapeDataString(a.StaffId) + (a.SpeciesKey != string.Empty ? "&amp;sp=" + Uri.EscapeDataString(a.SpeciesKey) : string.Empty);
            string acts = "<div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.resched()\">" + (drop ? "Change day" : "Change time") + "</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button><button type=\"button\" class=\"ma-btn ma-btn-danger\" onclick=\"MyAppointmentJs.cancel(" + (drop ? "1" : "0") + ")\">Cancel booking</button></div>";
            if ((a.Status is "booked" or "confirmed") && a.StartAt > now)
            {
                if (drop)
                {
                    sb.Append(PreOp(sv));
                }
                if (CanChange(site, a))
                {
                    sb.Append(acts);
                }
                else
                {
                    sb.Append("<p class=\"ma-warn-box\">This booking is less than " + site.Settings.CancelHours + " hours away, so it can't be changed online. To change or cancel, please call " + Ui.Enc(site.Settings.Phone) + ".</p><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button></div>");
                }
            }
            else if (a.Status is "booked" or "confirmed")
            {
                sb.Append("<p class=\"ma-info-box\">This booking time has passed. Questions? Call " + Ui.Enc(site.Settings.Phone) + ".</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book again</a></div>");
            }
            else if (state == "cancelled")
            {
                sb.Append("<p class=\"ma-info-box\">This booking was cancelled.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book again</a></div>");
            }
            else if (state is "done" or "discharged")
            {
                sb.Append("<p class=\"ma-ok-box\">" + (state == "done" ? "This visit is complete." : Ui.Enc(pn) + " is home again.") + " Thank you for trusting us with " + Ui.Enc(pn) + ".</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book again</a></div>");
            }
            else if (state == "noshow")
            {
                sb.Append("<p class=\"ma-warn-box\">We missed you at this visit.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"" + again + "\">Book a new time</a></div>");
            }
            else if (state is "arrived" or "inroom")
            {
                sb.Append("<p class=\"ma-ok-box\">You are checked in. Please take a seat; we will call you shortly.</p>");
            }
            else if (state is "droppedoff" or "insurgery")
            {
                sb.Append("<p class=\"ma-ok-box\">" + Ui.Enc(pn) + " is with us now, so this booking can't be changed or cancelled online. We will call you when " + Ui.Enc(pn) + " is ready to go home. Questions? Call " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            else if (state == "ready")
            {
                sb.Append("<p class=\"ma-ok-box\">" + Ui.Enc(pn) + " is ready for pick-up. See you soon.</p>");
            }
            sb.Append("<div id=\"ma-move\"></div>" + await History(site, a.ApptId) + "</article>");
            return sb.ToString();
        }

        private static DateTime LastDay(SiteInfo site)
        {
            return App.Now(site.S).Date.AddDays(Math.Clamp(site.Settings.BookAheadDays, 1, 365));
        }

        private static (TimeSpan From, TimeSpan To)? Clinic(SiteInfo site, DateTime day)
        {
            if (site.Holidays.Any(h => h.Date.Date == day.Date))
            {
                return null;
            }
            var (open, f, t) = Slots.Hours(site.S, day);
            return open ? (f, t) : null;
        }

        private static string SizeOf(SiteInfo site, MaAppt a)
        {
            return site.HasSize(a.SpeciesKey) && a.Size is "small" or "medium" or "large" ? a.Size : string.Empty;
        }

        private static async Task<List<Opening>> Avail(SiteInfo site, MaAppt a, CareService sv, Clinician p, DateTime day)
        {
            List<(Opening O, Clinician P)> list = await Site.Openings(site, sv, p.Id, day, SizeOf(site, a), a.SpeciesKey, a.ApptId);
            List<Opening> result = new List<Opening>();
            foreach (Opening o in list.Where(x => x.P.Id == p.Id).Select(x => x.O).OrderBy(o => o.Start).ThenBy(o => o.ProcAt ?? o.Start))
            {
                if (o.Start != a.StartAt && !result.Any(r => r.Start == o.Start))
                {
                    result.Add(o);
                }
            }
            return result;
        }

        private string V(string key, int max)
        {
            return Ui.Clip(GetDataValue(key) ?? string.Empty, max);
        }

        private static void Dead(ApiResponse response, string msg)
        {
            response.ExecuteScript("MyAppointmentJs.toast('" + Ui.Js(msg) + "');");
        }

        public async Task<ApiResponse> Times()
        {
            ApiResponse response = new ApiResponse();
            if (Slow("times:" + App.Ip(), 60))
            {
                Dead(response, "Please slow down a moment, then try again.");
                return response;
            }
            MaAppt? a = await Reach(false);
            SiteInfo site = await Site.Load();
            if (a == null || !CanChange(site, a))
            {
                Dead(response, "This booking can no longer be changed online.");
                return response;
            }
            CareService? sv = site.Service(a.BooktypeId);
            Clinician? p = site.Provider(a.StaffId);
            if (sv == null || p == null || !Site.Online(p, sv))
            {
                response.SetElementContents("ma-move", "<p class=\"ma-warn-box\">This booking can't be moved online right now. Please call " + Ui.Enc(site.Settings.Phone) + ".</p>");
                response.ExecuteScript("MyAppointmentJs.moving();");
                return response;
            }
            DateTime today = App.Now(site.S).Date;
            DateTime last = LastDay(site);
            DateTime wk = Ui.ParseDay(V("wk", 12)) ?? today;
            wk = wk < today ? today : wk > last ? last : wk;
            DateTime? day = Ui.ParseDay(V("day", 12));
            if (day == null || day < wk || day > wk.AddDays(6))
            {
                day = null;
                for (int i = 0; i < 7 && day == null; i++)
                {
                    if ((await Avail(site, a, sv, p, wk.AddDays(i))).Count > 0)
                    {
                        day = wk.AddDays(i);
                    }
                }
            }
            bool drop = a.ProcAt.HasValue;
            string head = drop ? "Pick a new drop-off day with " + Ui.Enc(p.Name) : "Pick a new time with " + Ui.Enc(p.Name);
            response.SetElementContents("ma-move", "<div class=\"ma-mv\" id=\"ma-mv\" data-wk=\"" + Ui.Ymd(wk) + "\"><div class=\"ma-mvh\"><h3>" + head + "</h3><button type=\"button\" class=\"ma-btn ma-btn-sm ma-btn-ghost\" onclick=\"MyAppointmentJs.closeMove()\">Keep my time</button></div>" + await Strip(site, a, sv, p, wk, day ?? wk) + "</div>");
            response.ExecuteScript("MyAppointmentJs.moving();");
            return response;
        }

        private static async Task<string> Strip(SiteInfo site, MaAppt a, CareService sv, Clinician p, DateTime wk, DateTime day)
        {
            bool drop = a.ProcAt.HasValue;
            DateTime today = App.Now(site.S).Date;
            DateTime last = LastDay(site);
            DateTime prev = wk.AddDays(-7) < today ? today : wk.AddDays(-7);
            StringBuilder sb = new StringBuilder("<div class=\"ma-wk\"><button type=\"button\" class=\"ma-wnav\" aria-label=\"Earlier days\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(prev) + "')\"" + (wk <= today ? " disabled" : string.Empty) + ">&lsaquo;</button><div class=\"ma-days\">");
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                DateTime d = wk.AddDays(i);
                bool closed = Clinic(site, d) == null;
                int n = d > last || closed ? 0 : (await Avail(site, a, sv, p, d)).Count;
                total += n;
                string label = closed ? "Closed" : d > last ? "&mdash;" : n == 0 ? "Full" : drop ? "Space" : n + (n == 1 ? " time" : " times");
                sb.Append("<button type=\"button\" class=\"ma-day" + (d == day && n > 0 ? " ma-act" : string.Empty) + "\" aria-pressed=\"" + (d == day && n > 0 ? "true" : "false") + "\" onclick=\"MyAppointmentJs.day('" + Ui.Ymd(d) + "')\"" + (n == 0 ? " disabled" : string.Empty) + ">" + (d == today ? "<small class=\"ma-tdy\" data-d=\"" + d.ToString("ddd", Ui.Inv) + "\">Today</small>" : "<small>" + d.ToString("ddd", Ui.Inv) + "</small>") + "<b>" + d.Day + "</b><small>" + d.ToString("MMM", Ui.Inv) + "</small><em>" + label + "</em></button>");
            }
            sb.Append("</div><button type=\"button\" class=\"ma-wnav\" aria-label=\"Later days\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(wk.AddDays(7)) + "')\"" + (wk.AddDays(7) > last ? " disabled" : string.Empty) + ">&rsaquo;</button></div>");
            if (total == 0)
            {
                DateTime? n = null;
                for (int i = 7; i < 70 && n == null; i++)
                {
                    DateTime d = wk.AddDays(i);
                    if (d > last)
                    {
                        break;
                    }
                    List<Opening> l = await Avail(site, a, sv, p, d);
                    if (l.Count > 0)
                    {
                        n = l[0].Start;
                    }
                }
                sb.Append("<div class=\"ma-nofree\"><p>No free times in these seven days.</p>" + (n != null ? "<button type=\"button\" class=\"ma-btn ma-btn-sm ma-btn-accent\" onclick=\"MyAppointmentJs.week('" + Ui.Ymd(n.Value.Date) + "')\">Jump to " + Day(n.Value, false) + "</button>" : "<p class=\"ma-hint\">Please call " + Ui.Enc(site.Settings.Phone) + " and we will find you a time.</p>") + "</div>");
                return sb.ToString();
            }
            List<Opening> slots = await Avail(site, a, sv, p, day);
            sb.Append("<p class=\"ma-dayname\">" + LongDay(day) + "</p>");
            (string Name, int From, int To)[] parts = drop ? new[] { ("Drop-off time", 0, 1440) } : new[] { ("Morning", 0, 720), ("Afternoon", 720, 1020), ("Evening", 1020, 1440) };
            foreach ((string name, int from, int to) in parts)
            {
                List<Opening> part = slots.Where(x => x.Start.TimeOfDay.TotalMinutes >= from && x.Start.TimeOfDay.TotalMinutes < to).ToList();
                if (part.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"ma-tgroup\"><h3>" + name + "</h3><div class=\"ma-times\">");
                foreach (Opening o in part)
                {
                    sb.Append("<button type=\"button\" class=\"ma-ts\" onclick=\"MyAppointmentJs.pickTime('" + Stamp(o.Start) + "')\"><b>" + (drop ? DropWindowOf(site, sv, o.Start) : Hm(site, o.Start)) + "</b></button>");
                }
                sb.Append("</div></div>");
            }
            if (drop)
            {
                sb.Append("<p class=\"ma-pick\">Pick-up from " + HmText(site, sv.PickUp) + " the same day.</p>");
            }
            return sb.ToString();
        }

        private static string DropWindowOf(SiteInfo site, CareService sv, DateTime start)
        {
            TimeSpan? dt = Ui.ParseHm(sv.DropTo);
            return dt != null && dt.Value > start.TimeOfDay ? Hm(site, start) + " &ndash; " + Hm(site, start.Date.Add(dt.Value)) : Hm(site, start);
        }

        private static string SlotText(SiteInfo site, DateTime start, DateTime? proc)
        {
            return Day(start, false) + " " + (proc.HasValue ? "drop-off " : string.Empty) + Hm(site, start);
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            DateTime? t = Ui.ParseStamp(V("t", 20));
            MaAppt? a = await Reach(false);
            SiteInfo site = await Site.Load();
            if (a == null || t == null || !CanChange(site, a))
            {
                Dead(response, "This booking can no longer be changed online.");
                return response;
            }
            CareService? sv = site.Service(a.BooktypeId);
            Clinician? p = site.Provider(a.StaffId);
            Opening? hit = sv != null && p != null && Site.Online(p, sv) ? (await Avail(site, a, sv, p, t.Value.Date)).FirstOrDefault(o => o.Start == t.Value) : null;
            if (sv == null || p == null || hit == null)
            {
                Dead(response, "Sorry, that time is no longer free.");
                return response;
            }
            bool drop = hit.ProcAt.HasValue;
            if (V("go", 2) != "1")
            {
                string to = drop ? LongDay(t.Value) + ", drop off " + DropWindowOf(site, sv, t.Value) : LongDay(t.Value) + " at " + Hm(site, t.Value);
                response.SetElementContents("ma-move", "<div class=\"ma-mv ma-mvok\"><h3>" + (drop ? "Move your drop-off?" : "Move your visit?") + "</h3><p><s>" + Day(a.StartAt, false) + " at " + Hm(site, a.StartAt) + "</s><br><b>" + to + "</b></p><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.move('" + Stamp(t.Value) + "',1)\">Yes, move it</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.resched()\">Pick another time</button></div></div>");
                response.ExecuteScript("MyAppointmentJs.moving();");
                return response;
            }
            DateTime ba = hit.ProcAt ?? hit.Start;
            DateTime bz = hit.ProcAt.HasValue ? hit.ProcAt.Value.AddMinutes(hit.ProcMinutes) : hit.End;
            string text = "Moved online from " + SlotText(site, a.StartAt, a.ProcAt) + " to " + SlotText(site, hit.Start, hit.ProcAt);
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", a.ApptId, 50), App.P("pid", a.PetId, 50), App.PDate("pz", hit.PickupAt ?? hit.End), App.PDate("old", a.StartAt), App.P("sid", a.StaffId, 50), App.P("rt", a.RoomtypeId, 50), App.PDate("ba", ba), App.PDate("bz", bz), App.PDate("st", hit.Start), App.PDate("en", hit.End),
                App.PInt("mins", (int)(hit.End - hit.Start).TotalMinutes), App.PDate("proc", hit.ProcAt), App.PInt("pmins", hit.ProcAt.HasValue ? hit.ProcMinutes : 0), App.PDate("pick", hit.PickupAt),
                App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(text, 300), 300)
            };
            List<string> sql = new List<string>
            {
                "IF NOT EXISTS (SELECT 1 FROM APPOINTMENT WHERE APPT_ID = @aid AND STATUS IN (N'booked', N'confirmed') AND START_AT = @old) THROW 50002, N'STATECHANGED', 1;",
                "UPDATE STAFF SET SYSDTE = SYSDTE WHERE STAFF_ID = @sid",
                "UPDATE ROOMTYPE SET SYSDTE = SYSDTE WHERE ROOMTYPE_ID = @rt",
                "IF @rt <> N'' AND (SELECT COUNT(*) FROM APPOINTMENT WHERE ROOMTYPE_ID = @rt AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) >= (SELECT COUNT(*) FROM ROOM WHERE ROOMTYPE_ID = @rt AND ACTIVE = 1) THROW 50003, N'SLOTTAKEN', 1;",
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE STAFF_ID = @sid AND APPT_ID <> @aid AND STATUS NOT IN (N'cancelled', N'noshow') AND VISIT_TYPE <> N'boarding' AND ISNULL(PROC_AT, START_AT) < @bz AND (CASE WHEN PROC_AT IS NULL THEN END_AT ELSE DATEADD(MINUTE, PROC_MINUTES, PROC_AT) END) > @ba) THROW 50003, N'SLOTTAKEN', 1;",
                "UPDATE PET SET SYSDTE = SYSDTE WHERE PET_ID = @pid",
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid AND APPT_ID <> @aid AND STATUS IN (N'booked', N'confirmed', N'checkedin') AND VISIT_TYPE <> N'boarding' AND START_AT < @pz AND ISNULL(PICKUP_AT, END_AT) > @st) THROW 50006, N'PETBUSY', 1;",
                "UPDATE APPOINTMENT SET START_AT = @st, END_AT = @en, MINUTES = @mins, PROC_AT = @proc, PROC_MINUTES = @pmins, PICKUP_AT = @pick, STATUS = N'booked', SYSDTE = GETDATE() WHERE APPT_ID = @aid",
                LogSql
            };
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                await App.Log("web", "move booking online", a.ApptCode + " · failed", false);
                Dead(response, r.Contains("SLOTTAKEN", StringComparison.OrdinalIgnoreCase) || r.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase) ? "That time isn't available. Please pick another." : "This booking was just changed. Please look it up again.");
                return response;
            }
            await App.Log("web", "move booking online", a.ApptCode + " · " + SlotText(site, hit.Start, hit.ProcAt), true);
            MaAppt? fresh = await ByCode(a.ApptCode);
            response.SetElementContents("ma-res", await Card(site, fresh ?? a));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Done. Your booking has been moved.');");
            return response;
        }

        public async Task<ApiResponse> Cancel()
        {
            ApiResponse response = new ApiResponse();
            MaAppt? a = await Reach(false);
            SiteInfo site = await Site.Load();
            if (a == null || !CanChange(site, a))
            {
                Dead(response, "This booking can no longer be cancelled online.");
                return response;
            }
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = N'Cancelled online by the owner', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;",
                LogSql
            }, new List<SqlParameter> { App.P("aid", a.ApptId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", "Cancelled online by the owner", 300) });
            if (Helper.IsError(r))
            {
                await App.Log("web", "cancel booking online", a.ApptCode + " · failed", false);
                Dead(response, "This booking was just changed. Please look it up again.");
                return response;
            }
            await App.Log("web", "cancel booking online", a.ApptCode + " · " + a.PetName, true);
            MaAppt? fresh = await ByCode(a.ApptCode);
            response.SetElementContents("ma-res", await Card(site, fresh ?? a));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Your booking has been cancelled.');");
            return response;
        }

        private static TimeSpan OutBy(SiteInfo site)
        {
            return Ui.ParseHm(site.Settings.CheckOutBy) ?? TimeSpan.FromHours(11);
        }

        private static int Nights(DateTime a, DateTime b)
        {
            return Math.Max(0, (b.Date - a.Date).Days);
        }

        private static bool CanStay(SiteInfo site, MaAppt s)
        {
            return (s.Status is "booked" or "confirmed") && s.VisitId == string.Empty && (s.StartAt - App.Now(site.S)).TotalHours >= Math.Max(0, site.Settings.StayCancelHours);
        }

        private static async Task<List<MaItem>> Items(List<string> ids)
        {
            if (ids.Count == 0)
            {
                return new List<MaItem>();
            }
            List<SqlParameter> ps = new List<SqlParameter>();
            string inList = Slots.InList(ids, "it", ps);
            var (list, emsg) = await Helper.GetListT<MaItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, UNIT AS Unit, PRICE AS Price, PRICE_M AS PriceM, PRICE_L AS PriceL FROM ITEM WHERE ITEM_ID IN (" + inList + ")", ps);
            return emsg == string.Empty && list != null ? list : new List<MaItem>();
        }

        private static decimal Sized(MaItem i, string size)
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

        private static async Task<decimal> NightPrice(string roomtypeId, string size)
        {
            var (row, emsg) = await Helper.GetOneT<MaItem>("SELECT I.ITEM_ID AS ItemId, I.ITEM_NAME AS ItemName, I.UNIT AS Unit, I.PRICE AS Price, I.PRICE_M AS PriceM, I.PRICE_L AS PriceL FROM ROOMTYPE T JOIN ITEM I ON I.ITEM_ID = T.NIGHT_ITEM_ID WHERE T.ROOMTYPE_ID = @rt", new List<SqlParameter> { App.P("rt", roomtypeId, 50) });
            return emsg == string.Empty && row != null ? Sized(row, size) : 0m;
        }

        private static async Task<decimal> StayPrice(SiteInfo site, MaAppt s, int nights)
        {
            string size = SizeOf(site, s);
            decimal total = await NightPrice(s.RoomtypeId, size) * nights;
            foreach (MaItem i in await Items(Site.Csv(s.Addons)))
            {
                total += i.Unit is "day" or "night" ? Sized(i, size) * nights : Sized(i, size);
            }
            return total;
        }

        private static async Task<List<string>> Missing(SiteInfo site, MaAppt s, DateTime outDay)
        {
            VaxRule? rule = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == s.SpeciesKey);
            if (rule == null || rule.Required.Count == 0)
            {
                return new List<string>();
            }
            var (rows, emsg) = await Helper.GetListT<MaVax>("SELECT VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE PET_ID = @pid", new List<SqlParameter> { App.P("pid", s.PetId, 50) });
            List<MaVax> list = emsg == string.Empty && rows != null ? rows : new List<MaVax>();
            List<string> miss = new List<string>();
            foreach (string name in rule.Required)
            {
                MaVax? last = list.Where(r => r.VaxName.Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.GivenDate).ThenByDescending(r => r.DueDate).FirstOrDefault();
                if (last == null || last.DueDate.Date < outDay.Date)
                {
                    miss.Add(name);
                }
            }
            return miss;
        }

        private static async Task<string> VaxNote(SiteInfo site, MaAppt s)
        {
            VaxRule? rule = site.Settings.BoardingVax.FirstOrDefault(v => v.Species == s.SpeciesKey);
            if (rule == null || rule.Required.Count == 0)
            {
                return "<p class=\"ma-ok-box\">No vaccines are needed for this stay.</p>";
            }
            List<string> missing = await Missing(site, s, s.EndAt);
            if (missing.Count == 0)
            {
                return "<p class=\"ma-ok-box\">Vaccines on file are valid until check-out: " + Ui.Enc(string.Join(", ", rule.Required)) + ".</p>";
            }
            CareService? shot = site.Services.FirstOrDefault(x => x.Online && x.VisitType == "clinic" && x.Name.Contains("vaccine", StringComparison.OrdinalIgnoreCase) && Site.ForSpecies(x, s.SpeciesKey));
            return "<p class=\"ma-warn-box\">Please bring proof of these vaccines at check-in: " + Ui.Enc(string.Join(", ", missing)) + ". They must be valid until check-out." + (shot != null ? " Or <a href=\"Book?sv=" + Uri.EscapeDataString(shot.Key) + "&amp;sp=" + Uri.EscapeDataString(s.SpeciesKey) + "\">book a vaccine visit</a> before the stay." : string.Empty) + "</p>";
        }

        private static (string Feeding, string Other) SplitNote(string note)
        {
            string n = (note ?? string.Empty).Replace("\r", string.Empty);
            if (!n.StartsWith("Feeding: ", StringComparison.Ordinal))
            {
                return (string.Empty, n);
            }
            int i = n.IndexOf('\n');
            return i < 0 ? (n.Substring(9), string.Empty) : (n.Substring(9, i - 9), n.Substring(i + 1));
        }

        private static async Task<string> StayCard(SiteInfo site, MaAppt s)
        {
            string pn = s.PetName != string.Empty ? s.PetName : "your pet";
            int nights = Nights(s.StartAt, s.EndAt);
            string state = StateOf(s);
            List<MaItem> adds = await Items(Site.Csv(s.Addons));
            string run = s.TypeName != string.Empty ? s.TypeName : "Boarding";
            StringBuilder sb = new StringBuilder("<article class=\"ma-box ma-appt\" id=\"ma-appt\" tabindex=\"-1\"" + StayIcs(site, s, run) + "><div class=\"ma-atop\">" + Pill(state) + "<span class=\"ma-acode\">" + Ui.Enc(s.ApptCode) + "</span></div><div class=\"ma-amain\">" + (SafeImage(s.Photo) ? "<img class=\"ma-face ma-petpic\" src=\"" + Ui.Attr(s.Photo) + "\" alt=\"\" width=\"72\" height=\"72\">" : "<span class=\"ma-face ma-noface ma-stayic\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"30\" height=\"30\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\"><path d=\"M3 11l9-7 9 7v9H3z\"/><path d=\"M9 20v-6h6v6\"/></svg></span>") + "<div><h2>Boarding stay for " + Ui.Enc(pn) + "</h2><p>" + Ui.Enc(run) + " &middot; " + Ui.Plural(nights, "night", "nights") + "</p></div></div><dl class=\"ma-sumdl\">");
            sb.Append("<div><dt>Pet</dt><dd>" + Ui.Enc(PetLine(site, s)) + "</dd></div>");
            sb.Append("<div><dt>Check in</dt><dd>" + LongDay(s.StartAt) + "<br>at " + Hm(site, s.StartAt) + "</dd></div>");
            sb.Append("<div><dt>Check out</dt><dd>" + LongDay(s.EndAt) + "<br>by " + Hm(site, s.EndAt) + "</dd></div>");
            sb.Append("<div><dt>Room</dt><dd>" + Ui.Enc(run) + "</dd></div>");
            sb.Append("<div><dt>Extras</dt><dd>" + (adds.Count > 0 ? Ui.Enc(string.Join(", ", adds.Select(x => x.ItemName))) : "None") + "</dd></div>");
            string feeding = SplitNote(s.Note).Feeding;
            if (feeding != string.Empty)
            {
                sb.Append("<div><dt>Feeding</dt><dd>" + Ui.Enc(feeding) + "</dd></div>");
            }
            if (site.Settings.ShowPrices && s.EstPrice > 0)
            {
                sb.Append("<div><dt>Price</dt><dd>" + Cash(s.EstPrice) + "</dd></div>");
            }
            sb.Append("<div><dt>Booked by</dt><dd>" + Ui.Enc(Who(s)) + "</dd></div></dl>");
            if (s.Status is "booked" or "confirmed")
            {
                sb.Append(await VaxNote(site, s));
                if (CanStay(site, s))
                {
                    sb.Append("<div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.stayDates()\">Change dates</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button><button type=\"button\" class=\"ma-btn ma-btn-danger\" onclick=\"MyAppointmentJs.cancelStay()\">Cancel stay</button></div>");
                }
                else
                {
                    sb.Append("<p class=\"ma-warn-box\">Check-in is less than " + site.Settings.StayCancelHours + " hours away, so this stay can't be changed online. To change or cancel, please call " + Ui.Enc(site.Settings.Phone) + ".</p><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.ics('ma-appt')\">Add to calendar</button></div>");
                }
            }
            else if (state == "checkedin")
            {
                sb.Append("<p class=\"ma-ok-box\">" + Ui.Enc(pn) + " is staying with us. Check-out is by " + Hm(site, s.EndAt) + " on " + Day(s.EndAt, false) + ".</p>");
            }
            else if (state == "checkedout")
            {
                sb.Append("<p class=\"ma-ok-box\">" + Ui.Enc(pn) + " is home again. Thank you for staying with us.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"BookStay?run=" + Uri.EscapeDataString(s.RoomtypeId) + "\">Book another stay</a></div>");
            }
            else if (state == "cancelled")
            {
                sb.Append("<p class=\"ma-info-box\">This stay was cancelled.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"BookStay?run=" + Uri.EscapeDataString(s.RoomtypeId) + "\">Book again</a></div>");
            }
            else if (state == "noshow")
            {
                sb.Append("<p class=\"ma-warn-box\">We missed you at check-in for this stay.</p><div class=\"ma-aacts\"><a class=\"ma-btn ma-btn-accent\" href=\"BookStay?run=" + Uri.EscapeDataString(s.RoomtypeId) + "\">Book a new stay</a></div>");
            }
            sb.Append("<div id=\"ma-move\"></div>" + await History(site, s.ApptId) + "</article>");
            return sb.ToString();
        }

        private static List<string> InTimes(SiteInfo site)
        {
            TimeSpan from = Ui.ParseHm(site.Settings.CheckInFrom) ?? TimeSpan.FromHours(7.5);
            TimeSpan to = Ui.ParseHm(site.Settings.CheckInTo) ?? TimeSpan.FromHours(18);
            List<string> list = new List<string>();
            for (TimeSpan t = from; t <= to; t += TimeSpan.FromMinutes(30))
            {
                list.Add(Hm24(t));
            }
            return list;
        }

        private static string StayCheck(SiteInfo site, DateTime? from, string inTime, DateTime? to)
        {
            if (from == null)
            {
                return "Choose a check-in date.";
            }
            if (to == null)
            {
                return "Choose a check-out date.";
            }
            TimeSpan? t = Ui.ParseHm(inTime);
            if (t == null || !InTimes(site).Contains(inTime))
            {
                return "Choose a check-in time.";
            }
            if (to.Value <= from.Value)
            {
                return "Check-out must be at least one day after check-in.";
            }
            int max = Math.Clamp(site.Settings.StayMaxNights, 1, 365);
            if (Nights(from.Value, to.Value) > max)
            {
                return "Stays can be up to " + max + " nights online. For a longer stay, please call us.";
            }
            DateTime now = App.Now(site.S);
            if (from.Value.Add(t.Value) < now.AddHours(Math.Max(0, site.Settings.MinNoticeHours)))
            {
                return "Check-in must be at least " + Math.Max(0, site.Settings.MinNoticeHours) + " hours from now.";
            }
            if (from.Value > LastDay(site))
            {
                return "We take online bookings up to " + Math.Clamp(site.Settings.BookAheadDays, 1, 365) + " days ahead.";
            }
            (TimeSpan From, TimeSpan To)? c1 = Clinic(site, from.Value);
            if (c1 == null)
            {
                return "We are closed on " + ClosedWhy(site, from.Value) + ", so check-in can't be that day.";
            }
            if (t.Value < c1.Value.From || t.Value >= c1.Value.To)
            {
                return "On " + from.Value.ToString("dddd", Ui.Inv) + " we are open " + Hm(site, DateTime.Today.Add(c1.Value.From)) + " to " + Hm(site, DateTime.Today.Add(c1.Value.To)) + ". Please pick a check-in time in those hours.";
            }
            if (Clinic(site, to.Value) == null)
            {
                return "We are closed on " + ClosedWhy(site, to.Value) + ", so check-out can't be that day.";
            }
            return string.Empty;
        }

        private static string ClosedWhy(SiteInfo site, DateTime d)
        {
            Holiday? h = site.Holidays.FirstOrDefault(x => x.Date.Date == d.Date);
            return Day(d, false) + (h != null ? " (" + h.Name + ")" : string.Empty);
        }

        public async Task<ApiResponse> StayForm()
        {
            ApiResponse response = new ApiResponse();
            MaAppt? s = await Reach(true);
            SiteInfo site = await Site.Load();
            if (s == null || !CanStay(site, s))
            {
                Dead(response, "This stay can no longer be changed online.");
                return response;
            }
            DateTime today = App.Now(site.S).Date;
            string cur = Hm24(s.StartAt.TimeOfDay);
            List<(string, string)> opts = InTimes(site).Select(x => (x, HmText(site, x))).ToList();
            response.SetElementContents("ma-move", "<div class=\"ma-mv\" id=\"ma-mv\"><div class=\"ma-mvh\"><h3>Choose new dates</h3><button type=\"button\" class=\"ma-btn ma-btn-sm ma-btn-ghost\" onclick=\"MyAppointmentJs.closeMove()\">Keep my dates</button></div><div class=\"ma-sform\"><label class=\"ma-field\"><span>Check-in date</span><input type=\"date\" id=\"ma-sfrom\" min=\"" + Ui.Ymd(today) + "\" max=\"" + Ui.Ymd(LastDay(site)) + "\" value=\"" + Ui.Ymd(s.StartAt) + "\"></label><label class=\"ma-field\"><span>Check-in time</span><select id=\"ma-sin\">" + Ui.Opts(opts, cur) + "</select></label><label class=\"ma-field\"><span>Check-out date</span><input type=\"date\" id=\"ma-sto\" min=\"" + Ui.Ymd(today.AddDays(1)) + "\" value=\"" + Ui.Ymd(s.EndAt) + "\"></label></div><p class=\"ma-hint\">Check-out is by " + HmText(site, site.Settings.CheckOutBy) + ". Check-in and check-out must be on days we are open.</p><em class=\"ma-err\" id=\"ma-e-stay\" role=\"alert\"></em><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.stayMove(0)\">Check these dates</button></div></div>");
            response.ExecuteScript("MyAppointmentJs.moving();");
            return response;
        }

        private static async Task<(string Room, string Error)> StayRoom(SiteInfo site, MaAppt s, DateTime from, DateTime to)
        {
            DateTime today = App.Now(site.S).Date;
            var (busy, berr) = await Helper.GetListT<MaBusy>(BusyCond, new List<SqlParameter> { App.PDate("din", from.Date), App.PDate("dout", to.Date), App.PDate("today", today), App.P("ign", s.ApptId, 50), App.P("ivis", string.Empty, 50), App.PBit("inst", false) });
            var (rooms, rerr) = await Helper.GetListT<MaRoom>("SELECT ROOM_ID AS RoomId, ROOM_ORDER AS RoomOrder, ROOM_NAME AS RoomName FROM ROOM WHERE ROOMTYPE_ID = @rt AND ACTIVE = 1 ORDER BY ROOM_ORDER, ROOM_NAME", new List<SqlParameter> { App.P("rt", s.RoomtypeId, 50) });
            if (berr != string.Empty || rerr != string.Empty || busy == null || rooms == null)
            {
                return (string.Empty, "Those dates could not be checked. Please try again.");
            }
            HashSet<string> taken = busy.Select(x => x.RoomId).ToHashSet();
            List<MaRoom> free = rooms.Where(r => !taken.Contains(r.RoomId)).ToList();
            if (free.Count == 0)
            {
                return (string.Empty, "Sorry, the " + (s.TypeName != string.Empty ? s.TypeName.ToLowerInvariant() : "room") + " is full on some of those nights. Please try other dates.");
            }
            return (free.Any(r => r.RoomId == s.RoomId) ? s.RoomId : free[0].RoomId, string.Empty);
        }

        public async Task<ApiResponse> StayMove()
        {
            ApiResponse response = new ApiResponse();
            DateTime? from = Ui.ParseDay(V("sfrom", 12));
            DateTime? to = Ui.ParseDay(V("sto", 12));
            string inTime = V("sin", 5);
            bool go = V("go", 2) == "1";
            MaAppt? s = await Reach(true);
            SiteInfo site = await Site.Load();
            if (s == null || !CanStay(site, s))
            {
                Dead(response, "This stay can no longer be changed online.");
                return response;
            }
            string err = StayCheck(site, from, inTime, to);
            string room = string.Empty;
            if (err == string.Empty)
            {
                (room, err) = await StayRoom(site, s, from!.Value, to!.Value);
            }
            if (err != string.Empty)
            {
                response.SetElementContents("ma-e-stay", Ui.Enc(err));
                if (go)
                {
                    Dead(response, "Sorry, those dates are no longer free.");
                }
                return response;
            }
            DateTime st = from!.Value.Add(Ui.ParseHm(inTime)!.Value);
            DateTime en = to!.Value.Add(OutBy(site));
            int nights = Nights(st, en);
            decimal price = await StayPrice(site, s, nights);
            if (!go)
            {
                response.SetElementContents("ma-move", "<div class=\"ma-mv ma-mvok\" id=\"ma-mv\" data-from=\"" + Ui.Ymd(from.Value) + "\" data-in=\"" + Ui.Attr(inTime) + "\" data-to=\"" + Ui.Ymd(to.Value) + "\"><h3>Change your stay?</h3><p><s>" + Day(s.StartAt, false) + " to " + Day(s.EndAt, false) + " &middot; " + Ui.Plural(Nights(s.StartAt, s.EndAt), "night", "nights") + (site.Settings.ShowPrices ? " &middot; " + Cash(s.EstPrice) : string.Empty) + "</s><br><b>" + Day(from.Value, false) + " at " + HmText(site, inTime) + " to " + Day(to.Value, false) + " &middot; " + Ui.Plural(nights, "night", "nights") + (site.Settings.ShowPrices ? " &middot; " + Cash(price) : string.Empty) + "</b></p><em class=\"ma-err\" id=\"ma-e-stay\" role=\"alert\"></em><div class=\"ma-aacts\"><button type=\"button\" class=\"ma-btn ma-btn-accent\" onclick=\"MyAppointmentJs.stayMove(1)\">Yes, change my stay</button><button type=\"button\" class=\"ma-btn ma-btn-ghost\" onclick=\"MyAppointmentJs.stayDates()\">Pick other dates</button></div></div>");
                response.ExecuteScript("MyAppointmentJs.moving();");
                return response;
            }
            bool vx = (await Missing(site, s, en)).Count > 0;
            string text = "Dates changed online from " + Day(s.StartAt, false) + " – " + Day(s.EndAt, false) + " to " + Day(st, false) + " – " + Day(en, false);
            DateTime today = App.Now(site.S).Date;
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("aid", s.ApptId, 50), App.P("pid", s.PetId, 50), App.P("room", room, 50), App.PDate("st", st), App.PDate("en", en), App.PDate("din", st.Date), App.PDate("dout", en.Date), App.PDate("today", today),
                App.P("ign", s.ApptId, 50), App.P("ivis", string.Empty, 50), App.PBit("inst", false), App.PDec("price", price), App.PBit("vx", vx), App.P("lid", App.NewId(), 50), App.P("ltext", Ui.Clip(text, 300), 300)
            };
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE PET SET SYSDTE = SYSDTE WHERE PET_ID = @pid",
                "UPDATE ROOM SET SYSDTE = SYSDTE WHERE ROOM_ID = @room",
                RoomGuard,
                PetGuard,
                "UPDATE APPOINTMENT SET ROOM_ID = @room, START_AT = @st, END_AT = @en, EST_PRICE = @price, VAX_PENDING = @vx, STATUS = N'booked', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;",
                LogSql
            }, ps);
            if (Helper.IsError(r))
            {
                await App.Log("web", "change stay online", s.ApptCode + " · failed", false);
                string why = r.Contains("ROOMBUSY", StringComparison.OrdinalIgnoreCase) ? "Sorry, that room was just taken for those nights. Please try other dates." : r.Contains("PETBUSY", StringComparison.OrdinalIgnoreCase) ? "This pet already has another stay on some of these nights. Please pick other dates or call us." : "This stay was just changed. Please look it up again.";
                response.SetElementContents("ma-e-stay", Ui.Enc(why));
                Dead(response, why);
                return response;
            }
            await App.Log("web", "change stay online", s.ApptCode + " · " + Day(st, false) + " to " + Day(en, false), true);
            MaAppt? fresh = await ByCode(s.ApptCode);
            response.SetElementContents("ma-res", await StayCard(site, fresh ?? s));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Done. Your stay has new dates.');");
            return response;
        }

        public async Task<ApiResponse> CancelStay()
        {
            ApiResponse response = new ApiResponse();
            MaAppt? s = await Reach(true);
            SiteInfo site = await Site.Load();
            if (s == null || !CanStay(site, s))
            {
                Dead(response, "This stay can no longer be cancelled online. Please call us.");
                return response;
            }
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = N'Cancelled online by the owner', SYSDTE = GETDATE() WHERE APPT_ID = @aid AND VISIT_TYPE = N'boarding' AND STATUS IN (N'booked', N'confirmed') AND VISIT_ID = N''; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;",
                LogSql
            }, new List<SqlParameter> { App.P("aid", s.ApptId, 50), App.P("lid", App.NewId(), 50), App.P("ltext", "Cancelled online by the owner", 300) });
            if (Helper.IsError(r))
            {
                await App.Log("web", "cancel stay online", s.ApptCode + " · failed", false);
                Dead(response, "This stay was just changed. Please look it up again.");
                return response;
            }
            await App.Log("web", "cancel stay online", s.ApptCode + " · " + s.PetName, true);
            MaAppt? fresh = await ByCode(s.ApptCode);
            response.SetElementContents("ma-res", await StayCard(site, fresh ?? s));
            response.ExecuteScript("MyAppointmentJs.found();MyAppointmentJs.toast('Your stay has been cancelled.');");
            return response;
        }

        private static string Utc(SiteInfo site, DateTime local)
        {
            DateTime u;
            try
            {
                u = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), App.Zone(site.Settings.TimeZone));
            }
            catch (ArgumentException)
            {
                u = DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
            return u.ToString("yyyyMMdd'T'HHmmss'Z'", Ui.Inv);
        }

        private static Dictionary<string, string> Ev(SiteInfo site, DateTime s, DateTime e, string title, string desc, string uid)
        {
            return new Dictionary<string, string> { ["s"] = Utc(site, s), ["e"] = Utc(site, e), ["t"] = title, ["d"] = desc, ["u"] = uid };
        }

        private static string IcsAttrs(SiteInfo site, MaAppt a, CareService? sv)
        {
            string pn = a.PetName != string.Empty ? a.PetName : "Pet";
            string name = a.BookName != string.Empty ? a.BookName : "Visit";
            List<Dictionary<string, string>> ev = new List<Dictionary<string, string>>();
            if (a.ProcAt.HasValue)
            {
                DateTime pick = a.PickupAt ?? a.StartAt.Date.AddHours(15);
                ev.Add(Ev(site, a.StartAt, a.EndAt, "Drop off " + pn + ": " + name + " | " + site.Settings.Name, "Booking code " + a.ApptCode + ". " + string.Join(" ", sv?.PreOp ?? new List<string>()), a.ApptCode + "-in"));
                ev.Add(Ev(site, pick, pick.AddMinutes(30), "Pick up " + pn + " | " + site.Settings.Name, "Booking code " + a.ApptCode + ". We call you when " + pn + " is ready.", a.ApptCode + "-out"));
            }
            else
            {
                ev.Add(Ev(site, a.StartAt, a.EndAt, pn + ": " + name + (a.StaffName != string.Empty ? " with " + a.StaffName : string.Empty) + " | " + site.Settings.Name, "Booking code " + a.ApptCode, a.ApptCode));
            }
            return " data-code=\"" + Ui.Attr(a.ApptCode) + "\" data-loc=\"" + Ui.Attr(site.Settings.Name + ", " + site.Settings.Address) + "\" data-ev=\"" + Ui.Attr(JsonSerializer.Serialize(ev)) + "\"";
        }

        private static string StayIcs(SiteInfo site, MaAppt s, string run)
        {
            string pn = s.PetName != string.Empty ? s.PetName : "Pet";
            List<Dictionary<string, string>> ev = new List<Dictionary<string, string>>
            {
                Ev(site, s.StartAt, s.StartAt.AddMinutes(30), "Check in " + pn + " for boarding | " + site.Settings.Name, "Booking code " + s.ApptCode + ". " + run, s.ApptCode + "-in"),
                Ev(site, s.EndAt.AddMinutes(-30), s.EndAt, "Pick up " + pn + " from boarding | " + site.Settings.Name, "Booking code " + s.ApptCode + ". Check-out by " + Hm(site, s.EndAt) + ".", s.ApptCode + "-out")
            };
            return " data-code=\"" + Ui.Attr(s.ApptCode) + "\" data-loc=\"" + Ui.Attr(site.Settings.Name + ", " + site.Settings.Address) + "\" data-ev=\"" + Ui.Attr(JsonSerializer.Serialize(ev)) + "\"";
        }
    }
}
