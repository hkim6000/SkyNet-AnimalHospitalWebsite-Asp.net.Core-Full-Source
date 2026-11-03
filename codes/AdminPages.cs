using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminPages : WebPage
    {
        private const string Key = "pages";
        private const string Me = "AdminPagesJs";
        private static readonly (string Key, string Name, string Href)[] Extra = { ("faqs", "Questions", "PetOwners"), ("vaxchart", "Vaccine chart", "PetOwners") };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Pages | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--pg-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("pg"));
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

        private static ApiResponse Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript(Me + ".toast('" + Ui.Js(msg) + "');");
            return response;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string message)
        {
            await App.Log(c.U.StaffId, action, "pages", false);
            response.ExecuteScript(Me + ".fail('" + Ui.Js(message) + "');");
            return response;
        }

        private string Val(string k, int max)
        {
            return Ui.Clip(GetDataValue(k) ?? string.Empty, max);
        }

        private DateTime? Base()
        {
            return long.TryParse(Val("pbase", 24), out long t) && t > 0 && t < DateTime.MaxValue.Ticks ? new DateTime(t) : null;
        }

        private static string Stamp(DateTime d)
        {
            return d == default ? string.Empty : d.Ticks.ToString(Ui.Inv);
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("pg-menu", Auth.Menu("pg", Me, "AdminPages", c));
            response.SetElementContents("pg-who", Auth.Who("pg", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("pg-main", "<div class=\"pg-ptop\"><div><h1>Pages</h1></div></div><div class=\"pg-panel\"><p>You don't have access to this page. Ask the hospital manager if you need it.</p></div>");
                return response;
            }
            response.SetElementContents("pg-main", MainHtml(c));
            response.ExecuteScript(Me + ".loaded();");
            return response;
        }

        private static string PickItem(PgMedia m)
        {
            bool vid = m.Kind == "video";
            List<string> tags = Site.Csv(m.Tags);
            return "<button type=\"button\" class=\"pg-pki\" data-src=\"" + Ui.Attr(m.FullPath) + "\" data-thumb=\"" + Ui.Attr(m.ThumbPath) + "\" data-kind=\"" + Ui.Attr(m.Kind) + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"" + Me + ".pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (tags.Count > 0 ? " · " + string.Join(", ", tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.ThumbPath) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"pg-vbadge\">" + (m.Seconds / 60).ToString(Ui.Inv) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "media picker", "You can look at the pages but not change them.");
            }
            string allow = Val("pallow", 10) is "photo" or "video" or "doc" ? Val("pallow", 10) : string.Empty;
            string kind = allow != string.Empty ? allow : Val("pkind", 10) is "photo" or "video" or "doc" ? Val("pkind", 10) : string.Empty;
            string term = Val("pterm", 60).Trim().ToLowerInvariant();
            string tag = Val("ptag", 40);
            List<string> ctx = Val("pctx", 80).ToLowerInvariant().Split(',').Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
            int page = int.TryParse(GetDataValue("ppg"), out int pv) ? Math.Clamp(pv, 1, 200) : 1;
            const int size = 30;
            var (rows, emsg) = await Helper.GetListT<PgMedia>("SELECT MEDIA_ID AS MediaId, KIND AS Kind, TITLE AS Title, ALT AS Alt, TAGS AS Tags, FULL_PATH AS FullPath, THUMB_PATH AS ThumbPath, SECONDS AS Seconds, ADDED_UTC AS AddedUtc FROM MEDIA ORDER BY ADDED_UTC DESC, MEDIA_ID");
            if (emsg != string.Empty || rows == null)
            {
                return Toast(response, "The media library could not be loaded. Try again.");
            }
            List<PgMedia> all = rows.Where(m => (kind == string.Empty ? m.Kind != "doc" : m.Kind == kind) && (tag == string.Empty || Site.Csv(m.Tags).Contains(tag)) && (term == string.Empty || (m.Title + " " + m.Alt + " " + m.Tags).ToLowerInvariant().Contains(term))).OrderByDescending(m => Site.Csv(m.Tags).Count(t => ctx.Contains(t))).ThenByDescending(m => m.AddedUtc).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (PgMedia m in all.Skip((page - 1) * size).Take(size))
            {
                sb.Append(PickItem(m));
            }
            if (all.Count == 0)
            {
                sb.Append("<p class=\"pg-none pg-pkall\">Nothing matches. Upload a photo here or in the media library.</p>");
            }
            string tags = page == 1 ? JsonSerializer.Serialize(rows.SelectMany(m => Site.Csv(m.Tags)).Distinct().OrderBy(t => t).ToList()) : "null";
            response.ExecuteScript(Me + ".pkShown(" + JsonSerializer.Serialize(sb.ToString()).Replace("<", "\\u003c") + "," + (all.Count > page * size ? "true" : "false") + "," + tags.Replace("<", "\\u003c") + "," + (page > 1 ? "true" : "false") + ");");
            return response;
        }

        private static string PickExt(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            {
                return "jpg";
            }
            if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
            {
                return "png";
            }
            if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            {
                return "webp";
            }
            return string.Empty;
        }

        private static (int W, int H) ImageSize(byte[] b)
        {
            try
            {
                if (b.Length > 24 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
                {
                    return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
                }
                if (b.Length > 30 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
                {
                    string chunk = Encoding.ASCII.GetString(b, 12, 4);
                    if (chunk == "VP8 " && b[23] == 0x9D && b[24] == 0x01 && b[25] == 0x2A)
                    {
                        return ((b[26] | (b[27] << 8)) & 0x3FFF, (b[28] | (b[29] << 8)) & 0x3FFF);
                    }
                    if (chunk == "VP8L" && b[20] == 0x2F)
                    {
                        int bits = b[21] | (b[22] << 8) | (b[23] << 16) | (b[24] << 24);
                        return ((bits & 0x3FFF) + 1, ((bits >> 14) & 0x3FFF) + 1);
                    }
                    if (chunk == "VP8X")
                    {
                        return ((b[24] | (b[25] << 8) | (b[26] << 16)) + 1, (b[27] | (b[28] << 8) | (b[29] << 16)) + 1);
                    }
                    return (0, 0);
                }
                if (b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8)
                {
                    int i = 2;
                    while (i + 9 < b.Length)
                    {
                        if (b[i] != 0xFF)
                        {
                            i++;
                            continue;
                        }
                        byte m = b[i + 1];
                        if (m == 0xFF)
                        {
                            i++;
                            continue;
                        }
                        if (m == 0xD8 || m == 0x01 || (m >= 0xD0 && m <= 0xD7))
                        {
                            i += 2;
                            continue;
                        }
                        int len = (b[i + 2] << 8) | b[i + 3];
                        if (m >= 0xC0 && m <= 0xCF && m != 0xC4 && m != 0xC8 && m != 0xCC)
                        {
                            return ((b[i + 7] << 8) | b[i + 8], (b[i + 5] << 8) | b[i + 6]);
                        }
                        if (len < 2)
                        {
                            break;
                        }
                        i += 2 + len;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return (0, 0);
            }
            return (0, 0);
        }

        private static bool SizeOk((int W, int H) d)
        {
            return d.W > 0 && d.H > 0 && d.W <= 4000 && d.H <= 4000 && (long)d.W * d.H <= 16000000L;
        }

        private static byte[]? PickData(string data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && PickExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public async Task<ApiResponse> MediaUp()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can("media", 'A'))
            {
                await App.Log(c.U.StaffId, "upload photo", "pages", false);
                response.ExecuteScript(Me + ".pkUploaded(false,'Uploading needs the Add right on the Media library.');");
                return response;
            }
            byte[]? big = PickData(GetDataValue("full") ?? string.Empty, 2400 * 1024);
            byte[]? small = PickData(GetDataValue("thumb") ?? string.Empty, 600 * 1024);
            if (big == null || small == null || PickExt(big) != PickExt(small))
            {
                response.ExecuteScript(Me + ".pkUploaded(false,'That file is not a JPG, PNG or WebP photo, or it is too large.');");
                return response;
            }
            (int w, int h) = ImageSize(big);
            if (!SizeOk((w, h)) || !SizeOk(ImageSize(small)))
            {
                response.ExecuteScript(Me + ".pkUploaded(false,'That photo is too big. Use one up to 4000 pixels on each side.');");
                return response;
            }
            string ext = PickExt(big);
            string id = App.NewId();
            string dir = Path.Combine(App.Root, "images", "media");
            Directory.CreateDirectory(Path.Combine(dir, "full"));
            Directory.CreateDirectory(Path.Combine(dir, "thumbs"));
            string full = "images/media/full/" + id + "." + ext;
            string thumb = "images/media/thumbs/" + id + "." + ext;
            await File.WriteAllBytesAsync(Path.Combine(dir, "full", id + "." + ext), big);
            await File.WriteAllBytesAsync(Path.Combine(dir, "thumbs", id + "." + ext), small);
            string title = Regex.Replace(Val("pname", 80), "\\s+", " ").Trim();
            if (title.Length < 2)
            {
                title = "Photo " + id.Substring(0, 6);
            }
            List<string> ctx = Val("pctx", 80).ToLowerInvariant().Split(',').Select(x => Regex.Replace(x, "[^a-z0-9 -]", string.Empty).Trim()).Where(x => x != string.Empty).Distinct().Take(4).ToList();
            string res = await Helper.PutData(new List<string> { "INSERT INTO MEDIA (MEDIA_ID, KIND, TITLE, ALT, TAGS, FULL_PATH, THUMB_PATH, WIDTH, HEIGHT, BYTES, ADDED_BY, ADDED_UTC) VALUES (@id, N'photo', @title, N'', @tags, @full, @thumb, @w, @h, @bytes, @by, SYSUTCDATETIME())" }, new List<SqlParameter>
            {
                App.P("id", id, 50), App.P("title", title, 120), App.P("tags", string.Join(",", ctx), 200), App.P("full", full, 300), App.P("thumb", thumb, 300),
                App.PInt("w", w), App.PInt("h", h), new SqlParameter("bytes", SqlDbType.BigInt) { Value = (long)big.Length }, App.P("by", c.U.StaffId, 50)
            });
            if (Helper.IsError(res))
            {
                File.Delete(Path.Combine(dir, "full", id + "." + ext));
                File.Delete(Path.Combine(dir, "thumbs", id + "." + ext));
                response.ExecuteScript(Me + ".pkUploaded(false,'The photo could not be saved. Try again.');");
                return response;
            }
            Site.Forget();
            await App.Log(c.U.StaffId, "upload photo", title, true);
            response.ExecuteScript(Me + ".pkUploaded(true,'');");
            return response;
        }


        private static string CE(string f, string value, string ph, string cls, bool ro, bool multi = false)
        {
            return "<div class=\"" + cls + (multi ? " pg-ml" : string.Empty) + "\" data-f=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Attr(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Enc(value) + "</div>";
        }

        private static string CI(string f, string value, string ph, bool ro, bool multi)
        {
            return "<div data-i=\"" + f + "\" contenteditable=\"" + (ro ? "false" : "plaintext-only") + "\" data-ph=\"" + Ui.Attr(ph) + "\"" + (multi ? " data-ml=\"1\"" : string.Empty) + ">" + Ui.Enc(value) + "</div>";
        }

        private static string RichEd(string html, string ph, bool ro)
        {
            return "<div class=\"pg-er\" data-f=\"text\" contenteditable=\"" + (ro ? "false" : "true") + "\" data-ph=\"" + Ui.Attr(ph) + "\">" + Blocks.Sanitize(html, 20000) + "</div>";
        }

        private static string MediaEd(SiteInfo site, string src, string allow, string ph, bool ro)
        {
            string thumb = Blocks.ThumbOf(site, src);
            bool vid = Blocks.IsVideo(src);
            return "<div class=\"pg-em\" data-f=\"media\" data-src=\"" + Ui.Attr(src) + "\" data-allow=\"" + allow + "\" data-ph=\"" + Ui.Attr(ph) + "\"><button type=\"button\" class=\"pg-emb\"" + (ro ? " disabled" : " onclick=\"" + Me + ".media(this)\"") + " aria-label=\"" + Ui.Attr(ph) + "\">" + (thumb != string.Empty ? "<img src=\"" + Ui.Attr(thumb) + "\" alt=\"\">" + (vid ? "<span class=\"pg-emv\">Video</span>" : string.Empty) + (ro ? string.Empty : "<span class=\"pg-emc\">Change</span>") : "<span>" + Ui.Enc(ph) + "</span>") + "</button>" + (ro ? string.Empty : "<button type=\"button\" class=\"pg-emx\" onclick=\"" + Me + ".noMedia(this)\"" + (thumb == string.Empty ? " hidden" : string.Empty) + ">Remove</button>") + "</div>";
        }

        private static string BtnEd(string n, string label, string link, string title, bool ro)
        {
            string r = ro ? " readonly" : string.Empty;
            return "<div class=\"pg-ebtn\"><span>" + title + "</span><input type=\"text\" data-f=\"label" + n + "\" maxlength=\"40\" value=\"" + Ui.Attr(label) + "\" placeholder=\"Button text\" aria-label=\"" + title + " text\"" + r + "><input type=\"text\" data-f=\"link" + n + "\" maxlength=\"300\" value=\"" + Ui.Attr(link) + "\" placeholder=\"Link: Book, Services or https://\" aria-label=\"" + title + " link\"" + r + "></div>";
        }

        private static string Svg(string d, double w = 2)
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"" + w.ToString(Ui.Inv) + "\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"" + d + "\"/></svg>";
        }

        private const string Up = "M6 15l6-6 6 6";
        private const string Down = "M6 9l6 6 6-6";
        private const string Bin = "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3";

        private static string ItemEd(SiteInfo site, Block b, PartSpec spec, BlockItem it, bool ro)
        {
            bool icon = spec.ItemFields.Contains("icon");
            bool link = spec.ItemFields.Contains("link");
            bool pic = spec.ItemFields.Contains("media");
            (string tp, string xp) = b.Type switch
            {
                "steps" => ("Step", "What happens in this step"),
                "stats" => ("Number, like 4,200", "What it counts"),
                "people" => ("Name", "Role or title"),
                "boxes" => ("Box title", "One item per line"),
                "faq" => ("Question", "Answer"),
                "cards" => ("Card title", "Short text"),
                _ => ("Title", "Text")
            };
            StringBuilder sb = new StringBuilder("<div class=\"pg-eit\">");
            if (icon)
            {
                sb.Append("<select data-i=\"icon\" aria-label=\"Icon\"" + (ro ? " disabled" : string.Empty) + "><option value=\"\">No icon</option>" + Ui.Opts(Blocks.Icons.Select(x => (x.Key, x.Name)), it.Icon) + "</select>");
            }
            if (pic)
            {
                sb.Append(MediaEd(site, it.Media, "photo", "Add a photo", ro).Replace("data-f=\"media\"", "data-i=\"media\"").Replace("class=\"pg-em\"", "class=\"pg-em pg-emi\""));
            }
            sb.Append(CI("title", it.Title, tp, ro, false) + CI("text", it.Text, xp, ro, true));
            if (link)
            {
                sb.Append("<div class=\"pg-elink\"><input type=\"text\" data-i=\"label\" maxlength=\"40\" value=\"" + Ui.Attr(it.Label) + "\" placeholder=\"Link text\" aria-label=\"Link text\"" + (ro ? " readonly" : string.Empty) + "><input type=\"text\" data-i=\"link\" maxlength=\"300\" value=\"" + Ui.Attr(it.Link) + "\" placeholder=\"Link: Services or https://\" aria-label=\"Link\"" + (ro ? " readonly" : string.Empty) + "></div>");
            }
            if (!ro)
            {
                sb.Append("<div class=\"pg-ictl\"><button type=\"button\" aria-label=\"Move earlier\" onclick=\"" + Me + ".itemMove(this,-1)\">" + Svg(Up) + "</button><button type=\"button\" aria-label=\"Move later\" onclick=\"" + Me + ".itemMove(this,1)\">" + Svg(Down) + "</button><button type=\"button\" aria-label=\"Remove\" onclick=\"" + Me + ".itemDrop(this)\">" + Svg(Bin, 1.8) + "</button></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Ed(SiteInfo site, Block b, bool ro)
        {
            PartSpec? spec = Blocks.Spec(b);
            if (spec == null)
            {
                return string.Empty;
            }
            bool data = b.Type == "data";
            string x = data ? "data" : b.Type;
            StringBuilder sb = new StringBuilder("<div class=\"pg-blk pg-x-" + x + (b.Hidden ? " pg-hid" : string.Empty) + "\" data-id=\"" + Ui.Attr(b.Id) + "\" data-type=\"" + Ui.Attr(b.Type) + "\" data-src=\"" + Ui.Attr(b.Source) + "\" data-hidden=\"" + (b.Hidden ? "1" : "0") + "\"><header class=\"pg-bh\">");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pg-grip\" aria-label=\"Drag to move\" title=\"Drag to move\" onpointerdown=\"" + Me + ".grab(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button>");
            }
            sb.Append("<b>" + Ui.Enc(spec.Name) + "</b>" + (data ? "<span class=\"pg-src\">From your data</span>" : string.Empty) + (b.Hidden && ro ? "<span class=\"pg-src\">Hidden</span>" : string.Empty) + "<span class=\"pg-bsp\"></span>");
            if (!ro)
            {
                sb.Append("<button type=\"button\" class=\"pg-hb\" aria-pressed=\"" + (b.Hidden ? "true" : "false") + "\" onclick=\"" + Me + ".hide(this)\" title=\"Hide on the website without deleting\"><span>" + (b.Hidden ? "Show" : "Hide") + "</span></button><button type=\"button\" class=\"pg-hb\" aria-label=\"Move up\" title=\"Move up\" onclick=\"" + Me + ".move(this,-1)\">" + Svg(Up) + "</button><button type=\"button\" class=\"pg-hb\" aria-label=\"Move down\" title=\"Move down\" onclick=\"" + Me + ".move(this,1)\">" + Svg(Down) + "</button>" + (data ? string.Empty : "<button type=\"button\" class=\"pg-hb pg-del\" aria-label=\"Remove block\" title=\"Remove block\" onclick=\"" + Me + ".drop(this)\">" + Svg(Bin, 1.8) + "</button>"));
            }
            sb.Append("</header><div class=\"pg-bb\">");
            if (data)
            {
                sb.Append("<div class=\"pg-dinfo\">" + Ui.Enc(spec.Desc) + (spec.Fields == string.Empty ? " Nothing to write here: move it or hide it." : string.Empty) + "</div>");
            }
            string kick = Blocks.Has(spec, "kicker") ? CE("kicker", b.Kicker, data && b.Source == "contact" ? "Note box title, like New to us?" : "Small label above the title", "pg-ek", ro) : string.Empty;
            string title = Blocks.Has(spec, "title") ? CE("title", b.Title, "Title", "pg-et", ro) : string.Empty;
            string text = Blocks.Has(spec, "text") ? RichEd(b.Text, data && b.Source == "contact" ? "Text for the note box" : "Write here", ro) : string.Empty;
            string media = Blocks.Has(spec, "media") ? MediaEd(site, b.Media, b.Type == "quote" ? "photo" : string.Empty, b.Type == "quote" ? "Add a photo" : "Add a picture or video", ro) : string.Empty;
            string btns = Blocks.Has(spec, "buttons") ? "<div class=\"pg-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Main button", ro) + BtnEd("2", b.Label2, b.Link2, "Second button", ro) + "</div>" : Blocks.Has(spec, "button") ? "<div class=\"pg-ebtns\">" + BtnEd(string.Empty, b.Label, b.Link, "Button", ro) + "</div>" : Blocks.Has(spec, "label") ? "<div class=\"pg-ebtns\"><div class=\"pg-ebtn\"><span>Button text</span><input type=\"text\" data-f=\"label\" maxlength=\"40\" value=\"" + Ui.Attr(b.Label) + "\" placeholder=\"Book a stay\" aria-label=\"Button text\"" + (ro ? " readonly" : string.Empty) + "></div></div>" : string.Empty;
            string extra = string.Empty;
            if (Blocks.Has(spec, "extra"))
            {
                (string Key, string Name)[] ex = b.Type == "hero" ? Blocks.HeroExtras : Blocks.SplitExtras;
                extra = "<label class=\"pg-eopt\">Also show <select data-f=\"extra\"" + (ro ? " disabled" : string.Empty) + ">" + Ui.Opts(ex.Select(e => (e.Key, e.Name)), b.Extra) + "</select></label>";
            }
            if (Blocks.Has(spec, "flip"))
            {
                extra += "<label class=\"pg-chk\"><input type=\"checkbox\" data-f=\"flip\"" + (b.Flip ? " checked" : string.Empty) + (ro ? " disabled" : string.Empty) + " onchange=\"this.closest('.pg-bb').querySelector('.pg-two').classList.toggle('pg-flip',this.checked)\"> Picture on the right</label>";
            }
            switch (b.Type)
            {
                case "hero":
                    sb.Append("<div class=\"pg-two\"><div class=\"pg-stack\">" + kick + title + text + btns + extra + "</div>" + media + "</div>");
                    break;
                case "split":
                    sb.Append("<div class=\"pg-two" + (b.Flip ? " pg-flip" : string.Empty) + "\">" + media + "<div class=\"pg-stack\">" + kick + title + text + btns + "</div></div>" + extra);
                    break;
                case "quote":
                    sb.Append(media + CE("text", b.Text, "The quote", "pg-eq", ro, true) + CE("title", b.Title, "Who said it, for example Biscuit's owner", "pg-et", ro));
                    break;
                case "media":
                    sb.Append(media + CE("text", b.Text, "Caption (optional)", "pg-hint2", ro));
                    break;
                default:
                    sb.Append(kick + title + text + (media != string.Empty ? "<div class=\"pg-narrow\">" + media + "</div>" : string.Empty) + btns + extra);
                    break;
            }
            if (Blocks.Has(spec, "items"))
            {
                sb.Append("<div><div class=\"pg-eitems\" data-f=\"items\">");
                foreach (BlockItem it in b.Items)
                {
                    sb.Append(ItemEd(site, b, spec, it, ro));
                }
                sb.Append("</div>" + (ro ? string.Empty : "<button type=\"button\" class=\"pg-eadd pg-mt10\" onclick=\"" + Me + ".addItem(this)\">+ Add " + (b.Type switch { "steps" => "a step", "faq" => "a question", "boxes" => "a box", "cards" => "a card", "stats" => "a number", "people" => "a person", _ => "a line" }) + "</button><template>" + ItemEd(site, b, spec, new BlockItem(), false) + "</template>") + "</div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string Tabs(string cur)
        {
            return string.Concat(Blocks.Pages.Select(p => (p.Key, p.Name)).Concat(Extra.Select(e => (e.Key, e.Name))).Select(p => "<button type=\"button\" class=\"pg-chip" + (p.Key == cur ? " pg-act" : string.Empty) + (Extra.Any(e => e.Key == p.Key) ? " pg-chipx" : string.Empty) + "\" aria-pressed=\"" + (p.Key == cur ? "true" : "false") + "\" onclick=\"" + Me + ".tab('" + p.Key + "')\">" + Ui.Enc(p.Name) + "</button>"));
        }

        private static string MainHtml(Ctx c)
        {
            bool canE = c.Can(Key, 'E');
            return "<div class=\"pg-ptop\"><div><h1>Pages</h1><p>" + (canE ? "Click any text to change it. Drag a block by its handle, or use the arrows, to move it. Changes go live when you save. Questions and the vaccine chart for the Pet owners page have their own tabs." : "You can look at the pages but not change them.") + "</p></div></div>" + (canE ? "<p class=\"pg-tokens\"><b>Tip</b> Write <code>{name}</code>, <code>{town}</code> or <code>{phone}</code> in any text and the website shows the hospital's name, town or phone from Settings. For example: Call {phone} to book.</p>" : string.Empty) + "<div class=\"pg-ptools\"><div class=\"pg-ptabs\" id=\"pg-ptabs\">" + Tabs("home") + "</div><div class=\"pg-pacts\"><span class=\"pg-dirty\" id=\"pg-dirty\"></span><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" id=\"pg-verbtn\" onclick=\"" + Me + ".versions()\">Versions</button><button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" id=\"pg-viewlink\" data-href=\"Home\" onclick=\"" + Me + ".view()\">Open page</button>" + (canE ? "<button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-accent\" id=\"pg-savebtn\" onclick=\"" + Me + ".save()\">Save page</button>" : string.Empty) + "</div></div><div class=\"pg-canvas\" id=\"pg-canvas\"></div><div id=\"pg-holder\" hidden></div><div class=\"pg-rt\" id=\"pg-rt\" role=\"toolbar\" aria-label=\"Text style\"><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"" + Me + ".fmt('bold')\" title=\"Bold\"><b>B</b></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"" + Me + ".fmt('italic')\" title=\"Italic\"><i>I</i></button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"" + Me + ".fmt('link')\" title=\"Link\">Link</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"" + Me + ".fmt('insertUnorderedList')\" title=\"List\">&bull; List</button><button type=\"button\" onmousedown=\"event.preventDefault()\" onclick=\"" + Me + ".fmt('clear')\" title=\"Clear formatting\">Clear</button></div>";
        }

        private static string Canvas(SiteInfo site, PageDoc doc, bool canE)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Block b in doc.Blocks)
            {
                sb.Append(Ed(site, b, !canE));
            }
            if (canE)
            {
                sb.Append("<div class=\"pg-addp\" id=\"pg-addp\"><b>Add a block</b><div class=\"pg-addl\">" + string.Concat(Blocks.Types.Select(t => "<button type=\"button\" onclick=\"" + Me + ".addBlock('" + t.Key + "')\"><b>" + Ui.Enc(t.Name) + "</b><small>" + Ui.Enc(t.Desc) + "</small></button>")) + "</div><p class=\"pg-hint2 pg-mt10\">Blocks marked From your data (teams, vets, runs, forms and more) cannot be removed, but you can move them, change their titles or hide them.</p></div>");
            }
            return sb.ToString();
        }

        private static string Ctl(string fn)
        {
            return "<div class=\"pg-ictl\"><button type=\"button\" aria-label=\"Move up\" onclick=\"" + Me + "." + fn + "Move(this,-1)\">" + Svg(Up) + "</button><button type=\"button\" aria-label=\"Move down\" onclick=\"" + Me + "." + fn + "Move(this,1)\">" + Svg(Down) + "</button><button type=\"button\" aria-label=\"Remove\" onclick=\"" + Me + "." + fn + "Drop(this)\">" + Svg(Bin, 1.8) + "</button></div>";
        }

        private static string FaqRow(Faq f, bool ro)
        {
            string r = ro ? " readonly" : string.Empty;
            return "<div class=\"pg-frow\"><label class=\"pg-field\"><span>Question</span><input type=\"text\" data-q=\"q\" maxlength=\"120\" value=\"" + Ui.Attr(f.Q) + "\"" + r + "></label><label class=\"pg-field\"><span>Answer</span><textarea data-q=\"a\" rows=\"3\" maxlength=\"500\"" + r + ">" + Ui.Enc(f.A) + "</textarea></label>" + (ro ? string.Empty : Ctl("faq")) + "</div>";
        }

        private static string FaqCanvas(SiteInfo site, bool canE)
        {
            StringBuilder sb = new StringBuilder("<div class=\"pg-panel pg-xed\"><h2>Questions on the Pet owners page</h2><p class=\"pg-hint2\">Short questions and answers. Up to 20. They open one by one on the website.</p><div class=\"pg-flist\" id=\"pg-flist\">");
            foreach (Faq f in site.Faqs)
            {
                sb.Append(FaqRow(f, !canE));
            }
            sb.Append("</div>" + (site.Faqs.Count == 0 ? "<p class=\"pg-none\" id=\"pg-fnone\">No questions yet.</p>" : string.Empty));
            if (canE)
            {
                sb.Append("<button type=\"button\" class=\"pg-eadd pg-mt10\" onclick=\"" + Me + ".faqAdd()\">+ Add a question</button><template id=\"pg-ftpl\">" + FaqRow(new Faq(), false) + "</template>");
            }
            sb.Append("<em class=\"pg-err\" id=\"pg-e-x\" role=\"alert\"></em></div>");
            return sb.ToString();
        }

        private static string VaxRow(SiteInfo site, VaxPlan v, bool ro)
        {
            string r = ro ? " readonly" : string.Empty;
            string d = ro ? " disabled" : string.Empty;
            List<(string, string)> sp = site.SpeciesList.Select(s => (s.Key, s.Name)).ToList();
            if (v.Species != string.Empty && !sp.Any(x => x.Item1 == v.Species))
            {
                sp.Add((v.Species, v.Species));
            }
            return "<div class=\"pg-vxrow\"><label class=\"pg-field\"><span>Animal</span><select data-v=\"species\"" + d + ">" + Ui.Opts(sp, v.Species) + "</select></label><label class=\"pg-field\"><span>Vaccine</span><input type=\"text\" data-v=\"name\" maxlength=\"40\" value=\"" + Ui.Attr(v.Name) + "\"" + r + "></label><label class=\"pg-field\"><span>First doses</span><input type=\"text\" data-v=\"first\" maxlength=\"80\" value=\"" + Ui.Attr(v.First) + "\"" + r + "></label><label class=\"pg-field\"><span>Then</span><input type=\"text\" data-v=\"every\" maxlength=\"80\" value=\"" + Ui.Attr(v.Every) + "\"" + r + "></label><label class=\"pg-field\"><span>Remind after (months)</span><input type=\"number\" data-v=\"months\" min=\"1\" max=\"120\" value=\"" + v.Months.ToString(Ui.Inv) + "\"" + r + "></label><label class=\"pg-chk pg-vxcore\"><input type=\"checkbox\" data-v=\"core\"" + (v.Core ? " checked" : string.Empty) + d + "> Core vaccine</label>" + (ro ? string.Empty : Ctl("vax")) + "</div>";
        }

        private static string VaxCanvas(SiteInfo site, bool canE)
        {
            StringBuilder sb = new StringBuilder("<div class=\"pg-panel pg-xed\"><h2>Vaccine chart on the Pet owners page</h2><p class=\"pg-hint2\">One row per vaccine and animal. Core vaccines are shown first and marked as recommended for every pet. Up to 40 rows.</p><div class=\"pg-vxlist\" id=\"pg-vxlist\">");
            foreach (VaxPlan v in site.Settings.VaxChart)
            {
                sb.Append(VaxRow(site, v, !canE));
            }
            sb.Append("</div>");
            if (canE)
            {
                sb.Append("<button type=\"button\" class=\"pg-eadd pg-mt10\" onclick=\"" + Me + ".vaxAdd()\">+ Add a vaccine</button><template id=\"pg-vxtpl\">" + VaxRow(site, new VaxPlan { Species = site.SpeciesList.FirstOrDefault()?.Key ?? "dog", Months = 12, Core = false }, false) + "</template>");
            }
            sb.Append("<em class=\"pg-err\" id=\"pg-e-x\" role=\"alert\"></em></div>");
            return sb.ToString();
        }

        private async Task<string> SettingStamp(string key)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT SYSDTE FROM SETTING WHERE SKEY = @k", new List<SqlParameter> { App.P("k", key, 100) });
            return emsg == string.Empty && dt.Rows.Count > 0 ? Stamp(Convert.ToDateTime(dt.Rows[0][0])) : string.Empty;
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                return response;
            }
            string want = Val("argA", 20);
            bool canE = c.Can(Key, 'E');
            (string Key, string Name, string Href) x = Extra.FirstOrDefault(e => e.Key == want);
            if (x.Key != null)
            {
                SiteInfo fresh = await Site.Load(true);
                response.SetElementContents("pg-canvas", x.Key == "faqs" ? FaqCanvas(fresh, canE) : VaxCanvas(fresh, canE));
                response.SetElementContents("pg-ptabs", Tabs(x.Key));
                response.ExecuteScript("document.getElementById('pg-viewlink').setAttribute('data-href','" + x.Href + "');" + Me + ".shown('" + x.Key + "','" + await SettingStamp(x.Key) + "');");
                return response;
            }
            PageSpec p = Blocks.PageOf(want) ?? Blocks.Pages[0];
            SiteInfo site = await Site.Load();
            PageDoc doc = await Blocks.LoadDoc(p.Key);
            response.SetElementContents("pg-canvas", Canvas(site, doc, canE));
            response.SetElementContents("pg-ptabs", Tabs(p.Key));
            response.ExecuteScript("document.getElementById('pg-viewlink').setAttribute('data-href','" + p.Href + "');" + Me + ".shown('" + p.Key + "','" + Stamp(doc.Saved) + "');");
            return response;
        }

        public async Task<ApiResponse> AddBlock()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "add block", "You can look at the pages but not change them.");
            }
            PartSpec? t = Blocks.Types.FirstOrDefault(x => x.Key == Val("btype", 20));
            if (t == null)
            {
                return response;
            }
            SiteInfo site = await Site.Load();
            Block b = new Block { Id = Blocks.NewId(), Type = t.Key };
            if (t.Key is "cards" or "steps" or "boxes" or "faq" or "stats" or "people")
            {
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
                b.Items.Add(new BlockItem());
            }
            response.SetElementContents("pg-holder", Ed(site, b, false));
            response.ExecuteScript(Me + ".added();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "save page", "You can look at the pages but not change them.");
            }
            PageSpec? p = Blocks.PageOf(Val("argA", 20));
            if (p == null)
            {
                return await Refuse(response, c, "save page", "Reload the page and try again.");
            }
            SiteInfo site = await Site.Load(true);
            if (site.Partial)
            {
                response.ExecuteScript(Me + ".fail('The page could not be checked right now. Try again in a moment.');");
                return response;
            }
            (List<Block>? list, string e) = Blocks.Clean(site, p, GetDataValue("blocks") ?? string.Empty);
            if (list == null)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(e) + "');");
                return response;
            }
            string err = await Blocks.Save(c.U.StaffId, p, list, Base());
            if (err != string.Empty)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(err) + "');");
                return response;
            }
            await App.Log(c.U.StaffId, "save page", p.Name + " · " + Ui.Plural(list.Count, "block", "blocks"), true);
            PageDoc doc = await Blocks.LoadDoc(p.Key);
            response.SetElementContents("pg-canvas", Canvas(site, doc, true));
            response.ExecuteScript(Me + ".shown('" + p.Key + "','" + Stamp(doc.Saved) + "');" + Me + ".saved('" + Ui.Js(p.Name + " saved. It is live on the website.") + "');");
            return response;
        }

        public async Task<ApiResponse> Versions()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                return await Refuse(response, c, "page versions", "You don't have access to Pages.");
            }
            PageSpec? p = Blocks.PageOf(Val("argA", 20));
            if (p == null)
            {
                return Toast(response, "Questions and the vaccine chart do not keep versions. Use the page tabs to see page versions.");
            }
            PageDoc doc = await Blocks.LoadDoc(p.Key);
            List<PageVersion> list = await Blocks.Versions(p.Key);
            var (who, _) = await Helper.GetDataTable("SELECT STAFF_NAME FROM STAFF WHERE STAFF_ID = @s", new List<SqlParameter> { App.P("s", doc.By, 50) });
            string by = who.Rows.Count > 0 ? Convert.ToString(who.Rows[0][0]) ?? string.Empty : string.Empty;
            Dictionary<string, string> s = await App.Settings();
            bool canE = c.Can(Key, 'E');
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(p.Name) + ": versions</h2><p class=\"pg-hint pg-vhint\">The last " + Blocks.Keep.ToString(Ui.Inv) + " saved versions are kept. Restoring one makes it live right away.</p><div class=\"pg-vlist\"><div class=\"pg-vrow\"><div><b>Live now</b><small>" + (doc.Saved != default ? "Saved " + Ui.Time(doc.Saved, "full") + (by != string.Empty ? " by " + Ui.Enc(by) : string.Empty) : "Sample content") + " &middot; " + Ui.Plural(doc.Blocks.Count, "block", "blocks") + "</small></div></div>");
            if (list.Count == 0)
            {
                sb.Append("<p class=\"pg-hint\">No earlier versions yet. They appear here after you save.</p>");
            }
            foreach (PageVersion v in list)
            {
                string when = App.Local(s, v.When).ToString("MMM d, h:mm tt", Ui.Us);
                sb.Append("<div class=\"pg-vrow\"><div><b>" + Ui.Time(v.When, "full") + "</b><small>" + (v.ByName != string.Empty ? Ui.Enc(v.ByName) + " &middot; " : string.Empty) + Ui.Plural(v.Blocks.Count, "block", "blocks") + "</small></div>" + (canE ? "<button type=\"button\" class=\"pg-btn pg-btn-sm pg-btn-ghost\" onclick=\"" + Me + ".restore('" + Ui.Attr(Ui.Js(v.Id)) + "','" + Ui.Attr(Ui.Js(when)) + "')\">Restore</button>" : string.Empty) + "</div>");
            }
            sb.Append("</div><div class=\"pg-mfoot\"><button type=\"button\" class=\"pg-btn pg-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            response.SetElementContents("pg-mbody", sb.ToString());
            response.ExecuteScript(Me + ".openModal();");
            return response;
        }

        public async Task<ApiResponse> Restore()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "restore page", "You can look at the pages but not change them.");
            }
            PageSpec? p = Blocks.PageOf(Val("argA", 20));
            if (p == null)
            {
                return response;
            }
            string err = await Blocks.Restore(c.U.StaffId, p, Val("vi", 50), Base());
            if (err != string.Empty)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(err) + "');");
                return response;
            }
            await App.Log(c.U.StaffId, "restore page", p.Name, true);
            SiteInfo site = await Site.Load();
            PageDoc doc = await Blocks.LoadDoc(p.Key);
            response.SetElementContents("pg-canvas", Canvas(site, doc, true));
            response.ExecuteScript(Me + ".shown('" + p.Key + "','" + Stamp(doc.Saved) + "');" + Me + ".saved('Version restored. It is live on the website.');");
            return response;
        }

        private async Task<string> PutSetting(string key, string value, DateTime? stamp)
        {
            string r = await Helper.PutData(new List<string>
            {
                "IF NOT EXISTS (SELECT 1 FROM SETTING WHERE SKEY = @k) INSERT INTO SETTING (SKEY, SVALUE) VALUES (@k, N'[]')",
                "UPDATE SETTING SET SYSDTE = SYSDTE WHERE SKEY = @k",
                "IF @base IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SETTING WHERE SKEY = @k AND SYSDTE >= DATEADD(MILLISECOND, -5, @base) AND SYSDTE <= DATEADD(MILLISECOND, 5, @base)) THROW 50002, N'STATECHANGED', 1;",
                "UPDATE SETTING SET SVALUE = @v, SYSDTE = GETDATE() WHERE SKEY = @k"
            }, new List<SqlParameter> { App.P("k", key, 100), App.P("v", value, -1), App.PDate("base", stamp) });
            if (Helper.IsError(r))
            {
                return r.Contains("STATECHANGED", StringComparison.Ordinal) ? "Someone else changed this while you were working on it. Reload the page to see their changes, then make yours again." : "That could not be saved. Try again.";
            }
            Site.Forget();
            return string.Empty;
        }

        private static List<Dictionary<string, JsonElement>>? Rows(string? json)
        {
            if (json == null || json.Length > 40000)
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string Str(Dictionary<string, JsonElement> d, string k)
        {
            return d.TryGetValue(k, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
        }

        public async Task<ApiResponse> SaveFaqs()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "save questions", "You can look at the pages but not change them.");
            }
            List<Dictionary<string, JsonElement>>? rows = Rows(GetDataValue("xrows"));
            if (rows == null)
            {
                response.SetElementContents("pg-e-x", "The list could not be read, so nothing was saved. Reload the page and try again.");
                response.ExecuteScript(Me + ".fail('');");
                return response;
            }
            List<Faq> list = new List<Faq>();
            int n = 0;
            foreach (Dictionary<string, JsonElement> r in rows)
            {
                n++;
                string q = Ui.OneLine(Str(r, "q"), 400);
                string a = Blocks.Plain(Str(r, "a"), 2000);
                if (q == string.Empty && a == string.Empty)
                {
                    continue;
                }
                string err = q == string.Empty ? "Question " + n + " has an answer but no question." : a == string.Empty ? "Question " + n + " needs an answer." : q.Length > 120 ? "Question " + n + " is too long. Keep it under 120 characters." : a.Length > 500 ? "The answer to question " + n + " is too long. Keep it under 500 characters." : string.Empty;
                if (err != string.Empty)
                {
                    response.SetElementContents("pg-e-x", Ui.Enc(err));
                    response.ExecuteScript(Me + ".fail('');");
                    return response;
                }
                list.Add(new Faq { Q = q, A = a });
            }
            if (list.Count > 20)
            {
                response.SetElementContents("pg-e-x", "Keep it to 20 questions or fewer.");
                response.ExecuteScript(Me + ".fail('');");
                return response;
            }
            string e = await PutSetting("faqs", Site.FaqJson(list), Base());
            if (e != string.Empty)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(e) + "');");
                return response;
            }
            await App.Log(c.U.StaffId, "save questions", Ui.Plural(list.Count, "question", "questions"), true);
            SiteInfo site = await Site.Load(true);
            response.SetElementContents("pg-canvas", FaqCanvas(site, true));
            response.ExecuteScript(Me + ".shown('faqs','" + await SettingStamp("faqs") + "');" + Me + ".saved('Questions saved. They are live on the website.');");
            return response;
        }

        public async Task<ApiResponse> SaveVax()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "save vaccine chart", "You can look at the pages but not change them.");
            }
            SiteInfo site = await Site.Load(true);
            if (site.Partial || site.SpeciesList.Count == 0)
            {
                response.ExecuteScript(Me + ".fail('The vaccine chart could not be checked right now. Try again in a moment.');");
                return response;
            }
            List<Dictionary<string, JsonElement>>? rows = Rows(GetDataValue("xrows"));
            if (rows == null)
            {
                response.SetElementContents("pg-e-x", "The list could not be read, so nothing was saved. Reload the page and try again.");
                response.ExecuteScript(Me + ".fail('');");
                return response;
            }
            List<VaxPlan> list = new List<VaxPlan>();
            int n = 0;
            foreach (Dictionary<string, JsonElement> r in rows)
            {
                n++;
                string name = Ui.OneLine(Str(r, "name"), 200);
                string first = Ui.OneLine(Str(r, "first"), 200);
                string every = Ui.OneLine(Str(r, "every"), 200);
                string sp = Str(r, "species");
                if (name == string.Empty && first == string.Empty && every == string.Empty)
                {
                    continue;
                }
                int months = int.TryParse(Str(r, "months"), out int m) ? m : 0;
                string err = name == string.Empty ? "Row " + n + " needs the vaccine's name." : name.Length > 40 ? "The vaccine name in row " + n + " is too long." : first.Length > 80 || every.Length > 80 ? "Row " + n + " has text that is too long. Keep each box under 80 characters." : !site.SpeciesList.Any(x => x.Key == sp) ? "Choose the animal in row " + n + "." : months < 1 || months > 120 ? "In row " + n + ", the reminder must be between 1 and 120 months." : string.Empty;
                if (err != string.Empty)
                {
                    response.SetElementContents("pg-e-x", Ui.Enc(err));
                    response.ExecuteScript(Me + ".fail('');");
                    return response;
                }
                list.Add(new VaxPlan { Species = sp, Name = name, First = first, Every = every, Months = months, Core = Str(r, "core") == "1" });
            }
            if (list.Count > 40)
            {
                response.SetElementContents("pg-e-x", "Keep it to 40 rows or fewer.");
                response.ExecuteScript(Me + ".fail('');");
                return response;
            }
            string e = await PutSetting("vaxchart", Site.VaxJson(list), Base());
            if (e != string.Empty)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(e) + "');");
                return response;
            }
            await App.Log(c.U.StaffId, "save vaccine chart", Ui.Plural(list.Count, "row", "rows"), true);
            site = await Site.Load(true);
            response.SetElementContents("pg-canvas", VaxCanvas(site, true));
            response.ExecuteScript(Me + ".shown('vaxchart','" + await SettingStamp("vaxchart") + "');" + Me + ".saved('Vaccine chart saved. It is live on the website.');");
            return response;
        }
    }
}
