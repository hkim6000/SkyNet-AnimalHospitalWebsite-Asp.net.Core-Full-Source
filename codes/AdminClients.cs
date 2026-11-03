using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminClients : WebPage
    {
        private const string Key = "clients";
        private const int Size = 25;

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Clients | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--cl-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("cl"))
                .Replace("{plhd_open}", Ui.Attr(CleanId(QueryValue("c"))))
                .Replace("{plhd_new}", (QueryValue("n") ?? string.Empty).Trim() == "1" ? "1" : string.Empty);
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

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminClientsJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminClientsJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"cl-panel cl-deny\"><h1>Clients</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"cl-btn cl-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminClientsJs.out();");
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
            response.SetElementContents("cl-menu", Auth.Menu("cl", "AdminClientsJs", "AdminClients", c));
            response.SetElementContents("cl-who", Auth.Who("cl", c));
            response.ExecuteScript("AdminClientsJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("cl-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cl-ptop\"><div><h1>Clients</h1><p>Pet owners, how to reach them and what they have coming up.</p></div>");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<div class=\"cl-acts\"><button type=\"button\" class=\"cl-btn cl-btn-accent\" onclick=\"AdminClientsJs.add()\">Add client</button></div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"cl-bar\"><label class=\"cl-field cl-grow2\"><span>Search</span><input type=\"search\" id=\"cl-term\" maxlength=\"80\" placeholder=\"Name, phone, email, client no. or pet name\" autocomplete=\"off\" oninput=\"AdminClientsJs.typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();AdminClientsJs.page(1);}\"></label>");
            sb.Append("<label class=\"cl-field\"><span>Show</span><select id=\"cl-show\" onchange=\"AdminClientsJs.page(1)\"><option value=\"active\">Active clients</option><option value=\"inactive\">Inactive clients</option><option value=\"all\">All clients</option></select></label></div>");
            sb.Append("<div id=\"cl-list\">" + await ListHtml(c, s, string.Empty, "active", 1) + "</div>");
            response.SetElementContents("cl-main", sb.ToString());
            response.ExecuteScript("AdminClientsJs.ready();");
            string open = CleanId(GetDataValue("openc"));
            if (open != string.Empty)
            {
                await Detail(response, c, s, open, false);
            }
            else if ((GetDataValue("openn") ?? string.Empty).Trim() == "1" && c.Can(Key, 'A'))
            {
                response.SetElementContents("cl-mbody", FormHtml(c, null));
                response.ExecuteScript("AdminClientsJs.shown();");
            }
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
                response.SetElementContents("cl-main", Denied());
                return response;
            }
            string show = Val("show", 10);
            int.TryParse(GetDataValue("pg"), out int pg);
            response.SetElementContents("cl-list", await ListHtml(c, await App.Settings(), Val("term", 80), show, Math.Max(pg, 1)));
            response.ExecuteScript("AdminClientsJs.ready();");
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

        private static string When(DateTime d, Dictionary<string, string> s)
        {
            return d.ToString("ddd MMM d", Ui.Us) + " &middot; " + Ui.Hm(d, App.S(s, "clock24") == "1");
        }

        private static string Chip(string status)
        {
            string text = status switch
            {
                "booked" => "Booked",
                "confirmed" => "Confirmed",
                "checkedin" => "Checked in",
                "noshow" => "No-show",
                "cancelled" => "Cancelled",
                "waiting" => "Waiting",
                "inroom" => "In a room",
                "checkout" => "At checkout",
                "closed" => "Done",
                "billed" => "Billed",
                "partial" => "Part paid",
                _ => status
            };
            return "<span class=\"cl-st cl-st-" + Ui.Attr(status) + "\">" + Ui.Enc(text) + "</span>";
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
                _ => fix ? "Fixed" : string.Empty
            };
        }

        private static bool Money(Ctx c)
        {
            return c.Can("invoices", 'V') || c.Can("balances", 'V');
        }

        private static async Task<string> ListHtml(Ctx c, Dictionary<string, string> s, string term, string show, int pg)
        {
            DateTime now = App.Now(s);
            string own = " AND (@own = 0 OR X.STAFF_ID = @sid)";
            string digits = Ui.Digits(term);
            int skip = 0;
            List<SqlParameter> Ps() => new List<SqlParameter>
            {
                App.P("t", term, 80),
                App.P("tl", Like(term), 300),
                App.P("dl", Like(digits), 300),
                App.P("dg", digits.Length >= 3 ? digits : string.Empty, 40),
                App.PDate("now", now),
                App.PBit("own", c.U.OwnOnly),
                App.P("sid", c.U.StaffId, 50),
                App.PInt("skip", skip),
                App.PInt("take", Size)
            };
            string where = " WHERE (@t = N'' OR C.FIRST_NAME LIKE @tl OR C.LAST_NAME LIKE @tl OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @tl OR C.EMAIL LIKE @tl OR C.CLIENT_NO LIKE @tl OR EXISTS (SELECT 1 FROM PET P WHERE P.CLIENT_ID = C.CLIENT_ID AND P.PET_NAME LIKE @tl) OR (@dg <> N'' AND (" + Dig("C.PHONE") + " LIKE @dl OR " + Dig("C.PHONE2") + " LIKE @dl)))";
            if (show == "inactive")
            {
                where += " AND C.ACTIVE = 0";
            }
            else if (show != "all")
            {
                where += " AND C.ACTIVE = 1";
            }
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM CLIENT C" + where, Ps());
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            int page = Ui.PageOf(pg.ToString(Ui.Inv), Size, total);
            skip = (page - 1) * Size;
            string sql = "SELECT C.CLIENT_ID AS ClientId, C.CLIENT_NO AS ClientNo, C.FIRST_NAME AS FirstName, C.LAST_NAME AS LastName, C.PHONE AS Phone, C.EMAIL AS Email, C.ALERT AS Alert, C.ACTIVE AS Active, " +
                "ISNULL((SELECT STRING_AGG(P.PET_NAME + N' (' + ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) + N')', N', ') WITHIN GROUP (ORDER BY P.PET_NAME) FROM PET P LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY WHERE P.CLIENT_ID = C.CLIENT_ID AND P.ACTIVE = 1), N'') AS Pets, " +
                "(SELECT MAX(X.CHECKIN_AT) FROM VISIT X WHERE X.CLIENT_ID = C.CLIENT_ID AND X.STATUS <> N'cancelled' AND X.VISIT_TYPE <> N'boarding'" + own + ") AS LastVisit, " +
                "(SELECT MIN(X.START_AT) FROM APPOINTMENT X WHERE X.CLIENT_ID = C.CLIENT_ID AND X.START_AT >= @now AND X.STATUS IN (N'booked', N'confirmed') AND X.VISIT_TYPE <> N'boarding'" + own + ") AS NextAppt, " +
                "ISNULL((SELECT SUM(I.BALANCE) FROM INVOICE I WHERE I.CLIENT_ID = C.CLIENT_ID AND I.STATUS IN (N'billed', N'partial')), 0) AS Balance, 0 AS Total " +
                "FROM CLIENT C" + where + " ORDER BY C.LAST_NAME, C.FIRST_NAME, C.CLIENT_NO OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
            var (rows, emsg) = await Helper.GetListT<ClListRow>(sql, Ps());
            if (emsg != string.Empty || rows == null)
            {
                return "<div class=\"cl-panel\"><p>The client list could not be loaded. Try again in a moment.</p></div>";
            }
            if (rows.Count == 0)
            {
                string none = term != string.Empty ? "No client matches &ldquo;" + Ui.Enc(term) + "&rdquo;." : show == "inactive" ? "There are no inactive clients." : "There are no clients yet.";
                return "<div class=\"cl-panel cl-empty\"><p>" + none + "</p></div>";
            }
            bool money = Money(c);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cl-tw\"><table class=\"cl-table\"><thead><tr><th>Client</th><th>Phone</th><th>Email</th><th>Pets</th><th>Last visit</th>" + (money ? "<th class=\"cl-num\">Open balance</th>" : string.Empty) + "<th>Next appointment</th></tr></thead><tbody>");
            foreach (ClListRow r in rows)
            {
                string js = "AdminClientsJs.open('" + Ui.Js(r.ClientId) + "')";
                sb.Append("<tr class=\"cl-row" + (r.Active ? string.Empty : " cl-off") + "\" tabindex=\"0\" onclick=\"" + js + "\" onkeydown=\"if(event.key==='Enter'){" + js + ";}\">");
                sb.Append("<td class=\"cl-cname\"><b>" + Ui.Enc(r.LastName + ", " + r.FirstName) + "</b><small>" + Ui.Enc(r.ClientNo) + (r.Active ? string.Empty : " &middot; inactive") + "</small>" + (r.Alert != string.Empty ? "<span class=\"cl-tag cl-tag-alert\" title=\"" + Ui.Attr(r.Alert) + "\">Alert</span>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Phone\" class=\"cl-nw\">" + (r.Phone != string.Empty ? Ui.Enc(r.Phone) : "<i class=\"cl-dim\">None</i>") + "</td>");
                sb.Append("<td data-l=\"Email\" class=\"cl-mail\">" + (r.Email != string.Empty ? Ui.Enc(r.Email) : "<i class=\"cl-dim\">None</i>") + "</td>");
                sb.Append("<td data-l=\"Pets\">" + (r.Pets != string.Empty ? Ui.Enc(r.Pets) : "<i class=\"cl-dim\">No pets</i>") + "</td>");
                sb.Append("<td data-l=\"Last visit\" class=\"cl-nw\">" + (r.LastVisit.HasValue ? Short(r.LastVisit.Value) : "<i class=\"cl-dim\">Never</i>") + "</td>");
                if (money)
                {
                    sb.Append("<td data-l=\"Open balance\" class=\"cl-num\">" + (r.Balance > 0 ? "<b class=\"cl-owe\">" + Ui.Money(r.Balance) + "</b>" : "<span class=\"cl-dim\">$0.00</span>") + "</td>");
                }
                sb.Append("<td data-l=\"Next appointment\" class=\"cl-nw\">" + (r.NextAppt.HasValue ? When(r.NextAppt.Value, s) : "<i class=\"cl-dim\">None</i>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("cl", page, Size, total, "client", "clients", "AdminClientsJs.page"));
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("cl-main", Denied());
                return response;
            }
            await Detail(response, c, await App.Settings(), CleanId(GetDataValue("clientid")), false);
            return response;
        }

        private static async Task<ClClient?> Client(string id)
        {
            var (row, emsg) = await Helper.GetOneT<ClClient>("SELECT C.CLIENT_ID AS ClientId, C.CLIENT_NO AS ClientNo, C.FIRST_NAME AS FirstName, C.LAST_NAME AS LastName, C.PHONE AS Phone, C.PHONE2 AS Phone2, C.EMAIL AS Email, C.CONTACT_PREF AS ContactPref, C.ADDRESS1 AS Address1, C.CITY AS City, C.STATE AS State, C.ZIP AS Zip, C.NOTE AS Note, C.ALERT AS Alert, C.ACTIVE AS Active, C.CREATED_UTC AS CreatedUtc, ISNULL(S.STAFF_NAME, N'') AS CreatedName FROM CLIENT C LEFT JOIN STAFF S ON S.STAFF_ID = C.CREATED_BY WHERE C.CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static string PrefText(string pref)
        {
            return pref switch { "email" => "Email", "text" => "Text message", _ => "Phone call" };
        }

        private static async Task Detail(ApiResponse response, Ctx c, Dictionary<string, string> s, string id, bool added)
        {
            ClClient? cl = id == string.Empty ? null : await Client(id);
            if (cl == null)
            {
                response.SetElementContents("cl-mbody", "<h2>Client not found</h2><p>This client may have been deleted. Close this window and search again.</p>");
                response.ExecuteScript("AdminClientsJs.shown('');");
                return;
            }
            DateTime now = App.Now(s);
            List<SqlParameter> P() => new List<SqlParameter> { App.P("cid", cl.ClientId, 50), App.PDate("now", now), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) };
            string own = " AND (@own = 0 OR X.STAFF_ID = @sid)";
            var (pets, _) = await Helper.GetListT<ClPetRow>("SELECT P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SEX AS Sex, P.FIXED AS Fixed, P.BIRTH_DATE AS BirthDate, P.ALERT AS Alert, P.PHOTO AS Photo, P.ACTIVE AS Active, P.DECEASED_DATE AS DeceasedDate FROM PET P LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY WHERE P.CLIENT_ID = @cid ORDER BY P.ACTIVE DESC, P.PET_NAME", P());
            var (appts, _) = await Helper.GetListT<ClApptRow>("SELECT TOP 10 X.APPT_ID AS ApptId, X.START_AT AS StartAt, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, ISNULL(S.STAFF_NAME, N'') AS StaffName, X.STATUS AS Status FROM APPOINTMENT X LEFT JOIN PET P ON P.PET_ID = X.PET_ID LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = X.BOOKTYPE_ID LEFT JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.CLIENT_ID = @cid AND X.START_AT >= @now AND X.STATUS IN (N'booked', N'confirmed') AND X.VISIT_TYPE <> N'boarding'" + own + " ORDER BY X.START_AT", P());
            var (visits, _) = await Helper.GetListT<ClVisitRow>("SELECT TOP 10 X.VISIT_ID AS VisitId, X.CHECKIN_AT AS CheckinAt, ISNULL(P.PET_NAME, N'') AS PetName, X.REASON AS Reason, X.STATUS AS Status, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM VISIT X LEFT JOIN PET P ON P.PET_ID = X.PET_ID LEFT JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.CLIENT_ID = @cid AND X.VISIT_TYPE <> N'boarding'" + own + " ORDER BY X.CHECKIN_AT DESC", P());
            List<ClInvRow>? invs = null;
            if (Money(c))
            {
                var (ir, _) = await Helper.GetListT<ClInvRow>("SELECT INVOICE_ID AS InvoiceId, INVOICE_NO AS InvoiceNo, STATUS AS Status, TOTAL AS Total, BALANCE AS Balance, DUE_DATE AS DueDate, CREATED_UTC AS CreatedUtc FROM INVOICE WHERE CLIENT_ID = @cid AND STATUS IN (N'billed', N'partial') AND BALANCE > 0 ORDER BY CREATED_UTC", P());
                invs = ir;
            }
            pets ??= new List<ClPetRow>();
            appts ??= new List<ClApptRow>();
            visits ??= new List<ClVisitRow>();
            string cid = Ui.Js(cl.ClientId);
            StringBuilder sb = new StringBuilder();
            if (added)
            {
                sb.Append("<div class=\"cl-done\"><div><b>" + Ui.Enc(cl.FirstName + " " + cl.LastName) + " is saved as " + Ui.Enc(cl.ClientNo) + ".</b><span>Add their pet now so you can book a visit.</span></div>" + (c.Can("pets", 'A') ? "<a class=\"cl-btn cl-btn-accent cl-btn-sm\" href=\"AdminPets?c=" + Uri.EscapeDataString(cl.ClientId) + "&amp;n=1\">Add a pet</a>" : string.Empty) + "</div>");
            }
            sb.Append("<div class=\"cl-dhead\"><div><h2>" + Ui.Enc(cl.FirstName + " " + cl.LastName) + "</h2><p class=\"cl-sub\">" + Ui.Enc(cl.ClientNo) + (cl.Active ? string.Empty : " &middot; <span class=\"cl-st cl-st-off\">Inactive</span>") + " &middot; client since " + Ui.Time(cl.CreatedUtc, "day") + "</p></div><div class=\"cl-acts\">");
            if (c.Can(Key, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"cl-btn cl-btn-sm\" onclick=\"AdminClientsJs.edit('" + cid + "')\">Edit</button>");
            }
            if (c.Can("pets", 'A'))
            {
                sb.Append("<a class=\"cl-btn cl-btn-sm cl-btn-ghost\" href=\"AdminPets?c=" + Uri.EscapeDataString(cl.ClientId) + "&amp;n=1\">Add pet</a>");
            }
            if (c.Can("invoices", 'V'))
            {
                sb.Append("<a class=\"cl-btn cl-btn-sm cl-btn-ghost\" href=\"AdminInvoices?c=" + Uri.EscapeDataString(cl.ClientId) + "\">Invoices</a>");
            }
            if (c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"cl-btn cl-btn-sm cl-btn-danger\" onclick=\"AdminClientsJs.ask('" + cid + "')\">Delete</button>");
            }
            sb.Append("</div></div>");
            if (cl.Alert != string.Empty)
            {
                sb.Append("<div class=\"cl-alert\" role=\"alert\"><b>Alert</b><span>" + Ui.Enc(cl.Alert) + "</span></div>");
            }
            sb.Append("<div class=\"cl-dgrid\"><section class=\"cl-box\"><h3>Contact</h3><dl class=\"cl-dl\">");
            sb.Append("<dt>Phone</dt><dd>" + (cl.Phone != string.Empty ? "<a href=\"tel:" + Ui.Attr(Ui.Digits(cl.Phone)) + "\">" + Ui.Enc(cl.Phone) + "</a>" : "<i class=\"cl-dim\">None</i>") + "</dd>");
            if (cl.Phone2 != string.Empty)
            {
                sb.Append("<dt>Other phone</dt><dd><a href=\"tel:" + Ui.Attr(Ui.Digits(cl.Phone2)) + "\">" + Ui.Enc(cl.Phone2) + "</a></dd>");
            }
            sb.Append("<dt>Email</dt><dd class=\"cl-mail\">" + (cl.Email != string.Empty ? "<a href=\"mailto:" + Ui.Attr(cl.Email) + "\">" + Ui.Enc(cl.Email) + "</a>" : "<i class=\"cl-dim\">None</i>") + "</dd>");
            sb.Append("<dt>Prefers</dt><dd>" + PrefText(cl.ContactPref) + "</dd>");
            string addr = string.Join(", ", new[] { cl.Address1, cl.City, (cl.State + " " + cl.Zip).Trim() }.Where(x => x != string.Empty));
            sb.Append("<dt>Address</dt><dd>" + (addr != string.Empty ? Ui.Enc(addr) : "<i class=\"cl-dim\">None</i>") + "</dd></dl>");
            if (cl.Note != string.Empty)
            {
                sb.Append("<div class=\"cl-note\"><b>Note</b><p>" + Ui.Enc(cl.Note).Replace("\n", "<br>") + "</p></div>");
            }
            sb.Append("</section><section class=\"cl-box\"><h3>Pets</h3>");
            if (pets.Count == 0)
            {
                sb.Append("<p class=\"cl-dim\">No pets on file yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"cl-pets\">");
                foreach (ClPetRow p in pets)
                {
                    List<string> bits = new List<string> { p.SpeciesName };
                    if (p.Breed != string.Empty)
                    {
                        bits.Add(p.Breed);
                    }
                    string sex = SexText(p.Sex, p.Fixed);
                    if (sex != string.Empty)
                    {
                        bits.Add(sex);
                    }
                    string age = Age(p.BirthDate, now.Date, p.DeceasedDate);
                    if (age != string.Empty)
                    {
                        bits.Add(age);
                    }
                    string name = c.Can("pets", 'V') ? "<a href=\"AdminPets?p=" + Uri.EscapeDataString(p.PetId) + "\">" + Ui.Enc(p.PetName) + "</a>" : Ui.Enc(p.PetName);
                    sb.Append("<li class=\"" + (p.Active ? string.Empty : "cl-off") + "\">" + Ui.Avatar("cl", p.PetId, p.PetName, p.Photo, "pet") + "<div class=\"cl-pinfo\"><b>" + name + "</b><small>" + Ui.Enc(string.Join(" · ", bits)) + "</small>");
                    if (p.DeceasedDate.HasValue)
                    {
                        sb.Append("<span class=\"cl-tag\">Passed away " + Short(p.DeceasedDate.Value) + "</span>");
                    }
                    else if (!p.Active)
                    {
                        sb.Append("<span class=\"cl-tag\">Inactive</span>");
                    }
                    if (p.Alert != string.Empty)
                    {
                        sb.Append("<span class=\"cl-tag cl-tag-alert\">" + Ui.Enc(p.Alert) + "</span>");
                    }
                    sb.Append("</div>");
                    if (p.Active && c.Can("appts", 'A'))
                    {
                        sb.Append("<a class=\"cl-btn cl-btn-sm cl-btn-ghost\" href=\"AdminAppts?p=" + Uri.EscapeDataString(p.PetId) + "\">Book</a>");
                    }
                    sb.Append("</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div>");
            sb.Append("<section class=\"cl-box\"><h3>Upcoming appointments</h3>");
            if (appts.Count == 0)
            {
                sb.Append("<p class=\"cl-dim\">Nothing booked.</p>");
            }
            else
            {
                sb.Append("<ul class=\"cl-rows\">");
                foreach (ClApptRow a in appts)
                {
                    string inner = "<span class=\"cl-rw\">" + When(a.StartAt, s) + "</span><span class=\"cl-rt\"><b>" + Ui.Enc(a.PetName) + "</b> " + Ui.Enc(a.BookName) + (a.StaffName != string.Empty ? " <small>with " + Ui.Enc(a.StaffName) + "</small>" : string.Empty) + "</span>" + Chip(a.Status);
                    sb.Append("<li>" + (c.Can("appts", 'V') ? "<a class=\"cl-rl\" href=\"AdminAppts?a=" + Uri.EscapeDataString(a.ApptId) + "\">" + inner + "</a>" : "<div class=\"cl-rl\">" + inner + "</div>") + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section><section class=\"cl-box\"><h3>Recent visits</h3>");
            if (visits.Count == 0)
            {
                sb.Append("<p class=\"cl-dim\">No visits yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"cl-rows\">");
                foreach (ClVisitRow v in visits)
                {
                    string inner = "<span class=\"cl-rw\">" + Short(v.CheckinAt) + "</span><span class=\"cl-rt\"><b>" + Ui.Enc(v.PetName) + "</b> " + Ui.Enc(v.Reason != string.Empty ? v.Reason : "Visit") + (v.StaffName != string.Empty ? " <small>with " + Ui.Enc(v.StaffName) + "</small>" : string.Empty) + "</span>" + Chip(v.Status);
                    sb.Append("<li>" + (c.Can("visits", 'V') ? "<a class=\"cl-rl\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">" + inner + "</a>" : "<div class=\"cl-rl\">" + inner + "</div>") + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section>");
            if (invs != null)
            {
                sb.Append("<section class=\"cl-box\"><h3>Invoices with a balance</h3>");
                if (invs.Count == 0)
                {
                    sb.Append("<p class=\"cl-dim\">Nothing owed.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"cl-rows\">");
                    foreach (ClInvRow i in invs)
                    {
                        bool late = i.DueDate.HasValue && i.DueDate.Value.Date < now.Date;
                        string inner = "<span class=\"cl-rw\">" + Ui.Enc(i.InvoiceNo) + "</span><span class=\"cl-rt\">" + Ui.Time(i.CreatedUtc, "day") + " &middot; total " + Ui.Money(i.Total) + (i.DueDate.HasValue ? " <small class=\"" + (late ? "cl-late" : string.Empty) + "\">" + (late ? "past due since " : "due ") + Short(i.DueDate.Value) + "</small>" : string.Empty) + "</span><b class=\"cl-owe\">" + Ui.Money(i.Balance) + "</b>";
                        sb.Append("<li>" + (c.Can("invoices", 'V') ? "<a class=\"cl-rl\" href=\"AdminInvoices?c=" + Uri.EscapeDataString(cl.ClientId) + "\">" + inner + "</a>" : "<div class=\"cl-rl\">" + inner + "</div>") + "</li>");
                    }
                    sb.Append("</ul><p class=\"cl-sum\">Open balance <b class=\"cl-owe\">" + Ui.Money(invs.Sum(x => x.Balance)) + "</b></p>");
                }
                sb.Append("</section>");
            }
            response.SetElementContents("cl-mbody", sb.ToString());
            response.ExecuteScript("AdminClientsJs.shown('" + cid + "');");
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            if (!c.Can(Key, id == string.Empty ? 'A' : 'E'))
            {
                Toast(response, id == string.Empty ? "You can't add clients." : "You can't change clients.");
                return response;
            }
            ClClient? cl = null;
            if (id != string.Empty)
            {
                cl = await Client(id);
                if (cl == null)
                {
                    Toast(response, "This client was not found. It may have been deleted.");
                    return response;
                }
            }
            response.SetElementContents("cl-mbody", FormHtml(c, cl));
            response.ExecuteScript("AdminClientsJs.shown();");
            return response;
        }

        private static string Field(string id, string label, string value, int max, string type = "text", string extra = "", string err = "")
        {
            return "<label class=\"cl-field" + (extra != string.Empty ? " " + extra : string.Empty) + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"cl-f-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\" autocomplete=\"off\">" + (err != string.Empty ? "<em class=\"cl-err\" id=\"cl-e-" + err + "\"></em>" : string.Empty) + "</label>";
        }

        private static string FormHtml(Ctx c, ClClient? cl)
        {
            bool add = cl == null;
            ClClient v = cl ?? new ClClient { ContactPref = "phone", Active = true };
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "New client" : "Edit " + Ui.Enc(v.FirstName + " " + v.LastName)) + "</h2>");
            sb.Append("<form class=\"cl-form\" onsubmit=\"return AdminClientsJs.save(0)\" novalidate><input type=\"hidden\" id=\"cl-f-id\" value=\"" + Ui.Attr(v.ClientId) + "\">");
            sb.Append("<div class=\"cl-fgrid\">");
            sb.Append(Field("first", "First name", v.FirstName, 60, "text", string.Empty, "first"));
            sb.Append(Field("last", "Last name", v.LastName, 60, "text", string.Empty, "last"));
            sb.Append(Field("phone", "Phone", v.Phone, 30, "tel", string.Empty, "phone"));
            sb.Append(Field("phone2", "Other phone", v.Phone2, 30, "tel", string.Empty, "phone2"));
            sb.Append(Field("email", "Email", v.Email, 120, "email", string.Empty, "email"));
            sb.Append("<label class=\"cl-field\"><span>How they like to be reached</span><select id=\"cl-f-pref\">" + Ui.Opts(new[] { ("phone", "Phone call"), ("email", "Email"), ("text", "Text message") }, v.ContactPref) + "</select><em class=\"cl-err\" id=\"cl-e-pref\"></em></label>");
            sb.Append(Field("addr", "Street address", v.Address1, 120, "text", "cl-span2"));
            sb.Append(Field("city", "City", v.City, 60));
            sb.Append("<div class=\"cl-fpair\">" + Field("state", "State", v.State, 20) + Field("zip", "ZIP", v.Zip, 12) + "</div>");
            sb.Append("<label class=\"cl-field cl-span2\"><span>Alert <small>shown in red to everyone who opens this client</small></span><input type=\"text\" id=\"cl-f-alert\" maxlength=\"200\" value=\"" + Ui.Attr(v.Alert) + "\" placeholder=\"For example: prefers no phone calls before 10 am\"></label>");
            sb.Append("<label class=\"cl-field cl-span2\"><span>Note</span><textarea id=\"cl-f-note\" rows=\"3\" maxlength=\"1000\">" + Ui.Enc(v.Note) + "</textarea></label>");
            if (!add)
            {
                sb.Append("<label class=\"cl-check cl-span2\"><input type=\"checkbox\" id=\"cl-f-active\"" + (v.Active ? " checked" : string.Empty) + "><span>Active client</span></label>");
            }
            sb.Append("</div><em class=\"cl-err\" id=\"cl-e-form\"></em><div id=\"cl-dup\"></div>");
            sb.Append("<div class=\"cl-facts\"><button type=\"submit\" class=\"cl-btn cl-btn-accent\">" + (add ? "Add client" : "Save changes") + "</button><button type=\"button\" class=\"cl-btn cl-btn-ghost\" onclick=\"" + (add ? "AdminClientsJs.closeModal()" : "AdminClientsJs.open('" + Ui.Js(v.ClientId) + "')") + "\">Cancel</button></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, add ? "add client" : "edit client", "not allowed", false);
                Toast(response, add ? "You can't add clients." : "You can't change clients.");
                return response;
            }
            ClClient? old = null;
            if (!add)
            {
                old = await Client(id);
                if (old == null)
                {
                    Toast(response, "This client was not found. It may have been deleted.");
                    return response;
                }
            }
            string first = Ui.OneLine(GetDataValue("first"), 60);
            string last = Ui.OneLine(GetDataValue("last"), 60);
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string phone2 = Ui.CleanPhone(GetDataValue("phone2"));
            string email = Val("email", 120);
            string pref = Val("pref", 10);
            if (pref != "email" && pref != "text")
            {
                pref = "phone";
            }
            string addr = Ui.OneLine(GetDataValue("addr"), 120);
            string city = Ui.OneLine(GetDataValue("city"), 60);
            string state = Ui.OneLine(GetDataValue("state"), 20);
            string zip = Ui.OneLine(GetDataValue("zip"), 12);
            string alert = Ui.OneLine(GetDataValue("alert"), 200);
            string note = Val("note", 1000);
            bool active = add || (GetDataValue("active") ?? string.Empty).Trim() == "1";
            bool force = (GetDataValue("force") ?? string.Empty).Trim() == "1";
            Dictionary<string, string> errs = new Dictionary<string, string> { { "first", string.Empty }, { "last", string.Empty }, { "phone", string.Empty }, { "phone2", string.Empty }, { "email", string.Empty }, { "pref", string.Empty }, { "form", string.Empty } };
            if (first == string.Empty)
            {
                errs["first"] = "Enter the first name.";
            }
            if (last == string.Empty)
            {
                errs["last"] = "Enter the last name.";
            }
            if (phone != string.Empty && Ui.Digits(phone).Length < 7)
            {
                errs["phone"] = "Enter the full phone number.";
            }
            if (phone2 != string.Empty && Ui.Digits(phone2).Length < 7)
            {
                errs["phone2"] = "Enter the full phone number.";
            }
            if (email != string.Empty && !Ui.Email(email))
            {
                errs["email"] = "This email address doesn't look right.";
            }
            if (phone == string.Empty && email == string.Empty)
            {
                errs["phone"] = "Enter a phone number or an email address.";
            }
            if (pref == "email" && email == string.Empty && errs["phone"] == string.Empty)
            {
                errs["pref"] = "Add an email address, or pick another way to reach them.";
            }
            if (pref != "email" && phone == string.Empty && phone2 == string.Empty && errs["phone"] == string.Empty)
            {
                errs["pref"] = "Add a phone number, or pick email.";
            }
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("cl-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                response.SetElementContents("cl-dup", string.Empty);
                return response;
            }
            if (!force)
            {
                string dup = await Duplicates(id, phone, phone2, email);
                if (dup != string.Empty)
                {
                    response.SetElementContents("cl-dup", dup);
                    response.ExecuteScript("AdminClientsJs.dupShown();");
                    return response;
                }
            }
            string newId = add ? App.NewId() : id;
            List<SqlParameter> Ps() => new List<SqlParameter>
            {
                App.P("cid", newId, 50),
                App.P("first", first, 60),
                App.P("last", last, 60),
                App.P("phone", phone, 30),
                App.P("phone2", phone2, 30),
                App.P("email", email, 120),
                App.P("pref", pref, 10),
                App.P("addr", addr, 120),
                App.P("city", city, 60),
                App.P("state", state, 20),
                App.P("zip", zip, 12),
                App.P("note", note, 1000),
                App.P("alert", alert, 200),
                App.PBit("active", active),
                App.P("me", c.U.StaffId, 50)
            };
            string result;
            if (add)
            {
                string sql = "INSERT INTO CLIENT (CLIENT_ID, CLIENT_NO, FIRST_NAME, LAST_NAME, PHONE, PHONE2, EMAIL, CONTACT_PREF, ADDRESS1, CITY, STATE, ZIP, NOTE, ALERT, ACTIVE, CREATED_UTC, CREATED_BY, SYSDTE) " +
                    "SELECT @cid, N'C' + RIGHT(N'0000' + CAST(X.N AS NVARCHAR(10)), CASE WHEN X.N > 99999 THEN LEN(CAST(X.N AS NVARCHAR(10))) ELSE 5 END), @first, @last, @phone, @phone2, @email, @pref, @addr, @city, @state, @zip, @note, @alert, 1, SYSUTCDATETIME(), @me, GETDATE() " +
                    "FROM (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(CLIENT_NO, 2, 10) AS INT)), 1000) + 1 AS N FROM CLIENT WITH (UPDLOCK, HOLDLOCK) WHERE CLIENT_NO LIKE N'C[0-9]%' AND SUBSTRING(CLIENT_NO, 2, 20) NOT LIKE N'%[^0-9]%') X";
                result = await Helper.PutData(new List<string> { sql }, Ps());
                if (Helper.IsError(result) && (result.Contains("UX_CLIENT_NO", StringComparison.OrdinalIgnoreCase) || result.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || result.Contains("unique", StringComparison.OrdinalIgnoreCase)))
                {
                    result = await Helper.PutData(new List<string> { sql }, Ps());
                }
            }
            else
            {
                result = await Helper.PutData(new List<string> { "UPDATE CLIENT SET FIRST_NAME = @first, LAST_NAME = @last, PHONE = @phone, PHONE2 = @phone2, EMAIL = @email, CONTACT_PREF = @pref, ADDRESS1 = @addr, CITY = @city, STATE = @state, ZIP = @zip, NOTE = @note, ALERT = @alert, ACTIVE = @active, SYSDTE = GETDATE() WHERE CLIENT_ID = @cid" }, Ps());
                if (!Helper.IsError(result) && !active)
                {
                    await Helper.PutData(new List<string>
                    {
                        "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) SELECT CONVERT(NVARCHAR(50), NEWID()), APPT_ID, SYSUTCDATETIME(), @me, N'Cancelled: client marked inactive' FROM APPOINTMENT WHERE CLIENT_ID = @cid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today",
                        "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = N'Client marked inactive', SYSDTE = GETDATE() WHERE CLIENT_ID = @cid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today"
                    }, new List<SqlParameter> { App.P("cid", newId, 50), App.P("me", c.U.StaffId, 50), App.PDate("today", App.Now(await App.Settings()).Date) });
                }
            }
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, add ? "add client" : "edit client", first + " " + last + " failed", false);
                response.SetElementContents("cl-e-form", "The client could not be saved. Try again.");
                return response;
            }
            ClClient? saved = await Client(newId);
            await App.Log(c.U.StaffId, add ? "added client" : "edited client", (saved?.ClientNo ?? string.Empty) + " " + first + " " + last + (force ? " (duplicate kept)" : string.Empty), true);
            await Detail(response, c, await App.Settings(), newId, add);
            response.ExecuteScript("AdminClientsJs.saved(" + (add ? "true" : "false") + ");");
            return response;
        }

        private static async Task<string> Duplicates(string id, string phone, string phone2, string email)
        {
            List<string> digits = new[] { Ui.Digits(phone), Ui.Digits(phone2) }.Where(d => d.Length >= 7).Distinct().ToList();
            if (digits.Count == 0 && email == string.Empty)
            {
                return string.Empty;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("cid", id, 50), App.P("email", email, 120), App.P("d0", digits.Count > 0 ? digits[0] : "-", 40), App.P("d1", digits.Count > 1 ? digits[1] : "-", 40) };
            string sql = "SELECT TOP 5 CLIENT_ID AS ClientId, CLIENT_NO AS ClientNo, FIRST_NAME AS FirstName, LAST_NAME AS LastName, PHONE AS Phone, PHONE2 AS Phone2, EMAIL AS Email FROM CLIENT WHERE CLIENT_ID <> @cid AND ((@email <> N'' AND EMAIL = @email) OR " + Dig("PHONE") + " IN (@d0, @d1) OR " + Dig("PHONE2") + " IN (@d0, @d1)) ORDER BY LAST_NAME, FIRST_NAME";
            var (rows, emsg) = await Helper.GetListT<ClDupRow>(sql, ps);
            if (emsg != string.Empty || rows == null || rows.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"cl-warn\" role=\"alert\"><b>This may be a client you already have</b><ul>");
            foreach (ClDupRow r in rows)
            {
                List<string> why = new List<string>();
                if (email != string.Empty && string.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    why.Add("same email");
                }
                if (digits.Contains(Ui.Digits(r.Phone)) || digits.Contains(Ui.Digits(r.Phone2)))
                {
                    why.Add("same phone number");
                }
                sb.Append("<li><b>" + Ui.Enc(r.FirstName + " " + r.LastName) + "</b> (" + Ui.Enc(r.ClientNo) + ") &middot; " + Ui.Enc(string.Join(" and ", why)) + "</li>");
            }
            sb.Append("</ul><p>Check it is a different person before you save. Families sometimes share a phone number.</p><div class=\"cl-facts\"><button type=\"button\" class=\"cl-btn cl-btn-sm\" onclick=\"AdminClientsJs.save(1)\">Save anyway</button></div></div>");
            return sb.ToString();
        }

        private static async Task<ClUseRow> Use(string id)
        {
            var (row, _) = await Helper.GetOneT<ClUseRow>("SELECT (SELECT COUNT(*) FROM PET WHERE CLIENT_ID = @cid) AS Pets, (SELECT COUNT(*) FROM VISIT WHERE CLIENT_ID = @cid OR PET_ID IN (SELECT PET_ID FROM PET WHERE CLIENT_ID = @cid)) AS Visits, (SELECT COUNT(*) FROM APPOINTMENT WHERE CLIENT_ID = @cid OR PET_ID IN (SELECT PET_ID FROM PET WHERE CLIENT_ID = @cid)) AS Appts, (SELECT COUNT(*) FROM INVOICE WHERE CLIENT_ID = @cid) AS Invoices, (SELECT COUNT(*) FROM PAYMENT WHERE CLIENT_ID = @cid) AS Payments", new List<SqlParameter> { App.P("cid", id, 50) });
            return row ?? new ClUseRow { Visits = 1 };
        }

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                Toast(response, "You can't delete clients.");
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            ClClient? cl = id == string.Empty ? null : await Client(id);
            if (cl == null)
            {
                Toast(response, "This client was not found. It may have been deleted.");
                return response;
            }
            ClUseRow u = await Use(id);
            string cid = Ui.Js(id);
            string name = Ui.Enc(cl.FirstName + " " + cl.LastName);
            StringBuilder sb = new StringBuilder();
            if (u.Visits > 0 || u.Appts > 0 || u.Invoices > 0 || u.Payments > 0)
            {
                List<string> why = new List<string>();
                if (u.Visits > 0)
                {
                    why.Add(Ui.Plural(u.Visits, "visit", "visits"));
                }
                if (u.Appts > 0)
                {
                    why.Add(Ui.Plural(u.Appts, "appointment", "appointments"));
                }
                if (u.Invoices > 0)
                {
                    why.Add(Ui.Plural(u.Invoices, "invoice", "invoices"));
                }
                if (u.Payments > 0)
                {
                    why.Add(Ui.Plural(u.Payments, "payment", "payments"));
                }
                sb.Append("<h2>" + name + " can't be deleted</h2><p class=\"cl-lead\">This client has history: " + Ui.Enc(string.Join(", ", why)) + ". Medical and money records must be kept.</p>");
                if (c.Can(Key, 'E') && cl.Active)
                {
                    sb.Append("<p>Mark the client inactive instead. They drop off the client list but their history stays.</p><div class=\"cl-facts\"><button type=\"button\" class=\"cl-btn cl-btn-accent\" onclick=\"AdminClientsJs.retire('" + cid + "')\">Mark inactive</button>");
                }
                else
                {
                    sb.Append("<div class=\"cl-facts\">");
                }
                sb.Append("<button type=\"button\" class=\"cl-btn cl-btn-ghost\" onclick=\"AdminClientsJs.open('" + cid + "')\">Back</button></div>");
            }
            else
            {
                sb.Append("<h2>Delete " + name + "?</h2><p class=\"cl-lead\">" + (u.Pets > 0 ? "Their " + Ui.Plural(u.Pets, "pet", "pets") + ", with weights and vaccines, are deleted too. " : string.Empty) + "This can't be undone.</p>");
                sb.Append("<div class=\"cl-facts\"><button type=\"button\" class=\"cl-btn cl-btn-danger\" onclick=\"AdminClientsJs.remove('" + cid + "')\">Delete for good</button><button type=\"button\" class=\"cl-btn cl-btn-ghost\" onclick=\"AdminClientsJs.open('" + cid + "')\">Keep</button></div>");
            }
            response.SetElementContents("cl-mbody", sb.ToString());
            response.ExecuteScript("AdminClientsJs.shown();");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            if (!c.Can(Key, 'D'))
            {
                await App.Log(c.U.StaffId, "delete client", "not allowed", false);
                Toast(response, "You can't delete clients.");
                return response;
            }
            ClClient? cl = id == string.Empty ? null : await Client(id);
            if (cl == null)
            {
                Toast(response, "This client was not found. It may have been deleted.");
                return response;
            }
            ClUseRow u = await Use(id);
            if (u.Visits > 0 || u.Appts > 0 || u.Invoices > 0 || u.Payments > 0)
            {
                Toast(response, "This client has history and can't be deleted.");
                return response;
            }
            string pets = "SELECT PET_ID FROM PET WHERE CLIENT_ID = @cid";
            string result = await Helper.PutData(new List<string>
            {
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE CLIENT_ID = @cid OR PET_ID IN (" + pets + ")) OR EXISTS (SELECT 1 FROM VISIT WHERE CLIENT_ID = @cid OR PET_ID IN (" + pets + ")) OR EXISTS (SELECT 1 FROM INVOICE WHERE CLIENT_ID = @cid) OR EXISTS (SELECT 1 FROM PAYMENT WHERE CLIENT_ID = @cid) THROW 50001, N'CLIENTBUSY', 1;",
                "DELETE FROM PETWEIGHT WHERE PET_ID IN (" + pets + ")",
                "DELETE FROM PETVACCINE WHERE PET_ID IN (" + pets + ")",
                "DELETE FROM REMINDER WHERE PET_ID IN (" + pets + ")",
                "DELETE FROM MAILLOG WHERE CLIENT_ID = @cid",
                "DELETE FROM PET WHERE CLIENT_ID = @cid",
                "DELETE FROM CLIENT WHERE CLIENT_ID = @cid"
            }, new List<SqlParameter> { App.P("cid", id, 50) });
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "delete client", cl.ClientNo + " failed", false);
                Toast(response, result.Contains("CLIENTBUSY") ? "This client just got history and can't be deleted." : "The client could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "deleted client", cl.ClientNo + " " + cl.FirstName + " " + cl.LastName + (u.Pets > 0 ? " and " + Ui.Plural(u.Pets, "pet", "pets") : string.Empty), true);
            response.ExecuteScript("AdminClientsJs.removed('" + Ui.Js(cl.FirstName + " " + cl.LastName + " was deleted.") + "');");
            return response;
        }

        public async Task<ApiResponse> Retire()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("clientid"));
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "mark client inactive", "not allowed", false);
                Toast(response, "You can't change clients.");
                return response;
            }
            ClClient? cl = id == string.Empty ? null : await Client(id);
            if (cl == null)
            {
                Toast(response, "This client was not found. It may have been deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "UPDATE CLIENT SET ACTIVE = 0, SYSDTE = GETDATE() WHERE CLIENT_ID = @cid",
                "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) SELECT CONVERT(NVARCHAR(50), NEWID()), APPT_ID, SYSUTCDATETIME(), @me, N'Cancelled: client marked inactive' FROM APPOINTMENT WHERE CLIENT_ID = @cid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today",
                "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = N'Client marked inactive', SYSDTE = GETDATE() WHERE CLIENT_ID = @cid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today"
            }, new List<SqlParameter> { App.P("cid", id, 50), App.P("me", c.U.StaffId, 50), App.PDate("today", App.Now(await App.Settings()).Date) });
            if (Helper.IsError(result))
            {
                Toast(response, "The client could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "marked client inactive", cl.ClientNo + " " + cl.FirstName + " " + cl.LastName, true);
            await Detail(response, c, await App.Settings(), id, false);
            response.ExecuteScript("AdminClientsJs.saved(false);AdminClientsJs.toast('" + Ui.Js(cl.FirstName + " " + cl.LastName + " is now inactive.") + "');");
            return response;
        }
    }
}
