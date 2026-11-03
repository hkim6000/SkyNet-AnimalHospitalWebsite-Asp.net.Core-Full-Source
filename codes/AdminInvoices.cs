using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminInvoices : WebPage
    {
        private const string Key = "invoices";
        private const string Me = "AdminInvoicesJs";
        private const int Size = 25;

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Invoices | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--iv-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("iv"))
                .Replace("{plhd_qi}", Ui.Attr(CleanId(QueryValue("i"))))
                .Replace("{plhd_qc}", Ui.Attr(CleanId(QueryValue("c"))));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key), max);
        }

        private static decimal? Dec(string? v)
        {
            string t = (v ?? string.Empty).Trim().Replace("$", string.Empty).Trim();
            if (!Regex.IsMatch(t, "^(\\d{1,3}(,\\d{3})*|\\d+)(\\.\\d{1,2})?$"))
            {
                return null;
            }
            return decimal.TryParse(t.Replace(",", string.Empty), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) ? d : null;
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
            return "<div class=\"iv-panel iv-deny\"><h1>Invoices</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"iv-btn iv-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static string Missing()
        {
            return "<div class=\"iv-panel iv-deny\"><h1>Invoice not found</h1><p>This invoice does not exist any more.</p><a class=\"iv-btn iv-btn-ghost\" href=\"AdminInvoices\">All invoices</a></div>";
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
                "clinic" => "Clinic visit",
                "surgery" => "Surgery",
                "grooming" => "Grooming",
                "boarding" => "Boarding stay",
                "" => "No visit",
                _ => t
            };
        }

        private static string Chip(string status)
        {
            return "<span class=\"iv-st iv-st-" + Ui.Attr(status) + "\">" + Ui.Enc(Billing.StatusName(status)) + "</span>";
        }

        private static string Owner(BlInvoice r)
        {
            string n = (r.FirstName + " " + r.LastName).Trim();
            return n != string.Empty ? n : "No owner on file";
        }

        private static bool Overdue(BlInvoice r, DateTime today)
        {
            return (r.Status == "billed" || r.Status == "partial") && r.Balance > 0 && r.DueDate.HasValue && r.DueDate.Value.Date < today;
        }

        private sealed class Filter
        {
            public string Status = string.Empty;
            public DateTime? From;
            public DateTime? To;
            public string Term = string.Empty;
            public string ClientId = string.Empty;
            public int Page = 1;
        }

        private Filter Read(string clientKey)
        {
            Filter f = new Filter();
            string st = Val("ist", 10);
            f.Status = Billing.States.Contains(st) || st == "owing" || st == "overdue" ? st : string.Empty;
            f.From = Ui.ParseDay(GetDataValue("dfrom"));
            f.To = Ui.ParseDay(GetDataValue("dto"));
            f.Term = Ui.OneLine(GetDataValue("term"), 80);
            f.ClientId = CleanId(GetDataValue(clientKey));
            f.Page = int.TryParse(GetDataValue("pg"), out int p) ? Math.Max(1, p) : 1;
            return f;
        }

        private static (string Where, Func<List<SqlParameter>> Ps) Where(Ctx c, Filter f, Dictionary<string, string> s)
        {
            TimeZoneInfo z = App.Zone(App.S(s, "timezone"));
            DateTime? fu = f.From.HasValue ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(f.From.Value.Date, DateTimeKind.Unspecified), z) : null;
            DateTime? tu = f.To.HasValue ? TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(f.To.Value.Date.AddDays(1), DateTimeKind.Unspecified), z) : null;
            string like = f.Term == string.Empty ? string.Empty : "%" + f.Term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
            DateTime today = App.Now(s).Date;
            string where = " WHERE (@st = N'' OR I.STATUS = @st OR (@st = N'owing' AND I.STATUS IN (N'billed', N'partial')) OR (@st = N'overdue' AND I.STATUS IN (N'billed', N'partial') AND I.BALANCE > 0 AND I.DUE_DATE < @today))" +
                " AND (@fu IS NULL OR I.CREATED_UTC >= @fu) AND (@tu IS NULL OR I.CREATED_UTC < @tu) AND (@cid = N'' OR I.CLIENT_ID = @cid)" +
                " AND (@q = N'' OR I.INVOICE_NO LIKE @q ESCAPE N'\\' OR C.FIRST_NAME LIKE @q ESCAPE N'\\' OR C.LAST_NAME LIKE @q ESCAPE N'\\' OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @q ESCAPE N'\\' OR P.PET_NAME LIKE @q ESCAPE N'\\') AND (@own = 0 OR V.STAFF_ID = @sid)";
            List<SqlParameter> Ps() => new List<SqlParameter> { App.P("st", f.Status, 10), App.PDate("today", today), App.PDate("fu", fu), App.PDate("tu", tu), App.P("cid", f.ClientId, 50), App.P("q", like, 200), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) };
            return (where, Ps);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("iv-menu", Auth.Menu("iv", Me, "AdminInvoices", c));
            response.SetElementContents("iv-who", Auth.Who("iv", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("iv-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string iid = CleanId(GetDataValue("openi"));
            if (iid != string.Empty)
            {
                await DrawDetail(response, c, s, iid, string.Empty);
                return response;
            }
            Filter f = new Filter { ClientId = CleanId(GetDataValue("openc")) };
            string clientName = string.Empty;
            if (f.ClientId != string.Empty)
            {
                var (ct, cerr) = await Helper.GetDataTable("SELECT FIRST_NAME + N' ' + LAST_NAME FROM CLIENT WHERE CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", f.ClientId, 50) });
                clientName = cerr == string.Empty && ct.Rows.Count > 0 ? Convert.ToString(ct.Rows[0][0]) ?? string.Empty : string.Empty;
                if (clientName == string.Empty)
                {
                    f.ClientId = string.Empty;
                }
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"iv-ptop\"><div><h1>Invoices</h1><p>Every invoice with its status. Open one to see the lines and payments, email it, print it or record a payment.</p></div><div class=\"iv-acts\">");
            if (c.Can("balances", 'V'))
            {
                sb.Append("<a class=\"iv-btn iv-btn-ghost\" href=\"AdminBalances\">Balances</a>");
            }
            sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-ghost\" id=\"iv-csvb\" onclick=\"" + Me + ".csv(this)\">Download list (CSV)</button></div></div>");
            sb.Append("<div class=\"iv-bar\"><label class=\"iv-field iv-grow2\"><span>Search</span><input type=\"search\" id=\"iv-term\" maxlength=\"80\" placeholder=\"Invoice number, owner or pet\" autocomplete=\"off\" oninput=\"" + Me + ".typed()\"></label>");
            sb.Append("<label class=\"iv-field\"><span>Status</span><select id=\"iv-ist\" onchange=\"" + Me + ".find(1)\"><option value=\"\">All</option><option value=\"owing\">Money owed (billed or part paid)</option><option value=\"overdue\">Overdue</option><option value=\"open\">Being prepared</option><option value=\"paid\">Paid</option><option value=\"partial\">Part paid</option><option value=\"billed\">Billed</option><option value=\"void\">Void</option></select></label>");
            sb.Append("<label class=\"iv-field iv-dfield\"><span>From</span><input type=\"date\" id=\"iv-dfrom\" onchange=\"" + Me + ".find(1)\"></label><label class=\"iv-field iv-dfield\"><span>To</span><input type=\"date\" id=\"iv-dto\" onchange=\"" + Me + ".find(1)\"></label></div>");
            sb.Append("<div class=\"iv-chips\" id=\"iv-chips\" data-cid=\"" + Ui.Attr(f.ClientId) + "\">" + (f.ClientId != string.Empty ? "<span class=\"iv-chip\">Owner: " + Ui.Enc(clientName) + "<button type=\"button\" class=\"iv-chipx\" aria-label=\"Show all owners\" onclick=\"" + Me + ".allOwners()\">&times;</button></span>" + (c.Can("clients", 'V') ? "<a class=\"iv-link\" href=\"AdminClients?c=" + Uri.EscapeDataString(f.ClientId) + "\">Open owner</a>" : string.Empty) + (c.Can("balances", 'V') ? "<a class=\"iv-link\" href=\"AdminBalances?c=" + Uri.EscapeDataString(f.ClientId) + "\">Statement</a>" : string.Empty) : string.Empty) + "</div>");
            sb.Append("<div id=\"iv-list\">" + await ListHtml(c, s, f) + "</div><div id=\"iv-csvall\" hidden></div>");
            response.SetElementContents("iv-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('list');");
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
                response.SetElementContents("iv-main", Denied());
                return response;
            }
            response.SetElementContents("iv-list", await ListHtml(c, await App.Settings(), Read("clientid")));
            response.ExecuteScript(Me + ".ready('list');");
            return response;
        }

        private static string CsvLine(BlInvoice r, Dictionary<string, string> s)
        {
            string[] cells =
            {
                r.InvoiceNo, Ui.Ymd(App.Local(s, r.CreatedUtc)), Owner(r), r.PetName, TypeName(r.VisitType),
                r.Total.ToString("0.00", CultureInfo.InvariantCulture), r.Paid.ToString("0.00", CultureInfo.InvariantCulture), r.Balance.ToString("0.00", CultureInfo.InvariantCulture),
                Billing.StatusName(r.Status), r.DueDate.HasValue ? Ui.Ymd(r.DueDate.Value) : string.Empty
            };
            return string.Join(",", cells.Select(Ui.Csv));
        }

        private static async Task<string> ListHtml(Ctx c, Dictionary<string, string> s, Filter f)
        {
            var (where, ps) = Where(c, f, s);
            var (sum, serr) = await Helper.GetOneT<IvSum>("SELECT COUNT(*) AS RowCnt, ISNULL(SUM(CASE WHEN I.STATUS = N'void' THEN 0 ELSE I.TOTAL END), 0) AS Total, ISNULL(SUM(CASE WHEN I.STATUS = N'void' THEN 0 ELSE I.PAID END), 0) AS Paid, ISNULL(SUM(CASE WHEN I.STATUS IN (N'void', N'open') THEN 0 ELSE I.BALANCE END), 0) AS Balance" + Billing.InvFrom + where, ps());
            if (serr != string.Empty || sum == null)
            {
                return "<div class=\"iv-tw\"><p class=\"iv-empty\">The invoices could not be loaded. Try again.</p></div>";
            }
            int page = Math.Clamp(f.Page, 1, Math.Max(1, (sum.RowCnt + Size - 1) / Size));
            List<SqlParameter> lp = ps();
            lp.Add(App.PInt("off", (page - 1) * Size));
            lp.Add(App.PInt("take", Size));
            var (rows, rerr) = await Helper.GetListT<BlInvoice>("SELECT " + Billing.InvCols + Billing.InvFrom + where + " ORDER BY I.CREATED_UTC DESC, I.INVOICE_NO DESC OFFSET @off ROWS FETCH NEXT @take ROWS ONLY", lp);
            List<BlInvoice> list = rerr == string.Empty && rows != null ? rows : new List<BlInvoice>();
            DateTime today = App.Now(s).Date;
            StringBuilder sb = new StringBuilder();
            if (list.Count == 0)
            {
                return "<div class=\"iv-tw\"><p class=\"iv-empty\">No invoices match. Try a different search or status.</p></div>";
            }
            sb.Append("<div class=\"iv-tw\"><table class=\"iv-table iv-itable\"><thead><tr><th>Invoice</th><th>Date</th><th>Owner</th><th>Pet</th><th>Visit</th><th class=\"iv-num\">Total</th><th class=\"iv-num\">Paid</th><th class=\"iv-num\">Balance</th><th>Status</th><th>Due</th></tr></thead><tbody>");
            foreach (BlInvoice r in list)
            {
                string go = Me + ".open('" + Ui.Js(r.InvoiceId) + "')";
                bool late = Overdue(r, today);
                sb.Append("<tr class=\"iv-row" + (r.Status == "void" ? " iv-off" : string.Empty) + "\" tabindex=\"0\" onclick=\"" + go + "\" onkeydown=\"if(event.key==='Enter')" + go + "\">");
                sb.Append("<td class=\"iv-cname\"><b class=\"iv-nw\">" + Ui.Enc(r.InvoiceNo) + "</b><small class=\"iv-mob\">" + Ui.Enc(Owner(r) + (r.PetName != string.Empty ? " · " + r.PetName : string.Empty)) + "</small></td>");
                sb.Append("<td data-l=\"Date\" class=\"iv-nw\">" + Ui.Enc(App.Local(s, r.CreatedUtc).ToString("MMM d, yyyy", Ui.Us)) + "</td>");
                sb.Append("<td data-l=\"Owner\" class=\"iv-desk\">" + Ui.Enc(Owner(r)) + "</td>");
                sb.Append("<td data-l=\"Pet\" class=\"iv-desk\">" + Ui.Enc(r.PetName != string.Empty ? r.PetName : "-") + "</td>");
                sb.Append("<td data-l=\"Visit\">" + Ui.Enc(TypeName(r.VisitType)) + "</td>");
                sb.Append("<td data-l=\"Total\" class=\"iv-num\">" + Ui.Money(r.Total) + "</td>");
                sb.Append("<td data-l=\"Paid\" class=\"iv-num\">" + Ui.Money(r.Paid) + "</td>");
                sb.Append("<td data-l=\"Balance\" class=\"iv-num" + (r.Balance > 0 && r.Status != "void" && r.Status != "open" ? " iv-owe" : string.Empty) + "\">" + Ui.Money(r.Status == "void" ? 0 : r.Balance) + "</td>");
                sb.Append("<td data-l=\"Status\">" + Chip(r.Status) + "</td>");
                sb.Append("<td data-l=\"Due\" class=\"iv-nw" + (late ? " iv-late" : string.Empty) + "\">" + (r.DueDate.HasValue && r.Status != "paid" && r.Status != "void" ? Ui.Enc(r.DueDate.Value.ToString("MMM d, yyyy", Ui.Us)) + (late ? "<small class=\"iv-blk\">Overdue</small>" : string.Empty) : "<span class=\"iv-dim\">-</span>") + "</td></tr>");
            }
            sb.Append("</tbody><tfoot><tr class=\"iv-trow\"><td colspan=\"5\" class=\"iv-tlabel\">" + Ui.Plural(sum.RowCnt, "invoice", "invoices") + " in this list<small class=\"iv-blk iv-dim\">Totals leave out void invoices; the balance also leaves out invoices still at checkout.</small></td><td data-l=\"Total\" class=\"iv-num\">" + Ui.Money(sum.Total) + "</td><td data-l=\"Paid\" class=\"iv-num\">" + Ui.Money(sum.Paid) + "</td><td data-l=\"Balance\" class=\"iv-num\">" + Ui.Money(sum.Balance) + "</td><td colspan=\"2\"></td></tr></tfoot></table></div>");
            sb.Append(Ui.Pager("iv", page, Size, sum.RowCnt, "invoice", "invoices", Me + ".find"));
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
            Filter f = Read("clientid");
            var (where, ps) = Where(c, f, s);
            var (rows, rerr) = await Helper.GetListT<BlInvoice>("SELECT TOP 5000 " + Billing.InvCols + Billing.InvFrom + where + " ORDER BY I.CREATED_UTC DESC, I.INVOICE_NO DESC", ps());
            List<BlInvoice> list = rerr == string.Empty && rows != null ? rows : new List<BlInvoice>();
            response.SetElementContents("iv-csvall", string.Concat(list.Select(r => "<i data-csv=\"" + Ui.Attr(CsvLine(r, s)) + "\"></i>")));
            response.ExecuteScript(Me + ".csvGo();");
            await App.Log(c.U.StaffId, "download invoices", Ui.Plural(list.Count, "row", "rows"), true);
            return response;
        }

        private async Task DrawDetail(ApiResponse response, Ctx c, Dictionary<string, string> s, string invoiceId, string toast)
        {
            BlInvoice? inv = await Billing.Get(invoiceId);
            if (inv == null || !c.Mine(inv.VisitStaffId))
            {
                response.SetElementContents("iv-main", Missing());
                response.ExecuteScript(Me + ".ready('missing');");
                return;
            }
            response.SetElementContents("iv-main", await DetailHtml(c, s, inv));
            response.ExecuteScript(Me + ".ready('detail', '" + Ui.Js(inv.InvoiceId) + "');");
            Toast(response, toast);
        }

        private static async Task<string> DetailHtml(Ctx c, Dictionary<string, string> s, BlInvoice inv)
        {
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            List<BlLine> lines = await Billing.Lines(inv.InvoiceId);
            List<BlPay> pays = await Billing.Payments(inv.InvoiceId);
            bool late = Overdue(inv, today);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"iv-ptop\"><div><h1>Invoice " + Ui.Enc(inv.InvoiceNo) + "</h1><p>" + Ui.Enc(Owner(inv) + (inv.PetName != string.Empty ? " · " + inv.PetName : string.Empty) + " · " + App.Local(s, inv.CreatedUtc).ToString("MMM d, yyyy", Ui.Us)) + "</p></div><div class=\"iv-acts\"><a class=\"iv-btn iv-btn-ghost\" href=\"AdminInvoices\">All invoices</a>");
            if (inv.ClientId != string.Empty)
            {
                sb.Append("<a class=\"iv-btn iv-btn-ghost\" href=\"AdminInvoices?c=" + Uri.EscapeDataString(inv.ClientId) + "\">This owner's invoices</a>");
            }
            sb.Append("</div></div>");
            if (late)
            {
                sb.Append("<div class=\"iv-alert\"><b>Overdue</b><span>" + Ui.Money(inv.Balance) + " was due " + Ui.Enc(Ui.Day(inv.DueDate!.Value)) + " (" + Ui.Plural((today - inv.DueDate.Value.Date).Days, "day", "days") + " ago).</span></div>");
            }
            sb.Append("<div class=\"iv-grid\"><div class=\"iv-colm\"><section class=\"iv-panel\"><div class=\"iv-shead\"><h2>Lines</h2>" + Chip(inv.Status) + "</div>");
            sb.Append("<div class=\"iv-tw\"><table class=\"iv-table iv-ltable\"><thead><tr><th>Item</th><th class=\"iv-num\">Qty</th><th class=\"iv-num\">Unit price</th><th class=\"iv-num\">Amount</th></tr></thead><tbody>");
            if (lines.Count == 0)
            {
                sb.Append("<tr><td colspan=\"4\" class=\"iv-empty iv-empty-s\">No charges on this invoice.</td></tr>");
            }
            foreach (BlLine l in lines)
            {
                sb.Append("<tr><td class=\"iv-cname\"><b>" + Ui.Enc(l.Description) + (l.Taxable ? " <span class=\"iv-tag iv-tag-tax\">Tax</span>" : string.Empty) + "</b>" + (l.LineId == string.Empty ? "<small>Added at checkout</small>" : string.Empty) + "</td><td data-l=\"Qty\" class=\"iv-num\">" + Ui.Num(l.Qty) + "</td><td data-l=\"Unit price\" class=\"iv-num\">" + Ui.Money(l.UnitPrice) + "</td><td data-l=\"Amount\" class=\"iv-num iv-amt\">" + Ui.Money(l.Amount) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append("<dl class=\"iv-tdl iv-tdl-in\"><dt>Subtotal</dt><dd>" + Ui.Money(inv.Subtotal) + "</dd>");
            decimal applied = Billing.DiscountApplied(inv);
            if (applied > 0)
            {
                string label = Billing.DiscountLabel(inv.DiscountNote);
                sb.Append("<dt>Discount" + (label != string.Empty ? "<small>" + Ui.Enc(label) + "</small>" : string.Empty) + "</dt><dd>" + Ui.Money(-applied) + "</dd>");
            }
            sb.Append("<dt>Tax</dt><dd>" + Ui.Money(inv.Tax) + "</dd><dt class=\"iv-big\">Total</dt><dd class=\"iv-big\">" + Ui.Money(inv.Total) + "</dd><dt>Paid</dt><dd>" + Ui.Money(inv.Paid) + "</dd></dl>");
            if (inv.Status != "void")
            {
                sb.Append("<div class=\"iv-bal" + (inv.Balance == 0 ? " iv-bal-ok" : inv.Balance < 0 ? " iv-bal-bad" : string.Empty) + "\"><span>Balance</span><b>" + Ui.Money(inv.Balance) + "</b></div>");
            }
            sb.Append("</section>");
            bool canVoidPay = c.Can("payments", 'X') && inv.Status != "void";
            sb.Append("<section class=\"iv-panel\"><div class=\"iv-shead\"><h2>Payments</h2>");
            bool canPay = c.Can("payments", 'A') && inv.Status != "void" && inv.Status != "open" && inv.Balance > 0;
            if (canPay)
            {
                sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-sm iv-btn-accent\" onclick=\"" + Me + ".payForm(this)\">Record a payment</button>");
            }
            sb.Append("</div>");
            if (pays.Count == 0)
            {
                sb.Append("<p class=\"iv-sub\">No payments yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"iv-pays\">");
                foreach (BlPay p in pays)
                {
                    DateTime local = App.Local(s, p.PaidUtc);
                    sb.Append("<li class=\"" + (p.IsVoid ? "iv-off" : string.Empty) + "\"><div class=\"iv-pt\"><b>" + (p.IsVoid ? "<s>" : string.Empty) + Ui.Enc(Billing.MethodName(p.Method) + (p.Reference != string.Empty ? " " + p.Reference : string.Empty)) + (p.IsVoid ? "</s>" : string.Empty) + "</b><small>" + Ui.Enc(p.PayNo + " · " + local.ToString("MMM d, yyyy h:mm tt", Ui.Us) + (p.StaffName != string.Empty ? " · " + p.StaffName : string.Empty)) + "</small>" + (p.IsVoid ? "<small class=\"iv-late\">Void: " + Ui.Enc(p.VoidReason) + "</small>" : string.Empty) + "</div><b class=\"iv-pamt\">" + Ui.Money(p.Amount) + "</b>");
                    if (canVoidPay && !p.IsVoid)
                    {
                        sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-sm iv-btn-danger\" onclick=\"" + Me + ".voidPay('" + Ui.Js(p.PayId) + "', '" + Ui.Js(Billing.MethodName(p.Method) + " " + Ui.Money(p.Amount).Replace("&minus;", "-")) + "')\">Void</button>");
                    }
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
            }
            if (inv.Status == "open")
            {
                sb.Append("<p class=\"iv-info\">This invoice is still being prepared at the front desk." + (c.Can("checkout", 'V') && inv.VisitId != string.Empty ? " <a class=\"iv-link\" href=\"AdminCheckout?v=" + Uri.EscapeDataString(inv.VisitId) + "\">Continue the checkout</a>" : string.Empty) + "</p>");
            }
            sb.Append("<em class=\"iv-err\" id=\"iv-e-pays\"></em></section></div>");
            sb.Append("<div class=\"iv-cols\"><section class=\"iv-panel\"><h2 class=\"iv-h2s\">Details</h2><dl class=\"iv-dl\">");
            sb.Append("<dt>Status</dt><dd>" + Chip(inv.Status) + "</dd>");
            sb.Append("<dt>Made</dt><dd>" + Ui.Time(inv.CreatedUtc, "full") + "</dd>");
            if (inv.BilledUtc.HasValue)
            {
                sb.Append("<dt>Billed</dt><dd>" + Ui.Time(inv.BilledUtc.Value, "day") + "</dd>");
            }
            if (inv.DueDate.HasValue && inv.Status != "void")
            {
                sb.Append("<dt>Due</dt><dd" + (late ? " class=\"iv-late\"" : string.Empty) + ">" + Ui.Enc(Ui.Day(inv.DueDate.Value)) + (late ? " &middot; overdue" : string.Empty) + "</dd>");
            }
            if (inv.ClosedUtc.HasValue)
            {
                sb.Append("<dt>" + (inv.Status == "void" ? "Voided" : "Closed") + "</dt><dd>" + Ui.Time(inv.ClosedUtc.Value, "day") + "</dd>");
            }
            if (inv.Status == "void" && inv.VoidReason != string.Empty)
            {
                sb.Append("<dt>Why void</dt><dd>" + Ui.Enc(inv.VoidReason) + "</dd>");
            }
            sb.Append("<dt>Visit</dt><dd>" + Ui.Enc(TypeName(inv.VisitType) + (inv.VisitNo != string.Empty ? " · " + inv.VisitNo : string.Empty)) + (inv.StaffName != string.Empty ? "<small class=\"iv-blk iv-dim\">" + Ui.Enc(inv.StaffName) + "</small>" : string.Empty) + "</dd></dl>");
            sb.Append("<div class=\"iv-stack\">");
            if (c.Can(Key, 'E') && inv.Status != "void")
            {
                if (Ui.Email(inv.Email.Trim()))
                {
                    sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-accent iv-btn-wide\" onclick=\"" + Me + ".mail(this)\">Email invoice to " + Ui.Enc(inv.FirstName != string.Empty ? inv.FirstName : "owner") + "</button>");
                }
                else
                {
                    sb.Append("<p class=\"iv-warn\">No valid email address for this owner, so the invoice can't be emailed.</p>");
                }
            }
            sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-ghost iv-btn-wide\" onclick=\"" + Me + ".print(this)\">Print</button>");
            if (inv.VisitId != string.Empty && inv.VisitType != "boarding" && c.Can("visits", 'V'))
            {
                sb.Append("<a class=\"iv-btn iv-btn-ghost iv-btn-wide\" href=\"AdminVisit?v=" + Uri.EscapeDataString(inv.VisitId) + "\">Open the visit</a>");
            }
            if (inv.VisitId != string.Empty && c.Can("checkout", 'V') && (inv.Status == "open" || inv.VisitStatus == "checkout"))
            {
                sb.Append("<a class=\"iv-btn iv-btn-ghost iv-btn-wide\" href=\"AdminCheckout?v=" + Uri.EscapeDataString(inv.VisitId) + "\">Open the checkout</a>");
            }
            if (c.Can(Key, 'X') && inv.Status != "void")
            {
                bool hasPay = pays.Any(p => !p.IsVoid);
                sb.Append("<button type=\"button\" class=\"iv-btn iv-btn-danger iv-btn-wide\"" + (hasPay ? " disabled" : string.Empty) + " onclick=\"" + Me + ".voidInv()\">Void invoice</button>" + (hasPay ? "<p class=\"iv-hint\">An invoice with payments can't be voided. Void the payments first.</p>" : string.Empty));
            }
            sb.Append("</div></section>");
            sb.Append("<section class=\"iv-panel\"><h2 class=\"iv-h2s\">Owner</h2><p class=\"iv-oname\">" + (c.Can("clients", 'V') && inv.ClientId != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(inv.ClientId) + "\">" + Ui.Enc(Owner(inv)) + "</a>" : Ui.Enc(Owner(inv))) + "</p><dl class=\"iv-dl\"><dt>Phone</dt><dd>" + (inv.Phone != string.Empty ? Ui.Enc(inv.Phone) : "<span class=\"iv-dim\">None</span>") + "</dd><dt>Email</dt><dd class=\"iv-mail\">" + (inv.Email != string.Empty ? Ui.Enc(inv.Email) : "<span class=\"iv-late\">No email on file</span>") + "</dd></dl>");
            var (bt, berr) = await Helper.GetDataTable("SELECT COUNT(*), ISNULL(SUM(BALANCE), 0) FROM INVOICE WHERE CLIENT_ID = @cid AND STATUS IN (N'billed', N'partial') AND BALANCE > 0", new List<SqlParameter> { App.P("cid", inv.ClientId, 50) });
            int n = !c.U.OwnOnly && berr == string.Empty && bt.Rows.Count > 0 ? Convert.ToInt32(bt.Rows[0][0]) : 0;
            if (n > 0)
            {
                sb.Append("<p class=\"iv-sub iv-mt\">Owes " + Ui.Money(Convert.ToDecimal(bt.Rows[0][1])) + " in all on " + Ui.Plural(n, "invoice", "invoices") + "." + (c.Can("balances", 'V') ? " <a class=\"iv-link\" href=\"AdminBalances?c=" + Uri.EscapeDataString(inv.ClientId) + "\">Statement</a>" : string.Empty) + "</p>");
            }
            sb.Append("</section>");
            sb.Append("<section class=\"iv-panel\"><h2 class=\"iv-h2s\">Notes</h2>");
            if (c.Can(Key, 'E'))
            {
                sb.Append("<label class=\"iv-field\"><span>Note on this invoice <small>(printed on the invoice)</small></span><textarea id=\"iv-inote\" rows=\"3\" maxlength=\"600\">" + Ui.Enc(inv.Note) + "</textarea></label><em class=\"iv-err\" id=\"iv-e-note\"></em><div class=\"iv-mfoot iv-mfoot-l\"><button type=\"button\" class=\"iv-btn iv-btn-sm iv-btn-ghost\" id=\"iv-ngo\" onclick=\"" + Me + ".saveNote()\">Save note</button></div>");
            }
            else
            {
                sb.Append(inv.Note != string.Empty ? "<p class=\"iv-note\">" + Ui.Enc(inv.Note) + "</p>" : "<p class=\"iv-sub\">No note.</p>");
            }
            sb.Append("</section>");
            var (mails, merr) = await Helper.GetListT<IvMail>("SELECT TOP 20 M.MAIL_KIND AS MailKind, M.TO_ADDR AS ToAddr, M.SUBJECT AS Subject, M.SENT_UTC AS SentUtc, M.RESULT AS Result, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM MAILLOG M LEFT JOIN STAFF S ON S.STAFF_ID = M.STAFF_ID WHERE M.INVOICE_ID = @iid OR (M.VISIT_ID = @vid AND M.VISIT_ID <> N'') ORDER BY M.SENT_UTC DESC", new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("vid", inv.VisitId, 50) });
            List<IvMail> ml = merr == string.Empty && mails != null ? mails : new List<IvMail>();
            sb.Append("<section class=\"iv-panel\"><h2 class=\"iv-h2s\">Emails</h2>");
            if (ml.Count == 0)
            {
                sb.Append("<p class=\"iv-sub\">Nothing emailed yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"iv-pays\">");
                foreach (IvMail m in ml)
                {
                    bool ok = m.Result == "Sent";
                    sb.Append("<li><div class=\"iv-pt\"><b class=\"iv-mail\">" + Ui.Enc(m.ToAddr != string.Empty ? m.ToAddr : "No address") + "</b><small>" + Ui.Enc((m.MailKind == "visit" ? "Visit summary" : m.MailKind == "statement" ? "Statement" : "Invoice") + (m.StaffName != string.Empty ? " · " + m.StaffName : string.Empty)) + " &middot; " + Ui.Time(m.SentUtc, "ago") + "</small></div><span class=\"iv-st " + (ok ? "iv-st-paid" : m.Result.StartsWith("Demo", StringComparison.Ordinal) ? "iv-st-billed" : "iv-st-void") + "\">" + Ui.Enc(m.Result) + "</span></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div></div>");
            return sb.ToString();
        }

        private async Task<(Ctx? C, BlInvoice? Inv, Dictionary<string, string> S)> Reach(ApiResponse response, string menu, char right)
        {
            Dictionary<string, string> s = new Dictionary<string, string>();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null, s);
            }
            if (!c.Can(Key, 'V') || !c.Can(menu, right))
            {
                Toast(response, "You don't have the right to do that.");
                return (null, null, s);
            }
            s = await App.Settings();
            string iid = CleanId(GetDataValue("invoiceid"));
            BlInvoice? inv = iid == string.Empty ? null : await Billing.Get(iid);
            if (inv == null || !c.Mine(inv.VisitStaffId))
            {
                response.SetElementContents("iv-main", Missing());
                response.ExecuteScript(Me + ".closeModal();");
                return (null, null, s);
            }
            return (c, inv, s);
        }

        public async Task<ApiResponse> Detail()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, Key, 'V');
            if (c == null || inv == null)
            {
                return response;
            }
            await DrawDetail(response, c, s, inv.InvoiceId, string.Empty);
            return response;
        }

        public async Task<ApiResponse> PayForm()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, "payments", 'A');
            if (c == null || inv == null)
            {
                return response;
            }
            if (inv.Status == "void" || inv.Status == "open" || inv.Balance <= 0)
            {
                await DrawDetail(response, c, s, inv.InvoiceId, inv.Balance <= 0 ? "Nothing is owed on this invoice." : "A payment can't be recorded here.");
                return response;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Record a payment</h2><p class=\"iv-mlead\">Invoice " + Ui.Enc(inv.InvoiceNo) + " &middot; " + Ui.Enc(Owner(inv)) + " &middot; balance " + Ui.Money(inv.Balance) + "</p>");
            sb.Append("<div class=\"iv-fgrid\"><label class=\"iv-field\"><span>How the owner pays</span><select id=\"iv-pm\" onchange=\"" + Me + ".method()\"><option value=\"card\">Card</option><option value=\"cash\">Cash</option><option value=\"check\">Check</option><option value=\"other\">Other</option></select></label>");
            sb.Append("<label class=\"iv-field\"><span>Amount</span><input id=\"iv-pa\" type=\"text\" inputmode=\"decimal\" maxlength=\"10\" value=\"" + inv.Balance.ToString("0.00", CultureInfo.InvariantCulture) + "\" data-bal=\"" + inv.Balance.ToString("0.00", CultureInfo.InvariantCulture) + "\" oninput=\"" + Me + ".change()\"></label>");
            sb.Append("<label class=\"iv-field iv-span2\"><span id=\"iv-prl\">Last 4 digits of the card</span><input id=\"iv-pr\" type=\"text\" maxlength=\"60\" autocomplete=\"off\"></label></div><p class=\"iv-hint\" id=\"iv-ph\">Never write the full card number.</p>");
            sb.Append("<em class=\"iv-err iv-merr\" id=\"iv-e-pay\"></em><div class=\"iv-mfoot\"><button type=\"button\" class=\"iv-btn iv-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"iv-btn iv-btn-accent\" id=\"iv-pgo\" onclick=\"" + Me + ".pay()\">Save payment</button></div>");
            response.SetElementContents("iv-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('iv-pa');");
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, "payments", 'A');
            if (c == null || inv == null)
            {
                return response;
            }
            string method = Val("pmethod", 10);
            decimal? amt = Dec(GetDataValue("pamount"));
            if (amt == null)
            {
                response.SetElementContents("iv-e-pay", "Write the amount as a number, for example 45.00.");
                return response;
            }
            if (inv.Status == "open")
            {
                response.SetElementContents("iv-e-pay", "This invoice is still at checkout. Take the payment there.");
                return response;
            }
            decimal before = inv.Balance;
            var (_, err) = await Billing.AddPayment(c, inv.InvoiceId, method, amt.Value, Val("pref", 60), s, Val("newid", 40));
            if (err != string.Empty)
            {
                response.SetElementContents("iv-e-pay", Ui.Enc(err));
                return response;
            }
            response.ExecuteScript(Me + ".closeModal();");
            decimal a = Math.Round(amt.Value, 2);
            await DrawDetail(response, c, s, inv.InvoiceId, method == "cash" && a > before && before > 0 ? "Cash saved. Give " + Ui.Money(a - before).Replace("&minus;", "-") + " change." : "Payment saved.");
            return response;
        }

        public async Task<ApiResponse> PayVoid()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, "payments", 'X');
            if (c == null || inv == null)
            {
                return response;
            }
            string pid = Val("payid", 50);
            if (!(await Billing.Payments(inv.InvoiceId)).Any(x => x.PayId == pid))
            {
                response.ExecuteScript(Me + ".closeModal();");
                await DrawDetail(response, c, s, inv.InvoiceId, "This payment was not found.");
                return response;
            }
            string err = await Billing.VoidPayment(c, pid, Val("vreason", 200), s);
            if (err != string.Empty)
            {
                response.SetElementContents("iv-e-void", Ui.Enc(err));
                return response;
            }
            response.ExecuteScript(Me + ".closeModal();");
            await DrawDetail(response, c, s, inv.InvoiceId, "Payment voided.");
            return response;
        }

        public async Task<ApiResponse> VoidInv()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, Key, 'X');
            if (c == null || inv == null)
            {
                return response;
            }
            string err = await Billing.VoidInvoice(c, inv.InvoiceId, Val("ireason", 200), s);
            if (err != string.Empty)
            {
                response.SetElementContents("iv-e-void", Ui.Enc(err));
                return response;
            }
            response.ExecuteScript(Me + ".closeModal();");
            await DrawDetail(response, c, s, inv.InvoiceId, "Invoice voided.");
            return response;
        }

        public async Task<ApiResponse> SaveNote()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, Key, 'E');
            if (c == null || inv == null)
            {
                return response;
            }
            string err = await Billing.SetNote(c, inv.InvoiceId, GetDataValue("inote") ?? string.Empty);
            if (err != string.Empty)
            {
                response.SetElementContents("iv-e-note", Ui.Enc(err));
                return response;
            }
            await DrawDetail(response, c, s, inv.InvoiceId, "Note saved.");
            return response;
        }

        public async Task<ApiResponse> EmailInv()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, Key, 'E');
            if (c == null || inv == null)
            {
                return response;
            }
            if (inv.Status == "void")
            {
                await DrawDetail(response, c, s, inv.InvoiceId, "A void invoice can't be emailed.");
                return response;
            }
            var (to, subject, html, files) = await Letters.Invoice(inv.InvoiceId, s);
            string err = await Letters.Send(c, "invoice", inv.ClientId, inv.InvoiceId, inv.VisitId, to, subject, html, files);
            await DrawDetail(response, c, s, inv.InvoiceId, err != string.Empty ? string.Empty : App.Demo ? "Demo: email not really sent." : "Invoice emailed to " + to + ".");
            if (err != string.Empty)
            {
                response.SetElementContents("iv-e-pays", Ui.Enc(err));
                response.ExecuteScript(Me + ".problem('" + Ui.Js(err) + "');");
            }
            else if (App.Demo)
            {
                response.SetElementContents("iv-mbody", "<div class=\"iv-pvbar\"><b>Demo: not really sent</b><span class=\"iv-acts\"><button type=\"button\" class=\"iv-btn iv-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></span></div><p class=\"iv-info iv-pvnote\">This site is in demo mode, so nothing was sent to " + Ui.Enc(to) + ". The email log shows \"Demo: not sent\". This is what the owner would get:</p><div class=\"iv-pv\">" + html + "</div>");
                response.ExecuteScript(Me + ".shown('');");
            }
            return response;
        }

        public async Task<ApiResponse> PrintView()
        {
            ApiResponse response = new ApiResponse();
            var (c, inv, s) = await Reach(response, Key, 'V');
            if (c == null || inv == null)
            {
                return response;
            }
            var (_, _, html, _) = await Letters.Invoice(inv.InvoiceId, s);
            response.SetElementContents("iv-mbody", "<div class=\"iv-pvbar\"><b>Print preview</b><span class=\"iv-acts\"><button type=\"button\" class=\"iv-btn iv-btn-accent\" onclick=\"" + Me + ".doPrint()\">Print</button><button type=\"button\" class=\"iv-btn iv-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></span></div><div class=\"iv-pv\">" + html + "</div>");
            response.ExecuteScript(Me + ".shown('');");
            return response;
        }
    }
}
