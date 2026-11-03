using System.Data;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public sealed class PageSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Href { get; init; } = string.Empty;
        public string[] Data { get; init; } = Array.Empty<string>();
    }

    public sealed class PartSpec
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Desc { get; init; } = string.Empty;
        public string Fields { get; init; } = string.Empty;
        public string ItemFields { get; init; } = string.Empty;
    }

    public static class Blocks
    {
        public const int Keep = 20;

        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static readonly PageSpec[] Pages =
        {
            new PageSpec { Key = "home", Name = "Home", Href = "Home", Data = new[] { "teams", "vets", "boardband", "hours" } },
            new PageSpec { Key = "services", Name = "Services", Href = "Services", Data = new[] { "servicelist" } },
            new PageSpec { Key = "vets", Name = "Vets", Href = "Vets", Data = new[] { "vetlist" } },
            new PageSpec { Key = "boarding", Name = "Boarding", Href = "Boarding", Data = new[] { "runlist", "vaxrules" } },
            new PageSpec { Key = "petowners", Name = "Pet owners", Href = "PetOwners", Data = new[] { "vaxchart", "insurance" } },
            new PageSpec { Key = "emergency", Name = "Emergency", Href = "Emergency", Data = new[] { "urgent" } },
            new PageSpec { Key = "media", Name = "Media", Href = "Media", Data = new[] { "albums", "videos", "docs" } },
            new PageSpec { Key = "contact", Name = "Contact", Href = "Contact", Data = new[] { "contact" } }
        };

        public static readonly PartSpec[] Types =
        {
            new PartSpec { Key = "hero", Name = "Page top", Desc = "Big title, text, buttons and a picture or video.", Fields = "kicker,title,text,media,buttons,extra" },
            new PartSpec { Key = "text", Name = "Text", Desc = "A title and paragraphs.", Fields = "kicker,title,text" },
            new PartSpec { Key = "split", Name = "Picture and text", Desc = "A picture or video beside the text.", Fields = "kicker,title,text,media,button,flip,extra" },
            new PartSpec { Key = "cards", Name = "Cards", Desc = "A row of cards with an icon, title, text and link.", Fields = "kicker,title,text,items", ItemFields = "icon,title,text,link" },
            new PartSpec { Key = "steps", Name = "Steps", Desc = "Numbered steps, for a process or a timeline.", Fields = "kicker,title,text,items", ItemFields = "title,text" },
            new PartSpec { Key = "stats", Name = "Numbers", Desc = "A row of big numbers with a label under each.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "boxes", Name = "Checklists", Desc = "Boxes with a title and one tick per line.", Fields = "kicker,title,items", ItemFields = "title,lines" },
            new PartSpec { Key = "people", Name = "People", Desc = "Portraits with a name and a role, for a team.", Fields = "kicker,title,text,items", ItemFields = "media,title,text" },
            new PartSpec { Key = "faq", Name = "Questions", Desc = "Questions that open to show the answer.", Fields = "kicker,title,items", ItemFields = "title,text" },
            new PartSpec { Key = "quote", Name = "Quote", Desc = "A quote with a name and a small photo.", Fields = "quote,name,media" },
            new PartSpec { Key = "media", Name = "Picture or video", Desc = "One wide picture or video with a caption.", Fields = "media,caption" },
            new PartSpec { Key = "cta", Name = "Call to action", Desc = "A colored band with a title, text and buttons.", Fields = "kicker,title,text,buttons" }
        };

        public static readonly PartSpec[] Parts =
        {
            new PartSpec { Key = "teams", Name = "Teams", Desc = "Every team with its picture and the next free online time. Edit them in Booking types.", Fields = "kicker,title,text" },
            new PartSpec { Key = "vets", Name = "Our vets", Desc = "Four vets with photo, role and the animals they see. Edit them in Staff.", Fields = "kicker,title,text" },
            new PartSpec { Key = "boardband", Name = "Boarding band", Desc = "The boarding runs with their nightly price and a button to book a stay.", Fields = "kicker,title,text,label" },
            new PartSpec { Key = "hours", Name = "Hours and address", Desc = "Opening hours (today highlighted), address, phone and the pet insurance list from Settings.", Fields = "kicker,title" },
            new PartSpec { Key = "servicelist", Name = "Service list", Desc = "Filters for team and animal, then every service with length, price and the next online time.", Fields = "title" },
            new PartSpec { Key = "vetlist", Name = "Vet list", Desc = "Filters for team and animal, then every vet, technician and groomer shown on the website.", Fields = "title" },
            new PartSpec { Key = "runlist", Name = "Runs and prices", Desc = "Every boarding run and cat condo with its price, free places in the next 7 nights and the add-ons.", Fields = "kicker,title,text,label" },
            new PartSpec { Key = "vaxrules", Name = "Boarding vaccines", Desc = "The vaccines each animal needs for a stay, from Settings, and check-in and check-out times.", Fields = "kicker,title,text" },
            new PartSpec { Key = "vaxchart", Name = "Vaccine schedule", Desc = "The vaccine plan for dogs, cats and rabbits. Edit it on the Vaccine chart tab.", Fields = "kicker,title,text" },
            new PartSpec { Key = "insurance", Name = "Pet insurance", Desc = "The pet insurance plans from Settings.", Fields = "kicker,title,text" },
            new PartSpec { Key = "urgent", Name = "Urgent care box", Desc = "Our phone and hours today, plus the after-hours emergency hospital from Settings.", Fields = "kicker,title,text" },
            new PartSpec { Key = "albums", Name = "Photo albums", Desc = "Every published album from Albums. Each opens to its photos.", Fields = "kicker,title" },
            new PartSpec { Key = "videos", Name = "Videos", Desc = "Every video in the media library tagged media-center.", Fields = "kicker,title" },
            new PartSpec { Key = "docs", Name = "Documents", Desc = "Every PDF in the media library tagged media-center, to read or download.", Fields = "kicker,title" },
            new PartSpec { Key = "contact", Name = "Contact details and form", Desc = "Address, phone, opening hours from Settings, a note box and the message form.", Fields = "kicker,title,text" }
        };

        public static readonly (string Key, string Name)[] Icons = { ("paw", "Paw"), ("dog", "Dog"), ("cat", "Cat"), ("heart", "Heart"), ("tooth", "Tooth"), ("syringe", "Vaccine"), ("scissors", "Grooming"), ("bed", "Boarding"), ("scalpel", "Surgery"), ("stethoscope", "Stethoscope"), ("shield", "Shield"), ("clock", "Clock"), ("pin", "Map pin"), ("cal", "Calendar"), ("check", "Tick"), ("phone", "Phone"), ("mail", "Email"), ("doc", "Document"), ("star", "Star"), ("users", "People") };

        public static readonly (string Key, string Name)[] HeroExtras = { ("", "Nothing else"), ("finder", "Find a time box") };

        public static readonly (string Key, string Name)[] SplitExtras = { ("", "Nothing else"), ("counts", "Vets, services and clients counts") };

        private static readonly string[] Allowed = { "p", "br", "b", "i", "ul", "ol", "li", "a" };

        public static PageSpec? PageOf(string key)
        {
            return Pages.FirstOrDefault(p => p.Key == key);
        }

        public static PartSpec? Spec(Block b)
        {
            return b.Type == "data" ? Parts.FirstOrDefault(p => p.Key == b.Source) : Types.FirstOrDefault(t => t.Key == b.Type);
        }

        public static bool Has(PartSpec s, string field)
        {
            return ("," + s.Fields + ",").Contains("," + field + ",", StringComparison.Ordinal);
        }

        public static string NewId()
        {
            return "b" + Guid.NewGuid().ToString("N").Substring(0, 9);
        }

        public static List<Block> Parse(string? json)
        {
            try
            {
                List<Block>? list = JsonSerializer.Deserialize<List<Block>>(string.IsNullOrWhiteSpace(json) ? "[]" : json, ReadOpts);
                List<Block> outList = new List<Block>();
                foreach (Block? b in list ?? new List<Block>())
                {
                    if (b == null)
                    {
                        continue;
                    }
                    b.Id ??= string.Empty;
                    b.Type ??= "text";
                    b.Source ??= string.Empty;
                    b.Kicker ??= string.Empty;
                    b.Title ??= string.Empty;
                    b.Text ??= string.Empty;
                    b.Media ??= string.Empty;
                    b.Label ??= string.Empty;
                    b.Link ??= string.Empty;
                    b.Label2 ??= string.Empty;
                    b.Link2 ??= string.Empty;
                    b.Extra ??= string.Empty;
                    b.Items = (b.Items ?? new List<BlockItem>()).Where(i => i != null).Select(i => new BlockItem { Icon = i.Icon ?? string.Empty, Media = i.Media ?? string.Empty, Title = i.Title ?? string.Empty, Text = i.Text ?? string.Empty, Link = i.Link ?? string.Empty, Label = i.Label ?? string.Empty }).ToList();
                    outList.Add(b);
                }
                return outList;
            }
            catch (JsonException)
            {
                return new List<Block>();
            }
        }

        public static string Json(List<Block> list)
        {
            return JsonSerializer.Serialize(list, WriteOpts);
        }

        public static void Ensure(PageSpec p, List<Block> list)
        {
            foreach (string k in p.Data)
            {
                if (!list.Any(b => b.Type == "data" && b.Source == k))
                {
                    list.Add(new Block { Id = NewId(), Type = "data", Source = k, Title = Parts.First(x => x.Key == k).Name });
                }
            }
        }

        public static string SafeLink(string? value)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0 || v.Length > 300)
            {
                return string.Empty;
            }
            if (Regex.IsMatch(v, "^https?://[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^mailto:[^\\s<>\"']+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^tel:[0-9+() .-]+$", RegexOptions.IgnoreCase) || Regex.IsMatch(v, "^#[A-Za-z0-9_-]+$") || Regex.IsMatch(v, "^[A-Za-z][A-Za-z0-9_-]*(\\?[A-Za-z0-9=&%._+-]*)?(#[A-Za-z0-9_-]+)?$"))
            {
                return v;
            }
            return string.Empty;
        }

        public static string Sanitize(string? html, int max)
        {
            string src = Regex.Replace(html ?? string.Empty, "<(script|style|template|iframe|object)[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            src = Regex.Replace(src, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            foreach (Match m in Regex.Matches(src, "<[^>]*>|[^<]+|<"))
            {
                string t = m.Value;
                if (!t.StartsWith('<') || t == "<")
                {
                    sb.Append(Ui.Enc(WebUtility.HtmlDecode(t)));
                    continue;
                }
                Match tag = Regex.Match(t, "^<\\s*(/?)\\s*([a-zA-Z][a-zA-Z0-9]*)([^>]*)>$");
                if (!tag.Success)
                {
                    continue;
                }
                bool close = tag.Groups[1].Value == "/";
                string name = tag.Groups[2].Value.ToLowerInvariant() switch
                {
                    "strong" => "b",
                    "em" => "i",
                    "div" => "p",
                    "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "p",
                    string n => n
                };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                if (name == "br")
                {
                    if (!close)
                    {
                        sb.Append("<br>");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at < 0)
                    {
                        continue;
                    }
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                    continue;
                }
                if (name == "p" && open.Contains("p"))
                {
                    int at = open.LastIndexOf("p");
                    for (int i = open.Count - 1; i >= at; i--)
                    {
                        sb.Append("</" + open[i] + ">");
                    }
                    open.RemoveRange(at, open.Count - at);
                }
                if (name == "a")
                {
                    Match h = Regex.Match(tag.Groups[3].Value, "href\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
                    string href = h.Success ? SafeLink(WebUtility.HtmlDecode(h.Groups[2].Value + h.Groups[3].Value + h.Groups[4].Value)) : string.Empty;
                    if (href == string.Empty || open.Contains("a"))
                    {
                        continue;
                    }
                    bool outside = href.StartsWith("http", StringComparison.OrdinalIgnoreCase);
                    sb.Append("<a href=\"" + Ui.Attr(href) + "\"" + (outside ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string outHtml = Regex.Replace(sb.ToString(), "<(p|b|i|li|ul|ol)>(\\s|<br>)*</\\1>", string.Empty);
            outHtml = Regex.Replace(outHtml, "(<br>\\s*){3,}", "<br><br>").Trim();
            return outHtml.Length > max ? string.Empty : outHtml;
        }

        public static string Plain(string? value, int max)
        {
            string v = Regex.Replace((value ?? string.Empty).Replace("\r", string.Empty), "[ \\t]+", " ");
            v = Regex.Replace(v, "\\n{3,}", "\n\n").Trim();
            return v.Length > max ? v.Substring(0, max).Trim() : v;
        }

        public static (List<Block>? Blocks, string Error) Clean(SiteInfo site, PageSpec page, string json)
        {
            List<Block>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<Block>>(json ?? string.Empty, ReadOpts);
            }
            catch (JsonException)
            {
                return (null, "The page could not be read. Reload and try again.");
            }
            if (raw == null || raw.Count == 0 || raw.Count > 40)
            {
                return (null, "A page needs between 1 and 40 blocks.");
            }
            HashSet<string> media = site.Media.Where(m => m.Kind != "doc").Select(m => m.Full).ToHashSet(StringComparer.Ordinal);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            List<Block> list = new List<Block>();
            foreach (Block? r in raw)
            {
                if (r == null)
                {
                    continue;
                }
                string type = r.Type ?? string.Empty;
                PartSpec? spec = type == "data" ? Parts.FirstOrDefault(p => p.Key == r.Source && page.Data.Contains(p.Key)) : Types.FirstOrDefault(t => t.Key == type);
                if (spec == null)
                {
                    return (null, "That page has a block it cannot use. Reload and try again.");
                }
                if (type == "data" && list.Any(x => x.Type == "data" && x.Source == spec.Key))
                {
                    return (null, spec.Name + " is on the page twice.");
                }
                string id = Regex.IsMatch(r.Id ?? string.Empty, "^b[a-z0-9]{3,12}$") && !ids.Contains(r.Id!) ? r.Id! : NewId();
                ids.Add(id);
                Block b = new Block { Id = id, Type = type, Source = type == "data" ? spec.Key : string.Empty, Hidden = r.Hidden };
                b.Kicker = Has(spec, "kicker") ? Plain(r.Kicker, 80) : string.Empty;
                b.Title = Has(spec, "title") || Has(spec, "name") ? Plain(r.Title, 140).Replace("\n", " ") : string.Empty;
                if (Has(spec, "text"))
                {
                    b.Text = Sanitize(r.Text, 6000);
                    if (b.Text == string.Empty && Regex.Replace(r.Text ?? string.Empty, "<[^>]*>|\\s|&nbsp;", string.Empty).Length > 0)
                    {
                        return (null, "The text in " + spec.Name + " is too long.");
                    }
                }
                else if (Has(spec, "quote") || Has(spec, "caption"))
                {
                    b.Text = Plain(r.Text, Has(spec, "quote") ? 600 : 240);
                }
                if (Has(spec, "media"))
                {
                    string m = (r.Media ?? string.Empty).Trim();
                    if (m != string.Empty && !media.Contains(m))
                    {
                        return (null, "A picture in " + spec.Name + " is no longer in the media library. Choose it again.");
                    }
                    b.Media = m;
                }
                if (Has(spec, "buttons") || Has(spec, "button"))
                {
                    b.Label = Plain(r.Label, 40).Replace("\n", " ");
                    b.Link = SafeLink(r.Link);
                    if (b.Label != string.Empty && b.Link == string.Empty)
                    {
                        return (null, "Check the link of the button “" + b.Label + "”. Use a page name like Services, Book or Contact#form, or a full https:// address.");
                    }
                    if (Has(spec, "buttons"))
                    {
                        b.Label2 = Plain(r.Label2, 40).Replace("\n", " ");
                        b.Link2 = SafeLink(r.Link2);
                        if (b.Label2 != string.Empty && b.Link2 == string.Empty)
                        {
                            return (null, "Check the link of the button “" + b.Label2 + "”.");
                        }
                    }
                }
                if (Has(spec, "label"))
                {
                    b.Label = Plain(r.Label, 40).Replace("\n", " ");
                }
                b.Flip = Has(spec, "flip") && r.Flip;
                if (Has(spec, "extra"))
                {
                    (string Key, string Name)[] ex = type == "hero" ? HeroExtras : SplitExtras;
                    b.Extra = ex.Any(x => x.Key == r.Extra) ? r.Extra ?? string.Empty : string.Empty;
                }
                if (Has(spec, "items"))
                {
                    foreach (BlockItem? it in (r.Items ?? new List<BlockItem>()).Take(12))
                    {
                        if (it == null)
                        {
                            continue;
                        }
                        BlockItem c = new BlockItem { Title = Plain(it.Title, 120).Replace("\n", " "), Text = Plain(it.Text, 600) };
                        if (spec.ItemFields.Contains("icon"))
                        {
                            c.Icon = Icons.Any(x => x.Key == it.Icon) ? it.Icon ?? string.Empty : string.Empty;
                        }
                        if (spec.ItemFields.Contains("media"))
                        {
                            string m = (it.Media ?? string.Empty).Trim();
                            if (m != string.Empty && !media.Contains(m))
                            {
                                return (null, "A photo in " + spec.Name + " is no longer in the media library. Choose it again.");
                            }
                            c.Media = m;
                        }
                        if (spec.ItemFields.Contains("link"))
                        {
                            c.Link = SafeLink(it.Link);
                            c.Label = c.Link != string.Empty ? Plain(it.Label, 40).Replace("\n", " ") : string.Empty;
                            if ((it.Link ?? string.Empty).Trim() != string.Empty && c.Link == string.Empty)
                            {
                                return (null, "Check the link on the card “" + c.Title + "”.");
                            }
                        }
                        if (c.Title != string.Empty || c.Text != string.Empty || c.Media != string.Empty)
                        {
                            b.Items.Add(c);
                        }
                    }
                }
                list.Add(b);
            }
            string? missing = page.Data.FirstOrDefault(k => !list.Any(x => x.Type == "data" && x.Source == k));
            if (missing != null)
            {
                return (null, (Parts.FirstOrDefault(p => p.Key == missing)?.Name ?? missing) + " must stay on the page. Hide it instead.");
            }
            return (list, string.Empty);
        }

        public static async Task<PageDoc> LoadDoc(string key)
        {
            PageDoc doc = new PageDoc { Key = key };
            var (dt, emsg) = await Helper.GetDataTable("SELECT BLOCKS, SAVED_UTC, SAVED_BY FROM PAGEDOC WHERE PAGE_KEY = @k", new List<SqlParameter> { App.P("k", key, 30) });
            if (emsg == string.Empty && dt.Rows.Count > 0)
            {
                doc.Blocks = Parse(Convert.ToString(dt.Rows[0]["BLOCKS"]));
                doc.Saved = Convert.ToDateTime(dt.Rows[0]["SAVED_UTC"]);
                doc.By = Convert.ToString(dt.Rows[0]["SAVED_BY"]) ?? string.Empty;
            }
            PageSpec? p = PageOf(key);
            if (p != null)
            {
                Ensure(p, doc.Blocks);
            }
            return doc;
        }

        public static async Task<List<PageVersion>> Versions(string key)
        {
            List<PageVersion> list = new List<PageVersion>();
            var (dt, emsg) = await Helper.GetDataTable("SELECT V.VER_ID, V.BLOCKS, V.SAVED_UTC, V.SAVED_BY, ISNULL(S.STAFF_NAME, N'') AS WHO FROM PAGEVER V LEFT JOIN STAFF S ON S.STAFF_ID = V.SAVED_BY WHERE V.PAGE_KEY = @k ORDER BY V.SAVED_UTC DESC, V.VER_ID DESC", new List<SqlParameter> { App.P("k", key, 30) });
            if (emsg != string.Empty)
            {
                return list;
            }
            foreach (DataRow r in dt.Rows)
            {
                list.Add(new PageVersion { Id = Convert.ToString(r["VER_ID"]) ?? string.Empty, Blocks = Parse(Convert.ToString(r["BLOCKS"])), When = Convert.ToDateTime(r["SAVED_UTC"]), By = Convert.ToString(r["SAVED_BY"]) ?? string.Empty, ByName = Convert.ToString(r["WHO"]) ?? string.Empty });
            }
            return list;
        }

        public static async Task<string> Save(string staffId, PageSpec p, List<Block> list, DateTime? baseUtc)
        {
            Ensure(p, list);
            var (dt, emsg) = await Helper.GetDataTable("SELECT COUNT(*) FROM PAGEDOC WHERE PAGE_KEY = @k", new List<SqlParameter> { App.P("k", p.Key, 30) });
            if (emsg != string.Empty)
            {
                return "The page could not be saved. Try again.";
            }
            string result;
            if (Convert.ToInt32(dt.Rows[0][0]) == 0)
            {
                result = await Helper.PutData(new List<string> { "INSERT INTO PAGEDOC (PAGE_KEY, BLOCKS, SAVED_UTC, SAVED_BY) VALUES (@k, @b, SYSUTCDATETIME(), @by)" }, new List<SqlParameter> { App.P("k", p.Key, 30), App.P("b", Json(list), -1), App.P("by", staffId, 50) });
            }
            else
            {
                result = await Helper.PutData(new List<string>
                {
                    "UPDATE PAGEDOC SET SAVED_BY = SAVED_BY WHERE PAGE_KEY = @k",
                    "IF @base IS NOT NULL AND NOT EXISTS (SELECT 1 FROM PAGEDOC WHERE PAGE_KEY = @k AND SAVED_UTC >= DATEADD(MILLISECOND, -2, @base) AND SAVED_UTC <= DATEADD(MILLISECOND, 2, @base)) THROW 50002, N'STATECHANGED', 1;",
                    "INSERT INTO PAGEVER (VER_ID, PAGE_KEY, BLOCKS, SAVED_UTC, SAVED_BY) SELECT @vid, PAGE_KEY, BLOCKS, SAVED_UTC, SAVED_BY FROM PAGEDOC WHERE PAGE_KEY = @k",
                    "UPDATE PAGEDOC SET BLOCKS = @b, SAVED_UTC = SYSUTCDATETIME(), SAVED_BY = @by WHERE PAGE_KEY = @k",
                    "DELETE FROM PAGEVER WHERE PAGE_KEY = @k AND VER_ID NOT IN (SELECT TOP 20 VER_ID FROM PAGEVER WHERE PAGE_KEY = @k ORDER BY SAVED_UTC DESC, VER_ID DESC)"
                }, new List<SqlParameter> { App.P("k", p.Key, 30), App.P("b", Json(list), -1), App.P("by", staffId, 50), App.P("vid", App.NewId(), 50), App.PDate("base", baseUtc) });
            }
            if (Helper.IsError(result))
            {
                return result.Contains("STATECHANGED", StringComparison.Ordinal) ? "Someone else saved this page while you were working on it. Reload the page to see their changes, then make yours again." : "The page could not be saved. Try again.";
            }
            Site.Forget();
            return string.Empty;
        }

        public static async Task<string> Restore(string staffId, PageSpec p, string verId, DateTime? baseUtc)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT BLOCKS FROM PAGEVER WHERE VER_ID = @v AND PAGE_KEY = @k", new List<SqlParameter> { App.P("v", verId, 50), App.P("k", p.Key, 30) });
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                return "That version is no longer kept. Open Versions again.";
            }
            List<Block> list = Parse(Convert.ToString(dt.Rows[0]["BLOCKS"]));
            list = list.Where(b => b.Type != "data" || p.Data.Contains(b.Source)).ToList();
            Ensure(p, list);
            SiteInfo site = await Site.Load(true);
            if (site.Partial)
            {
                return "The page could not be checked right now. Try again in a moment.";
            }
            HashSet<string> media = site.Media.Where(m => m.Kind != "doc").Select(m => m.Full).ToHashSet(StringComparer.Ordinal);
            foreach (Block b in list)
            {
                if (b.Media != string.Empty && !media.Contains(b.Media))
                {
                    b.Media = string.Empty;
                }
                foreach (BlockItem it in b.Items.Where(i => i.Media != string.Empty && !media.Contains(i.Media)))
                {
                    it.Media = string.Empty;
                }
            }
            (List<Block>? clean, string err) = Clean(site, p, Json(list));
            if (clean == null)
            {
                return err;
            }
            return await Save(staffId, p, clean, baseUtc);
        }

        public static async Task<List<string>> Uses(string src, string kind = "", string title = "", string tags = "")
        {
            List<string> u = new List<string>();
            if (src == string.Empty)
            {
                return u;
            }
            string sql = "SELECT N'Team ' + TEAM_NAME AS U FROM TEAM WHERE IMAGE = @src UNION ALL SELECT N'Booking type ' + BOOK_NAME FROM BOOKTYPE WHERE IMAGE = @src UNION ALL SELECT N'Staff ' + STAFF_NAME FROM STAFF WHERE PHOTO = @src UNION ALL SELECT N'Room type ' + TYPE_NAME FROM ROOMTYPE WHERE IMAGE = @src UNION ALL SELECT N'Pet ' + PET_NAME FROM PET WHERE PHOTO = @src UNION ALL SELECT N'Album ' + TITLE FROM ALBUM A WHERE A.COVER = @src OR EXISTS (SELECT 1 FROM ALBUMITEM I WHERE I.ALBUM_ID = A.ALBUM_ID AND I.FULL_PATH = @src)";
            var (dt, emsg) = await Helper.GetDataTable(sql, new List<SqlParameter> { App.P("src", src, 300) });
            if (emsg != string.Empty)
            {
                u.Add("Could not check where it is used");
                return u;
            }
            foreach (DataRow r in dt.Rows)
            {
                u.Add(Convert.ToString(r[0]) ?? string.Empty);
            }
            List<string> tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if ((kind == "video" || kind == "doc") && tagList.Contains("media-center"))
            {
                u.Add("Page Media");
            }
            if (kind == "doc" && title.Contains("surgery", StringComparison.OrdinalIgnoreCase))
            {
                u.Add("Drop-off service pages");
            }
            if (kind == "doc" && title.Contains("vaccine", StringComparison.OrdinalIgnoreCase))
            {
                u.Add("Page Pet owners");
            }
            var (pdt, perr) = await Helper.GetDataTable("SELECT PAGE_KEY, BLOCKS FROM PAGEDOC WHERE BLOCKS LIKE @like", new List<SqlParameter> { App.P("like", "%" + src.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%", 400) });
            if (perr == string.Empty)
            {
                foreach (DataRow r in pdt.Rows)
                {
                    List<Block> list = Parse(Convert.ToString(r["BLOCKS"]));
                    if (list.Any(b => b.Media == src || b.Items.Any(i => i.Media == src)))
                    {
                        PageSpec? p = PageOf(Convert.ToString(r["PAGE_KEY"]) ?? string.Empty);
                        u.Add("Page " + (p != null ? p.Name : Convert.ToString(r["PAGE_KEY"])));
                    }
                }
            }
            return u.Distinct().ToList();
        }

        public static bool IsDoc(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "\\.pdf$", RegexOptions.IgnoreCase);
        }

        public static bool IsVideo(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "\\.(mp4|webm)$", RegexOptions.IgnoreCase);
        }

        private static bool Exists(string src)
        {
            return File.Exists(Path.Combine(App.Root, src.Replace('/', Path.DirectorySeparatorChar)));
        }

        public static bool SafeImage(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..") && Exists(src);
        }

        public static bool SafeVideo(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(mp4|webm)$") && !src!.Contains("..") && Exists(src);
        }

        public static bool SafeDoc(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.pdf$") && !src!.Contains("..") && Exists(src);
        }

        public static string Thumb(string full)
        {
            return full.Contains("/full/", StringComparison.Ordinal) ? full.Replace("/full/", "/thumbs/") : full;
        }

        public static string ThumbOf(SiteInfo site, string src)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = site.MediaOf(src);
            return m != null && m.Thumb != string.Empty ? m.Thumb : IsVideo(src) || IsDoc(src) ? string.Empty : Thumb(src);
        }

        public static string Poster(SiteInfo site, string src)
        {
            return ThumbOf(site, src);
        }

        public static string Svg(string inner, int size = 22)
        {
            return "<svg viewBox=\"0 0 24 24\" width=\"" + size + "\" height=\"" + size + "\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">" + inner + "</svg>";
        }

        public static string Icon(string k)
        {
            return k switch
            {
                "paw" => Svg("<ellipse cx=\"12\" cy=\"15.6\" rx=\"4.3\" ry=\"3.7\"/><circle cx=\"6.3\" cy=\"10.6\" r=\"1.9\"/><circle cx=\"9.7\" cy=\"6.4\" r=\"1.9\"/><circle cx=\"14.3\" cy=\"6.4\" r=\"1.9\"/><circle cx=\"17.7\" cy=\"10.6\" r=\"1.9\"/>"),
                "dog" => Svg("<path d=\"M8 5.5L4.5 7 4 12.5l3 1M16 5.5L19.5 7l.5 5.5-3 1\"/><path d=\"M8 5.5h8l1 7.5c0 3.5-2.2 6-5 6s-5-2.5-5-6z\"/><path d=\"M11 15h2l-1 1.2z\"/><circle cx=\"10\" cy=\"11\" r=\".5\" fill=\"currentColor\"/><circle cx=\"14\" cy=\"11\" r=\".5\" fill=\"currentColor\"/>"),
                "cat" => Svg("<path d=\"M5.5 11.5L5 4l4.2 3.2h5.6L19 4l-.5 7.5c0 4.4-2.9 7.5-6.5 7.5s-6.5-3.1-6.5-7.5z\"/><path d=\"M11 14.5h2l-1 1zM3.5 13.5l3 .6M3.5 16.4l3-.6M20.5 13.5l-3 .6M20.5 16.4l-3-.6\"/><circle cx=\"9.5\" cy=\"11.5\" r=\".5\" fill=\"currentColor\"/><circle cx=\"14.5\" cy=\"11.5\" r=\".5\" fill=\"currentColor\"/>"),
                "heart" => Svg("<path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/>"),
                "tooth" => Svg("<path d=\"M7.5 3.5c-2.6 0-4 2-4 4.6 0 2.4 1.2 3.9 1.7 5.9.7 2.7.8 6.5 2.6 6.5 1.7 0 1.6-4.6 4.2-4.6s2.5 4.6 4.2 4.6c1.8 0 1.9-3.8 2.6-6.5.5-2 1.7-3.5 1.7-5.9 0-2.6-1.4-4.6-4-4.6-1.9 0-2.8 1.1-4.5 1.1S9.4 3.5 7.5 3.5z\"/>"),
                "syringe" => Svg("<path d=\"M17 3l4 4M19 5l-3 3M13 6l5 5M15.5 8.5L7 17l-3 1 1-3 8.5-8.5M4 20l1.5-1.5M10 10.5l1.5 1.5M8 12.5l1.5 1.5\"/>"),
                "scissors" => Svg("<circle cx=\"6\" cy=\"6.5\" r=\"2.8\"/><circle cx=\"6\" cy=\"17.5\" r=\"2.8\"/><path d=\"M8.3 8.2L20 18M8.3 15.8L20 6\"/>"),
                "bed" => Svg("<path d=\"M3 18.5V6M3 14.5h18v4M21 14.5V12a3 3 0 0 0-3-3h-7v5.5\"/><circle cx=\"7\" cy=\"11.3\" r=\"1.9\"/>"),
                "scalpel" => Svg("<path d=\"M12.5 11.5L4 20\"/><path d=\"M12.5 11.5l2.6 2.6c3.2-1.6 5.7-5.6 5.4-10.1-4 .4-6.6 3.7-8 7.5z\"/>"),
                "stethoscope" => Svg("<path d=\"M6 3.5v5a4 4 0 0 0 8 0v-5M5 3.5h2M13 3.5h2\"/><path d=\"M10 12.5v2a5 5 0 0 0 10 0V12\"/><circle cx=\"20\" cy=\"10\" r=\"2\"/>"),
                "shield" => Svg("<path d=\"M12 3l7 3v5c0 4.5-3 8-7 10-4-2-7-5.5-7-10V6z\"/><path d=\"M9 12l2 2 4-4\"/>"),
                "clock" => Svg("<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/>"),
                "pin" => Svg("<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/>"),
                "cal" => Svg("<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/>"),
                "check" => Svg("<path d=\"M5 12.5l4.5 4.5L19 7.5\"/>"),
                "phone" => Svg("<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/>"),
                "mail" => Svg("<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/>"),
                "doc" => Svg("<path d=\"M7 3h7l4 4v14H7z\"/><path d=\"M14 3v4h4M10 12h5M10 16h5\"/>"),
                "star" => Svg("<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/>"),
                "users" => Svg("<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/>"),
                _ => string.Empty
            };
        }

        public static (int W, int H) Dims(MediaItem? m, string src)
        {
            if (m != null && m.Width > 0 && m.Height > 0)
            {
                return (m.Width, m.Height);
            }
            return src.Contains("/people/", StringComparison.Ordinal) ? (480, 600) : src.Contains("/pets/", StringComparison.Ordinal) ? (600, 600) : (1600, 1067);
        }

        public static string Img(SiteInfo site, string src, string alt, string cls, bool thumb, bool eager)
        {
            if (src == string.Empty || !SafeImage(src))
            {
                return string.Empty;
            }
            MediaItem? m = site.MediaOf(src);
            string use = src;
            (int w, int h) = Dims(m, src);
            if (thumb)
            {
                string t = ThumbOf(site, src);
                if (t != src && SafeImage(t))
                {
                    use = t;
                    if (w > 640)
                    {
                        h = (int)Math.Round(h * 640.0 / w);
                        w = 640;
                    }
                }
            }
            string a = m != null && m.Alt != string.Empty ? m.Alt : alt;
            return "<img" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + " src=\"" + Ui.Attr(use) + "\" alt=\"" + Ui.Attr(a) + "\" width=\"" + w + "\" height=\"" + h + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        public static string Mv(SiteInfo site, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            if (!IsVideo(src))
            {
                return Img(site, src, alt, string.Empty, false, eager);
            }
            if (!SafeVideo(src))
            {
                return string.Empty;
            }
            MediaItem? m = site.MediaOf(src);
            string poster = Poster(site, src);
            (int w, int h) = Dims(m, src);
            return "<video src=\"" + Ui.Attr(src) + "\"" + (poster != string.Empty && SafeImage(poster) ? " poster=\"" + Ui.Attr(poster) + "\"" : string.Empty) + " width=\"" + w + "\" height=\"" + h + "\" playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"none\" aria-label=\"" + Ui.Attr(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"") + "></video>";
        }

        public static string Face(string p, SiteInfo site, Clinician c)
        {
            string img = Img(site, c.Photo, c.Name, p + "-face", false, false);
            return img != string.Empty ? img : "<span class=\"" + p + "-face " + p + "-noface\" style=\"--h:" + Ui.Hue(c.Id) + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(c.Name)) + "</span>";
        }

        public static string Chips(string p, SiteInfo site, IEnumerable<string> species)
        {
            List<string> l = species.ToList();
            if (l.Count == 0)
            {
                return "<span class=\"" + p + "-chipx\">All animals</span>";
            }
            return string.Concat(l.Select(s => "<span class=\"" + p + "-chipx\">" + Ui.Enc(site.SpeciesName(s)) + "</span>"));
        }

        public static string Lines(string v)
        {
            return Ui.Enc(v).Replace("\n", "<br>");
        }

        public static string Btn(string p, SiteInfo site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"" + p + "-btn " + (main ? p + "-btn-accent" : p + "-btn-ghost") + "\" href=\"" + Ui.Attr(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Enc(Site.Tok(site, label)) + "</a>";
        }

        public static string Btns(string p, SiteInfo site, Block b)
        {
            string x = Btn(p, site, b.Label, b.Link, true) + Btn(p, site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"" + p + "-acts\">" + x + "</div>";
        }

        public static string Kick(string p, SiteInfo site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<span class=\"" + p + "-kick\">" + Ui.Enc(Site.Tok(site, b.Kicker)) + "</span>";
        }

        public static string H2(string p, SiteInfo site, Block b)
        {
            return b.Title == string.Empty ? string.Empty : "<h2 class=\"" + p + "-h2\">" + Ui.Enc(Site.Tok(site, b.Title)) + "</h2>";
        }

        public static string Rich(string p, SiteInfo site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"" + p + "-rich" + (cls != string.Empty ? " " + cls : string.Empty) + "\">" + Site.TokH(site, html) + "</div>";
        }

        public static string Head(string p, SiteInfo site, Block b, bool lead)
        {
            string x = Kick(p, site, b) + H2(p, site, b) + (lead ? Rich(p, site, b.Text, p + "-lead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"" + p + "-bhead\">" + x + "</div>";
        }

        public static string HeadMore(string p, SiteInfo site, Block b, string href, string label)
        {
            return "<div class=\"" + p + "-shead\"><div>" + Kick(p, site, b) + H2(p, site, b) + Rich(p, site, b.Text, p + "-lead") + "</div><a class=\"" + p + "-more\" href=\"" + href + "\">" + label + " &rarr;</a></div>";
        }

        public static string Sec(string p, Block b, string inner, string cls = "")
        {
            return "<section class=\"" + p + "-sec" + (cls != string.Empty ? " " + cls : string.Empty) + "\" id=\"" + Ui.Attr(b.Id) + "\"><div class=\"" + p + "-wrap\">" + inner + "</div></section>";
        }

        public static string Hero(string p, SiteInfo site, Block b, bool h1, string extraHtml)
        {
            bool find = b.Extra == "finder" && extraHtml != string.Empty;
            string tag = h1 ? "h1" : "h2";
            string media = Mv(site, b.Media, Site.Tok(site, b.Title), IsVideo(b.Media), h1);
            return "<section class=\"" + p + "-hero" + (find ? " " + p + "-hfind" : string.Empty) + (media == string.Empty ? " " + p + "-hplain" : string.Empty) + "\" id=\"" + Ui.Attr(b.Id) + "\"><div class=\"" + p + "-wrap " + p + "-hero-in\"><div class=\"" + p + "-hero-t\">" + Kick(p, site, b) + "<" + tag + " class=\"" + p + "-htitle\">" + Ui.Enc(Site.Tok(site, b.Title)) + "</" + tag + ">" + Rich(p, site, b.Text, p + "-lead") + Btns(p, site, b) + (find ? extraHtml : string.Empty) + "</div>" + (media != string.Empty ? "<div class=\"" + p + "-hero-p\">" + media + "</div>" : string.Empty) + "</div></section>";
        }

        public static string Split(string p, SiteInfo site, Block b)
        {
            string counts = b.Extra == "counts" ? "<div class=\"" + p + "-nums\"><span><b>" + site.Providers.Count(x => x.Active && x.Role == "vet") + "</b>vets</span><span><b>" + site.Services.Count(x => x.Active) + "</b>services</span><span><b>" + Ui.Num(site.ClientCount) + "</b>clients</span></div>" : string.Empty;
            string media = Mv(site, b.Media, Site.Tok(site, b.Title), false, false);
            string btn = Btn(p, site, b.Label, b.Link, false);
            return Sec(p, b, "<div class=\"" + p + "-bsplit" + (b.Flip ? " " + p + "-flip" : string.Empty) + (media == string.Empty ? " " + p + "-nomedia" : string.Empty) + "\">" + (media != string.Empty ? "<div class=\"" + p + "-bsm\">" + media + "</div>" : string.Empty) + "<div>" + Kick(p, site, b) + H2(p, site, b) + Rich(p, site, b.Text, p + "-lead") + counts + (btn != string.Empty ? "<div class=\"" + p + "-acts\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        public static string Cards(string p, SiteInfo site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6 || b.Items.Count == 5)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(p, site, b, true) + "<div class=\"" + p + "-bcards " + p + "-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"" + p + "-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Enc(Site.Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Site.Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"" + p + "-bcard\" href=\"" + Ui.Attr(it.Link) + "\">" + inner + "<span class=\"" + p + "-more\">" + Ui.Enc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"" + p + "-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(p, b, sb.ToString());
        }

        public static string Steps(string p, SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(p, site, b, true) + "<ol class=\"" + p + "-bsteps " + p + "-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><span>" + i++ + "</span><b>" + Ui.Enc(Site.Tok(site, it.Title)) + "</b><p>" + Lines(Site.Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(p, b, sb.ToString());
        }

        public static string StatRow(string p, SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(p, site, b, false) + "<div class=\"" + p + "-bstats " + p + "-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Enc(Site.Tok(site, it.Title)) + "</b><span>" + Lines(Site.Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(p, b, sb.ToString());
        }

        public static string People(string p, SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(p, site, b, true) + "<div class=\"" + p + "-bpeople " + p + "-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                string img = it.Media != string.Empty && !IsVideo(it.Media) ? Img(site, it.Media, it.Title, string.Empty, false, false) : string.Empty;
                sb.Append("<div class=\"" + p + "-person\">" + (img != string.Empty ? img : "<span class=\"" + p + "-noface\" style=\"--h:" + Ui.Hue(it.Title) + "\" aria-hidden=\"true\">" + Ui.Enc(Ui.Initials(it.Title)) + "</span>") + "<b>" + Ui.Enc(Site.Tok(site, it.Title)) + "</b><span>" + Lines(Site.Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(p, b, sb.ToString());
        }

        public static string Boxes(string p, SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(p, site, b, false) + "<div class=\"" + p + "-bboxes " + p + "-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"" + p + "-box\">" + (it.Title != string.Empty ? "<h3>" + Ui.Enc(Site.Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"" + p + "-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Enc(Site.Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(p, b, sb.ToString());
        }

        public static string Faq(string p, SiteInfo site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"" + p + "-bfaq\">" + Head(p, site, b, false) + "<div class=\"" + p + "-faq\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details><summary>" + Ui.Enc(Site.Tok(site, it.Title)) + "</summary><p>" + Lines(Site.Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(p, b, sb.ToString());
        }

        public static string Quote(string p, SiteInfo site, Block b)
        {
            string img = b.Media != string.Empty && !IsVideo(b.Media) ? Img(site, b.Media, b.Title, string.Empty, true, false) : string.Empty;
            return Sec(p, b, "<figure class=\"" + p + "-bquote\">" + img + "<blockquote>" + Lines(Site.Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Enc(Site.Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        public static string Wide(string p, SiteInfo site, Block b)
        {
            string media = Mv(site, b.Media, b.Text, false, false);
            return media == string.Empty ? string.Empty : Sec(p, b, "<figure class=\"" + p + "-bmedia\">" + media + (b.Text != string.Empty ? "<figcaption>" + Ui.Enc(Site.Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        public static string Cta(string p, SiteInfo site, Block b)
        {
            return Sec(p, b, "<div class=\"" + p + "-bcta\"><div>" + Kick(p, site, b) + H2(p, site, b) + Rich(p, site, b.Text, p + "-lead") + "</div>" + Btns(p, site, b) + "</div>");
        }

        public static string TextBlock(string p, SiteInfo site, Block b)
        {
            return Sec(p, b, "<div class=\"" + p + "-btext\">" + Kick(p, site, b) + H2(p, site, b) + Rich(p, site, b.Text, string.Empty) + "</div>");
        }

        public static string Render(string p, SiteInfo site, Block b, bool h1, Func<Block, string> data, Func<Block, string>? heroExtra = null)
        {
            return b.Type switch
            {
                "hero" => Hero(p, site, b, h1, heroExtra != null ? heroExtra(b) : string.Empty),
                "text" => TextBlock(p, site, b),
                "split" => Split(p, site, b),
                "cards" => Cards(p, site, b),
                "steps" => Steps(p, site, b),
                "stats" => StatRow(p, site, b),
                "people" => People(p, site, b),
                "boxes" => Boxes(p, site, b),
                "faq" => Faq(p, site, b),
                "quote" => Quote(p, site, b),
                "media" => Wide(p, site, b),
                "cta" => Cta(p, site, b),
                "data" => data(b),
                _ => string.Empty
            };
        }

        public static string RenderPage(string p, SiteInfo site, string pageKey, string fallbackH1, Func<Block, string> data, Func<Block, string>? heroExtra = null)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (Block b in site.BlocksOf(pageKey).Where(x => !x.Hidden))
            {
                sb.Append(Render(p, site, b, first && b.Type == "hero", data, heroExtra));
                if (b.Type == "hero")
                {
                    first = false;
                }
            }
            if (first)
            {
                sb.Insert(0, "<h1 class=\"" + p + "-sr\">" + Ui.Enc(fallbackH1) + "</h1>");
            }
            return sb.ToString();
        }
    }
}
