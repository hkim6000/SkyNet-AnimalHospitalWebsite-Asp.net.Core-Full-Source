using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminBalances : WebPage
    {
        private const string Key = "balances";
        private const int Size = 50;
        private const string Open = "I.STATUS IN (N'billed', N'partial') AND I.BALANCE > 0";
        private const string InvCols = "I.INVOICE_ID AS InvoiceId, I.INVOICE_NO AS InvoiceNo, I.CLIENT_ID AS ClientId, ISNULL(C.CLIENT_NO, N'') AS ClientNo, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.EMAIL, N'') AS Email, I.STATUS AS Status, I.TOTAL AS Total, I.PAID AS Paid, I.BALANCE AS Balance, I.DUE_DATE AS DueDate, I.BILLED_UTC AS BilledUtc, I.CREATED_UTC AS CreatedUtc, ISNULL(P.PET_NAME, N'') AS PetName, V.CHECKIN_AT AS VisitAt";
        private const string InvJoins = " FROM INVOICE I LEFT JOIN CLIENT C ON C.CLIENT_ID = I.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID";
        private static readonly string[] Buckets = { "cur", "b30", "b60", "b90", "old" };
        private static readonly string[] Methods = { "card", "cash", "check", "other" };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Balances | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--bl-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("bl"))
                .Replace("{plhd_qc}", Ui.Attr(CleanId(QueryValue("c"))));
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBalancesJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminBalancesJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"bl-panel bl-deny\"><h1>Balances</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"bl-btn bl-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminBalancesJs.out();");
            return response;
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private static string Owner(string first, string last)
        {
            string n = (first + " " + last).Trim();
            return n == string.Empty ? "Unknown owner" : n;
        }

        private static int Late(DateTime? due, DateTime today)
        {
            return due.HasValue ? (int)(today - due.Value.Date).TotalDays : 0;
        }

        private static string Bucket(int late)
        {
            if (late <= 0)
            {
                return "cur";
            }
            if (late <= 30)
            {
                return "b30";
            }
            if (late <= 60)
            {
                return "b60";
            }
            return late <= 90 ? "b90" : "old";
        }

        private static string BucketName(string b)
        {
            return b switch
            {
                "cur" => "Current",
                "b30" => "1–30 days",
                "b60" => "31–60 days",
                "b90" => "61–90 days",
                _ => "Over 90 days"
            };
        }

        private static string AgeTag(string b, int late)
        {
            string cls = b == "cur" ? "bl-age-cur" : b == "b30" ? "bl-age-b30" : b == "b60" ? "bl-age-b60" : "bl-age-old";
            string text = b == "cur" ? "Current" : late + (late == 1 ? " day late" : " days late");
            return "<span class=\"bl-st " + cls + "\">" + Ui.Enc(text) + "</span>";
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

        private static async Task<List<BalInv>> Invoices(string clientId, Dictionary<string, string> s)
        {
            List<SqlParameter> ps = new List<SqlParameter> { App.P("cid", clientId, 50) };
            var (rows, emsg) = await Helper.GetListT<BalInv>("SELECT " + InvCols + InvJoins + " WHERE " + Open + " AND (@cid = N'' OR I.CLIENT_ID = @cid) ORDER BY I.DUE_DATE, I.INVOICE_NO", ps);
            List<BalInv> list = emsg == string.Empty && rows != null ? rows : new List<BalInv>();
            foreach (BalInv i in list.Where(x => !x.DueDate.HasValue))
            {
                i.DueDate = App.Local(s, i.BilledUtc ?? i.CreatedUtc).Date.AddDays(Billing.DueDays(s));
            }
            return list.OrderBy(x => x.DueDate).ThenBy(x => x.InvoiceNo).ToList();
        }

        private sealed class OwnerSum
        {
            public string ClientId = string.Empty;
            public string Name = string.Empty;
            public string ClientNo = string.Empty;
            public string Phone = string.Empty;
            public string Email = string.Empty;
            public int N;
            public decimal Due;
            public decimal PastDue;
            public DateTime? Oldest;
            public int Late;
            public string Bucket = "cur";
            public string Search = string.Empty;
            public BalLast? Last;
        }

        private static List<OwnerSum> Owners(List<BalInv> invs, DateTime today)
        {
            List<OwnerSum> list = new List<OwnerSum>();
            foreach (IGrouping<string, BalInv> g in invs.GroupBy(i => i.ClientId))
            {
                BalInv f = g.First();
                DateTime? oldest = g.Where(i => i.DueDate.HasValue).Select(i => i.DueDate).Min();
                int late = Late(oldest, today);
                list.Add(new OwnerSum
                {
                    ClientId = g.Key,
                    Name = Owner(f.FirstName, f.LastName),
                    ClientNo = f.ClientNo,
                    Phone = f.Phone,
                    Email = f.Email,
                    N = g.Count(),
                    Due = g.Sum(i => i.Balance),
                    PastDue = g.Where(i => Late(i.DueDate, today) > 0).Sum(i => i.Balance),
                    Oldest = oldest,
                    Late = Math.Max(0, late),
                    Bucket = Bucket(late),
                    Search = (Owner(f.FirstName, f.LastName) + " " + f.ClientNo + " " + f.Phone + " " + Ui.Digits(f.Phone) + " " + f.Email + " " + string.Join(" ", g.Select(i => i.InvoiceNo + " " + i.PetName))).ToLowerInvariant()
                });
            }
            return list;
        }

        private sealed class Filter
        {
            public string Bucket = string.Empty;
            public bool PastOnly;
            public string Term = string.Empty;
            public string Sort = "amount";
            public string Page = string.Empty;
        }

        private Filter Read()
        {
            Filter f = new Filter();
            string b = (GetDataValue("bucket") ?? string.Empty).Trim();
            f.Bucket = Buckets.Contains(b) ? b : string.Empty;
            f.PastOnly = (GetDataValue("pastonly") ?? string.Empty).Trim() == "1";
            f.Term = Ui.OneLine(GetDataValue("term"), 80).ToLowerInvariant();
            f.Sort = (GetDataValue("sort") ?? string.Empty).Trim() == "oldest" ? "oldest" : "amount";
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
            response.SetElementContents("bl-menu", Auth.Menu("bl", "AdminBalancesJs", "AdminBalances", c));
            response.SetElementContents("bl-who", Auth.Who("bl", c));
            response.ExecuteScript("AdminBalancesJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bl-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bl-ptop\"><div><h1>Balances</h1><p>Owners with money still due on billed invoices, oldest first or largest first. Open an owner to see their statement, take a payment or email the statement.</p></div></div>");
            var (tiles, list) = await ResultHtml(c, s, new Filter());
            sb.Append("<div id=\"bl-tiles\">" + tiles + "</div>");
            sb.Append("<div class=\"bl-bar\"><label class=\"bl-field bl-grow2\"><span>Search</span><input type=\"search\" id=\"bl-term\" maxlength=\"80\" placeholder=\"Owner, phone, email, pet or invoice number\" autocomplete=\"off\" oninput=\"AdminBalancesJs.typed()\"></label>");
            sb.Append("<label class=\"bl-field\"><span>Age</span><select id=\"bl-bucket\" onchange=\"AdminBalancesJs.list()\"><option value=\"\">Any age</option>" + Ui.Opts(Buckets.Select(b => (b, BucketName(b))), string.Empty) + "</select></label>");
            sb.Append("<label class=\"bl-field\"><span>Sort by</span><select id=\"bl-sort\" onchange=\"AdminBalancesJs.list()\"><option value=\"amount\">Largest amount</option><option value=\"oldest\">Oldest due date</option></select></label>");
            sb.Append("<label class=\"bl-check bl-pcheck\"><input type=\"checkbox\" id=\"bl-past\" onchange=\"AdminBalancesJs.list()\"> Past due only</label></div>");
            sb.Append("<div id=\"bl-list\">" + list + "</div>");
            if (c.Can(Key, 'E'))
            {
                sb.Append("<div class=\"bl-bulk\" id=\"bl-bulk\" hidden><b id=\"bl-bulkn\">0 selected</b><span class=\"bl-acts\"><button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-accent\" onclick=\"AdminBalancesJs.bulkAsk()\">Email statements</button><button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-ghost bl-btn-inv\" onclick=\"AdminBalancesJs.clearSel()\">Clear</button></span></div>");
            }
            response.SetElementContents("bl-main", sb.ToString());
            response.ExecuteScript("AdminBalancesJs.ready(true);");
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
                response.SetElementContents("bl-main", Denied());
                return response;
            }
            var (tiles, list) = await ResultHtml(c, await App.Settings(), Read());
            response.SetElementContents("bl-tiles", tiles);
            response.SetElementContents("bl-list", list);
            response.ExecuteScript("AdminBalancesJs.ready(false);");
            return response;
        }

        private static async Task<List<BalLast>> Lasts()
        {
            var (rows, emsg) = await Helper.GetListT<BalLast>("SELECT C.CLIENT_ID AS ClientId, M.SENT_UTC AS StmtUtc, ISNULL(M.RESULT, N'') AS StmtResult, (SELECT MAX(Y.PAID_UTC) FROM PAYMENT Y WHERE Y.CLIENT_ID = C.CLIENT_ID AND Y.IS_VOID = 0) AS PayUtc FROM CLIENT C OUTER APPLY (SELECT TOP 1 L.SENT_UTC, L.RESULT FROM MAILLOG L WHERE L.CLIENT_ID = C.CLIENT_ID AND L.MAIL_KIND = N'statement' ORDER BY L.SENT_UTC DESC) M WHERE EXISTS (SELECT 1 FROM INVOICE I WHERE I.CLIENT_ID = C.CLIENT_ID AND " + Open + ")");
            return emsg == string.Empty && rows != null ? rows : new List<BalLast>();
        }

        private static async Task<(string Tiles, string List)> ResultHtml(Ctx c, Dictionary<string, string> s, Filter f)
        {
            DateTime today = App.Now(s).Date;
            List<BalInv> invs = await Invoices(string.Empty, s);
            List<OwnerSum> owners = Owners(invs, today);
            List<BalLast> lasts = await Lasts();
            foreach (OwnerSum o in owners)
            {
                o.Last = lasts.FirstOrDefault(l => l.ClientId == o.ClientId);
            }
            StringBuilder sb = new StringBuilder();
            decimal total = invs.Sum(i => i.Balance);
            decimal past = invs.Where(i => Late(i.DueDate, today) > 0).Sum(i => i.Balance);
            sb.Append("<div class=\"bl-tiles\">");
            sb.Append("<div class=\"bl-tile bl-tile-tot\"><span>Total due</span><b>" + Ui.Money(total) + "</b><small>" + Ui.Plural(owners.Count, "owner", "owners") + " &middot; " + Ui.Plural(invs.Count, "invoice", "invoices") + "</small></div>");
            sb.Append("<button type=\"button\" class=\"bl-tile bl-tile-past" + (past == 0 ? " bl-tz" : string.Empty) + (f.PastOnly ? " bl-on" : string.Empty) + "\" onclick=\"AdminBalancesJs.pastTile()\"><span>Past due</span><b>" + Ui.Money(past) + "</b><small>" + Ui.Plural(owners.Count(o => o.PastDue > 0), "owner", "owners") + "</small></button>");
            foreach (string b in Buckets)
            {
                List<BalInv> inB = invs.Where(i => Bucket(Late(i.DueDate, today)) == b).ToList();
                sb.Append("<button type=\"button\" class=\"bl-tile bl-tb-" + b + (inB.Count == 0 ? " bl-tz" : string.Empty) + (f.Bucket == b ? " bl-on" : string.Empty) + "\" onclick=\"AdminBalancesJs.bucketTile('" + b + "')\"><span>" + Ui.Enc(BucketName(b)) + "</span><b>" + Ui.Money(inB.Sum(i => i.Balance)) + "</b><small>" + Ui.Plural(inB.Count, "invoice", "invoices") + "</small></button>");
            }
            sb.Append("</div>");
            string tilesHtml = sb.ToString();
            sb.Clear();
            IEnumerable<OwnerSum> q = owners;
            if (f.Bucket != string.Empty)
            {
                q = q.Where(o => o.Bucket == f.Bucket);
            }
            if (f.PastOnly)
            {
                q = q.Where(o => o.PastDue > 0);
            }
            if (f.Term != string.Empty)
            {
                string dg = Ui.Digits(f.Term);
                q = q.Where(o => o.Search.Contains(f.Term) || (dg.Length >= 3 && o.Search.Contains(dg)));
            }
            List<OwnerSum> shown = f.Sort == "oldest"
                ? q.OrderBy(o => o.Oldest ?? DateTime.MaxValue).ThenByDescending(o => o.Due).ToList()
                : q.OrderByDescending(o => o.Due).ThenBy(o => o.Name).ToList();
            bool edit = c.Can(Key, 'E');
            sb.Append("<div class=\"bl-lhead\"><h2>Owners</h2><span class=\"bl-sub\">" + Ui.Plural(shown.Count, "owner", "owners") + " &middot; " + Ui.Money(shown.Sum(o => o.Due)) + " due" + (shown.Count != owners.Count ? " &middot; filtered" : string.Empty) + "</span></div>");
            if (owners.Count == 0)
            {
                sb.Append("<div class=\"bl-panel bl-empty\">Nobody owes anything right now. Every billed invoice is paid.</div>");
                return (tilesHtml, sb.ToString());
            }
            if (shown.Count == 0)
            {
                sb.Append("<div class=\"bl-panel bl-empty\">No owners match the filters.</div>");
                return (tilesHtml, sb.ToString());
            }
            sb.Append("<div class=\"bl-tw\"><table class=\"bl-table\"><thead><tr>");
            if (edit)
            {
                sb.Append("<th class=\"bl-ck\"><label class=\"bl-cbox\"><input type=\"checkbox\" id=\"bl-all\" aria-label=\"Select all owners on this page\" onclick=\"event.stopPropagation()\" onchange=\"AdminBalancesJs.selAll(this.checked)\"></label></th>");
            }
            sb.Append("<th>Owner</th><th>Contact</th><th class=\"bl-num\">Invoices</th><th class=\"bl-num\">Total due</th><th>Oldest due</th><th>Age</th><th>Last statement</th><th>Last payment</th></tr></thead><tbody>");
            int page = Ui.PageOf(f.Page, Size, shown.Count);
            foreach (OwnerSum o in shown.Skip((page - 1) * Size).Take(Size))
            {
                bool mail = Ui.Email(o.Email);
                sb.Append("<tr class=\"bl-row\" tabindex=\"0\" data-id=\"" + Ui.Attr(o.ClientId) + "\" onclick=\"AdminBalancesJs.open('" + Ui.Js(o.ClientId) + "')\" onkeydown=\"if(event.key==='Enter')AdminBalancesJs.open('" + Ui.Js(o.ClientId) + "')\">");
                if (edit)
                {
                    sb.Append("<td class=\"bl-ck\"><label class=\"bl-cbox\" onclick=\"event.stopPropagation()\"><input type=\"checkbox\" class=\"bl-sel\" value=\"" + Ui.Attr(o.ClientId) + "\" data-mail=\"" + (mail ? "1" : "0") + "\" aria-label=\"Select " + Ui.Attr(o.Name) + "\" onchange=\"AdminBalancesJs.sel()\"></label></td>");
                }
                sb.Append("<td class=\"bl-cname\"><b>" + Ui.Enc(o.Name) + "</b><small>" + Ui.Enc(o.ClientNo) + "</small></td>");
                sb.Append("<td data-l=\"Contact\" class=\"bl-ct\"><span class=\"bl-cv\">" + (o.Phone != string.Empty ? "<span class=\"bl-blk\">" + Ui.Enc(o.Phone) + "</span>" : string.Empty) + (o.Email != string.Empty ? "<span class=\"bl-em bl-dim\" title=\"" + Ui.Attr(o.Email) + "\">" + Ui.Enc(o.Email) + "</span>" : "<span class=\"bl-em bl-dim\">No email</span>") + "</span></td>");
                sb.Append("<td data-l=\"Invoices\" class=\"bl-num\">" + o.N + "</td>");
                sb.Append("<td data-l=\"Total due\" class=\"bl-num bl-owe\">" + Ui.Money(o.Due) + "</td>");
                sb.Append("<td data-l=\"Oldest due\" class=\"bl-nw\">" + (o.Oldest.HasValue ? Ui.Enc(Ui.Day(o.Oldest.Value)) : "<span class=\"bl-dim\">&mdash;</span>") + "</td>");
                sb.Append("<td data-l=\"Age\">" + AgeTag(o.Bucket, o.Late) + "</td>");
                sb.Append("<td data-l=\"Last statement\">" + StmtCell(o.Last) + "</td>");
                sb.Append("<td data-l=\"Last payment\">" + (o.Last?.PayUtc != null ? Ui.Time(o.Last.PayUtc.Value, "day") : "<span class=\"bl-dim\">None yet</span>") + "</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("bl", page, Size, shown.Count, "owner", "owners", "AdminBalancesJs.page"));
            return (tilesHtml, sb.ToString());
        }

        private static string StmtCell(BalLast? l)
        {
            if (l == null || !l.StmtUtc.HasValue)
            {
                return "<span class=\"bl-dim\">Never</span>";
            }
            string r = l.StmtResult;
            string tag = r == "Sent" ? string.Empty : r.StartsWith("Demo", StringComparison.Ordinal) ? "<span class=\"bl-tag bl-tag-warn\">Demo, not sent</span>" : "<span class=\"bl-tag bl-tag-alert\">Failed</span>";
            return Ui.Time(l.StmtUtc.Value, "day") + (tag != string.Empty ? " " + tag : string.Empty);
        }

        public async Task<ApiResponse> Statement()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bl-main", Denied());
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            string html = await StHtml(c, await App.Settings(), id);
            if (html == string.Empty)
            {
                Toast(response, "That owner could not be found.");
                response.ExecuteScript("AdminBalancesJs.stGone();");
                return response;
            }
            response.SetElementContents("bl-mbody", html);
            response.ExecuteScript("AdminBalancesJs.stOpen('" + Ui.Js(id) + "');");
            return response;
        }

        private static async Task<string> StHtml(Ctx c, Dictionary<string, string> s, string id)
        {
            if (id == string.Empty)
            {
                return string.Empty;
            }
            var (cl, cerr) = await Helper.GetOneT<BalClient>("SELECT CLIENT_ID AS ClientId, CLIENT_NO AS ClientNo, FIRST_NAME AS FirstName, LAST_NAME AS LastName, PHONE AS Phone, EMAIL AS Email, ADDRESS1 AS Address1, CITY AS City, STATE AS State, ZIP AS Zip FROM CLIENT WHERE CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", id, 50) });
            if (cerr != string.Empty || cl == null)
            {
                return string.Empty;
            }
            DateTime today = App.Now(s).Date;
            List<BalInv> invs = await Invoices(id, s);
            string sub = "INVOICE_ID IN (SELECT I.INVOICE_ID FROM INVOICE I WHERE I.CLIENT_ID = @cid AND " + Open + ")";
            var (lines, lerr) = await Helper.GetListT<BalLine>("SELECT INVOICE_ID AS InvoiceId, DESCRIPTION AS Description, QTY AS Qty, AMOUNT AS Amount FROM INVOICELINE WHERE QTY <> 0 AND " + sub + " ORDER BY LINE_ORDER, DESCRIPTION", new List<SqlParameter> { App.P("cid", id, 50) });
            var (pays, perr) = await Helper.GetListT<BalPay>("SELECT INVOICE_ID AS InvoiceId, PAY_NO AS PayNo, PAID_UTC AS PaidUtc, METHOD AS Method, REFERENCE AS Reference, AMOUNT AS Amount FROM PAYMENT WHERE IS_VOID = 0 AND " + sub + " ORDER BY PAID_UTC", new List<SqlParameter> { App.P("cid", id, 50) });
            var (mails, merr) = await Helper.GetListT<BalMail>("SELECT TOP 5 M.SENT_UTC AS SentUtc, M.TO_ADDR AS ToAddr, M.RESULT AS Result, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM MAILLOG M LEFT JOIN STAFF S ON S.STAFF_ID = M.STAFF_ID WHERE M.CLIENT_ID = @cid AND M.MAIL_KIND = N'statement' ORDER BY M.SENT_UTC DESC", new List<SqlParameter> { App.P("cid", id, 50) });
            List<BalLine> ln = lerr == string.Empty && lines != null ? lines : new List<BalLine>();
            List<BalPay> py = perr == string.Empty && pays != null ? pays : new List<BalPay>();
            List<BalMail> ml = merr == string.Empty && mails != null ? mails : new List<BalMail>();
            bool canPay = c.Can("payments", 'A');
            bool canMail = c.Can(Key, 'E');
            bool invLink = c.Can("invoices", 'V');
            bool cliLink = c.Can("clients", 'V');
            bool mail = Ui.Email(cl.Email);
            string name = Owner(cl.FirstName, cl.LastName);
            string addr = string.Join(", ", new[] { cl.Address1, (cl.City + " " + cl.State).Trim(), cl.Zip }.Where(x => x.Trim() != string.Empty));
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"bl-dhead\"><div><h2>" + Ui.Enc(name) + "</h2><p class=\"bl-sub\">Statement &middot; " + Ui.Enc(cl.ClientNo) + (cl.Phone != string.Empty ? " &middot; " + Ui.Enc(cl.Phone) : string.Empty) + "</p><p class=\"bl-sub bl-mail\">" + (cl.Email != string.Empty ? Ui.Enc(cl.Email) : "No email on file") + (addr != string.Empty ? " &middot; " + Ui.Enc(addr) : string.Empty) + "</p></div><div class=\"bl-acts\">");
            if (canMail && invs.Count > 0)
            {
                sb.Append(mail ? "<button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-accent\" onclick=\"AdminBalancesJs.mail('" + Ui.Js(id) + "')\">Email statement</button>" : "<button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-ghost\" disabled title=\"No valid email address on file\">Email statement</button>");
            }
            if (invs.Count > 0)
            {
                sb.Append("<button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-ghost\" onclick=\"AdminBalancesJs.print('" + Ui.Js(id) + "')\">Print statement</button>");
            }
            if (cliLink)
            {
                sb.Append("<a class=\"bl-btn bl-btn-sm bl-btn-ghost\" href=\"AdminClients?c=" + Uri.EscapeDataString(id) + "\">Open client</a>");
            }
            if (invLink)
            {
                sb.Append("<a class=\"bl-btn bl-btn-sm bl-btn-ghost\" href=\"AdminInvoices?c=" + Uri.EscapeDataString(id) + "\">All invoices</a>");
            }
            sb.Append("</div></div>");
            if (canMail && !mail && invs.Count > 0)
            {
                sb.Append("<div class=\"bl-warn bl-wtop\">There is no valid email address for this owner, so the statement can only be printed. Add an email on the client page to send it.</div>");
            }
            if (invs.Count == 0)
            {
                sb.Append("<div class=\"bl-done bl-dfull\"><div><b>Nothing is owed</b><span>Every billed invoice for this owner is paid.</span></div></div>");
            }
            else
            {
                sb.Append("<div class=\"bl-aging\">");
                foreach (string b in Buckets)
                {
                    decimal amt = invs.Where(i => Bucket(Late(i.DueDate, today)) == b).Sum(i => i.Balance);
                    sb.Append("<div class=\"bl-ag" + (amt > 0 && b != "cur" ? " bl-ag-late" : string.Empty) + "\"><span>" + Ui.Enc(BucketName(b)) + "</span><b>" + Ui.Money(amt) + "</b></div>");
                }
                sb.Append("<div class=\"bl-ag bl-ag-tot\"><span>Total due</span><b>" + Ui.Money(invs.Sum(i => i.Balance)) + "</b></div></div>");
                sb.Append("<h3>Open invoices (" + invs.Count + ")</h3>");
                foreach (BalInv i in invs)
                {
                    int late = Late(i.DueDate, today);
                    string b = Bucket(late);
                    List<BalLine> il = ln.Where(x => x.InvoiceId == i.InvoiceId).ToList();
                    List<BalPay> ip = py.Where(x => x.InvoiceId == i.InvoiceId).ToList();
                    string iid = Ui.Attr(i.InvoiceId);
                    sb.Append("<div class=\"bl-ibox\" id=\"bl-ib-" + iid + "\"><div class=\"bl-ihead\"><div><b>" + (invLink ? "<a class=\"bl-link\" href=\"AdminInvoices?i=" + Uri.EscapeDataString(i.InvoiceId) + "\">" + Ui.Enc(i.InvoiceNo) + "</a>" : Ui.Enc(i.InvoiceNo)) + "</b><small>" + (i.PetName != string.Empty ? Ui.Enc(i.PetName) + " &middot; " : string.Empty) + (i.VisitAt.HasValue ? "Visit " + Ui.Enc(Ui.Day(i.VisitAt.Value)) : "Created " + Ui.Enc(Ui.Day(App.Local(s, i.CreatedUtc)))) + "</small></div><div class=\"bl-ibal\"><span>Balance</span><b>" + Ui.Money(i.Balance) + "</b></div></div>");
                    sb.Append("<div class=\"bl-imeta\"><span class=\"bl-st bl-st-" + Ui.Attr(i.Status) + "\">" + (i.Status == "partial" ? "Partly paid" : "Billed") + "</span>" + AgeTag(b, late) + "<span class=\"bl-dim\">" + (i.DueDate.HasValue ? "Due " + Ui.Enc(Ui.Day(i.DueDate.Value)) : "No due date") + "</span></div>");
                    if (il.Count > 0)
                    {
                        sb.Append("<ul class=\"bl-ilines\">");
                        foreach (BalLine x in il.Take(5))
                        {
                            sb.Append("<li><span>" + Ui.Enc(x.Description) + (x.Qty != 1 ? " <i class=\"bl-dim\">&times; " + Ui.Num(x.Qty) + "</i>" : string.Empty) + "</span><b>" + Ui.Money(x.Amount) + "</b></li>");
                        }
                        if (il.Count > 5)
                        {
                            sb.Append("<li class=\"bl-more\"><span>and " + Ui.Plural(il.Count - 5, "more item", "more items") + "</span><b>" + Ui.Money(il.Skip(5).Sum(x => x.Amount)) + "</b></li>");
                        }
                        sb.Append("</ul>");
                    }
                    sb.Append("<div class=\"bl-itot\"><span>Total <b>" + Ui.Money(i.Total) + "</b></span><span>Paid <b>" + Ui.Money(i.Paid) + "</b></span><span>Balance <b class=\"bl-owe\">" + Ui.Money(i.Balance) + "</b></span></div>");
                    if (ip.Count > 0)
                    {
                        sb.Append("<ul class=\"bl-ipays\">");
                        foreach (BalPay p in ip)
                        {
                            sb.Append("<li><span>" + Ui.Enc(Ui.Day(App.Local(s, p.PaidUtc))) + " &middot; " + Ui.Enc(MethodName(p.Method)) + (p.Reference != string.Empty ? " " + Ui.Enc(p.Reference) : string.Empty) + " <i class=\"bl-dim\">" + Ui.Enc(p.PayNo) + "</i></span><b>" + Ui.Money(p.Amount) + "</b></li>");
                        }
                        sb.Append("</ul>");
                    }
                    if (canPay)
                    {
                        sb.Append("<div class=\"bl-iacts\"><button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-accent\" id=\"bl-pb-" + iid + "\" onclick=\"AdminBalancesJs.payOpen('" + Ui.Js(i.InvoiceId) + "')\">Record payment</button></div>");
                        sb.Append("<div class=\"bl-payf\" id=\"bl-pf-" + iid + "\" hidden><div class=\"bl-pgrid\"><label class=\"bl-field\"><span>Method</span><select id=\"bl-pm-" + iid + "\" onchange=\"AdminBalancesJs.methodHint('" + Ui.Js(i.InvoiceId) + "')\">" + Ui.Opts(Methods.Select(m => (m, MethodName(m))), "card") + "</select></label>");
                        sb.Append("<label class=\"bl-field\"><span>Amount</span><input type=\"text\" inputmode=\"decimal\" id=\"bl-pa-" + iid + "\" maxlength=\"12\" value=\"" + i.Balance.ToString("0.00", App.Inv) + "\" autocomplete=\"off\"></label>");
                        sb.Append("<label class=\"bl-field\"><span id=\"bl-ph-" + iid + "\">Last 4 digits of the card</span><input type=\"text\" id=\"bl-pr-" + iid + "\" maxlength=\"60\" autocomplete=\"off\"></label></div>");
                        sb.Append("<em class=\"bl-err bl-merr\" id=\"bl-pe-" + iid + "\"></em><div class=\"bl-mfoot\"><button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-ghost\" onclick=\"AdminBalancesJs.payClose('" + Ui.Js(i.InvoiceId) + "')\">Cancel</button><button type=\"button\" class=\"bl-btn bl-btn-sm bl-btn-accent\" onclick=\"AdminBalancesJs.pay('" + Ui.Js(id) + "','" + Ui.Js(i.InvoiceId) + "','" + App.NewId() + "')\">Save payment</button></div></div>");
                    }
                    sb.Append("</div>");
                }
            }
            sb.Append("<h3>Statements sent</h3>");
            if (ml.Count == 0)
            {
                sb.Append("<p class=\"bl-dim bl-empty-s\">No statement has been emailed to this owner yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"bl-rows\">");
                foreach (BalMail m in ml)
                {
                    string tag = m.Result == "Sent" ? "<span class=\"bl-tag bl-tag-ok\">Sent</span>" : m.Result.StartsWith("Demo", StringComparison.Ordinal) ? "<span class=\"bl-tag bl-tag-warn\">Demo, not sent</span>" : "<span class=\"bl-tag bl-tag-alert\">Failed</span>";
                    sb.Append("<li class=\"bl-rl\"><span class=\"bl-rw\">" + Ui.Time(m.SentUtc, "full") + "</span><span class=\"bl-rt\">" + Ui.Enc(m.ToAddr) + (m.StaffName != string.Empty ? " <small>by " + Ui.Enc(m.StaffName) + "</small>" : string.Empty) + (m.Result != "Sent" && !m.Result.StartsWith("Demo", StringComparison.Ordinal) ? " <small>" + Ui.Enc(m.Result) + "</small>" : string.Empty) + "</span>" + tag + "</li>");
                }
                sb.Append("</ul>");
            }
            return sb.ToString();
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
            return decimal.TryParse(t, System.Globalization.NumberStyles.AllowDecimalPoint, App.Inv, out decimal d) ? d : null;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string cid = CleanId(GetDataValue("clientid"));
            string iid = CleanId(GetDataValue("invid"));
            string errId = "bl-pe-" + iid;
            if (!c.Can(Key, 'V') || !c.Can("payments", 'A'))
            {
                response.SetElementContents(errId, "You can't record payments.");
                response.ExecuteScript("AdminBalancesJs.failed();");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            var (own, oerr) = await Helper.GetDataTable("SELECT I.BALANCE FROM INVOICE I WHERE I.INVOICE_ID = @iid AND I.CLIENT_ID = @cid AND " + Open, new List<SqlParameter> { App.P("iid", iid, 50), App.P("cid", cid, 50) });
            if (oerr != string.Empty || own.Rows.Count == 0)
            {
                response.SetElementContents("bl-mbody", await StHtml(c, s, cid));
                response.ExecuteScript("AdminBalancesJs.changed('" + Ui.Js("That invoice has nothing owing any more. The statement is refreshed.") + "');");
                return response;
            }
            string method = (GetDataValue("method") ?? string.Empty).Trim();
            decimal? amt = Amount(GetDataValue("amount"));
            if (!amt.HasValue || amt.Value <= 0)
            {
                response.SetElementContents(errId, "Enter the amount, for example 45.50.");
                response.ExecuteScript("AdminBalancesJs.failed();");
                return response;
            }
            decimal before = Convert.ToDecimal(own.Rows[0][0], App.Inv);
            var (pid, err) = await Billing.AddPayment(c, iid, method, amt.Value, GetDataValue("refno") ?? string.Empty, s, GetDataValue("paykey") ?? string.Empty);
            if (err != string.Empty)
            {
                response.SetElementContents(errId, Ui.Enc(err));
                response.ExecuteScript("AdminBalancesJs.failed();");
                return response;
            }
            var (pt, perr) = await Helper.GetDataTable("SELECT PAY_NO, AMOUNT FROM PAYMENT WHERE PAY_ID = @pid", new List<SqlParameter> { App.P("pid", pid, 50) });
            string no = perr == string.Empty && pt.Rows.Count > 0 ? Convert.ToString(pt.Rows[0][0]) ?? string.Empty : string.Empty;
            decimal stored = perr == string.Empty && pt.Rows.Count > 0 ? Convert.ToDecimal(pt.Rows[0][1], App.Inv) : amt.Value;
            decimal change = method == "cash" && amt.Value > stored ? amt.Value - stored : 0;
            string msg = (no != string.Empty ? no + ": " : "Payment saved: ") + "$" + stored.ToString("#,##0.00", App.Inv) + " " + MethodName(method).ToLowerInvariant() + (change > 0 ? ". Give $" + change.ToString("#,##0.00", App.Inv) + " change." : stored >= before ? ". Invoice paid in full." : ".");
            response.SetElementContents("bl-mbody", await StHtml(c, s, cid));
            response.ExecuteScript("AdminBalancesJs.changed('" + Ui.Js(msg) + "');");
            return response;
        }

        private static async Task<(string Result, string Error)> MailOne(Ctx c, Dictionary<string, string> s, string cid)
        {
            var (to, subject, html, files) = await Letters.Statement(cid, s);
            if (html == string.Empty)
            {
                return ("failed", "The owner could not be found.");
            }
            if (to == string.Empty)
            {
                var (et, eerr) = await Helper.GetDataTable("SELECT EMAIL FROM CLIENT WHERE CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", cid, 50) });
                string raw = eerr == string.Empty && et.Rows.Count > 0 ? Convert.ToString(et.Rows[0][0]) ?? string.Empty : string.Empty;
                await Letters.Send(c, "statement", cid, string.Empty, string.Empty, raw, subject, html, files);
                return ("noemail", "There is no valid email address for this owner.");
            }
            string err = await Letters.Send(c, "statement", cid, string.Empty, string.Empty, to, subject, html, files);
            if (err != string.Empty)
            {
                return ("failed", err);
            }
            return (App.Demo ? "demo" : "sent", to);
        }

        public async Task<ApiResponse> Mail()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't email statements.");
                response.ExecuteScript("AdminBalancesJs.failed();");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string cid = CleanId(GetDataValue("clientid"));
            var (n, nerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM INVOICE I WHERE I.CLIENT_ID = @cid AND " + Open, new List<SqlParameter> { App.P("cid", cid, 50) });
            if (nerr != string.Empty || Convert.ToInt32(n.Rows[0][0], App.Inv) == 0)
            {
                response.SetElementContents("bl-mbody", await StHtml(c, s, cid));
                response.ExecuteScript("AdminBalancesJs.changed('" + Ui.Js("Nothing is owed, so there is no statement to send.") + "');");
                return response;
            }
            var (result, info) = await MailOne(c, s, cid);
            string msg = result == "sent" ? "Statement emailed to " + info + "." : result == "demo" ? "Demo mode: the statement to " + info + " was logged but not sent." : info;
            response.SetElementContents("bl-mbody", await StHtml(c, s, cid));
            response.ExecuteScript("AdminBalancesJs.changed('" + Ui.Js(msg) + "');");
            return response;
        }

        public async Task<ApiResponse> Print()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("bl-main", Denied());
                return response;
            }
            var (_, _, html, _) = await Letters.Statement(CleanId(GetDataValue("clientid")), await App.Settings());
            if (html == string.Empty)
            {
                Toast(response, "The statement could not be made.");
                return response;
            }
            response.SetElementContents("bl-print", html);
            response.ExecuteScript("AdminBalancesJs.doPrint();");
            return response;
        }

        public async Task<ApiResponse> Bulk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't email statements.");
                response.ExecuteScript("AdminBalancesJs.failed();");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<string> ids = (GetDataValue("clientids") ?? string.Empty).Split(',').Select(CleanId).Where(x => x != string.Empty).Distinct().Take(300).ToList();
            List<BalInv> invs = await Invoices(string.Empty, s);
            Dictionary<string, string> names = invs.GroupBy(i => i.ClientId).ToDictionary(g => g.Key, g => Owner(g.First().FirstName, g.First().LastName));
            int sent = 0, demo = 0, none = 0, paid = 0;
            List<string> fails = new List<string>();
            List<string> noMail = new List<string>();
            foreach (string id in ids)
            {
                if (!names.ContainsKey(id))
                {
                    paid++;
                    continue;
                }
                var (result, info) = await MailOne(c, s, id);
                if (result == "sent")
                {
                    sent++;
                }
                else if (result == "demo")
                {
                    demo++;
                }
                else if (result == "noemail")
                {
                    none++;
                    noMail.Add(names[id]);
                }
                else
                {
                    fails.Add(names[id] + ": " + info);
                }
            }
            await App.Log(c.U.StaffId, "email statements", sent + " sent, " + demo + " demo, " + none + " no email, " + fails.Count + " failed", fails.Count == 0);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Statements</h2><p class=\"bl-mlead\">" + Ui.Plural(ids.Count, "owner was", "owners were") + " selected.</p>");
            sb.Append("<div class=\"bl-res" + (App.Demo ? string.Empty : " bl-res3") + "\"><div class=\"bl-r-ok\"><span>Sent</span><b>" + sent + "</b></div>" + (App.Demo ? "<div class=\"bl-r-warn\"><span>Demo, not sent</span><b>" + demo + "</b></div>" : string.Empty) + "<div><span>No email</span><b>" + none + "</b></div><div class=\"" + (fails.Count > 0 ? "bl-r-err" : string.Empty) + "\"><span>Failed</span><b>" + fails.Count + "</b></div></div>");
            if (demo > 0)
            {
                sb.Append("<div class=\"bl-info\">This is the demo, so no email leaves the building. Each statement is logged as &ldquo;Demo: not sent&rdquo;.</div>");
            }
            if (paid > 0)
            {
                sb.Append("<p class=\"bl-dim bl-rnote\">" + Ui.Plural(paid, "owner had", "owners had") + " nothing owing any more and " + (paid == 1 ? "was" : "were") + " skipped.</p>");
            }
            if (noMail.Count > 0)
            {
                sb.Append("<h3>No email on file</h3><p class=\"bl-rnote\">" + Ui.Enc(string.Join(", ", noMail)) + "</p>");
            }
            if (fails.Count > 0)
            {
                sb.Append("<h3>Not sent</h3><ul class=\"bl-rows\">" + string.Concat(fails.Select(f => "<li class=\"bl-rl\"><span class=\"bl-rt\">" + Ui.Enc(f) + "</span></li>")) + "</ul>");
            }
            sb.Append("<div class=\"bl-mfoot\"><button type=\"button\" class=\"bl-btn bl-btn-accent\" onclick=\"AdminBalancesJs.closeModal()\">Done</button></div>");
            response.SetElementContents("bl-mbody", sb.ToString());
            response.ExecuteScript("AdminBalancesJs.bulkDone();");
            return response;
        }
    }
}
