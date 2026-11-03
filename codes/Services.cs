using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Services : WebPage
    {
        private const string P = "sv";

        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            View v = await Render(site, Ui.Clip(QueryValue("s"), 40), Ui.Clip(QueryValue("sp"), 20));
            Head(site, v.Title + " | " + site.Settings.Name, v.Desc, Pic(site, Ui.Clip(QueryValue("s"), 40)));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "ServicesJs", "Services", site).Replace("{plhd_main}", v.Main);
        }

        public async Task<ApiResponse> Filter()
        {
            ApiResponse response = new ApiResponse();
            SiteInfo site = await Site.Load();
            string team = Team(site, Ui.Clip(GetDataValue("team"), 40));
            string sp = Species(site, Ui.Clip(GetDataValue("animal"), 20));
            response.SetElementContents("sv-list", await List(site, team, sp));
            response.SetElementContents("sv-tchips", TeamChips(site, team));
            response.ExecuteScript("ServicesJs.filtered('" + Ui.Js(team) + "','" + Ui.Js(sp) + "');");
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

        private static string Pic(SiteInfo site, string team)
        {
            Specialty? t = site.Specialties.FirstOrDefault(x => x.Key == team);
            if (t != null && Blocks.SafeImage(t.Image))
            {
                return t.Image;
            }
            Block? b = site.BlocksOf("services").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            return b != null && Blocks.SafeImage(b.Media) ? b.Media : string.Empty;
        }

        private static string Species(SiteInfo site, string v)
        {
            return site.Settings.Species.Contains(v) ? v : string.Empty;
        }

        private static List<Specialty> Teams(SiteInfo site)
        {
            return site.Specialties.Where(x => x.Active && site.Services.Any(v => v.Active && v.Specialty == x.Key)).OrderBy(x => x.Order).ToList();
        }

        private static string Team(SiteInfo site, string v)
        {
            return Teams(site).Any(x => x.Key == v) ? v : string.Empty;
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
            return sv.Kind switch { "groom" => Sized(sv) ? "Grooming by size" : "Grooming", "dropoff" => "Drop-off", _ => "Visit" };
        }

        private static string Length(SiteInfo site, CareService sv)
        {
            if (sv.Kind == "dropoff")
            {
                return "Drop off " + Site.HmText(site, sv.DropFrom) + "&ndash;" + Site.HmText(site, sv.DropTo) + ", home from " + Site.HmText(site, sv.PickUp);
            }
            if (Sized(sv))
            {
                return "S " + Site.Dur(Site.MinutesFor(sv, "small")) + " &middot; M " + Site.Dur(Site.MinutesFor(sv, "medium")) + " &middot; L " + Site.Dur(Site.MinutesFor(sv, "large"));
            }
            return Site.Dur(sv.Minutes);
        }

        private static string PriceText(SiteInfo site, CareService sv)
        {
            if (!site.Settings.ShowPrices || sv.Price <= 0)
            {
                return string.Empty;
            }
            if (Sized(sv))
            {
                return "S " + Site.Price(Site.PriceFor(sv, "small")) + " &middot; M " + Site.Price(Site.PriceFor(sv, "medium")) + " &middot; L " + Site.Price(Site.PriceFor(sv, "large"));
            }
            return (sv.Kind == "dropoff" ? "From " : string.Empty) + Site.Price(sv.Price);
        }

        private static string KindIcon(CareService sv, Specialty t)
        {
            return Blocks.Icon(sv.Kind == "groom" ? "scissors" : sv.Kind == "dropoff" ? (t.Key == "dental" ? "tooth" : "scalpel") : t.Key == "wellness" ? "syringe" : t.Key == "dental" ? "tooth" : "stethoscope");
        }

        private static async Task<string> Item(SiteInfo site, CareService sv, Specialty t, string sp)
        {
            string href = "Service?s=" + Uri.EscapeDataString(sv.Key);
            string img = Blocks.Img(site, sv.Image, sv.Name, string.Empty, true, false);
            string price = PriceText(site, sv);
            string book = "Book?s=" + Uri.EscapeDataString(sv.Key) + (sp != string.Empty ? "&amp;sp=" + Uri.EscapeDataString(sp) : string.Empty);
            bool online = sv.Online && Site.For(site, sv, sp).Count > 0;
            string next = online ? NextText(site, (await Site.Next(site, sv, null, 28, sp))?.Start) : "Book by phone";
            StringBuilder sb = new StringBuilder("<article class=\"sv-item\" style=\"--c:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\"><a class=\"sv-item-img\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\">" + (img != string.Empty ? img : KindIcon(sv, t)) + "</a><div class=\"sv-item-b\"><span class=\"sv-kind sv-kind-" + Ui.Attr(sv.Kind) + "\">" + KindName(sv) + "</span><h3><a href=\"" + href + "\">" + Ui.Enc(sv.Name) + "</a></h3><p>" + Ui.Enc(sv.Short) + "</p><span class=\"sv-meta\">" + Blocks.Chips(P, site, sv.Species) + "</span></div>");
            sb.Append("<dl class=\"sv-facts\"><div><dt>" + (sv.Kind == "dropoff" ? "Day" : "Length") + "</dt><dd>" + Length(site, sv) + "</dd></div>" + (price != string.Empty ? "<div><dt>Price</dt><dd>" + price + "</dd></div>" : string.Empty) + "</dl>");
            sb.Append("<div class=\"sv-item-f\"><span class=\"sv-next\"><em>" + (online ? (sv.Kind == "dropoff" ? "Next drop-off" : "Next online time") : "Online booking off") + "</em><b>" + next + "</b></span><span class=\"sv-item-a\"><a class=\"sv-btn sv-btn-ghost sv-btn-sm\" href=\"" + href + "\" aria-label=\"Details about " + Ui.Attr(sv.Name) + "\">Details</a>" + (online ? "<a class=\"sv-btn sv-btn-accent sv-btn-sm\" href=\"" + book + "\" aria-label=\"Book " + Ui.Attr(sv.Name) + "\">Book</a>" : "<a class=\"sv-btn sv-btn-accent sv-btn-sm\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">Call</a>") + "</span></div></article>");
            return sb.ToString();
        }

        private static async Task<string> List(SiteInfo site, string team, string sp)
        {
            await Task.WhenAll(site.Services.Where(x => x.Active && x.Online && (team == string.Empty || x.Specialty == team) && Site.ForSpecies(x, sp) && Site.For(site, x, sp).Count > 0).Select(x => Site.Next(site, x, null, 28, sp)));
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (Specialty t in Teams(site).Where(x => team == string.Empty || x.Key == team))
            {
                List<CareService> list = site.Services.Where(x => x.Active && x.Specialty == t.Key && Site.ForSpecies(x, sp)).OrderBy(x => x.Order).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                n += list.Count;
                sb.Append("<div class=\"sv-group\" style=\"--c:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\"><div class=\"sv-ghead\"><div><h3><i></i>" + Ui.Enc(t.Name) + "</h3><p>" + Ui.Enc(t.Blurb) + "</p></div><a class=\"sv-more\" href=\"Vets?s=" + Uri.EscapeDataString(t.Key) + "\">Who you can see &rarr;</a></div><div class=\"sv-items\">");
                foreach (CareService sv in list)
                {
                    sb.Append(await Item(site, sv, t, sp));
                }
                sb.Append("</div></div>");
            }
            if (n == 0)
            {
                return "<div class=\"sv-empty\">" + Blocks.Icon("paw") + "<b>No services match these filters.</b><p>Try another animal or team, or call us on " + Ui.Enc(site.Settings.Phone) + " and we will help.</p><button type=\"button\" class=\"sv-btn sv-btn-ghost sv-btn-sm\" onclick=\"ServicesJs.reset()\">Show every service</button></div>";
            }
            return "<p class=\"sv-count\">" + Ui.Plural(n, "service", "services") + (sp != string.Empty ? " for " + Ui.Enc(site.SpeciesPlural(sp).ToLowerInvariant()) : string.Empty) + "</p>" + sb;
        }

        private static string TeamChips(SiteInfo site, string team)
        {
            StringBuilder sb = new StringBuilder("<button type=\"button\" class=\"sv-chip" + (team == string.Empty ? " sv-act" : string.Empty) + "\" aria-pressed=\"" + (team == string.Empty ? "true" : "false") + "\" onclick=\"ServicesJs.team('')\">All teams</button>");
            foreach (Specialty t in Teams(site))
            {
                bool on = t.Key == team;
                sb.Append("<button type=\"button\" class=\"sv-chip" + (on ? " sv-act" : string.Empty) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"ServicesJs.team('" + Ui.Js(t.Key) + "')\"><i style=\"background:" + (Ui.SafeColor(t.Color) ? t.Color : "#3F7D6B") + "\"></i>" + Ui.Enc(t.Name) + "</button>");
            }
            return sb.ToString();
        }

        private static async Task<string> DataPart(SiteInfo site, Block b, string team, string sp)
        {
            if (b.Source != "servicelist")
            {
                return string.Empty;
            }
            string animals = Ui.Opts(new[] { (string.Empty, "All animals") }.Concat(site.Settings.Species.Select(x => (x, site.SpeciesName(x)))), sp);
            return Blocks.Sec(P, b, (b.Title != string.Empty ? "<h2 class=\"sv-h2 sv-ltitle\">" + Ui.Enc(Site.Tok(site, b.Title)) + "</h2>" : string.Empty) + "<div class=\"sv-filters\" id=\"sv-filters\" data-team=\"" + Ui.Attr(team) + "\" data-sp=\"" + Ui.Attr(sp) + "\"><div class=\"sv-chips\" id=\"sv-tchips\" role=\"group\" aria-label=\"Team\">" + TeamChips(site, team) + "</div><label class=\"sv-field sv-animal\"><span>Animal</span><select id=\"sv-animal\" onchange=\"ServicesJs.animal()\">" + animals + "</select></label></div><div class=\"sv-list\" id=\"sv-list\" aria-live=\"polite\">" + await List(site, team, sp) + "</div>");
        }

        private static async Task<View> Render(SiteInfo site, string teamValue, string spValue)
        {
            string team = Team(site, teamValue);
            string sp = Species(site, spValue);
            Dictionary<string, string> parts = new Dictionary<string, string>();
            foreach (Block b in site.BlocksOf("services").Where(x => !x.Hidden && x.Type == "data"))
            {
                if (!parts.ContainsKey(b.Id))
                {
                    parts[b.Id] = await DataPart(site, b, team, sp);
                }
            }
            string main = Blocks.RenderPage(P, site, "services", "Services", b => parts.TryGetValue(b.Id, out string? h) ? h : string.Empty);
            Specialty? t = site.Specialties.FirstOrDefault(x => x.Key == team);
            return new View { Title = t != null ? t.Name : "Services", Desc = t != null && t.Blurb != string.Empty ? t.Blurb : "Every service at " + site.Settings.Name + " with its length, price and the next free time online.", Main = main };
        }
    }
}
