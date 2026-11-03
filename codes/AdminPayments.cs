using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminPayments : WebPage
    {
        private const string Key = "payments";
        private const int Size = 100;
        private static readonly string[] Methods = { "card", "cash", "check", "other" };
        private const string Joins = " FROM PAYMENT Y LEFT JOIN INVOICE I ON I.INVOICE_ID = Y.INVOICE_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = Y.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN STAFF S ON S.STAFF_ID = Y.STAFF_ID";
        private const string Where = " WHERE Y.PAID_UTC >= @fu AND Y.PAID_UTC < @tu AND (@m = N'' OR Y.METHOD = @m) AND (@sid = N'' OR Y.STAFF_ID = @sid) AND (@t = N'' OR Y.PAY_NO LIKE @tl OR I.INVOICE_NO LIKE @tl OR C.FIRST_NAME LIKE @tl OR C.LAST_NAME LIKE @tl OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @tl OR P.PET_NAME LIKE @tl OR Y.REFERENCE LIKE @tl)";
        private const string RowCols = "Y.PAY_ID AS PayId, Y.PAY_NO AS PayNo, Y.PAID_UTC AS PaidUtc, Y.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, Y.INVOICE_ID AS InvoiceId, ISNULL(I.INVOICE_NO, N'') AS InvoiceNo, ISNULL(P.PET_NAME, N'') AS PetName, Y.METHOD AS Method, Y.REFERENCE AS Reference, Y.AMOUNT AS Amount, ISNULL(S.STAFF_NAME, N'') AS StaffName, Y.IS_VOID AS IsVoid, Y.VOID_REASON AS VoidReason";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Payments | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            DateTime? qd = Ui.ParseDay(QueryValue("d"));
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--py-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("py"))
                .Replace("{plhd_qd}", qd.HasValue ? Ui.Ymd(qd.Value) : string.Empty);
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminPaymentsJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminPaymentsJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"py-panel py-deny\"><h1>Payments</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"py-btn py-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminPaymentsJs.out();");
            return response;
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private static string Like(string t)
        {
            return "%" + t.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private static DateTime ToUtc(Dictionary<string, string> s, DateTime local)
        {
            TimeZoneInfo z = App.Zone(App.S(s, "timezone"));
            DateTime l = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(l, z);
            }
            catch (ArgumentException)
            {
                return DateTime.SpecifyKind(l - z.GetUtcOffset(l), DateTimeKind.Utc);
            }
        }

        private static string MethodName(string m)
        {
            return m switch
            {
                "card" => "Card",
                "cash" => "Cash",
                "check" => "Check",
                _ => "Other"
            };
        }

        private static string Owner(string first, string last)
        {
            string n = (first + " " + last).Trim();
            return n == string.Empty ? "Unknown owner" : n;
        }

        private sealed class Filter
        {
            public DateTime From;
            public DateTime To;
            public string Method = string.Empty;
            public string StaffId = string.Empty;
            public string Term = string.Empty;
            public string Page = string.Empty;
            public bool OneDay => From == To;
        }

        private Filter Read(Dictionary<string, string> s)
        {
            Filter f = new Filter();
            DateTime today = App.Now(s).Date;
            DateTime from = Ui.ParseDay(GetDataValue("from")) ?? today;
            DateTime to = Ui.ParseDay(GetDataValue("to")) ?? from;
            if (to < from)
            {
                (from, to) = (to, from);
            }
            if ((to - from).TotalDays > 366)
            {
                to = from.AddDays(366);
            }
            f.From = from;
            f.To = to;
            string m = (GetDataValue("meth") ?? string.Empty).Trim();
            f.Method = Methods.Contains(m) ? m : string.Empty;
            f.StaffId = CleanId(GetDataValue("staffid"));
            f.Term = Ui.OneLine(GetDataValue("term"), 80);
            f.Page = Ui.Clip(GetDataValue("page"), 6);
            return f;
        }

        private static List<SqlParameter> Ps(Dictionary<string, string> s, Filter f)
        {
            return new List<SqlParameter>
            {
                App.PDate("fu", ToUtc(s, f.From)),
                App.PDate("tu", ToUtc(s, f.To.AddDays(1))),
                App.P("m", f.Method, 20),
                App.P("sid", f.StaffId, 50),
                App.P("t", f.Term, 80),
                App.P("tl", Like(f.Term), 300)
            };
        }

        private static string RangeText(Filter f)
        {
            if (f.OneDay)
            {
                return Ui.LongDay(f.From);
            }
            return Ui.Day(f.From, f.From.Year != f.To.Year) + " to " + Ui.Day(f.To);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("py-menu", Auth.Menu("py", "AdminPaymentsJs", "AdminPayments", c));
            response.SetElementContents("py-who", Auth.Who("py", c));
            response.ExecuteScript("AdminPaymentsJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("py-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Filter f = Read(s);
            var (staff, serr) = await Helper.GetListT<PyStaff>("SELECT S.STAFF_ID AS StaffId, S.STAFF_NAME AS StaffName FROM STAFF S WHERE EXISTS (SELECT 1 FROM PAYMENT Y WHERE Y.STAFF_ID = S.STAFF_ID) ORDER BY S.STAFF_NAME");
            List<PyStaff> people = serr == string.Empty && staff != null ? staff : new List<PyStaff>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"py-ptop\"><div><h1>Payments</h1><p>Every payment taken at the desk or against a bill, by day. Voided payments stay in the list, crossed out, and never count in the totals.</p></div><div class=\"py-acts\">");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<button type=\"button\" class=\"py-btn py-btn-accent\" onclick=\"AdminPaymentsJs.record()\">Record a payment</button>");
            }
            sb.Append("<button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPaymentsJs.csv()\">Download CSV</button></div></div>");
            sb.Append("<div class=\"py-dbar\"><div class=\"py-dnav\"><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm py-arrow\" aria-label=\"Earlier\" onclick=\"AdminPaymentsJs.step(-1)\">&larr;</button>");
            sb.Append("<label class=\"py-field\"><span>From</span><input type=\"date\" id=\"py-from\" value=\"" + Ui.Ymd(f.From) + "\" onchange=\"AdminPaymentsJs.dates('from')\"></label>");
            sb.Append("<label class=\"py-field\"><span>To</span><input type=\"date\" id=\"py-to\" value=\"" + Ui.Ymd(f.To) + "\" onchange=\"AdminPaymentsJs.dates('to')\"></label>");
            sb.Append("<button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm py-arrow\" aria-label=\"Later\" onclick=\"AdminPaymentsJs.step(1)\">&rarr;</button></div>");
            sb.Append("<div class=\"py-quick\"><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\" onclick=\"AdminPaymentsJs.quick('today')\">Today</button><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\" onclick=\"AdminPaymentsJs.quick('week')\">Last 7 days</button><button type=\"button\" class=\"py-btn py-btn-ghost py-btn-sm\" onclick=\"AdminPaymentsJs.quick('month')\">This month</button></div></div>");
            sb.Append("<div class=\"py-bar\"><label class=\"py-field\"><span>Method</span><select id=\"py-meth\" onchange=\"AdminPaymentsJs.list()\"><option value=\"\">All methods</option>" + Ui.Opts(Methods.Select(m => (m, MethodName(m))), string.Empty) + "</select></label>");
            sb.Append("<label class=\"py-field\"><span>Taken by</span><select id=\"py-staff\" onchange=\"AdminPaymentsJs.list()\"><option value=\"\">Anyone</option>" + Ui.Opts(people.Select(p => (p.StaffId, p.StaffName)), string.Empty) + "</select></label>");
            sb.Append("<label class=\"py-field py-grow2\"><span>Search</span><input type=\"search\" id=\"py-term\" maxlength=\"80\" placeholder=\"Owner, pet, invoice or payment number, reference\" autocomplete=\"off\" oninput=\"AdminPaymentsJs.typed()\"></label></div>");
            sb.Append("<div id=\"py-res\">" + await ResultHtml(c, s, f) + "</div>");
            response.SetElementContents("py-main", sb.ToString());
            response.ExecuteScript("AdminPaymentsJs.shown('" + Ui.Ymd(f.From) + "','" + Ui.Ymd(f.To) + "','" + Ui.Ymd(App.Now(s).Date) + "');");
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
                response.SetElementContents("py-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Filter f = Read(s);
            response.SetElementContents("py-from", Ui.Ymd(f.From));
            response.SetElementContents("py-to", Ui.Ymd(f.To));
            response.SetElementContents("py-res", await ResultHtml(c, s, f));
            response.ExecuteScript("AdminPaymentsJs.shown('" + Ui.Ymd(f.From) + "','" + Ui.Ymd(f.To) + "','" + Ui.Ymd(App.Now(s).Date) + "');");
            return response;
        }

        private async Task<string> ResultHtml(Ctx c, Dictionary<string, string> s, Filter f)
        {
            var (sums, e1) = await Helper.GetListT<PySum>("SELECT Y.METHOD AS Method, SUM(CASE WHEN Y.IS_VOID = 0 THEN 1 ELSE 0 END) AS N, SUM(CASE WHEN Y.IS_VOID = 0 THEN Y.AMOUNT ELSE 0 END) AS Total, SUM(CASE WHEN Y.IS_VOID = 1 THEN 1 ELSE 0 END) AS Voided" + Joins + Where + " GROUP BY Y.METHOD", Ps(s, f));
            if (e1 != string.Empty)
            {
                return "<p class=\"py-empty\">The payments could not be loaded right now. Please try again in a moment.</p>";
            }
            List<PySum> tot = sums ?? new List<PySum>();
            int count = tot.Sum(x => x.N);
            int all = count + tot.Sum(x => x.Voided);
            int page = Ui.PageOf(f.Page, Size, all);
            List<SqlParameter> lp = Ps(s, f);
            lp.Add(App.PInt("skip", (page - 1) * Size));
            lp.Add(App.PInt("take", Size));
            var (rows, e2) = await Helper.GetListT<PyRow>("SELECT " + RowCols + Joins + Where + " ORDER BY Y.PAID_UTC DESC, Y.PAY_NO DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", lp);
            if (e2 != string.Empty)
            {
                return "<p class=\"py-empty\">The payments could not be loaded right now. Please try again in a moment.</p>";
            }
            var (cash, e3) = await Helper.GetListT<PyCash>("SELECT Y.PAID_UTC AS PaidUtc, Y.AMOUNT AS Amount FROM PAYMENT Y WHERE Y.METHOD = N'cash' AND Y.IS_VOID = 0 AND Y.PAID_UTC >= @fu AND Y.PAID_UTC < @tu", new List<SqlParameter> { App.PDate("fu", ToUtc(s, f.From)), App.PDate("tu", ToUtc(s, f.To.AddDays(1))) });
            List<PyCash> cashRows = e3 == string.Empty && cash != null ? cash : new List<PyCash>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"py-top\"><div class=\"py-tiles\">");
            foreach (string m in Methods)
            {
                PySum? x = tot.FirstOrDefault(t => t.Method == m);
                sb.Append(Tile(MethodName(m), x?.Total ?? 0, x?.N ?? 0, false));
            }
            sb.Append(Tile("Total", tot.Sum(x => x.Total), count, true));
            sb.Append("</div>");
            sb.Append(DrawerHtml(s, f, cashRows));
            sb.Append("</div>");
            int voided = tot.Sum(x => x.Voided);
            sb.Append("<div class=\"py-lhead\"><h2>" + Ui.Enc(RangeText(f)) + "</h2><span class=\"py-sub\">" + Ui.Plural(count, "payment", "payments") + (voided > 0 ? " &middot; " + Ui.Plural(voided, "voided", "voided") : string.Empty) + (f.Method != string.Empty || f.StaffId != string.Empty || f.Term != string.Empty ? " &middot; filtered" : string.Empty) + "</span></div>");
            List<PyRow> list = rows ?? new List<PyRow>();
            if (list.Count == 0)
            {
                sb.Append("<div class=\"py-panel py-empty\">No payments " + (f.OneDay ? "on this day" : "in these dates") + (f.Method != string.Empty || f.StaffId != string.Empty || f.Term != string.Empty ? " match the filters" : string.Empty) + ".</div>");
                return sb.ToString();
            }
            bool canVoid = c.Can(Key, 'X');
            bool invLink = c.Can("invoices", 'V');
            bool cliLink = c.Can("clients", 'V');
            sb.Append("<div class=\"py-tw\"><table class=\"py-table\"><thead><tr><th>Payment</th><th>" + (f.OneDay ? "Time" : "When") + "</th><th>Owner</th><th>Invoice</th><th>Pet</th><th>Method</th><th>Reference</th><th class=\"py-num\">Amount</th><th>Taken by</th>" + (canVoid ? "<th><span class=\"py-sr\">Actions</span></th>" : string.Empty) + "</tr></thead><tbody>");
            foreach (PyRow r in list)
            {
                DateTime local = App.Local(s, r.PaidUtc);
                string owner = Owner(r.FirstName, r.LastName);
                sb.Append("<tr class=\"" + (r.IsVoid ? "py-void" : string.Empty) + "\">");
                sb.Append("<td class=\"py-cname\"><b>" + Ui.Enc(r.PayNo) + "</b>" + (r.IsVoid ? "<small class=\"py-vr\">Voided" + (r.VoidReason != string.Empty ? ": " + Ui.Enc(r.VoidReason) : string.Empty) + "</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"" + (f.OneDay ? "Time" : "When") + "\" class=\"py-nw\">" + (f.OneDay ? Ui.Hm(local) : Ui.Enc(Ui.Day(local, false)) + " <span class=\"py-dim\">" + Ui.Hm(local) + "</span>") + "</td>");
                sb.Append("<td data-l=\"Owner\">" + (cliLink && r.ClientId != string.Empty ? "<a class=\"py-link\" href=\"AdminClients?c=" + Uri.EscapeDataString(r.ClientId) + "\">" + Ui.Enc(owner) + "</a>" : Ui.Enc(owner)) + "</td>");
                sb.Append("<td data-l=\"Invoice\" class=\"py-nw\">" + (r.InvoiceNo == string.Empty ? "<span class=\"py-dim\">&mdash;</span>" : invLink ? "<a class=\"py-link\" href=\"AdminInvoices?i=" + Uri.EscapeDataString(r.InvoiceId) + "\">" + Ui.Enc(r.InvoiceNo) + "</a>" : Ui.Enc(r.InvoiceNo)) + "</td>");
                sb.Append("<td data-l=\"Pet\">" + (r.PetName == string.Empty ? "<span class=\"py-dim\">&mdash;</span>" : Ui.Enc(r.PetName)) + "</td>");
                sb.Append("<td data-l=\"Method\"><span class=\"py-st py-m-" + Ui.Attr(Methods.Contains(r.Method) ? r.Method : "other") + "\">" + Ui.Enc(MethodName(r.Method)) + "</span></td>");
                sb.Append("<td data-l=\"Reference\">" + (r.Reference == string.Empty ? "<span class=\"py-dim\">&mdash;</span>" : Ui.Enc(r.Reference)) + "</td>");
                sb.Append("<td data-l=\"Amount\" class=\"py-num py-amt\">" + Ui.Money(r.Amount) + "</td>");
                sb.Append("<td data-l=\"Taken by\">" + (r.StaffName == string.Empty ? "<span class=\"py-dim\">&mdash;</span>" : Ui.Enc(r.StaffName)) + "</td>");
                if (canVoid)
                {
                    sb.Append("<td class=\"py-oacts\">" + (r.IsVoid ? string.Empty : "<button type=\"button\" class=\"py-btn py-btn-sm py-btn-danger\" onclick=\"AdminPaymentsJs.voidAsk('" + Ui.Js(r.PayId) + "')\">Void</button>") + "</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("py", page, Size, all, "payment", "payments", "AdminPaymentsJs.page"));
            return sb.ToString();
        }

        private static string Tile(string label, decimal amount, int n, bool total)
        {
            return "<div class=\"py-tile" + (total ? " py-tile-tot" : string.Empty) + "\"><span>" + Ui.Enc(label) + "</span><b>" + Ui.Money(amount) + "</b><small>" + Ui.Plural(n, "payment", "payments") + "</small></div>";
        }

        private static string DrawerHtml(Dictionary<string, string> s, Filter f, List<PyCash> cash)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"py-drawer\"><span class=\"py-dlab\">Cash drawer</span>");
            if (f.OneDay)
            {
                decimal sum = cash.Sum(x => x.Amount);
                sb.Append("<b>" + Ui.Money(sum) + "</b><small>" + Ui.Plural(cash.Count, "cash payment", "cash payments") + " on " + Ui.Enc(Ui.Day(f.From, false)) + ", all staff</small>");
            }
            else
            {
                List<IGrouping<DateTime, PyCash>> days = cash.GroupBy(x => App.Local(s, x.PaidUtc).Date).OrderByDescending(g => g.Key).ToList();
                sb.Append("<b>" + Ui.Money(cash.Sum(x => x.Amount)) + "</b><small>Cash for these dates, all staff</small>");
                if (days.Count > 0)
                {
                    sb.Append("<ul class=\"py-dlist\">");
                    foreach (IGrouping<DateTime, PyCash> g in days)
                    {
                        sb.Append("<li><span>" + Ui.Enc(Ui.Day(g.Key, false)) + "</span><b>" + Ui.Money(g.Sum(x => x.Amount)) + "</b></li>");
                    }
                    sb.Append("</ul>");
                }
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("py-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Filter f = Read(s);
            var (rows, emsg) = await Helper.GetListT<PyRow>("SELECT " + RowCols + Joins + Where + " ORDER BY Y.PAID_UTC, Y.PAY_NO OFFSET 0 ROWS FETCH NEXT 20000 ROWS ONLY", Ps(s, f));
            if (emsg != string.Empty)
            {
                Toast(response, "The list could not be downloaded. Please try again.");
                return response;
            }
            List<PyRow> list = rows ?? new List<PyRow>();
            if (list.Count == 0)
            {
                Toast(response, "There are no payments in the list to download.");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("Payment no,Date,Time,Owner,Invoice no,Pet,Method,Reference,Amount,Taken by,Voided,Void reason\r\n");
            foreach (PyRow r in list)
            {
                DateTime local = App.Local(s, r.PaidUtc);
                string[] cells = { r.PayNo, Ui.Ymd(local), local.ToString("HH:mm", CultureInfo.InvariantCulture), Owner(r.FirstName, r.LastName), r.InvoiceNo, r.PetName, MethodName(r.Method), r.Reference, r.Amount.ToString("0.00", CultureInfo.InvariantCulture), r.StaffName, r.IsVoid ? "Yes" : "No", r.VoidReason };
                sb.Append(string.Join(",", cells.Select(Ui.Csv)) + "\r\n");
            }
            string name = "payments-" + Ui.Ymd(f.From) + (f.OneDay ? string.Empty : "-to-" + Ui.Ymd(f.To)) + ".csv";
            response.SetVariableData("pycsv", new { name = name, text = sb.ToString(), count = list.Count });
            response.ExecuteScript("AdminPaymentsJs.gotCsv();");
            return response;
        }

        private static decimal? Amount(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            if (t.StartsWith('$'))
            {
                t = t.Substring(1).Trim();
            }
            if (t.Length > 15 || !(Regex.IsMatch(t, "^[0-9]{1,3}(,[0-9]{3})*(\\.[0-9]{1,2})?$") || Regex.IsMatch(t, "^[0-9]+(\\.[0-9]{1,2})?$")))
            {
                return null;
            }
            t = t.Replace(",", string.Empty);
            return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) ? d : null;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A'))
            {
                response.SetElementContents("py-found", "<p class=\"py-err\">You can't record payments.</p>");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string term = Ui.OneLine(GetDataValue("findterm"), 80);
            string digits = Ui.Digits(term);
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("t", term, 80),
                App.P("tl", Like(term), 300),
                App.P("dg", digits.Length >= 3 ? digits : string.Empty, 40),
                App.P("dl", Like(digits), 300)
            };
            var (rows, emsg) = await Helper.GetListT<PyInv>("SELECT I.INVOICE_ID AS InvoiceId, I.INVOICE_NO AS InvoiceNo, I.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(P.PET_NAME, N'') AS PetName, I.STATUS AS Status, I.TOTAL AS Total, I.PAID AS Paid, I.BALANCE AS Balance, I.DUE_DATE AS DueDate, I.CREATED_UTC AS CreatedUtc FROM INVOICE I LEFT JOIN CLIENT C ON C.CLIENT_ID = I.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE I.STATUS IN (N'billed', N'partial') AND I.BALANCE > 0 AND (@t = N'' OR I.INVOICE_NO LIKE @tl OR C.FIRST_NAME LIKE @tl OR C.LAST_NAME LIKE @tl OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @tl OR C.CLIENT_NO LIKE @tl OR P.PET_NAME LIKE @tl OR (@dg <> N'' AND REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N'') LIKE @dl)) ORDER BY I.CREATED_UTC DESC OFFSET 0 ROWS FETCH NEXT 30 ROWS ONLY", ps);
            if (emsg != string.Empty)
            {
                response.SetElementContents("py-found", "<p class=\"py-err\">The invoices could not be loaded right now. Please try again in a moment.</p>");
                return response;
            }
            List<PyInv> list = rows ?? new List<PyInv>();
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                sb.Append("<p class=\"py-dim py-empty-s\">" + (term == string.Empty ? "No invoice has money owing right now." : "No unpaid invoice matches &ldquo;" + Ui.Enc(term) + "&rdquo;.") + "</p>");
            }
            else
            {
                sb.Append("<p class=\"py-dim\">" + (term == string.Empty ? "Most recent unpaid invoices" : Ui.Plural(list.Count, "unpaid invoice", "unpaid invoices") + " found") + "</p>");
                DateTime today = App.Now(s).Date;
                foreach (PyInv r in list)
                {
                    string due = r.DueDate.HasValue ? (r.DueDate.Value.Date < today ? "Past due since " : "Due ") + Ui.Day(r.DueDate.Value, r.DueDate.Value.Year != today.Year) : "Billed";
                    sb.Append("<button type=\"button\" class=\"py-fi\" onclick=\"AdminPaymentsJs.pick('" + Ui.Js(r.InvoiceId) + "')\"><span><b>" + Ui.Enc(Owner(r.FirstName, r.LastName)) + (r.PetName != string.Empty ? " &middot; " + Ui.Enc(r.PetName) : string.Empty) + "</b><small>" + Ui.Enc(r.InvoiceNo) + " &middot; " + Ui.Enc(StatusName(r.Status)) + " &middot; " + Ui.Enc(due) + "</small></span><span class=\"py-fbal\"><small>Balance</small><b>" + Ui.Money(r.Balance) + "</b></span></button>");
                }
            }
            response.SetElementContents("py-found", sb.ToString());
            return response;
        }

        private static string StatusName(string st)
        {
            return st switch
            {
                "open" => "Being checked out",
                "billed" => "Billed",
                "partial" => "Partly paid",
                "paid" => "Paid",
                "void" => "Void",
                _ => st
            };
        }

        public async Task<ApiResponse> PayForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A'))
            {
                Toast(response, "You can't record payments.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string id = CleanId(GetDataValue("invid"));
            var (r, emsg) = await Helper.GetOneT<PyInv>("SELECT I.INVOICE_ID AS InvoiceId, I.INVOICE_NO AS InvoiceNo, I.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(P.PET_NAME, N'') AS PetName, I.STATUS AS Status, I.TOTAL AS Total, I.PAID AS Paid, I.BALANCE AS Balance, I.DUE_DATE AS DueDate, I.CREATED_UTC AS CreatedUtc FROM INVOICE I LEFT JOIN CLIENT C ON C.CLIENT_ID = I.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE I.INVOICE_ID = @iid", new List<SqlParameter> { App.P("iid", id, 50) });
            if (emsg != string.Empty || r == null)
            {
                Toast(response, "That invoice could not be found.");
                return response;
            }
            if (r.Status == "open")
            {
                Toast(response, r.InvoiceNo + " is still being checked out. Take the payment on the checkout page.");
                response.ExecuteScript("AdminPaymentsJs.record();");
                return response;
            }
            if (r.Status == "void" || r.Balance <= 0)
            {
                Toast(response, "Nothing is owed on " + r.InvoiceNo + " any more.");
                response.ExecuteScript("AdminPaymentsJs.record();");
                return response;
            }
            string iid = Ui.Js(r.InvoiceId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Record a payment</h2><p class=\"py-mlead\">" + Ui.Enc(Owner(r.FirstName, r.LastName)) + (r.PetName != string.Empty ? " &middot; " + Ui.Enc(r.PetName) : string.Empty) + " &middot; " + (c.Can("invoices", 'V') ? "<a class=\"py-link\" href=\"AdminInvoices?i=" + Uri.EscapeDataString(r.InvoiceId) + "\">" + Ui.Enc(r.InvoiceNo) + "</a>" : Ui.Enc(r.InvoiceNo)) + " &middot; " + Ui.Enc(StatusName(r.Status)) + (r.DueDate.HasValue && r.Status != "open" ? ", due " + Ui.Enc(Ui.Day(r.DueDate.Value)) : string.Empty) + "</p>");
            sb.Append("<div class=\"py-psum\"><div><span>Total</span><b>" + Ui.Money(r.Total) + "</b></div><div><span>Paid</span><b>" + Ui.Money(r.Paid) + "</b></div><div class=\"py-pbal\"><span>Balance</span><b>" + Ui.Money(r.Balance) + "</b></div></div>");
            sb.Append("<div class=\"py-pgrid\"><label class=\"py-field\"><span>How they pay</span><select id=\"py-pmethod\" onchange=\"AdminPaymentsJs.methodHint()\">" + Ui.Opts(Methods.Select(m => (m, MethodName(m))), "card") + "</select></label>");
            sb.Append("<label class=\"py-field\"><span>Amount</span><input type=\"text\" inputmode=\"decimal\" id=\"py-pamount\" maxlength=\"12\" value=\"" + r.Balance.ToString("0.00", CultureInfo.InvariantCulture) + "\" autocomplete=\"off\"></label>");
            sb.Append("<label class=\"py-field\"><span id=\"py-prefh\">Last 4 digits of the card</span><input type=\"text\" id=\"py-pref\" maxlength=\"60\" autocomplete=\"off\"></label></div>");
            sb.Append("<p class=\"py-dim py-phint\">Card and check payments can't be more than the balance. Cash can be: only the balance is recorded and the change to give back is shown.</p>");
            sb.Append("<em class=\"py-err py-merr\" id=\"py-merr\"></em><div class=\"py-mfoot\"><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPaymentsJs.back()\">Back</button><button type=\"button\" class=\"py-btn py-btn-accent\" onclick=\"AdminPaymentsJs.savePay('" + iid + "','" + App.NewId() + "')\">Save payment</button></div>");
            response.SetElementContents("py-mbody", sb.ToString());
            response.ExecuteScript("AdminPaymentsJs.modal('py-pamount');");
            return response;
        }

        public async Task<ApiResponse> SavePay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A'))
            {
                response.SetElementContents("py-merr", "You can't record payments.");
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string id = CleanId(GetDataValue("invid"));
            string method = (GetDataValue("method") ?? string.Empty).Trim();
            decimal? amt = Amount(GetDataValue("amount"));
            if (!amt.HasValue || amt.Value <= 0)
            {
                response.SetElementContents("py-merr", "Enter the amount, for example 45.50.");
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            var (bt, berr) = await Helper.GetDataTable("SELECT BALANCE FROM INVOICE WHERE INVOICE_ID = @iid", new List<SqlParameter> { App.P("iid", id, 50) });
            decimal before = berr == string.Empty && bt.Rows.Count > 0 ? Convert.ToDecimal(bt.Rows[0][0], CultureInfo.InvariantCulture) : 0;
            var (pid, err) = await Billing.AddPayment(c, id, method, amt.Value, GetDataValue("refno") ?? string.Empty, s, GetDataValue("paykey") ?? string.Empty);
            if (err != string.Empty)
            {
                response.SetElementContents("py-merr", Ui.Enc(err));
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            var (pt, perr) = await Helper.GetDataTable("SELECT PAY_NO, AMOUNT FROM PAYMENT WHERE PAY_ID = @pid", new List<SqlParameter> { App.P("pid", pid, 50) });
            string no = perr == string.Empty && pt.Rows.Count > 0 ? Convert.ToString(pt.Rows[0][0]) ?? string.Empty : string.Empty;
            decimal stored = perr == string.Empty && pt.Rows.Count > 0 ? Convert.ToDecimal(pt.Rows[0][1], CultureInfo.InvariantCulture) : amt.Value;
            decimal change = method == "cash" && amt.Value > stored ? amt.Value - stored : 0;
            string msg = (no != string.Empty ? no + ": " : "Payment saved: ") + "$" + stored.ToString("#,##0.00", CultureInfo.InvariantCulture) + " " + MethodName(method).ToLowerInvariant() + (change > 0 ? ". Give $" + change.ToString("#,##0.00", CultureInfo.InvariantCulture) + " change." : stored >= before ? ". Invoice paid in full." : ".");
            response.ExecuteScript("AdminPaymentsJs.done('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> VoidAsk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'X'))
            {
                Toast(response, "You can't void payments.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string id = CleanId(GetDataValue("payid"));
            var (r, emsg) = await Helper.GetOneT<PyRow>("SELECT " + RowCols + Joins + " WHERE Y.PAY_ID = @pid", new List<SqlParameter> { App.P("pid", id, 50) });
            if (emsg != string.Empty || r == null)
            {
                Toast(response, "That payment could not be found.");
                return response;
            }
            if (r.IsVoid)
            {
                Toast(response, r.PayNo + " is already void.");
                response.ExecuteScript("AdminPaymentsJs.list();");
                return response;
            }
            DateTime local = App.Local(s, r.PaidUtc);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Void payment " + Ui.Enc(r.PayNo) + "</h2><p class=\"py-mlead\">" + Ui.Money(r.Amount) + " " + Ui.Enc(MethodName(r.Method).ToLowerInvariant()) + (r.Reference != string.Empty ? " (" + Ui.Enc(r.Reference) + ")" : string.Empty) + " from " + Ui.Enc(Owner(r.FirstName, r.LastName)) + " on " + Ui.Enc(Ui.Day(local)) + " at " + Ui.Hm(local) + (r.InvoiceNo != string.Empty ? ", for " + Ui.Enc(r.InvoiceNo) : string.Empty) + ".</p>");
            sb.Append("<div class=\"py-warn py-vwarn\">The payment stays in the list, crossed out, and the money goes back onto the invoice balance. Refund the owner yourself if they were charged.</div>");
            sb.Append("<label class=\"py-field py-vfield\"><span>Why is it voided?</span><textarea id=\"py-vreason\" rows=\"3\" maxlength=\"200\" placeholder=\"For example: card charged twice, wrong invoice\"></textarea></label>");
            sb.Append("<em class=\"py-err py-merr\" id=\"py-merr\"></em><div class=\"py-mfoot\"><button type=\"button\" class=\"py-btn py-btn-ghost\" onclick=\"AdminPaymentsJs.closeModal()\">Keep it</button><button type=\"button\" class=\"py-btn py-btn-danger\" onclick=\"AdminPaymentsJs.doVoid('" + Ui.Js(r.PayId) + "')\">Void payment</button></div>");
            response.SetElementContents("py-mbody", sb.ToString());
            response.ExecuteScript("AdminPaymentsJs.modal('py-vreason');");
            return response;
        }

        public async Task<ApiResponse> VoidPay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'X'))
            {
                response.SetElementContents("py-merr", "You can't void payments.");
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            string id = CleanId(GetDataValue("payid"));
            string reason = Ui.OneLine(GetDataValue("reason"), 200);
            if (reason == string.Empty)
            {
                response.SetElementContents("py-merr", "Write why this payment is voided.");
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            string err = await Billing.VoidPayment(c, id, reason, await App.Settings());
            if (err != string.Empty)
            {
                response.SetElementContents("py-merr", Ui.Enc(err));
                response.ExecuteScript("AdminPaymentsJs.failed();");
                return response;
            }
            var (pt, perr) = await Helper.GetDataTable("SELECT PAY_NO FROM PAYMENT WHERE PAY_ID = @pid", new List<SqlParameter> { App.P("pid", id, 50) });
            string no = perr == string.Empty && pt.Rows.Count > 0 ? Convert.ToString(pt.Rows[0][0]) ?? string.Empty : "The payment";
            response.ExecuteScript("AdminPaymentsJs.done('" + Ui.Js(no + " is voided. The balance is back on the invoice.") + "');");
            return response;
        }
    }
}
