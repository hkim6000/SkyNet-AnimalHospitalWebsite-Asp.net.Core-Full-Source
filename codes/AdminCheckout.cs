using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminCheckout : WebPage
    {
        private const string Key = "checkout";
        private const string Me = "AdminCheckoutJs";

        private const string VisitSql = "SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.VISIT_TYPE AS VisitType, V.STATUS AS Status, V.STAFF_ID AS StaffId, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.REASON AS Reason, V.CHECKIN_AT AS CheckinAt, V.READY_AT AS ReadyAt, V.CLOSED_AT AS ClosedAt, " +
            "P.PET_ID AS PetId, P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SIZE AS Size, P.PHOTO AS Photo, P.ALERT AS PetAlert, " +
            "V.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.EMAIL, N'') AS Email, ISNULL(C.ALERT, N'') AS OwnerAlert " +
            "FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID WHERE V.VISIT_ID = @vid";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Checkout | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ck-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ck"))
                .Replace("{plhd_open}", Ui.Attr(CleanId(QueryValue("v"))));
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
            string t = (v ?? string.Empty).Trim().Replace("$", string.Empty).Replace("%", string.Empty).Trim();
            if (!Regex.IsMatch(t, "^(\\d{1,3}(,\\d{3})*|\\d+)(\\.\\d{1,2})?$"))
            {
                return null;
            }
            return decimal.TryParse(t.Replace(",", string.Empty), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) ? d : null;
        }

        private static string Plain(decimal v)
        {
            return v.ToString("0.00", CultureInfo.InvariantCulture);
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
            return "<div class=\"ck-panel ck-deny\"><h1>Checkout</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"ck-btn ck-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static string Panel(string title, string text, string links)
        {
            return "<div class=\"ck-panel ck-deny\"><h1>" + Ui.Enc(title) + "</h1><p>" + text + "</p><div class=\"ck-acts\">" + links + "</div></div>";
        }

        private static string DeskLink()
        {
            return "<a class=\"ck-btn ck-btn-ghost\" href=\"AdminCheckout\">Back to the desk</a>";
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
                _ => t
            };
        }

        private static string ItemTypeName(string t)
        {
            return t switch
            {
                "practice" => "Exam and procedures",
                "medicine" => "Medicine",
                "test" => "Test",
                "vaccine" => "Vaccine",
                "grooming" => "Grooming",
                "product" => "Product",
                "boarding" => "Boarding",
                "fee" => "Fee",
                _ => t
            };
        }

        private static string Chip(string status)
        {
            return "<span class=\"ck-st ck-st-" + Ui.Attr(status) + "\">" + Ui.Enc(Billing.StatusName(status)) + "</span>";
        }

        private static string Ago(DateTime local, DateTime now)
        {
            int m = (int)Math.Floor((now - local).TotalMinutes);
            if (m < 1)
            {
                return "just now";
            }
            if (m < 60)
            {
                return Ui.Plural(m, "min", "mins") + " ago";
            }
            int h = m / 60;
            return h < 24 ? Ui.Plural(h, "hour", "hours") + " ago" : Ui.Day(local, false);
        }

        private static string When(DateTime local, DateTime now)
        {
            return local.Date == now.Date ? Ui.Hm(local) : Ui.Day(local, local.Year != now.Year) + ", " + Ui.Hm(local);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ck-menu", Auth.Menu("ck", Me, "AdminCheckout", c));
            response.SetElementContents("ck-who", Auth.Who("ck", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ck-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string vid = CleanId(GetDataValue("openv"));
            if (vid == string.Empty)
            {
                response.SetElementContents("ck-main", await DeskHtml(c, s));
                response.ExecuteScript(Me + ".ready('desk');");
                return response;
            }
            await Draw(response, c, s, vid, string.Empty, string.Empty);
            return response;
        }

        public async Task<ApiResponse> Desk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ck-main", Denied());
                return response;
            }
            response.SetElementContents("ck-desk", await DeskBody(c, await App.Settings()));
            response.ExecuteScript(Me + ".ready('desk');");
            return response;
        }

        private static async Task<string> DeskHtml(Ctx c, Dictionary<string, string> s)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ck-ptop\"><div><h1>Checkout</h1><p>Pets ready to go home. Open one to finish the invoice, take payment and send the owner the visit summary.</p></div><div class=\"ck-acts\">");
            if (c.Can("invoices", 'V'))
            {
                sb.Append("<a class=\"ck-btn ck-btn-ghost\" href=\"AdminInvoices\">All invoices</a>");
            }
            sb.Append("<button type=\"button\" class=\"ck-btn ck-btn-ghost\" onclick=\"" + Me + ".desk()\">Refresh</button></div></div>");
            sb.Append("<div id=\"ck-desk\">" + await DeskBody(c, s) + "</div>");
            return sb.ToString();
        }

        private static async Task<string> DeskBody(Ctx c, Dictionary<string, string> s)
        {
            DateTime now = App.Now(s);
            List<SqlParameter> Ps() => new List<SqlParameter> { App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50), App.PDate("d0", now.Date), App.PDate("d1", now.Date.AddDays(1)) };
            var (ready, rerr) = await Helper.GetListT<CkDesk>("SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.VISIT_TYPE AS VisitType, ISNULL(S.STAFF_NAME, N'') AS StaffName, V.READY_AT AS ReadyAt, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.PHOTO AS Photo, P.PET_ID AS PetId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.PHONE, N'') AS Phone, " +
                "(SELECT ISNULL(SUM(ROUND(O.QTY * O.UNIT_PRICE, 2)), 0) FROM ORDERLINE O WHERE O.VISIT_ID = V.VISIT_ID AND O.STATUS <> N'cancelled') AS Estimate, ISNULL(I.INVOICE_NO, N'') AS InvoiceNo, ISNULL(I.TOTAL, 0) AS InvTotal, ISNULL(I.PAID, 0) AS InvPaid " +
                "FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID LEFT JOIN INVOICE I ON I.VISIT_ID = V.VISIT_ID AND I.STATUS = N'open' " +
                "WHERE V.STATUS = N'checkout' AND (@own = 0 OR V.STAFF_ID = @sid) ORDER BY V.READY_AT, V.CHECKIN_AT", Ps());
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*), ISNULL(SUM(I.PAID), 0) FROM VISIT V JOIN INVOICE I ON I.VISIT_ID = V.VISIT_ID AND I.STATUS <> N'open' WHERE V.STATUS = N'closed' AND V.CLOSED_AT >= @d0 AND V.CLOSED_AT < @d1 AND (@own = 0 OR V.STAFF_ID = @sid)", Ps());
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            decimal took = total > 0 ? Convert.ToDecimal(cnt.Rows[0][1]) : 0m;
            var (done, derr) = await Helper.GetListT<CkDone>("SELECT TOP 12 V.VISIT_ID AS VisitId, I.INVOICE_ID AS InvoiceId, I.INVOICE_NO AS InvoiceNo, I.STATUS AS Status, I.TOTAL AS Total, I.PAID AS Paid, I.BALANCE AS Balance, V.CLOSED_AT AS ClosedAt, P.PET_NAME AS PetName, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, V.VISIT_TYPE AS VisitType " +
                "FROM VISIT V JOIN INVOICE I ON I.VISIT_ID = V.VISIT_ID AND I.STATUS <> N'open' JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID " +
                "WHERE V.STATUS = N'closed' AND V.CLOSED_AT >= @d0 AND V.CLOSED_AT < @d1 AND (@own = 0 OR V.STAFF_ID = @sid) ORDER BY V.CLOSED_AT DESC", Ps());
            StringBuilder sb = new StringBuilder();
            List<CkDesk> rows = rerr == string.Empty && ready != null ? ready : new List<CkDesk>();
            sb.Append("<section class=\"ck-sect\"><div class=\"ck-shead\"><h2>Ready for checkout</h2><span class=\"ck-sub\">" + Ui.Plural(rows.Count, "pet waiting", "pets waiting") + "</span></div>");
            if (rows.Count == 0)
            {
                sb.Append("<div class=\"ck-tw\"><p class=\"ck-empty\">No one is waiting at the desk right now. Pets show up here when their visit is sent to checkout.</p></div>");
            }
            else
            {
                sb.Append("<div class=\"ck-tw\"><table class=\"ck-table\"><thead><tr><th>Pet</th><th>Owner</th><th>Visit</th><th>Seen by</th><th>Ready</th><th class=\"ck-num\">Estimate</th><th><span class=\"ck-sr\">Open</span></th></tr></thead><tbody>");
                foreach (CkDesk r in rows)
                {
                    string go = Me + ".go('" + Ui.Js(r.VisitId) + "')";
                    sb.Append("<tr class=\"ck-row\" tabindex=\"0\" onclick=\"" + go + "\" onkeydown=\"if(event.key==='Enter')" + go + "\">");
                    sb.Append("<td class=\"ck-cname\"><div class=\"ck-pn\">" + Ui.Avatar("ck", r.PetId, r.PetName, r.Photo) + "<div><b>" + Ui.Enc(r.PetName) + "</b><small>" + Ui.Enc(r.SpeciesName) + "</small></div></div></td>");
                    sb.Append("<td data-l=\"Owner\"><span>" + Ui.Enc(r.OwnerName) + (r.Phone != string.Empty ? "<small class=\"ck-blk ck-dim\">" + Ui.Enc(r.Phone) + "</small>" : string.Empty) + "</span></td>");
                    sb.Append("<td data-l=\"Visit\"><span>" + Ui.Enc(TypeName(r.VisitType)) + "<small class=\"ck-blk ck-dim\">" + Ui.Enc(r.VisitNo) + "</small></span></td>");
                    sb.Append("<td data-l=\"Seen by\">" + Ui.Enc(r.StaffName != string.Empty ? r.StaffName : "Not set") + "</td>");
                    sb.Append("<td data-l=\"Ready\"><span>" + (r.ReadyAt.HasValue ? Ui.Enc(When(r.ReadyAt.Value, now)) + "<small class=\"ck-blk ck-dim\">" + Ui.Enc(Ago(r.ReadyAt.Value, now)) + "</small>" : "&mdash;") + "</span></td>");
                    sb.Append("<td data-l=\"Estimate\" class=\"ck-num\"><span>" + (r.InvoiceNo != string.Empty ? Ui.Money(r.InvTotal) + "<small class=\"ck-blk ck-dim\">Invoice started" + (r.InvPaid > 0 ? ", " + Ui.Money(r.InvPaid) + " paid" : string.Empty) + "</small>" : Ui.Money(r.Estimate)) + "</span></td>");
                    sb.Append("<td class=\"ck-oacts\"><a class=\"ck-btn ck-btn-sm ck-btn-accent\" href=\"AdminCheckout?v=" + Uri.EscapeDataString(r.VisitId) + "\" onclick=\"event.stopPropagation()\">Check out</a></td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            sb.Append("</section>");
            List<CkDone> fin = derr == string.Empty && done != null ? done : new List<CkDone>();
            sb.Append("<section class=\"ck-sect\"><div class=\"ck-shead\"><h2>Paid and finished today</h2><span class=\"ck-sub\">" + Ui.Plural(total, "checkout", "checkouts") + (total > 0 ? " &middot; " + Ui.Money(took) + " paid" : string.Empty) + (total > fin.Count ? " &middot; latest " + fin.Count + " shown" : string.Empty) + "</span>" + (c.Can("invoices", 'V') && total > fin.Count ? "<a class=\"ck-link\" href=\"AdminInvoices\">All invoices</a>" : string.Empty) + "</div>");
            if (fin.Count == 0)
            {
                sb.Append("<div class=\"ck-tw\"><p class=\"ck-empty\">Nothing finished yet today.</p></div>");
            }
            else
            {
                sb.Append("<div class=\"ck-tw\"><table class=\"ck-table\"><thead><tr><th>Pet</th><th>Finished</th><th>Invoice</th><th class=\"ck-num\">Total</th><th class=\"ck-num\">Balance</th><th>Status</th></tr></thead><tbody>");
                foreach (CkDone d in fin)
                {
                    string go = Me + ".go('" + Ui.Js(d.VisitId) + "')";
                    sb.Append("<tr class=\"ck-row\" tabindex=\"0\" onclick=\"" + go + "\" onkeydown=\"if(event.key==='Enter')" + go + "\">");
                    sb.Append("<td class=\"ck-cname\"><b>" + Ui.Enc(d.PetName) + "</b><small>" + Ui.Enc(d.OwnerName) + "</small></td>");
                    sb.Append("<td data-l=\"Finished\">" + (d.ClosedAt.HasValue ? Ui.Enc(Ui.Hm(d.ClosedAt.Value)) : "&mdash;") + "</td>");
                    sb.Append("<td data-l=\"Invoice\"><span class=\"ck-nw\">" + Ui.Enc(d.InvoiceNo) + "</span></td>");
                    sb.Append("<td data-l=\"Total\" class=\"ck-num\">" + Ui.Money(d.Total) + "</td>");
                    sb.Append("<td data-l=\"Balance\" class=\"ck-num" + (d.Balance > 0 ? " ck-owe" : string.Empty) + "\">" + Ui.Money(d.Balance) + "</td>");
                    sb.Append("<td data-l=\"Status\">" + Chip(d.Status) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static async Task<CkVisit?> Visit(string visitId)
        {
            var (row, emsg) = await Helper.GetOneT<CkVisit>(VisitSql, new List<SqlParameter> { App.P("vid", visitId, 50) });
            return emsg == string.Empty ? row : null;
        }

        private async Task Draw(ApiResponse response, Ctx c, Dictionary<string, string> s, string visitId, string toast, string mailNote)
        {
            var (html, mode) = await View(c, s, visitId, mailNote);
            response.SetElementContents("ck-main", html);
            response.ExecuteScript(Me + ".ready('" + mode + "');");
            Toast(response, toast);
        }

        private static async Task<(string Html, string Mode)> View(Ctx c, Dictionary<string, string> s, string visitId, string mailNote)
        {
            CkVisit? v = await Visit(visitId);
            if (v == null || !c.Mine(v.StaffId))
            {
                return (Panel("Visit not found", "This visit does not exist, or it is not one you can open.", DeskLink()), "missing");
            }
            BlInvoice? inv = await Billing.ForVisit(v.VisitId);
            if (v.Status == "checkout" && (inv == null || inv.Status == "open"))
            {
                if (c.Can(Key, 'E'))
                {
                    var (iid, err) = await Billing.Open(c, v.VisitId, s);
                    if (err != string.Empty)
                    {
                        return (Panel("The invoice could not be opened", Ui.Enc(err), DeskLink()), "missing");
                    }
                    inv = await Billing.Get(iid);
                }
                if (inv == null)
                {
                    return (await Estimate(c, s, v), "estimate");
                }
                if (inv.Status == "open")
                {
                    return (await Work(c, s, v, inv), "work");
                }
            }
            if (inv != null && inv.Status != "open")
            {
                return (await Summary(c, s, v, inv, mailNote), "done");
            }
            return (NotReady(c, v, inv), "missing");
        }

        private static string VisitLinks(Ctx c, CkVisit v)
        {
            StringBuilder sb = new StringBuilder();
            if (v.VisitType == "boarding")
            {
                if (c.Can("boarding", 'V'))
                {
                    sb.Append("<a class=\"ck-btn ck-btn-ghost\" href=\"AdminBoarding\">Boarding board</a>");
                }
            }
            else if (c.Can("visits", 'V'))
            {
                sb.Append("<a class=\"ck-btn ck-btn-ghost\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">Visit record</a>");
            }
            return sb.ToString();
        }

        private static string NotReady(Ctx c, CkVisit v, BlInvoice? inv)
        {
            string pet = Ui.Enc(v.PetName);
            string text = v.Status switch
            {
                "waiting" => pet + " is checked in and waiting to be seen. The visit comes to the desk when it is sent to checkout.",
                "inroom" => v.VisitType == "boarding" ? pet + " is still staying with us. Check the stay out on the Boarding board first; it then comes to the desk." : pet + " is still with " + Ui.Enc(v.StaffName != string.Empty ? v.StaffName : "the team") + ". The visit comes to the desk when it is sent to checkout.",
                "cancelled" => "This visit was cancelled, so there is nothing to check out.",
                "closed" => "This visit is closed and has no invoice.",
                _ => "This visit is not waiting at checkout."
            };
            StringBuilder links = new StringBuilder(DeskLink());
            links.Append(VisitLinks(c, v));
            if (v.VisitType != "boarding" && c.Can("today", 'V') && (v.Status == "waiting" || v.Status == "inroom"))
            {
                links.Append("<a class=\"ck-btn ck-btn-ghost\" href=\"AdminToday\">Today board</a>");
            }
            if (inv != null && c.Can("invoices", 'V'))
            {
                links.Append("<a class=\"ck-btn ck-btn-ghost\" href=\"AdminInvoices?i=" + Uri.EscapeDataString(inv.InvoiceId) + "\">Invoice " + Ui.Enc(inv.InvoiceNo) + "</a>");
            }
            return Panel(v.Status == "cancelled" ? "Visit cancelled" : v.Status == "closed" ? "Visit closed" : "Not at checkout yet", text, links.ToString());
        }

        private static string Head(Ctx c, CkVisit v, DateTime now, string title)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ck-ptop\"><div><h1>" + Ui.Enc(title) + "</h1><p>" + Ui.Enc(v.PetName + " · " + (v.OwnerName != string.Empty ? v.OwnerName : "No owner on file") + " · " + v.VisitNo) + "</p></div><div class=\"ck-acts\">" + DeskLink() + VisitLinks(c, v) + "</div></div>");
            if (v.PetAlert != string.Empty)
            {
                sb.Append("<div class=\"ck-alert\"><b>Pet alert</b><span>" + Ui.Enc(v.PetAlert) + "</span></div>");
            }
            if (v.OwnerAlert != string.Empty)
            {
                sb.Append("<div class=\"ck-alert\"><b>Owner alert</b><span>" + Ui.Enc(v.OwnerAlert) + "</span></div>");
            }
            return sb.ToString();
        }

        private static string PetCard(CkVisit v, DateTime now)
        {
            StringBuilder sb = new StringBuilder();
            List<string> bits = new List<string> { v.SpeciesName };
            if (v.Breed != string.Empty)
            {
                bits.Add(v.Breed);
            }
            if (v.Size != string.Empty)
            {
                bits.Add(v.Size);
            }
            sb.Append("<section class=\"ck-panel\"><div class=\"ck-pn ck-pbig\">" + Ui.Avatar("ck", v.PetId, v.PetName, v.Photo, "lg") + "<div><b>" + Ui.Enc(v.PetName) + "</b><small>" + Ui.Enc(string.Join(" · ", bits)) + "</small></div></div>");
            sb.Append("<dl class=\"ck-dl ck-mt\"><dt>Visit</dt><dd>" + Ui.Enc(TypeName(v.VisitType) + " · " + v.VisitNo) + "</dd>");
            sb.Append("<dt>" + (v.VisitType == "boarding" ? "Looked after by" : "Seen by") + "</dt><dd>" + Ui.Enc(v.StaffName != string.Empty ? v.StaffName : "Not set") + "</dd>");
            sb.Append("<dt>Checked in</dt><dd>" + Ui.Enc(When(v.CheckinAt, now)) + "</dd>");
            if (v.ReadyAt.HasValue)
            {
                sb.Append("<dt>Ready</dt><dd>" + Ui.Enc(When(v.ReadyAt.Value, now)) + " <span class=\"ck-dim\">(" + Ui.Enc(Ago(v.ReadyAt.Value, now)) + ")</span></dd>");
            }
            if (v.Reason != string.Empty)
            {
                sb.Append("<dt>Reason</dt><dd>" + Ui.Enc(v.Reason) + "</dd>");
            }
            sb.Append("</dl></section>");
            return sb.ToString();
        }

        private static async Task<string> OwnerCard(Ctx c, CkVisit v, string invoiceId)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"ck-panel\"><h2 class=\"ck-h2s\">Owner</h2>");
            string name = Ui.Enc(v.OwnerName != string.Empty ? v.OwnerName : "No owner on file");
            sb.Append("<p class=\"ck-oname\">" + (c.Can("clients", 'V') && v.ClientId != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(v.ClientId) + "\">" + name + "</a>" : name) + "</p>");
            sb.Append("<dl class=\"ck-dl\"><dt>Phone</dt><dd>" + (v.Phone != string.Empty ? "<a href=\"tel:" + Ui.Attr(Ui.CleanPhone(v.Phone)) + "\">" + Ui.Enc(v.Phone) + "</a>" : "<span class=\"ck-dim\">None</span>") + "</dd>");
            sb.Append("<dt>Email</dt><dd class=\"ck-mail\">" + (Ui.Email(v.Email.Trim()) ? Ui.Enc(v.Email) : "<span class=\"ck-late\">No email on file</span>") + "</dd></dl>");
            var (bt, berr) = await Helper.GetDataTable("SELECT COUNT(*), ISNULL(SUM(BALANCE), 0) FROM INVOICE WHERE CLIENT_ID = @cid AND INVOICE_ID <> @iid AND STATUS IN (N'billed', N'partial') AND BALANCE > 0", new List<SqlParameter> { App.P("cid", v.ClientId, 50), App.P("iid", invoiceId, 50) });
            int n = berr == string.Empty && bt.Rows.Count > 0 ? Convert.ToInt32(bt.Rows[0][0]) : 0;
            decimal owed = n > 0 ? Convert.ToDecimal(bt.Rows[0][1]) : 0m;
            if (n > 0)
            {
                string txt = "Also owes <b>" + Ui.Money(owed) + "</b> on " + Ui.Plural(n, "other invoice", "other invoices") + ".";
                sb.Append("<div class=\"ck-warn\">" + txt + (c.Can("balances", 'V') ? " <a class=\"ck-link\" href=\"AdminBalances?c=" + Uri.EscapeDataString(v.ClientId) + "\">See the balance</a>" : string.Empty) + "</div>");
            }
            else
            {
                sb.Append("<p class=\"ck-sub ck-mt\">No other money owed.</p>");
            }
            sb.Append("</section>");
            return sb.ToString();
        }

        private static string Totals(BlInvoice inv, Dictionary<string, string> s, bool open)
        {
            decimal rate = Billing.TaxRate(s);
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"ck-panel ck-tot\" id=\"ck-tot\"><h2 class=\"ck-h2s\">Totals</h2><dl class=\"ck-tdl\"><dt>Subtotal</dt><dd>" + Ui.Money(inv.Subtotal) + "</dd>");
            decimal applied = Billing.DiscountApplied(inv);
            if (applied > 0)
            {
                string label = Billing.DiscountLabel(inv.DiscountNote);
                sb.Append("<dt>Discount" + (label != string.Empty ? "<small>" + Ui.Enc(label) + "</small>" : string.Empty) + "</dt><dd>" + Ui.Money(-applied) + "</dd>");
            }
            string taxLabel = open ? (rate > 0 ? "Tax (" + rate.ToString("0.####", CultureInfo.InvariantCulture) + "% on taxable items)" : "Tax (no sales tax set)") : "Tax";
            sb.Append("<dt>" + Ui.Enc(taxLabel) + "</dt><dd>" + Ui.Money(inv.Tax) + "</dd>");
            sb.Append("<dt class=\"ck-big\">Total</dt><dd class=\"ck-big\">" + Ui.Money(inv.Total) + "</dd><dt>Paid</dt><dd>" + Ui.Money(inv.Paid) + "</dd></dl>");
            sb.Append("<div class=\"ck-bal" + (inv.Balance == 0 ? " ck-bal-ok" : inv.Balance < 0 ? " ck-bal-bad" : string.Empty) + "\"><span>" + (inv.Balance < 0 ? "Paid too much" : "Balance") + "</span><b id=\"ck-balv\">" + Ui.Money(inv.Balance) + "</b></div></section>");
            return sb.ToString();
        }

        private static string Lines(BlInvoice inv, List<BlLine> lines, bool edit)
        {
            StringBuilder sb = new StringBuilder();
            List<BlLine> live = lines.Where(x => x.Qty > 0).ToList();
            List<BlLine> gone = lines.Where(x => x.Qty <= 0).ToList();
            sb.Append("<div class=\"ck-tw\"><table class=\"ck-table ck-ltable\"><thead><tr><th>Item</th><th class=\"ck-num\">Qty</th><th class=\"ck-num\">Unit price</th><th class=\"ck-num\">Amount</th>" + (edit ? "<th><span class=\"ck-sr\">Actions</span></th>" : string.Empty) + "</tr></thead><tbody>");
            if (live.Count == 0)
            {
                sb.Append("<tr><td colspan=\"" + (edit ? 5 : 4) + "\" class=\"ck-empty ck-empty-s\">No charges on this invoice yet.</td></tr>");
            }
            int n = 0;
            foreach (BlLine l in live)
            {
                List<string> meta = new List<string> { ItemTypeName(l.ItemType) };
                if (l.Unit != string.Empty && l.Unit != "each")
                {
                    meta.Add("per " + l.Unit);
                }
                if (l.LineId == string.Empty)
                {
                    meta.Add("added at checkout");
                }
                sb.Append("<tr data-id=\"" + Ui.Attr(l.IlineId) + "\" id=\"ck-lr-" + n + "\"><td class=\"ck-cname\"><b>" + Ui.Enc(l.Description) + (l.Taxable ? " <span class=\"ck-tag ck-tag-tax\">Tax</span>" : string.Empty) + "</b><small>" + Ui.Enc(string.Join(" · ", meta)) + "</small></td>");
                if (edit)
                {
                    sb.Append("<td data-l=\"Qty\" class=\"ck-num\"><input class=\"ck-lin\" id=\"ck-lq-" + n + "\" type=\"text\" inputmode=\"decimal\" maxlength=\"8\" value=\"" + Ui.Attr(Ui.Num(l.Qty)) + "\" aria-label=\"Quantity of " + Ui.Attr(l.Description) + "\" onchange=\"" + Me + ".saveLine(" + n + ")\"></td>");
                    sb.Append("<td data-l=\"Unit price\" class=\"ck-num\"><input class=\"ck-lin ck-lin-p\" id=\"ck-lp-" + n + "\" type=\"text\" inputmode=\"decimal\" maxlength=\"10\" value=\"" + Ui.Attr(Plain(l.UnitPrice)) + "\" aria-label=\"Unit price of " + Ui.Attr(l.Description) + "\" onchange=\"" + Me + ".saveLine(" + n + ")\"></td>");
                }
                else
                {
                    sb.Append("<td data-l=\"Qty\" class=\"ck-num\">" + Ui.Num(l.Qty) + "</td><td data-l=\"Unit price\" class=\"ck-num\">" + Ui.Money(l.UnitPrice) + "</td>");
                }
                sb.Append("<td data-l=\"Amount\" class=\"ck-num ck-amt\">" + Ui.Money(l.Amount) + "</td>");
                if (edit)
                {
                    sb.Append("<td class=\"ck-oacts\"><button type=\"button\" class=\"ck-btn ck-btn-sm ck-btn-ghost\" onclick=\"" + Me + ".removeLine('" + Ui.Js(l.IlineId) + "', this)\">Remove</button></td>");
                }
                sb.Append("</tr>");
                n++;
            }
            foreach (BlLine l in gone)
            {
                sb.Append("<tr class=\"ck-off\"><td class=\"ck-cname\"><b><s>" + Ui.Enc(l.Description) + "</s></b><small>Removed at checkout, not charged</small></td><td data-l=\"Qty\" class=\"ck-num\">" + Ui.Num(l.OrderQty) + "</td><td data-l=\"Unit price\" class=\"ck-num\">" + Ui.Money(l.UnitPrice) + "</td><td data-l=\"Amount\" class=\"ck-num\">" + Ui.Money(0) + "</td>");
                if (edit)
                {
                    sb.Append("<td class=\"ck-oacts\"><button type=\"button\" class=\"ck-btn ck-btn-sm ck-btn-ghost\" onclick=\"" + Me + ".restoreLine('" + Ui.Js(l.IlineId) + "', this)\">Add back</button></td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static string AddForm(List<BlItem> items, CkVisit v)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ck-addl\"><label class=\"ck-field ck-grow\"><span>Add from the catalog</span><select id=\"ck-aitem\"><option value=\"\">Pick an item&hellip;</option>");
            foreach (IGrouping<string, BlItem> g in items.Where(x => x.Species.Trim() == string.Empty || x.Species.Split(',').Select(y => y.Trim()).Contains(v.SpeciesKey)).GroupBy(x => x.ItemType))
            {
                sb.Append("<optgroup label=\"" + Ui.Attr(ItemTypeName(g.Key)) + "\">");
                foreach (BlItem i in g)
                {
                    sb.Append("<option value=\"" + Ui.Attr(i.ItemId) + "\">" + Ui.Enc(i.ItemName + " · $" + Billing.PriceFor(i, v.Size).ToString("#,##0.00", CultureInfo.InvariantCulture) + (i.Unit != string.Empty && i.Unit != "each" ? " per " + i.Unit : string.Empty)) + "</option>");
                }
                sb.Append("</optgroup>");
            }
            sb.Append("</select></label><label class=\"ck-field ck-qf\"><span>Qty</span><input id=\"ck-aqty\" type=\"text\" inputmode=\"decimal\" maxlength=\"8\" value=\"1\"></label><button type=\"button\" class=\"ck-btn ck-btn-accent\" id=\"ck-ago\" onclick=\"" + Me + ".addLine()\">Add line</button></div><em class=\"ck-err\" id=\"ck-e-add\"></em>");
            return sb.ToString();
        }

        private static string DiscForm(BlInvoice inv)
        {
            bool isPct = Billing.IsPercent(inv.DiscountNote);
            decimal pct = Billing.PercentOf(inv.DiscountNote);
            string note = Billing.DiscountReason(inv.DiscountNote);
            string label = Billing.DiscountLabel(inv.DiscountNote);
            bool has = inv.Discount > 0 || inv.DiscountNote != string.Empty;
            StringBuilder sb = new StringBuilder();
            sb.Append("<details class=\"ck-disc\"" + (has ? " open" : string.Empty) + "><summary>" + (has ? "Discount: " + Ui.Money(-Billing.DiscountApplied(inv)) + (label != string.Empty ? " &middot; " + Ui.Enc(label) : string.Empty) : "Add a discount") + "</summary>");
            sb.Append("<div class=\"ck-addl\"><label class=\"ck-field ck-qf2\"><span>Kind</span><select id=\"ck-dmode\"><option value=\"amount\"" + (isPct ? string.Empty : " selected") + ">Dollars off</option><option value=\"percent\"" + (isPct ? " selected" : string.Empty) + ">Percent off</option></select></label>");
            sb.Append("<label class=\"ck-field ck-qf\"><span>How much</span><input id=\"ck-dval\" type=\"text\" inputmode=\"decimal\" maxlength=\"10\" value=\"" + (has ? Ui.Attr(isPct ? pct.ToString("0.##", CultureInfo.InvariantCulture) : Plain(inv.Discount)) : string.Empty) + "\"></label>");
            sb.Append("<label class=\"ck-field ck-grow\"><span>Reason</span><input id=\"ck-dnote\" type=\"text\" maxlength=\"90\" placeholder=\"For example: senior pet, second pet, staff pet\" value=\"" + Ui.Attr(note) + "\"></label>");
            sb.Append("<span class=\"ck-acts\"><button type=\"button\" class=\"ck-btn ck-btn-accent\" id=\"ck-dgo\" onclick=\"" + Me + ".discount()\">Apply</button>" + (has ? "<button type=\"button\" class=\"ck-btn ck-btn-ghost\" onclick=\"" + Me + ".noDiscount(this)\">Remove</button>" : string.Empty) + "</span></div><em class=\"ck-err\" id=\"ck-e-disc\"></em></details>");
            return sb.ToString();
        }

        private static string PayList(List<BlPay> pays, DateTime now, Dictionary<string, string> s, bool canVoid)
        {
            if (pays.Count == 0)
            {
                return "<p class=\"ck-sub\">No payments yet.</p>";
            }
            StringBuilder sb = new StringBuilder("<ul class=\"ck-pays\">");
            foreach (BlPay p in pays)
            {
                DateTime local = App.Local(s, p.PaidUtc);
                sb.Append("<li class=\"" + (p.IsVoid ? "ck-off" : string.Empty) + "\"><div class=\"ck-pt\"><b>" + (p.IsVoid ? "<s>" : string.Empty) + Ui.Enc(Billing.MethodName(p.Method) + (p.Reference != string.Empty ? " " + p.Reference : string.Empty)) + (p.IsVoid ? "</s>" : string.Empty) + "</b><small>" + Ui.Enc(p.PayNo + " · " + When(local, now) + (p.StaffName != string.Empty ? " · " + p.StaffName : string.Empty)) + "</small>" + (p.IsVoid ? "<small class=\"ck-late\">Void: " + Ui.Enc(p.VoidReason) + "</small>" : string.Empty) + "</div><b class=\"ck-pamt\">" + Ui.Money(p.Amount) + "</b>");
                if (canVoid && !p.IsVoid)
                {
                    sb.Append("<button type=\"button\" class=\"ck-btn ck-btn-sm ck-btn-danger\" onclick=\"" + Me + ".voidPay('" + Ui.Js(p.PayId) + "', '" + Ui.Js(Billing.MethodName(p.Method) + " " + Ui.Money(p.Amount).Replace("&minus;", "-")) + "')\">Void</button>");
                }
                sb.Append("</li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string PayForm(BlInvoice inv)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ck-pform\"><h3 class=\"ck-h3\">Take a payment</h3><div class=\"ck-fpair\"><label class=\"ck-field\"><span>How the owner pays</span><select id=\"ck-pm\" onchange=\"" + Me + ".method()\"><option value=\"card\">Card</option><option value=\"cash\">Cash</option><option value=\"check\">Check</option><option value=\"other\">Other</option></select></label>");
            sb.Append("<label class=\"ck-field\"><span id=\"ck-pal\">Amount</span><input id=\"ck-pa\" type=\"text\" inputmode=\"decimal\" maxlength=\"10\" value=\"" + Ui.Attr(Plain(inv.Balance)) + "\" data-bal=\"" + Ui.Attr(Plain(inv.Balance)) + "\" oninput=\"" + Me + ".change()\"></label></div>");
            sb.Append("<label class=\"ck-field ck-mt\"><span id=\"ck-prl\">Last 4 digits of the card</span><input id=\"ck-pr\" type=\"text\" maxlength=\"60\" autocomplete=\"off\"></label><p class=\"ck-hint\" id=\"ck-ph\">Never write the full card number.</p>");
            sb.Append("<em class=\"ck-err\" id=\"ck-e-pay\"></em><button type=\"button\" class=\"ck-btn ck-btn-accent ck-btn-wide ck-mt\" id=\"ck-pgo\" onclick=\"" + Me + ".pay()\">Take payment</button></div>");
            return sb.ToString();
        }

        private static string FinishBox(BlInvoice inv, Dictionary<string, string> s)
        {
            DateTime due = App.Now(s).Date.AddDays(Billing.DueDays(s));
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"ck-panel ck-fin\"><h2 class=\"ck-h2s\">Finish checkout</h2>");
            if (inv.Balance < 0)
            {
                sb.Append("<div class=\"ck-warn\">The payments are " + Ui.Money(-inv.Balance) + " more than the total. Void a payment or change the lines before finishing.</div>");
            }
            else if (inv.Balance == 0)
            {
                sb.Append("<p class=\"ck-sub\">Everything is paid. Finishing closes the visit and locks the invoice.</p><button type=\"button\" class=\"ck-btn ck-btn-accent ck-btn-wide ck-mt\" id=\"ck-fgo\" onclick=\"" + Me + ".finish(this)\">Paid &mdash; finish checkout</button>");
            }
            else
            {
                sb.Append("<p class=\"ck-sub\">" + Ui.Money(inv.Balance) + " is still to pay. Take a payment above, or bill the owner to pay later.</p><button type=\"button\" class=\"ck-btn ck-btn-accent ck-btn-wide ck-mt\" disabled>Paid &mdash; finish checkout</button>");
            }
            if (inv.Balance > 0)
            {
                sb.Append("<button type=\"button\" class=\"ck-btn ck-btn-ghost ck-btn-wide ck-mt\" id=\"ck-bgo\" onclick=\"" + Me + ".bill('" + Ui.Js(Ui.Money(inv.Balance).Replace("&minus;", "-")) + "', '" + Ui.Js(Ui.Day(due)) + "')\">Bill the owner &mdash; pay later</button><p class=\"ck-hint\">The balance goes on the owner's account, due " + Ui.Enc(Ui.Day(due)) + ".</p>");
            }
            sb.Append("<em class=\"ck-err\" id=\"ck-e-fin\"></em></section>");
            return sb.ToString();
        }

        private static async Task<string> Work(Ctx c, Dictionary<string, string> s, CkVisit v, BlInvoice inv)
        {
            DateTime now = App.Now(s);
            bool edit = c.Can(Key, 'E') && inv.Status == "open";
            List<BlLine> lines = await Billing.Lines(inv.InvoiceId, true);
            List<BlPay> pays = await Billing.Payments(inv.InvoiceId);
            StringBuilder sb = new StringBuilder();
            sb.Append(Head(c, v, now, "Checkout"));
            sb.Append("<div class=\"ck-grid\"><div class=\"ck-colm\">");
            sb.Append(PetCard(v, now));
            sb.Append("<section class=\"ck-panel\"><div class=\"ck-shead\"><h2>Invoice " + Ui.Enc(inv.InvoiceNo) + "</h2>" + Chip(inv.Status) + "</div>");
            sb.Append(Lines(inv, lines, edit));
            if (edit)
            {
                sb.Append("<em class=\"ck-err ck-mt\" id=\"ck-e-line\"></em>");
                sb.Append(AddForm(await Billing.Catalog(), v));
                sb.Append(DiscForm(inv));
            }
            else if (!c.Can(Key, 'E'))
            {
                sb.Append("<p class=\"ck-info\">You can look at this invoice. Changing it and taking payments needs checkout edit rights.</p>");
            }
            sb.Append("</section></div><div class=\"ck-cols\">");
            sb.Append(await OwnerCard(c, v, inv.InvoiceId));
            sb.Append(Totals(inv, s, true));
            sb.Append("<section class=\"ck-panel\"><h2 class=\"ck-h2s\">Payments</h2>" + PayList(pays, now, s, edit));
            if (edit && inv.Balance > 0)
            {
                sb.Append(PayForm(inv));
            }
            sb.Append("</section>");
            if (edit)
            {
                sb.Append(FinishBox(inv, s));
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static async Task<string> Estimate(Ctx c, Dictionary<string, string> s, CkVisit v)
        {
            DateTime now = App.Now(s);
            var (orders, _) = await Helper.GetListT<CkOrder>("SELECT ITEM_NAME AS ItemName, QTY AS Qty, UNIT_PRICE AS UnitPrice, TAXABLE AS Taxable FROM ORDERLINE WHERE VISIT_ID = @vid AND STATUS <> N'cancelled' ORDER BY LINE_ORDER, ITEM_NAME", new List<SqlParameter> { App.P("vid", v.VisitId, 50) });
            List<CkOrder> list = orders ?? new List<CkOrder>();
            StringBuilder sb = new StringBuilder();
            sb.Append(Head(c, v, now, "Checkout"));
            sb.Append("<div class=\"ck-grid\"><div class=\"ck-colm\">" + PetCard(v, now) + "<section class=\"ck-panel\"><div class=\"ck-shead\"><h2>Charges so far</h2><span class=\"ck-sub\">Estimate</span></div>");
            sb.Append("<div class=\"ck-tw\"><table class=\"ck-table\"><thead><tr><th>Item</th><th class=\"ck-num\">Qty</th><th class=\"ck-num\">Amount</th></tr></thead><tbody>");
            if (list.Count == 0)
            {
                sb.Append("<tr><td colspan=\"3\" class=\"ck-empty ck-empty-s\">No charges yet.</td></tr>");
            }
            foreach (CkOrder o in list)
            {
                sb.Append("<tr><td class=\"ck-cname\"><b>" + Ui.Enc(o.ItemName) + "</b></td><td data-l=\"Qty\" class=\"ck-num\">" + Ui.Num(o.Qty) + "</td><td data-l=\"Amount\" class=\"ck-num\">" + Ui.Money(Math.Round(o.Qty * o.UnitPrice, 2)) + "</td></tr>");
            }
            sb.Append("</tbody></table></div><p class=\"ck-sum\">Estimate before tax and discounts: <b>" + Ui.Money(list.Sum(o => Math.Round(o.Qty * o.UnitPrice, 2))) + "</b></p><p class=\"ck-info\">The invoice is not started yet. Someone with checkout edit rights starts it when they open this page.</p></section></div>");
            sb.Append("<div class=\"ck-cols\">" + await OwnerCard(c, v, string.Empty) + "</div></div>");
            return sb.ToString();
        }

        private static async Task<string> Summary(Ctx c, Dictionary<string, string> s, CkVisit v, BlInvoice inv, string mailNote)
        {
            DateTime now = App.Now(s);
            var (to, _, html, _) = await Letters.Visit(v.VisitId, s);
            StringBuilder sb = new StringBuilder();
            sb.Append(Head(c, v, now, inv.Status == "void" ? "Invoice void" : "Checkout finished"));
            sb.Append("<div class=\"ck-grid\"><div class=\"ck-colm\">");
            sb.Append("<section class=\"ck-panel\"><div class=\"ck-shead\"><h2>Visit summary for the owner</h2><span class=\"ck-sub\">" + (App.Demo ? "Preview" : "Email and print") + "</span></div><div class=\"ck-letter\">" + html + "</div></section>");
            sb.Append("</div><div class=\"ck-cols\">");
            sb.Append("<section class=\"ck-panel\"><div class=\"ck-donebox ck-done-" + Ui.Attr(inv.Status) + "\">" + Chip(inv.Status) + "<h2>");
            if (inv.Status == "paid")
            {
                sb.Append("Paid in full");
            }
            else if (inv.Status == "void")
            {
                sb.Append("This invoice is void");
            }
            else
            {
                sb.Append(Ui.Money(inv.Balance) + " billed to the owner");
            }
            sb.Append("</h2><p>Invoice " + Ui.Enc(inv.InvoiceNo) + " &middot; total " + Ui.Money(inv.Total) + " &middot; paid " + Ui.Money(inv.Paid) + (inv.Balance > 0 && inv.DueDate.HasValue ? " &middot; due " + Ui.Enc(Ui.Day(inv.DueDate.Value)) : string.Empty) + "</p>" + (v.ClosedAt.HasValue ? "<p>Visit closed " + Ui.Enc(When(v.ClosedAt.Value, now)) + "</p>" : string.Empty) + "</div>");
            if (mailNote != string.Empty)
            {
                bool bad = !mailNote.StartsWith("Demo mode", StringComparison.Ordinal) && !mailNote.StartsWith("Emailed", StringComparison.Ordinal);
                sb.Append("<div class=\"" + (bad ? "ck-warn" : "ck-info") + " ck-mnote\" id=\"ck-mnote\">" + Ui.Enc(mailNote) + "</div>");
            }
            sb.Append("<div class=\"ck-stack\">");
            if (c.Can(Key, 'E') && inv.Status != "void")
            {
                if (to != string.Empty)
                {
                    sb.Append("<button type=\"button\" class=\"ck-btn ck-btn-accent ck-btn-wide\" id=\"ck-mgo\" onclick=\"" + Me + ".mail(this)\">Email to owner</button><p class=\"ck-hint\">Sends the visit summary and invoice to " + Ui.Enc(to) + ".</p>");
                }
                else
                {
                    sb.Append("<p class=\"ck-warn\">There is no valid email address for this owner, so the summary can't be emailed. Print it instead" + (c.Can("clients", 'E') ? ", or add an email on the owner's page" : string.Empty) + ".</p>");
                }
            }
            sb.Append("<button type=\"button\" class=\"ck-btn ck-btn-ghost ck-btn-wide\" onclick=\"" + Me + ".print(this)\">Print</button>");
            if (c.Can("invoices", 'V'))
            {
                sb.Append("<a class=\"ck-btn ck-btn-ghost ck-btn-wide\" href=\"AdminInvoices?i=" + Uri.EscapeDataString(inv.InvoiceId) + "\">Open invoice " + Ui.Enc(inv.InvoiceNo) + "</a>");
            }
            sb.Append("<a class=\"ck-btn ck-btn-ghost ck-btn-wide\" href=\"AdminCheckout\">Back to the desk</a></div></section>");
            var (mails, merr) = await Helper.GetListT<CkMail>("SELECT TOP 8 M.MAIL_KIND AS MailKind, M.TO_ADDR AS ToAddr, M.SUBJECT AS Subject, M.SENT_UTC AS SentUtc, M.RESULT AS Result, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM MAILLOG M LEFT JOIN STAFF S ON S.STAFF_ID = M.STAFF_ID WHERE (M.VISIT_ID = @vid AND M.VISIT_ID <> N'') OR (M.INVOICE_ID = @iid AND M.INVOICE_ID <> N'') ORDER BY M.SENT_UTC DESC", new List<SqlParameter> { App.P("vid", v.VisitId, 50), App.P("iid", inv.InvoiceId, 50) });
            sb.Append("<section class=\"ck-panel\"><h2 class=\"ck-h2s\">Emails</h2>");
            List<CkMail> ml = merr == string.Empty && mails != null ? mails : new List<CkMail>();
            if (ml.Count == 0)
            {
                sb.Append("<p class=\"ck-sub\">Nothing emailed yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"ck-pays\">");
                foreach (CkMail m in ml)
                {
                    bool ok = m.Result == "Sent";
                    sb.Append("<li><div class=\"ck-pt\"><b class=\"ck-mail\">" + Ui.Enc(m.ToAddr != string.Empty ? m.ToAddr : "No address") + "</b><small>" + Ui.Enc((m.MailKind == "visit" ? "Visit summary" : m.MailKind == "statement" ? "Statement" : "Invoice") + (m.StaffName != string.Empty ? " · " + m.StaffName : string.Empty)) + " &middot; " + Ui.Time(m.SentUtc, "ago") + "</small></div><span class=\"ck-st " + (ok ? "ck-st-paid" : m.Result.StartsWith("Demo", StringComparison.Ordinal) ? "ck-st-billed" : "ck-st-void") + "\">" + Ui.Enc(m.Result) + "</span></li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div></div>");
            return sb.ToString();
        }

        private async Task<(Ctx? C, CkVisit? V, BlInvoice? Inv, Dictionary<string, string> S)> Reach(ApiResponse response, char right, bool needOpen)
        {
            Dictionary<string, string> s = new Dictionary<string, string>();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null, null, s);
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, right))
            {
                Toast(response, "You don't have the right to do that.");
                return (null, null, null, s);
            }
            s = await App.Settings();
            string vid = CleanId(GetDataValue("visitid"));
            CkVisit? v = vid == string.Empty ? null : await Visit(vid);
            if (v == null || !c.Mine(v.StaffId))
            {
                response.SetElementContents("ck-main", Panel("Visit not found", "This visit does not exist, or it is not one you can open.", DeskLink()));
                return (null, null, null, s);
            }
            BlInvoice? inv = await Billing.ForVisit(v.VisitId);
            if (inv == null || (needOpen && inv.Status != "open"))
            {
                await Draw(response, c, s, v.VisitId, needOpen && inv != null ? "This checkout is already finished." : "The invoice is refreshed.", string.Empty);
                return (null, null, null, s);
            }
            return (c, v, inv, s);
        }

        private async Task<ApiResponse> After(ApiResponse response, Ctx c, Dictionary<string, string> s, CkVisit v, string err, string errId, string ok)
        {
            bool stale = err.StartsWith("Someone else", StringComparison.Ordinal) || err.StartsWith("The charges", StringComparison.Ordinal) || err.StartsWith("This invoice is finished", StringComparison.Ordinal) || err.StartsWith("This line was not found", StringComparison.Ordinal) || err.StartsWith("This invoice was not found", StringComparison.Ordinal);
            if (err == string.Empty || stale)
            {
                await Draw(response, c, s, v.VisitId, err == string.Empty ? ok : string.Empty, string.Empty);
            }
            if (err != string.Empty)
            {
                response.SetElementContents(errId, Ui.Enc(err));
                response.ExecuteScript(Me + ".shake('" + errId + "');");
            }
            return response;
        }

        public async Task<ApiResponse> LineSave()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            decimal? q = Dec(GetDataValue("lqty"));
            decimal? p = Dec(GetDataValue("lprice"));
            string err = q == null || p == null ? "Write the quantity and the price as numbers, for example 2 or 18.50." : await Billing.SetLine(c, inv.InvoiceId, Val("ilineid", 50), q.Value, p.Value, s);
            return await After(response, c, s, v, err, "ck-e-line", "Line saved.");
        }

        public async Task<ApiResponse> LineRemove()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            string err = await Billing.RemoveLine(c, inv.InvoiceId, Val("ilineid", 50), s);
            return await After(response, c, s, v, err, "ck-e-line", "Line removed.");
        }

        public async Task<ApiResponse> LineRestore()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            string err = await Billing.RestoreLine(c, inv.InvoiceId, Val("ilineid", 50), s);
            return await After(response, c, s, v, err, "ck-e-line", "Line added back.");
        }

        public async Task<ApiResponse> LineAdd()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            string item = Val("itemid", 50);
            decimal? q = Dec(GetDataValue("aqty"));
            string err = item == string.Empty ? "Pick an item from the list." : q == null ? "Write the quantity as a number." : await Billing.AddLine(c, inv.InvoiceId, Val("newid", 40), item, q.Value, s);
            return await After(response, c, s, v, err, "ck-e-add", "Line added.");
        }

        public async Task<ApiResponse> Discount()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            bool off = Val("dclear", 2) == "1";
            bool pct = Val("dmode", 10) == "percent";
            decimal? d = off ? 0m : Dec(GetDataValue("dval"));
            string err = d == null ? "Write how much as a number, for example 10." : await Billing.SetDiscount(c, inv.InvoiceId, pct, d.Value, off ? string.Empty : Val("dnote", 200), s);
            return await After(response, c, s, v, err, "ck-e-disc", off || d == 0 ? "Discount removed." : "Discount applied.");
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            string method = Val("pmethod", 10);
            decimal? amt = Dec(GetDataValue("pamount"));
            if (amt == null)
            {
                return await After(response, c, s, v, "Write the amount as a number, for example 45.00.", "ck-e-pay", string.Empty);
            }
            decimal before = inv.Balance;
            var (_, err) = await Billing.AddPayment(c, inv.InvoiceId, method, amt.Value, Val("pref", 60), s, Val("newid", 40), true);
            string ok = "Payment saved.";
            if (err == string.Empty && method == "cash" && Math.Round(amt.Value, 2) > before && before > 0)
            {
                ok = "Cash saved. Give " + Ui.Money(Math.Round(amt.Value, 2) - before).Replace("&minus;", "-") + " change.";
            }
            await After(response, c, s, v, err, "ck-e-pay", ok);
            if (err == string.Empty && ok.StartsWith("Cash saved", StringComparison.Ordinal))
            {
                response.ExecuteScript(Me + ".changeDue('" + Ui.Js(ok) + "');");
            }
            return response;
        }

        public async Task<ApiResponse> PayVoid()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            string pid = Val("payid", 50);
            if (!(await Billing.Payments(inv.InvoiceId)).Any(x => x.PayId == pid))
            {
                return await After(response, c, s, v, "This payment was not found.", "ck-e-fin", string.Empty);
            }
            string err = await Billing.VoidPayment(c, pid, Val("vreason", 200), s);
            if (err != string.Empty && err.StartsWith("Write why", StringComparison.Ordinal))
            {
                response.SetElementContents("ck-e-void", Ui.Enc(err));
                return response;
            }
            response.ExecuteScript(Me + ".closeModal();");
            return await After(response, c, s, v, err, "ck-e-fin", "Payment voided.");
        }

        private static async Task<string> AutoMail(Ctx c, Dictionary<string, string> s, CkVisit v, BlInvoice inv, bool manual)
        {
            var (to, subject, html, files) = await Letters.Visit(v.VisitId, s);
            string r = await Letters.Send(c, "visit", v.ClientId, inv.InvoiceId, v.VisitId, to, subject, html, files);
            if (r != string.Empty)
            {
                return to == string.Empty && !manual ? "No email address on file, so the summary was not emailed. Print it for the owner instead." : r;
            }
            return App.Demo ? "Demo mode: the email to " + to + " was not really sent. The preview on this page is what the owner would get." : "Emailed the visit summary and invoice to " + to + ".";
        }

        public async Task<ApiResponse> Finish()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', true);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            bool later = Val("later", 2) == "1";
            string err = await Billing.Finish(c, inv.InvoiceId, later, s);
            if (err != string.Empty)
            {
                response.ExecuteScript(Me + ".closeModal();");
                return await After(response, c, s, v, err, "ck-e-fin", string.Empty);
            }
            response.ExecuteScript(Me + ".closeModal();");
            string note = string.Empty;
            if (App.S(s, "mail.after.checkout") == "1")
            {
                BlInvoice? done = await Billing.Get(inv.InvoiceId);
                note = await AutoMail(c, s, v, done ?? inv, false);
            }
            await Draw(response, c, s, v.VisitId, later ? "Billed to the owner. Visit closed." : "Checkout finished. Visit closed.", note);
            return response;
        }

        public async Task<ApiResponse> EmailOwner()
        {
            ApiResponse response = new ApiResponse();
            var (c, v, inv, s) = await Reach(response, 'E', false);
            if (c == null || v == null || inv == null)
            {
                return response;
            }
            if (inv.Status == "open")
            {
                await Draw(response, c, s, v.VisitId, "Finish the checkout before emailing the summary.", string.Empty);
                return response;
            }
            string note = await AutoMail(c, s, v, inv, true);
            await Draw(response, c, s, v.VisitId, note.StartsWith("Demo mode", StringComparison.Ordinal) ? "Demo: email not really sent." : note.StartsWith("Emailed", StringComparison.Ordinal) ? "Email sent." : string.Empty, note);
            return response;
        }

        public async Task<ApiResponse> PrintView()
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
                Toast(response, "You don't have the right to do that.");
                return response;
            }
            string vid = CleanId(GetDataValue("visitid"));
            CkVisit? v = vid == string.Empty ? null : await Visit(vid);
            if (v == null || !c.Mine(v.StaffId))
            {
                Toast(response, "This visit was not found.");
                return response;
            }
            var (_, _, html, _) = await Letters.Visit(v.VisitId, await App.Settings());
            response.SetElementContents("ck-mbody", "<div class=\"ck-pvbar\"><b>Print preview</b><span class=\"ck-acts\"><button type=\"button\" class=\"ck-btn ck-btn-accent\" onclick=\"" + Me + ".doPrint()\">Print</button><button type=\"button\" class=\"ck-btn ck-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></span></div><div class=\"ck-pv\">" + html + "</div>");
            response.ExecuteScript(Me + ".printShown();");
            return response;
        }
    }
}
