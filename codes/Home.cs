using System.Text;
using System.Text.Json;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Home : WebPage
    {
        private const string P = "hm";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string main = await Render(site);
            Head(site, site.Settings.Name + (site.Settings.Town != string.Empty ? " | " + site.Settings.Town : string.Empty), site.Settings.Tagline, HeroImage(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "HomeJs", "Home", site).Replace("{plhd_main}", main);
        }

        public async Task<ApiResponse> Next()
        {
            ApiResponse response = new ApiResponse();
            SiteInfo site = await Site.Load();
            string sp = Species(site, Ui.Clip(GetDataValue("spc"), 20));
            string key = Ui.Clip(GetDataValue("svc"), 80);
            CareService? sv = site.Services.FirstOrDefault(x => x.Key == key && x.Active && x.Online && Site.ForSpecies(x, sp));
            response.SetElementContents("hm-fnext", await NextLine(site, sv, sp));
            return response;
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

        private static string HeroImage(SiteInfo site)
        {
            Block? b = site.BlocksOf("home").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            return b != null && Blocks.SafeImage(b.Media) ? b.Media : string.Empty;
        }

        private static string Species(SiteInfo site, string v)
        {
            return site.Settings.Species.Contains(v) ? v : string.Empty;
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

        private static async Task<string> NextLine(SiteInfo site, CareService? sv, string sp)
        {
            if (sv == null)
            {
                return "<span>Choose a service to see the next free time.</span>";
            }
            (DateTime Start, Clinician P)? n = await Site.Next(site, sv, null, 28, sp);
            if (n == null)
            {
                return "<span>No online times in the next 4 weeks.</span> <a href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">Call " + Ui.Enc(site.Settings.Phone) + "</a>";
            }
            return "<span>Next free time</span> <b>" + NextText(site, n.Value.Start) + "</b> <span>with " + Ui.Enc(n.Value.P.Name) + "</span>";
        }

        private static async Task<string> Finder(SiteInfo site)
        {
            List<Specialty> teams = site.Specialties.Where(x => x.Active).OrderBy(x => x.Order).ToList();
            List<CareService> list = site.Services.Where(x => x.Active && x.Online && teams.Any(t => t.Key == x.Specialty)).OrderBy(x => teams.FindIndex(t => t.Key == x.Specialty)).ThenBy(x => x.Order).ToList();
            var data = list.Select(x => new { k = x.Key, n = x.Name, t = teams.First(t => t.Key == x.Specialty).Name, s = x.Species });
            StringBuilder opts = new StringBuilder();
            foreach (Specialty t in teams)
            {
                List<CareService> mine = list.Where(x => x.Specialty == t.Key).ToList();
                if (mine.Count == 0)
                {
                    continue;
                }
                opts.Append("<optgroup label=\"" + Ui.Attr(t.Name) + "\">");
                foreach (CareService x in mine)
                {
                    opts.Append("<option value=\"" + Ui.Attr(x.Key) + "\">" + Ui.Enc(x.Name) + "</option>");
                }
                opts.Append("</optgroup>");
            }
            string sp = Ui.Opts(new[] { (string.Empty, "Any animal") }.Concat(site.Settings.Species.Select(s => (s, site.SpeciesName(s)))), string.Empty);
            return "<form class=\"hm-find\" action=\"Book\" method=\"get\" onsubmit=\"return HomeJs.find()\" aria-label=\"Find a time\"><h2>Find a time</h2><div class=\"hm-find-in\"><label class=\"hm-field\"><span>Your pet</span><select id=\"hm-fsp\" name=\"sp\" onchange=\"HomeJs.species()\">" + sp + "</select></label><label class=\"hm-field\"><span>What do you need?</span><select id=\"hm-fsv\" name=\"s\" onchange=\"HomeJs.pick()\" data-list=\"" + Ui.Attr(JsonSerializer.Serialize(data)) + "\">" + opts + "</select></label><button type=\"submit\" class=\"hm-btn hm-btn-accent\">" + Blocks.Icon("cal") + "Find a time</button></div><p class=\"hm-fnext\" id=\"hm-fnext\" aria-live=\"polite\">" + await NextLine(site, list.FirstOrDefault(), string.Empty) + "</p></form>";
        }

        private static async Task<DateTime?> TeamNext(SiteInfo site, Specialty t)
        {
            DateTime? best = null;
            foreach (CareService sv in site.Services.Where(x => x.Active && x.Online && x.Specialty == t.Key))
            {
                (DateTime Start, Clinician P)? n = await Site.Next(site, sv, null, 28);
                if (n != null && (best == null || n.Value.Start < best.Value))
                {
                    best = n.Value.Start;
                }
            }
            return best;
        }

        private static async Task<string> DataPart(SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder();
            switch (b.Source)
            {
                case "teams":
                    List<Specialty> teams = site.Specialties.Where(x => x.Active && site.Services.Any(v => v.Active && v.Specialty == x.Key)).OrderBy(x => x.Order).ToList();
                    if (teams.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append(Blocks.HeadMore(P, site, b, "Services", "All services") + "<div class=\"hm-teams\">");
                    foreach (Specialty t in teams)
                    {
                        bool online = site.Services.Any(x => x.Active && x.Online && x.Specialty == t.Key);
                        string img = Blocks.Img(site, t.Image, t.Name, string.Empty, true, false);
                        string color = Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B";
                        string next = online ? NextText(site, await TeamNext(site, t)) : Ui.Enc(site.Settings.Phone);
                        sb.Append("<a class=\"hm-team\" href=\"Services?s=" + Uri.EscapeDataString(t.Key) + "\" style=\"--c:" + color + "\"><span class=\"hm-team-img\">" + (img != string.Empty ? img : Blocks.Icon("paw")) + "</span><span class=\"hm-team-b\"><b>" + Ui.Enc(t.Name) + "</b><small>" + Ui.Enc(t.Blurb) + "</small><span class=\"hm-team-n\"><em>" + (online ? "Next online time" : "Book by phone") + "</em><strong>" + next + "</strong></span></span></a>");
                    }
                    sb.Append("</div>");
                    return Blocks.Sec(P, b, sb.ToString());
                case "vets":
                    List<Clinician> vets = site.Providers.Where(x => x.Active && x.Role == "vet").OrderBy(x => x.Order).Take(4).ToList();
                    if (vets.Count == 0)
                    {
                        return string.Empty;
                    }
                    sb.Append(Blocks.HeadMore(P, site, b, "Vets", "Meet the whole team") + "<div class=\"hm-vets\">");
                    foreach (Clinician p in vets)
                    {
                        sb.Append("<a class=\"hm-vet\" href=\"Vet?v=" + Uri.EscapeDataString(p.Id) + "\">" + Blocks.Face(P, site, p) + "<span class=\"hm-vet-b\"><b>" + Ui.Enc(p.Name) + "</b><small>" + Ui.Enc(p.Title) + "</small><span class=\"hm-meta\">" + Blocks.Chips(P, site, p.Species) + "</span></span></a>");
                    }
                    sb.Append("</div>");
                    return Blocks.Sec(P, b, sb.ToString());
                case "boardband":
                    List<RunType> runs = site.Runs.Where(x => x.Active).OrderBy(x => x.Order).ToList();
                    sb.Append("<div class=\"hm-band\"><div class=\"hm-band-t\"><span class=\"hm-band-ico\">" + Blocks.Icon("bed") + "</span>" + Blocks.Kick(P, site, b) + Blocks.H2(P, site, b) + Blocks.Rich(P, site, b.Text, "hm-lead") + "<div class=\"hm-acts\"><a class=\"hm-btn hm-btn-accent\" href=\"Boarding\">" + Ui.Enc(b.Label != string.Empty ? Site.Tok(site, b.Label) : "See boarding") + "</a></div></div>");
                    if (runs.Count > 0)
                    {
                        sb.Append("<ul class=\"hm-runs\">");
                        foreach (RunType r in runs)
                        {
                            sb.Append("<li><span class=\"hm-run-i\">" + Blocks.Icon(r.Species == "cat" ? "cat" : "dog") + "</span><span class=\"hm-run-n\"><b>" + Ui.Enc(r.Name) + "</b><small>" + Ui.Enc(r.Short) + "</small></span>" + (site.Settings.ShowPrices && r.Price > 0 ? "<span class=\"hm-run-p\"><b>" + Site.Price(r.Price) + "</b><small>a night</small></span>" : string.Empty) + "</li>");
                        }
                        sb.Append("</ul>");
                    }
                    sb.Append("</div>");
                    return Blocks.Sec(P, b, sb.ToString());
                case "hours":
                    sb.Append(Blocks.Head(P, site, b, false) + "<div class=\"hm-visit\"><div class=\"hm-box\"><h3>" + Blocks.Icon("clock") + "Opening hours</h3><div class=\"hm-hours\">" + Site.HoursRows(P, site, "hm-hrow") + "</div>" + Closed(site) + "</div>");
                    sb.Append("<div class=\"hm-box\"><h3>" + Blocks.Icon("pin") + "Find us</h3><p class=\"hm-addr\">" + Ui.Enc(site.Settings.Address) + "</p><p><a class=\"hm-more\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a></p><p><a class=\"hm-more\" href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a></p><a class=\"hm-btn hm-btn-ghost hm-btn-sm\" href=\"Contact\">Directions and parking</a></div>");
                    sb.Append("<div class=\"hm-box\"><h3>" + Blocks.Icon("shield") + "Pet insurance</h3><p class=\"hm-hint\">We work with these plans and you can always pay at the visit.</p><div class=\"hm-ins\">");
                    foreach (string ins in site.Settings.Insurance)
                    {
                        sb.Append("<span>" + Ui.Enc(ins) + "</span>");
                    }
                    sb.Append("</div></div></div>");
                    return Blocks.Sec(P, b, sb.ToString());
            }
            return string.Empty;
        }

        private static string Closed(SiteInfo site)
        {
            DateTime today = Site.Today(site);
            for (int i = 0; i <= 30; i++)
            {
                DateTime d = today.AddDays(i);
                Holiday? h = Site.HolidayOn(site, d);
                if (h != null)
                {
                    return "<p class=\"hm-info-box\">Closed " + (i == 0 ? "today" : i == 1 ? "tomorrow" : Ui.Enc(Ui.LongDay(d))) + " &middot; " + Ui.Enc(h.Name) + "</p>";
                }
            }
            for (int i = 0; i <= 7; i++)
            {
                DateTime d = today.AddDays(i);
                if (Site.Clinic(site, d) == null)
                {
                    return "<p class=\"hm-info-box\">Next closed day: " + (i == 0 ? "today" : i == 1 ? "tomorrow" : Ui.Enc(d.ToString("dddd, MMMM d", Ui.Us))) + "</p>";
                }
            }
            return string.Empty;
        }

        private static async Task<string> Render(SiteInfo site)
        {
            await Task.WhenAll(site.Services.Where(x => x.Active && x.Online).Select(x => Site.Next(site, x, null, 28)));
            Dictionary<string, string> parts = new Dictionary<string, string>();
            string finder = string.Empty;
            foreach (Block b in site.BlocksOf("home").Where(x => !x.Hidden))
            {
                if (b.Type == "data" && !parts.ContainsKey(b.Id))
                {
                    parts[b.Id] = await DataPart(site, b);
                }
                if (b.Type == "hero" && b.Extra == "finder" && finder == string.Empty)
                {
                    finder = await Finder(site);
                }
            }
            return Blocks.RenderPage(P, site, "home", site.Settings.Name, b => parts.TryGetValue(b.Id, out string? h) ? h : string.Empty, b => b.Extra == "finder" ? finder : string.Empty);
        }
    }
}
