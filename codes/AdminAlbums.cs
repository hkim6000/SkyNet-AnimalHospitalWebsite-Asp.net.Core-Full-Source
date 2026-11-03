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
    public class AdminAlbums : WebPage
    {
        private const string Key = "albums";
        private const string Me = "AdminAlbumsJs";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Albums | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--aa-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("aa"));
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
            await App.Log(c.U.StaffId, action, "albums", false);
            response.ExecuteScript(Me + ".unlock();");
            return Toast(response, message);
        }

        private string Val(string k, int max)
        {
            return Ui.Clip(GetDataValue(k) ?? string.Empty, max);
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
            response.SetElementContents("aa-menu", Auth.Menu("aa", Me, "AdminAlbums", c));
            response.SetElementContents("aa-who", Auth.Who("aa", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("aa-main", "<div class=\"aa-ptop\"><div><h1>Albums</h1></div></div><div class=\"aa-panel\"><p>You don't have access to this page. Ask the hospital manager if you need it.</p></div>");
                return response;
            }
            response.SetElementContents("aa-main", "<div class=\"aa-ptop\"><div><h1>Albums</h1><p>Photo albums on the Media page of the website. Each album picks its photos from the media library. Deleting an album keeps the photos.</p></div><div class=\"aa-acts\">" + (c.Can("media", 'V') ? "<a class=\"aa-btn aa-btn-ghost\" href=\"AdminMedia\">Media library</a>" : string.Empty) + (c.Can(Key, 'A') ? "<button type=\"button\" class=\"aa-btn aa-btn-accent\" onclick=\"" + Me + ".edit('')\">New album</button>" : string.Empty) + "<a class=\"aa-btn aa-btn-ghost\" href=\"Media\" target=\"_blank\" rel=\"noopener\">View on site</a></div></div><div class=\"aa-albs\" id=\"aa-list\"></div>");
            response.ExecuteScript(Me + ".loaded();");
            return response;
        }

        private static string PickItem(AaMedia m)
        {
            bool vid = m.Kind == "video";
            List<string> tags = Site.Csv(m.Tags);
            return "<button type=\"button\" class=\"aa-pki\" data-src=\"" + Ui.Attr(m.FullPath) + "\" data-thumb=\"" + Ui.Attr(m.ThumbPath) + "\" data-kind=\"" + Ui.Attr(m.Kind) + "\" data-title=\"" + Ui.Attr(m.Title) + "\" onclick=\"" + Me + ".pkToggle(this)\" title=\"" + Ui.Attr(m.Title + (tags.Count > 0 ? " · " + string.Join(", ", tags) : string.Empty)) + "\"><img src=\"" + Ui.Attr(m.ThumbPath) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<b class=\"aa-vbadge\">" + (m.Seconds / 60).ToString(Ui.Inv) + ":" + (m.Seconds % 60).ToString("00", Ui.Inv) + "</b>" : string.Empty) + "<span>" + Ui.Enc(m.Title) + "</span><i>&#10003;</i></button>";
        }

        public async Task<ApiResponse> MediaPick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E') && !c.Can(Key, 'A'))
            {
                return await Refuse(response, c, "media picker", "You can look at albums but not change them.");
            }
            string allow = Val("pallow", 10) is "photo" or "video" or "doc" ? Val("pallow", 10) : string.Empty;
            string kind = allow != string.Empty ? allow : Val("pkind", 10) is "photo" or "video" or "doc" ? Val("pkind", 10) : string.Empty;
            string term = Val("pterm", 60).Trim().ToLowerInvariant();
            string tag = Val("ptag", 40);
            List<string> ctx = Val("pctx", 80).ToLowerInvariant().Split(',').Select(x => x.Trim()).Where(x => x != string.Empty).ToList();
            int page = int.TryParse(GetDataValue("ppg"), out int pv) ? Math.Clamp(pv, 1, 200) : 1;
            const int size = 30;
            var (rows, emsg) = await Helper.GetListT<AaMedia>("SELECT MEDIA_ID AS MediaId, KIND AS Kind, TITLE AS Title, ALT AS Alt, TAGS AS Tags, FULL_PATH AS FullPath, THUMB_PATH AS ThumbPath, SECONDS AS Seconds, ADDED_UTC AS AddedUtc FROM MEDIA ORDER BY ADDED_UTC DESC, MEDIA_ID");
            if (emsg != string.Empty || rows == null)
            {
                return Toast(response, "The media library could not be loaded. Try again.");
            }
            List<AaMedia> all = rows.Where(m => (kind == string.Empty ? m.Kind != "doc" : m.Kind == kind) && (tag == string.Empty || Site.Csv(m.Tags).Contains(tag)) && (term == string.Empty || (m.Title + " " + m.Alt + " " + m.Tags).ToLowerInvariant().Contains(term))).OrderByDescending(m => Site.Csv(m.Tags).Count(t => ctx.Contains(t))).ThenByDescending(m => m.AddedUtc).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (AaMedia m in all.Skip((page - 1) * size).Take(size))
            {
                sb.Append(PickItem(m));
            }
            if (all.Count == 0)
            {
                sb.Append("<p class=\"aa-none aa-pkall\">Nothing matches. Upload a photo here or in the media library.</p>");
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
                await App.Log(c.U.StaffId, "upload photo", "albums", false);
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

        private static async Task<Dictionary<string, AaMedia>> MediaMap(IEnumerable<string> srcs)
        {
            List<string> list = srcs.Distinct().ToList();
            Dictionary<string, AaMedia> d = new Dictionary<string, AaMedia>();
            if (list.Count == 0)
            {
                return d;
            }
            List<SqlParameter> ps = new List<SqlParameter>();
            List<string> names = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                names.Add("@m" + i);
                ps.Add(App.P("m" + i, list[i], 300));
            }
            var (rows, emsg) = await Helper.GetListT<AaMedia>("SELECT MEDIA_ID AS MediaId, KIND AS Kind, TITLE AS Title, ALT AS Alt, TAGS AS Tags, FULL_PATH AS FullPath, THUMB_PATH AS ThumbPath, SECONDS AS Seconds, ADDED_UTC AS AddedUtc FROM MEDIA WHERE FULL_PATH IN (" + string.Join(", ", names) + ")", ps);
            foreach (AaMedia m in emsg == string.Empty && rows != null ? rows : new List<AaMedia>())
            {
                d[m.FullPath] = m;
            }
            return d;
        }

        private static string ThumbOf(Dictionary<string, AaMedia> map, string src)
        {
            return map.TryGetValue(src, out AaMedia? m) && m.ThumbPath != string.Empty ? m.ThumbPath : Blocks.Thumb(src);
        }

        public async Task<ApiResponse> List()
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
            var (albums, emsg) = await Helper.GetListT<AaAlbum>("SELECT A.ALBUM_ID AS AlbumId, A.TITLE AS Title, A.TEXT AS Text, A.COVER AS Cover, A.ALBUM_DATE AS AlbumDate, A.PUBLISHED AS Published, (SELECT COUNT(*) FROM ALBUMITEM I WHERE I.ALBUM_ID = A.ALBUM_ID) AS Items, ISNULL((SELECT TOP 1 I.FULL_PATH FROM ALBUMITEM I WHERE I.ALBUM_ID = A.ALBUM_ID ORDER BY I.ITEM_ORDER), N'') AS FirstPath FROM ALBUM A ORDER BY A.ALBUM_DATE DESC, A.TITLE");
            if (emsg != string.Empty || albums == null)
            {
                return Toast(response, "The albums could not be loaded. Try again.");
            }
            Dictionary<string, AaMedia> map = await MediaMap(albums.Select(a => a.Cover != string.Empty ? a.Cover : a.FirstPath).Where(x => x != string.Empty));
            StringBuilder sb = new StringBuilder();
            if (albums.Count == 0)
            {
                sb.Append("<p class=\"aa-none\">No albums yet.</p>");
            }
            foreach (AaAlbum a in albums)
            {
                string cov = a.Cover != string.Empty ? a.Cover : a.FirstPath;
                sb.Append("<button type=\"button\" class=\"aa-albc\" onclick=\"" + Me + ".edit('" + Ui.Attr(Ui.Js(a.AlbumId)) + "')\"><span>" + (cov != string.Empty ? "<img src=\"" + Ui.Attr(ThumbOf(map, cov)) + "\" alt=\"\" loading=\"lazy\">" : string.Empty) + "<em>" + Ui.Plural(a.Items, "photo", "photos") + "</em></span><span><b>" + Ui.Enc(a.Title) + "</b><small>" + Ui.Day(a.AlbumDate) + "</small><span><span class=\"aa-st aa-st-" + (a.Published ? "live\">Published" : "off\">Hidden") + "</span></span></span></button>");
            }
            response.SetElementContents("aa-list", sb.ToString());
            return response;
        }

        private static async Task<AaAlbum?> One(string id)
        {
            var (a, emsg) = await Helper.GetOneT<AaAlbum>("SELECT A.ALBUM_ID AS AlbumId, A.TITLE AS Title, A.TEXT AS Text, A.COVER AS Cover, A.ALBUM_DATE AS AlbumDate, A.PUBLISHED AS Published FROM ALBUM A WHERE A.ALBUM_ID = @id", new List<SqlParameter> { App.P("id", id, 50) });
            return emsg == string.Empty ? a : null;
        }

        private static async Task<List<string>> ItemsOf(string id)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT FULL_PATH FROM ALBUMITEM WHERE ALBUM_ID = @id ORDER BY ITEM_ORDER, FULL_PATH", new List<SqlParameter> { App.P("id", id, 50) });
            return emsg == string.Empty ? dt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]) ?? string.Empty).ToList() : new List<string>();
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                return await Refuse(response, c, "album form", "You don't have access to albums.");
            }
            string id = Val("alid", 50);
            AaAlbum? a = id != string.Empty ? await One(id) : null;
            if (id != string.Empty && a == null)
            {
                response.ExecuteScript(Me + ".list(1);");
                return Toast(response, "That album no longer exists.");
            }
            if (a == null && !c.Can(Key, 'A'))
            {
                return await Refuse(response, c, "new album", "Adding albums needs the Add right on Albums.");
            }
            Dictionary<string, string> s = await App.Settings();
            AaAlbum v = a ?? new AaAlbum { AlbumId = App.NewId(), AlbumDate = App.Now(s).Date, Published = true };
            List<string> items = a != null ? await ItemsOf(a.AlbumId) : new List<string>();
            Dictionary<string, AaMedia> map = await MediaMap(items);
            bool canE = a == null ? c.Can(Key, 'A') : c.Can(Key, 'E');
            string ro = canE ? string.Empty : " readonly";
            var list = items.Select(src => new { src, thumb = ThumbOf(map, src), title = map.TryGetValue(src, out AaMedia? m) ? m.Title : string.Empty }).ToList();
            StringBuilder sb = new StringBuilder("<h2>" + (a == null ? "New album" : Ui.Enc(a.Title)) + "</h2><div class=\"aa-form\"><div class=\"aa-f2\"><label class=\"aa-field\"><span>Title</span><input type=\"text\" id=\"aa-altitle\" maxlength=\"60\" value=\"" + Ui.Attr(v.Title) + "\"" + ro + "></label><label class=\"aa-field\"><span>Date</span><input type=\"date\" id=\"aa-aldate\" value=\"" + Ui.Ymd(v.AlbumDate) + "\"" + ro + "></label></div><label class=\"aa-field\"><span>Short line</span><input type=\"text\" id=\"aa-altext\" maxlength=\"160\" value=\"" + Ui.Attr(v.Text) + "\"" + ro + "></label><label class=\"aa-chk\"><input type=\"checkbox\" id=\"aa-alpub\"" + (v.Published ? " checked" : string.Empty) + (canE ? string.Empty : " disabled") + "> Show on the website</label><div class=\"aa-ph\"><h3 class=\"aa-phh\">Photos &middot; <span id=\"aa-acount\"></span></h3>" + (canE ? "<button type=\"button\" class=\"aa-btn aa-btn-sm\" onclick=\"" + Me + ".addPhotos()\">Add photos</button>" : string.Empty) + "</div><div class=\"aa-agrid\" id=\"aa-aitems\"></div><em class=\"aa-err\" id=\"aa-e-album\" role=\"alert\"></em></div><div class=\"aa-mfoot\">");
            if (a != null && c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-danger aa-left\" onclick=\"" + Me + ".drop('" + Ui.Attr(Ui.Js(a.AlbumId)) + "','" + Ui.Attr(Ui.Js(a.Title)) + "')\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"aa-btn aa-btn-ghost\" onclick=\"" + Me + ".closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"aa-btn aa-btn-accent\" id=\"aa-savebtn\" onclick=\"" + Me + ".save('" + Ui.Attr(Ui.Js(v.AlbumId)) + "'," + (a == null ? "1" : "0") + ")\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("aa-mbody", sb.ToString());
            response.ExecuteScript(Me + ".setup(" + JsonSerializer.Serialize(list).Replace("<", "\\u003c") + ",'" + Ui.Js(v.Cover) + "'," + (canE ? "true" : "false") + ");" + Me + ".openModal();");
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
            bool isNew = Val("alnew", 1) == "1";
            if (!c.Can(Key, isNew ? 'A' : 'E'))
            {
                return await Refuse(response, c, isNew ? "add album" : "edit album", isNew ? "Adding albums needs the Add right on Albums." : "You can look at albums but not change them.");
            }
            string id = Val("alid", 50);
            if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,50}$"))
            {
                return await Refuse(response, c, "album", "Reload the page and try again.");
            }
            string title = Ui.OneLine(GetDataValue("altitle"), 60);
            string text = Ui.OneLine(GetDataValue("altext"), 160);
            DateTime? day = Ui.ParseDay(Val("aldate", 10));
            bool pub = Val("alpub", 1) == "1";
            string rawItems = GetDataValue("alitems") ?? string.Empty;
            List<string>? parsed = null;
            if (rawItems.Length <= 60000)
            {
                try
                {
                    parsed = JsonSerializer.Deserialize<List<string>>(rawItems);
                }
                catch (JsonException)
                {
                    parsed = null;
                }
            }
            if (parsed == null)
            {
                response.SetElementContents("aa-e-album", "The photo list could not be read. Close the album and try again.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            List<string> srcs = parsed;
            srcs = srcs.Where(x => x != null).Select(x => x.Trim()).Where(x => x.Length > 0 && x.Length <= 300).Distinct().Take(200).ToList();
            Dictionary<string, AaMedia> map = await MediaMap(srcs);
            List<string> ok = srcs.Where(x => map.TryGetValue(x, out AaMedia? m) && m.Kind == "photo").ToList();
            string err = title.Length < 2 ? "Enter the album's title." : day == null ? "Choose the album's date." : ok.Count == 0 && pub ? "Add at least one photo, or untick Show on the website." : string.Empty;
            if (err != string.Empty)
            {
                response.SetElementContents("aa-e-album", err);
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            string cover = ok.Contains(Val("alcover", 300)) ? Val("alcover", 300) : string.Empty;
            List<string> sql = new List<string>();
            List<SqlParameter> ps = new List<SqlParameter> { App.P("id", id, 50), App.P("title", title, 120), App.P("text", text, 600), App.P("cover", cover, 300), App.PDate("day", day), App.PBit("pub", pub), App.P("by", c.U.StaffId, 50) };
            if (isNew)
            {
                sql.Add("IF EXISTS (SELECT 1 FROM ALBUM WHERE ALBUM_ID = @id) THROW 50003, N'ALBUMEXISTS', 1; INSERT INTO ALBUM (ALBUM_ID, TITLE, TEXT, COVER, ALBUM_DATE, PUBLISHED, ADDED_BY) VALUES (@id, @title, @text, @cover, CAST(@day AS DATE), @pub, @by);");
            }
            else
            {
                sql.Add("UPDATE ALBUM SET TITLE = @title, TEXT = @text, COVER = @cover, ALBUM_DATE = CAST(@day AS DATE), PUBLISHED = @pub, SYSDTE = GETDATE() WHERE ALBUM_ID = @id; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;");
            }
            sql.Add("DELETE FROM ALBUMITEM WHERE ALBUM_ID = @id");
            for (int i = 0; i < ok.Count; i++)
            {
                sql.Add("INSERT INTO ALBUMITEM (ALBUM_ID, FULL_PATH, ITEM_ORDER) VALUES (@id, @s" + i + ", " + (i + 1) + ")");
                ps.Add(App.P("s" + i, ok[i], 300));
            }
            string res = await Helper.PutData(sql, ps);
            if (Helper.IsError(res) && res.Contains("ALBUMEXISTS", StringComparison.Ordinal))
            {
                await App.Log(c.U.StaffId, "add album", "album already exists", false);
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(1);");
                return Toast(response, "This album is already saved. Open it from the list to change it.");
            }
            if (Helper.IsError(res))
            {
                response.SetElementContents("aa-e-album", res.Contains("STATECHANGED", StringComparison.Ordinal) ? "This album was deleted by someone else. Close and reload." : "The album could not be saved. Try again.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            Site.Forget();
            await App.Log(c.U.StaffId, isNew ? "add album" : "edit album", title + " · " + Ui.Plural(ok.Count, "photo", "photos") + (pub ? string.Empty : " · hidden"), true);
            response.ExecuteScript(Me + ".closeModal();" + Me + ".list(1);" + Me + ".toast('Album saved.');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                return await Refuse(response, c, "delete album", "Deleting albums needs the Delete right on Albums.");
            }
            string id = Val("alid", 50);
            AaAlbum? a = await One(id);
            if (a == null)
            {
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(1);");
                return response;
            }
            string res = await Helper.PutData(new List<string> { "DELETE FROM ALBUMITEM WHERE ALBUM_ID = @id", "DELETE FROM ALBUM WHERE ALBUM_ID = @id" }, new List<SqlParameter> { App.P("id", id, 50) });
            if (Helper.IsError(res))
            {
                return Toast(response, "The album could not be deleted. Try again.");
            }
            Site.Forget();
            await App.Log(c.U.StaffId, "delete album", a.Title, true);
            response.ExecuteScript(Me + ".closeModal();" + Me + ".list(1);" + Me + ".toast('Album deleted. The photos stay in the media library.');");
            return response;
        }
    }
}
