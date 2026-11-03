using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminReminders : WebPage
    {
        private const string Key = "reminders";
        private const string PetCols = "P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.PHOTO AS Photo, P.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.EMAIL, N'') AS Email, ISNULL(C.CONTACT_PREF, N'phone') AS Pref";
        private const string Booked = "(SELECT MIN(A.START_AT) FROM APPOINTMENT A WHERE A.PET_ID = P.PET_ID AND A.START_AT >= @now AND A.STATUS IN (N'booked', N'confirmed') AND A.VISIT_TYPE IN (N'clinic', N'surgery')) AS BookedAt";
        private const string LastCols = "L.CONTACTED_UTC AS LastUtc, ISNULL(L.HOW, N'') AS LastHow, ISNULL(L.NOTE, N'') AS LastNote, ISNULL(L.STAFF_NAME, N'') AS LastBy";
        private const string PetJoins = " JOIN PET P ON P.PET_ID = X.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY";
        private const string Live = "P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL";
        private const string RecheckText = "LEFT(CASE WHEN X.REASON = N'' OR X.REASON LIKE N'Recheck%' THEN N'Recheck' ELSE N'Recheck: ' + X.REASON END, 100)";
        private static readonly string[] Hows = { "phone", "email", "text", "letter" };
        private static readonly string[] Outcomes = { "Talked to the owner", "Left a message", "Sent", "No answer", "Booked a visit", "Owner will call back", "Owner declined", "Wrong number or bounced" };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Reminders | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--rm-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("rm"))
                .Replace("{plhd_open}", string.Empty);
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminRemindersJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminRemindersJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"rm-panel rm-deny\"><h1>Reminders</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"rm-btn rm-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminRemindersJs.out();");
            return response;
        }

        private sealed class Filter
        {
            public string Kind = string.Empty;
            public bool Late;
            public int Quiet;
            public string Term = string.Empty;
            public string Page = string.Empty;
        }

        private Filter Read()
        {
            Filter f = new Filter();
            string kind = (GetDataValue("kind") ?? string.Empty).Trim();
            f.Kind = kind == "vaccine" || kind == "recheck" || kind == "wellness" ? kind : string.Empty;
            f.Late = (GetDataValue("show") ?? string.Empty).Trim() == "overdue";
            int.TryParse(GetDataValue("quiet"), out int q);
            f.Quiet = q == 7 || q == 14 || q == 30 || q == 60 ? q : 0;
            f.Term = Ui.OneLine(GetDataValue("term"), 80);
            f.Page = Ui.Clip(GetDataValue("page"), 6);
            return f;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("rm-menu", Auth.Menu("rm", "AdminRemindersJs", "AdminReminders", c));
            response.SetElementContents("rm-who", Auth.Who("rm", c));
            response.ExecuteScript("AdminRemindersJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rm-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            int days = Math.Max(1, App.SInt(s, "remind.days", 30));
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rm-ptop\"><div><h1>Reminders</h1><p>Pets due for vaccines, rechecks and wellness exams, overdue or due in the next " + Ui.Plural(days, "day", "days") + ". One call covers every pet in the family.</p></div><div class=\"rm-acts\"><button type=\"button\" class=\"rm-btn rm-btn-ghost\" onclick=\"AdminRemindersJs.csv()\">Download list (CSV)</button></div></div>");
            sb.Append("<div class=\"rm-tabs\" role=\"tablist\"><button type=\"button\" class=\"rm-tab rm-on\" role=\"tab\" aria-selected=\"true\" data-tab=\"due\" onclick=\"AdminRemindersJs.tab('due')\">Due list</button><button type=\"button\" class=\"rm-tab\" role=\"tab\" aria-selected=\"false\" data-tab=\"log\" onclick=\"AdminRemindersJs.tab('log')\">Recent contacts</button></div>");
            sb.Append("<section class=\"rm-pane\" id=\"rm-pane-due\"><div class=\"rm-bar\">");
            sb.Append("<label class=\"rm-field rm-grow\"><span>Search</span><input type=\"search\" id=\"rm-term\" maxlength=\"80\" placeholder=\"Pet, owner, phone, email or what is due\" autocomplete=\"off\" oninput=\"AdminRemindersJs.typed()\"></label>");
            sb.Append("<label class=\"rm-field\"><span>What is due</span><select id=\"rm-kind\" onchange=\"AdminRemindersJs.list()\"><option value=\"\">Everything</option><option value=\"vaccine\">Vaccines</option><option value=\"recheck\">Rechecks</option><option value=\"wellness\">Wellness exams</option></select></label>");
            sb.Append("<label class=\"rm-field\"><span>Show</span><select id=\"rm-show\" onchange=\"AdminRemindersJs.list()\"><option value=\"\">Overdue and due soon</option><option value=\"overdue\">Overdue only</option></select></label>");
            sb.Append("<label class=\"rm-field\"><span>Not contacted in</span><select id=\"rm-quiet\" onchange=\"AdminRemindersJs.list()\"><option value=\"\">Any time</option><option value=\"7\">Last 7 days</option><option value=\"14\">Last 14 days</option><option value=\"30\">Last 30 days</option><option value=\"60\">Last 60 days</option></select></label></div>");
            sb.Append("<div id=\"rm-list\">" + await ListHtml(c, s, new Filter()) + "</div></section>");
            sb.Append("<section class=\"rm-pane rm-pane-off\" id=\"rm-pane-log\"><div id=\"rm-log\"><p class=\"rm-wait\">Loading&hellip;</p></div></section>");
            if (c.Can(Key, 'E'))
            {
                sb.Append("<div class=\"rm-bulk\" id=\"rm-bulk\" hidden><b id=\"rm-bulkn\">0 selected</b><span class=\"rm-acts\"><button type=\"button\" class=\"rm-btn rm-btn-sm rm-btn-accent\" onclick=\"AdminRemindersJs.logSel()\">Log contact for selected</button><button type=\"button\" class=\"rm-btn rm-btn-sm rm-btn-ghost rm-btn-inv\" onclick=\"AdminRemindersJs.clearSel()\">Clear</button></span></div>");
            }
            response.SetElementContents("rm-main", sb.ToString());
            response.ExecuteScript("AdminRemindersJs.ready();");
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
                response.SetElementContents("rm-main", Denied());
                return response;
            }
            response.SetElementContents("rm-list", await ListHtml(c, await App.Settings(), Read()));
            response.ExecuteScript("AdminRemindersJs.ready();");
            return response;
        }

        private static async Task<List<RmDue>> Due(Dictionary<string, string> s)
        {
            DateTime now = App.Now(s);
            DateTime soon = now.Date.AddDays(Math.Max(1, App.SInt(s, "remind.days", 30)));
            int months = Math.Clamp(App.SInt(s, "wellness.months", 12), 1, 60);
            List<SqlParameter> P() => new List<SqlParameter> { App.PDate("now", now), App.PDate("soon", soon), App.PInt("wm", months) };
            string last = " OUTER APPLY (SELECT TOP 1 R.CONTACTED_UTC, R.HOW, R.NOTE, ISNULL(T.STAFF_NAME, N'') AS STAFF_NAME FROM REMINDER R LEFT JOIN STAFF T ON T.STAFF_ID = R.STAFF_ID WHERE R.PET_ID = P.PET_ID AND R.ITEM_TEXT = {0} AND R.DUE_DATE = {1} ORDER BY R.CONTACTED_UTC DESC) L";
            string vax = "SELECT N'vaccine' AS Kind, X.VAX_ID AS RefId, " + PetCols + ", X.VAX_NAME AS ItemText, X.DUE_DATE AS DueDate, " + Booked + ", " + LastCols + " FROM PETVACCINE X" + PetJoins + string.Format(last, "X.VAX_NAME", "X.DUE_DATE") + " WHERE " + Live + " AND X.DUE_DATE <= @soon AND NOT EXISTS (SELECT 1 FROM PETVACCINE W WHERE W.PET_ID = X.PET_ID AND W.VAX_NAME = X.VAX_NAME AND (W.GIVEN_DATE > X.GIVEN_DATE OR (W.GIVEN_DATE = X.GIVEN_DATE AND W.VAX_ID > X.VAX_ID)))";
            string rc = "SELECT N'recheck' AS Kind, X.VISIT_ID AS RefId, " + PetCols + ", " + RecheckText + " AS ItemText, X.RECHECK_DATE AS DueDate, " + Booked + ", " + LastCols + " FROM VISIT X" + PetJoins + string.Format(last, RecheckText, "X.RECHECK_DATE") + " WHERE " + Live + " AND X.STATUS = N'closed' AND X.RECHECK_DATE IS NOT NULL AND X.RECHECK_DATE <= @soon AND NOT EXISTS (SELECT 1 FROM VISIT Z WHERE Z.PET_ID = X.PET_ID AND Z.STATUS <> N'cancelled' AND Z.VISIT_TYPE IN (N'clinic', N'surgery') AND (Z.CHECKIN_AT > X.CHECKIN_AT OR (Z.CHECKIN_AT = X.CHECKIN_AT AND Z.VISIT_ID > X.VISIT_ID)))";
            string well = "SELECT N'wellness' AS Kind, X.PET_ID AS RefId, " + PetCols + ", N'Wellness exam' AS ItemText, CAST(DATEADD(MONTH, @wm, Q.LastAt) AS DATE) AS DueDate, " + Booked + ", " + LastCols + " FROM PET X" + PetJoins + " CROSS APPLY (SELECT MAX(V.CHECKIN_AT) AS LastAt FROM VISIT V LEFT JOIN APPOINTMENT A ON A.APPT_ID = V.APPT_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = A.BOOKTYPE_ID WHERE V.PET_ID = X.PET_ID AND V.VISIT_TYPE = N'clinic' AND V.STATUS <> N'cancelled' AND (B.BOOK_NAME LIKE N'%wellness%' OR B.BOOK_NAME LIKE N'%senior%' OR B.BOOK_NAME LIKE N'%puppy%' OR V.REASON LIKE N'%wellness%' OR V.REASON LIKE N'%exam%')) Q" + string.Format(last, "N'Wellness exam'", "CAST(DATEADD(MONTH, @wm, Q.LastAt) AS DATE)") + " WHERE " + Live + " AND P.SPECIES_KEY IN (N'dog', N'cat') AND Q.LastAt IS NOT NULL AND CAST(DATEADD(MONTH, @wm, Q.LastAt) AS DATE) <= @soon";
            List<RmDue> all = new List<RmDue>();
            foreach (string sql in new[] { vax, rc, well })
            {
                var (rows, emsg) = await Helper.GetListT<RmDue>(sql, P());
                if (emsg == string.Empty && rows != null)
                {
                    all.AddRange(rows);
                }
            }
            return all;
        }

        private static string RowKey(RmDue r)
        {
            return r.Kind.Substring(0, 1) + ":" + r.RefId;
        }

        private static string Owner(RmDue r)
        {
            string n = (r.FirstName + " " + r.LastName).Trim();
            return n != string.Empty ? n : "No owner on file";
        }

        private static string KindName(string k)
        {
            return k switch
            {
                "vaccine" => "Vaccine",
                "recheck" => "Recheck",
                "wellness" => "Wellness",
                _ => k
            };
        }

        private static string HowName(string h)
        {
            return h switch
            {
                "phone" => "Phone call",
                "email" => "Email",
                "text" => "Text message",
                "letter" => "Letter",
                _ => h
            };
        }

        private static string PrefName(string p)
        {
            return p switch
            {
                "email" => "Prefers email",
                "text" => "Prefers text",
                _ => "Prefers a call"
            };
        }

        private static string Short(DateTime d)
        {
            return d.ToString("MMM d, yyyy", Ui.Us);
        }

        private static string DueWords(DateTime due, DateTime today)
        {
            int d = (int)(due.Date - today).TotalDays;
            if (d < 0)
            {
                return Ui.Plural(-d, "day", "days") + " overdue";
            }
            if (d == 0)
            {
                return "Due today";
            }
            return "Due in " + Ui.Plural(d, "day", "days");
        }

        private static string Sentence(RmDue r)
        {
            return r.Kind switch
            {
                "vaccine" => r.PetName + " is due for the " + r.ItemText + " vaccine (due " + Short(r.DueDate) + ").",
                "recheck" => r.PetName + " is due for a recheck visit" + (r.ItemText.StartsWith("Recheck: ", StringComparison.Ordinal) ? " after the " + r.ItemText.Substring(9).ToLowerInvariant() : string.Empty) + " (by " + Short(r.DueDate) + ").",
                _ => r.PetName + " is due for a wellness exam (due " + Short(r.DueDate) + ")."
            };
        }

        private static string MailTo(List<RmDue> rows, Dictionary<string, string> s)
        {
            RmDue f = rows[0];
            string clinic = App.S(s, "name", "Animal Hospital");
            List<string> pets = rows.Select(r => r.PetName).Distinct().ToList();
            string names = pets.Count == 1 ? pets[0] : string.Join(", ", pets.Take(pets.Count - 1)) + " and " + pets[^1];
            string subject = "A friendly reminder for " + names + " from " + clinic;
            StringBuilder body = new StringBuilder();
            body.Append("Hello " + (f.FirstName != string.Empty ? f.FirstName : "there") + ",\n\nThis is a friendly reminder from " + clinic + ":\n\n");
            foreach (RmDue r in rows)
            {
                body.Append("- " + Sentence(r) + "\n");
            }
            string phone = App.S(s, "phone");
            body.Append("\nPlease " + (phone != string.Empty ? "call us at " + phone + " or " : string.Empty) + "reply to this email and we will find a time that suits you.\n\nThank you,\n" + clinic);
            if (!Ui.Email(f.Email) || Regex.IsMatch(f.Email, "[?&=#%\\s<>\"]"))
            {
                return string.Empty;
            }
            return "mailto:" + Uri.EscapeDataString(f.Email).Replace("%40", "@") + "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(body.ToString());
        }

        private static bool Match(RmDue r, string term)
        {
            if (term == string.Empty)
            {
                return true;
            }
            string t = term.ToLowerInvariant();
            string digits = Ui.Digits(term);
            if ((r.PetName + " " + r.FirstName + " " + r.LastName + " " + r.Email + " " + r.ItemText + " " + KindName(r.Kind)).ToLowerInvariant().Contains(t))
            {
                return true;
            }
            return digits.Length >= 3 && Ui.Digits(r.Phone).Contains(digits);
        }

        private static string CsvLine(RmDue r, DateTime today, Dictionary<string, string> s)
        {
            string last = r.LastUtc.HasValue ? App.Local(s, r.LastUtc.Value).ToString("yyyy-MM-dd HH:mm", Ui.Inv) + " " + HowName(r.LastHow) + (r.LastBy != string.Empty ? " by " + r.LastBy : string.Empty) : string.Empty;
            string[] cells =
            {
                Owner(r), PrefName(r.Pref), r.Phone, r.Email, r.PetName, r.SpeciesName, r.ItemText, KindName(r.Kind),
                r.DueDate.ToString("yyyy-MM-dd", Ui.Inv), r.DueDate.Date < today ? "Overdue" : "Due soon", last, r.LastNote
            };
            return string.Join(",", cells.Select(Ui.Csv));
        }

        private static async Task<string> ListHtml(Ctx c, Dictionary<string, string> s, Filter f)
        {
            DateTime today = App.Now(s).Date;
            bool h24 = App.S(s, "clock24") == "1";
            List<RmDue> all = await Due(s);
            List<RmDue> rows = all.Where(r => (f.Kind == string.Empty || r.Kind == f.Kind) && (!f.Late || r.DueDate.Date < today) && Match(r, f.Term)).ToList();
            List<RmDue> booked = rows.Where(r => r.BookedAt.HasValue).OrderBy(r => r.BookedAt).ToList();
            rows = rows.Where(r => !r.BookedAt.HasValue).ToList();
            int quieted = 0;
            if (f.Quiet > 0)
            {
                DateTime cut = DateTime.UtcNow.AddDays(-f.Quiet);
                quieted = rows.Count(r => r.LastUtc.HasValue && r.LastUtc.Value >= cut);
                rows = rows.Where(r => !r.LastUtc.HasValue || r.LastUtc.Value < cut).ToList();
            }
            bool edit = c.Can(Key, 'E');
            bool book = c.Can("appts", 'A');
            bool chart = c.Can("charts", 'V');
            bool owners = c.Can("clients", 'V');
            var groups = rows.GroupBy(r => r.ClientId != string.Empty ? r.ClientId : "pet:" + r.PetId)
                .Select(g => g.OrderBy(r => r.DueDate).ThenBy(r => r.PetName).ToList())
                .OrderBy(g => g[0].DueDate).ThenBy(g => g[0].LastName).ThenBy(g => g[0].FirstName)
                .ToList();
            int late = rows.Count(r => r.DueDate.Date < today);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rm-sum\"><p>");
            if (rows.Count == 0)
            {
                sb.Append("Nobody to contact with these filters.");
            }
            else
            {
                sb.Append("<b>" + Ui.Plural(rows.Count, "reminder", "reminders") + "</b> for " + Ui.Plural(groups.Count, "family", "families") + (late > 0 ? " &middot; <b class=\"rm-late\">" + late.ToString(Ui.Inv) + " overdue</b>" : string.Empty));
            }
            if (quieted > 0)
            {
                sb.Append(" &middot; " + Ui.Plural(quieted, "reminder", "reminders") + " hidden because the owner was contacted lately");
            }
            sb.Append("</p>");
            if (booked.Count > 0)
            {
                sb.Append("<details class=\"rm-booked\"><summary>" + booked.Count.ToString(Ui.Inv) + " already booked</summary><ul>");
                foreach (RmDue r in booked)
                {
                    sb.Append("<li><b>" + Ui.Enc(r.PetName) + "</b> <span class=\"rm-dim\">" + Ui.Enc(Owner(r)) + "</span><span>" + Ui.Enc(r.ItemText) + " &middot; due " + Short(r.DueDate) + "</span><span class=\"rm-st rm-st-confirmed\">Booked " + Ui.Enc(r.BookedAt!.Value.ToString("ddd MMM d", Ui.Us)) + " " + Ui.Hm(r.BookedAt.Value, h24) + "</span></li>");
                }
                sb.Append("</ul></details>");
            }
            sb.Append("</div>");
            if (rows.Count == 0)
            {
                sb.Append("<div class=\"rm-panel rm-empty\"><p>" + (f.Kind == string.Empty && !f.Late && f.Quiet == 0 && f.Term == string.Empty ? "Everyone is up to date. Nothing is overdue or due soon without a booking." : "No reminders match these filters.") + "</p></div>");
                return sb.ToString();
            }
            int per = 25;
            int pg = Ui.PageOf(f.Page, per, groups.Count);
            sb.Append("<div id=\"rm-csvall\" hidden>" + string.Concat(groups.SelectMany(x => x).Select(r => "<i data-csv=\"" + Ui.Attr(CsvLine(r, today, s)) + "\"></i>")) + "</div>");
            foreach (List<RmDue> g in groups.Skip((pg - 1) * per).Take(per))
            {
                RmDue o = g[0];
                string owner = Ui.Enc(Owner(o));
                sb.Append("<section class=\"rm-grp\"><header class=\"rm-gh\">");
                if (edit)
                {
                    sb.Append("<label class=\"rm-ck\"><input type=\"checkbox\" class=\"rm-gsel\" onchange=\"AdminRemindersJs.grp(this)\" aria-label=\"Select every reminder for " + Ui.Attr(Owner(o)) + "\"></label>");
                }
                sb.Append("<div class=\"rm-gwho\"><b>" + (owners && o.ClientId != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(o.ClientId) + "\">" + owner + "</a>" : owner) + "</b><span class=\"rm-pref rm-pref-" + Ui.Attr(o.Pref) + "\">" + PrefName(o.Pref) + "</span><small>");
                List<string> bits = new List<string>();
                if (o.Phone != string.Empty)
                {
                    bits.Add("<a href=\"tel:" + Ui.Attr(Ui.Digits(o.Phone)) + "\">" + Ui.Enc(o.Phone) + "</a>");
                }
                string mail = o.Email != string.Empty ? MailTo(g, s) : string.Empty;
                if (o.Email != string.Empty)
                {
                    bits.Add(mail != string.Empty ? "<a class=\"rm-mail\" href=\"" + Ui.Attr(mail) + "\">" + Ui.Enc(o.Email) + "</a>" : Ui.Enc(o.Email));
                }
                sb.Append(bits.Count > 0 ? string.Join(" &middot; ", bits) : "<i class=\"rm-dim\">No phone or email on file</i>");
                sb.Append("</small></div><div class=\"rm-acts\">");
                if (o.Phone != string.Empty)
                {
                    sb.Append("<a class=\"rm-btn rm-btn-sm rm-btn-ghost\" href=\"tel:" + Ui.Attr(Ui.Digits(o.Phone)) + "\">Call</a>");
                }
                if (mail != string.Empty)
                {
                    sb.Append("<a class=\"rm-btn rm-btn-sm rm-btn-ghost\" href=\"" + Ui.Attr(mail) + "\">Email</a>");
                }
                if (edit)
                {
                    sb.Append("<button type=\"button\" class=\"rm-btn rm-btn-sm\" onclick=\"AdminRemindersJs.logGroup(this)\">Log contact" + (g.Count > 1 ? " for all " + g.Count.ToString(Ui.Inv) : string.Empty) + "</button>");
                }
                sb.Append("</div></header><div class=\"rm-rows\">");
                foreach (RmDue r in g)
                {
                    bool isLate = r.DueDate.Date < today;
                    sb.Append("<div class=\"rm-row" + (edit ? string.Empty : " rm-row-ro") + "\" data-k=\"" + Ui.Attr(RowKey(r)) + "\" data-csv=\"" + Ui.Attr(CsvLine(r, today, s)) + "\">");
                    if (edit)
                    {
                        sb.Append("<label class=\"rm-ck\"><input type=\"checkbox\" class=\"rm-sel\" onchange=\"AdminRemindersJs.sel()\" aria-label=\"Select " + Ui.Attr(r.PetName + " " + r.ItemText) + "\"></label>");
                    }
                    sb.Append("<div class=\"rm-pet\">" + Ui.Avatar("rm", r.PetId, r.PetName, r.Photo) + "<span><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName) + "</small></span></div>");
                    sb.Append("<div class=\"rm-what\"><span class=\"rm-kind rm-kind-" + Ui.Attr(r.Kind) + "\">" + KindName(r.Kind) + "</span><b>" + Ui.Enc(r.ItemText) + "</b></div>");
                    sb.Append("<div class=\"rm-due" + (isLate ? " rm-late" : string.Empty) + "\"><b>" + Short(r.DueDate) + "</b><small>" + DueWords(r.DueDate, today) + "</small></div>");
                    sb.Append("<div class=\"rm-last\">");
                    if (r.LastUtc.HasValue)
                    {
                        sb.Append("<span><b>" + Ui.Enc(HowName(r.LastHow)) + "</b> " + Ui.Time(r.LastUtc.Value, "ago") + (r.LastBy != string.Empty ? " &middot; " + Ui.Enc(r.LastBy) : string.Empty) + "</span>" + (r.LastNote != string.Empty ? "<small>" + Ui.Enc(r.LastNote) + "</small>" : string.Empty));
                    }
                    else
                    {
                        sb.Append("<span class=\"rm-dim\">Not contacted yet</span>");
                    }
                    sb.Append("</div><div class=\"rm-racts\">");
                    if (edit)
                    {
                        sb.Append("<button type=\"button\" class=\"rm-lbtn\" onclick=\"AdminRemindersJs.logOne(this)\">Log</button>");
                    }
                    if (book)
                    {
                        sb.Append("<a class=\"rm-lbtn\" href=\"AdminAppts?p=" + Uri.EscapeDataString(r.PetId) + "\">Book</a>");
                    }
                    if (chart)
                    {
                        sb.Append("<a class=\"rm-lbtn\" href=\"AdminChart?p=" + Uri.EscapeDataString(r.PetId) + "\">Chart</a>");
                    }
                    sb.Append("</div></div>");
                }
                sb.Append("</div></section>");
            }
            sb.Append(Ui.Pager("rm", pg, per, groups.Count, "family", "families", "AdminRemindersJs.page"));
            return sb.ToString();
        }

        private static List<string> Keys(string? raw)
        {
            return (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => Regex.IsMatch(k, "^[vrw]:[A-Za-z0-9_-]{1,50}$"))
                .Distinct()
                .Take(300)
                .ToList();
        }

        private static async Task<List<RmDue>> Picked(Dictionary<string, string> s, List<string> keys)
        {
            Dictionary<string, RmDue> map = new Dictionary<string, RmDue>();
            foreach (RmDue r in await Due(s))
            {
                map[RowKey(r)] = r;
            }
            return keys.Where(map.ContainsKey).Select(k => map[k]).ToList();
        }

        public async Task<ApiResponse> LogForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can look at reminders but not log contacts. Ask a manager if you need to.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<RmDue> items = await Picked(s, Keys(GetDataValue("rkeys")));
            if (items.Count == 0)
            {
                Toast(response, "Those reminders are no longer due. The list has been refreshed.");
                response.SetElementContents("rm-list", await ListHtml(c, s, Read()));
                response.ExecuteScript("AdminRemindersJs.ready();");
                return response;
            }
            int families = items.Select(r => r.ClientId).Distinct().Count();
            List<string> prefs = items.Select(r => r.Pref).Distinct().ToList();
            string how = prefs.Count == 1 && Hows.Contains(prefs[0]) ? prefs[0] : "phone";
            string outcome = how == "phone" ? "Talked to the owner" : "Sent";
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Log contact</h2><p class=\"rm-lead\">" + (items.Count == 1 ? "For this reminder" : "The same contact for these " + items.Count.ToString(Ui.Inv) + " reminders") + (families > 1 ? " (" + Ui.Plural(families, "family", "families") + ")" : string.Empty) + ":</p><ul class=\"rm-litems\">");
            foreach (RmDue r in items.Take(12))
            {
                sb.Append("<li><b>" + Ui.Enc(r.PetName) + "</b> <span>" + Ui.Enc(r.ItemText) + "</span> <small>" + Ui.Enc(Owner(r)) + " &middot; due " + Short(r.DueDate) + "</small></li>");
            }
            if (items.Count > 12)
            {
                sb.Append("<li class=\"rm-dim\">and " + (items.Count - 12).ToString(Ui.Inv) + " more</li>");
            }
            sb.Append("</ul><fieldset class=\"rm-how\"><legend>How did you reach out?</legend><div class=\"rm-hows\">");
            foreach (string h in Hows)
            {
                sb.Append("<label class=\"rm-hopt\"><input type=\"radio\" name=\"rm-how\" value=\"" + h + "\"" + (h == how ? " checked" : string.Empty) + "><span>" + HowName(h) + "</span></label>");
            }
            sb.Append("</div></fieldset><div class=\"rm-fgrid\"><label class=\"rm-field\"><span>What happened</span><select id=\"rm-out\">" + Ui.Opts(Outcomes.Select(x => (x, x)), outcome) + "</select></label>");
            sb.Append("<label class=\"rm-field rm-span2\"><span>Note <small>(optional)</small></span><textarea id=\"rm-note\" rows=\"3\" maxlength=\"240\" placeholder=\"For example: will call back after payday\"></textarea></label></div>");
            sb.Append("<em class=\"rm-err\" id=\"rm-e-log\"></em><div class=\"rm-facts\"><button type=\"button\" class=\"rm-btn rm-btn-accent\" id=\"rm-save\" onclick=\"AdminRemindersJs.save()\">Save contact</button><button type=\"button\" class=\"rm-btn rm-btn-ghost\" onclick=\"AdminRemindersJs.closeModal()\">Cancel</button></div>");
            response.SetElementContents("rm-mbody", sb.ToString());
            response.ExecuteScript("AdminRemindersJs.shown();");
            return response;
        }

        public async Task<ApiResponse> LogSave()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "log reminder contact", "not allowed", false);
                response.SetElementContents("rm-e-log", "You don't have the right to log contacts.");
                response.ExecuteScript("AdminRemindersJs.unlock();");
                return response;
            }
            string how = (GetDataValue("how") ?? string.Empty).Trim();
            string outcome = (GetDataValue("outcome") ?? string.Empty).Trim();
            string note = Ui.OneLine(GetDataValue("note"), 240);
            if (!Hows.Contains(how))
            {
                response.SetElementContents("rm-e-log", "Choose how you reached the owner.");
                response.ExecuteScript("AdminRemindersJs.unlock();");
                return response;
            }
            if (!Outcomes.Contains(outcome))
            {
                response.SetElementContents("rm-e-log", "Choose what happened.");
                response.ExecuteScript("AdminRemindersJs.unlock();");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<RmDue> items = await Picked(s, Keys(GetDataValue("rkeys")));
            if (items.Count == 0)
            {
                response.SetElementContents("rm-e-log", "Those reminders are no longer due. Close this window to see the fresh list.");
                response.ExecuteScript("AdminRemindersJs.unlock();");
                return response;
            }
            string text = Ui.Clip(outcome + (note != string.Empty ? " — " + note : string.Empty), 300);
            List<string> sqls = new List<string>();
            List<SqlParameter> ps = new List<SqlParameter> { App.P("how", how, 10), App.P("note", text, 300), App.P("sid", c.U.StaffId, 50) };
            for (int i = 0; i < items.Count; i++)
            {
                string n = i.ToString(Ui.Inv);
                sqls.Add("INSERT INTO REMINDER (REM_ID, PET_ID, ITEM_TEXT, DUE_DATE, CONTACTED_UTC, HOW, NOTE, STAFF_ID) VALUES (@id" + n + ", @pid" + n + ", @item" + n + ", @due" + n + ", SYSUTCDATETIME(), @how, @note, @sid)");
                ps.Add(App.P("id" + n, App.NewId(), 50));
                ps.Add(App.P("pid" + n, items[i].PetId, 50));
                ps.Add(App.P("item" + n, Ui.Clip(items[i].ItemText, 100), 100));
                ps.Add(new SqlParameter("due" + n, System.Data.SqlDbType.Date) { Value = items[i].DueDate.Date });
            }
            string result = await Helper.PutData(sqls, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "log reminder contact", "failed", false);
                response.SetElementContents("rm-e-log", "The contact could not be saved. Try again in a moment.");
                response.ExecuteScript("AdminRemindersJs.unlock();");
                return response;
            }
            string target = items.Count == 1 ? items[0].PetName + " · " + items[0].ItemText : Ui.Plural(items.Count, "reminder", "reminders");
            await App.Log(c.U.StaffId, "logged reminder contact", target + " · " + HowName(how), true);
            response.SetElementContents("rm-list", await ListHtml(c, s, Read()));
            response.ExecuteScript("AdminRemindersJs.saved();AdminRemindersJs.ready();");
            Toast(response, "Contact logged for " + Ui.Plural(items.Count, "reminder", "reminders") + ".");
            return response;
        }

        public async Task<ApiResponse> Contacts()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rm-main", Denied());
                return response;
            }
            var (rows, emsg) = await Helper.GetListT<RmContact>("SELECT TOP 50 R.REM_ID AS RemId, R.PET_ID AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(P.CLIENT_ID, N'') AS ClientId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, R.ITEM_TEXT AS ItemText, R.DUE_DATE AS DueDate, R.CONTACTED_UTC AS ContactedUtc, R.HOW AS How, R.NOTE AS Note, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM REMINDER R LEFT JOIN PET P ON P.PET_ID = R.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = R.STAFF_ID ORDER BY R.CONTACTED_UTC DESC, R.REM_ID");
            StringBuilder sb = new StringBuilder();
            if (emsg != string.Empty || rows == null)
            {
                sb.Append("<div class=\"rm-panel\"><p>The contact log could not be loaded. Try again in a moment.</p></div>");
            }
            else if (rows.Count == 0)
            {
                sb.Append("<div class=\"rm-panel rm-empty\"><p>No contacts logged yet.</p></div>");
            }
            else
            {
                bool chart = c.Can("charts", 'V');
                bool owners = c.Can("clients", 'V');
                sb.Append("<p class=\"rm-sum\">The last " + Ui.Plural(rows.Count, "contact", "contacts") + " logged, newest first.</p><div class=\"rm-tw\"><table class=\"rm-table\"><thead><tr><th>When</th><th>Pet</th><th>Owner</th><th>What was due</th><th>How</th><th>Note</th><th>By</th></tr></thead><tbody>");
                foreach (RmContact r in rows)
                {
                    string pet = r.PetName != string.Empty ? Ui.Enc(r.PetName) : "<i class=\"rm-dim\">Removed pet</i>";
                    string owner = r.OwnerName != string.Empty ? Ui.Enc(r.OwnerName) : "<i class=\"rm-dim\">&ndash;</i>";
                    sb.Append("<tr><td data-l=\"When\" class=\"rm-nw\">" + Ui.Time(r.ContactedUtc, "full") + "</td>");
                    sb.Append("<td data-l=\"Pet\" class=\"rm-cname\">" + (chart && r.PetName != string.Empty ? "<a class=\"rm-link\" href=\"AdminChart?p=" + Uri.EscapeDataString(r.PetId) + "\">" + pet + "</a>" : pet) + "</td>");
                    sb.Append("<td data-l=\"Owner\">" + (owners && r.ClientId != string.Empty && r.OwnerName != string.Empty ? "<a class=\"rm-link\" href=\"AdminClients?c=" + Uri.EscapeDataString(r.ClientId) + "\">" + owner + "</a>" : owner) + "</td>");
                    sb.Append("<td data-l=\"Due\"><span>" + Ui.Enc(r.ItemText) + " <small class=\"rm-dim\">due " + Short(r.DueDate) + "</small></span></td>");
                    sb.Append("<td data-l=\"How\">" + Ui.Enc(HowName(r.How)) + "</td>");
                    sb.Append("<td data-l=\"Note\">" + (r.Note != string.Empty ? Ui.Enc(r.Note) : "<i class=\"rm-dim\">&ndash;</i>") + "</td>");
                    sb.Append("<td data-l=\"By\">" + (r.StaffName != string.Empty ? Ui.Enc(r.StaffName) : "<i class=\"rm-dim\">&ndash;</i>") + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            response.SetElementContents("rm-log", sb.ToString());
            response.ExecuteScript("AdminRemindersJs.logged();");
            return response;
        }
    }
}
