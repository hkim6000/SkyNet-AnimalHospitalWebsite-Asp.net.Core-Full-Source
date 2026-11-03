using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminPets : WebPage
    {
        private const string Key = "pets";
        private const int Size = 25;
        private const string PetCols = "P.PET_ID AS PetId, P.CLIENT_ID AS ClientId, P.PET_NAME AS PetName, P.SPECIES_KEY AS SpeciesKey, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, ISNULL(S.HAS_SIZE, 0) AS HasSize, P.BREED AS Breed, P.SEX AS Sex, P.FIXED AS Fixed, P.BIRTH_DATE AS BirthDate, P.COLOR AS Color, P.SIZE AS Size, P.MICROCHIP AS Microchip, P.ALERT AS Alert, P.PHOTO AS Photo, P.ACTIVE AS Active, P.DECEASED_DATE AS DeceasedDate, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, ISNULL(C.CLIENT_NO, N'') AS OwnerNo, ISNULL(C.PHONE, N'') AS OwnerPhone, ISNULL(C.ALERT, N'') AS OwnerAlert";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Pets | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--pt-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("pt"))
                .Replace("{plhd_open}", Ui.Attr(CleanId(QueryValue("p"))))
                .Replace("{plhd_owner}", Ui.Attr(CleanId(QueryValue("c"))))
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
                response.ExecuteScript("AdminPetsJs.expired();");
            }
            return c;
        }

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript("AdminPetsJs.toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"pt-panel pt-deny\"><h1>Pets</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"pt-btn pt-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminPetsJs.out();");
            return response;
        }

        private static async Task<List<PtSpecies>> SpeciesList()
        {
            var (list, emsg) = await Helper.GetListT<PtSpecies>("SELECT SPECIES_KEY AS SpeciesKey, SPECIES_NAME AS SpeciesName, HAS_SIZE AS HasSize FROM SPECIES WHERE ACTIVE = 1 ORDER BY SPECIES_ORDER, SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<PtSpecies>();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("pt-menu", Auth.Menu("pt", "AdminPetsJs", "AdminPets", c));
            response.SetElementContents("pt-who", Auth.Who("pt", c));
            response.ExecuteScript("AdminPetsJs.secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("pt-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            List<PtSpecies> species = await SpeciesList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"pt-ptop\"><div><h1>Pets</h1><p>Every patient, with weights, vaccines and what is booked next.</p></div>");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<div class=\"pt-acts\"><button type=\"button\" class=\"pt-btn pt-btn-accent\" onclick=\"AdminPetsJs.add('')\">Add pet</button></div>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"pt-bar\"><label class=\"pt-field pt-grow2\"><span>Search</span><input type=\"search\" id=\"pt-term\" maxlength=\"80\" placeholder=\"Pet name, owner name, phone or microchip\" autocomplete=\"off\" oninput=\"AdminPetsJs.typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();AdminPetsJs.page(1);}\"></label>");
            sb.Append("<label class=\"pt-field\"><span>Species</span><select id=\"pt-sp\" onchange=\"AdminPetsJs.page(1)\"><option value=\"\">All species</option>" + Ui.Opts(species.Select(x => (x.SpeciesKey, x.SpeciesName)), string.Empty) + "</select></label>");
            sb.Append("<label class=\"pt-field\"><span>Show</span><select id=\"pt-show\" onchange=\"AdminPetsJs.page(1)\"><option value=\"active\">Active pets</option><option value=\"inactive\">Inactive pets</option><option value=\"deceased\">Passed away</option><option value=\"all\">All pets</option></select></label></div>");
            sb.Append("<div id=\"pt-list\">" + await ListHtml(c, s, string.Empty, string.Empty, "active", 1) + "</div>");
            response.SetElementContents("pt-main", sb.ToString());
            response.ExecuteScript("AdminPetsJs.ready();");
            string open = CleanId(GetDataValue("openp"));
            if (open != string.Empty)
            {
                await Detail(response, c, s, open, string.Empty);
            }
            else if ((GetDataValue("openn") ?? string.Empty).Trim() == "1" && c.Can(Key, 'A'))
            {
                await ShowForm(response, null, CleanId(GetDataValue("openo")), s);
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
                response.SetElementContents("pt-main", Denied());
                return response;
            }
            int.TryParse(GetDataValue("pg"), out int pg);
            response.SetElementContents("pt-list", await ListHtml(c, await App.Settings(), Val("term", 80), Val("sp", 20), Val("show", 10), Math.Max(pg, 1)));
            response.ExecuteScript("AdminPetsJs.ready();");
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
                "ok" => "Up to date",
                "due" => "Due soon",
                "overdue" => "Overdue",
                "old" => "Replaced",
                _ => status
            };
            return "<span class=\"pt-st pt-st-" + Ui.Attr(status) + "\">" + Ui.Enc(text) + "</span>";
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

        private static async Task<string> ListHtml(Ctx c, Dictionary<string, string> s, string term, string sp, string show, int pg)
        {
            DateTime now = App.Now(s);
            string digits = Ui.Digits(term);
            string own = " AND (@own = 0 OR X.STAFF_ID = @sid)";
            int skip = 0;
            List<SqlParameter> Ps() => new List<SqlParameter>
            {
                App.P("t", term, 80),
                App.P("tl", Like(term), 300),
                App.P("dl", Like(digits), 300),
                App.P("dg", digits.Length >= 3 ? digits : string.Empty, 40),
                App.P("sp", sp, 20),
                App.PDate("today", now.Date),
                App.PDate("soon", now.Date.AddDays(App.SInt(s, "remind.days", 30))),
                App.PBit("own", c.U.OwnOnly),
                App.P("sid", c.U.StaffId, 50),
                App.PInt("skip", skip),
                App.PInt("take", Size)
            };
            string where = " WHERE (@t = N'' OR P.PET_NAME LIKE @tl OR P.MICROCHIP LIKE @tl OR C.FIRST_NAME LIKE @tl OR C.LAST_NAME LIKE @tl OR (C.FIRST_NAME + N' ' + C.LAST_NAME) LIKE @tl OR (@dg <> N'' AND (" + Dig("C.PHONE") + " LIKE @dl OR " + Dig("C.PHONE2") + " LIKE @dl))) AND (@sp = N'' OR P.SPECIES_KEY = @sp)";
            where += show switch
            {
                "inactive" => " AND P.ACTIVE = 0 AND P.DECEASED_DATE IS NULL",
                "deceased" => " AND P.DECEASED_DATE IS NOT NULL",
                "all" => string.Empty,
                _ => " AND P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL"
            };
            string from = " FROM PET P LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY";
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*)" + from + where, Ps());
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            int page = Ui.PageOf(pg.ToString(Ui.Inv), Size, total);
            skip = (page - 1) * Size;
            string latest = "NOT EXISTS (SELECT 1 FROM PETVACCINE W WHERE W.PET_ID = V.PET_ID AND W.VAX_NAME = V.VAX_NAME AND (W.GIVEN_DATE > V.GIVEN_DATE OR (W.GIVEN_DATE = V.GIVEN_DATE AND W.VAX_ID > V.VAX_ID)))";
            string sql = "SELECT P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(S.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, P.SEX AS Sex, P.FIXED AS Fixed, P.BIRTH_DATE AS BirthDate, P.PHOTO AS Photo, P.ALERT AS Alert, P.ACTIVE AS Active, P.DECEASED_DATE AS DeceasedDate, P.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'') AS OwnerName, " +
                "(SELECT MAX(X.CHECKIN_AT) FROM VISIT X WHERE X.PET_ID = P.PET_ID AND X.STATUS <> N'cancelled' AND X.VISIT_TYPE <> N'boarding'" + own + ") AS LastVisit, " +
                "(SELECT COUNT(*) FROM PETVACCINE V WHERE V.PET_ID = P.PET_ID AND V.DUE_DATE <= @soon AND " + latest + ") AS VaxDue, " +
                "(SELECT COUNT(*) FROM PETVACCINE V WHERE V.PET_ID = P.PET_ID AND V.DUE_DATE < @today AND " + latest + ") AS VaxLate" +
                from + where + " ORDER BY P.PET_NAME, C.LAST_NAME, P.PET_ID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";
            var (rows, emsg) = await Helper.GetListT<PtListRow>(sql, Ps());
            if (emsg != string.Empty || rows == null)
            {
                return "<div class=\"pt-panel\"><p>The pet list could not be loaded. Try again in a moment.</p></div>";
            }
            if (rows.Count == 0)
            {
                string none = term != string.Empty ? "No pet matches &ldquo;" + Ui.Enc(term) + "&rdquo;." : "No pets to show here.";
                return "<div class=\"pt-panel pt-empty\"><p>" + none + "</p></div>";
            }
            bool owners = c.Can("clients", 'V');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"pt-tw\"><table class=\"pt-table\"><thead><tr><th>Pet</th><th>Species and breed</th><th>Sex</th><th>Age</th><th>Owner</th><th>Last visit</th><th>Vaccines</th></tr></thead><tbody>");
            foreach (PtListRow r in rows)
            {
                string js = "AdminPetsJs.open('" + Ui.Js(r.PetId) + "')";
                bool gone = r.DeceasedDate.HasValue;
                sb.Append("<tr class=\"pt-row" + (r.Active && !gone ? string.Empty : " pt-off") + "\" tabindex=\"0\" onclick=\"" + js + "\" onkeydown=\"if(event.key==='Enter'){" + js + ";}\">");
                sb.Append("<td class=\"pt-cname\"><div class=\"pt-pn\">" + Ui.Avatar("pt", r.PetId, r.PetName, r.Photo) + "<div><b>" + Ui.Enc(r.PetName) + "</b>" + (gone ? "<small>Passed away " + Short(r.DeceasedDate!.Value) + "</small>" : !r.Active ? "<small>Inactive</small>" : string.Empty) + (r.Alert != string.Empty ? "<span class=\"pt-tag pt-tag-alert\" title=\"" + Ui.Attr(r.Alert) + "\">" + Ui.Enc(r.Alert) + "</span>" : string.Empty) + "</div></div></td>");
                sb.Append("<td data-l=\"Species\">" + Ui.Enc(r.SpeciesName + (r.Breed != string.Empty ? " · " + r.Breed : string.Empty)) + "</td>");
                sb.Append("<td data-l=\"Sex\">" + Ui.Enc(SexText(r.Sex, r.Fixed)) + "</td>");
                string age = Age(r.BirthDate, now.Date, r.DeceasedDate);
                sb.Append("<td data-l=\"Age\" class=\"pt-nw\">" + (age != string.Empty ? age : "<i class=\"pt-dim\">Not known</i>") + "</td>");
                string owner = r.OwnerName != string.Empty ? Ui.Enc(r.OwnerName) : "<i class=\"pt-dim\">No owner</i>";
                sb.Append("<td data-l=\"Owner\">" + (owners && r.OwnerName != string.Empty ? "<a class=\"pt-link\" href=\"AdminClients?c=" + Uri.EscapeDataString(r.ClientId) + "\" onclick=\"event.stopPropagation()\">" + owner + "</a>" : owner) + "</td>");
                sb.Append("<td data-l=\"Last visit\" class=\"pt-nw\">" + (r.LastVisit.HasValue ? Short(r.LastVisit.Value) : "<i class=\"pt-dim\">Never</i>") + "</td>");
                string vax = gone || !r.Active ? "<i class=\"pt-dim\">&ndash;</i>" : r.VaxLate > 0 ? "<span class=\"pt-tag pt-tag-alert\">" + r.VaxDue + " due &middot; " + r.VaxLate + " overdue</span>" : r.VaxDue > 0 ? "<span class=\"pt-tag pt-tag-warn\">" + r.VaxDue + " due soon</span>" : "<span class=\"pt-dim\">None due</span>";
                sb.Append("<td data-l=\"Vaccines\">" + vax + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("pt", page, Size, total, "pet", "pets", "AdminPetsJs.page"));
            return sb.ToString();
        }

        private static async Task<PtPet?> Pet(string id)
        {
            var (row, emsg) = await Helper.GetOneT<PtPet>("SELECT " + PetCols + " FROM PET P LEFT JOIN SPECIES S ON S.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = P.CLIENT_ID WHERE P.PET_ID = @pid", new List<SqlParameter> { App.P("pid", id, 50) });
            return emsg == string.Empty ? row : null;
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
                response.SetElementContents("pt-main", Denied());
                return response;
            }
            await Detail(response, c, await App.Settings(), CleanId(GetDataValue("petid")), string.Empty);
            return response;
        }

        private static async Task Detail(ApiResponse response, Ctx c, Dictionary<string, string> s, string id, string banner)
        {
            PtPet? p = id == string.Empty ? null : await Pet(id);
            if (p == null)
            {
                response.SetElementContents("pt-mbody", "<h2>Pet not found</h2><p>This pet may have been deleted. Close this window and search again.</p>");
                response.ExecuteScript("AdminPetsJs.shown('');");
                return;
            }
            DateTime now = App.Now(s);
            List<SqlParameter> P() => new List<SqlParameter> { App.P("pid", p.PetId, 50), App.PDate("now", now), App.PBit("own", c.U.OwnOnly), App.P("sid", c.U.StaffId, 50) };
            string own = " AND (@own = 0 OR X.STAFF_ID = @sid)";
            var (appts, _) = await Helper.GetListT<PtApptRow>("SELECT TOP 10 X.APPT_ID AS ApptId, X.START_AT AS StartAt, ISNULL(B.BOOK_NAME, N'Visit') AS BookName, ISNULL(S.STAFF_NAME, N'') AS StaffName, X.STATUS AS Status FROM APPOINTMENT X LEFT JOIN BOOKTYPE B ON B.BOOKTYPE_ID = X.BOOKTYPE_ID LEFT JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.PET_ID = @pid AND X.START_AT >= @now AND X.STATUS IN (N'booked', N'confirmed') AND X.VISIT_TYPE <> N'boarding'" + own + " ORDER BY X.START_AT", P());
            var (visits, _) = await Helper.GetListT<PtVisitRow>("SELECT TOP 10 X.VISIT_ID AS VisitId, X.CHECKIN_AT AS CheckinAt, X.REASON AS Reason, X.STATUS AS Status, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM VISIT X LEFT JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.PET_ID = @pid AND X.VISIT_TYPE <> N'boarding'" + own + " ORDER BY X.CHECKIN_AT DESC", P());
            appts ??= new List<PtApptRow>();
            visits ??= new List<PtVisitRow>();
            string pid = Ui.Js(p.PetId);
            bool gone = p.DeceasedDate.HasValue;
            StringBuilder sb = new StringBuilder();
            if (banner != string.Empty)
            {
                sb.Append("<div class=\"pt-done\"><div><b>" + Ui.Enc(banner) + "</b>" + (c.Can("appts", 'A') && p.Active && !gone ? "<span>Book a visit for " + Ui.Enc(p.PetName) + " now, or close this window.</span>" : string.Empty) + "</div>" + (c.Can("appts", 'A') && p.Active && !gone ? "<a class=\"pt-btn pt-btn-accent pt-btn-sm\" href=\"AdminAppts?p=" + Uri.EscapeDataString(p.PetId) + "\">Book a visit</a>" : string.Empty) + "</div>");
            }
            List<string> bits = new List<string> { p.SpeciesName };
            if (p.Breed != string.Empty)
            {
                bits.Add(p.Breed);
            }
            bits.Add(SexText(p.Sex, p.Fixed));
            string age = Age(p.BirthDate, now.Date, p.DeceasedDate);
            if (age != string.Empty)
            {
                bits.Add(age);
            }
            sb.Append("<div class=\"pt-dhead\"><div class=\"pt-dwho\">" + Ui.Avatar("pt", p.PetId, p.PetName, p.Photo, "lg") + "<div><h2>" + Ui.Enc(p.PetName) + "</h2><p class=\"pt-sub\">" + Ui.Enc(string.Join(" · ", bits)) + "</p>");
            if (gone)
            {
                sb.Append("<span class=\"pt-st pt-st-off\">Passed away " + Short(p.DeceasedDate!.Value) + "</span>");
            }
            else if (!p.Active)
            {
                sb.Append("<span class=\"pt-st pt-st-off\">Inactive</span>");
            }
            string ownerName = p.OwnerName != string.Empty ? Ui.Enc(p.OwnerName) : "No owner";
            sb.Append("<p class=\"pt-owner\">Owner: " + (c.Can("clients", 'V') && p.OwnerName != string.Empty ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(p.ClientId) + "\">" + ownerName + "</a>" : ownerName) + (p.OwnerPhone != string.Empty ? " &middot; <a href=\"tel:" + Ui.Attr(Ui.Digits(p.OwnerPhone)) + "\">" + Ui.Enc(p.OwnerPhone) + "</a>" : string.Empty) + "</p></div></div><div class=\"pt-acts\">");
            if (c.Can("appts", 'A') && p.Active && !gone)
            {
                sb.Append("<a class=\"pt-btn pt-btn-sm pt-btn-accent\" href=\"AdminAppts?p=" + Uri.EscapeDataString(p.PetId) + "\">Book</a>");
            }
            if (c.Can("charts", 'V'))
            {
                sb.Append("<a class=\"pt-btn pt-btn-sm pt-btn-ghost\" href=\"AdminChart?p=" + Uri.EscapeDataString(p.PetId) + "\">Patient chart</a>");
            }
            if (c.Can(Key, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"pt-btn pt-btn-sm\" onclick=\"AdminPetsJs.edit('" + pid + "')\">Edit</button>");
            }
            if (c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"pt-btn pt-btn-sm pt-btn-danger\" onclick=\"AdminPetsJs.ask('" + pid + "')\">Delete</button>");
            }
            sb.Append("</div></div>");
            if (p.Alert != string.Empty)
            {
                sb.Append("<div class=\"pt-alert\" role=\"alert\"><b>Alert</b><span>" + Ui.Enc(p.Alert) + "</span></div>");
            }
            if (p.OwnerAlert != string.Empty)
            {
                sb.Append("<div class=\"pt-alert pt-alert-owner\" role=\"alert\"><b>Owner alert</b><span>" + Ui.Enc(p.OwnerAlert) + "</span></div>");
            }
            sb.Append("<div class=\"pt-dgrid\"><section class=\"pt-box\"><h3>Profile</h3><dl class=\"pt-dl\">");
            sb.Append("<dt>Species</dt><dd>" + Ui.Enc(p.SpeciesName) + "</dd>");
            sb.Append("<dt>Breed</dt><dd>" + (p.Breed != string.Empty ? Ui.Enc(p.Breed) : "<i class=\"pt-dim\">Not known</i>") + "</dd>");
            sb.Append("<dt>Sex</dt><dd>" + (p.Sex == "m" ? "Male" : p.Sex == "f" ? "Female" : "<i class=\"pt-dim\">Not known</i>") + "</dd>");
            sb.Append("<dt>" + (p.Sex == "f" ? "Spayed" : p.Sex == "m" ? "Neutered" : "Spayed or neutered") + "</dt><dd>" + (p.Fixed ? "Yes" : "No") + "</dd>");
            sb.Append("<dt>Born</dt><dd>" + (p.BirthDate.HasValue ? Short(p.BirthDate.Value) + (age != string.Empty ? " <small class=\"pt-dim\">(" + age + (gone ? " when passed" : " old") + ")</small>" : string.Empty) : "<i class=\"pt-dim\">Not known</i>") + "</dd>");
            sb.Append("<dt>Colour</dt><dd>" + (p.Color != string.Empty ? Ui.Enc(p.Color) : "<i class=\"pt-dim\">Not noted</i>") + "</dd>");
            if (p.HasSize)
            {
                sb.Append("<dt>Size</dt><dd>" + (p.Size != string.Empty ? Ui.Enc(char.ToUpperInvariant(p.Size[0]) + p.Size.Substring(1)) : "<i class=\"pt-dim\">Not set</i>") + "</dd>");
            }
            sb.Append("<dt>Microchip</dt><dd>" + (p.Microchip != string.Empty ? Ui.Enc(p.Microchip) : "<i class=\"pt-dim\">None</i>") + "</dd>");
            sb.Append("<dt>Alert</dt><dd>" + (p.Alert != string.Empty ? "<b class=\"pt-late\">" + Ui.Enc(p.Alert) + "</b>" : "<i class=\"pt-dim\">None</i>") + "</dd></dl></section>");
            sb.Append("<section class=\"pt-box\" id=\"pt-wsec\">" + await WeightHtml(c, s, p) + "</section></div>");
            sb.Append("<section class=\"pt-box\" id=\"pt-vsec\">" + await VaxHtml(c, s, p) + "</section>");
            sb.Append("<div class=\"pt-dgrid pt-mt\"><section class=\"pt-box\"><h3>Upcoming appointments</h3>");
            if (appts.Count == 0)
            {
                sb.Append("<p class=\"pt-dim\">Nothing booked.</p>");
            }
            else
            {
                sb.Append("<ul class=\"pt-rows\">");
                foreach (PtApptRow a in appts)
                {
                    string inner = "<span class=\"pt-rw\">" + When(a.StartAt, s) + "</span><span class=\"pt-rt\">" + Ui.Enc(a.BookName) + (a.StaffName != string.Empty ? " <small>with " + Ui.Enc(a.StaffName) + "</small>" : string.Empty) + "</span>" + Chip(a.Status);
                    sb.Append("<li>" + (c.Can("appts", 'V') ? "<a class=\"pt-rl\" href=\"AdminAppts?a=" + Uri.EscapeDataString(a.ApptId) + "\">" + inner + "</a>" : "<div class=\"pt-rl\">" + inner + "</div>") + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section><section class=\"pt-box\"><h3>Last visits</h3>");
            if (visits.Count == 0)
            {
                sb.Append("<p class=\"pt-dim\">No visits yet.</p>");
            }
            else
            {
                sb.Append("<ul class=\"pt-rows\">");
                foreach (PtVisitRow v in visits)
                {
                    string inner = "<span class=\"pt-rw\">" + Short(v.CheckinAt) + "</span><span class=\"pt-rt\">" + Ui.Enc(v.Reason != string.Empty ? v.Reason : "Visit") + (v.StaffName != string.Empty ? " <small>with " + Ui.Enc(v.StaffName) + "</small>" : string.Empty) + "</span>" + Chip(v.Status);
                    sb.Append("<li>" + (c.Can("visits", 'V') ? "<a class=\"pt-rl\" href=\"AdminVisit?v=" + Uri.EscapeDataString(v.VisitId) + "\">" + inner + "</a>" : "<div class=\"pt-rl\">" + inner + "</div>") + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("</section></div>");
            response.SetElementContents("pt-mbody", sb.ToString());
            response.ExecuteScript("AdminPetsJs.shown('" + pid + "');");
        }

        private static string Lb(decimal v)
        {
            return v.ToString("0.##", Ui.Inv) + " lb";
        }

        private static string Chart(List<PtWeight> list)
        {
            List<PtWeight> pts = list.OrderBy(w => w.WeighedAt).ToList();
            if (pts.Count < 2)
            {
                return string.Empty;
            }
            double w = 420, h = 160, l = 44, r = 12, t = 12, b = 30;
            double min = (double)pts.Min(x => x.Lb), max = (double)pts.Max(x => x.Lb);
            double pad = Math.Max((max - min) * 0.15, Math.Max(max * 0.03, 0.2));
            min = Math.Max(0, min - pad);
            max += pad;
            double t0 = pts[0].WeighedAt.Ticks, t1 = pts[^1].WeighedAt.Ticks;
            double X(DateTime d) => t1 > t0 ? l + (d.Ticks - t0) / (t1 - t0) * (w - l - r) : (l + w - r) / 2;
            double Y(decimal v) => t + (max - (double)v) / (max - min) * (h - t - b);
            string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
            StringBuilder sb = new StringBuilder();
            sb.Append("<svg class=\"pt-chart\" viewBox=\"0 0 " + F(w) + " " + F(h) + "\" role=\"img\" aria-label=\"Weight from " + Ui.Attr(Lb(pts[0].Lb)) + " to " + Ui.Attr(Lb(pts[^1].Lb)) + "\">");
            foreach (double gv in new[] { min, (min + max) / 2, max })
            {
                double gy = t + (max - gv) / (max - min) * (h - t - b);
                sb.Append("<line class=\"pt-cg\" x1=\"" + F(l) + "\" x2=\"" + F(w - r) + "\" y1=\"" + F(gy) + "\" y2=\"" + F(gy) + "\"/><text class=\"pt-ct\" x=\"" + F(l - 8) + "\" y=\"" + F(gy + 4) + "\" text-anchor=\"end\">" + gv.ToString("0.#", CultureInfo.InvariantCulture) + "</text>");
            }
            sb.Append("<text class=\"pt-ct\" x=\"" + F(l) + "\" y=\"" + F(h - 8) + "\">" + pts[0].WeighedAt.ToString("MMM yyyy", Ui.Us) + "</text><text class=\"pt-ct\" x=\"" + F(w - r) + "\" y=\"" + F(h - 8) + "\" text-anchor=\"end\">" + pts[^1].WeighedAt.ToString("MMM yyyy", Ui.Us) + "</text>");
            sb.Append("<polyline class=\"pt-cl\" points=\"" + string.Join(" ", pts.Select(x => F(X(x.WeighedAt)) + "," + F(Y(x.Lb)))) + "\"/>");
            foreach (PtWeight x in pts)
            {
                sb.Append("<g class=\"pt-cp\"><circle class=\"pt-chit\" cx=\"" + F(X(x.WeighedAt)) + "\" cy=\"" + F(Y(x.Lb)) + "\" r=\"12\"/><circle class=\"pt-cd\" cx=\"" + F(X(x.WeighedAt)) + "\" cy=\"" + F(Y(x.Lb)) + "\" r=\"4.5\"/><title>" + Ui.Enc(Short(x.WeighedAt) + ": " + Lb(x.Lb)) + "</title></g>");
            }
            sb.Append("</svg>");
            return sb.ToString();
        }

        private static async Task<string> WeightHtml(Ctx c, Dictionary<string, string> s, PtPet p)
        {
            var (list, _) = await Helper.GetListT<PtWeight>("SELECT W.WEIGHT_ID AS WeightId, W.WEIGHED_AT AS WeighedAt, W.LB AS Lb, W.VISIT_ID AS VisitId, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM PETWEIGHT W LEFT JOIN STAFF S ON S.STAFF_ID = W.STAFF_ID WHERE W.PET_ID = @pid ORDER BY W.WEIGHED_AT DESC", new List<SqlParameter> { App.P("pid", p.PetId, 50) });
            list ??= new List<PtWeight>();
            bool edit = c.Can(Key, 'E');
            string pid = Ui.Js(p.PetId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h3>Weight</h3>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"pt-dim\">No weights yet.</p>");
            }
            else
            {
                PtWeight last = list[0];
                string trend = string.Empty;
                if (list.Count > 1)
                {
                    decimal d = last.Lb - list[1].Lb;
                    trend = d == 0 ? " <small class=\"pt-dim\">same as before</small>" : " <small class=\"pt-dim\">" + (d > 0 ? "up " : "down ") + Lb(Math.Abs(d)) + " since " + Short(list[1].WeighedAt) + "</small>";
                }
                sb.Append("<p class=\"pt-wnow\"><b>" + Lb(last.Lb) + "</b> on " + Short(last.WeighedAt) + trend + "</p>");
                sb.Append(Chart(list));
                sb.Append("<div class=\"pt-wtw\"><table class=\"pt-mini\"><thead><tr><th>Date</th><th class=\"pt-num\">Weight</th><th>From</th><th></th></tr></thead><tbody>");
                foreach (PtWeight w in list)
                {
                    sb.Append("<tr><td class=\"pt-nw\">" + Short(w.WeighedAt) + "<small class=\"pt-dim pt-wt\">" + Ui.Hm(w.WeighedAt, App.S(s, "clock24") == "1") + "</small></td><td class=\"pt-num\">" + Lb(w.Lb) + "</td><td>" + (w.VisitId != string.Empty ? "Visit" : "By hand") + "</td><td class=\"pt-num\">");
                    if (edit && w.VisitId == string.Empty)
                    {
                        sb.Append("<button type=\"button\" class=\"pt-lbtn\" onclick=\"AdminPetsJs.wdel('" + pid + "','" + Ui.Js(w.WeightId) + "')\" aria-label=\"Delete weight " + Ui.Attr(Short(w.WeighedAt)) + "\">Delete</button>");
                    }
                    sb.Append("</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            if (edit)
            {
                DateTime now = App.Now(s);
                sb.Append("<div class=\"pt-wadd\"><label class=\"pt-field\"><span>Weighed on</span><input type=\"datetime-local\" id=\"pt-w-at\" value=\"" + now.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv) + "\" max=\"" + now.ToString("yyyy-MM-dd'T'23:59", Ui.Inv) + "\"></label><label class=\"pt-field\"><span>Pounds</span><input type=\"number\" id=\"pt-w-lb\" min=\"0.01\" max=\"500\" step=\"0.01\" inputmode=\"decimal\" placeholder=\"lb\"></label><button type=\"button\" class=\"pt-btn pt-btn-sm\" onclick=\"AdminPetsJs.wadd('" + pid + "')\">Add weight</button></div><em class=\"pt-err\" id=\"pt-e-w\"></em>");
            }
            return sb.ToString();
        }

        private static async Task<string> VaxHtml(Ctx c, Dictionary<string, string> s, PtPet p)
        {
            var (list, _) = await Helper.GetListT<PtVax>("SELECT V.VAX_ID AS VaxId, V.ITEM_ID AS ItemId, V.VAX_NAME AS VaxName, V.GIVEN_DATE AS GivenDate, V.DUE_DATE AS DueDate, V.VISIT_ID AS VisitId, V.NOTE AS Note, ISNULL(S.STAFF_NAME, N'') AS StaffName, (SELECT COUNT(*) FROM PETVACCINE W WHERE W.PET_ID = V.PET_ID AND W.VAX_NAME = V.VAX_NAME AND (W.GIVEN_DATE > V.GIVEN_DATE OR (W.GIVEN_DATE = V.GIVEN_DATE AND W.VAX_ID > V.VAX_ID))) AS Newer FROM PETVACCINE V LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID WHERE V.PET_ID = @pid ORDER BY CASE WHEN (SELECT COUNT(*) FROM PETVACCINE W WHERE W.PET_ID = V.PET_ID AND W.VAX_NAME = V.VAX_NAME AND (W.GIVEN_DATE > V.GIVEN_DATE OR (W.GIVEN_DATE = V.GIVEN_DATE AND W.VAX_ID > V.VAX_ID))) = 0 THEN 0 ELSE 1 END, V.DUE_DATE, V.VAX_NAME", new List<SqlParameter> { App.P("pid", p.PetId, 50) });
            list ??= new List<PtVax>();
            DateTime today = App.Now(s).Date;
            DateTime soon = today.AddDays(App.SInt(s, "remind.days", 30));
            bool edit = c.Can(Key, 'E');
            string pid = Ui.Js(p.PetId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"pt-shead\"><h3>Vaccines</h3>" + (edit ? "<button type=\"button\" class=\"pt-btn pt-btn-sm pt-btn-ghost\" onclick=\"AdminPetsJs.vform('" + pid + "','')\">Add vaccine</button>" : string.Empty) + "</div><div id=\"pt-vf\"></div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"pt-dim\">No vaccines on file.</p>");
                return sb.ToString();
            }
            sb.Append("<div class=\"pt-wtw\"><table class=\"pt-mini pt-vax\"><thead><tr><th>Vaccine</th><th>Given</th><th>Due</th><th>Status</th><th>From</th><th></th></tr></thead><tbody>");
            foreach (PtVax v in list)
            {
                string st = v.Newer > 0 ? "old" : v.DueDate.Date < today ? "overdue" : v.DueDate.Date <= soon ? "due" : "ok";
                bool gone = p.DeceasedDate.HasValue || !p.Active;
                sb.Append("<tr class=\"" + (st == "old" ? "pt-off pt-vold" : string.Empty) + "\"><td><b>" + Ui.Enc(v.VaxName) + "</b>" + (v.Note != string.Empty ? "<small class=\"pt-vnote\">" + Ui.Enc(v.Note) + "</small>" : string.Empty) + "</td><td data-l=\"Given\">" + Short(v.GivenDate) + "</td><td data-l=\"Due\">" + Short(v.DueDate) + "</td><td data-l=\"Status\">" + (gone && st != "old" ? "<span class=\"pt-dim\">&ndash;</span>" : Chip(st)) + "</td><td data-l=\"From\">" + (v.VisitId != string.Empty ? "Visit" : "Entered by hand") + "</td><td class=\"pt-num pt-vact\">");
                if (edit && v.VisitId == string.Empty)
                {
                    sb.Append("<button type=\"button\" class=\"pt-lbtn\" onclick=\"AdminPetsJs.vform('" + pid + "','" + Ui.Js(v.VaxId) + "')\">Edit</button><button type=\"button\" class=\"pt-lbtn pt-lbad\" onclick=\"AdminPetsJs.vdel('" + pid + "','" + Ui.Js(v.VaxId) + "','" + Ui.Js(v.VaxName) + "')\">Delete</button>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            int older = list.Count(x => x.Newer > 0);
            if (older > 0)
            {
                sb.Append("<button type=\"button\" class=\"pt-lbtn pt-vmore\" onclick=\"AdminPetsJs.vmore(this)\" data-n=\"" + older + "\">Show " + Ui.Plural(older, "older record", "older records") + "</button>");
            }
            return sb.ToString();
        }

        private async Task<(Ctx? c, PtPet? p)> EditGate(ApiResponse response, string what)
        {
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return (null, null);
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, what, "not allowed", false);
                Toast(response, "You can't change pet records.");
                return (null, null);
            }
            string id = CleanId(GetDataValue("petid"));
            PtPet? p = id == string.Empty ? null : await Pet(id);
            if (p == null)
            {
                Toast(response, "This pet was not found. It may have been deleted.");
                return (null, null);
            }
            return (c, p);
        }

        public async Task<ApiResponse> WeightAdd()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "add weight");
            if (c == null || p == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            DateTime? at = Ui.ParseStamp(GetDataValue("wat"));
            string lbText = Val("wlb", 20);
            bool okLb = decimal.TryParse(lbText, NumberStyles.Number, Ui.Inv, out decimal lb);
            string err = string.Empty;
            if (at == null)
            {
                err = "Pick the date and time it was weighed.";
            }
            else if (at.Value > now.AddMinutes(5))
            {
                err = "The date can't be in the future.";
            }
            else if (p.BirthDate.HasValue && at.Value.Date < p.BirthDate.Value.Date)
            {
                err = "The date is before " + Ui.Enc(p.PetName) + " was born.";
            }
            else if (!okLb || lb <= 0 || lb > 500)
            {
                err = "Enter the weight in pounds, for example 12.4.";
            }
            response.SetElementContents("pt-e-w", err);
            if (err != string.Empty)
            {
                return response;
            }
            lb = Math.Round(lb, 2);
            string result = await Helper.PutData(new List<string> { "INSERT INTO PETWEIGHT (WEIGHT_ID, PET_ID, WEIGHED_AT, LB, VISIT_ID, STAFF_ID, SYSDTE) VALUES (@wid, @pid, @at, @lb, N'', @me, GETDATE())" }, new List<SqlParameter> { App.P("wid", App.NewId(), 50), App.P("pid", p.PetId, 50), App.PDate("at", at), new SqlParameter("lb", System.Data.SqlDbType.Decimal) { Precision = 7, Scale = 2, Value = lb }, App.P("me", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                response.SetElementContents("pt-e-w", "The weight could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "added weight", p.PetName + " " + Lb(lb), true);
            response.SetElementContents("pt-wsec", await WeightHtml(c, s, p));
            Toast(response, "Weight saved.");
            return response;
        }

        public async Task<ApiResponse> WeightDel()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "delete weight");
            if (c == null || p == null)
            {
                return response;
            }
            string wid = CleanId(GetDataValue("weightid"));
            var (dt, emsg) = await Helper.GetDataTable("SELECT VISIT_ID, LB FROM PETWEIGHT WHERE WEIGHT_ID = @wid AND PET_ID = @pid", new List<SqlParameter> { App.P("wid", wid, 50), App.P("pid", p.PetId, 50) });
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                Toast(response, "This weight was already removed.");
            }
            else if ((Convert.ToString(dt.Rows[0][0]) ?? string.Empty) != string.Empty)
            {
                Toast(response, "This weight came from a visit. Change it in the visit record.");
            }
            else
            {
                string result = await Helper.PutData(new List<string> { "DELETE FROM PETWEIGHT WHERE WEIGHT_ID = @wid AND PET_ID = @pid AND VISIT_ID = N''" }, new List<SqlParameter> { App.P("wid", wid, 50), App.P("pid", p.PetId, 50) });
                if (Helper.IsError(result))
                {
                    Toast(response, "The weight could not be deleted. Try again.");
                    return response;
                }
                await App.Log(c.U.StaffId, "deleted weight", p.PetName + " " + Lb(Convert.ToDecimal(dt.Rows[0][1])), true);
                Toast(response, "Weight deleted.");
            }
            response.SetElementContents("pt-wsec", await WeightHtml(c, await App.Settings(), p));
            return response;
        }

        private static async Task<List<PtVaxItem>> VaxItems(string species)
        {
            var (list, emsg) = await Helper.GetListT<PtVaxItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, VAX_MONTHS AS VaxMonths, SPECIES AS Species FROM ITEM WHERE ITEM_TYPE = N'vaccine' AND ACTIVE = 1 ORDER BY ITEM_ORDER, ITEM_NAME");
            return emsg == string.Empty && list != null ? list.Where(i => Slots.ForSpecies(i.Species, species)).ToList() : new List<PtVaxItem>();
        }

        private static string VaxName(string itemName)
        {
            string n = Regex.Replace(itemName, "\\s+vaccine$", string.Empty, RegexOptions.IgnoreCase).Trim();
            return Ui.Clip(n == string.Empty ? itemName : n, 60);
        }

        public async Task<ApiResponse> VaxForm()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "edit vaccine");
            if (c == null || p == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string vid = CleanId(GetDataValue("vaxid"));
            PtVax v = new PtVax { GivenDate = App.Now(s).Date };
            if (vid != string.Empty)
            {
                var (row, _) = await Helper.GetOneT<PtVax>("SELECT VAX_ID AS VaxId, ITEM_ID AS ItemId, VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate, VISIT_ID AS VisitId, NOTE AS Note, N'' AS StaffName, 0 AS Newer FROM PETVACCINE WHERE VAX_ID = @vid AND PET_ID = @pid", new List<SqlParameter> { App.P("vid", vid, 50), App.P("pid", p.PetId, 50) });
                if (row == null)
                {
                    Toast(response, "This vaccine record was not found.");
                    return response;
                }
                if (row.VisitId != string.Empty)
                {
                    Toast(response, "This vaccine came from a visit. Change it in the visit record.");
                    return response;
                }
                v = row;
            }
            List<PtVaxItem> items = await VaxItems(p.SpeciesKey);
            bool known = v.ItemId != string.Empty && items.Any(i => i.ItemId == v.ItemId);
            string pid = Ui.Js(p.PetId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"pt-vform\"><input type=\"hidden\" id=\"pt-v-id\" value=\"" + Ui.Attr(v.VaxId) + "\"><div class=\"pt-fgrid\">");
            sb.Append("<label class=\"pt-field\"><span>Vaccine</span><select id=\"pt-v-item\" onchange=\"AdminPetsJs.vitem()\"><option value=\"\" data-m=\"0\">Other (type the name)</option>");
            foreach (PtVaxItem i in items)
            {
                bool sel = known ? i.ItemId == v.ItemId : vid == string.Empty && i == items[0];
                sb.Append("<option value=\"" + Ui.Attr(i.ItemId) + "\" data-m=\"" + i.VaxMonths + "\" data-n=\"" + Ui.Attr(VaxName(i.ItemName)) + "\"" + (sel ? " selected" : string.Empty) + ">" + Ui.Enc(i.ItemName) + (i.VaxMonths > 0 ? " (every " + (i.VaxMonths % 12 == 0 ? Ui.Plural(i.VaxMonths / 12, "year", "years") : Ui.Plural(i.VaxMonths, "month", "months")) + ")" : string.Empty) + "</option>");
            }
            sb.Append("</select></label>");
            string nm = v.VaxName != string.Empty ? v.VaxName : items.Count > 0 && vid == string.Empty ? VaxName(items[0].ItemName) : string.Empty;
            sb.Append("<label class=\"pt-field\"><span>Name on the record</span><input type=\"text\" id=\"pt-v-name\" maxlength=\"60\" value=\"" + Ui.Attr(nm) + "\"><em class=\"pt-err\" id=\"pt-e-vname\"></em></label>");
            sb.Append("<label class=\"pt-field\"><span>Given on</span><input type=\"date\" id=\"pt-v-given\" value=\"" + Ui.Ymd(v.GivenDate) + "\" max=\"" + Ui.Ymd(App.Now(s).Date) + "\" onchange=\"AdminPetsJs.vitem()\"><em class=\"pt-err\" id=\"pt-e-vgiven\"></em></label>");
            sb.Append("<label class=\"pt-field\"><span>Due again on</span><input type=\"date\" id=\"pt-v-due\" value=\"" + (vid != string.Empty ? Ui.Ymd(v.DueDate) : string.Empty) + "\" oninput=\"this.setAttribute('data-touched','1')\"" + (vid != string.Empty ? " data-touched=\"1\"" : string.Empty) + "><em class=\"pt-err\" id=\"pt-e-vdue\"></em></label>");
            sb.Append("<label class=\"pt-field pt-span2\"><span>Note <small>optional, for example the clinic that gave it</small></span><input type=\"text\" id=\"pt-v-note\" maxlength=\"200\" value=\"" + Ui.Attr(v.Note) + "\"></label>");
            sb.Append("</div><div class=\"pt-facts\"><button type=\"button\" class=\"pt-btn pt-btn-sm pt-btn-accent\" onclick=\"AdminPetsJs.vsave('" + pid + "')\">" + (vid == string.Empty ? "Add vaccine" : "Save vaccine") + "</button><button type=\"button\" class=\"pt-btn pt-btn-sm pt-btn-ghost\" onclick=\"AdminPetsJs.vclose()\">Cancel</button></div></div>");
            response.SetElementContents("pt-vf", sb.ToString());
            response.ExecuteScript("AdminPetsJs.vshown(" + (vid == string.Empty ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> VaxSave()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "save vaccine");
            if (c == null || p == null)
            {
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            string vid = CleanId(GetDataValue("vaxid"));
            string itemId = CleanId(GetDataValue("vitem"));
            string name = Ui.OneLine(GetDataValue("vname"), 60);
            string note = Ui.OneLine(GetDataValue("vnote"), 200);
            DateTime? given = Ui.ParseDay(GetDataValue("vgiven"));
            DateTime? due = Ui.ParseDay(GetDataValue("vdue"));
            PtVaxItem? item = null;
            if (itemId != string.Empty)
            {
                item = (await VaxItems(p.SpeciesKey)).FirstOrDefault(i => i.ItemId == itemId);
                if (item == null)
                {
                    itemId = string.Empty;
                }
            }
            if (name == string.Empty && item != null)
            {
                name = VaxName(item.ItemName);
            }
            if (due == null && given != null && item != null && item.VaxMonths > 0)
            {
                due = given.Value.AddMonths(item.VaxMonths);
            }
            string eName = name == string.Empty ? "Enter the vaccine name." : string.Empty;
            string eGiven = given == null ? "Pick the date it was given." : given.Value > today ? "The date can't be in the future." : p.BirthDate.HasValue && given.Value < p.BirthDate.Value.Date ? "This is before " + Ui.Enc(p.PetName) + " was born." : string.Empty;
            string eDue = due == null ? "Pick when it is due again." : given != null && due.Value < given.Value ? "The due date must be after the date given." : due.Value > today.AddYears(10) ? "The due date is too far ahead." : string.Empty;
            response.SetElementContents("pt-e-vname", eName);
            response.SetElementContents("pt-e-vgiven", eGiven);
            response.SetElementContents("pt-e-vdue", eDue);
            if (eName != string.Empty || eGiven != string.Empty || eDue != string.Empty)
            {
                return response;
            }
            string newVid = vid == string.Empty ? App.NewId() : vid;
            List<SqlParameter> Ps() => new List<SqlParameter> { App.P("vid", newVid, 50), App.P("pid", p.PetId, 50), App.P("item", itemId, 50), App.P("name", name, 60), App.PDate("given", given), App.PDate("due", due), App.P("note", note, 200), App.P("me", c.U.StaffId, 50) };
            string result;
            if (vid == string.Empty)
            {
                result = await Helper.PutData(new List<string> { "INSERT INTO PETVACCINE (VAX_ID, PET_ID, ITEM_ID, VAX_NAME, GIVEN_DATE, DUE_DATE, VISIT_ID, STAFF_ID, NOTE, SYSDTE) VALUES (@vid, @pid, @item, @name, CAST(@given AS DATE), CAST(@due AS DATE), N'', @me, @note, GETDATE())" }, Ps());
            }
            else
            {
                var (dt, _) = await Helper.GetDataTable("SELECT VISIT_ID FROM PETVACCINE WHERE VAX_ID = @vid AND PET_ID = @pid", Ps());
                if (dt.Rows.Count == 0 || (Convert.ToString(dt.Rows[0][0]) ?? string.Empty) != string.Empty)
                {
                    Toast(response, "This vaccine record can't be changed here.");
                    return response;
                }
                result = await Helper.PutData(new List<string> { "UPDATE PETVACCINE SET ITEM_ID = @item, VAX_NAME = @name, GIVEN_DATE = CAST(@given AS DATE), DUE_DATE = CAST(@due AS DATE), NOTE = @note, STAFF_ID = @me, SYSDTE = GETDATE() WHERE VAX_ID = @vid AND PET_ID = @pid AND VISIT_ID = N''" }, Ps());
            }
            if (Helper.IsError(result))
            {
                response.SetElementContents("pt-e-vname", "The vaccine could not be saved. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, vid == string.Empty ? "added vaccine" : "edited vaccine", p.PetName + " " + name + " due " + Ui.Ymd(due!.Value), true);
            response.SetElementContents("pt-vsec", await VaxHtml(c, s, p));
            response.ExecuteScript("AdminPetsJs.changed('" + Ui.Js(vid == string.Empty ? "Vaccine added." : "Vaccine saved.") + "');");
            return response;
        }

        public async Task<ApiResponse> VaxDel()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "delete vaccine");
            if (c == null || p == null)
            {
                return response;
            }
            string vid = CleanId(GetDataValue("vaxid"));
            var (dt, emsg) = await Helper.GetDataTable("SELECT VISIT_ID, VAX_NAME FROM PETVACCINE WHERE VAX_ID = @vid AND PET_ID = @pid", new List<SqlParameter> { App.P("vid", vid, 50), App.P("pid", p.PetId, 50) });
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                Toast(response, "This vaccine record was already removed.");
            }
            else if ((Convert.ToString(dt.Rows[0][0]) ?? string.Empty) != string.Empty)
            {
                Toast(response, "This vaccine came from a visit. Change it in the visit record.");
            }
            else
            {
                string result = await Helper.PutData(new List<string> { "DELETE FROM PETVACCINE WHERE VAX_ID = @vid AND PET_ID = @pid AND VISIT_ID = N''" }, new List<SqlParameter> { App.P("vid", vid, 50), App.P("pid", p.PetId, 50) });
                if (Helper.IsError(result))
                {
                    Toast(response, "The vaccine could not be deleted. Try again.");
                    return response;
                }
                await App.Log(c.U.StaffId, "deleted vaccine", p.PetName + " " + Convert.ToString(dt.Rows[0][1]), true);
                response.ExecuteScript("AdminPetsJs.changed('Vaccine deleted.');");
            }
            response.SetElementContents("pt-vsec", await VaxHtml(c, await App.Settings(), p));
            return response;
        }

        private static async Task<PtOwner?> Owner(string id)
        {
            var (row, emsg) = await Helper.GetOneT<PtOwner>("SELECT CLIENT_ID AS ClientId, CLIENT_NO AS ClientNo, FIRST_NAME AS FirstName, LAST_NAME AS LastName, PHONE AS Phone, ACTIVE AS Active FROM CLIENT WHERE CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static string OwnerChip(PtOwner o)
        {
            return Ui.Enc(o.FirstName + " " + o.LastName) + " <small>" + Ui.Enc(o.ClientNo + (o.Phone != string.Empty ? " · " + o.Phone : string.Empty)) + "</small>";
        }

        public async Task<ApiResponse> OwnerFind()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A') && !c.Can(Key, 'E'))
            {
                return response;
            }
            string term = Val("oterm", 80);
            if (term.Length < 2)
            {
                response.SetElementContents("pt-f-ores", string.Empty);
                return response;
            }
            string digits = Ui.Digits(term);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("tl", Like(term), 300), App.P("dg", digits.Length >= 3 ? digits : string.Empty, 40), App.P("dl", Like(digits), 300) };
            var (rows, emsg) = await Helper.GetListT<PtOwner>("SELECT TOP 8 CLIENT_ID AS ClientId, CLIENT_NO AS ClientNo, FIRST_NAME AS FirstName, LAST_NAME AS LastName, PHONE AS Phone, ACTIVE AS Active FROM CLIENT WHERE FIRST_NAME LIKE @tl OR LAST_NAME LIKE @tl OR (FIRST_NAME + N' ' + LAST_NAME) LIKE @tl OR CLIENT_NO LIKE @tl OR EMAIL LIKE @tl OR (@dg <> N'' AND " + Dig("PHONE") + " LIKE @dl) ORDER BY ACTIVE DESC, LAST_NAME, FIRST_NAME", ps);
            StringBuilder sb = new StringBuilder();
            if (emsg != string.Empty || rows == null || rows.Count == 0)
            {
                sb.Append("<p class=\"pt-dim pt-onone\">No client found." + (c.Can("clients", 'A') ? " <a class=\"pt-link\" href=\"AdminClients?n=1\">Add a new client</a>" : string.Empty) + "</p>");
            }
            else
            {
                sb.Append("<ul class=\"pt-olist\">");
                foreach (PtOwner o in rows)
                {
                    sb.Append("<li><button type=\"button\" onclick=\"AdminPetsJs.pick('" + Ui.Js(o.ClientId) + "', this)\">" + OwnerChip(o) + (o.Active ? string.Empty : " <span class=\"pt-tag\">Inactive</span>") + "</button></li>");
                }
                sb.Append("</ul>");
            }
            response.SetElementContents("pt-f-ores", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("petid"));
            if (!c.Can(Key, id == string.Empty ? 'A' : 'E'))
            {
                Toast(response, id == string.Empty ? "You can't add pets." : "You can't change pet records.");
                return response;
            }
            PtPet? p = null;
            if (id != string.Empty)
            {
                p = await Pet(id);
                if (p == null)
                {
                    Toast(response, "This pet was not found. It may have been deleted.");
                    return response;
                }
            }
            await ShowForm(response, p, CleanId(GetDataValue("ownerid")), await App.Settings());
            return response;
        }

        private static async Task ShowForm(ApiResponse response, PtPet? pet, string ownerId, Dictionary<string, string> s)
        {
            bool add = pet == null;
            PtPet v = pet ?? new PtPet { Active = true, ClientId = ownerId };
            List<PtSpecies> species = await SpeciesList();
            if (add && species.Count > 0)
            {
                v.SpeciesKey = species[0].SpeciesKey;
            }
            PtOwner? owner = v.ClientId != string.Empty ? await Owner(v.ClientId) : null;
            var (breeds, _) = await Helper.GetListT<PtBreed>("SELECT SPECIES_KEY AS SpeciesKey, BREED_NAME AS BreedName FROM BREED ORDER BY SPECIES_KEY, BREED_NAME");
            breeds ??= new List<PtBreed>();
            DateTime today = App.Now(s).Date;
            bool sized = species.Any(x => x.SpeciesKey == v.SpeciesKey && x.HasSize);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "New pet" : "Edit " + Ui.Enc(v.PetName)) + "</h2><form class=\"pt-form\" onsubmit=\"return AdminPetsJs.save()\" novalidate><input type=\"hidden\" id=\"pt-f-id\" value=\"" + Ui.Attr(v.PetId) + "\"><input type=\"hidden\" id=\"pt-f-owner\" value=\"" + Ui.Attr(owner?.ClientId ?? string.Empty) + "\">");
            sb.Append("<div class=\"pt-field pt-ownbox\"><span>Owner</span><div class=\"pt-ochip\" id=\"pt-f-ochip\"" + (owner == null ? " hidden" : string.Empty) + "><b id=\"pt-f-oname\">" + (owner != null ? OwnerChip(owner) : string.Empty) + "</b><button type=\"button\" class=\"pt-lbtn\" onclick=\"AdminPetsJs.change()\">Change</button></div>");
            sb.Append("<div id=\"pt-f-ofind\"" + (owner != null ? " hidden" : string.Empty) + "><input type=\"search\" id=\"pt-f-osearch\" maxlength=\"80\" placeholder=\"Type the owner's name, phone or client no.\" autocomplete=\"off\" oninput=\"AdminPetsJs.ofind()\"><div id=\"pt-f-ores\"></div></div><em class=\"pt-err\" id=\"pt-e-owner\"></em></div>");
            sb.Append("<div class=\"pt-fgrid\">");
            sb.Append("<label class=\"pt-field\"><span>Pet name</span><input type=\"text\" id=\"pt-f-name\" maxlength=\"60\" value=\"" + Ui.Attr(v.PetName) + "\" autocomplete=\"off\"><em class=\"pt-err\" id=\"pt-e-name\"></em></label>");
            sb.Append("<label class=\"pt-field\"><span>Species</span><select id=\"pt-f-species\" onchange=\"AdminPetsJs.species()\">");
            foreach (PtSpecies x in species)
            {
                sb.Append("<option value=\"" + Ui.Attr(x.SpeciesKey) + "\" data-size=\"" + (x.HasSize ? "1" : "0") + "\"" + (x.SpeciesKey == v.SpeciesKey ? " selected" : string.Empty) + ">" + Ui.Enc(x.SpeciesName) + "</option>");
            }
            if (!add && !species.Any(x => x.SpeciesKey == v.SpeciesKey))
            {
                sb.Append("<option value=\"" + Ui.Attr(v.SpeciesKey) + "\" data-size=\"0\" selected>" + Ui.Enc(v.SpeciesName) + "</option>");
            }
            sb.Append("</select><em class=\"pt-err\" id=\"pt-e-species\"></em></label>");
            sb.Append("<label class=\"pt-field\"><span>Breed</span><input type=\"text\" id=\"pt-f-breed\" maxlength=\"60\" value=\"" + Ui.Attr(v.Breed) + "\" list=\"pt-bl-" + Ui.Attr(v.SpeciesKey) + "\" autocomplete=\"off\" placeholder=\"For example: Labrador mix\"></label>");
            foreach (IGrouping<string, PtBreed> g in breeds.GroupBy(x => x.SpeciesKey))
            {
                sb.Append("<datalist id=\"pt-bl-" + Ui.Attr(g.Key) + "\">");
                foreach (PtBreed br in g)
                {
                    sb.Append("<option value=\"" + Ui.Attr(br.BreedName) + "\"></option>");
                }
                sb.Append("</datalist>");
            }
            sb.Append("<label class=\"pt-field\"><span>Sex</span><select id=\"pt-f-sex\">" + Ui.Opts(new[] { ("", "Not known"), ("m", "Male"), ("f", "Female") }, v.Sex) + "</select></label>");
            sb.Append("<label class=\"pt-field\"><span>Born on <small>a guess is fine</small></span><input type=\"date\" id=\"pt-f-birth\" value=\"" + (v.BirthDate.HasValue ? Ui.Ymd(v.BirthDate.Value) : string.Empty) + "\" max=\"" + Ui.Ymd(today) + "\"><em class=\"pt-err\" id=\"pt-e-birth\"></em></label>");
            sb.Append("<label class=\"pt-check pt-fixrow\"><input type=\"checkbox\" id=\"pt-f-fixed\"" + (v.Fixed ? " checked" : string.Empty) + "><span>Spayed or neutered</span></label>");
            sb.Append("<label class=\"pt-field\"><span>Colour and markings</span><input type=\"text\" id=\"pt-f-color\" maxlength=\"60\" value=\"" + Ui.Attr(v.Color) + "\"></label>");
            sb.Append("<label class=\"pt-field\" id=\"pt-f-sizebox\"" + (sized ? string.Empty : " hidden") + "><span>Size <small>sets how long visits take</small></span><select id=\"pt-f-size\">" + Ui.Opts(new[] { ("", "Not set"), ("small", "Small"), ("medium", "Medium"), ("large", "Large") }, v.Size) + "</select></label>");
            sb.Append("<label class=\"pt-field\"><span>Microchip number</span><input type=\"text\" id=\"pt-f-chip\" maxlength=\"40\" value=\"" + Ui.Attr(v.Microchip) + "\" autocomplete=\"off\" inputmode=\"text\"><em class=\"pt-err\" id=\"pt-e-chip\"></em></label>");
            sb.Append("<label class=\"pt-field pt-span2\"><span>Alert <small>shown in red to everyone, for example: bites when nervous</small></span><input type=\"text\" id=\"pt-f-alert\" maxlength=\"200\" value=\"" + Ui.Attr(v.Alert) + "\"></label>");
            if (!add)
            {
                sb.Append("<label class=\"pt-check\"><input type=\"checkbox\" id=\"pt-f-active\"" + (v.Active ? " checked" : string.Empty) + "><span>Active pet</span></label>");
                sb.Append("<label class=\"pt-field\"><span>Passed away on <small>leave empty if alive</small></span><input type=\"date\" id=\"pt-f-died\" value=\"" + (v.DeceasedDate.HasValue ? Ui.Ymd(v.DeceasedDate.Value) : string.Empty) + "\" max=\"" + Ui.Ymd(today) + "\"><em class=\"pt-err\" id=\"pt-e-died\"></em></label>");
            }
            sb.Append("</div><em class=\"pt-err\" id=\"pt-e-form\"></em>");
            sb.Append("<div class=\"pt-facts\"><button type=\"submit\" class=\"pt-btn pt-btn-accent\">" + (add ? "Add pet" : "Save changes") + "</button><button type=\"button\" class=\"pt-btn pt-btn-ghost\" onclick=\"" + (add ? "AdminPetsJs.closeModal()" : "AdminPetsJs.open('" + Ui.Js(v.PetId) + "')") + "\">Cancel</button></div></form>");
            response.SetElementContents("pt-mbody", sb.ToString());
            response.ExecuteScript("AdminPetsJs.shown('');");
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("petid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, add ? "add pet" : "edit pet", "not allowed", false);
                Toast(response, add ? "You can't add pets." : "You can't change pet records.");
                return response;
            }
            PtPet? old = null;
            if (!add)
            {
                old = await Pet(id);
                if (old == null)
                {
                    Toast(response, "This pet was not found. It may have been deleted.");
                    return response;
                }
            }
            Dictionary<string, string> s = await App.Settings();
            DateTime today = App.Now(s).Date;
            string ownerId = CleanId(GetDataValue("ownerid"));
            PtOwner? owner = ownerId == string.Empty ? null : await Owner(ownerId);
            string name = Ui.OneLine(GetDataValue("name"), 60);
            string speciesKey = Val("species", 20);
            List<PtSpecies> species = await SpeciesList();
            PtSpecies? sp = species.FirstOrDefault(x => x.SpeciesKey == speciesKey);
            if (sp == null && old != null && old.SpeciesKey == speciesKey)
            {
                sp = new PtSpecies { SpeciesKey = old.SpeciesKey, SpeciesName = old.SpeciesName, HasSize = old.HasSize };
            }
            string breed = Ui.OneLine(GetDataValue("breed"), 60);
            string sex = Val("sex", 1).ToLowerInvariant();
            if (sex != "m" && sex != "f")
            {
                sex = string.Empty;
            }
            bool fix = (GetDataValue("fixed") ?? string.Empty).Trim() == "1";
            string birthText = Val("birth", 20);
            DateTime? birth = Ui.ParseDay(birthText);
            string color = Ui.OneLine(GetDataValue("color"), 60);
            string size = Val("size", 10);
            if (sp == null || !sp.HasSize || (size != "small" && size != "medium" && size != "large"))
            {
                size = string.Empty;
            }
            string chip = Regex.Replace(Ui.Clip(GetDataValue("chip"), 60), "[\\s-]", string.Empty);
            string alert = Ui.OneLine(GetDataValue("alert"), 200);
            bool active = add || (GetDataValue("active") ?? string.Empty).Trim() == "1";
            string diedText = add ? string.Empty : Val("died", 20);
            DateTime? died = Ui.ParseDay(diedText);
            Dictionary<string, string> errs = new Dictionary<string, string> { { "owner", string.Empty }, { "name", string.Empty }, { "species", string.Empty }, { "birth", string.Empty }, { "chip", string.Empty }, { "died", string.Empty }, { "form", string.Empty } };
            if (owner == null)
            {
                errs["owner"] = "Pick the owner.";
            }
            if (name == string.Empty)
            {
                errs["name"] = "Enter the pet's name.";
            }
            if (sp == null)
            {
                errs["species"] = "Pick the species.";
            }
            if (birthText != string.Empty && birth == null)
            {
                errs["birth"] = "This date doesn't look right.";
            }
            else if (birth != null && birth.Value > today)
            {
                errs["birth"] = "The birth date can't be in the future.";
            }
            else if (birth != null && birth.Value < today.AddYears(-80))
            {
                errs["birth"] = "This birth date is too long ago.";
            }
            if (!Regex.IsMatch(chip, "^[A-Za-z0-9]{0,30}$"))
            {
                errs["chip"] = "Use only letters and numbers, up to 30.";
            }
            if (diedText != string.Empty && died == null)
            {
                errs["died"] = "This date doesn't look right.";
            }
            else if (died != null && died.Value > today)
            {
                errs["died"] = "This date can't be in the future.";
            }
            else if (died != null && birth != null && died.Value < birth.Value)
            {
                errs["died"] = "This date is before the birth date.";
            }
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("pt-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                return response;
            }
            if (died != null)
            {
                active = false;
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("pid", add ? App.NewId() : id, 50),
                App.P("cid", owner!.ClientId, 50),
                App.P("name", name, 60),
                App.P("species", sp!.SpeciesKey, 20),
                App.P("breed", breed, 60),
                App.P("sex", sex, 1),
                App.PBit("fixed", fix),
                App.PDate("birth", birth),
                App.P("color", color, 60),
                App.P("size", size, 10),
                App.P("chip", chip, 30),
                App.P("alert", alert, 200),
                App.PBit("active", active),
                App.PDate("died", died),
                App.P("me", c.U.StaffId, 50)
            };
            string petId = Convert.ToString(ps[0].Value) ?? string.Empty;
            DateTime todayLocal = App.Now(await App.Settings()).Date;
            ps.Add(App.PDate("today", todayLocal));
            ps.Add(App.P("creason", died != null ? "Pet passed away" : "Pet marked inactive", 200));
            List<string> after = new List<string>
            {
                "UPDATE APPOINTMENT SET CLIENT_ID = @cid, SYSDTE = GETDATE() WHERE PET_ID = @pid AND CLIENT_ID <> @cid AND STATUS IN (N'booked', N'confirmed', N'checkedin') AND START_AT >= @today",
                "UPDATE VISIT SET CLIENT_ID = @cid, SYSDTE = GETDATE() WHERE PET_ID = @pid AND CLIENT_ID <> @cid AND STATUS IN (N'waiting', N'inroom', N'checkout')"
            };
            if (!active || died != null)
            {
                after.Add("INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) SELECT CONVERT(NVARCHAR(50), NEWID()), APPT_ID, SYSUTCDATETIME(), @me, N'Cancelled: ' + @creason FROM APPOINTMENT WHERE PET_ID = @pid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today");
                after.Add("UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = @creason, SYSDTE = GETDATE() WHERE PET_ID = @pid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today");
            }
            string result = add
                ? await Helper.PutData(new List<string> { "INSERT INTO PET (PET_ID, CLIENT_ID, PET_NAME, SPECIES_KEY, BREED, SEX, FIXED, BIRTH_DATE, COLOR, SIZE, MICROCHIP, ALERT, PHOTO, ACTIVE, DECEASED_DATE, CREATED_UTC, CREATED_BY, SYSDTE) VALUES (@pid, @cid, @name, @species, @breed, @sex, @fixed, CAST(@birth AS DATE), @color, @size, @chip, @alert, N'', 1, NULL, SYSUTCDATETIME(), @me, GETDATE())" }, ps)
                : await Helper.PutData(new List<string> { "UPDATE PET SET CLIENT_ID = @cid, PET_NAME = @name, SPECIES_KEY = @species, BREED = @breed, SEX = @sex, FIXED = @fixed, BIRTH_DATE = CAST(@birth AS DATE), COLOR = @color, SIZE = @size, MICROCHIP = @chip, ALERT = @alert, ACTIVE = @active, DECEASED_DATE = CAST(@died AS DATE), SYSDTE = GETDATE() WHERE PET_ID = @pid" }.Concat(after).ToList(), ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, add ? "add pet" : "edit pet", name + " failed", false);
                response.SetElementContents("pt-e-form", "The pet could not be saved. Try again.");
                return response;
            }
            string what = name + " (" + owner.FirstName + " " + owner.LastName + ")";
            await App.Log(c.U.StaffId, add ? "added pet" : died != null && old?.DeceasedDate == null ? "marked pet passed away" : "edited pet", what, true);
            await Detail(response, c, s, petId, add ? name + " is saved." : string.Empty);
            response.ExecuteScript("AdminPetsJs.changed('" + Ui.Js(add ? "The new pet is saved." : "Changes saved.") + "');");
            return response;
        }

        private static async Task<PtUseRow> Use(string id)
        {
            var (row, _) = await Helper.GetOneT<PtUseRow>("SELECT (SELECT COUNT(*) FROM VISIT WHERE PET_ID = @pid) AS Visits, (SELECT COUNT(*) FROM APPOINTMENT WHERE PET_ID = @pid) AS Appts", new List<SqlParameter> { App.P("pid", id, 50) });
            return row ?? new PtUseRow { Visits = 1 };
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
                Toast(response, "You can't delete pets.");
                return response;
            }
            string id = CleanId(GetDataValue("petid"));
            PtPet? p = id == string.Empty ? null : await Pet(id);
            if (p == null)
            {
                Toast(response, "This pet was not found. It may have been deleted.");
                return response;
            }
            PtUseRow u = await Use(id);
            string pid = Ui.Js(id);
            string name = Ui.Enc(p.PetName);
            StringBuilder sb = new StringBuilder();
            if (u.Visits > 0 || u.Appts > 0)
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
                sb.Append("<h2>" + name + " can't be deleted</h2><p class=\"pt-lead\">" + name + " has " + Ui.Enc(string.Join(" and ", why)) + " on file. Medical records must be kept.</p>");
                if (c.Can(Key, 'E') && p.Active)
                {
                    sb.Append("<p>Mark the pet inactive instead. It drops off the pet list but the history stays. If " + name + " passed away, edit the pet and fill in the date.</p><div class=\"pt-facts\"><button type=\"button\" class=\"pt-btn pt-btn-accent\" onclick=\"AdminPetsJs.retire('" + pid + "')\">Mark inactive</button>");
                }
                else
                {
                    sb.Append("<div class=\"pt-facts\">");
                }
                sb.Append("<button type=\"button\" class=\"pt-btn pt-btn-ghost\" onclick=\"AdminPetsJs.open('" + pid + "')\">Back</button></div>");
            }
            else
            {
                sb.Append("<h2>Delete " + name + "?</h2><p class=\"pt-lead\">The pet, with its weights and vaccines, is removed for good. This can't be undone.</p><div class=\"pt-facts\"><button type=\"button\" class=\"pt-btn pt-btn-danger\" onclick=\"AdminPetsJs.remove('" + pid + "')\">Delete for good</button><button type=\"button\" class=\"pt-btn pt-btn-ghost\" onclick=\"AdminPetsJs.open('" + pid + "')\">Keep</button></div>");
            }
            response.SetElementContents("pt-mbody", sb.ToString());
            response.ExecuteScript("AdminPetsJs.shown('');");
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
            if (!c.Can(Key, 'D'))
            {
                await App.Log(c.U.StaffId, "delete pet", "not allowed", false);
                Toast(response, "You can't delete pets.");
                return response;
            }
            string id = CleanId(GetDataValue("petid"));
            PtPet? p = id == string.Empty ? null : await Pet(id);
            if (p == null)
            {
                Toast(response, "This pet was not found. It may have been deleted.");
                return response;
            }
            PtUseRow u = await Use(id);
            if (u.Visits > 0 || u.Appts > 0)
            {
                Toast(response, "This pet has history and can't be deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "IF EXISTS (SELECT 1 FROM APPOINTMENT WHERE PET_ID = @pid) OR EXISTS (SELECT 1 FROM VISIT WHERE PET_ID = @pid) THROW 50001, N'PETBUSY', 1;",
                "DELETE FROM PETWEIGHT WHERE PET_ID = @pid",
                "DELETE FROM PETVACCINE WHERE PET_ID = @pid",
                "DELETE FROM REMINDER WHERE PET_ID = @pid",
                "DELETE FROM PET WHERE PET_ID = @pid"
            }, new List<SqlParameter> { App.P("pid", id, 50) });
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "delete pet", p.PetName + " failed", false);
                Toast(response, result.Contains("PETBUSY") ? "This pet just got history and can't be deleted." : "The pet could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "deleted pet", p.PetName + " (" + p.OwnerName + ")", true);
            response.ExecuteScript("AdminPetsJs.removed('" + Ui.Js(p.PetName + " was deleted.") + "');");
            return response;
        }

        public async Task<ApiResponse> Retire()
        {
            ApiResponse response = new ApiResponse();
            var (c, p) = await EditGate(response, "mark pet inactive");
            if (c == null || p == null)
            {
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "UPDATE PET SET ACTIVE = 0, SYSDTE = GETDATE() WHERE PET_ID = @pid",
                "INSERT INTO APPTLOG (LOG_ID, APPT_ID, LOG_UTC, STAFF_ID, LOG_TEXT) SELECT CONVERT(NVARCHAR(50), NEWID()), APPT_ID, SYSUTCDATETIME(), @me, N'Cancelled: pet marked inactive' FROM APPOINTMENT WHERE PET_ID = @pid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today",
                "UPDATE APPOINTMENT SET STATUS = N'cancelled', CANCEL_REASON = N'Pet marked inactive', SYSDTE = GETDATE() WHERE PET_ID = @pid AND STATUS IN (N'booked', N'confirmed') AND START_AT >= @today"
            }, new List<SqlParameter> { App.P("pid", p.PetId, 50), App.P("me", c.U.StaffId, 50), App.PDate("today", App.Now(await App.Settings()).Date) });
            if (Helper.IsError(result))
            {
                Toast(response, "The pet could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "marked pet inactive", p.PetName + " (" + p.OwnerName + ")", true);
            await Detail(response, c, await App.Settings(), p.PetId, string.Empty);
            response.ExecuteScript("AdminPetsJs.changed('" + Ui.Js(p.PetName + " is now inactive.") + "');");
            return response;
        }
    }
}
