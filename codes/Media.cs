using System.Text;
using System.Text.Json;
using AnimalHospital.Core;
using AnimalHospital.Models;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Media : WebPage
    {
        private const string P = "mc";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string main = Blocks.RenderPage(P, site, "media", "Media", b => b.Source switch
            {
                "albums" => Albums(site, b),
                "videos" => Videos(site, b),
                "docs" => Docs(site, b),
                _ => string.Empty
            });
            Head(site, "Media | " + site.Settings.Name, "Photo albums, videos and forms to download from " + site.Settings.Name + (site.Settings.Town != string.Empty ? " in " + site.Settings.Town : string.Empty) + ".", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "MediaJs", "Media", site).Replace("{plhd_main}", main);
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
            Block? b = site.BlocksOf("media").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            if (b != null && Blocks.SafeImage(b.Media))
            {
                return b.Media;
            }
            Album? a = site.Albums.FirstOrDefault(x => Blocks.SafeImage(x.Cover));
            return a != null ? a.Cover : string.Empty;
        }

        private static string Secs(int s)
        {
            return (s / 60).ToString(Ui.Inv) + ":" + (s % 60).ToString("00", Ui.Inv);
        }

        private static string Size(long bytes)
        {
            return bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.0", Ui.Inv) + " MB" : Math.Max(1, bytes / 1024).ToString(Ui.Inv) + " KB";
        }

        private static List<MediaItem> Tagged(SiteInfo site, string kind)
        {
            return site.Media.Where(m => m.Kind == kind && m.Tags.Contains("media-center")).OrderByDescending(m => m.When).ToList();
        }

        private static string BHead(SiteInfo site, Block b, string extra)
        {
            string head = Blocks.Kick(P, site, b) + Blocks.H2(P, site, b);
            return head == string.Empty && extra == string.Empty ? string.Empty : "<div class=\"mc-shead\"><div>" + head + "</div>" + extra + "</div>";
        }

        private static string Thumb(SiteInfo site, string src)
        {
            string t = Blocks.ThumbOf(site, src);
            return t != string.Empty && Blocks.SafeImage(t) ? t : src;
        }

        private static string Albums(SiteInfo site, Block b)
        {
            List<Album> albums = site.Albums.Where(a => a.Published).Select(a => new Album { Id = a.Id, Title = a.Title, Text = a.Text, Cover = a.Cover, Date = a.Date, Published = true, Items = a.Items.Where(Blocks.SafeImage).ToList() }).Where(a => a.Items.Count > 0).OrderByDescending(a => a.Date).ToList();
            if (albums.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(BHead(site, b, "<span class=\"mc-count\">" + Ui.Plural(albums.Count, "album", "albums") + "</span>") + "<div class=\"mc-agrid\">");
            foreach (Album a in albums)
            {
                var items = a.Items.Select(src => new { src, cap = site.MediaOf(src) is MediaItem mi ? (mi.Alt != string.Empty ? mi.Alt : mi.Title) : string.Empty });
                string cover = a.Cover != string.Empty && Blocks.SafeImage(a.Cover) ? a.Cover : a.Items[0];
                List<string> peek = a.Items.Where(x => x != cover).Take(2).ToList();
                sb.Append("<button type=\"button\" class=\"mc-alb\" data-items=\"" + Ui.Attr(JsonSerializer.Serialize(items)) + "\" data-title=\"" + Ui.Attr(a.Title) + "\" onclick=\"MediaJs.lbOpen(this)\" aria-label=\"Open the album " + Ui.Attr(a.Title) + ", " + a.Items.Count + " photos\"><span class=\"mc-alb-img\"><img src=\"" + Ui.Attr(Thumb(site, cover)) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\"><em>" + Blocks.Svg("<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"14\" rx=\"2\"/><circle cx=\"9\" cy=\"10\" r=\"1.6\"/><path d=\"M20.5 16l-5-5-8 8\"/>", 15) + Ui.Plural(a.Items.Count, "photo", "photos") + "</em></span>");
                if (peek.Count > 0)
                {
                    sb.Append("<span class=\"mc-peek\">");
                    foreach (string x in peek)
                    {
                        sb.Append("<img src=\"" + Ui.Attr(Thumb(site, x)) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"427\">");
                    }
                    sb.Append("</span>");
                }
                sb.Append("<span class=\"mc-alb-t\"><b>" + Ui.Enc(a.Title) + "</b>" + (a.Text != string.Empty ? "<small>" + Ui.Enc(a.Text) + "</small>" : string.Empty) + "<span class=\"mc-more\">Open album &rarr;</span></span></button>");
            }
            sb.Append("</div>");
            return Blocks.Sec(P, b, sb.ToString());
        }

        private static string Videos(SiteInfo site, Block b)
        {
            List<MediaItem> vids = Tagged(site, "video").Where(m => Blocks.SafeVideo(m.Full)).ToList();
            if (vids.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(BHead(site, b, string.Empty) + "<div class=\"mc-vgrid" + (vids.Count == 1 ? " mc-vone" : string.Empty) + "\">");
            foreach (MediaItem m in vids)
            {
                string poster = m.Thumb != string.Empty && Blocks.SafeImage(m.Thumb) ? " poster=\"" + Ui.Attr(m.Thumb) + "\"" : string.Empty;
                sb.Append("<figure class=\"mc-vid\"><video src=\"" + Ui.Attr(m.Full) + "\"" + poster + " controls preload=\"none\" playsinline width=\"" + (m.Width > 0 ? m.Width : 1280) + "\" height=\"" + (m.Height > 0 ? m.Height : 720) + "\" aria-label=\"" + Ui.Attr(m.Title) + "\"></video><figcaption><b>" + Ui.Enc(m.Title) + "</b>" + (m.Seconds > 0 ? "<small>" + Blocks.Svg("<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/>", 14) + Secs(m.Seconds) + "</small>" : string.Empty) + "</figcaption></figure>");
            }
            sb.Append("</div>");
            return Blocks.Sec(P, b, sb.ToString());
        }

        private static string Docs(SiteInfo site, Block b)
        {
            List<MediaItem> docs = Tagged(site, "doc").Where(m => Blocks.SafeDoc(m.Full)).ToList();
            if (docs.Count == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(BHead(site, b, string.Empty) + "<div class=\"mc-dgrid\">");
            foreach (MediaItem m in docs)
            {
                string file = Path.GetFileName(m.Full);
                string thumb = m.Thumb != string.Empty && Blocks.SafeImage(m.Thumb) ? "<img src=\"" + Ui.Attr(m.Thumb) + "\" alt=\"\" loading=\"lazy\" width=\"640\" height=\"828\">" : string.Empty;
                sb.Append("<div class=\"mc-doc\"><a class=\"mc-doc-img\" href=\"" + Ui.Attr(m.Full) + "\" target=\"_blank\" rel=\"noopener\" tabindex=\"-1\" aria-hidden=\"true\"><span class=\"mc-doc-ico\">" + Blocks.Icon("doc") + "</span>" + thumb + "</a><div class=\"mc-doc-b\"><b>" + Ui.Enc(m.Title) + "</b><small>PDF" + (m.Pages > 0 ? " &middot; " + Ui.Plural(m.Pages, "page", "pages") : string.Empty) + (m.Bytes > 0 ? " &middot; " + Size(m.Bytes) : string.Empty) + "</small><span class=\"mc-doc-a\"><a class=\"mc-btn mc-btn-sm mc-btn-accent\" href=\"" + Ui.Attr(m.Full) + "\" target=\"_blank\" rel=\"noopener\" aria-label=\"Open " + Ui.Attr(m.Title) + " (PDF, new tab)\">Open</a><a class=\"mc-btn mc-btn-sm mc-btn-ghost\" href=\"" + Ui.Attr(m.Full) + "\" download=\"" + Ui.Attr(file) + "\" aria-label=\"Download " + Ui.Attr(m.Title) + "\">" + Blocks.Svg("<path d=\"M12 4v11M7.5 10.5L12 15l4.5-4.5M5 19h14\"/>", 16) + " Download</a></span></div></div>");
            }
            sb.Append("</div>");
            return Blocks.Sec(P, b, sb.ToString());
        }
    }
}
