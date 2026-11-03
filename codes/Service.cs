using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Service : WebPage
    {
        private const string P = "se";

        private static readonly (string Key, string Name)[] Sizes = { ("small", "Small (under 20 lb)"), ("medium", "Medium (20 to 50 lb)"), ("large", "Large (over 50 lb)") };

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
            string key = Ui.Clip(QueryValue("s"), 80);
            View v = await Render(site, key != string.Empty ? key : Ui.Clip(QueryValue("k"), 80));
            Head(site, v.Title + " | " + site.Settings.Name, v.Desc, v.Image);
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "ServiceJs", "Service", site).Replace("{plhd_main}", v.Main);
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
            List<(string, string)> og = new List<(string, string)> { ("og:type", "website"), ("og:site_name", site.Settings.Name), ("og:title", title), ("og:description", desc), ("og:locale", "en_US") };
            if (image != string.Empty)
            {
                og.Add(("og:image", root + image));
            }
            foreach ((string k, string v) in og)
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Attr(v) } });
            }
            HtmlDoc.AddMetaElement("twitter:card", image != string.Empty ? "summary_large_image" : "summary");
        }

        private static string NextText(SiteInfo site, DateTime? n)
        {
            if (n == null)
            {
                return "No online times in the next 4 weeks";
            }
            DateTime today = Site.Today(site);
            DateTime d = n.Value.Date;
            string day = d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : Ui.Day(d, false);
            return day + " at " + Site.Hm(site, n.Value);
        }

        private static bool Sized(CareService sv)
        {
            return sv.Kind == "groom" && (sv.MinutesM > 0 || sv.MinutesL > 0 || sv.PriceM > 0 || sv.PriceL > 0);
        }

        private static string KindName(CareService sv)
        {
            return sv.Kind switch { "groom" => Sized(sv) ? "Grooming, priced by size" : "Grooming", "dropoff" => "Drop-off procedure", _ => "Visit" };
        }

        private static string Fact(string label, string value)
        {
            return value == string.Empty ? string.Empty : "<div><dt>" + label + "</dt><dd>" + value + "</dd></div>";
        }

        private static string Facts(SiteInfo site, CareService sv)
        {
            bool prices = site.Settings.ShowPrices && sv.Price > 0;
            StringBuilder sb = new StringBuilder("<dl class=\"se-facts\">" + Fact("Kind", KindName(sv)));
            if (Sized(sv))
            {
                sb.Append("</dl><table class=\"se-sizes\"><caption>Length" + (prices ? " and price" : string.Empty) + " by dog size</caption><thead><tr><th scope=\"col\">Size</th><th scope=\"col\">Length</th>" + (prices ? "<th scope=\"col\">Price</th>" : string.Empty) + "</tr></thead><tbody>");
                foreach ((string k, string n) in Sizes)
                {
                    sb.Append("<tr><th scope=\"row\">" + Ui.Enc(n) + "</th><td>" + Site.Dur(Site.MinutesFor(sv, k)) + "</td>" + (prices ? "<td>" + Site.Price(Site.PriceFor(sv, k)) + "</td>" : string.Empty) + "</tr>");
                }
                sb.Append("</tbody></table><dl class=\"se-facts\">");
            }
            else if (sv.Kind == "dropoff")
            {
                sb.Append(Fact("Drop off", Site.HmText(site, sv.DropFrom) + " &ndash; " + Site.HmText(site, sv.DropTo)));
                sb.Append(Fact("Pick up", "From " + Site.HmText(site, sv.PickUp) + ", same day"));
                sb.Append(Fact("Price", prices ? "From " + Site.Price(sv.Price) : string.Empty));
            }
            else
            {
                sb.Append(Fact("Length", Site.Dur(sv.Minutes)));
                sb.Append(Fact("Price", prices ? Site.Price(sv.Price) : string.Empty));
            }
            sb.Append(Fact("Animals", "<span class=\"se-meta\">" + Blocks.Chips(P, site, sv.Species) + "</span>"));
            sb.Append(Fact("New clients", sv.NewOk ? "Welcome" : "Existing clients only"));
            sb.Append("</dl>");
            return sb.ToString().Replace("<dl class=\"se-facts\"></dl>", string.Empty);
        }

        private static async Task<string> Times(SiteInfo site, CareService sv)
        {
            if (!sv.Online || Site.For(site, sv).Count == 0)
            {
                return "<h3>Book this service</h3><p class=\"se-info-box\">Online booking is off for this service. Please call <a href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> and the front desk will find a time.</p>";
            }
            StringBuilder sb = new StringBuilder("<h3>" + (sv.Kind == "dropoff" ? "Next drop-off times" : "Next free times") + "</h3>");
            DateTime today = Site.Today(site);
            DateTime last = Site.LastDay(site);
            int shown = 0;
            for (int i = 0; i < 28 && shown < 2; i++)
            {
                DateTime d = today.AddDays(i);
                if (d > last)
                {
                    break;
                }
                if (Site.Clinic(site, d) == null)
                {
                    continue;
                }
                List<(Opening O, Clinician P)> slots = await Site.Openings(site, sv, string.Empty, d, string.Empty, string.Empty);
                List<(Opening O, Clinician P)> pick = slots.GroupBy(x => x.O.Start).Select(g => g.First()).OrderBy(x => x.O.Start).ToList();
                if (pick.Count == 0)
                {
                    continue;
                }
                shown++;
                sb.Append("<p class=\"se-sday\">" + (d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : Ui.Enc(d.ToString("dddd, MMMM d", Ui.Us))) + "</p><div class=\"se-slots\">");
                foreach ((Opening o, Clinician p) in pick.Take(6))
                {
                    sb.Append("<a class=\"se-slot\" href=\"Book?s=" + Uri.EscapeDataString(sv.Key) + "&amp;v=" + Uri.EscapeDataString(p.Id) + "&amp;at=" + o.Start.ToString("yyyy-MM-dd'T'HH:mm", Ui.Inv) + "\" title=\"With " + Ui.Attr(p.Name) + "\"><b>" + Site.Hm(site, o.Start) + "</b><small>" + Ui.Enc(p.Name) + "</small></a>");
                }
                sb.Append("</div>");
            }
            if (shown == 0)
            {
                sb.Append("<p class=\"se-hint\">No online times in the next four weeks. Please call us on " + Ui.Enc(site.Settings.Phone) + ".</p>");
            }
            else if (Sized(sv))
            {
                sb.Append("<p class=\"se-hint\">Times shown for a small dog. Booking adjusts the length to your dog's size.</p>");
            }
            sb.Append("<a class=\"se-btn se-btn-accent se-btn-wide\" href=\"Book?s=" + Uri.EscapeDataString(sv.Key) + "\">See all times</a>");
            return sb.ToString();
        }

        private static string PreOpDoc(SiteInfo site)
        {
            MediaItem? m = site.Media.FirstOrDefault(x => x.Kind == "doc" && x.Title.Contains("before and after surgery", StringComparison.OrdinalIgnoreCase) && Blocks.SafeDoc(x.Full));
            if (m == null)
            {
                return string.Empty;
            }
            return "<a class=\"se-doc\" href=\"" + Ui.Attr(m.Full) + "\" target=\"_blank\" rel=\"noopener\">" + Blocks.Icon("doc") + "<span><b>" + Ui.Enc(m.Title) + "</b><small>PDF" + (m.Pages > 0 ? " &middot; " + Ui.Plural(m.Pages, "page", "pages") : string.Empty) + " &middot; opens in a new tab</small></span></a>";
        }

        private static async Task<string> Card(SiteInfo site, CareService sv)
        {
            string href = "Service?s=" + Uri.EscapeDataString(sv.Key);
            string img = Blocks.Img(site, sv.Image, sv.Name, string.Empty, true, false);
            bool online = sv.Online && Site.For(site, sv).Count > 0;
            string next = online ? NextText(site, (await Site.Next(site, sv, null, 28))?.Start) : "Book by phone";
            Specialty? t = site.Team(sv.Specialty);
            return "<a class=\"se-card\" href=\"" + href + "\" style=\"--c:" + (t != null && Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\"><span class=\"se-card-img\">" + (img != string.Empty ? img : Blocks.Icon("paw")) + "</span><span class=\"se-card-b\"><b>" + Ui.Enc(sv.Name) + "</b><small>" + Ui.Enc(sv.Short) + "</small><em>" + (online ? "Next: " : string.Empty) + next + "</em></span></a>";
        }

        private static string NotFound(SiteInfo site)
        {
            StringBuilder sb = new StringBuilder("<section class=\"se-none\"><div class=\"se-wrap\"><div class=\"se-none-in\"><span class=\"se-none-ico\">" + Blocks.Icon("paw") + "</span><span class=\"se-kick\">Service not found</span><h1>We could not find that service</h1><p class=\"se-lead\">It may have a new name or no longer be offered. Pick a team below, see every service, or call us on <a href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a>.</p><div class=\"se-none-teams\">");
            foreach (Specialty t in site.Specialties.Where(x => x.Active && site.Services.Any(v => v.Active && v.Specialty == x.Key)).OrderBy(x => x.Order))
            {
                sb.Append("<a class=\"se-chipl\" href=\"Services?s=" + Uri.EscapeDataString(t.Key) + "\"><i style=\"background:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\"></i>" + Ui.Enc(t.Name) + "</a>");
            }
            sb.Append("</div><div class=\"se-acts\"><a class=\"se-btn se-btn-accent\" href=\"Services\">See every service</a><a class=\"se-btn se-btn-ghost\" href=\"Book\">Book a visit</a></div></div></div></section>");
            return sb.ToString();
        }

        private static CareService? Find(SiteInfo site, string key)
        {
            if (key == string.Empty)
            {
                return null;
            }
            return site.Services.FirstOrDefault(x => x.Key == key && x.Active) ?? site.Services.FirstOrDefault(x => x.Key == "bt-" + key && x.Active);
        }

        private static async Task<View> Render(SiteInfo site, string key)
        {
            CareService? sv = Find(site, key);
            if (sv == null)
            {
                return new View { Title = "Service not found", Desc = "Every service at " + site.Settings.Name + ".", Main = NotFound(site) };
            }
            Specialty? sp = site.Team(sv.Specialty);
            string color = sp != null && Ui.SafeColor(sp.Color) ? sp.Color : "#3F7D6B";
            string img = Blocks.Img(site, sv.Image, sv.Name, string.Empty, false, true);
            bool online = sv.Online && Site.For(site, sv).Count > 0;
            StringBuilder sb = new StringBuilder("<section class=\"se-head" + (img == string.Empty ? " se-head-plain" : string.Empty) + "\" style=\"--c:" + color + "\"><div class=\"se-wrap se-head-in\"><div class=\"se-head-t\"><nav class=\"se-crumb\" aria-label=\"Breadcrumb\"><a href=\"Services\">Services</a><span aria-hidden=\"true\">/</span>" + (sp != null ? "<a href=\"Services?s=" + Uri.EscapeDataString(sp.Key) + "\">" + Ui.Enc(sp.Name) + "</a>" : string.Empty) + "</nav><span class=\"se-kick\">" + KindName(sv) + "</span><h1>" + Ui.Enc(sv.Name) + "</h1><p class=\"se-lead\">" + Ui.Enc(sv.Short) + "</p><div class=\"se-acts\">" + (online ? "<a class=\"se-btn se-btn-accent\" href=\"Book?s=" + Uri.EscapeDataString(sv.Key) + "\">Book online</a>" : string.Empty) + "<a class=\"se-btn se-btn-ghost\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">Call " + Ui.Enc(site.Settings.Phone) + "</a></div></div>" + (img != string.Empty ? "<div class=\"se-head-p\">" + img + "</div>" : string.Empty) + "</div></section>");
            sb.Append("<div class=\"se-wrap se-grid\"><div class=\"se-mainc\">");
            if (sv.Text != string.Empty)
            {
                sb.Append("<p class=\"se-text\">" + Blocks.Lines(sv.Text) + "</p>");
            }
            if (sv.Expect.Count > 0)
            {
                sb.Append("<h2 class=\"se-h3\">What to expect</h2><ul class=\"se-ticks\">");
                foreach (string x in sv.Expect)
                {
                    sb.Append("<li>" + Blocks.Icon("check") + "<span>" + Ui.Enc(x) + "</span></li>");
                }
                sb.Append("</ul>");
            }
            if (sv.Kind == "dropoff")
            {
                bool dental = sv.Specialty == "dental" || sv.Name.Contains("dental", StringComparison.OrdinalIgnoreCase);
                sb.Append("<h2 class=\"se-h3\">How the day works</h2><ol class=\"se-day\"><li><b>" + Site.HmText(site, sv.DropFrom) + " &ndash; " + Site.HmText(site, sv.DropTo) + "</b><span>Drop your pet off at the front desk. We check them in and answer your questions.</span></li><li><b>From " + Site.HmText(site, sv.ProcFrom) + "</b><span>The " + (dental ? "dental" : "surgery") + " team takes care of your pet. We call you when your pet is awake.</span></li><li><b>From " + Site.HmText(site, sv.PickUp) + "</b><span>Pick your pet up and go through the home care notes with a nurse.</span></li></ol>");
                if (sv.PreOp.Count > 0)
                {
                    sb.Append("<h2 class=\"se-h3\">Before the day</h2><ul class=\"se-ticks se-preop\">");
                    foreach (string x in sv.PreOp)
                    {
                        sb.Append("<li>" + Blocks.Icon("clock") + "<span>" + Ui.Enc(x) + "</span></li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append(PreOpDoc(site));
            }
            List<Clinician> ps = site.Providers.Where(p => p.Active && sv.Providers.Contains(p.Id)).OrderBy(p => p.Order).ToList();
            if (ps.Count > 0)
            {
                sb.Append("<h2 class=\"se-h3\">Who you can see</h2><div class=\"se-who\">");
                foreach (Clinician p in ps)
                {
                    sb.Append("<a class=\"se-whoi\" href=\"Vet?v=" + Uri.EscapeDataString(p.Id) + "\">" + Blocks.Face(P, site, p) + "<span><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + "</small></span></a>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div><aside class=\"se-side\" aria-label=\"Details and times\"><div class=\"se-box\">" + Facts(site, sv) + "</div><div class=\"se-box se-times\">" + await Times(site, sv) + "</div></aside></div>");
            List<CareService> more = site.Services.Where(x => x.Active && x.Specialty == sv.Specialty && x.Key != sv.Key).OrderBy(x => x.Order).Take(3).ToList();
            if (more.Count > 0)
            {
                sb.Append("<section class=\"se-sec\"><div class=\"se-wrap\"><div class=\"se-shead\"><h2 class=\"se-h2\">More in " + Ui.Enc(sp?.Name ?? "this team") + "</h2><a class=\"se-more\" href=\"Services?s=" + Uri.EscapeDataString(sv.Specialty) + "\">All " + Ui.Enc((sp?.Name ?? "team").ToLowerInvariant()) + " &rarr;</a></div><div class=\"se-cards\">");
                foreach (CareService m in more)
                {
                    sb.Append(await Card(site, m));
                }
                sb.Append("</div></div></section>");
            }
            return new View { Title = sv.Name, Desc = sv.Short != string.Empty ? sv.Short : sv.Name + " at " + site.Settings.Name + ".", Main = sb.ToString(), Image = Blocks.SafeImage(sv.Image) ? sv.Image : string.Empty };
        }
    }
}
