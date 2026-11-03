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
    public class AdminMedia : WebPage
    {
        private const string Key = "media";
        private const string Me = "AdminMediaJs";
        private const int Size = 24;
        private const int ChunkSize = 2 * 1024 * 1024;
        private const long MaxDoc = 20L * 1024 * 1024;
        private const string Cols = "M.MEDIA_ID AS MediaId, M.KIND AS Kind, M.TITLE AS Title, M.ALT AS Alt, M.TAGS AS Tags, M.FULL_PATH AS FullPath, M.THUMB_PATH AS ThumbPath, M.SECONDS AS Seconds, M.PAGES AS Pages, M.WIDTH AS Width, M.HEIGHT AS Height, M.BYTES AS Bytes, M.ADDED_BY AS AddedBy, ISNULL(S.STAFF_NAME, N'') AS AddedName, M.ADDED_UTC AS AddedUtc";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Media library | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--md-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("md"));
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

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string message, bool upload = false)
        {
            await App.Log(c.U.StaffId, action, "media library", false);
            if (upload)
            {
                response.ExecuteScript(Me + ".fail('" + Ui.Js(message) + "');");
                return response;
            }
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

        private static async Task<long> MaxVideo()
        {
            Dictionary<string, string> s = await App.Settings();
            return Math.Clamp(App.SInt(s, "video.mb", 25), 1, 200) * 1048576L;
        }

        private static string Clock(int s)
        {
            return (s / 60).ToString(Ui.Inv) + ":" + (s % 60).ToString("00", Ui.Inv);
        }

        private static string Bytes(long b)
        {
            return b <= 0 ? string.Empty : b < 1048576 ? Math.Max(1, b / 1024).ToString(Ui.Inv) + " KB" : (b / 1048576.0).ToString(b < 10485760 ? "0.0" : "0", Ui.Inv) + " MB";
        }

        private static List<string> Tags(string? v)
        {
            return (v ?? string.Empty).Split(',').Select(t => Regex.Replace(t.Trim().ToLowerInvariant(), "[^a-z0-9 -]", string.Empty).Trim()).Where(t => t.Length > 0).Select(t => t.Length > 24 ? t.Substring(0, 24) : t).Distinct().Take(8).ToList();
        }

        private static string Clean(string? v, int max)
        {
            return Regex.Replace(Ui.Clip(v, max), "\\s+", " ").Trim();
        }

        private static string KindName(string kind)
        {
            return kind == "video" ? "Video" : kind == "doc" ? "PDF" : "Photo";
        }

        private static string DataList(string id, IEnumerable<string> values)
        {
            StringBuilder sb = new StringBuilder("<datalist id=\"" + id + "\">");
            foreach (string v in values.Where(x => x != string.Empty).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(400))
            {
                sb.Append("<option value=\"" + Ui.Attr(v) + "\"></option>");
            }
            sb.Append("</datalist>");
            return sb.ToString();
        }

        private static async Task<List<MdItem>> All()
        {
            var (list, emsg) = await Helper.GetListT<MdItem>("SELECT " + Cols + " FROM MEDIA M LEFT JOIN STAFF S ON S.STAFF_ID = M.ADDED_BY ORDER BY M.ADDED_UTC DESC, M.TITLE");
            return emsg == string.Empty && list != null ? list : new List<MdItem>();
        }

        private static async Task<MdItem?> One(string id)
        {
            var (m, emsg) = await Helper.GetOneT<MdItem>("SELECT " + Cols + " FROM MEDIA M LEFT JOIN STAFF S ON S.STAFF_ID = M.ADDED_BY WHERE M.MEDIA_ID = @id", new List<SqlParameter> { App.P("id", id, 50) });
            return emsg == string.Empty ? m : null;
        }

        private static async Task<Dictionary<string, int>> UseCounts(List<MdItem> items)
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.Ordinal);
            void Add(string src, int n = 1)
            {
                if (src != string.Empty)
                {
                    d[src] = (d.TryGetValue(src, out int x) ? x : 0) + n;
                }
            }
            var (dt, emsg) = await Helper.GetDataTable("SELECT IMAGE AS SRC FROM TEAM WHERE IMAGE <> N'' UNION ALL SELECT IMAGE FROM BOOKTYPE WHERE IMAGE <> N'' UNION ALL SELECT PHOTO FROM STAFF WHERE PHOTO <> N'' UNION ALL SELECT IMAGE FROM ROOMTYPE WHERE IMAGE <> N'' UNION ALL SELECT PHOTO FROM PET WHERE PHOTO <> N'' UNION ALL SELECT DISTINCT A.ALBUM_ID + N'|' + I.FULL_PATH FROM ALBUMITEM I JOIN ALBUM A ON A.ALBUM_ID = I.ALBUM_ID UNION ALL SELECT ALBUM_ID + N'|' + COVER FROM ALBUM WHERE COVER <> N''");
            if (emsg == string.Empty)
            {
                HashSet<string> albumPairs = new HashSet<string>(StringComparer.Ordinal);
                foreach (DataRow r in dt.Rows)
                {
                    string v = Convert.ToString(r[0]) ?? string.Empty;
                    int bar = v.IndexOf('|');
                    if (bar >= 0)
                    {
                        if (albumPairs.Add(v))
                        {
                            Add(v.Substring(bar + 1));
                        }
                    }
                    else
                    {
                        Add(v);
                    }
                }
            }
            var (pdt, perr) = await Helper.GetDataTable("SELECT BLOCKS FROM PAGEDOC");
            if (perr == string.Empty)
            {
                foreach (DataRow r in pdt.Rows)
                {
                    List<Block> blocks = Blocks.Parse(Convert.ToString(r[0]));
                    foreach (string src in blocks.SelectMany(b => new[] { b.Media }.Concat(b.Items.Select(i => i.Media))).Where(x => x != string.Empty).Distinct())
                    {
                        Add(src);
                    }
                }
            }
            foreach (MdItem m in items)
            {
                List<string> tags = Site.Csv(m.Tags);
                if ((m.Kind == "video" || m.Kind == "doc") && tags.Contains("media-center"))
                {
                    Add(m.FullPath);
                }
                if (m.Kind == "doc" && m.Title.Contains("surgery", StringComparison.OrdinalIgnoreCase))
                {
                    Add(m.FullPath);
                }
                if (m.Kind == "doc" && m.Title.Contains("vaccine", StringComparison.OrdinalIgnoreCase))
                {
                    Add(m.FullPath);
                }
            }
            return d;
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            Stale();
            response.SetElementContents("md-menu", Auth.Menu("md", Me, "AdminMedia", c));
            response.SetElementContents("md-who", Auth.Who("md", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("md-main", "<div class=\"md-ptop\"><div><h1>Media library</h1></div></div><div class=\"md-panel\"><p>You don't have access to this page. Ask the hospital manager if you need it.</p></div>");
                return response;
            }
            List<MdItem> items = await All();
            var (adt, aerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ALBUM");
            int albums = aerr == string.Empty ? Convert.ToInt32(adt.Rows[0][0]) : 0;
            long maxV = await MaxVideo();
            List<string> allTags = items.SelectMany(m => Site.Csv(m.Tags)).Distinct().OrderBy(t => t).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"md-ptop\"><div><h1>Media library</h1><p>Photos, videos and PDF documents for the website. Teams, booking types, staff, boarding runs, pets, albums and Pages all pick their pictures from here.</p></div><div class=\"md-acts\">" + (c.Can("albums", 'V') ? "<a class=\"md-btn md-btn-ghost\" href=\"AdminAlbums\">Albums (" + albums.ToString(Ui.Inv) + ")</a>" : string.Empty) + "</div></div>");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<div class=\"md-ups\">");
                sb.Append("<details class=\"md-panel md-col\"><summary><b>Upload a photo</b><span>JPG, PNG or WebP &middot; made 1600 px wide</span></summary><div class=\"md-colb\"><div class=\"md-up\"><label class=\"md-drop\" id=\"md-pprev\" for=\"md-pfile\"><span>Choose a photo<br>JPG, PNG or WebP</span><input type=\"file\" id=\"md-pfile\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"" + Me + ".ppick(this)\"></label><div class=\"md-form\"><label class=\"md-field\"><span>Name</span><input type=\"text\" id=\"md-ptitle\" maxlength=\"80\" placeholder=\"Waiting room, morning\"></label><label class=\"md-field\"><span>Description for screen readers</span><input type=\"text\" id=\"md-palt\" maxlength=\"160\" placeholder=\"A calm waiting room with a dog bed by the window\"></label><label class=\"md-field\"><span>Tags, separated by commas</span><input type=\"text\" id=\"md-ptags\" maxlength=\"200\" list=\"md-taglist\" placeholder=\"hospital, boarding\"></label><em class=\"md-err\" id=\"md-e-pfile\" role=\"alert\"></em><div><button type=\"button\" class=\"md-btn md-btn-accent\" id=\"md-pbtn\" onclick=\"" + Me + ".pupload()\">Upload photo</button></div></div></div></div></details>");
                sb.Append("<details class=\"md-panel md-col\"><summary><b>Upload a video</b><span>MP4 &middot; up to " + (maxV / 1048576).ToString(Ui.Inv) + " MB</span></summary><div class=\"md-colb\"><div class=\"md-up\"><label class=\"md-drop\" id=\"md-vprev\" for=\"md-vfile\"><span>Choose a video<br>MP4</span><input type=\"file\" id=\"md-vfile\" accept=\"video/mp4,.mp4\" data-max=\"" + maxV.ToString(Ui.Inv) + "\" onchange=\"" + Me + ".vpick(this)\"></label><div class=\"md-form\"><label class=\"md-field\"><span>Name</span><input type=\"text\" id=\"md-vtitle\" maxlength=\"80\" placeholder=\"A walk through the hospital\"></label><label class=\"md-field\"><span>Tags, separated by commas</span><input type=\"text\" id=\"md-vtags\" maxlength=\"200\" list=\"md-taglist\" placeholder=\"media-center\"></label><em class=\"md-err\" id=\"md-e-vfile\" role=\"alert\"></em><p class=\"md-hint\">Sent in small parts of 2 MB. The cover picture is taken from the first second. Tag it media-center to show it on the Media page.</p><div class=\"md-prog\" id=\"md-vprog\"><i id=\"md-vbar\"></i></div><div><button type=\"button\" class=\"md-btn md-btn-accent\" id=\"md-vbtn\" onclick=\"" + Me + ".vupload()\">Upload video</button></div></div></div></div></details>");
                sb.Append("<details class=\"md-panel md-col\"><summary><b>Upload a PDF</b><span>PDF &middot; up to " + (MaxDoc / 1048576).ToString(Ui.Inv) + " MB</span></summary><div class=\"md-colb\"><div class=\"md-up\"><label class=\"md-drop md-dropd\" id=\"md-dprev\" for=\"md-dfile\"><span>Choose a PDF</span><input type=\"file\" id=\"md-dfile\" accept=\"application/pdf,.pdf\" data-max=\"" + MaxDoc.ToString(Ui.Inv) + "\" onchange=\"" + Me + ".dpick(this)\"></label><div class=\"md-form\"><label class=\"md-field\"><span>Name</span><input type=\"text\" id=\"md-dtitle\" maxlength=\"80\" placeholder=\"Puppy care guide\"></label><label class=\"md-field\"><span>Tags, separated by commas</span><input type=\"text\" id=\"md-dtags\" maxlength=\"200\" list=\"md-taglist\" placeholder=\"media-center, forms\"></label><div class=\"md-field\"><span>Cover picture (optional)</span><div class=\"md-covrow\"><label class=\"md-btn md-btn-sm md-btn-ghost md-pick\">Choose a cover<input type=\"file\" id=\"md-dcover\" accept=\"image/jpeg,image/png,image/webp\" onchange=\"" + Me + ".dcov(this)\"></label><span class=\"md-hint\" id=\"md-dcovt\">Without one, a simple cover with the name is made for you.</span></div></div><em class=\"md-err\" id=\"md-e-dfile\" role=\"alert\"></em><p class=\"md-hint\">Forms, care guides and handouts. Tag them media-center to show them on the Media page.</p><div class=\"md-prog\" id=\"md-dprog\"><i id=\"md-dbar\"></i></div><div><button type=\"button\" class=\"md-btn md-btn-accent\" id=\"md-dbtn\" onclick=\"" + Me + ".dupload()\">Upload PDF</button></div></div></div></div></details>");
                sb.Append("</div>" + DataList("md-taglist", allTags));
            }
            sb.Append("<div class=\"md-mchipbar\" id=\"md-mchips\">" + Chips(items, string.Empty) + "</div><div class=\"md-bar\"><label>Search<input type=\"search\" id=\"md-q\" list=\"md-qlist\" placeholder=\"Name, tag or description\" maxlength=\"60\" oninput=\"" + Me + ".search()\"></label>" + DataList("md-qlist", items.Select(m => m.Title)) + "<label>Tag<select id=\"md-mtag\" onchange=\"" + Me + ".list(1)\"><option value=\"\">Any tag</option>" + Ui.Opts(allTags.Select(t => (t, t)), string.Empty) + "</select></label><label>Used<select id=\"md-muse\" onchange=\"" + Me + ".list(1)\"><option value=\"\">Used or not</option><option value=\"used\">Used on the website</option><option value=\"free\">Not used yet</option></select></label></div><div class=\"md-mgrid\" id=\"md-mgrid\"></div><div id=\"md-pager\"></div>");
            response.SetElementContents("md-main", sb.ToString());
            response.ExecuteScript(Me + ".loaded();");
            return response;
        }

        private static string Chips(List<MdItem> items, string cur)
        {
            (string k, string n)[] list = { ("", "All"), ("photo", "Photos"), ("video", "Videos"), ("doc", "PDFs") };
            StringBuilder sb = new StringBuilder("<div class=\"md-chips\">");
            foreach ((string k, string n) in list)
            {
                sb.Append("<button type=\"button\" class=\"md-chip" + (k == cur ? " md-act" : string.Empty) + "\" aria-pressed=\"" + (k == cur ? "true" : "false") + "\" onclick=\"" + Me + ".setKind('" + k + "')\">" + n + "<em>" + (k == string.Empty ? items.Count : items.Count(m => m.Kind == k)).ToString(Ui.Inv) + "</em></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private async Task Redraw(ApiResponse response)
        {
            List<MdItem> items = await All();
            string kind = Val("mkind", 10) is "photo" or "video" or "doc" ? Val("mkind", 10) : string.Empty;
            string term = Val("mq", 60).Trim();
            string tag = Val("mtag", 40);
            string use = Val("muse", 10);
            Dictionary<string, int> used = await UseCounts(items);
            int Used(MdItem m) => used.TryGetValue(m.FullPath, out int n) ? n : 0;
            List<MdItem> all = items.Where(m => (kind == string.Empty || m.Kind == kind) && (tag == string.Empty || Site.Csv(m.Tags).Contains(tag)) && (use == string.Empty || (use == "used" ? Used(m) > 0 : Used(m) == 0)) && (term == string.Empty || (m.Title + " " + m.Alt + " " + m.Tags).Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            int page = Ui.PageOf(Val("mpg", 6), Size, all.Count);
            StringBuilder sb = new StringBuilder();
            if (all.Count == 0)
            {
                sb.Append("<p class=\"md-none\">Nothing matches.</p>");
            }
            foreach (MdItem m in all.Skip((page - 1) * Size).Take(Size))
            {
                bool vid = m.Kind == "video";
                bool doc = m.Kind == "doc";
                int n = Used(m);
                sb.Append("<div class=\"md-mitem\"><button type=\"button\" class=\"md-mthumb" + (doc ? " md-mdoc" : string.Empty) + "\" onclick=\"" + Me + ".view('" + Ui.Attr(Ui.Js(m.MediaId)) + "')\" aria-label=\"Open " + Ui.Attr(m.Title) + "\"><img src=\"" + Ui.Attr(m.ThumbPath) + "\" alt=\"\" loading=\"lazy\">" + (vid ? "<span class=\"md-play\"></span><em class=\"md-dur\">" + Clock(m.Seconds) + "</em>" : doc ? "<em class=\"md-dur\">PDF" + (m.Pages > 0 ? " &middot; " + m.Pages.ToString(Ui.Inv) + " p" : string.Empty) + "</em>" : string.Empty) + "</button><div class=\"md-mtxt\"><b>" + Ui.Enc(m.Title) + "</b><small>" + KindName(m.Kind) + (m.Width > 0 ? " &middot; " + m.Width.ToString(Ui.Inv) + "&times;" + m.Height.ToString(Ui.Inv) : string.Empty) + (m.Bytes > 0 ? " &middot; " + Bytes(m.Bytes) : string.Empty) + "</small><div class=\"md-tags\">" + (n > 0 ? "<span class=\"md-tag md-tag-use\">Used &times;" + n.ToString(Ui.Inv) + "</span>" : "<span class=\"md-tag\">Not used</span>") + string.Concat(Site.Csv(m.Tags).Take(3).Select(t => "<span class=\"md-tag\">" + Ui.Enc(t) + "</span>")) + "</div></div></div>");
            }
            response.SetElementContents("md-mgrid", sb.ToString());
            response.SetElementContents("md-pager", Ui.Pager("md", page, Size, all.Count, "item", "items", Me + ".list"));
            response.SetElementContents("md-mchips", Chips(items, kind));
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
                return await Refuse(response, c, "view media", "You don't have access to the media library.");
            }
            await Redraw(response);
            return response;
        }

        private static string Accept(string kind)
        {
            return kind == "video" ? "video/mp4,.mp4" : kind == "doc" ? "application/pdf,.pdf" : "image/jpeg,image/png,image/webp";
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                return await Refuse(response, c, "open media", "You don't have access to the media library.");
            }
            MdItem? m = await One(Val("mid", 50));
            if (m == null)
            {
                return Toast(response, "That item is no longer in the library.");
            }
            bool vid = m.Kind == "video";
            bool doc = m.Kind == "doc";
            bool canE = c.Can(Key, 'E');
            bool canD = c.Can(Key, 'D');
            List<string> uses = await Blocks.Uses(m.FullPath, m.Kind, m.Title, m.Tags);
            List<MdItem> items = await All();
            string ro = canE ? string.Empty : " readonly";
            string word = vid ? "video" : doc ? "PDF" : "photo";
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(m.Title) + "</h2><div class=\"md-pv\">" + (vid ? "<video controls playsinline preload=\"metadata\" poster=\"" + Ui.Attr(m.ThumbPath) + "\" src=\"" + Ui.Attr(m.FullPath) + "\"></video>" : doc ? "<a class=\"md-docopen\" href=\"" + Ui.Attr(m.FullPath) + "\" target=\"_blank\" rel=\"noopener\"><img src=\"" + Ui.Attr(m.ThumbPath) + "\" alt=\"\"><span>Open the PDF in a new tab</span></a>" : "<img src=\"" + Ui.Attr(m.FullPath) + "\" alt=\"" + Ui.Attr(m.Alt) + "\">") + "</div>");
            sb.Append("<div class=\"md-form md-mt\"><div class=\"md-f2\"><label class=\"md-field\"><span>Name</span><input type=\"text\" id=\"md-etitle\" maxlength=\"80\" value=\"" + Ui.Attr(m.Title) + "\"" + ro + "></label><label class=\"md-field\"><span>Tags, separated by commas</span><input type=\"text\" id=\"md-etags\" maxlength=\"200\" list=\"md-taglist2\" value=\"" + Ui.Attr(string.Join(", ", Site.Csv(m.Tags))) + "\"" + ro + "></label></div>" + DataList("md-taglist2", items.SelectMany(x => Site.Csv(x.Tags))) + (vid || doc ? string.Empty : "<label class=\"md-field\"><span>Description for screen readers</span><input type=\"text\" id=\"md-ealt\" maxlength=\"160\" value=\"" + Ui.Attr(m.Alt) + "\"" + ro + " placeholder=\"Describe what is in the photo\"></label>") + "<em class=\"md-err\" id=\"md-e-etitle\" role=\"alert\"></em></div>");
            sb.Append("<dl class=\"md-dl md-mt\"><dt>Type</dt><dd>" + (vid ? "Video &middot; " + Clock(m.Seconds) : doc ? "PDF document" + (m.Pages > 0 ? " &middot; " + Ui.Plural(m.Pages, "page", "pages") : string.Empty) : "Photo") + (m.Width > 0 ? " &middot; " + m.Width.ToString(Ui.Inv) + "&times;" + m.Height.ToString(Ui.Inv) : string.Empty) + (m.Bytes > 0 ? " &middot; " + Bytes(m.Bytes) : string.Empty) + "</dd><dt>Added</dt><dd>" + Ui.Enc(m.AddedName != string.Empty ? m.AddedName : "Sample library") + " &middot; " + Ui.Time(m.AddedUtc, "day") + "</dd><dt>Used on</dt><dd>" + (uses.Count > 0 ? "<ul class=\"md-uses\">" + string.Concat(uses.Select(u => "<li>" + Ui.Enc(u) + "</li>")) + "</ul>" : "Not used yet") + "</dd><dt>File</dt><dd>" + Ui.Enc(m.FullPath) + "</dd></dl>");
            if (canE)
            {
                long max = vid ? await MaxVideo() : doc ? MaxDoc : 30L * 1048576;
                sb.Append("<h3>Replace the file</h3><div class=\"md-rep\"><label class=\"md-btn md-btn-sm md-btn-ghost md-pick\" id=\"md-rbtn\">Choose a new " + word + "<input type=\"file\" id=\"md-rfile\" accept=\"" + Accept(m.Kind) + "\" data-max=\"" + max.ToString(Ui.Inv) + "\" onchange=\"" + Me + ".rpick(this,'" + Ui.Js(m.Kind) + "')\"></label><span class=\"md-hint\">The name and tags stay. Everywhere it is used shows the new file.</span></div><div class=\"md-prog\" id=\"md-rprog\"><i id=\"md-rbar\"></i></div><em class=\"md-err\" id=\"md-e-rfile\" role=\"alert\"></em>");
            }
            sb.Append("<div class=\"md-mfoot\">");
            if (canD)
            {
                sb.Append("<button type=\"button\" class=\"md-btn md-btn-danger md-left\" onclick=\"" + Me + ".del('" + Ui.Attr(Ui.Js(m.MediaId)) + "','" + Ui.Attr(Ui.Js(m.Title)) + "'," + uses.Count.ToString(Ui.Inv) + ")\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"md-btn md-btn-ghost\" onclick=\"" + Me + ".closeModal()\">" + (canE ? "Cancel" : "Close") + "</button>" + (canE ? "<button type=\"button\" class=\"md-btn md-btn-accent\" id=\"md-savebtn\" onclick=\"" + Me + ".save()\">Save</button>" : string.Empty) + "</div>");
            response.SetElementContents("md-mbody", sb.ToString());
            response.ExecuteScript(Me + ".openModal();");
            return response;
        }

        public async Task<ApiResponse> SaveInfo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "media info", "You can look at the library but not change it.");
            }
            string mid = Val("mid", 50);
            string title = Clean(GetDataValue("mtitle"), 80);
            if (title.Length < 2)
            {
                response.SetElementContents("md-e-etitle", "Give it a name.");
                response.ExecuteScript(Me + ".unsave();");
                return response;
            }
            MdItem? m = await One(mid);
            if (m == null)
            {
                return Toast(response, "That item is no longer in the library.");
            }
            string alt = m.Kind == "photo" ? Clean(GetDataValue("malt"), 160) : string.Empty;
            string tags = string.Join(",", Tags(GetDataValue("mtags")));
            string r = await Helper.PutData(new List<string> { "UPDATE MEDIA SET TITLE = @t, ALT = @a, TAGS = @g, SYSDTE = GETDATE() WHERE MEDIA_ID = @id" }, new List<SqlParameter> { App.P("t", title, 120), App.P("a", alt, 200), App.P("g", tags, 200), App.P("id", mid, 50) });
            if (Helper.IsError(r))
            {
                response.ExecuteScript(Me + ".unsave();");
                return Toast(response, "That could not be saved. Try again.");
            }
            Site.Forget();
            await App.Log(c.U.StaffId, "media info", m.Title != title ? m.Title + " → " + title : title, true);
            await Redraw(response);
            response.ExecuteScript(Me + ".saved();");
            return response;
        }

        private static string ImageExt(byte[] b)
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

        private static byte[]? DataImage(string? data, int max)
        {
            Match m = Regex.Match(data ?? string.Empty, "^data:image/(jpeg|png|webp);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success)
            {
                return null;
            }
            try
            {
                byte[] b = Convert.FromBase64String(m.Groups[2].Value);
                return b.Length <= max && ImageExt(b) != string.Empty ? b : null;
            }
            catch (FormatException)
            {
                return null;
            }
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

        private static (int W, int H) Fit(string? w, string? h, int max)
        {
            int a = int.TryParse(w, out int av) ? Math.Clamp(av, 0, 8192) : 0;
            int b = int.TryParse(h, out int bv) ? Math.Clamp(bv, 0, 8192) : 0;
            double r = max > 0 ? Math.Min(1.0, (double)max / Math.Max(1, Math.Max(a, b))) : 1.0;
            return ((int)Math.Round(a * r), (int)Math.Round(b * r));
        }

        private static string Up(string sub)
        {
            string d = Path.Combine(App.Root, "images", "media", sub);
            Directory.CreateDirectory(d);
            return d;
        }

        private static string Disk(string src)
        {
            return Path.Combine(App.Root, src.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void Drop(IEnumerable<string> files)
        {
            foreach (string f in files.Distinct())
            {
                if (f.StartsWith("images/media/", StringComparison.Ordinal) && !f.Contains("..") && File.Exists(Disk(f)))
                {
                    try
                    {
                        File.Delete(Disk(f));
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private static List<string> SwapSql()
        {
            return new List<string>
            {
                "UPDATE TEAM SET IMAGE = @to WHERE IMAGE = @from",
                "UPDATE BOOKTYPE SET IMAGE = @to WHERE IMAGE = @from",
                "UPDATE STAFF SET PHOTO = @to WHERE PHOTO = @from",
                "UPDATE ROOMTYPE SET IMAGE = @to WHERE IMAGE = @from",
                "UPDATE PET SET PHOTO = @to WHERE PHOTO = @from",
                "UPDATE ALBUM SET COVER = @to WHERE COVER = @from",
                "UPDATE ALBUMITEM SET FULL_PATH = @to WHERE FULL_PATH = @from",
                "UPDATE PAGEDOC SET BLOCKS = REPLACE(BLOCKS, @fromq, @toq) WHERE BLOCKS LIKE @like",
                "UPDATE PAGEVER SET BLOCKS = REPLACE(BLOCKS, @fromq, @toq) WHERE BLOCKS LIKE @like"
            };
        }

        private static List<SqlParameter> SwapParams(string from, string to)
        {
            return new List<SqlParameter>
            {
                App.P("from", from, 300), App.P("to", to, 300), App.P("fromq", "\"" + from + "\"", 310), App.P("toq", "\"" + to + "\"", 310),
                App.P("like", "%\"" + from.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "\"%", 400)
            };
        }

        private async Task<string> Store(Ctx c, string rid, string kind, string full, string thumb, int w, int h, long bytes, int secs, int pages, string title)
        {
            if (rid == string.Empty)
            {
                return await Helper.PutData(new List<string> { "INSERT INTO MEDIA (MEDIA_ID, KIND, TITLE, ALT, TAGS, FULL_PATH, THUMB_PATH, SECONDS, PAGES, WIDTH, HEIGHT, BYTES, ADDED_BY, ADDED_UTC) VALUES (@id, @kind, @title, @alt, @tags, @full, @thumb, @secs, @pages, @w, @h, @bytes, @by, SYSUTCDATETIME())" }, new List<SqlParameter>
                {
                    App.P("id", App.NewId(), 50), App.P("kind", kind, 10), App.P("title", title, 120), App.P("alt", kind == "photo" ? Clean(GetDataValue("malt"), 160) : string.Empty, 200), App.P("tags", string.Join(",", Tags(GetDataValue("mtags"))), 200),
                    App.P("full", full, 300), App.P("thumb", thumb, 300), App.PInt("secs", secs), App.PInt("pages", pages), App.PInt("w", w), App.PInt("h", h), new SqlParameter("bytes", SqlDbType.BigInt) { Value = bytes }, App.P("by", c.U.StaffId, 50)
                });
            }
            MdItem? m = await One(rid);
            if (m == null || m.Kind != kind)
            {
                return "Error: gone";
            }
            List<string> sql = SwapSql();
            sql.Add("UPDATE MEDIA SET FULL_PATH = @to, THUMB_PATH = @thumb, WIDTH = @w, HEIGHT = @h, BYTES = @bytes, SECONDS = @secs, PAGES = @pages, SYSDTE = GETDATE() WHERE MEDIA_ID = @id AND FULL_PATH = @from; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;");
            List<SqlParameter> ps = SwapParams(m.FullPath, full);
            ps.Add(App.P("thumb", thumb, 300));
            ps.Add(App.PInt("w", w));
            ps.Add(App.PInt("h", h));
            ps.Add(new SqlParameter("bytes", SqlDbType.BigInt) { Value = bytes });
            ps.Add(App.PInt("secs", secs > 0 ? secs : m.Seconds));
            ps.Add(App.PInt("pages", pages));
            ps.Add(App.P("id", rid, 50));
            string r = await Helper.PutData(sql, ps);
            if (!Helper.IsError(r))
            {
                Drop(new[] { m.FullPath, m.ThumbPath });
            }
            return r;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string rid = Val("rid", 50);
            if (!c.Can(Key, rid == string.Empty ? 'A' : 'E'))
            {
                return await Refuse(response, c, rid == string.Empty ? "upload photo" : "replace media", rid == string.Empty ? "Uploading needs the Add right on the media library." : "Replacing files needs the Edit right on the media library.", true);
            }
            string title = Clean(GetDataValue("mtitle"), 80);
            byte[]? big = DataImage(GetDataValue("full"), 2400 * 1024);
            byte[]? small = DataImage(GetDataValue("thumb"), 600 * 1024);
            if (rid == string.Empty && title.Length < 2)
            {
                response.ExecuteScript(Me + ".fail('Give the photo a name.');");
                return response;
            }
            if (big == null || small == null || ImageExt(big) != ImageExt(small))
            {
                response.ExecuteScript(Me + ".fail('That file is not a JPG, PNG or WebP photo, or it is too large.');");
                return response;
            }
            (int w, int h) = ImageSize(big);
            if (!SizeOk((w, h)) || !SizeOk(ImageSize(small)))
            {
                response.ExecuteScript(Me + ".fail('That photo is too big. Use one up to 4000 pixels on each side.');");
                return response;
            }
            string id = App.NewId();
            string ext = ImageExt(big);
            string full = "images/media/full/" + id + "." + ext;
            string thumb = "images/media/thumbs/" + id + "." + ext;
            await File.WriteAllBytesAsync(Path.Combine(Up("full"), id + "." + ext), big);
            await File.WriteAllBytesAsync(Path.Combine(Up("thumbs"), id + "." + ext), small);
            string r = await Store(c, rid, "photo", full, thumb, w, h, big.Length, 0, 0, title);
            if (Helper.IsError(r))
            {
                Drop(new[] { full, thumb });
                response.ExecuteScript(Me + ".fail('" + (rid != string.Empty ? "That photo is no longer in the library." : "The photo could not be saved. Try again.") + "');");
                return response;
            }
            Site.Forget();
            MdItem? after = rid != string.Empty ? await One(rid) : null;
            await App.Log(c.U.StaffId, rid == string.Empty ? "upload photo" : "replace media", rid == string.Empty ? title : "photo · " + (after?.Title ?? string.Empty), true);
            string newId = rid;
            if (rid == string.Empty)
            {
                var (dt, _) = await Helper.GetDataTable("SELECT MEDIA_ID FROM MEDIA WHERE FULL_PATH = @f", new List<SqlParameter> { App.P("f", full, 300) });
                newId = dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) ?? string.Empty : string.Empty;
            }
            response.ExecuteScript(Me + ".done('" + (rid == string.Empty ? "photo" : "replace") + "','" + Ui.Js(newId) + "','" + Ui.Js(full) + "','photo');");
            return response;
        }

        private static string CleanUid(string? v)
        {
            string t = (v ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(t, "^[a-f0-9]{8,40}$") ? t : string.Empty;
        }

        private static string PartFile(string staffId, string uid)
        {
            return Path.Combine(App.Root, "temp", "uploads", "media-" + Regex.Replace(staffId, "[^A-Za-z0-9-]", string.Empty) + "-" + uid + ".part");
        }

        private static void DropParts(string uid)
        {
            string dir = Path.Combine(App.Root, "temp", "uploads");
            if (uid == string.Empty || !Directory.Exists(dir))
            {
                return;
            }
            foreach (string f in Directory.GetFiles(dir, "media-*-" + uid + ".part"))
            {
                try
                {
                    File.Delete(f);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void Stale()
        {
            string dir = Path.Combine(App.Root, "temp", "uploads");
            if (!Directory.Exists(dir))
            {
                return;
            }
            foreach (string f in Directory.GetFiles(dir, "media-*.part"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddHours(-2))
                    {
                        File.Delete(f);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        public async Task<ApiResponse> Chunk()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'A') && !c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "upload", "Uploading needs the Add right on the media library.", true);
            }
            bool doc = Val("ukind", 10) == "doc";
            long max = doc ? MaxDoc : await MaxVideo();
            string uid = CleanUid(GetDataValue("uid"));
            int idx = int.TryParse(GetDataValue("idx"), out int i) ? i : -1;
            int total = int.TryParse(GetDataValue("total"), out int t) ? t : 0;
            if (uid == string.Empty || idx < 0 || total < 1 || idx >= total || (long)(total - 1) * ChunkSize >= max)
            {
                response.ExecuteScript(Me + ".fail('" + (doc ? "The PDF" : "The video") + " must be " + (max / 1048576).ToString(Ui.Inv) + " MB or smaller.');");
                return response;
            }
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(GetDataValue("part") ?? string.Empty);
            }
            catch (FormatException)
            {
                response.ExecuteScript(Me + ".fail('The upload was damaged. Please try again.');");
                return response;
            }
            string file = PartFile(c.U.StaffId, uid);
            string dir = Path.GetDirectoryName(file) ?? App.Root;
            Directory.CreateDirectory(dir);
            if (idx == 0)
            {
                Stale();
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                string mine = "media-" + Regex.Replace(c.U.StaffId, "[^A-Za-z0-9-]", string.Empty) + "-";
                if (Directory.GetFiles(dir, mine + "*.part").Length >= 3)
                {
                    response.ExecuteScript(Me + ".fail('You already have 3 uploads running. Wait for them to finish, or try again in a little while.');");
                    return response;
                }
            }
            long have = File.Exists(file) ? new FileInfo(file).Length : 0;
            if (have != (long)idx * ChunkSize || bytes.Length == 0 || bytes.Length > ChunkSize || (idx < total - 1 && bytes.Length != ChunkSize) || have + bytes.Length > max)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
                response.ExecuteScript(Me + ".fail('" + (have + bytes.Length > max ? (doc ? "The PDF" : "The video") + " must be " + (max / 1048576).ToString(Ui.Inv) + " MB or smaller." : "The upload was interrupted. Please try again.") + "');");
                return response;
            }
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Append, FileAccess.Write))
                {
                    await fs.WriteAsync(bytes);
                }
            }
            catch (IOException)
            {
                DropParts(uid);
                response.ExecuteScript(Me + ".fail('The upload was interrupted. Please try again.');");
                return response;
            }
            response.ExecuteScript(Me + ".chunkOk(" + idx.ToString(Ui.Inv) + ");");
            return response;
        }

        private static async Task<byte[]> Head(string file, int n)
        {
            byte[] head = new byte[n];
            using (FileStream fs = File.OpenRead(file))
            {
                int got = await fs.ReadAsync(head.AsMemory(0, n));
                return got < n ? Array.Empty<byte>() : head;
            }
        }

        public async Task<ApiResponse> VideoDone()
        {
            try
            {
                return await VideoFinish();
            }
            finally
            {
                DropParts(CleanUid(GetDataValue("uid")));
            }
        }

        private async Task<ApiResponse> VideoFinish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string rid = Val("rid", 50);
            if (!c.Can(Key, rid == string.Empty ? 'A' : 'E'))
            {
                return await Refuse(response, c, "upload video", "Uploading needs the Add right on the media library.", true);
            }
            string uid = CleanUid(GetDataValue("uid"));
            string file = uid != string.Empty ? PartFile(c.U.StaffId, uid) : string.Empty;
            if (file == string.Empty || !File.Exists(file))
            {
                response.ExecuteScript(Me + ".fail('The upload was interrupted. Please try again.');");
                return response;
            }
            string title = Clean(GetDataValue("mtitle"), 80);
            long size = new FileInfo(file).Length;
            long maxV = await MaxVideo();
            byte[] head = await Head(file, 12);
            byte[]? still = DataImage(GetDataValue("poster"), 1024 * 1024);
            string err = rid == string.Empty && title.Length < 2 ? "Give the video a name." : head.Length != 12 || head[4] != 0x66 || head[5] != 0x74 || head[6] != 0x79 || head[7] != 0x70 ? "That file is not an MP4 video." : size > maxV ? "The video must be " + (maxV / 1048576).ToString(Ui.Inv) + " MB or smaller." : still == null || !SizeOk(ImageSize(still)) ? "The cover picture could not be made. Try another browser." : string.Empty;
            if (err != string.Empty)
            {
                File.Delete(file);
                response.ExecuteScript(Me + ".fail('" + Ui.Js(err) + "');");
                return response;
            }
            string id = App.NewId();
            string full = "images/media/video/" + id + ".mp4";
            string pe = ImageExt(still!);
            string poster = "images/media/thumbs/" + id + "-p." + pe;
            File.Move(file, Path.Combine(Up("video"), id + ".mp4"));
            await File.WriteAllBytesAsync(Path.Combine(Up("thumbs"), id + "-p." + pe), still!);
            int secs = int.TryParse(GetDataValue("secs"), out int sv) ? Math.Clamp(sv, 0, 36000) : 0;
            (int w, int h) = Fit(GetDataValue("mw"), GetDataValue("mh"), 0);
            string r = await Store(c, rid, "video", full, poster, w, h, size, secs, 0, title);
            if (Helper.IsError(r))
            {
                Drop(new[] { full, poster });
                response.ExecuteScript(Me + ".fail('" + (rid != string.Empty ? "That video is no longer in the library." : "The video could not be saved. Try again.") + "');");
                return response;
            }
            Site.Forget();
            await App.Log(c.U.StaffId, rid == string.Empty ? "upload video" : "replace media", rid == string.Empty ? title : "video", true);
            string newId = rid;
            if (rid == string.Empty)
            {
                var (dt, _) = await Helper.GetDataTable("SELECT MEDIA_ID FROM MEDIA WHERE FULL_PATH = @f", new List<SqlParameter> { App.P("f", full, 300) });
                newId = dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) ?? string.Empty : string.Empty;
            }
            response.ExecuteScript(Me + ".done('" + (rid == string.Empty ? "video" : "replace") + "','" + Ui.Js(newId) + "','" + Ui.Js(full) + "','video');");
            return response;
        }

        public async Task<ApiResponse> DocDone()
        {
            try
            {
                return await DocFinish();
            }
            finally
            {
                DropParts(CleanUid(GetDataValue("uid")));
            }
        }

        private async Task<ApiResponse> DocFinish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string rid = Val("rid", 50);
            if (!c.Can(Key, rid == string.Empty ? 'A' : 'E'))
            {
                return await Refuse(response, c, "upload pdf", "Uploading needs the Add right on the media library.", true);
            }
            string uid = CleanUid(GetDataValue("uid"));
            string file = uid != string.Empty ? PartFile(c.U.StaffId, uid) : string.Empty;
            if (file == string.Empty || !File.Exists(file))
            {
                response.ExecuteScript(Me + ".fail('The upload was interrupted. Please try again.');");
                return response;
            }
            string title = Clean(GetDataValue("mtitle"), 80);
            long size = new FileInfo(file).Length;
            byte[] head = await Head(file, 5);
            byte[]? cover = DataImage(GetDataValue("cover"), 1024 * 1024);
            string err = rid == string.Empty && title.Length < 2 ? "Give the PDF a name." : head.Length < 5 || head[0] != 0x25 || head[1] != 0x50 || head[2] != 0x44 || head[3] != 0x46 ? "That file is not a PDF." : size > MaxDoc ? "The PDF must be " + (MaxDoc / 1048576).ToString(Ui.Inv) + " MB or smaller." : cover == null || !SizeOk(ImageSize(cover)) ? "The cover picture could not be made. Try another browser." : string.Empty;
            if (err != string.Empty)
            {
                File.Delete(file);
                response.ExecuteScript(Me + ".fail('" + Ui.Js(err) + "');");
                return response;
            }
            int pages;
            try
            {
                string text = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(file));
                pages = Regex.Matches(text, "/Type\\s*/Page(?![a-zA-Z])").Count;
            }
            catch (IOException)
            {
                pages = 0;
            }
            string id = App.NewId();
            string full = "images/media/docs/" + id + ".pdf";
            string ce = ImageExt(cover!);
            string thumb = "images/media/thumbs/" + id + "-c." + ce;
            File.Move(file, Path.Combine(Up("docs"), id + ".pdf"));
            await File.WriteAllBytesAsync(Path.Combine(Up("thumbs"), id + "-c." + ce), cover!);
            string r = await Store(c, rid, "doc", full, thumb, 0, 0, size, 0, pages, title);
            if (Helper.IsError(r))
            {
                Drop(new[] { full, thumb });
                response.ExecuteScript(Me + ".fail('" + (rid != string.Empty ? "That PDF is no longer in the library." : "The PDF could not be saved. Try again.") + "');");
                return response;
            }
            Site.Forget();
            await App.Log(c.U.StaffId, rid == string.Empty ? "upload pdf" : "replace media", rid == string.Empty ? title : "pdf", true);
            string newId = rid;
            if (rid == string.Empty)
            {
                var (dt, _) = await Helper.GetDataTable("SELECT MEDIA_ID FROM MEDIA WHERE FULL_PATH = @f", new List<SqlParameter> { App.P("f", full, 300) });
                newId = dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) ?? string.Empty : string.Empty;
            }
            response.ExecuteScript(Me + ".done('" + (rid == string.Empty ? "doc" : "replace") + "','" + Ui.Js(newId) + "','" + Ui.Js(full) + "','doc');");
            return response;
        }

        public async Task<ApiResponse> SetPoster()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "video cover", "Changing the cover picture needs the Edit right on the media library.");
            }
            string mid = Val("mid", 50);
            byte[]? still = DataImage(GetDataValue("poster"), 1024 * 1024);
            MdItem? m = await One(mid);
            if (still == null || !SizeOk(ImageSize(still)) || m == null || m.Kind != "video")
            {
                return response;
            }
            string name = App.NewId() + "-p." + ImageExt(still);
            string thumb = "images/media/thumbs/" + name;
            await File.WriteAllBytesAsync(Path.Combine(Up("thumbs"), name), still);
            int secs = int.TryParse(GetDataValue("secs"), out int sv) ? Math.Clamp(sv, 0, 36000) : 0;
            (int w, int h) = Fit(GetDataValue("mw"), GetDataValue("mh"), 0);
            string r = await Helper.PutData(new List<string> { "UPDATE MEDIA SET THUMB_PATH = @thumb, SECONDS = CASE WHEN @secs > 0 THEN @secs ELSE SECONDS END, WIDTH = CASE WHEN @w > 0 AND @h > 0 THEN @w ELSE WIDTH END, HEIGHT = CASE WHEN @w > 0 AND @h > 0 THEN @h ELSE HEIGHT END, SYSDTE = GETDATE() WHERE MEDIA_ID = @id AND KIND = N'video'" }, new List<SqlParameter> { App.P("thumb", thumb, 300), App.PInt("secs", secs), App.PInt("w", w), App.PInt("h", h), App.P("id", mid, 50) });
            if (Helper.IsError(r))
            {
                Drop(new[] { thumb });
                return response;
            }
            Drop(new[] { m.ThumbPath });
            Site.Forget();
            response.ExecuteScript(Me + ".postered('" + Ui.Js(mid) + "'," + (Val("again", 1) == "1" ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> Remove()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                return await Refuse(response, c, "delete media", "Deleting needs the Delete right on the media library.");
            }
            string mid = Val("mid", 50);
            MdItem? m = await One(mid);
            if (m == null)
            {
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(0);");
                return Toast(response, "That item is no longer in the library.");
            }
            List<string> uses = await Blocks.Uses(m.FullPath, m.Kind, m.Title, m.Tags);
            if (uses.Count > 0)
            {
                await App.Log(c.U.StaffId, "delete media", m.Title + " (in use)", false);
                response.ExecuteScript(Me + ".inUse('" + Ui.Js(m.Title) + "'," + JsonSerializer.Serialize(uses).Replace("<", "\\u003c") + ");");
                return response;
            }
            string r = await Helper.PutData(new List<string> { "DELETE FROM MEDIA WHERE MEDIA_ID = @id" }, new List<SqlParameter> { App.P("id", mid, 50) });
            if (Helper.IsError(r))
            {
                return Toast(response, "That could not be deleted. Try again.");
            }
            Drop(new[] { m.FullPath, m.ThumbPath });
            Site.Forget();
            await App.Log(c.U.StaffId, "delete media", KindName(m.Kind).ToLowerInvariant() + " · " + m.Title, true);
            await Redraw(response);
            response.ExecuteScript(Me + ".removed();");
            return response;
        }
    }
}
