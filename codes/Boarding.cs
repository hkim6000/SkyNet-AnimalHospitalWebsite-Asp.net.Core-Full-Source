using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Boarding : WebPage
    {
        private const string P = "bo";

        private static readonly (string Key, string Name)[] Sizes = { ("small", "Small (under 20 lb)"), ("medium", "Medium (20 to 50 lb)"), ("large", "Large (over 50 lb)") };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            Dictionary<string, int[]> used = await Used(site);
            string main = Blocks.RenderPage(P, site, "boarding", "Boarding", b => b.Source switch
            {
                "runlist" => RunList(site, b, used),
                "vaxrules" => VaxRules(site, b),
                _ => string.Empty
            });
            Head(site, "Boarding | " + site.Settings.Name, "Dog runs by size, cat condos, nightly prices, free places this week and the vaccines needed for a stay at " + site.Settings.Name + ".", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "BoardingJs", "Boarding", site).Replace("{plhd_main}", main);
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

        private static string Pic(SiteInfo site)
        {
            Block? b = site.BlocksOf("boarding").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            if (b != null && Blocks.SafeImage(b.Media))
            {
                return b.Media;
            }
            RunType? r = site.Runs.FirstOrDefault(x => Blocks.SafeImage(x.Image));
            return r != null ? r.Image : string.Empty;
        }

        private static async Task<Dictionary<string, int[]>> Used(SiteInfo site)
        {
            Dictionary<string, int[]> map = new Dictionary<string, int[]>();
            DateTime today = Site.Today(site);
            var (rows, emsg) = await Helper.GetListT<BoNightRow>("SELECT ISNULL(R.ROOMTYPE_ID, A.ROOMTYPE_ID) AS RoomtypeId, A.ROOM_ID AS RoomId, A.STATUS AS Status, A.START_AT AS FromAt, CASE WHEN A.STATUS = N'checkedin' AND V.PLAN_OUT IS NOT NULL THEN V.PLAN_OUT ELSE A.END_AT END AS ToAt FROM APPOINTMENT A LEFT JOIN ROOM R ON R.ROOM_ID = A.ROOM_ID AND A.ROOM_ID <> N'' LEFT JOIN VISIT V ON V.VISIT_ID = A.VISIT_ID AND A.VISIT_ID <> N'' WHERE A.VISIT_TYPE = N'boarding' AND A.START_AT < @to AND (A.STATUS IN (N'booked', N'confirmed') OR (A.STATUS = N'checkedin' AND V.STATUS IN (N'inroom', N'checkout')))", new List<SqlParameter> { App.PDate("to", today.AddDays(8)) });
            if (emsg != string.Empty || rows == null)
            {
                return map;
            }
            foreach (RunType r in site.Runs)
            {
                int[] n = new int[7];
                for (int i = 0; i < 7; i++)
                {
                    DateTime night = today.AddDays(i);
                    List<BoNightRow> on = rows.Where(x => x.RoomtypeId == r.Key && x.FromAt.Date <= night && (x.Status == "checkedin" && x.ToAt.Date <= today ? today.AddDays(1) : x.ToAt.Date) > night).ToList();
                    n[i] = on.Where(x => x.RoomId != string.Empty).Select(x => x.RoomId).Distinct().Count() + on.Count(x => x.RoomId == string.Empty);
                }
                map[r.Key] = n;
            }
            return map;
        }

        private static string SpeciesWho(SiteInfo site, RunType r)
        {
            List<string> l = r.SpeciesList.Count > 0 ? r.SpeciesList : new List<string> { r.Species };
            return string.Join(", ", l.Select(site.SpeciesPlural));
        }

        private static string ForWho(SiteInfo site, RunType r)
        {
            string sizes = r.Sizes.Count == 0 ? "any size" : string.Join(", ", r.Sizes.Select(s => Sizes.FirstOrDefault(x => x.Key == s).Name ?? s));
            return Ui.Enc(SpeciesWho(site, r)) + " &middot; " + Ui.Enc(sizes);
        }

        private static string Nights(SiteInfo site, RunType r, Dictionary<string, int[]> used)
        {
            DateTime today = Site.Today(site);
            int[] u = used.TryGetValue(r.Key, out int[]? x) ? x : new int[7];
            StringBuilder sb = new StringBuilder("<div class=\"bo-nights\" role=\"list\" aria-label=\"Free places for the next 7 nights\">");
            for (int i = 0; i < 7; i++)
            {
                DateTime night = today.AddDays(i);
                int free = Math.Max(0, r.Count - u[i]);
                string cls = free == 0 ? "bo-n-full" : free <= 2 ? "bo-n-few" : "bo-n-ok";
                string label = night.ToString("dddd, MMMM d", Ui.Us) + ": " + (free == 0 ? "full" : free + " of " + r.Count + " free");
                sb.Append("<span class=\"bo-n " + cls + "\" role=\"listitem\" title=\"" + Ui.Attr(label) + "\" aria-label=\"" + Ui.Attr(label) + "\"><small>" + (i == 0 ? "Tonight" : night.ToString("ddd", Ui.Us)) + "</small><b>" + (free == 0 ? "Full" : free.ToString(Ui.Inv)) + "</b><small>" + night.ToString("MMM d", Ui.Us) + "</small></span>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string RunList(SiteInfo site, Block b, Dictionary<string, int[]> used)
        {
            List<RunType> runs = site.Runs.Where(r => r.Active).OrderBy(r => r.Order).ToList();
            StringBuilder sb = new StringBuilder(Blocks.Head(P, site, b, true));
            if (runs.Count == 0)
            {
                sb.Append("<p class=\"bo-none\">Boarding is not open for booking right now. Please call us on " + Ui.Enc(site.Settings.Phone) + ".</p>");
                return Blocks.Sec(P, b, sb.ToString());
            }
            string label = b.Label != string.Empty ? b.Label : "Book a stay";
            bool prices = site.Settings.ShowPrices;
            sb.Append("<div class=\"bo-runs\">");
            foreach (RunType r in runs)
            {
                string pic = Blocks.ThumbOf(site, r.Image);
                string act = r.Online ? "<a class=\"bo-btn bo-btn-accent\" href=\"BookStay?r=" + Uri.EscapeDataString(r.Key) + "\" aria-label=\"" + Ui.Attr(Site.Tok(site, label) + ": " + r.Name) + "\">" + Ui.Enc(Site.Tok(site, label)) + "</a>" : "<a class=\"bo-btn bo-btn-ghost\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">Call to book</a>";
                sb.Append("<article class=\"bo-run\"><div class=\"bo-run-img\">" + (pic != string.Empty && Blocks.SafeImage(pic) ? "<img src=\"" + Ui.Attr(pic) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\">" : string.Empty) + (prices && r.Price > 0 ? "<span class=\"bo-price\"><b>" + Site.Price(r.Price) + "</b> a night</span>" : string.Empty) + "</div><div class=\"bo-run-b\"><span class=\"bo-for\">" + ForWho(site, r) + "</span><h3>" + Ui.Enc(r.Name) + "</h3>" + (r.Short != string.Empty ? "<p class=\"bo-short\">" + Ui.Enc(r.Short) + "</p>" : string.Empty) + (r.Text != string.Empty ? "<p class=\"bo-text\">" + Ui.Enc(r.Text) + "</p>" : string.Empty) + "<div class=\"bo-avail\"><span class=\"bo-alabel\">Free places each night</span>" + Nights(site, r, used) + "</div><div class=\"bo-run-f\"><small>" + Ui.Plural(r.Count, "place", "places") + " in all</small>" + act + "</div></div></article>");
            }
            sb.Append("</div>");
            List<AddOn> adds = site.AddOns.Where(a => a.Active).OrderBy(a => a.Order).ToList();
            if (adds.Count > 0)
            {
                sb.Append("<div class=\"bo-adds\"><div class=\"bo-adds-h\"><h3>Add-ons</h3><p>Add these when you book a stay." + (prices ? " Prices are on top of the nightly rate." : string.Empty) + "</p></div><ul class=\"bo-alist\">");
                foreach (AddOn a in adds)
                {
                    string price = a.Price <= 0 ? "Free" : Site.Price(a.Price) + (a.PerNight ? " a night" : " once");
                    string who = a.Species == string.Empty ? "All pets" : string.Join(", ", Site.Csv(a.Species).Select(site.SpeciesPlural)) + " only";
                    sb.Append("<li><div><b>" + Ui.Enc(a.Name) + "</b>" + (a.Text != string.Empty ? "<small>" + Ui.Enc(a.Text) + "</small>" : string.Empty) + "<span class=\"bo-who\">" + Ui.Enc(who) + "</span></div>" + (prices || a.Price <= 0 ? "<span class=\"bo-aprice" + (a.Price <= 0 ? " bo-free" : string.Empty) + "\">" + price + "</span>" : string.Empty) + "</li>");
                }
                sb.Append("</ul></div>");
            }
            return Blocks.Sec(P, b, sb.ToString(), "bo-rsec");
        }

        private static string VaxRules(SiteInfo site, Block b)
        {
            ClinicSettings s = site.Settings;
            StringBuilder sb = new StringBuilder("<div class=\"bo-vax\"><div class=\"bo-vax-t\">" + Blocks.Kick(P, site, b) + Blocks.H2(P, site, b) + Blocks.Rich(P, site, b.Text, "bo-lead") + "<div class=\"bo-vlist\">");
            List<VaxRule> rules = s.BoardingVax.Where(v => v.Required.Count > 0).ToList();
            if (rules.Count == 0)
            {
                sb.Append("<p class=\"bo-none\">No vaccines are needed for a stay right now.</p>");
            }
            foreach (VaxRule v in rules)
            {
                sb.Append("<div class=\"bo-vcard\"><span class=\"bo-vico\">" + Blocks.Icon(v.Species == "cat" ? "cat" : v.Species == "dog" ? "dog" : "paw") + "</span><div><b>" + Ui.Enc(site.SpeciesPlural(v.Species)) + " need</b><ul>");
                foreach (string n in v.Required)
                {
                    sb.Append("<li>" + Blocks.Svg("<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>", 14) + Ui.Enc(n) + "</li>");
                }
                sb.Append("</ul></div></div>");
            }
            sb.Append("</div><p class=\"bo-rule\">" + Blocks.Icon("shield") + "<span>Each vaccine must be <b>valid until the day you pick up</b>, not just the day you drop off. If one runs out during the stay, book a vaccine visit first.</span></p></div>");
            CareService? vv = site.Services.FirstOrDefault(x => x.Key == "bt-vaccine-visit" && x.Active && x.Online) ?? site.Services.FirstOrDefault(x => x.Active && x.Online && x.Name.Contains("vaccine", StringComparison.OrdinalIgnoreCase));
            sb.Append("<aside class=\"bo-times\" aria-label=\"Times and changes\"><h3>Times and changes</h3><dl><div><dt>" + Blocks.Icon("clock") + "Check-in</dt><dd>" + Site.HmText(site, s.CheckInFrom) + " &ndash; " + Site.HmText(site, s.CheckInTo) + "<small>On days we are open</small></dd></div><div><dt>" + Blocks.Icon("cal") + "Check-out</dt><dd>By " + Site.HmText(site, s.CheckOutBy) + "<small>The night count ends that morning</small></dd></div><div><dt>" + Blocks.Icon("phone") + "Cancel or change</dt><dd>Up to " + s.StayCancelHours + " hours before check-in<small>Online with your booking code, or call us</small></dd></div></dl><div class=\"bo-tacts\">" + (vv != null ? "<a class=\"bo-btn bo-btn-accent bo-btn-wide\" href=\"Book?s=" + Uri.EscapeDataString(vv.Key) + "\">Book a vaccine visit</a>" : string.Empty) + "<a class=\"bo-btn bo-btn-ghost bo-btn-wide\" href=\"MyAppointment\">Change or cancel a stay</a></div></aside></div>");
            return Blocks.Sec(P, b, sb.ToString());
        }
    }
}
