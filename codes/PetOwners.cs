using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class PetOwners : WebPage
    {
        private const string P = "pi";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string main = Blocks.RenderPage(P, site, "petowners", "For pet owners", b => b.Source switch
            {
                "vaxchart" => VaxChart(site, b),
                "insurance" => Insurance(site, b),
                _ => string.Empty
            });
            Head(site, "Pet owners | " + site.Settings.Name, "First visits, vaccine schedules for dogs, cats and rabbits, pet insurance and common questions at " + site.Settings.Name + ".", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "PetOwnersJs", "PetOwners", site).Replace("{plhd_main}", main);
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
            Block? b = site.BlocksOf("petowners").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            return b != null && Blocks.SafeImage(b.Media) ? b.Media : string.Empty;
        }

        private static string VaccineLink(SiteInfo site)
        {
            CareService? vv = site.Services.FirstOrDefault(x => x.Key == "bt-vaccine-visit" && x.Active && x.Online) ?? site.Services.FirstOrDefault(x => x.Active && x.Online && x.Name.Contains("vaccine", StringComparison.OrdinalIgnoreCase));
            return vv != null ? " <a href=\"Book?s=" + Uri.EscapeDataString(vv.Key) + "\">Book a vaccine visit</a>" : string.Empty;
        }

        private static string VaxChart(SiteInfo site, Block b)
        {
            List<VaxPlan> plans = site.Settings.VaxChart.Where(v => v.Name != string.Empty).ToList();
            List<string> order = site.SpeciesList.OrderBy(x => x.Order).Select(x => x.Key).ToList();
            List<string> kinds = plans.Select(v => v.Species).Distinct().OrderBy(k => order.IndexOf(k) < 0 ? 99 : order.IndexOf(k)).ToList();
            StringBuilder sb = new StringBuilder(Blocks.Head(P, site, b, true));
            if (kinds.Count == 0)
            {
                sb.Append("<p class=\"pi-none\">Ask us about the right vaccines for your pet.</p>");
                return Blocks.Sec(P, b, sb.ToString(), "pi-vsec");
            }
            sb.Append("<div class=\"pi-vax\"><div class=\"pi-tabs\" role=\"tablist\" aria-label=\"Animal\">");
            for (int i = 0; i < kinds.Count; i++)
            {
                string k = kinds[i];
                sb.Append("<button type=\"button\" role=\"tab\" class=\"pi-tab" + (i == 0 ? " pi-act" : string.Empty) + "\" id=\"pi-tab-" + Ui.Attr(k) + "\" aria-controls=\"pi-panel-" + Ui.Attr(k) + "\" aria-selected=\"" + (i == 0 ? "true" : "false") + "\" tabindex=\"" + (i == 0 ? "0" : "-1") + "\" data-k=\"" + Ui.Attr(k) + "\" onclick=\"PetOwnersJs.tab('" + Ui.Js(k) + "')\">" + Ui.Enc(site.SpeciesPlural(k)) + "<small>" + plans.Count(v => v.Species == k) + "</small></button>");
            }
            sb.Append("</div>");
            for (int i = 0; i < kinds.Count; i++)
            {
                string k = kinds[i];
                sb.Append("<div class=\"pi-panel\" role=\"tabpanel\" id=\"pi-panel-" + Ui.Attr(k) + "\" aria-labelledby=\"pi-tab-" + Ui.Attr(k) + "\"" + (i == 0 ? string.Empty : " hidden") + "><table class=\"pi-vtable\"><thead><tr><th scope=\"col\">Vaccine</th><th scope=\"col\">First doses</th><th scope=\"col\">Then</th></tr></thead><tbody>");
                foreach (VaxPlan v in plans.Where(x => x.Species == k).OrderBy(x => x.Core ? 0 : 1))
                {
                    sb.Append("<tr><th scope=\"row\"><b>" + Ui.Enc(v.Name) + "</b><span class=\"pi-vk " + (v.Core ? "pi-vk-core" : "pi-vk-life") + "\">" + (v.Core ? "Core" : "Lifestyle") + "</span></th><td data-l=\"First doses\">" + Ui.Enc(v.First) + "</td><td data-l=\"Then\">" + Ui.Enc(v.Every) + "</td></tr>");
                }
                sb.Append("</tbody></table></div>");
            }
            MediaItem? pdf = site.Media.Where(m => m.Kind == "doc" && (m.Title.Contains("vaccine", StringComparison.OrdinalIgnoreCase) || m.Full.Contains("vaccine", StringComparison.OrdinalIgnoreCase)) && Blocks.SafeDoc(m.Full)).OrderByDescending(m => m.When).FirstOrDefault();
            sb.Append("<div class=\"pi-vfoot\"><p class=\"pi-legend\"><span class=\"pi-vk pi-vk-core\">Core</span> every pet of that kind needs it. <span class=\"pi-vk pi-vk-life\">Lifestyle</span> depends on where your pet goes and who they meet.</p>");
            if (pdf != null)
            {
                sb.Append("<a class=\"pi-btn pi-btn-ghost pi-btn-sm\" href=\"" + Ui.Attr(pdf.Full) + "\" target=\"_blank\" rel=\"noopener\">" + Blocks.Svg("<path d=\"M7 3h7l4 4v14H7z\"/><path d=\"M14 3v4h4M10 12h5M10 16h5\"/>", 18) + " Vaccine schedule (PDF" + (pdf.Pages > 0 ? ", " + Ui.Plural(pdf.Pages, "page", "pages") : string.Empty) + ")</a>");
            }
            sb.Append("</div></div><p class=\"pi-hint pi-vnote\">Your vet will tailor the plan to your pet." + VaccineLink(site) + "</p>");
            return Blocks.Sec(P, b, sb.ToString(), "pi-vsec");
        }

        private static string Insurance(SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"pi-ins\"><div class=\"pi-ins-t\">" + Blocks.Kick(P, site, b) + Blocks.H2(P, site, b) + Blocks.Rich(P, site, b.Text, "pi-lead") + "</div><div class=\"pi-ins-l\">");
            if (site.Settings.Insurance.Count == 0)
            {
                sb.Append("<p class=\"pi-none\">Ask us which plans we work with.</p>");
            }
            else
            {
                sb.Append("<ul class=\"pi-plans\">");
                foreach (string ins in site.Settings.Insurance)
                {
                    bool pay = ins.StartsWith("Pay", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<li" + (pay ? " class=\"pi-pay\"" : string.Empty) + "><span class=\"pi-pico\">" + Blocks.Icon(pay ? "check" : "shield") + "</span>" + Ui.Enc(ins) + "</li>");
                }
                sb.Append("</ul>");
            }
            sb.Append("<p class=\"pi-hint\">Plan not listed? You can still pay at the visit and claim with the itemised receipt.</p></div></div>");
            return Blocks.Sec(P, b, sb.ToString());
        }
    }
}
