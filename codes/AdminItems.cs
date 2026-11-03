using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminItems : WebPage
    {
        private const string Key = "items";
        private const string Me = "AdminItemsJs";
        private const int Size = 30;
        private const int MaxBulk = 200;
        private const string Cols = "I.ITEM_ID AS ItemId, I.ITEM_CODE AS ItemCode, I.ITEM_NAME AS ItemName, I.ITEM_TYPE AS ItemType, I.CATEGORY AS Category, I.UNIT AS Unit, I.PRICE AS Price, I.PRICE_M AS PriceM, I.PRICE_L AS PriceL, I.TAXABLE AS Taxable, I.DOSE AS Dose, I.INSTRUCTIONS AS Instructions, I.VAX_MONTHS AS VaxMonths, I.SPECIES AS Species, I.TRACK_STOCK AS TrackStock, I.ITEM_ORDER AS ItemOrder, I.NOTE AS Note, I.ACTIVE AS Active";

        private static readonly (string Key, string Tab, string One)[] Types =
        {
            ("practice", "Exams & procedures", "Exam or procedure"),
            ("medicine", "Medicines", "Medicine"),
            ("test", "Tests", "Test"),
            ("vaccine", "Vaccines", "Vaccine"),
            ("grooming", "Grooming", "Grooming"),
            ("boarding", "Boarding", "Boarding"),
            ("product", "Products", "Product"),
            ("fee", "Fees", "Fee")
        };

        private static readonly string[] UnitList = { "each", "tablet", "capsule", "ml", "dose", "injection", "bottle", "tube", "bag", "pack", "night", "day", "tooth", "hour", "visit" };

        private sealed class Flt
        {
            public string Type { get; set; } = string.Empty;
            public string Cat { get; set; } = string.Empty;
            public string Term { get; set; } = string.Empty;
            public string Show { get; set; } = "active";
            public int Pg { get; set; } = 1;
        }

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Items and prices | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--it-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("it"));
        }

        private static string CleanId(string? v)
        {
            string t = (v ?? string.Empty).Trim();
            return Regex.IsMatch(t, "^[A-Za-z0-9_-]{1,50}$") ? t : string.Empty;
        }

        private string Val(string key, int max)
        {
            return Ui.Clip(GetDataValue(key), max);
        }

        private string NewKey()
        {
            string k = Val("newid", 40);
            return Regex.IsMatch(k, "^[a-f0-9]{32}$") ? k : string.Empty;
        }

        private static decimal? Dec(string? v)
        {
            string t = (v ?? string.Empty).Trim().Replace("$", string.Empty).Trim();
            if (t == string.Empty)
            {
                return 0m;
            }
            if (!Regex.IsMatch(t, "^(\\d{1,3}(,\\d{3})*|\\d+)(\\.\\d{1,2})?$|^\\.\\d{1,2}$"))
            {
                return null;
            }
            return decimal.TryParse(t.Replace(",", string.Empty), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal d) ? d : null;
        }

        private static string M2(decimal v)
        {
            return v.ToString("0.00", Ui.Inv);
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

        private static void Toast(ApiResponse response, string msg)
        {
            response.ExecuteScript(Me + ".unlock();" + Me + ".toast('" + Ui.Js(msg) + "');");
        }

        private static string Denied()
        {
            return "<div class=\"it-panel it-deny\"><h1>Items and prices</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"it-btn it-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        private static string TypeTab(string key)
        {
            foreach (var t in Types)
            {
                if (t.Key == key)
                {
                    return t.Tab;
                }
            }
            return key;
        }

        private static string TypeOne(string key)
        {
            foreach (var t in Types)
            {
                if (t.Key == key)
                {
                    return t.One;
                }
            }
            return key;
        }

        private static bool IsType(string key)
        {
            return Types.Any(t => t.Key == key);
        }

        private Flt ReadFilter()
        {
            Flt f = new Flt();
            string typ = Val("ityp", 20);
            f.Type = IsType(typ) ? typ : string.Empty;
            f.Cat = Ui.OneLine(GetDataValue("icat"), 60);
            f.Term = Ui.OneLine(GetDataValue("term"), 80);
            string show = Val("show", 10);
            f.Show = show == "inactive" || show == "all" ? show : "active";
            int.TryParse(GetDataValue("pg"), out int pg);
            f.Pg = Math.Max(1, pg);
            return f;
        }

        private static string Like(string t)
        {
            return "%" + t.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        }

        private static string Where(Flt f)
        {
            string w = " WHERE (@typ = N'' OR I.ITEM_TYPE = @typ) AND (@cat = N'' OR I.CATEGORY = @cat) AND (@t = N'' OR I.ITEM_NAME LIKE @tl OR I.ITEM_CODE LIKE @tl)";
            if (f.Show == "active")
            {
                w += " AND I.ACTIVE = 1";
            }
            else if (f.Show == "inactive")
            {
                w += " AND I.ACTIVE = 0";
            }
            return w;
        }

        private static List<SqlParameter> FPs(Flt f)
        {
            return new List<SqlParameter> { App.P("typ", f.Type, 20), App.P("cat", f.Cat, 60), App.P("t", f.Term, 80), App.P("tl", Like(f.Term), 300) };
        }

        private const string Order = " ORDER BY CASE I.ITEM_TYPE WHEN N'practice' THEN 1 WHEN N'medicine' THEN 2 WHEN N'test' THEN 3 WHEN N'vaccine' THEN 4 WHEN N'grooming' THEN 5 WHEN N'boarding' THEN 6 WHEN N'product' THEN 7 ELSE 8 END, I.ITEM_ORDER, I.ITEM_NAME, I.ITEM_ID";

        private static async Task<List<ItmSpecies>> SpeciesList()
        {
            var (list, emsg) = await Helper.GetListT<ItmSpecies>("SELECT SPECIES_KEY AS SpeciesKey, SPECIES_NAME AS SpeciesName, HAS_SIZE AS HasSize FROM SPECIES WHERE ACTIVE = 1 ORDER BY SPECIES_ORDER, SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<ItmSpecies>();
        }

        private static string SpeciesText(string csv, List<ItmSpecies> sp)
        {
            List<string> keys = (csv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (keys.Count == 0)
            {
                return "All";
            }
            return string.Join(", ", keys.Select(k => sp.FirstOrDefault(x => x.SpeciesKey == k)?.SpeciesName ?? k));
        }

        private static async Task<ItmRow?> Item(string id)
        {
            var (row, emsg) = await Helper.GetOneT<ItmRow>("SELECT " + Cols + " FROM ITEM I WHERE I.ITEM_ID = @iid", new List<SqlParameter> { App.P("iid", id, 50) });
            return emsg == string.Empty ? row : null;
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
            response.SetElementContents("it-menu", Auth.Menu("it", Me, "AdminItems", c));
            response.SetElementContents("it-who", Auth.Who("it", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("it-main", Denied());
                return response;
            }
            Flt f = new Flt();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"it-ptop\"><div><h1>Items and prices</h1><p>Everything the hospital sells: exams, medicines, tests, vaccines, grooming, boarding, products and fees.</p></div><div class=\"it-acts\">");
            if (c.Can(Key, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"it-btn it-btn-ghost\" id=\"it-pbtn\" onclick=\"" + Me + ".prices(this)\">Change prices</button>");
            }
            sb.Append("<button type=\"button\" class=\"it-btn it-btn-ghost\" id=\"it-csvb\" onclick=\"" + Me + ".csv(this)\">Download CSV</button>");
            if (c.Can(Key, 'A'))
            {
                sb.Append("<button type=\"button\" class=\"it-btn it-btn-accent\" onclick=\"" + Me + ".add(this)\">Add item</button>");
            }
            sb.Append("</div></div>");
            sb.Append("<div class=\"it-tabs\" id=\"it-tabs\" role=\"tablist\">" + await TabsHtml(f) + "</div>");
            sb.Append("<div class=\"it-bar\"><label class=\"it-field it-grow2\"><span>Search</span><input type=\"search\" id=\"it-term\" maxlength=\"80\" placeholder=\"Name or code\" autocomplete=\"off\" oninput=\"" + Me + ".typed()\" onkeydown=\"if(event.key==='Enter'){event.preventDefault();" + Me + ".find(1);}\"></label>");
            sb.Append("<label class=\"it-field\"><span>Category</span><div class=\"it-fsp\" id=\"it-catw\">" + await CatSelect(f) + "</div></label>");
            sb.Append("<label class=\"it-field\"><span>Show</span><select id=\"it-show\" onchange=\"" + Me + ".find(1)\"><option value=\"active\">Active items</option><option value=\"inactive\">Inactive items</option><option value=\"all\">All items</option></select></label></div>");
            sb.Append("<div id=\"it-list\">" + await ListHtml(c, f) + "</div><div id=\"it-csvall\" hidden></div>");
            response.SetElementContents("it-main", sb.ToString());
            response.ExecuteScript(Me + ".ready();");
            return response;
        }

        private static async Task<string> TabsHtml(Flt f)
        {
            var (counts, _) = await Helper.GetListT<ItmCount>("SELECT I.ITEM_TYPE AS ItemType, COUNT(*) AS Cnt FROM ITEM I WHERE (@show = N'all' OR (@show = N'active' AND I.ACTIVE = 1) OR (@show = N'inactive' AND I.ACTIVE = 0)) GROUP BY I.ITEM_TYPE", new List<SqlParameter> { App.P("show", f.Show, 10) });
            List<ItmCount> list = counts ?? new List<ItmCount>();
            StringBuilder sb = new StringBuilder();
            sb.Append("<button type=\"button\" role=\"tab\" aria-selected=\"" + (f.Type == string.Empty ? "true" : "false") + "\" class=\"it-tab" + (f.Type == string.Empty ? " it-on" : string.Empty) + "\" onclick=\"" + Me + ".tab('')\">All <small>" + list.Sum(x => x.Cnt) + "</small></button>");
            foreach (var t in Types)
            {
                int n = list.Where(x => x.ItemType == t.Key).Sum(x => x.Cnt);
                bool on = f.Type == t.Key;
                sb.Append("<button type=\"button\" role=\"tab\" aria-selected=\"" + (on ? "true" : "false") + "\" class=\"it-tab" + (on ? " it-on" : string.Empty) + "\" onclick=\"" + Me + ".tab('" + t.Key + "')\">" + Ui.Enc(t.Tab) + " <small>" + n + "</small></button>");
            }
            return sb.ToString();
        }

        private static async Task<string> CatSelect(Flt f)
        {
            var (dt, _) = await Helper.GetDataTable("SELECT DISTINCT I.CATEGORY FROM ITEM I WHERE I.CATEGORY <> N'' AND (@typ = N'' OR I.ITEM_TYPE = @typ) ORDER BY I.CATEGORY", new List<SqlParameter> { App.P("typ", f.Type, 20) });
            List<(string, string)> opts = new List<(string, string)> { (string.Empty, "All categories") };
            foreach (System.Data.DataRow r in dt.Rows)
            {
                string v = Convert.ToString(r[0]) ?? string.Empty;
                opts.Add((v, v));
            }
            if (f.Cat != string.Empty && !opts.Any(o => o.Item1 == f.Cat))
            {
                opts.Add((f.Cat, f.Cat));
            }
            return "<select id=\"it-cat\" onchange=\"" + Me + ".find(1)\">" + Ui.Opts(opts, f.Cat) + "</select>";
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
                response.SetElementContents("it-main", Denied());
                return response;
            }
            Flt f = ReadFilter();
            if ((GetDataValue("retab") ?? string.Empty).Trim() == "1")
            {
                response.SetElementContents("it-tabs", await TabsHtml(f));
                response.SetElementContents("it-catw", await CatSelect(f));
            }
            response.SetElementContents("it-list", await ListHtml(c, f));
            response.ExecuteScript(Me + ".ready();");
            return response;
        }

        private static string PriceCell(ItmRow r)
        {
            string main = "<b>" + Ui.Money(r.Price) + "</b><small class=\"it-pp\">" + (r.Unit == "each" ? "each" : "per " + Ui.Enc(r.Unit)) + (r.Taxable ? " &middot; taxable" : string.Empty) + "</small>";
            List<string> more = new List<string>();
            if (r.PriceM > 0)
            {
                more.Add("M " + Ui.Money(r.PriceM));
            }
            if (r.PriceL > 0)
            {
                more.Add("L " + Ui.Money(r.PriceL));
            }
            return main + (more.Count > 0 ? "<small class=\"it-pp\">" + string.Join(" &middot; ", more) + "</small>" : string.Empty);
        }

        private static async Task<string> ListHtml(Ctx c, Flt f)
        {
            string where = Where(f);
            var (cnt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ITEM I" + where, FPs(f));
            int total = cerr == string.Empty && cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            int page = Ui.PageOf(f.Pg.ToString(Ui.Inv), Size, total);
            List<SqlParameter> ps = FPs(f);
            ps.Add(App.PInt("skip", (page - 1) * Size));
            ps.Add(App.PInt("take", Size));
            var (rows, emsg) = await Helper.GetListT<ItmRow>("SELECT " + Cols + " FROM ITEM I" + where + Order + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", ps);
            if (emsg != string.Empty || rows == null)
            {
                return "<div class=\"it-panel\"><p>The item list could not be loaded. Try again in a moment.</p></div>";
            }
            if (rows.Count == 0)
            {
                string none = f.Term != string.Empty ? "No item matches &ldquo;" + Ui.Enc(f.Term) + "&rdquo;." : f.Show == "inactive" ? "There are no inactive items here." : "There are no items here yet.";
                return "<div class=\"it-panel it-empty\"><p>" + none + "</p></div>";
            }
            List<ItmSpecies> sp = await SpeciesList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"it-tw\"><table class=\"it-table\"><thead><tr><th>Code</th><th>Name</th><th>Type</th><th>Category</th><th class=\"it-num\">Price</th><th>Species</th></tr></thead><tbody>");
            foreach (ItmRow r in rows)
            {
                string js = Me + ".open('" + Ui.Js(r.ItemId) + "')";
                sb.Append("<tr class=\"it-row" + (r.Active ? string.Empty : " it-off") + "\" tabindex=\"0\" onclick=\"" + js + "\" onkeydown=\"if(event.key==='Enter'){" + js + ";}\">");
                sb.Append("<td data-l=\"Code\" class=\"it-nw it-code\">" + Ui.Enc(r.ItemCode) + "</td>");
                sb.Append("<td class=\"it-cname\"><b>" + Ui.Enc(r.ItemName) + (r.Active ? string.Empty : " <span class=\"it-st it-st-off\">Inactive</span>") + "</b><small class=\"it-mcode\">" + Ui.Enc(r.ItemCode) + "</small>" + (r.Note != string.Empty ? "<small>" + Ui.Enc(r.Note) + "</small>" : r.Dose != string.Empty ? "<small>Dose: " + Ui.Enc(r.Dose) + "</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Type\" class=\"it-nw\"><span class=\"it-v\">" + Ui.Enc(TypeOne(r.ItemType)) + (r.ItemType == "vaccine" && r.VaxMonths > 0 ? "<small class=\"it-pp\">every " + r.VaxMonths + " mo</small>" : string.Empty) + "</span></td>");
                sb.Append("<td data-l=\"Category\">" + (r.Category != string.Empty ? Ui.Enc(r.Category) : "<i class=\"it-dim\">None</i>") + "</td>");
                sb.Append("<td data-l=\"Price\" class=\"it-num\"><span class=\"it-v\">" + PriceCell(r) + "</span></td>");
                sb.Append("<td data-l=\"Species\">" + Ui.Enc(SpeciesText(r.Species, sp)) + "</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div>");
            sb.Append(Ui.Pager("it", page, Size, total, "item", "items", Me + ".find"));
            return sb.ToString();
        }

        private static string CsvLine(ItmRow r, List<ItmSpecies> sp)
        {
            string[] cells =
            {
                r.ItemCode, r.ItemName, TypeOne(r.ItemType), r.Category, r.Unit, M2(r.Price), r.PriceM > 0 ? M2(r.PriceM) : string.Empty, r.PriceL > 0 ? M2(r.PriceL) : string.Empty,
                r.Taxable ? "Yes" : "No", SpeciesText(r.Species, sp), r.Dose, r.Instructions, r.VaxMonths > 0 ? r.VaxMonths.ToString(Ui.Inv) : string.Empty,
                r.TrackStock ? "Yes" : "No", r.ItemOrder.ToString(Ui.Inv), r.Active ? "Active" : "Inactive", r.Note
            };
            return string.Join(",", cells.Select(Ui.Csv));
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                Toast(response, "You can't see the item list.");
                return response;
            }
            Flt f = ReadFilter();
            var (rows, emsg) = await Helper.GetListT<ItmRow>("SELECT " + Cols + " FROM ITEM I" + Where(f) + Order + " OFFSET 0 ROWS FETCH NEXT 5000 ROWS ONLY", FPs(f));
            if (emsg != string.Empty || rows == null)
            {
                Toast(response, "The list could not be loaded. Try again.");
                return response;
            }
            List<ItmSpecies> sp = await SpeciesList();
            response.SetElementContents("it-csvall", string.Concat(rows.Select(r => "<i data-csv=\"" + Ui.Attr(CsvLine(r, sp)) + "\"></i>")));
            response.ExecuteScript(Me + ".unlock();" + Me + ".csvGo();");
            return response;
        }

        private static async Task<ItmUse> Use(string id)
        {
            var (row, _) = await Helper.GetOneT<ItmUse>("SELECT (SELECT COUNT(*) FROM ORDERLINE WHERE ITEM_ID = @iid) AS Orders, (SELECT COUNT(*) FROM ORDERLINE WHERE ITEM_ID = @iid AND STATUS <> N'cancelled' AND ORDERED_UTC >= @since) AS Recent, (SELECT COUNT(*) FROM INVOICELINE WHERE ITEM_ID = @iid) AS InvLines, (SELECT COUNT(*) FROM BOOKTYPE WHERE ITEM_ID = @iid) AS Books, (SELECT COUNT(*) FROM ROOMTYPE WHERE NIGHT_ITEM_ID = @iid) AS Rooms, (SELECT COUNT(*) FROM TESTFIELD WHERE ITEM_ID = @iid) AS Fields, ISNULL((SELECT STRING_AGG(BOOK_NAME, N', ') FROM BOOKTYPE WHERE ITEM_ID = @iid), N'') AS BookNames, ISNULL((SELECT STRING_AGG(TYPE_NAME, N', ') FROM ROOMTYPE WHERE NIGHT_ITEM_ID = @iid), N'') AS RoomNames", new List<SqlParameter> { App.P("iid", id, 50), App.PDate("since", DateTime.UtcNow.AddMonths(-12)) });
            return row ?? new ItmUse { Orders = 1 };
        }

        private static List<string> TypesFor(string vt)
        {
            return vt switch
            {
                "surgery" => new List<string> { "practice" },
                "grooming" => new List<string> { "grooming" },
                _ => new List<string> { "practice", "test", "vaccine" }
            };
        }

        private static async Task<string> TypeClash(string id, string typ)
        {
            List<SqlParameter> P() => new List<SqlParameter> { App.P("iid", id, 50) };
            var (rooms, _) = await Helper.GetDataTable("SELECT TYPE_NAME FROM ROOMTYPE WHERE NIGHT_ITEM_ID = @iid ORDER BY TYPE_NAME", P());
            if (rooms.Rows.Count > 0 && typ != "boarding")
            {
                return "It is the nightly price of " + Ui.Enc(string.Join(", ", rooms.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToString(r[0])))) + ", so it must stay Boarding.";
            }
            var (fields, _) = await Helper.GetDataTable("SELECT COUNT(*) FROM TESTFIELD WHERE ITEM_ID = @iid", P());
            if (fields.Rows.Count > 0 && Convert.ToInt32(fields.Rows[0][0]) > 0 && typ != "test")
            {
                return "This test has result fields, so it must stay a Test. Remove the fields on the Test fields page first.";
            }
            var (books, _) = await Helper.GetDataTable("SELECT BOOK_NAME, VISIT_TYPE FROM BOOKTYPE WHERE ITEM_ID = @iid ORDER BY BOOK_NAME", P());
            List<string> bad = books.Rows.Cast<System.Data.DataRow>().Where(r => !TypesFor(Convert.ToString(r[1]) ?? string.Empty).Contains(typ)).Select(r => Convert.ToString(r[0]) ?? string.Empty).ToList();
            if (bad.Count > 0)
            {
                return "Booking type " + Ui.Enc(string.Join(", ", bad)) + " uses this item as its price, and the type " + Ui.Enc(TypeOne(typ)) + " doesn't fit that kind of booking. Link another item there first.";
            }
            return string.Empty;
        }

        private static async Task<string> ActiveRefs(string id)
        {
            List<SqlParameter> P() => new List<SqlParameter> { App.P("iid", id, 50) };
            var (books, _) = await Helper.GetDataTable("SELECT BOOK_NAME FROM BOOKTYPE WHERE ITEM_ID = @iid AND ACTIVE = 1 ORDER BY BOOK_NAME", P());
            var (rooms, _) = await Helper.GetDataTable("SELECT TYPE_NAME FROM ROOMTYPE WHERE NIGHT_ITEM_ID = @iid AND ACTIVE = 1 ORDER BY TYPE_NAME", P());
            List<string> parts = new List<string>();
            if (books.Rows.Count > 0)
            {
                parts.Add("Active booking type " + string.Join(", ", books.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToString(r[0]))) + " uses it as its price.");
            }
            if (rooms.Rows.Count > 0)
            {
                parts.Add("Active room type " + string.Join(", ", rooms.Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToString(r[0]))) + " uses it as the nightly price.");
            }
            return string.Join(" ", parts);
        }

        private static bool Used(ItmUse u)
        {
            return u.Orders > 0 || u.InvLines > 0 || u.Books > 0 || u.Rooms > 0 || u.Fields > 0;
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("itemid"));
            if (id == string.Empty && !c.Can(Key, 'A'))
            {
                Toast(response, "You can't add items.");
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                Toast(response, "You can't see items.");
                return response;
            }
            ItmRow? it = null;
            ItmUse? use = null;
            if (id != string.Empty)
            {
                it = await Item(id);
                if (it == null)
                {
                    Toast(response, "This item was not found. It may have been deleted.");
                    return response;
                }
                use = await Use(id);
            }
            string typ = Val("ityp", 20);
            response.SetElementContents("it-mbody", await FormHtml(c, it, use, IsType(typ) ? typ : "practice"));
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(" + (it == null ? "true" : "false") + ");");
            return response;
        }

        private static string Field(string id, string label, string value, int max, string type = "text", string extra = "", string hint = "", string attrs = "")
        {
            return "<label class=\"it-field" + (extra != string.Empty ? " " + extra : string.Empty) + "\"><span>" + label + (hint != string.Empty ? " <small>" + hint + "</small>" : string.Empty) + "</span><input type=\"" + type + "\" id=\"it-f-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\" autocomplete=\"off\"" + attrs + "><em class=\"it-err\" id=\"it-e-" + id + "\"></em></label>";
        }

        private static async Task<string> FormHtml(Ctx c, ItmRow? it, ItmUse? use, string typ)
        {
            bool add = it == null;
            bool can = add ? c.Can(Key, 'A') : c.Can(Key, 'E');
            ItmRow v = it ?? new ItmRow { ItemType = typ, Unit = "each", Active = true };
            List<ItmSpecies> sp = await SpeciesList();
            var (cats, _) = await Helper.GetDataTable("SELECT DISTINCT CATEGORY FROM ITEM WHERE CATEGORY <> N'' ORDER BY CATEGORY");
            var (units, _) = await Helper.GetDataTable("SELECT DISTINCT UNIT FROM ITEM WHERE UNIT <> N''");
            List<string> unitList = UnitList.ToList();
            foreach (System.Data.DataRow r in units.Rows)
            {
                string u = Convert.ToString(r[0]) ?? string.Empty;
                if (!unitList.Contains(u))
                {
                    unitList.Add(u);
                }
            }
            HashSet<string> mine = new HashSet<string>((v.Species ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "New item" : Ui.Enc(v.ItemName)) + "</h2>");
            if (!add)
            {
                sb.Append("<p class=\"it-mlead\">" + Ui.Enc(v.ItemCode) + " &middot; " + Ui.Enc(TypeOne(v.ItemType)) + (v.Active ? string.Empty : " &middot; <b>inactive</b>") + "</p>");
            }
            if (use != null)
            {
                sb.Append("<div class=\"it-use\"><b>Used " + Ui.Plural(use.Recent, "time", "times") + " in the last 12 months</b>");
                List<string> more = new List<string>();
                if (use.Orders > use.Recent)
                {
                    more.Add(Ui.Plural(use.Orders, "order", "orders") + " in all");
                }
                if (use.BookNames != string.Empty)
                {
                    more.Add("price of booking type " + use.BookNames);
                }
                if (use.RoomNames != string.Empty)
                {
                    more.Add("nightly price of " + use.RoomNames);
                }
                if (use.Fields > 0)
                {
                    more.Add(Ui.Plural(use.Fields, "result field", "result fields"));
                }
                if (more.Count > 0)
                {
                    sb.Append("<span>" + Ui.Enc(string.Join(" · ", more)) + "</span>");
                }
                sb.Append("</div>");
            }
            if (!can)
            {
                sb.Append("<p class=\"it-info\">You can look at this item. Ask a manager if a price or detail needs to change.</p>");
            }
            sb.Append("<form class=\"it-form\" onsubmit=\"return " + Me + ".save()\" novalidate><input type=\"hidden\" id=\"it-f-id\" value=\"" + Ui.Attr(v.ItemId) + "\"><fieldset class=\"it-fs\"" + (can ? string.Empty : " disabled") + ">");
            sb.Append("<div class=\"it-fgrid\">");
            sb.Append(Field("name", "Name", v.ItemName, 120, "text", "it-span2"));
            sb.Append(Field("code", "Code", v.ItemCode, 30, "text", string.Empty, "short and unique, like VAC-RAB", " spellcheck=\"false\" oninput=\"this.value=this.value.toUpperCase()\""));
            sb.Append("<label class=\"it-field\"><span>Type</span><select id=\"it-f-type\" onchange=\"" + Me + ".typeChg()\">" + Ui.Opts(Types.Select(t => (t.Key, t.One)), v.ItemType) + "</select><em class=\"it-err\" id=\"it-e-type\"></em></label>");
            sb.Append(Field("cat", "Category", v.Category, 60, "text", string.Empty, "pick or type a new one", " list=\"it-cats\""));
            sb.Append("<datalist id=\"it-cats\">");
            foreach (System.Data.DataRow r in cats.Rows)
            {
                sb.Append("<option value=\"" + Ui.Attr(Convert.ToString(r[0])) + "\"></option>");
            }
            sb.Append("</datalist>");
            sb.Append(Field("unit", "Unit", v.Unit, 20, "text", string.Empty, "what one price buys", " list=\"it-units\""));
            sb.Append("<datalist id=\"it-units\">" + string.Concat(unitList.Select(u => "<option value=\"" + Ui.Attr(u) + "\"></option>")) + "</datalist>");
            sb.Append("<div class=\"it-span2 it-prices\">");
            sb.Append(Field("price", "Price", M2(v.Price), 12, "text", string.Empty, string.Empty, " inputmode=\"decimal\""));
            sb.Append(Field("pm", "Medium pet price", v.PriceM > 0 ? M2(v.PriceM) : "0.00", 12, "text", string.Empty, "0 = same", " inputmode=\"decimal\""));
            sb.Append(Field("pl", "Large pet price", v.PriceL > 0 ? M2(v.PriceL) : "0.00", 12, "text", string.Empty, "0 = same", " inputmode=\"decimal\""));
            sb.Append("</div>");
            sb.Append("<label class=\"it-check it-span2\"><input type=\"checkbox\" id=\"it-f-tax\"" + (v.Taxable ? " checked" : string.Empty) + "><span>Taxable <small class=\"it-hint\">sales tax is added at checkout (usually products only)</small></span></label>");
            sb.Append("<div class=\"it-field it-span2\"><span>For which species <small>leave all unticked for every species</small></span><div class=\"it-checks\">");
            foreach (ItmSpecies s in sp)
            {
                sb.Append("<label class=\"it-check\"><input type=\"checkbox\" name=\"it-f-sp\" value=\"" + Ui.Attr(s.SpeciesKey) + "\"" + (mine.Contains(s.SpeciesKey) ? " checked" : string.Empty) + "><span>" + Ui.Enc(s.SpeciesName) + "</span></label>");
            }
            sb.Append("</div><em class=\"it-err\" id=\"it-e-sp\"></em></div>");
            sb.Append("<div class=\"it-span2 it-fgrid it-tw2\" id=\"it-medw\"" + (v.ItemType == "medicine" ? string.Empty : " hidden") + ">");
            sb.Append(Field("dose", "Usual dose", v.Dose, 120, "text", string.Empty, "copied onto the order, can be changed there"));
            sb.Append("<label class=\"it-field it-span2\"><span>Instructions for the owner</span><textarea id=\"it-f-inst\" rows=\"2\" maxlength=\"600\" placeholder=\"For example: give 1 tablet by mouth twice a day with food\">" + Ui.Enc(v.Instructions) + "</textarea><em class=\"it-err\" id=\"it-e-inst\"></em></label>");
            sb.Append("</div>");
            sb.Append("<div class=\"it-span2\" id=\"it-vaxw\"" + (v.ItemType == "vaccine" ? string.Empty : " hidden") + ">" + Field("vax", "Due again after (months)", v.VaxMonths > 0 ? v.VaxMonths.ToString(Ui.Inv) : string.Empty, 3, "text", string.Empty, "sets the next due date and reminder", " inputmode=\"numeric\"") + "</div>");
            sb.Append("<label class=\"it-check it-span2\"><input type=\"checkbox\" id=\"it-f-stock\"" + (v.TrackStock ? " checked" : string.Empty) + "><span>Track stock <small class=\"it-hint\">only remembered for now: stock counts are not built yet</small></span></label>");
            sb.Append(Field("order", "Sort order", v.ItemOrder.ToString(Ui.Inv), 5, "text", string.Empty, "lower comes first", " inputmode=\"numeric\""));
            sb.Append(Field("note", "Note", v.Note, 300, "text", string.Empty, "staff only"));
            if (!add)
            {
                sb.Append("<label class=\"it-check it-span2\"><input type=\"checkbox\" id=\"it-f-active\"" + (v.Active ? " checked" : string.Empty) + "><span>Active <small class=\"it-hint\">inactive items can't be added to new orders</small></span></label>");
            }
            sb.Append("</div></fieldset><em class=\"it-err\" id=\"it-e-form\"></em><div id=\"it-confirm\"></div>");
            sb.Append("<div class=\"it-mfoot\">");
            if (!add && c.Can(Key, 'D'))
            {
                sb.Append("<button type=\"button\" class=\"it-btn it-btn-danger it-mleft\" onclick=\"" + Me + ".ask('" + Ui.Js(v.ItemId) + "', this)\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".closeModal()\">" + (can ? "Cancel" : "Close") + "</button>");
            if (can)
            {
                sb.Append("<button type=\"submit\" class=\"it-btn it-btn-accent\" id=\"it-fsave\">" + (add ? "Add item" : "Save changes") + "</button>");
            }
            sb.Append("</div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = CleanId(GetDataValue("itemid"));
            bool add = id == string.Empty;
            if (!c.Can(Key, add ? 'A' : 'E'))
            {
                await App.Log(c.U.StaffId, add ? "add item" : "edit item", "not allowed", false);
                Toast(response, add ? "You can't add items." : "You can't change items.");
                return response;
            }
            ItmRow? old = null;
            string newId = id;
            if (add)
            {
                newId = NewKey();
                if (newId == string.Empty)
                {
                    Toast(response, "Please close this window and try again.");
                    return response;
                }
                ItmRow? done = await Item(newId);
                if (done != null)
                {
                    response.ExecuteScript(Me + ".saved('" + Ui.Js(done.ItemName + " was added.") + "');");
                    return response;
                }
            }
            else
            {
                old = await Item(id);
                if (old == null)
                {
                    Toast(response, "This item was not found. It may have been deleted.");
                    return response;
                }
            }
            string name = Ui.OneLine(GetDataValue("name"), 120);
            string code = Ui.OneLine(GetDataValue("code"), 30).ToUpperInvariant();
            string typ = Val("ftype", 20);
            string cat = Ui.OneLine(GetDataValue("cat"), 60);
            string unit = Ui.OneLine(GetDataValue("unit"), 20).ToLowerInvariant();
            decimal? price = Dec(GetDataValue("price"));
            decimal? pm = Dec(GetDataValue("pm"));
            decimal? pl = Dec(GetDataValue("pl"));
            bool tax = Val("tax", 2) == "1";
            bool stock = Val("stock", 2) == "1";
            string dose = Ui.OneLine(GetDataValue("dose"), 120);
            string inst = Val("inst", 600);
            string vaxText = Val("vax", 6);
            string orderText = Val("order", 8);
            string note = Ui.OneLine(GetDataValue("note"), 300);
            bool active = add || Val("active", 2) == "1";
            List<ItmSpecies> sp = await SpeciesList();
            List<string> picked = (GetDataValue("species") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
            Dictionary<string, string> errs = new Dictionary<string, string> { { "name", "" }, { "code", "" }, { "type", "" }, { "cat", "" }, { "unit", "" }, { "price", "" }, { "pm", "" }, { "pl", "" }, { "sp", "" }, { "dose", "" }, { "vax", "" }, { "order", "" }, { "form", "" } };
            if (name == string.Empty)
            {
                errs["name"] = "Enter the name.";
            }
            if (code == string.Empty)
            {
                errs["code"] = "Enter a short code.";
            }
            else if (!Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9._/-]{0,29}$"))
            {
                errs["code"] = "Use letters, numbers and dashes only.";
            }
            if (!IsType(typ))
            {
                errs["type"] = "Pick a type.";
            }
            if (unit == string.Empty)
            {
                errs["unit"] = "Enter a unit, like each or tablet.";
            }
            if (price == null || price > 99999.99m)
            {
                errs["price"] = "Enter a price like 32.00.";
            }
            if (pm == null || pm > 99999.99m)
            {
                errs["pm"] = "Enter a price like 40.00, or 0.";
            }
            if (pl == null || pl > 99999.99m)
            {
                errs["pl"] = "Enter a price like 48.00, or 0.";
            }
            if (picked.Any(k => !sp.Any(x => x.SpeciesKey == k)))
            {
                errs["sp"] = "Pick species from the list.";
            }
            int vax = 0;
            if (typ == "vaccine")
            {
                if (!int.TryParse(vaxText, NumberStyles.None, Ui.Inv, out vax) || vax < 1 || vax > 120)
                {
                    errs["vax"] = "Enter how many months until it is due again (1 to 120).";
                }
            }
            int order = 0;
            if (orderText != string.Empty && (!int.TryParse(orderText, NumberStyles.AllowLeadingSign, Ui.Inv, out order) || order < 0 || order > 99999))
            {
                errs["order"] = "Enter a whole number.";
            }
            if (!add && old != null && old.ItemType != typ && IsType(typ))
            {
                errs["type"] = await TypeClash(id, typ);
            }
            bool force = Val("force", 2) == "1";
            if (!add && old != null && old.Active && !active && !force && errs.Values.All(x => x == string.Empty))
            {
                string refs = await ActiveRefs(id);
                if (refs != string.Empty)
                {
                    foreach (string k in errs.Keys.ToList())
                    {
                        response.SetElementContents("it-e-" + k, string.Empty);
                    }
                    response.SetElementContents("it-confirm", "<div class=\"it-warn\" role=\"alert\"><b>Still in use</b><p>" + Ui.Enc(refs) + " Those will show no price until another item is linked.</p><div class=\"it-facts\"><button type=\"button\" class=\"it-btn it-btn-sm\" onclick=\"" + Me + ".save(1)\">Make inactive anyway</button></div></div>");
                    response.ExecuteScript(Me + ".unlock();" + Me + ".bad();");
                    return response;
                }
            }
            if (errs["code"] == string.Empty && code != string.Empty)
            {
                var (dup, _) = await Helper.GetDataTable("SELECT ITEM_NAME FROM ITEM WHERE ITEM_CODE = @code AND ITEM_ID <> @iid", new List<SqlParameter> { App.P("code", code, 30), App.P("iid", newId, 50) });
                if (dup.Rows.Count > 0)
                {
                    errs["code"] = "This code is already used by " + Ui.Enc(Convert.ToString(dup.Rows[0][0])) + ".";
                }
            }
            response.SetElementContents("it-confirm", string.Empty);
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("it-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                response.ExecuteScript(Me + ".unlock();" + Me + ".bad();");
                return response;
            }
            string speciesCsv = string.Join(",", sp.Where(x => picked.Contains(x.SpeciesKey)).Select(x => x.SpeciesKey));
            if (typ != "medicine")
            {
                dose = string.Empty;
                inst = string.Empty;
            }
            if (typ != "vaccine")
            {
                vax = 0;
            }
            List<SqlParameter> Ps() => new List<SqlParameter>
            {
                App.P("iid", newId, 50), App.P("code", code, 30), App.P("name", name, 120), App.P("typ", typ, 20), App.P("cat", cat, 60), App.P("unit", unit, 20),
                App.PDec("price", price ?? 0), App.PDec("pm", pm ?? 0), App.PDec("pl", pl ?? 0), App.PBit("tax", tax), App.P("dose", dose, 120), App.P("inst", inst, 600),
                App.PInt("vax", vax), App.P("sp", speciesCsv, 100), App.PBit("stock", stock), App.PInt("ord", order), App.P("note", note, 300), App.PBit("active", active)
            };
            string result;
            if (add)
            {
                result = await Helper.PutData(new List<string> { "IF EXISTS (SELECT 1 FROM ITEM WHERE ITEM_ID = @iid) THROW 50003, N'DONE', 1;", "INSERT INTO ITEM (ITEM_ID, ITEM_CODE, ITEM_NAME, ITEM_TYPE, CATEGORY, UNIT, PRICE, PRICE_M, PRICE_L, TAXABLE, DOSE, INSTRUCTIONS, VAX_MONTHS, SPECIES, TRACK_STOCK, ITEM_ORDER, NOTE, ACTIVE, SYSDTE) VALUES (@iid, @code, @name, @typ, @cat, @unit, @price, @pm, @pl, @tax, @dose, @inst, @vax, @sp, @stock, @ord, @note, 1, GETDATE())" }, Ps());
            }
            else
            {
                result = await Helper.PutData(new List<string> { "UPDATE ITEM SET ITEM_CODE = @code, ITEM_NAME = @name, ITEM_TYPE = @typ, CATEGORY = @cat, UNIT = @unit, PRICE = @price, PRICE_M = @pm, PRICE_L = @pl, TAXABLE = @tax, DOSE = @dose, INSTRUCTIONS = @inst, VAX_MONTHS = @vax, SPECIES = @sp, TRACK_STOCK = @stock, ITEM_ORDER = @ord, NOTE = @note, ACTIVE = @active, SYSDTE = GETDATE() WHERE ITEM_ID = @iid; IF @@ROWCOUNT = 0 THROW 50002, N'GONE', 1;" }, Ps());
            }
            if (Helper.IsError(result) && add && (result.Contains("DONE") || await Item(newId) != null))
            {
                response.ExecuteScript(Me + ".saved('" + Ui.Js(name + " was added.") + "');");
                return response;
            }
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, add ? "add item" : "edit item", name + " failed", false);
                bool dupCode = result.Contains("UX_ITEM_CODE", StringComparison.OrdinalIgnoreCase) || result.Contains("duplicate", StringComparison.OrdinalIgnoreCase) || result.Contains("unique", StringComparison.OrdinalIgnoreCase);
                response.SetElementContents(dupCode ? "it-e-code" : "it-e-form", dupCode ? "This code was just taken. Use another one." : result.Contains("GONE") ? "This item was deleted by someone else." : "The item could not be saved. Try again.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            string target = name;
            if (add)
            {
                target += " · " + code + " · " + M2(price ?? 0);
            }
            else if (old != null)
            {
                List<string> ch = new List<string>();
                if (old.Price != price)
                {
                    ch.Add("price " + M2(old.Price) + " → " + M2(price ?? 0));
                }
                if (old.PriceM != pm)
                {
                    ch.Add("medium " + M2(old.PriceM) + " → " + M2(pm ?? 0));
                }
                if (old.PriceL != pl)
                {
                    ch.Add("large " + M2(old.PriceL) + " → " + M2(pl ?? 0));
                }
                if (old.ItemName != name)
                {
                    ch.Add("was " + old.ItemName);
                }
                if (old.ItemCode != code)
                {
                    ch.Add("code " + old.ItemCode + " → " + code);
                }
                if (old.Active != active)
                {
                    ch.Add(active ? "made active" : "made inactive");
                }
                target += ch.Count > 0 ? " · " + string.Join("; ", ch) : " · details";
            }
            await App.Log(c.U.StaffId, add ? "added item" : "edited item", target, true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(name + (add ? " was added." : " was saved.")) + "');");
            return response;
        }

        public async Task<ApiResponse> Ask()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                Toast(response, "You can't delete items.");
                return response;
            }
            string id = CleanId(GetDataValue("itemid"));
            ItmRow? it = id == string.Empty ? null : await Item(id);
            if (it == null)
            {
                Toast(response, "This item was not found. It may have been deleted.");
                return response;
            }
            ItmUse u = await Use(id);
            string iid = Ui.Js(id);
            StringBuilder sb = new StringBuilder();
            if (Used(u))
            {
                List<string> why = new List<string>();
                if (u.Orders > 0)
                {
                    why.Add("it is on " + Ui.Plural(u.Orders, "order", "orders"));
                }
                if (u.InvLines > 0)
                {
                    why.Add("it is on " + Ui.Plural(u.InvLines, "invoice line", "invoice lines"));
                }
                if (u.Books > 0)
                {
                    why.Add("booking type " + u.BookNames + " uses its price");
                }
                if (u.Rooms > 0)
                {
                    why.Add(u.RoomNames + " uses it as the nightly price");
                }
                if (u.Fields > 0)
                {
                    why.Add("it has " + Ui.Plural(u.Fields, "result field", "result fields"));
                }
                sb.Append("<h2>" + Ui.Enc(it.ItemName) + " can't be deleted</h2><p class=\"it-mlead\">" + Ui.Enc(string.Join("; ", why)) + ". Old records must keep pointing to it.</p>");
                if (it.Active && c.Can(Key, 'E'))
                {
                    string refs = await ActiveRefs(id);
                    sb.Append("<p>Make it inactive instead. It stays on old records but can't be added to new orders.</p>" + (refs != string.Empty ? "<p class=\"it-warn\"><b>Still in use.</b> " + Ui.Enc(refs) + " Those will show no price until another item is linked.</p>" : string.Empty) + "<div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".open('" + iid + "')\">Back</button><button type=\"button\" class=\"it-btn it-btn-accent\" onclick=\"" + Me + ".retire('" + iid + "', this, " + (refs != string.Empty ? "1" : "0") + ")\">" + (refs != string.Empty ? "Make inactive anyway" : "Make inactive") + "</button></div>");
                }
                else
                {
                    sb.Append("<div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".open('" + iid + "')\">Back</button></div>");
                }
            }
            else
            {
                sb.Append("<h2>Delete " + Ui.Enc(it.ItemName) + "?</h2><p class=\"it-mlead\">It has never been used, so it can be removed for good. This can't be undone.</p>");
                sb.Append("<div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".open('" + iid + "')\">Keep it</button><button type=\"button\" class=\"it-btn it-btn-danger\" onclick=\"" + Me + ".remove('" + iid + "', this)\">Delete for good</button></div>");
            }
            response.SetElementContents("it-mbody", sb.ToString());
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(false);");
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
                await App.Log(c.U.StaffId, "delete item", "not allowed", false);
                Toast(response, "You can't delete items.");
                return response;
            }
            string id = CleanId(GetDataValue("itemid"));
            ItmRow? it = id == string.Empty ? null : await Item(id);
            if (it == null)
            {
                Toast(response, "This item was not found. It may have been deleted.");
                return response;
            }
            string result = await Helper.PutData(new List<string>
            {
                "IF EXISTS (SELECT 1 FROM ORDERLINE WHERE ITEM_ID = @iid) OR EXISTS (SELECT 1 FROM INVOICELINE WHERE ITEM_ID = @iid) OR EXISTS (SELECT 1 FROM BOOKTYPE WHERE ITEM_ID = @iid) OR EXISTS (SELECT 1 FROM ROOMTYPE WHERE NIGHT_ITEM_ID = @iid) OR EXISTS (SELECT 1 FROM TESTFIELD WHERE ITEM_ID = @iid) THROW 50001, N'ITEMUSED', 1;",
                "DELETE FROM ITEM WHERE ITEM_ID = @iid"
            }, new List<SqlParameter> { App.P("iid", id, 50) });
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "delete item", it.ItemName + " failed", false);
                Toast(response, result.Contains("ITEMUSED") ? "This item is in use now and can't be deleted. Make it inactive instead." : "The item could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "deleted item", it.ItemName + " · " + it.ItemCode, true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(it.ItemName + " was deleted.") + "');");
            return response;
        }

        public async Task<ApiResponse> Retire()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "make item inactive", "not allowed", false);
                Toast(response, "You can't change items.");
                return response;
            }
            string id = CleanId(GetDataValue("itemid"));
            ItmRow? it = id == string.Empty ? null : await Item(id);
            if (it == null)
            {
                Toast(response, "This item was not found. It may have been deleted.");
                return response;
            }
            if (Val("force", 2) != "1")
            {
                string refs = await ActiveRefs(id);
                if (refs != string.Empty)
                {
                    string iid = Ui.Js(id);
                    response.SetElementContents("it-mbody", "<h2>Make " + Ui.Enc(it.ItemName) + " inactive?</h2><p class=\"it-warn\"><b>Still in use.</b> " + Ui.Enc(refs) + " Those will show no price until another item is linked.</p><div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".open('" + iid + "')\">Back</button><button type=\"button\" class=\"it-btn it-btn-accent\" onclick=\"" + Me + ".retire('" + iid + "', this, 1)\">Make inactive anyway</button></div>");
                    response.ExecuteScript(Me + ".unlock();" + Me + ".shown(false);");
                    return response;
                }
            }
            string result = await Helper.PutData(new List<string> { "UPDATE ITEM SET ACTIVE = 0, SYSDTE = GETDATE() WHERE ITEM_ID = @iid" }, new List<SqlParameter> { App.P("iid", id, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, "The item could not be changed. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "edited item", it.ItemName + " · made inactive", true);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(it.ItemName + " is now inactive.") + "');");
            return response;
        }

        private static decimal Step(string v)
        {
            return v switch { "0.05" => 0.05m, "0.50" => 0.50m, "1.00" => 1.00m, _ => 0.01m };
        }

        private static decimal Calc(decimal p, bool up, bool pct, decimal amt, decimal step)
        {
            if (p <= 0)
            {
                return p;
            }
            decimal n = pct ? p * (up ? 1 + amt / 100m : 1 - amt / 100m) : up ? p + amt : p - amt;
            decimal r = (up ? Math.Ceiling(n / step) : Math.Floor(n / step)) * step;
            if (up && r < p)
            {
                r = p;
            }
            if (!up && r > p)
            {
                r = p;
            }
            return r;
        }

        private sealed class Bulk
        {
            public bool Up { get; set; }
            public bool Pct { get; set; }
            public decimal Amt { get; set; }
            public string Round { get; set; } = "0.01";
            public string Err { get; set; } = string.Empty;
        }

        private Bulk ReadBulk()
        {
            Bulk b = new Bulk { Up = Val("pdir", 10) != "lower", Pct = Val("pmode", 10) != "amt", Round = Val("pround", 6) };
            if (b.Round != "0.05" && b.Round != "0.50" && b.Round != "1.00")
            {
                b.Round = "0.01";
            }
            decimal? a = Dec(GetDataValue("pval"));
            if (a == null || a <= 0)
            {
                b.Err = b.Pct ? "Enter the percent, like 5 or 2.5." : "Enter the amount, like 2.00.";
            }
            else if (b.Pct && a > (b.Up ? 100m : 90m))
            {
                b.Err = b.Up ? "Raise by 100% or less." : "Lower by 90% or less.";
            }
            else if (!b.Pct && a > 10000m)
            {
                b.Err = "Enter an amount up to 10,000.";
            }
            b.Amt = a ?? 0;
            return b;
        }

        private static async Task<List<ItmChange>?> Changes(Flt f, Bulk b)
        {
            var (rows, emsg) = await Helper.GetListT<ItmRow>("SELECT " + Cols + " FROM ITEM I" + Where(f) + Order + " OFFSET 0 ROWS FETCH NEXT " + (MaxBulk + 1) + " ROWS ONLY", FPs(f));
            if (emsg != string.Empty || rows == null)
            {
                return null;
            }
            decimal step = Step(b.Round);
            List<ItmChange> list = new List<ItmChange>();
            foreach (ItmRow r in rows)
            {
                ItmChange ch = new ItmChange { Row = r, NewPrice = Calc(r.Price, b.Up, b.Pct, b.Amt, step), NewM = Calc(r.PriceM, b.Up, b.Pct, b.Amt, step), NewL = Calc(r.PriceL, b.Up, b.Pct, b.Amt, step) };
                if ((r.Price > 0 && ch.NewPrice <= 0) || (r.PriceM > 0 && ch.NewM <= 0) || (r.PriceL > 0 && ch.NewL <= 0) || ch.NewPrice > 99999.99m || ch.NewM > 99999.99m || ch.NewL > 99999.99m)
                {
                    ch.Skip = true;
                }
                list.Add(ch);
            }
            return list;
        }

        private static string Token(List<ItmChange> list)
        {
            string raw = string.Join("|", list.Where(x => x.Changed).Select(x => x.Row.ItemId + ":" + M2(x.Row.Price) + ":" + M2(x.Row.PriceM) + ":" + M2(x.Row.PriceL) + ":" + M2(x.NewPrice) + ":" + M2(x.NewM) + ":" + M2(x.NewL)));
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }

        private static string FilterText(Flt f)
        {
            List<string> parts = new List<string> { f.Type == string.Empty ? "all types" : TypeTab(f.Type) };
            if (f.Cat != string.Empty)
            {
                parts.Add("category " + f.Cat);
            }
            if (f.Term != string.Empty)
            {
                parts.Add("matching “" + f.Term + "”");
            }
            parts.Add(f.Show == "all" ? "active and inactive" : f.Show == "inactive" ? "inactive only" : "active only");
            return string.Join(" · ", parts);
        }

        public async Task<ApiResponse> PriceForm()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change prices.");
                return response;
            }
            Flt f = ReadFilter();
            var (cnt, _) = await Helper.GetDataTable("SELECT COUNT(*) FROM ITEM I" + Where(f), FPs(f));
            int total = cnt.Rows.Count > 0 ? Convert.ToInt32(cnt.Rows[0][0]) : 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Change prices</h2><p class=\"it-mlead\">For the items in the list now: <b>" + Ui.Enc(FilterText(f)) + "</b> (" + Ui.Plural(total, "item", "items") + "). Close this window and change the tabs or filters to pick other items.</p>");
            if (total == 0)
            {
                sb.Append("<p>There are no items in the list.</p><div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            }
            else if (total > MaxBulk)
            {
                sb.Append("<p class=\"it-warn\">That is more than " + MaxBulk + " items. Narrow the list with a tab, a category or a search first.</p><div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            }
            else
            {
                sb.Append("<div class=\"it-pgrid\">");
                sb.Append("<label class=\"it-field\"><span>Change</span><select id=\"it-pdir\" onchange=\"" + Me + ".pdirty()\"><option value=\"raise\">Raise prices</option><option value=\"lower\">Lower prices</option></select></label>");
                sb.Append("<label class=\"it-field\"><span>By</span><select id=\"it-pmode\" onchange=\"" + Me + ".pdirty()\"><option value=\"pct\">A percent</option><option value=\"amt\">A fixed amount ($)</option></select></label>");
                sb.Append("<label class=\"it-field\"><span>How much</span><input type=\"text\" id=\"it-pval\" maxlength=\"9\" inputmode=\"decimal\" placeholder=\"5\" autocomplete=\"off\" oninput=\"" + Me + ".pdirty()\"></label>");
                sb.Append("<label class=\"it-field\"><span>Round new prices to</span><select id=\"it-pround\" onchange=\"" + Me + ".pdirty()\"><option value=\"0.01\">The cent</option><option value=\"0.05\">Nearest 0.05</option><option value=\"0.50\">Nearest 0.50</option><option value=\"1.00\" selected>Nearest 1.00</option></select></label>");
                sb.Append("</div><em class=\"it-err\" id=\"it-e-pval\"></em><p class=\"it-hint it-pnote\">Medium and large pet prices change the same way. Free items and medium or large prices of 0 (same as the main price) stay as they are.</p>");
                sb.Append("<div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"button\" class=\"it-btn it-btn-accent\" id=\"it-pview\" onclick=\"" + Me + ".pview(this)\">Preview new prices</button></div>");
                sb.Append("<div id=\"it-pprev\"></div>");
            }
            response.SetElementContents("it-mbody", sb.ToString());
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(false);");
            return response;
        }

        private static string Pair(decimal o, decimal n, bool skip)
        {
            if (o <= 0)
            {
                return "<span class=\"it-dim\">&mdash;</span>";
            }
            if (skip || o == n)
            {
                return Ui.Money(o);
            }
            return "<span class=\"it-v\"><span class=\"it-old\">" + Ui.Money(o) + "</span> <span aria-hidden=\"true\">&rarr;</span> <b class=\"" + (n > o ? "it-upv" : "it-dnv") + "\">" + Ui.Money(n) + "</b></span>";
        }

        public async Task<ApiResponse> PricePreview()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change prices.");
                return response;
            }
            Flt f = ReadFilter();
            Bulk b = ReadBulk();
            response.SetElementContents("it-e-pval", b.Err);
            if (b.Err != string.Empty)
            {
                response.SetElementContents("it-pprev", string.Empty);
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            List<ItmChange>? list = await Changes(f, b);
            if (list == null)
            {
                Toast(response, "The items could not be loaded. Try again.");
                return response;
            }
            if (list.Count > MaxBulk)
            {
                response.SetElementContents("it-pprev", "<p class=\"it-warn\">That is more than " + MaxBulk + " items. Narrow the list first.</p>");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            int n = list.Count(x => x.Changed);
            int skipped = list.Count(x => x.Skip);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"it-pres\"><p class=\"it-psum\"><b>" + Ui.Plural(n, "item changes", "items change") + "</b>" + (list.Count - n - skipped > 0 ? " &middot; " + Ui.Plural(list.Count - n - skipped, "is skipped", "are skipped") + " because the price would not change" : string.Empty) + (skipped > 0 ? " &middot; <span class=\"it-late\">" + Ui.Plural(skipped, "is skipped", "are skipped") + " (would drop to $0 or below)</span>" : string.Empty) + "</p>");
            sb.Append("<div class=\"it-tw it-ptw\"><table class=\"it-table it-ptable\"><thead><tr><th>Item</th><th>Price</th><th>Medium</th><th>Large</th></tr></thead><tbody>");
            foreach (ItmChange x in list)
            {
                sb.Append("<tr class=\"" + (x.Changed ? string.Empty : "it-off") + "\"><td class=\"it-cname\"><b>" + Ui.Enc(x.Row.ItemName) + "</b><small>" + Ui.Enc(x.Row.ItemCode) + (x.Skip ? " &middot; skipped, would drop to $0 or below" : !x.Changed ? " &middot; skipped, no change after rounding" : string.Empty) + "</small></td>");
                sb.Append("<td data-l=\"Price\" class=\"it-nw\">" + Pair(x.Row.Price, x.NewPrice, x.Skip) + "</td><td data-l=\"Medium\" class=\"it-nw\">" + Pair(x.Row.PriceM, x.NewM, x.Skip) + "</td><td data-l=\"Large\" class=\"it-nw\">" + Pair(x.Row.PriceL, x.NewL, x.Skip) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            if (n > 0)
            {
                sb.Append("<div class=\"it-mfoot\"><button type=\"button\" class=\"it-btn it-btn-accent\" id=\"it-papply\" onclick=\"" + Me + ".papply(this)\">Apply " + Ui.Plural(n, "new price", "new prices") + "</button></div>");
            }
            sb.Append("</div>");
            response.SetElementContents("it-pprev", sb.ToString());
            response.ExecuteScript(Me + ".unlock();" + Me + ".previewed('" + Token(list) + "');");
            return response;
        }

        public async Task<ApiResponse> PriceApply()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "change prices", "not allowed", false);
                Toast(response, "You can't change prices.");
                return response;
            }
            Flt f = ReadFilter();
            Bulk b = ReadBulk();
            if (b.Err != string.Empty)
            {
                response.SetElementContents("it-e-pval", b.Err);
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            List<ItmChange>? list = await Changes(f, b);
            if (list == null || list.Count > MaxBulk)
            {
                Toast(response, "The items could not be loaded. Try again.");
                return response;
            }
            string tok = Val("ptok", 80);
            if (tok == string.Empty || tok != Token(list))
            {
                response.SetElementContents("it-pprev", "<p class=\"it-warn\">Prices changed since your preview. Look at the new preview before you apply.</p>");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            List<ItmChange> todo = list.Where(x => x.Changed).ToList();
            if (todo.Count == 0)
            {
                Toast(response, "Nothing to change.");
                return response;
            }
            string how = (b.Up ? "raise " : "lower ") + (b.Pct ? b.Amt.ToString("0.##", Ui.Inv) + "%" : "$" + M2(b.Amt)) + (b.Round != "0.01" ? ", round " + b.Round : string.Empty);
            List<string> sql = new List<string>();
            List<SqlParameter> ps = new List<SqlParameter> { App.P("me", c.U.StaffId, 50), App.P("ip", App.Ip(), 60), App.P("act", "changed price", 60) };
            int i = 0;
            foreach (ItmChange x in todo)
            {
                sql.Add("UPDATE ITEM SET PRICE = @n" + i + ", PRICE_M = @m" + i + ", PRICE_L = @l" + i + ", SYSDTE = GETDATE() WHERE ITEM_ID = @i" + i + " AND PRICE = @op" + i + " AND PRICE_M = @om" + i + " AND PRICE_L = @ol" + i + "; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;");
                ps.Add(App.P("i" + i, x.Row.ItemId, 50));
                ps.Add(App.PDec("n" + i, x.NewPrice));
                ps.Add(App.PDec("m" + i, x.NewM));
                ps.Add(App.PDec("l" + i, x.NewL));
                ps.Add(App.PDec("op" + i, x.Row.Price));
                ps.Add(App.PDec("om" + i, x.Row.PriceM));
                ps.Add(App.PDec("ol" + i, x.Row.PriceL));
                List<string> parts = new List<string>();
                if (x.NewPrice != x.Row.Price)
                {
                    parts.Add("price " + M2(x.Row.Price) + " → " + M2(x.NewPrice));
                }
                if (x.NewM != x.Row.PriceM)
                {
                    parts.Add("medium " + M2(x.Row.PriceM) + " → " + M2(x.NewM));
                }
                if (x.NewL != x.Row.PriceL)
                {
                    parts.Add("large " + M2(x.Row.PriceL) + " → " + M2(x.NewL));
                }
                sql.Add("INSERT INTO ACTIVITY (ACT_UTC, STAFF_ID, ACTION, TARGET, OK, IP) VALUES (SYSUTCDATETIME(), @me, @act, @g" + i + ", 1, @ip)");
                ps.Add(App.P("g" + i, Ui.Clip(x.Row.ItemName + " · " + string.Join("; ", parts) + " (" + how + ")", 200), 200));
                i++;
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, "change prices", FilterText(f) + " failed", false);
                response.SetElementContents("it-pprev", "<p class=\"it-warn\">" + (result.Contains("STATECHANGED") ? "Someone changed a price while you were looking. Nothing was changed. Preview again." : "The prices could not be changed. Nothing was changed. Try again.") + "</p>");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            response.ExecuteScript(Me + ".saved('" + Ui.Js(Ui.Plural(todo.Count, "price", "prices") + " changed.") + "');");
            return response;
        }
    }
}
