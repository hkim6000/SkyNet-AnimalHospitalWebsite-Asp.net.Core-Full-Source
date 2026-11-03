using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminSettings : WebPage
    {
        private const string Key = "settings";
        private const string Me = "AdminSettingsJs";
        private static readonly string[] Tabs = { "hospital", "hours", "bookings", "boarding", "reminders", "billing", "other", "species" };
        private static readonly int[] Slots = { 5, 10, 15, 20, 30 };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Settings | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--st-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("st"));
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
            return "<div class=\"st-panel st-deny\"><h1>Settings</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"st-btn st-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static string TabName(string t)
        {
            return t switch
            {
                "hospital" => "Hospital",
                "hours" => "Opening hours",
                "bookings" => "Bookings",
                "boarding" => "Boarding",
                "reminders" => "Reminders",
                "billing" => "Billing & email",
                "other" => "Other",
                "species" => "Species",
                _ => t
            };
        }

        private static string Fid(string key)
        {
            return key.Replace('.', '-');
        }

        private static string Err(string key)
        {
            return "<em class=\"st-err\" id=\"st-e-" + Fid(key) + "\"></em>";
        }

        private static string Dis(bool canE)
        {
            return canE ? string.Empty : " disabled";
        }

        private static string Text(string key, string label, string value, int max, bool canE, string hint = "", string type = "text", string cls = "")
        {
            return "<div class=\"st-fw" + (cls != string.Empty ? " " + cls : string.Empty) + "\"><label class=\"st-field\"><span>" + label + "</span><input type=\"" + type + "\" id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\" maxlength=\"" + max.ToString(Ui.Inv) + "\" value=\"" + Ui.Attr(value) + "\" autocomplete=\"off\"" + Dis(canE) + "></label>" + (hint != string.Empty ? "<p class=\"st-hint\">" + hint + "</p>" : string.Empty) + Err(key) + "</div>";
        }

        private static string Area(string key, string label, string value, int max, int rows, bool canE, string hint = "")
        {
            return "<div class=\"st-fw st-span2\"><label class=\"st-field\"><span>" + label + "</span><textarea id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\" rows=\"" + rows.ToString(Ui.Inv) + "\" maxlength=\"" + max.ToString(Ui.Inv) + "\"" + Dis(canE) + ">" + Ui.Enc(value) + "</textarea></label>" + (hint != string.Empty ? "<p class=\"st-hint\">" + hint + "</p>" : string.Empty) + Err(key) + "</div>";
        }

        private static string Num(string key, string label, string value, int min, int max, string unit, bool canE, string hint = "")
        {
            return "<div class=\"st-fw\"><label class=\"st-field\"><span>" + label + "</span><span class=\"st-unit\"><input type=\"number\" inputmode=\"numeric\" id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\" min=\"" + min.ToString(Ui.Inv) + "\" max=\"" + max.ToString(Ui.Inv) + "\" value=\"" + Ui.Attr(value) + "\"" + Dis(canE) + "><i>" + Ui.Enc(unit) + "</i></span></label>" + (hint != string.Empty ? "<p class=\"st-hint\">" + hint + "</p>" : string.Empty) + Err(key) + "</div>";
        }

        private static string Check(string key, string label, bool on, bool canE, string hint = "")
        {
            return "<div class=\"st-fw st-span2\"><label class=\"st-check\"><input type=\"checkbox\" id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\"" + (on ? " checked" : string.Empty) + Dis(canE) + ">" + label + "</label>" + (hint != string.Empty ? "<p class=\"st-hint st-hint-c\">" + hint + "</p>" : string.Empty) + Err(key) + "</div>";
        }

        private static string Time(string key, string label, string value, bool canE)
        {
            return "<div class=\"st-fw\"><label class=\"st-field\"><span>" + label + "</span><input type=\"time\" id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\" value=\"" + Ui.Attr(Ui.ParseHm(value) != null ? value : string.Empty) + "\"" + Dis(canE) + "></label>" + Err(key) + "</div>";
        }

        private static string Foot(string tab, bool canE, string label)
        {
            if (!canE)
            {
                return string.Empty;
            }
            return "<div class=\"st-sfoot\"><em class=\"st-err\" id=\"st-e-tab-" + tab + "\"></em><button type=\"button\" class=\"st-btn st-btn-accent\" id=\"st-go-" + tab + "\" onclick=\"" + Me + ".save('" + tab + "', this)\">" + label + "</button></div>";
        }

        private static List<string> Lines(string v)
        {
            return (v ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
        }

        private static List<string> Csv(string v)
        {
            return (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        private static List<(int Day, bool Open, string From, string To)> Hours(Dictionary<string, string> s)
        {
            List<(int, bool, string, string)> list = new List<(int, bool, string, string)>();
            Dictionary<int, (bool, string, string)> found = new Dictionary<int, (bool, string, string)>();
            foreach (string part in App.S(s, "hours").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split('|');
                if (f.Length >= 4 && int.TryParse(f[0], NumberStyles.Integer, Ui.Inv, out int d) && d >= 0 && d <= 6 && !found.ContainsKey(d))
                {
                    found[d] = (f[1] == "1", Ui.ParseHm(f[2]) != null ? f[2] : "09:00", Ui.ParseHm(f[3]) != null ? f[3] : "17:00");
                }
            }
            for (int d = 0; d < 7; d++)
            {
                (bool o, string a, string z) = found.TryGetValue(d, out var v) ? v : (false, "09:00", "17:00");
                list.Add((d, o, a, z));
            }
            return list;
        }

        private static async Task<List<SetVax>> Vaccines()
        {
            var (list, emsg) = await Helper.GetListT<SetVax>("SELECT ITEM_NAME AS ItemName, SPECIES AS Species FROM ITEM WHERE ITEM_TYPE = N'vaccine' AND ACTIVE = 1 ORDER BY ITEM_ORDER, ITEM_NAME");
            return emsg == string.Empty && list != null ? list : new List<SetVax>();
        }

        private static string VaxName(string itemName)
        {
            return Ui.Clip(Regex.Replace(itemName, "\\s+vaccine$", string.Empty, RegexOptions.IgnoreCase).Trim(), 60);
        }

        private static List<string> VaxFor(List<SetVax> items, string species)
        {
            return items.Where(i => Csv(i.Species).Count == 0 || Csv(i.Species).Contains(species)).Select(i => VaxName(i.ItemName)).Where(n => n != string.Empty && !n.Contains(',')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<TimeZoneInfo> Zones()
        {
            try
            {
                return TimeZoneInfo.GetSystemTimeZones().OrderBy(z => z.BaseUtcOffset).ThenBy(z => z.Id, StringComparer.Ordinal).ToList();
            }
            catch (InvalidTimeZoneException)
            {
                return new List<TimeZoneInfo>();
            }
        }

        private static string Offset(TimeZoneInfo z)
        {
            TimeSpan o = z.GetUtcOffset(DateTime.UtcNow);
            return "(UTC" + (o < TimeSpan.Zero ? "-" : "+") + o.Duration().ToString("hh\\:mm", Ui.Inv) + ")";
        }

        private static async Task<string> Panel(string tab, Dictionary<string, string> s, bool canE)
        {
            StringBuilder sb = new StringBuilder();
            switch (tab)
            {
                case "hospital":
                    {
                        sb.Append("<h2 class=\"st-h2\">Hospital</h2><p class=\"st-lead\">The name, contact details and colour used on the website, on letters and invoices and in this admin.</p><div class=\"st-fgrid\">");
                        sb.Append(Text("name", "Hospital name", App.S(s, "name"), 80, canE));
                        sb.Append(Text("tagline", "Tagline <small>(one line under the name)</small>", App.S(s, "tagline"), 200, canE));
                        sb.Append(Text("town", "Town", App.S(s, "town"), 60, canE));
                        sb.Append(Text("address", "Street address", App.S(s, "address"), 200, canE));
                        sb.Append(Text("phone", "Phone", App.S(s, "phone"), 30, canE, string.Empty, "tel"));
                        sb.Append(Text("email", "Email", App.S(s, "email"), 120, canE, "Shown to owners. Emails are sent from the address set up for the mail server.", "email"));
                        sb.Append(Area("urgent", "Urgent care message", App.S(s, "urgent"), 600, 3, canE, "Shown on the website: what owners should do in an emergency."));
                        sb.Append("</div><h3 class=\"st-h3\">Emergency partner <small>(where owners go when you are closed)</small></h3><div class=\"st-fgrid\">");
                        sb.Append(Text("er.name", "Name", App.S(s, "er.name"), 100, canE));
                        sb.Append(Text("er.phone", "Phone", App.S(s, "er.phone"), 30, canE, string.Empty, "tel"));
                        sb.Append(Text("er.address", "Address", App.S(s, "er.address"), 200, canE, string.Empty, "text", "st-span2"));
                        sb.Append("</div><h3 class=\"st-h3\">Look and insurance</h3><div class=\"st-fgrid\">");
                        string acc = App.S(s, "accent");
                        string col = Ui.SafeColor(acc) ? acc.ToLowerInvariant() : "#3f7d6b";
                        sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Accent colour</span><span class=\"st-colrow\"><input type=\"color\" id=\"st-f-accent\" data-sk=\"accent\" value=\"" + col + "\" oninput=\"" + Me + ".colour()\"" + Dis(canE) + "><b id=\"st-accv\">" + Ui.Enc(col.ToUpperInvariant()) + "</b></span></label><p class=\"st-hint\">Buttons, links and the logo in the admin and on the website.</p>" + Err("accent") + "</div>");
                        sb.Append(Area("insurance", "Insurance companies we work with <small>(one per line)</small>", string.Join("\n", Lines(App.S(s, "insurance"))), 2000, 5, canE));
                        sb.Append("</div>");
                        sb.Append(Foot(tab, canE, "Save hospital details"));
                        break;
                    }
                case "hours":
                    {
                        sb.Append("<h2 class=\"st-h2\">Opening hours</h2><p class=\"st-lead\">When the hospital is open. Online booking, the schedule and the website use these hours. Each vet's and groomer's own hours are set in Hours and time off.</p>");
                        sb.Append("<div class=\"st-hours\" role=\"group\" aria-label=\"Opening hours\"><div class=\"st-hrow st-hhead\" aria-hidden=\"true\"><span>Day</span><span>Open</span><span>From</span><span>To</span></div>");
                        foreach (var h in Hours(s))
                        {
                            string d = h.Day.ToString(Ui.Inv);
                            string dn = Ui.DayNames[h.Day];
                            sb.Append("<div class=\"st-hrow" + (h.Open ? string.Empty : " st-closed") + "\" id=\"st-hr-" + d + "\"><b>" + dn + "</b>");
                            sb.Append("<label class=\"st-check st-hopen\"><input type=\"checkbox\" id=\"st-f-h" + d + "-on\" data-sk=\"h" + d + ".on\"" + (h.Open ? " checked" : string.Empty) + Dis(canE) + " onchange=\"" + Me + ".day(" + d + ")\"><span>Open</span></label>");
                            sb.Append("<label class=\"st-field\"><span class=\"st-sr\">" + dn + " from</span><input type=\"time\" id=\"st-f-h" + d + "-from\" data-sk=\"h" + d + ".from\" value=\"" + Ui.Attr(h.From) + "\"" + Dis(canE) + "></label>");
                            sb.Append("<label class=\"st-field\"><span class=\"st-sr\">" + dn + " to</span><input type=\"time\" id=\"st-f-h" + d + "-to\" data-sk=\"h" + d + ".to\" value=\"" + Ui.Attr(h.To) + "\"" + Dis(canE) + "></label>");
                            sb.Append("<em class=\"st-err st-herr\" id=\"st-e-h" + d + "\"></em></div>");
                        }
                        sb.Append("</div>");
                        sb.Append(Foot(tab, canE, "Save opening hours"));
                        break;
                    }
                case "bookings":
                    {
                        sb.Append("<h2 class=\"st-h2\">Bookings</h2><p class=\"st-lead\">How appointments can be booked online and at the desk.</p><div class=\"st-fgrid\">");
                        string cur = App.S(s, "slot.minutes", "15");
                        sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Start times every</span><select id=\"st-f-slot-minutes\" data-sk=\"slot.minutes\"" + Dis(canE) + ">" + Ui.Opts(Slots.Select(m => (m.ToString(Ui.Inv), m.ToString(Ui.Inv) + " minutes")), cur) + "</select></label><p class=\"st-hint\">Appointments can start on these steps, for example 9:00, 9:15, 9:30.</p>" + Err("slot.minutes") + "</div>");
                        sb.Append(Num("book.notice.hours", "Shortest notice for online booking", App.S(s, "book.notice.hours", "2"), 0, 168, "hours", canE, "Owners can't book online closer to the start than this."));
                        sb.Append(Num("book.ahead.days", "Furthest ahead owners can book", App.S(s, "book.ahead.days", "60"), 1, 365, "days", canE));
                        sb.Append(Num("cancel.hours", "Owners can cancel online up to", App.S(s, "cancel.hours", "24"), 0, 168, "hours before", canE));
                        sb.Append(Check("show.prices", "Show prices on the website and when booking", App.S(s, "show.prices") == "1", canE));
                        sb.Append("</div>");
                        sb.Append(Foot(tab, canE, "Save booking settings"));
                        break;
                    }
                case "boarding":
                    {
                        sb.Append("<h2 class=\"st-h2\">Boarding</h2><p class=\"st-lead\">Rules for overnight stays in the dog runs and cat condos.</p><div class=\"st-fgrid\">");
                        sb.Append(Time("stay.checkin.from", "Check-in from", App.S(s, "stay.checkin.from", "07:30"), canE));
                        sb.Append(Time("stay.checkin.to", "Check-in until", App.S(s, "stay.checkin.to", "18:00"), canE));
                        sb.Append(Time("stay.checkout.by", "Check-out by", App.S(s, "stay.checkout.by", "11:00"), canE));
                        sb.Append(Num("stay.max.nights", "Longest stay", App.S(s, "stay.max.nights", "30"), 1, 90, "nights", canE));
                        sb.Append(Num("stay.cancel.hours", "Owners can cancel a stay online up to", App.S(s, "stay.cancel.hours", "48"), 0, 336, "hours before", canE));
                        sb.Append("</div><h3 class=\"st-h3\">Vaccines needed for a stay</h3><p class=\"st-lead\">The pet must have these vaccines up to date until the day it goes home. The list comes from the active vaccines in Items and prices.</p>");
                        List<SetVax> items = await Vaccines();
                        foreach ((string sp, string label) in new[] { ("dog", "Dogs"), ("cat", "Cats") })
                        {
                            string key = "stay.vax." + sp;
                            List<string> have = Csv(App.S(s, key));
                            List<string> opts = VaxFor(items, sp);
                            sb.Append("<fieldset class=\"st-fs\"><legend>" + label + "</legend><div class=\"st-checks\" id=\"st-f-" + Fid(key) + "\" data-sk=\"" + key + "\" data-multi=\"1\">");
                            foreach (string n in opts)
                            {
                                bool on = have.Any(h => h.Equals(n, StringComparison.OrdinalIgnoreCase));
                                sb.Append("<label class=\"st-check\"><input type=\"checkbox\" class=\"st-vx\" value=\"" + Ui.Attr(n) + "\"" + (on ? " checked" : string.Empty) + Dis(canE) + ">" + Ui.Enc(n) + "</label>");
                            }
                            foreach (string n in have.Where(h => !opts.Any(o => o.Equals(h, StringComparison.OrdinalIgnoreCase))))
                            {
                                sb.Append("<label class=\"st-check\"><input type=\"checkbox\" class=\"st-vx\" value=\"" + Ui.Attr(n) + "\" checked" + Dis(canE) + ">" + Ui.Enc(n) + " <small class=\"st-dim\">(not an active vaccine item)</small></label>");
                            }
                            if (opts.Count == 0 && have.Count == 0)
                            {
                                sb.Append("<p class=\"st-sub\">No active vaccines for " + label.ToLowerInvariant() + " in Items and prices.</p>");
                            }
                            sb.Append("</div>" + Err(key) + "</fieldset>");
                        }
                        sb.Append(Foot(tab, canE, "Save boarding settings"));
                        break;
                    }
                case "reminders":
                    {
                        sb.Append("<h2 class=\"st-h2\">Reminders</h2><p class=\"st-lead\">When vaccines, rechecks and yearly check-ups show up on the Reminders list.</p><div class=\"st-fgrid\">");
                        sb.Append(Num("remind.days", "Remind this many days before something is due", App.S(s, "remind.days", "30"), 1, 180, "days", canE));
                        sb.Append(Num("wellness.months", "Wellness exam every", App.S(s, "wellness.months", "12"), 1, 60, "months", canE, "A pet with no visit for this long shows as due for a check-up."));
                        sb.Append("</div>");
                        sb.Append(Foot(tab, canE, "Save reminder settings"));
                        break;
                    }
                case "billing":
                    {
                        sb.Append("<h2 class=\"st-h2\">Billing &amp; email</h2><p class=\"st-lead\">Tax, due dates and what is printed on invoices.</p><div class=\"st-fgrid\">");
                        sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Sales tax</span><span class=\"st-unit\"><input type=\"text\" inputmode=\"decimal\" id=\"st-f-tax-rate\" data-sk=\"tax.rate\" maxlength=\"8\" value=\"" + Ui.Attr(Billing.TaxRate(s).ToString("0.###", Ui.Inv)) + "\"" + Dis(canE) + "><i>%</i></span></label><p class=\"st-hint\">Added to items marked as taxable, for example products. Use 0 for no tax.</p>" + Err("tax.rate") + "</div>");
                        sb.Append(Num("invoice.due.days", "Billed invoices are due after", App.S(s, "invoice.due.days", "30"), 0, 365, "days", canE));
                        sb.Append(Area("invoice.note", "Note on every invoice and statement", App.S(s, "invoice.note"), 600, 3, canE));
                        sb.Append(Check("mail.after.checkout", "Email the visit summary and invoice to the owner when checkout is finished", App.S(s, "mail.after.checkout") == "1", canE));
                        sb.Append("</div>");
                        bool ready = Letters.MailReady();
                        string from = App.Cfg("app.mail.addr");
                        sb.Append("<div class=\"st-mail " + (ready ? "st-mail-ok" : "st-mail-no") + "\"><b>" + (ready ? "Email is set up" : "Email is not set up yet") + "</b><p>" + (ready ? "Emails are sent from <span class=\"st-mailaddr\">" + Ui.Enc(from) + "</span>." : "Invoices and visit summaries can't be emailed until the mail server is set up. Until then, print them or hand them over.") + "</p>");
                        if (App.Demo)
                        {
                            sb.Append("<p>This site runs in demo mode, so no email is really sent; each email is only written to the email log.</p>");
                        }
                        sb.Append("<p class=\"st-hint\">The mail server, its sign-in and the from-address are not changed here. They are set in the website's configuration file (appConfig/application.cfg) by whoever looks after the website.</p></div>");
                        sb.Append(Foot(tab, canE, "Save billing settings"));
                        break;
                    }
                case "other":
                    {
                        sb.Append("<h2 class=\"st-h2\">Other</h2><p class=\"st-lead\">Time zone, clock and upload sizes.</p><div class=\"st-fgrid\">");
                        string tz = App.S(s, "timezone");
                        List<TimeZoneInfo> zones = Zones();
                        StringBuilder zo = new StringBuilder();
                        if (tz == string.Empty || zones.All(z => z.Id != tz))
                        {
                            zo.Append("<option value=\"" + Ui.Attr(tz) + "\" selected>" + Ui.Enc(tz == string.Empty ? "Server time zone" : tz + " (unknown)") + "</option>");
                        }
                        foreach (TimeZoneInfo z in zones)
                        {
                            zo.Append("<option value=\"" + Ui.Attr(z.Id) + "\"" + (z.Id == tz ? " selected" : string.Empty) + ">" + Ui.Enc(Offset(z) + " " + z.Id) + "</option>");
                        }
                        sb.Append("<div class=\"st-fw st-span2\"><label class=\"st-field\"><span>Time zone of the hospital</span><select id=\"st-f-timezone\" data-sk=\"timezone\"" + Dis(canE) + ">" + zo + "</select></label><p class=\"st-hint\">All times in the admin and on the website follow this time zone. Now it is " + Ui.Enc(Ui.Hm(App.Now(s), App.S(s, "clock24") == "1") + " on " + Ui.LongDay(App.Now(s))) + " there.</p>" + Err("timezone") + "</div>");
                        sb.Append(Check("clock24", "Show times with the 24-hour clock (14:30 instead of 2:30 PM)", App.S(s, "clock24") == "1", canE));
                        sb.Append(Num("video.mb", "Largest video upload", App.S(s, "video.mb", "25"), 1, 500, "MB", canE));
                        sb.Append(Num("file.mb", "Largest file upload on visits", App.S(s, "file.mb", "15"), 1, 100, "MB", canE));
                        sb.Append("</div>");
                        sb.Append(Foot(tab, canE, "Save"));
                        break;
                    }
                case "species":
                    {
                        sb.Append(await SpeciesHtml(canE));
                        break;
                    }
            }
            return sb.ToString();
        }

        private static async Task<List<SetSpecies>> SpeciesRows()
        {
            var (list, emsg) = await Helper.GetListT<SetSpecies>("SELECT S.SPECIES_KEY AS SpeciesKey, S.SPECIES_NAME AS SpeciesName, S.PLURAL_NAME AS PluralName, S.HAS_SIZE AS HasSize, S.SPECIES_ORDER AS SpeciesOrder, S.ACTIVE AS Active, ISNULL(P.N, 0) AS Pets FROM SPECIES S LEFT JOIN (SELECT SPECIES_KEY, COUNT(*) AS N FROM PET WHERE ACTIVE = 1 AND DECEASED_DATE IS NULL GROUP BY SPECIES_KEY) P ON P.SPECIES_KEY = S.SPECIES_KEY ORDER BY S.SPECIES_ORDER, S.SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<SetSpecies>();
        }

        private static async Task<string> SpeciesHtml(bool canE)
        {
            List<SetSpecies> rows = await SpeciesRows();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"st-shead\"><div><h2 class=\"st-h2\">Species</h2><p class=\"st-lead\">The kinds of animals the hospital sees. Inactive species can't be picked for new pets; pets already on file keep theirs.</p></div>");
            if (canE)
            {
                sb.Append("<button type=\"button\" class=\"st-btn st-btn-accent\" onclick=\"" + Me + ".spForm('')\">Add a species</button>");
            }
            sb.Append("</div><div class=\"st-tw\"><table class=\"st-table\"><thead><tr><th>Species</th><th>Plural</th><th>Sizes</th><th class=\"st-num\">Pets on file</th><th class=\"st-num\">Order</th><th>Status</th><th><span class=\"st-sr\">Actions</span></th></tr></thead><tbody>");
            foreach (SetSpecies r in rows)
            {
                sb.Append("<tr class=\"" + (r.Active ? string.Empty : "st-off") + "\"><td class=\"st-cname\"><b>" + Ui.Enc(r.SpeciesName) + "</b><small>" + Ui.Enc(r.SpeciesKey) + "</small></td><td data-l=\"Plural\">" + Ui.Enc(r.PluralName) + "</td><td data-l=\"Sizes\">" + (r.HasSize ? "Small, medium, large" : "<span class=\"st-dim\">No sizes</span>") + "</td><td data-l=\"Pets on file\" class=\"st-num\">" + r.Pets.ToString("#,##0", Ui.Inv) + "</td><td data-l=\"Order\" class=\"st-num\">" + r.SpeciesOrder.ToString(Ui.Inv) + "</td><td data-l=\"Status\">" + (r.Active ? "<span class=\"st-st st-st-on\">Active</span>" : "<span class=\"st-st st-st-off\">Inactive</span>") + "</td><td class=\"st-acell\">" + (canE ? "<button type=\"button\" class=\"st-btn st-btn-sm st-btn-ghost\" onclick=\"" + Me + ".spForm('" + Ui.Js(r.SpeciesKey) + "')\">Edit</button> <button type=\"button\" class=\"st-btn st-btn-sm st-btn-danger\" onclick=\"" + Me + ".spDel('" + Ui.Js(r.SpeciesKey) + "')\">Delete</button>" : string.Empty) + "</td></tr>");
            }
            if (rows.Count == 0)
            {
                sb.Append("<tr><td colspan=\"7\" class=\"st-empty\">No species yet.</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("st-menu", Auth.Menu("st", Me, "AdminSettings", c));
            response.SetElementContents("st-who", Auth.Who("st", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("st-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            bool canE = c.Can(Key, 'E');
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"st-ptop\"><div><h1>Settings</h1><p>Hospital details, opening hours, booking rules, boarding, reminders, tax and email. Each tab is saved on its own.</p></div></div>");
            if (!canE)
            {
                sb.Append("<p class=\"st-info\">You can look at the settings but not change them.</p>");
            }
            sb.Append("<div class=\"st-tabs\" role=\"tablist\" aria-label=\"Settings\">");
            foreach (string t in Tabs)
            {
                sb.Append("<button type=\"button\" role=\"tab\" class=\"st-tab\" id=\"st-tab-" + t + "\" aria-controls=\"st-p-" + t + "\" aria-selected=\"false\" onclick=\"" + Me + ".tab('" + t + "')\">" + Ui.Enc(TabName(t)) + "</button>");
            }
            sb.Append("</div>");
            foreach (string t in Tabs)
            {
                sb.Append("<section class=\"st-panel st-tabp\" role=\"tabpanel\" id=\"st-p-" + t + "\" aria-labelledby=\"st-tab-" + t + "\" hidden>" + await Panel(t, s, canE) + "</section>");
            }
            response.SetElementContents("st-main", sb.ToString());
            response.ExecuteScript(Me + ".ready();");
            return response;
        }

        private string F(string key)
        {
            return GetDataValue("f_" + key.Replace('.', '_')) ?? string.Empty;
        }

        private sealed class Form
        {
            public Dictionary<string, string> Next = new Dictionary<string, string>();
            public Dictionary<string, string> Err = new Dictionary<string, string>();
        }

        private void TextField(Form f, string key, int max, bool required, string label)
        {
            string v = Ui.OneLine(F(key), max + 50);
            if (required && v == string.Empty)
            {
                f.Err[key] = "Write the " + label + ".";
            }
            else if (v.Length > max)
            {
                f.Err[key] = "Keep it under " + max.ToString(Ui.Inv) + " characters.";
            }
            f.Next[key] = v;
        }

        private void IntField(Form f, string key, int min, int max)
        {
            string v = F(key).Trim();
            if (!int.TryParse(v, NumberStyles.Integer, Ui.Inv, out int n) || n < min || n > max)
            {
                f.Err[key] = "Write a whole number from " + min.ToString(Ui.Inv) + " to " + max.ToString(Ui.Inv) + ".";
                f.Next[key] = v;
                return;
            }
            f.Next[key] = n.ToString(Ui.Inv);
        }

        private void BitField(Form f, string key)
        {
            f.Next[key] = F(key).Trim() == "1" ? "1" : "0";
        }

        private void HmField(Form f, string key)
        {
            string v = F(key).Trim();
            TimeSpan? t = Ui.ParseHm(v);
            if (t == null || t.Value >= TimeSpan.FromHours(24))
            {
                f.Err[key] = "Choose a time.";
                f.Next[key] = v;
                return;
            }
            f.Next[key] = t.Value.ToString("hh\\:mm", Ui.Inv);
        }

        private async Task<Form> Read(string tab, Dictionary<string, string> s)
        {
            Form f = new Form();
            switch (tab)
            {
                case "hospital":
                    {
                        TextField(f, "name", 80, true, "hospital name");
                        TextField(f, "tagline", 200, false, "tagline");
                        TextField(f, "town", 60, false, "town");
                        TextField(f, "address", 200, false, "address");
                        string ph = F("phone").Trim();
                        string phc = Ui.CleanPhone(ph);
                        if (ph.Length > 30 || phc != ph || (ph != string.Empty && Ui.Digits(ph).Length < 7))
                        {
                            f.Err["phone"] = "Write a phone number with digits, for example (555) 014-2200.";
                        }
                        f.Next["phone"] = phc;
                        string em = F("email").Trim();
                        if (em != string.Empty && !Ui.Email(em))
                        {
                            f.Err["email"] = "Write a full email address, or leave it empty.";
                        }
                        f.Next["email"] = em;
                        TextField(f, "urgent", 600, false, "message");
                        TextField(f, "er.name", 100, false, "name");
                        string erp = F("er.phone").Trim();
                        string erpc = Ui.CleanPhone(erp);
                        if (erp.Length > 30 || erpc != erp || (erp != string.Empty && Ui.Digits(erp).Length < 7))
                        {
                            f.Err["er.phone"] = "Write a phone number with digits.";
                        }
                        f.Next["er.phone"] = erpc;
                        TextField(f, "er.address", 200, false, "address");
                        string acc = F("accent").Trim().ToUpperInvariant();
                        if (!Ui.SafeColor(acc))
                        {
                            f.Err["accent"] = "Pick a colour.";
                        }
                        f.Next["accent"] = acc;
                        List<string> ins = Lines(F("insurance")).Select(x => Regex.Replace(x, "\\s+", " ")).ToList();
                        if (ins.Count > 30)
                        {
                            f.Err["insurance"] = "Keep the list to 30 lines or fewer.";
                        }
                        else if (ins.Any(x => x.Length > 100))
                        {
                            f.Err["insurance"] = "Keep each line under 100 characters.";
                        }
                        f.Next["insurance"] = string.Join("\n", ins);
                        break;
                    }
                case "hours":
                    {
                        List<string> parts = new List<string>();
                        var old = Hours(s);
                        for (int d = 0; d < 7; d++)
                        {
                            string ds = d.ToString(Ui.Inv);
                            bool on = F("h" + ds + ".on").Trim() == "1";
                            TimeSpan? a = Ui.ParseHm(F("h" + ds + ".from").Trim());
                            TimeSpan? z = Ui.ParseHm(F("h" + ds + ".to").Trim());
                            if (on && (a == null || z == null))
                            {
                                f.Err["h" + ds] = "Choose both times for " + Ui.DayNames[d] + ".";
                            }
                            else if (on && z!.Value <= a!.Value)
                            {
                                f.Err["h" + ds] = Ui.DayNames[d] + ": closing time must be after opening time.";
                            }
                            else if (on && (a!.Value >= TimeSpan.FromHours(24) || z!.Value > TimeSpan.FromHours(24) || z.Value == TimeSpan.FromHours(24)))
                            {
                                f.Err["h" + ds] = Ui.DayNames[d] + ": choose times within the day.";
                            }
                            string fa = a != null && a.Value < TimeSpan.FromHours(24) ? a.Value.ToString("hh\\:mm", Ui.Inv) : old[d].From;
                            string fz = z != null && z.Value < TimeSpan.FromHours(24) ? z.Value.ToString("hh\\:mm", Ui.Inv) : old[d].To;
                            parts.Add(ds + "|" + (on ? "1" : "0") + "|" + fa + "|" + fz);
                        }
                        f.Next["hours"] = string.Join(";", parts);
                        break;
                    }
                case "bookings":
                    {
                        string sm = F("slot.minutes").Trim();
                        if (!Slots.Select(x => x.ToString(Ui.Inv)).Contains(sm))
                        {
                            f.Err["slot.minutes"] = "Choose 5, 10, 15, 20 or 30 minutes.";
                        }
                        f.Next["slot.minutes"] = sm;
                        IntField(f, "book.notice.hours", 0, 168);
                        IntField(f, "book.ahead.days", 1, 365);
                        IntField(f, "cancel.hours", 0, 168);
                        BitField(f, "show.prices");
                        break;
                    }
                case "boarding":
                    {
                        HmField(f, "stay.checkin.from");
                        HmField(f, "stay.checkin.to");
                        HmField(f, "stay.checkout.by");
                        if (!f.Err.ContainsKey("stay.checkin.from") && !f.Err.ContainsKey("stay.checkin.to") && string.CompareOrdinal(f.Next["stay.checkin.to"], f.Next["stay.checkin.from"]) <= 0)
                        {
                            f.Err["stay.checkin.to"] = "Check-in must end after it starts.";
                        }
                        IntField(f, "stay.max.nights", 1, 90);
                        IntField(f, "stay.cancel.hours", 0, 336);
                        List<SetVax> items = await Vaccines();
                        foreach (string sp in new[] { "dog", "cat" })
                        {
                            string key = "stay.vax." + sp;
                            List<string> allowed = VaxFor(items, sp);
                            List<string> have = Csv(App.S(s, key));
                            List<string> given = Csv(F(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            List<string> keep = new List<string>();
                            foreach (string g in given)
                            {
                                string? hit = allowed.FirstOrDefault(a => a.Equals(g, StringComparison.OrdinalIgnoreCase)) ?? have.FirstOrDefault(h => h.Equals(g, StringComparison.OrdinalIgnoreCase));
                                if (hit == null)
                                {
                                    f.Err[key] = "One of the vaccines is not on the list any more. Reload the page.";
                                }
                                else
                                {
                                    keep.Add(hit);
                                }
                            }
                            List<string> ordered = allowed.Where(a => keep.Contains(a)).Concat(keep.Where(k => !allowed.Contains(k))).ToList();
                            f.Next[key] = string.Join(",", ordered);
                            if (f.Next[key].Length > 400)
                            {
                                f.Err[key] = "Too many vaccines ticked.";
                            }
                        }
                        break;
                    }
                case "reminders":
                    {
                        IntField(f, "remind.days", 1, 180);
                        IntField(f, "wellness.months", 1, 60);
                        break;
                    }
                case "billing":
                    {
                        string tr = F("tax.rate").Trim().TrimEnd('%').Trim();
                        if (!Regex.IsMatch(tr, "^\\d{1,2}(\\.\\d{1,3})?$") || !decimal.TryParse(tr, NumberStyles.AllowDecimalPoint, Ui.Inv, out decimal rate) || rate > 30m)
                        {
                            f.Err["tax.rate"] = "Write a percent from 0 to 30, for example 7.25.";
                            f.Next["tax.rate"] = tr;
                        }
                        else
                        {
                            f.Next["tax.rate"] = rate.ToString("0.###", Ui.Inv);
                        }
                        IntField(f, "invoice.due.days", 0, 365);
                        string note = (F("invoice.note")).Replace("\r", string.Empty).Trim();
                        note = Regex.Replace(note, "\n{3,}", "\n\n");
                        if (note.Length > 600)
                        {
                            f.Err["invoice.note"] = "Keep the note under 600 characters.";
                        }
                        f.Next["invoice.note"] = note;
                        BitField(f, "mail.after.checkout");
                        break;
                    }
                case "other":
                    {
                        string tz = F("timezone").Trim();
                        if (tz != App.S(s, "timezone") && !Zones().Any(z => z.Id == tz))
                        {
                            f.Err["timezone"] = "Choose a time zone from the list.";
                        }
                        f.Next["timezone"] = tz;
                        BitField(f, "clock24");
                        IntField(f, "video.mb", 1, 500);
                        IntField(f, "file.mb", 1, 100);
                        break;
                    }
            }
            return f;
        }

        private static string Short(string v)
        {
            string t = Regex.Replace((v ?? string.Empty).Replace("\n", " / "), "\\s+", " ").Trim();
            if (t == string.Empty)
            {
                return "(empty)";
            }
            return t.Length > 80 ? t.Substring(0, 77) + "..." : t;
        }

        private static IEnumerable<string> ErrIds(string tab)
        {
            return tab switch
            {
                "hospital" => new[] { "name", "tagline", "town", "address", "phone", "email", "urgent", "er.name", "er.phone", "er.address", "accent", "insurance" },
                "hours" => Enumerable.Range(0, 7).Select(d => "h" + d.ToString(Ui.Inv)),
                "bookings" => new[] { "slot.minutes", "book.notice.hours", "book.ahead.days", "cancel.hours", "show.prices" },
                "boarding" => new[] { "stay.checkin.from", "stay.checkin.to", "stay.checkout.by", "stay.max.nights", "stay.cancel.hours", "stay.vax.dog", "stay.vax.cat" },
                "reminders" => new[] { "remind.days", "wellness.months" },
                "billing" => new[] { "tax.rate", "invoice.due.days", "invoice.note", "mail.after.checkout" },
                "other" => new[] { "timezone", "clock24", "video.mb", "file.mb" },
                _ => Array.Empty<string>()
            };
        }

        public async Task<ApiResponse> SaveTab()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string tab = Ui.Clip(GetDataValue("stab"), 20);
            if (!Tabs.Contains(tab) || tab == "species")
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'E'))
            {
                Toast(response, "You don't have the right to change settings.");
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            Form f = await Read(tab, s);
            foreach (string k in ErrIds(tab))
            {
                response.SetElementContents("st-e-" + Fid(k), f.Err.TryGetValue(k, out string? e) ? Ui.Enc(e) : string.Empty);
            }
            if (f.Err.Count > 0)
            {
                response.SetElementContents("st-e-tab-" + tab, "Not saved. Check the fields marked in red.");
                return response;
            }
            List<string> changed = f.Next.Where(kv => !s.ContainsKey(kv.Key) || s[kv.Key] != kv.Value).Select(kv => kv.Key).ToList();
            if (changed.Count == 0)
            {
                response.SetElementContents("st-e-tab-" + tab, string.Empty);
                Toast(response, "Nothing changed.");
                return response;
            }
            List<string> sql = new List<string>();
            List<SqlParameter> ps = new List<SqlParameter>();
            for (int i = 0; i < changed.Count; i++)
            {
                string n = i.ToString(Ui.Inv);
                sql.Add("UPDATE SETTING SET SVALUE = @v" + n + ", SYSDTE = GETDATE() WHERE SKEY = @k" + n + "; IF @@ROWCOUNT = 0 INSERT INTO SETTING (SKEY, SVALUE, SYSDTE) VALUES (@k" + n + ", @v" + n + ", GETDATE());");
                ps.Add(App.P("k" + n, changed[i], 100));
                ps.Add(App.P("v" + n, f.Next[changed[i]], -1));
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "change settings", TabName(tab) + " failed", false);
                response.SetElementContents("st-e-tab-" + tab, "The settings could not be saved. Try again.");
                return response;
            }
            foreach (string k in changed)
            {
                await App.Log(c.U.StaffId, "change setting", k + ": " + Short(App.S(s, k)) + " → " + Short(f.Next[k]), true);
            }
            Dictionary<string, string> s2 = await App.Settings();
            response.SetElementContents("st-e-tab-" + tab, string.Empty);
            response.SetElementContents("st-p-" + tab, await Panel(tab, s2, true));
            string acc = App.S(s2, "accent");
            response.ExecuteScript(Me + ".saved('" + tab + "', '" + Ui.Js(Ui.SafeColor(acc) ? acc : string.Empty) + "', '" + Ui.Js(App.S(s2, "name", "Animal Hospital")) + "');");
            Toast(response, TabName(tab) + " saved: " + Ui.Plural(changed.Count, "change", "changes") + ".");
            return response;
        }

        public async Task<ApiResponse> SpForm()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'E'))
            {
                Toast(response, "You don't have the right to change settings.");
                return response;
            }
            string key = Ui.Clip(GetDataValue("spkey"), 20);
            SetSpecies r = new SetSpecies { Active = true };
            List<SetSpecies> rows = await SpeciesRows();
            if (key != string.Empty)
            {
                SetSpecies? row = rows.FirstOrDefault(x => x.SpeciesKey == key);
                if (row == null)
                {
                    response.SetElementContents("st-p-species", await SpeciesHtml(true));
                    Toast(response, "This species does not exist any more.");
                    return response;
                }
                r = row;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (key == string.Empty ? "Add a species" : "Edit " + Ui.Enc(r.SpeciesName)) + "</h2>");
            if (key != string.Empty && r.Pets > 0)
            {
                sb.Append("<p class=\"st-mlead\">" + Ui.Plural(r.Pets, "pet", "pets") + " on file are " + Ui.Enc(r.PluralName != string.Empty ? r.PluralName.ToLowerInvariant() : r.SpeciesName.ToLowerInvariant()) + ".</p>");
            }
            sb.Append("<div class=\"st-fgrid\" id=\"st-spf\" data-spkey=\"" + Ui.Attr(r.SpeciesKey) + "\">");
            if (key == string.Empty)
            {
                sb.Append("<div class=\"st-fw st-span2\"><label class=\"st-field\"><span>Short key <small>(lower-case letters only, can't be changed later)</small></span><input type=\"text\" id=\"st-spk\" maxlength=\"20\" autocomplete=\"off\" autocapitalize=\"none\" spellcheck=\"false\" placeholder=\"for example ferret\"></label><em class=\"st-err\" id=\"st-e-spk\"></em></div>");
            }
            sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Name</span><input type=\"text\" id=\"st-spn\" maxlength=\"40\" value=\"" + Ui.Attr(r.SpeciesName) + "\" placeholder=\"for example Ferret\"></label><em class=\"st-err\" id=\"st-e-spn\"></em></div>");
            sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Plural <small>(empty = name + s)</small></span><input type=\"text\" id=\"st-spp\" maxlength=\"40\" value=\"" + Ui.Attr(r.PluralName) + "\" placeholder=\"for example Ferrets\"></label><em class=\"st-err\" id=\"st-e-spp\"></em></div>");
            sb.Append("<div class=\"st-fw\"><label class=\"st-field\"><span>Order <small>(lower comes first; empty = last)</small></span><input type=\"number\" inputmode=\"numeric\" id=\"st-spo\" min=\"0\" max=\"999\" value=\"" + (key == string.Empty ? string.Empty : r.SpeciesOrder.ToString(Ui.Inv)) + "\"></label><em class=\"st-err\" id=\"st-e-spo\"></em></div>");
            sb.Append("<div class=\"st-fw st-cbox\"><label class=\"st-check\"><input type=\"checkbox\" id=\"st-sps\"" + (r.HasSize ? " checked" : string.Empty) + ">Has sizes (small, medium, large)</label></div>");
            sb.Append("<div class=\"st-fw st-span2\"><label class=\"st-check\"><input type=\"checkbox\" id=\"st-spa\"" + (r.Active ? " checked" : string.Empty) + ">Active <small class=\"st-dim\">(can be picked for new pets)</small></label></div>");
            sb.Append("</div><em class=\"st-err st-merr\" id=\"st-e-sp\"></em><div class=\"st-mfoot\"><button type=\"button\" class=\"st-btn st-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"st-btn st-btn-accent\" id=\"st-spgo\" onclick=\"" + Me + ".spSave()\">" + (key == string.Empty ? "Add species" : "Save changes") + "</button></div>");
            response.SetElementContents("st-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('" + (key == string.Empty ? "st-spk" : "st-spn") + "');");
            return response;
        }

        private static async Task<List<string>> SpeciesUses(string key)
        {
            List<string> uses = new List<string>();
            List<SqlParameter> Ps() => new List<SqlParameter> { App.P("key", key, 20) };
            string inCsv = "(N',' + REPLACE(ISNULL(SPECIES, N''), N' ', N'') + N',') LIKE N'%,' + @key + N',%'";
            var (pets, e1) = await Helper.GetDataTable("SELECT COUNT(*) FROM PET WHERE SPECIES_KEY = @key", Ps());
            int np = e1 == string.Empty ? Convert.ToInt32(pets.Rows[0][0]) : -1;
            if (np != 0)
            {
                uses.Add(np < 0 ? "Pets could not be checked." : Ui.Plural(np, "pet", "pets") + " on file (also inactive and passed-away pets)");
            }
            var (staff, e2) = await Helper.GetDataTable("SELECT S.STAFF_NAME FROM STAFFSPECIES X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.SPECIES_KEY = @key ORDER BY S.STAFF_NAME", Ps());
            if (e2 == string.Empty && staff.Rows.Count > 0)
            {
                uses.Add("Staff who see this species: " + string.Join(", ", staff.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]))));
            }
            var (tests, e3) = await Helper.GetDataTable("SELECT DISTINCT I.ITEM_NAME FROM TESTFIELD F JOIN ITEM I ON I.ITEM_ID = F.ITEM_ID WHERE F.SPECIES_KEY = @key ORDER BY I.ITEM_NAME", Ps());
            if (e3 == string.Empty && tests.Rows.Count > 0)
            {
                uses.Add("Test result ranges: " + string.Join(", ", tests.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]))));
            }
            var (books, e4) = await Helper.GetDataTable("SELECT BOOK_NAME FROM BOOKTYPE WHERE " + inCsv + " ORDER BY BOOK_NAME", Ps());
            if (e4 == string.Empty && books.Rows.Count > 0)
            {
                uses.Add("Booking types: " + string.Join(", ", books.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]))));
            }
            var (rooms, e5) = await Helper.GetDataTable("SELECT TYPE_NAME FROM ROOMTYPE WHERE " + inCsv + " ORDER BY TYPE_NAME", Ps());
            if (e5 == string.Empty && rooms.Rows.Count > 0)
            {
                uses.Add("Room types: " + string.Join(", ", rooms.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]))));
            }
            var (items, e6) = await Helper.GetDataTable("SELECT ITEM_NAME FROM ITEM WHERE " + inCsv + " ORDER BY ITEM_NAME", Ps());
            if (e6 == string.Empty && items.Rows.Count > 0)
            {
                uses.Add("Items and prices: " + string.Join(", ", items.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0])).Take(12)) + (items.Rows.Count > 12 ? " and " + (items.Rows.Count - 12).ToString(Ui.Inv) + " more" : string.Empty));
            }
            if (e2 != string.Empty || e3 != string.Empty || e4 != string.Empty || e5 != string.Empty || e6 != string.Empty)
            {
                uses.Add("Some uses could not be checked. Try again.");
            }
            return uses;
        }

        public async Task<ApiResponse> SpDelAsk()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'E'))
            {
                Toast(response, "You don't have the right to change settings.");
                return response;
            }
            string key = Ui.Clip(GetDataValue("spkey"), 20);
            SetSpecies? row = (await SpeciesRows()).FirstOrDefault(x => x.SpeciesKey == key);
            if (row == null)
            {
                response.SetElementContents("st-p-species", await SpeciesHtml(true));
                Toast(response, "This species does not exist any more.");
                return response;
            }
            List<string> uses = await SpeciesUses(key);
            var (br, _) = await Helper.GetDataTable("SELECT COUNT(*) FROM BREED WHERE SPECIES_KEY = @key", new List<SqlParameter> { App.P("key", key, 20) });
            int breeds = br.Rows.Count > 0 ? Convert.ToInt32(br.Rows[0][0]) : 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Delete " + Ui.Enc(row.SpeciesName) + "?</h2>");
            if (uses.Count > 0)
            {
                sb.Append("<p class=\"st-mlead\">" + Ui.Enc(row.SpeciesName) + " is still in use, so it can't be deleted:</p><ul class=\"st-uses\">");
                foreach (string u in uses)
                {
                    sb.Append("<li>" + Ui.Enc(u) + "</li>");
                }
                sb.Append("</ul><p class=\"st-mlead\">" + (row.Active ? "Make it inactive instead (Edit, untick Active): it then can't be picked for new pets, booking types or staff, and everything already on file keeps it." : "It is already inactive, so it can't be picked for anything new.") + "</p>");
                sb.Append("<div class=\"st-mfoot\"><button type=\"button\" class=\"st-btn st-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button>" + (row.Active ? "<button type=\"button\" class=\"st-btn st-btn-accent\" onclick=\"" + Me + ".spForm('" + Ui.Js(key) + "')\">Edit species</button>" : string.Empty) + "</div>");
            }
            else
            {
                sb.Append("<p class=\"st-mlead\">Nothing uses this species: no pets, staff, test ranges, booking types, room types or items." + (breeds > 0 ? " Its " + Ui.Plural(breeds, "breed suggestion", "breed suggestions") + " will be deleted too." : string.Empty) + " This can't be undone.</p>");
                sb.Append("<em class=\"st-err st-merr\" id=\"st-e-spdel\"></em><div class=\"st-mfoot\"><button type=\"button\" class=\"st-btn st-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"st-btn st-btn-danger\" id=\"st-spdelgo\" onclick=\"" + Me + ".spDelGo('" + Ui.Js(key) + "')\">Delete species</button></div>");
            }
            response.SetElementContents("st-mbody", sb.ToString());
            response.ExecuteScript(Me + ".shown('');");
            return response;
        }

        public async Task<ApiResponse> SpDelete()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "delete species", "not allowed", false);
                Toast(response, "You don't have the right to change settings.");
                return response;
            }
            string key = Ui.Clip(GetDataValue("spkey"), 20);
            SetSpecies? row = (await SpeciesRows()).FirstOrDefault(x => x.SpeciesKey == key);
            if (row == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                response.SetElementContents("st-p-species", await SpeciesHtml(true));
                Toast(response, "This species does not exist any more.");
                return response;
            }
            List<string> uses = await SpeciesUses(key);
            if (uses.Count > 0)
            {
                response.SetElementContents("st-e-spdel", Ui.Enc("It is in use now: " + uses[0]));
                return response;
            }
            string inCsv = "(N',' + REPLACE(ISNULL(SPECIES, N''), N' ', N'') + N',') LIKE N'%,' + @key + N',%'";
            string result = await Helper.PutData(new List<string>
            {
                "UPDATE SPECIES SET SPECIES_NAME = SPECIES_NAME WHERE SPECIES_KEY = @key",
                "IF EXISTS (SELECT 1 FROM PET WHERE SPECIES_KEY = @key) OR EXISTS (SELECT 1 FROM STAFFSPECIES WHERE SPECIES_KEY = @key) OR EXISTS (SELECT 1 FROM TESTFIELD WHERE SPECIES_KEY = @key) OR EXISTS (SELECT 1 FROM BOOKTYPE WHERE " + inCsv + ") OR EXISTS (SELECT 1 FROM ROOMTYPE WHERE " + inCsv + ") OR EXISTS (SELECT 1 FROM ITEM WHERE " + inCsv + ") THROW 50004, N'SPECIESUSED', 1;",
                "DELETE FROM BREED WHERE SPECIES_KEY = @key",
                "DELETE FROM SETTING WHERE SKEY = N'stay.vax.' + @key",
                "DELETE FROM SPECIES WHERE SPECIES_KEY = @key"
            }, new List<SqlParameter> { App.P("key", key, 20) });
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "delete species", key + " failed", false);
                response.SetElementContents("st-e-spdel", result.Contains("SPECIESUSED") ? "Something started using this species just now, so it was not deleted." : "The species could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "delete species", key + " · " + row.SpeciesName, true);
            Site.Forget();
            response.ExecuteScript(Me + ".closeModal();");
            response.SetElementContents("st-p-species", await SpeciesHtml(true));
            Toast(response, row.SpeciesName + " was deleted.");
            return response;
        }

        public async Task<ApiResponse> SpSave()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V') || !c.Can(Key, 'E'))
            {
                Toast(response, "You don't have the right to change settings.");
                return response;
            }
            foreach (string e in new[] { "spk", "spn", "spp", "spo", "sp" })
            {
                response.SetElementContents("st-e-" + e, string.Empty);
            }
            string key = Ui.Clip(GetDataValue("spkey"), 20);
            bool isNew = key == string.Empty;
            List<SetSpecies> rows = await SpeciesRows();
            SetSpecies? cur = isNew ? null : rows.FirstOrDefault(x => x.SpeciesKey == key);
            if (!isNew && cur == null)
            {
                response.ExecuteScript(Me + ".closeModal();");
                response.SetElementContents("st-p-species", await SpeciesHtml(true));
                Toast(response, "This species does not exist any more.");
                return response;
            }
            bool bad = false;
            if (isNew)
            {
                key = (GetDataValue("spnew") ?? string.Empty).Trim();
                if (!Regex.IsMatch(key, "^[a-z]{2,20}$"))
                {
                    response.SetElementContents("st-e-spk", "Use 2 to 20 lower-case letters a to z, no spaces.");
                    bad = true;
                }
                else if (rows.Any(x => x.SpeciesKey == key))
                {
                    response.SetElementContents("st-e-spk", "This key is already used.");
                    bad = true;
                }
            }
            string name = Ui.OneLine(GetDataValue("spname"), 60);
            if (name == string.Empty || name.Length > 40)
            {
                response.SetElementContents("st-e-spn", name == string.Empty ? "Write the name." : "Keep it under 40 characters.");
                bad = true;
            }
            else if (rows.Any(x => x.SpeciesKey != key && x.SpeciesName.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                response.SetElementContents("st-e-spn", "Another species already has this name.");
                bad = true;
            }
            string plural = Ui.OneLine(GetDataValue("spplural"), 60);
            if (plural.Length > 40)
            {
                response.SetElementContents("st-e-spp", "Keep it under 40 characters.");
                bad = true;
            }
            if (plural == string.Empty && name != string.Empty)
            {
                plural = Ui.Clip(name + "s", 40);
            }
            string ot = Ui.Clip(GetDataValue("sporder"), 6);
            int order = 0;
            bool auto = ot == string.Empty;
            if (!auto && (!int.TryParse(ot, NumberStyles.Integer, Ui.Inv, out order) || order < 0 || order > 999))
            {
                response.SetElementContents("st-e-spo", "Write a whole number from 0 to 999, or leave it empty.");
                bad = true;
            }
            if (auto)
            {
                order = cur?.SpeciesOrder ?? (rows.Count == 0 ? 1 : rows.Max(x => x.SpeciesOrder) + 1);
            }
            bool sizes = Ui.Clip(GetDataValue("spsize"), 2) == "1";
            bool active = Ui.Clip(GetDataValue("spact"), 2) == "1";
            if (bad)
            {
                response.SetElementContents("st-e-sp", "Check the fields marked in red.");
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("key", key, 20), App.P("name", name, 40), App.P("plural", plural, 40), App.PBit("size", sizes), App.PInt("ord", order), App.PBit("act", active) };
            string sql = isNew
                ? "IF EXISTS (SELECT 1 FROM SPECIES WHERE SPECIES_KEY = @key) THROW 50003, N'DUPKEY', 1; INSERT INTO SPECIES (SPECIES_KEY, SPECIES_NAME, PLURAL_NAME, HAS_SIZE, SPECIES_ORDER, ACTIVE) VALUES (@key, @name, @plural, @size, @ord, @act)"
                : "UPDATE SPECIES SET SPECIES_NAME = @name, PLURAL_NAME = @plural, HAS_SIZE = @size, SPECIES_ORDER = @ord, ACTIVE = @act WHERE SPECIES_KEY = @key; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";
            string result = await Helper.PutData(new List<string> { sql }, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, isNew ? "add species" : "edit species", key + " failed", false);
                response.SetElementContents(result.Contains("DUPKEY") ? "st-e-spk" : "st-e-sp", result.Contains("DUPKEY") ? "This key is already used." : "The species could not be saved. Try again.");
                return response;
            }
            if (isNew)
            {
                await App.Log(c.U.StaffId, "add species", key + " · " + name, true);
            }
            else
            {
                List<string> ch = new List<string>();
                if (cur!.SpeciesName != name)
                {
                    ch.Add("name " + cur.SpeciesName + " → " + name);
                }
                if (cur.PluralName != plural)
                {
                    ch.Add("plural " + cur.PluralName + " → " + plural);
                }
                if (cur.HasSize != sizes)
                {
                    ch.Add("sizes " + (cur.HasSize ? "yes" : "no") + " → " + (sizes ? "yes" : "no"));
                }
                if (cur.SpeciesOrder != order)
                {
                    ch.Add("order " + cur.SpeciesOrder.ToString(Ui.Inv) + " → " + order.ToString(Ui.Inv));
                }
                if (cur.Active != active)
                {
                    ch.Add(active ? "active" : "inactive");
                }
                await App.Log(c.U.StaffId, "edit species", key + " · " + (ch.Count == 0 ? "no changes" : string.Join(", ", ch)), true);
            }
            response.ExecuteScript(Me + ".closeModal();");
            response.SetElementContents("st-p-species", await SpeciesHtml(true));
            Toast(response, isNew ? name + " added." : name + " saved.");
            return response;
        }
    }
}
