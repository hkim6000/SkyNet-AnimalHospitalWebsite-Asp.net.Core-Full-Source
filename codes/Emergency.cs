using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Emergency : WebPage
    {
        private const string P = "em";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string main = Blocks.RenderPage(P, site, "emergency", "Emergency", b => DataPart(site, b));
            Head(site, "Emergency | " + site.Settings.Name, "Who to call if your pet needs urgent help, day or night" + (site.Settings.Town != string.Empty ? ", near " + site.Settings.Town : string.Empty) + ".", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "EmergencyJs", "Emergency", site).Replace("{plhd_main}", main);
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
            Block? b = site.BlocksOf("emergency").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            return b != null && Blocks.SafeImage(b.Media) ? b.Media : string.Empty;
        }

        private static (bool Open, string Line) OpenState(SiteInfo site)
        {
            DateTime now = Site.Now(site);
            (TimeSpan From, TimeSpan To)? w = Site.Clinic(site, now.Date);
            if (w != null && now.TimeOfDay >= w.Value.From && now.TimeOfDay < w.Value.To)
            {
                return (true, "We are open now, until " + Site.Hm(site, w.Value.To) + " today.");
            }
            if (w != null && now.TimeOfDay < w.Value.From)
            {
                return (false, "We are closed now. We open today at " + Site.Hm(site, w.Value.From) + ".");
            }
            Holiday? h = Site.HolidayOn(site, now.Date);
            for (int i = 1; i <= 14; i++)
            {
                DateTime d = now.Date.AddDays(i);
                (TimeSpan From, TimeSpan To)? n = Site.Clinic(site, d);
                if (n != null)
                {
                    return (false, "We are closed now" + (h != null ? " for " + Ui.Enc(h.Name) : string.Empty) + ". We open " + (i == 1 ? "tomorrow" : d.ToString("dddd", Ui.Us)) + " at " + Site.Hm(site, n.Value.From) + ".");
                }
            }
            return (false, "We are closed now.");
        }

        private static string DataPart(SiteInfo site, Block b)
        {
            if (b.Source != "urgent")
            {
                return string.Empty;
            }
            ClinicSettings st = site.Settings;
            (bool open, string line) = OpenState(site);
            bool er = st.ErName != string.Empty || st.ErPhone != string.Empty;
            StringBuilder sb = new StringBuilder(Blocks.Head(P, site, b, true) + "<div class=\"em-ubox" + (open ? " em-isopen" : " em-isclosed") + "\">");
            sb.Append("<div class=\"em-card em-us\"><span class=\"em-tag\">" + (open ? "<i></i>Open now" : "<i class=\"em-shut\"></i>Closed now") + "</span><h3>Call us now</h3><p class=\"em-who\">" + Ui.Enc(st.Name) + "</p><a class=\"em-tel\" href=\"tel:" + Ui.Attr(Site.Tel(st.Phone)) + "\">" + Blocks.Icon("phone") + "<span><small>Tap to call</small><b>" + Ui.Enc(st.Phone) + "</b></span></a><p class=\"em-state\">" + line + "</p><p class=\"em-sub\">" + Ui.Enc(st.Address) + "</p></div>");
            if (er)
            {
                sb.Append("<div class=\"em-card em-erc\"><span class=\"em-tag em-tag-er\">" + (open ? "When we are closed" : "Go here now") + "</span><h3>After-hours emergency care</h3><p class=\"em-who\">" + Ui.Enc(st.ErName) + "</p>" + (st.ErPhone != string.Empty ? "<a class=\"em-tel em-tel-er\" href=\"tel:" + Ui.Attr(Site.Tel(st.ErPhone)) + "\">" + Blocks.Icon("phone") + "<span><small>Tap to call</small><b>" + Ui.Enc(st.ErPhone) + "</b></span></a>" : string.Empty) + (st.ErAddress != string.Empty ? "<p class=\"em-state\">" + Blocks.Icon("pin") + "<span>" + Ui.Enc(st.ErAddress) + "</span></p>" : string.Empty) + "<p class=\"em-sub\">They are a separate hospital. Please call them on the way so they can get ready.</p></div>");
            }
            sb.Append("</div><p class=\"em-note\">" + Blocks.Icon("shield") + "<span>This page does not give veterinary advice. If you are worried about your pet, call a vet.</span></p>");
            return Blocks.Sec(P, b, sb.ToString());
        }
    }
}
