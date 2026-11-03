using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Vet : WebPage
    {
        private const string P = "pr";

        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
            public string Image { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string id = Ui.Clip(QueryValue("v"), 80);
            View v = await Render(site, id != string.Empty ? id : Ui.Clip(QueryValue("id"), 80));
            Head(site, v.Title + " | " + site.Settings.Name, v.Desc, v.Image);
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "VetJs", "Vet", site).Replace("{plhd_main}", v.Main);
        }

        private void Head(SiteInfo site, string title, string desc, string image)
        {
            HtmlDoc.SetTitle(Ui.Enc(title));
            bool hasDesc = false;
            foreach (HtmlElement e in HtmlDoc.HtmlElements ?? new List<HtmlElement>())
            {
                HtmlTag? t = e.Element;
                if (t == null)
                {
                    continue;
                }
                if (t.Tag == "html")
                {
                    t.SetAttribute("lang", "en");
                }
                else if (t.Tag == "meta" && t.GetAttribute("name") == "description")
                {
                    t.SetAttribute("content", Ui.Attr(desc));
                    hasDesc = true;
                }
            }
            if (!hasDesc)
            {
                HtmlDoc.AddMetaElement("description", Ui.Attr(desc));
            }
            HttpContext? h = HttpCurrent.Context;
            string root = h != null ? h.Request.Scheme + "://" + h.Request.Host + h.Request.PathBase + "/" : string.Empty;
            List<(string, string)> og = new List<(string, string)> { ("og:type", "profile"), ("og:site_name", site.Settings.Name), ("og:title", title), ("og:description", desc), ("og:locale", "en_US") };
            if (image != string.Empty)
            {
                og.Add(("og:image", root + image));
            }
            foreach ((string k, string v) in og)
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Attr(v) } });
            }
            HtmlDoc.AddMetaElement("twitter:card", "summary");
        }

        private static string NextText(SiteInfo site, DateTime? n)
        {
            if (n == null)
            {
                return "No online times in the next 2 weeks";
            }
            DateTime today = Site.Today(site);
            DateTime d = n.Value.Date;
            string day = d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : Ui.Day(d, false);
            return day + " at " + Site.Hm(site, n.Value);
        }

        private static string RoleName(SiteInfo site, string role)
        {
            return role switch { "tech" => "Technician", "groomer" => "Groomer", _ => site.Settings.ProviderWord != string.Empty ? site.Settings.ProviderWord : "Vet" };
        }

        private static string SpecTag(SiteInfo site, string key)
        {
            Specialty? sp = site.Team(key);
            return sp == null ? string.Empty : "<span class=\"pr-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#3F7D6B") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
        }

        private static string Length(SiteInfo site, CareService sv)
        {
            if (sv.Kind == "dropoff")
            {
                return "Drop off " + Site.HmText(site, sv.DropFrom) + " to " + Site.HmText(site, sv.DropTo) + ", home from " + Site.HmText(site, sv.PickUp);
            }
            if (sv.Kind == "groom")
            {
                int lo = Site.MinutesFor(sv, "small");
                int hi = Math.Max(Site.MinutesFor(sv, "medium"), Site.MinutesFor(sv, "large"));
                return hi > lo ? Site.Dur(lo) + " to " + Site.Dur(hi) + " by size" : Site.Dur(lo);
            }
            return Site.Dur(sv.Minutes);
        }

        private static string PriceText(SiteInfo site, CareService sv)
        {
            if (!site.Settings.ShowPrices || sv.Price <= 0)
            {
                return string.Empty;
            }
            if (sv.Kind == "groom")
            {
                decimal lo = Site.PriceFor(sv, "small");
                decimal hi = Math.Max(Site.PriceFor(sv, "medium"), Site.PriceFor(sv, "large"));
                return hi > lo ? Site.Price(lo) + " to " + Site.Price(hi) : Site.Price(lo);
            }
            return Site.Price(sv.Price);
        }

        private static string ShortName(Clinician p)
        {
            string[] parts = p.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return p.Name;
            }
            return p.Name.StartsWith("Dr", StringComparison.Ordinal) ? "Dr. " + parts[^1] : parts[0];
        }

        private static string NotFound(SiteInfo site)
        {
            string word = site.Settings.ProvidersWord != string.Empty ? site.Settings.ProvidersWord : "Vets";
            return "<section class=\"pr-nf\"><div class=\"pr-wrap\"><div class=\"pr-nfbox\"><span class=\"pr-nfico\"><svg viewBox=\"0 0 24 24\" width=\"30\" height=\"30\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"6.5\" cy=\"10\" r=\"1.9\"/><circle cx=\"10\" cy=\"5.8\" r=\"1.9\"/><circle cx=\"14.6\" cy=\"5.8\" r=\"1.9\"/><circle cx=\"18\" cy=\"10\" r=\"1.9\"/><path d=\"M12.3 11.6c-2.9 0-5.4 3.4-5.4 5.8 0 1.6 1.2 2.6 2.8 2.6 1.1 0 1.7-.6 2.6-.6s1.5.6 2.6.6c1.6 0 2.8-1 2.8-2.6 0-2.4-2.5-5.8-5.4-5.8z\"/></svg></span><h1>We couldn't find that person</h1><p>The link may be old, or they may have moved on. Everyone on our team today is on the " + Ui.Enc(word.ToLowerInvariant()) + " page.</p><div class=\"pr-acts\"><a class=\"pr-btn pr-btn-accent\" href=\"Vets\">Meet the team</a><a class=\"pr-btn pr-btn-ghost\" href=\"Book\">Book a visit</a></div></div></div></section>";
        }

        private static async Task<View> Render(SiteInfo site, string id)
        {
            Clinician? p = id == string.Empty ? null : site.Providers.FirstOrDefault(x => x.Id == id && x.Active);
            if (p == null)
            {
                return new View { Title = "Not found", Desc = "Meet the team at " + site.Settings.Name + ".", Main = NotFound(site) };
            }
            string word = site.Settings.ProvidersWord != string.Empty ? site.Settings.ProvidersWord : "Vets";
            DateTime today = Site.Today(site);
            List<CareService> svs = site.Services.Where(s => s.Active && s.Providers.Contains(p.Id)).OrderBy(s => site.Specialties.FindIndex(t => t.Key == s.Specialty)).ThenBy(s => s.Order).ToList();
            bool photo = Blocks.SafeImage(p.Photo);
            StringBuilder sb = new StringBuilder("<div class=\"pr-wrap\"><nav class=\"pr-crumb\" aria-label=\"Breadcrumb\"><a href=\"Vets\">" + Ui.Enc(word) + "</a><span aria-hidden=\"true\">/</span><span>" + Ui.Enc(p.Name) + "</span></nav>");
            sb.Append("<section class=\"pr-prof\"><div class=\"pr-prof-img\"><span class=\"pr-noface\" style=\"--h:" + Ui.Hue(p.Id) + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(p.Name)) + "</span>" + (photo ? "<img class=\"pr-face\" src=\"" + Ui.Attr(p.Photo) + "\" alt=\"Portrait of " + Ui.Attr(p.Name) + "\" width=\"480\" height=\"600\">" : string.Empty) + "</div><div class=\"pr-prof-t\"><div class=\"pr-tags\"><span class=\"pr-role pr-role-" + Ui.Attr(p.Role) + "\">" + Ui.Enc(RoleName(site, p.Role)) + "</span>" + SpecTag(site, p.Specialty) + "</div><h1>" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? "<small>, " + Ui.Enc(p.Credentials) + "</small>" : string.Empty) + "</h1><p class=\"pr-ptitle\">" + Ui.Enc(p.Title) + "</p>" + (p.Bio != string.Empty ? "<p class=\"pr-bio\">" + Ui.Enc(p.Bio) + "</p>" : string.Empty));
            sb.Append("<dl class=\"pr-pfacts\"><div><dt>Sees</dt><dd class=\"pr-sees\">" + (p.Species.Count == 0 ? "<span class=\"pr-sp\">All animals</span>" : string.Concat(p.Species.Select(s => "<span class=\"pr-sp\">" + Ui.Enc(site.SpeciesPlural(s)) + "</span>"))) + "</dd></div>");
            if (p.Languages.Count > 0)
            {
                sb.Append("<div><dt>Languages</dt><dd>" + Ui.Enc(string.Join(", ", p.Languages)) + "</dd></div>");
            }
            if (p.Education.Count > 0)
            {
                sb.Append("<div><dt>Training</dt><dd>" + string.Join("<br>", p.Education.Select(Ui.Enc)) + "</dd></div>");
            }
            sb.Append("</dl>");
            bool online = p.Accepting && svs.Any(s => Site.Online(p, s));
            sb.Append(online ? "<div class=\"pr-acts\"><a class=\"pr-btn pr-btn-accent\" href=\"Book?v=" + Uri.EscapeDataString(p.Id) + (svs.Count == 1 ? "&amp;s=" + Uri.EscapeDataString(svs[0].Key) : string.Empty) + "\">Book with " + Ui.Enc(ShortName(p)) + "</a><a class=\"pr-btn pr-btn-ghost\" href=\"#pr-svs\">See services and times</a></div>" : "<p class=\"pr-warn-box\">" + Ui.Enc(ShortName(p)) + " is not taking online bookings right now. Please call <a href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a>.</p>");
            sb.Append("</div></section><div class=\"pr-pgrid\"><section class=\"pr-box\" id=\"pr-svs\"><h2>Services and next free times</h2>");
            if (svs.Count == 0)
            {
                sb.Append("<p class=\"pr-hint\">No services are listed yet. Please call us to book.</p>");
            }
            else
            {
                sb.Append("<div class=\"pr-svlist\">");
                foreach (CareService sv in svs)
                {
                    bool web = Site.Online(p, sv);
                    (DateTime Start, Clinician P)? n = web ? await Site.Next(site, sv, p, 14) : null;
                    string price = PriceText(site, sv);
                    string href = "Book?s=" + Uri.EscapeDataString(sv.Key) + "&amp;v=" + Uri.EscapeDataString(p.Id);
                    string act = !web ? "<a class=\"pr-slot pr-slot-off\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\"><small>Book by phone</small><b>" + Ui.Enc(site.Settings.Phone) + "</b></a>" : n == null ? "<a class=\"pr-slot pr-slot-off\" href=\"" + href + "\"><small>No free time in 2 weeks</small><b>See later dates</b></a>" : "<a class=\"pr-slot\" href=\"" + href + "&amp;at=" + n.Value.Start.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv) + "\"><small>" + (sv.Kind == "dropoff" ? "Next drop-off" : "Next free time") + "</small><b>" + NextText(site, n.Value.Start) + "</b></a>";
                    sb.Append("<div class=\"pr-svrow\"><div class=\"pr-svt\"><a href=\"Service?s=" + Uri.EscapeDataString(sv.Key) + "\"><b>" + Ui.Enc(sv.Name) + "</b></a><small>" + Length(site, sv) + (price != string.Empty ? " &middot; " + price : string.Empty) + "</small></div>" + act + "</div>");
                }
                sb.Append("</div><p class=\"pr-hint\">Times update live. Pick one to choose your pet and confirm.</p>");
            }
            sb.Append("</section><div class=\"pr-side\"><section class=\"pr-box\"><h2>Weekly hours</h2><table class=\"pr-week\"><caption class=\"pr-sr\">Weekly hours for " + Ui.Enc(p.Name) + "</caption><tbody>");
            int dow = (int)today.DayOfWeek;
            foreach (int d in new[] { 1, 2, 3, 4, 5, 6, 0 })
            {
                WorkDay? w = p.Hours.FirstOrDefault(h => h.Day == d);
                OpenDay? o = site.Settings.Hours.FirstOrDefault(h => h.Day == d);
                bool on = w != null && w.On && o != null && o.Open;
                string brk = on && w!.BreakFrom != string.Empty && w.BreakTo != string.Empty ? "<small>Break " + Site.HmText(site, w.BreakFrom) + " &ndash; " + Site.HmText(site, w.BreakTo) + "</small>" : string.Empty;
                sb.Append("<tr class=\"" + (d == dow ? "pr-now" : string.Empty) + (on ? string.Empty : " pr-off") + "\"><th scope=\"row\">" + Ui.DayNames[d] + (d == dow ? " <em>Today</em>" : string.Empty) + "</th><td>" + (on ? Site.HmText(site, w!.From) + " &ndash; " + Site.HmText(site, w.To) + brk : "Not in") + "</td></tr>");
            }
            sb.Append("</tbody></table></section><section class=\"pr-box\"><h2>Time off</h2>");
            List<TimeOff> away = p.TimeOff.Where(t => t.To.Date >= today).OrderBy(t => t.From).Take(6).ToList();
            if (away.Count == 0)
            {
                sb.Append("<p class=\"pr-hint\">No time off planned.</p>");
            }
            else
            {
                sb.Append("<ul class=\"pr-away\">");
                foreach (TimeOff t in away)
                {
                    string when = t.From.Date == t.To.Date ? Ui.Day(t.From, false) : Ui.Day(t.From, false) + " &ndash; " + Ui.Day(t.To, false);
                    sb.Append("<li><b>" + when + "</b></li>");
                }
                sb.Append("</ul><p class=\"pr-hint\">While away, book another " + Ui.Enc((site.Settings.ProviderWord != string.Empty ? site.Settings.ProviderWord : "vet").ToLowerInvariant()) + " in the same team.</p>");
            }
            sb.Append("</section></div></div></div>");
            return new View { Title = p.Name, Desc = p.Title + " at " + site.Settings.Name + ". Sees " + (p.Species.Count == 0 ? "all animals" : string.Join(", ", p.Species.Select(site.SpeciesPlural)).ToLowerInvariant()) + ".", Main = sb.ToString(), Image = photo ? p.Photo : string.Empty };
        }
    }
}
