using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminTests : WebPage
    {
        private const string Key = "tests";
        private const string Me = "AdminTestsJs";
        private const string FieldCols = "F.FIELD_ID AS FieldId, F.ITEM_ID AS ItemId, F.FIELD_NAME AS FieldName, F.UNIT AS Unit, F.SPECIES_KEY AS SpeciesKey, F.LOW_VALUE AS LowValue, F.HIGH_VALUE AS HighValue, F.DECIMALS AS Decimals, F.IS_TEXT AS IsText, F.FIELD_ORDER AS FieldOrder, (SELECT COUNT(*) FROM RESULT R WHERE R.FIELD_ID = F.FIELD_ID) AS Results";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Test fields | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ts-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ts"));
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
            return "<div class=\"ts-panel ts-deny\"><h1>Test fields</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"ts-btn ts-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static async Task<List<TstTest>> Tests()
        {
            var (list, emsg) = await Helper.GetListT<TstTest>("SELECT I.ITEM_ID AS ItemId, I.ITEM_CODE AS ItemCode, I.ITEM_NAME AS ItemName, I.SPECIES AS Species, I.ACTIVE AS Active, (SELECT COUNT(*) FROM TESTFIELD F WHERE F.ITEM_ID = I.ITEM_ID) AS Fields FROM ITEM I WHERE I.ITEM_TYPE = N'test' ORDER BY I.ACTIVE DESC, I.ITEM_ORDER, I.ITEM_NAME");
            return emsg == string.Empty && list != null ? list : new List<TstTest>();
        }

        private static async Task<TstTest?> Test(string id)
        {
            var (row, emsg) = await Helper.GetOneT<TstTest>("SELECT I.ITEM_ID AS ItemId, I.ITEM_CODE AS ItemCode, I.ITEM_NAME AS ItemName, I.SPECIES AS Species, I.ACTIVE AS Active, (SELECT COUNT(*) FROM TESTFIELD F WHERE F.ITEM_ID = I.ITEM_ID) AS Fields FROM ITEM I WHERE I.ITEM_ID = @iid AND I.ITEM_TYPE = N'test'", new List<SqlParameter> { App.P("iid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<TstField>> Fields(string itemId)
        {
            var (list, emsg) = await Helper.GetListT<TstField>("SELECT " + FieldCols + " FROM TESTFIELD F WHERE F.ITEM_ID = @iid ORDER BY F.FIELD_ORDER, F.SPECIES_KEY, F.FIELD_NAME", new List<SqlParameter> { App.P("iid", itemId, 50) });
            return emsg == string.Empty && list != null ? list : new List<TstField>();
        }

        private static async Task<TstField?> OneField(string id)
        {
            var (row, emsg) = await Helper.GetOneT<TstField>("SELECT " + FieldCols + " FROM TESTFIELD F WHERE F.FIELD_ID = @fid", new List<SqlParameter> { App.P("fid", id, 50) });
            return emsg == string.Empty ? row : null;
        }

        private static async Task<List<TstSpecies>> SpeciesList()
        {
            var (list, emsg) = await Helper.GetListT<TstSpecies>("SELECT SPECIES_KEY AS SpeciesKey, SPECIES_NAME AS SpeciesName FROM SPECIES WHERE ACTIVE = 1 ORDER BY SPECIES_ORDER, SPECIES_NAME");
            return emsg == string.Empty && list != null ? list : new List<TstSpecies>();
        }

        private static string SpName(string key, List<TstSpecies> sp)
        {
            return key == string.Empty ? "All species" : sp.FirstOrDefault(x => x.SpeciesKey == key)?.SpeciesName ?? key;
        }

        private static string Num(decimal v, int dec)
        {
            return v.ToString("0." + new string('0', Math.Clamp(dec, 0, 4)) + new string('#', Math.Max(0, 3 - Math.Clamp(dec, 0, 4))), Ui.Inv).TrimEnd('.');
        }

        private static string Range(TstField f)
        {
            if (f.IsText)
            {
                return "<span class=\"ts-dim\">Text</span>";
            }
            if (f.LowValue.HasValue && f.HighValue.HasValue)
            {
                return Num(f.LowValue.Value, f.Decimals) + " &ndash; " + Num(f.HighValue.Value, f.Decimals);
            }
            if (f.LowValue.HasValue)
            {
                return "at least " + Num(f.LowValue.Value, f.Decimals);
            }
            if (f.HighValue.HasValue)
            {
                return "up to " + Num(f.HighValue.Value, f.Decimals);
            }
            return "<span class=\"ts-dim\">No range</span>";
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("ts-menu", Auth.Menu("ts", Me, "AdminTests", c));
            response.SetElementContents("ts-who", Auth.Who("ts", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ts-main", Denied());
                return response;
            }
            List<TstTest> tests = await Tests();
            string pick = CleanId(GetDataValue("testid"));
            if (pick == string.Empty || !tests.Any(t => t.ItemId == pick))
            {
                pick = tests.Count > 0 ? tests[0].ItemId : string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ts-ptop\"><div><h1>Test fields</h1><p>The boxes a vet or technician fills in for each lab test, with the normal range used to flag high and low results.</p></div></div>");
            sb.Append("<div class=\"ts-lay\"><nav class=\"ts-panel ts-tlist\" aria-label=\"Tests\"><div class=\"ts-tlh\"><b>Tests</b><small>" + Ui.Plural(tests.Count, "test", "tests") + "</small></div><div id=\"ts-tests\">" + ListHtml(tests, pick) + "</div><p class=\"ts-hint ts-tadd\">New tests are added on the Items and prices page with the type Test.</p></nav>");
            sb.Append("<section class=\"ts-pane\" id=\"ts-pane\">" + await PaneHtml(c, pick) + "</section></div>");
            response.SetElementContents("ts-main", sb.ToString());
            response.ExecuteScript(Me + ".ready('" + Ui.Js(pick) + "', false);");
            return response;
        }

        private static string ListHtml(List<TstTest> tests, string pick)
        {
            if (tests.Count == 0)
            {
                return "<p class=\"ts-empty-s\">There are no tests yet.</p>";
            }
            StringBuilder sb = new StringBuilder();
            foreach (TstTest t in tests)
            {
                bool on = t.ItemId == pick;
                sb.Append("<button type=\"button\" class=\"ts-tli" + (on ? " ts-on" : string.Empty) + (t.Active ? string.Empty : " ts-off") + "\" aria-current=\"" + (on ? "true" : "false") + "\" onclick=\"" + Me + ".pick('" + Ui.Js(t.ItemId) + "')\"><span><b>" + Ui.Enc(t.ItemName) + "</b><small>" + Ui.Enc(t.ItemCode) + (t.Active ? string.Empty : " &middot; inactive") + "</small></span><i class=\"" + (t.Fields == 0 ? "ts-zero" : string.Empty) + "\">" + t.Fields + "</i></button>");
            }
            return sb.ToString();
        }

        private static async Task<string> PaneHtml(Ctx c, string itemId)
        {
            TstTest? t = itemId == string.Empty ? null : await Test(itemId);
            if (t == null)
            {
                return "<div class=\"ts-panel ts-empty\"><p>Pick a test on the left.</p></div>";
            }
            bool edit = c.Can(Key, 'E');
            List<TstField> fields = await Fields(itemId);
            List<TstSpecies> sp = await SpeciesList();
            string tid = Ui.Js(t.ItemId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ts-panel\"><div class=\"ts-shead\"><div><h2>" + Ui.Enc(t.ItemName) + "</h2><p class=\"ts-sub\">" + Ui.Enc(t.ItemCode) + " &middot; " + Ui.Plural(fields.Count, "field", "fields") + (t.Active ? string.Empty : " &middot; inactive test") + "</p></div><div class=\"ts-acts\">");
            sb.Append("<button type=\"button\" class=\"ts-btn ts-btn-ghost ts-btn-sm\" onclick=\"" + Me + ".preview('" + tid + "', this)\">Preview for a dog and a cat</button>");
            if (edit)
            {
                sb.Append("<button type=\"button\" class=\"ts-btn ts-btn-accent ts-btn-sm\" onclick=\"" + Me + ".add('" + tid + "', this)\">Add field</button>");
            }
            sb.Append("</div></div>");
            sb.Append("<p class=\"ts-hint ts-lead2\">Fields show in this order on the result form. A row for one species replaces the all-species row with the same name for that species, so cats can have their own normal range.</p>");
            if (fields.Count == 0)
            {
                sb.Append("<div class=\"ts-empty\"><p>No fields yet. Without fields the result form shows one free-text box called Result.</p></div></div>");
                return sb.ToString();
            }
            HashSet<string> general = new HashSet<string>(fields.Where(f => f.SpeciesKey == string.Empty).Select(f => f.FieldName.Trim().ToLowerInvariant()));
            sb.Append("<div class=\"ts-tw ts-ftw\"><table class=\"ts-table\"><thead><tr>" + (edit ? "<th><span class=\"ts-sr\">Move</span></th>" : string.Empty) + "<th>Field</th><th>Unit</th><th>Normal range</th><th>Decimals</th><th>Species</th><th class=\"ts-num\">Results</th></tr></thead><tbody>");
            for (int i = 0; i < fields.Count; i++)
            {
                TstField f = fields[i];
                string fid = Ui.Js(f.FieldId);
                string js = Me + ".open('" + fid + "')";
                bool over = f.SpeciesKey != string.Empty && general.Contains(f.FieldName.Trim().ToLowerInvariant());
                sb.Append("<tr class=\"" + (edit ? "ts-row" : string.Empty) + (over ? " ts-over" : string.Empty) + "\"" + (edit ? " tabindex=\"0\" onclick=\"" + js + "\" onkeydown=\"if(event.key==='Enter'){" + js + ";}\"" : string.Empty) + ">");
                if (edit)
                {
                    sb.Append("<td class=\"ts-mv\"><button type=\"button\" class=\"ts-ibtn ts-arrow\" aria-label=\"Move up\" onclick=\"event.stopPropagation();" + Me + ".move('" + fid + "','up',this)\"" + (i == 0 ? " disabled" : string.Empty) + ">&uarr;</button><button type=\"button\" class=\"ts-ibtn ts-arrow\" aria-label=\"Move down\" onclick=\"event.stopPropagation();" + Me + ".move('" + fid + "','down',this)\"" + (i == fields.Count - 1 ? " disabled" : string.Empty) + ">&darr;</button></td>");
                }
                sb.Append("<td class=\"ts-cname\"><b>" + Ui.Enc(f.FieldName) + "</b>" + (over ? "<small>" + Ui.Enc(SpName(f.SpeciesKey, sp)) + " range, replaces the general one</small>" : string.Empty) + "</td>");
                sb.Append("<td data-l=\"Unit\">" + (f.Unit != string.Empty ? Ui.Enc(f.Unit) : "<span class=\"ts-dim\">&mdash;</span>") + "</td>");
                sb.Append("<td data-l=\"Normal range\" class=\"ts-nw\">" + Range(f) + "</td>");
                sb.Append("<td data-l=\"Decimals\">" + (f.IsText ? "<span class=\"ts-dim\">&mdash;</span>" : f.Decimals.ToString(Ui.Inv)) + "</td>");
                sb.Append("<td data-l=\"Species\">" + (f.SpeciesKey == string.Empty ? "<span class=\"ts-dim\">All</span>" : "<span class=\"ts-tag ts-tag-warn\">" + Ui.Enc(SpName(f.SpeciesKey, sp)) + "</span>") + "</td>");
                sb.Append("<td data-l=\"Results\" class=\"ts-num\">" + f.Results.ToString("#,##0", Ui.Inv) + "</td></tr>");
            }
            sb.Append("</tbody></table></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("ts-main", Denied());
                return response;
            }
            string id = CleanId(GetDataValue("testid"));
            await Redraw(response, c, id, (GetDataValue("jump") ?? string.Empty).Trim() == "1");
            return response;
        }

        private static async Task Redraw(ApiResponse response, Ctx c, string id, bool jump)
        {
            List<TstTest> tests = await Tests();
            response.SetElementContents("ts-tests", ListHtml(tests, id));
            response.SetElementContents("ts-pane", await PaneHtml(c, id));
            response.ExecuteScript(Me + ".unlock();" + Me + ".ready('" + Ui.Js(id) + "', " + (jump ? "true" : "false") + ");");
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change test fields.");
                return response;
            }
            string fid = CleanId(GetDataValue("fieldid"));
            TstField? f = null;
            string itemId = CleanId(GetDataValue("testid"));
            if (fid != string.Empty)
            {
                f = await OneField(fid);
                if (f == null)
                {
                    Toast(response, "This field was not found. It may have been deleted.");
                    return response;
                }
                itemId = f.ItemId;
            }
            TstTest? t = itemId == string.Empty ? null : await Test(itemId);
            if (t == null)
            {
                Toast(response, "This test was not found.");
                return response;
            }
            response.SetElementContents("ts-mbody", FormHtml(t, f, await SpeciesList()));
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(" + (f == null ? "true" : "false") + ");");
            return response;
        }

        private static string Field(string id, string label, string value, int max, string extra = "", string hint = "", string attrs = "")
        {
            return "<label class=\"ts-field" + (extra != string.Empty ? " " + extra : string.Empty) + "\"><span>" + label + (hint != string.Empty ? " <small>" + hint + "</small>" : string.Empty) + "</span><input type=\"text\" id=\"ts-f-" + id + "\" maxlength=\"" + max + "\" value=\"" + Ui.Attr(value) + "\" autocomplete=\"off\"" + attrs + "><em class=\"ts-err\" id=\"ts-e-" + id + "\"></em></label>";
        }

        private static string FormHtml(TstTest t, TstField? f, List<TstSpecies> sp)
        {
            bool add = f == null;
            TstField v = f ?? new TstField { ItemId = t.ItemId, Decimals = 1 };
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + (add ? "New field" : Ui.Enc(v.FieldName)) + "</h2><p class=\"ts-mlead\">" + Ui.Enc(t.ItemName) + "</p>");
            sb.Append("<form class=\"ts-form\" onsubmit=\"return " + Me + ".save()\" novalidate><input type=\"hidden\" id=\"ts-f-id\" value=\"" + Ui.Attr(v.FieldId) + "\"><input type=\"hidden\" id=\"ts-f-test\" value=\"" + Ui.Attr(t.ItemId) + "\">");
            sb.Append("<div class=\"ts-fgrid\">");
            sb.Append(Field("name", "Name", v.FieldName, 60, string.Empty, "as printed on the lab report"));
            sb.Append("<label class=\"ts-field\"><span>Species <small>blank = all species</small></span><select id=\"ts-f-sp\">" + Ui.Opts(new[] { (string.Empty, "All species") }.Concat(sp.Select(x => (x.SpeciesKey, x.SpeciesName))), v.SpeciesKey) + "</select><em class=\"ts-err\" id=\"ts-e-sp\"></em></label>");
            sb.Append("<label class=\"ts-check ts-span2\"><input type=\"checkbox\" id=\"ts-f-text\"" + (v.IsText ? " checked" : string.Empty) + " onchange=\"" + Me + ".kind()\"><span>Text only <small class=\"ts-hint\">for words like Negative or a written finding, no numbers or range</small></span></label>");
            sb.Append("<div class=\"ts-span2 ts-fgrid\" id=\"ts-numw\"" + (v.IsText ? " hidden" : string.Empty) + ">");
            sb.Append(Field("unit", "Unit", v.Unit, 20, string.Empty, "like mg/dL", " spellcheck=\"false\""));
            sb.Append("<label class=\"ts-field\"><span>Decimals <small>shown in results</small></span><select id=\"ts-f-dec\">" + Ui.Opts(Enumerable.Range(0, 5).Select(i => (i.ToString(Ui.Inv), i == 0 ? "0 (whole numbers)" : i.ToString(Ui.Inv))), v.Decimals.ToString(Ui.Inv)) + "</select><em class=\"ts-err\" id=\"ts-e-dec\"></em></label>");
            sb.Append(Field("low", "Normal low", v.LowValue.HasValue ? Num(v.LowValue.Value, v.Decimals) : string.Empty, 14, string.Empty, "blank = no low limit", " inputmode=\"decimal\""));
            sb.Append(Field("high", "Normal high", v.HighValue.HasValue ? Num(v.HighValue.Value, v.Decimals) : string.Empty, 14, string.Empty, "blank = no high limit", " inputmode=\"decimal\""));
            sb.Append("</div></div>");
            if (!add)
            {
                sb.Append("<p class=\"ts-hint ts-pnote\">" + (v.Results > 0 ? Ui.Plural(v.Results, "result was", "results were") + " entered in this field. They keep their own copy of the range, so changes only affect new results." : "No results were entered in this field yet.") + "</p>");
            }
            sb.Append("<em class=\"ts-err\" id=\"ts-e-form\"></em><div class=\"ts-mfoot\">");
            if (!add)
            {
                sb.Append("<button type=\"button\" class=\"ts-btn ts-btn-danger ts-mleft\" onclick=\"" + Me + ".ask('" + Ui.Js(v.FieldId) + "', this)\">Delete</button>");
            }
            sb.Append("<button type=\"button\" class=\"ts-btn ts-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Cancel</button><button type=\"submit\" class=\"ts-btn ts-btn-accent\" id=\"ts-fsave\">" + (add ? "Add field" : "Save changes") + "</button></div></form>");
            return sb.ToString();
        }

        private static (bool Ok, decimal? Value) ParseNum(string v)
        {
            if (v == string.Empty)
            {
                return (true, null);
            }
            if (!Regex.IsMatch(v, "^-?(\\d{1,9}(\\.\\d{1,3})?|\\.\\d{1,3})$"))
            {
                return (false, null);
            }
            return decimal.TryParse(v, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Ui.Inv, out decimal d) ? (true, d) : (false, null);
        }

        private static List<string> Renumber(List<TstField> ordered, List<SqlParameter> ps)
        {
            List<string> sql = new List<string>();
            for (int i = 0; i < ordered.Count; i++)
            {
                sql.Add("UPDATE TESTFIELD SET FIELD_ORDER = @o" + i + " WHERE FIELD_ID = @f" + i);
                ps.Add(App.PInt("o" + i, i + 1));
                ps.Add(App.P("f" + i, ordered[i].FieldId, 50));
            }
            return sql;
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
                await App.Log(c.U.StaffId, "edit test field", "not allowed", false);
                Toast(response, "You can't change test fields.");
                return response;
            }
            string fid = CleanId(GetDataValue("fieldid"));
            bool add = fid == string.Empty;
            TstField? old = null;
            string itemId = CleanId(GetDataValue("testid"));
            if (add)
            {
                fid = NewKey();
                if (fid == string.Empty)
                {
                    Toast(response, "Please close this window and try again.");
                    return response;
                }
                TstField? done = await OneField(fid);
                if (done != null)
                {
                    await Redraw(response, c, done.ItemId, false);
                    response.ExecuteScript(Me + ".saved('" + Ui.Js(done.FieldName + " was added.") + "');");
                    return response;
                }
            }
            else
            {
                old = await OneField(fid);
                if (old == null)
                {
                    Toast(response, "This field was not found. It may have been deleted.");
                    return response;
                }
                itemId = old.ItemId;
            }
            TstTest? t = itemId == string.Empty ? null : await Test(itemId);
            if (t == null)
            {
                Toast(response, "This test was not found.");
                return response;
            }
            string name = Ui.OneLine(GetDataValue("fname"), 60);
            string sp = Val("fsp", 20);
            bool text = Val("ftext", 2) == "1";
            string unit = Ui.OneLine(GetDataValue("funit"), 20);
            string decText = Val("fdec", 3);
            (bool lowOk, decimal? low) = ParseNum(Val("flow", 20));
            (bool highOk, decimal? high) = ParseNum(Val("fhigh", 20));
            List<TstSpecies> species = await SpeciesList();
            Dictionary<string, string> errs = new Dictionary<string, string> { { "name", "" }, { "sp", "" }, { "unit", "" }, { "dec", "" }, { "low", "" }, { "high", "" }, { "form", "" } };
            if (name == string.Empty)
            {
                errs["name"] = "Enter the field name.";
            }
            if (sp != string.Empty && !species.Any(x => x.SpeciesKey == sp))
            {
                errs["sp"] = "Pick a species from the list.";
            }
            int dec = 0;
            if (!text)
            {
                if (!int.TryParse(decText, NumberStyles.None, Ui.Inv, out dec) || dec < 0 || dec > 4)
                {
                    errs["dec"] = "Pick 0 to 4 decimals.";
                }
                if (!lowOk)
                {
                    errs["low"] = "Enter a number like 5.5, or leave it blank.";
                }
                if (!highOk)
                {
                    errs["high"] = "Enter a number like 16.9, or leave it blank.";
                }
                if (lowOk && highOk && low.HasValue && high.HasValue && low >= high)
                {
                    errs["high"] = "The high value must be more than the low value.";
                }
            }
            else
            {
                unit = string.Empty;
                low = null;
                high = null;
                dec = 0;
            }
            if (errs["name"] == string.Empty && errs["sp"] == string.Empty)
            {
                var (dup, _) = await Helper.GetDataTable("SELECT 1 FROM TESTFIELD WHERE ITEM_ID = @iid AND SPECIES_KEY = @sp AND LOWER(FIELD_NAME) = LOWER(@name) AND FIELD_ID <> @fid", new List<SqlParameter> { App.P("iid", itemId, 50), App.P("sp", sp, 20), App.P("name", name, 60), App.P("fid", fid, 50) });
                if (dup.Rows.Count > 0)
                {
                    errs["name"] = "This test already has a field called " + Ui.Enc(name) + " for " + Ui.Enc(SpName(sp, species).ToLowerInvariant()) + ".";
                }
            }
            foreach (KeyValuePair<string, string> e in errs)
            {
                response.SetElementContents("ts-e-" + e.Key, e.Value);
            }
            if (errs.Values.Any(x => x != string.Empty))
            {
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.P("fid", fid, 50), App.P("iid", itemId, 50), App.P("name", name, 60), App.P("unit", unit, 20), App.P("sp", sp, 20),
                new SqlParameter("low", System.Data.SqlDbType.Decimal) { Precision = 12, Scale = 3, Value = low.HasValue ? low.Value : DBNull.Value },
                new SqlParameter("high", System.Data.SqlDbType.Decimal) { Precision = 12, Scale = 3, Value = high.HasValue ? high.Value : DBNull.Value },
                App.PInt("dec", dec), App.PBit("text", text)
            };
            List<string> sql = new List<string>();
            if (add)
            {
                List<TstField> cur = await Fields(itemId);
                TstField? twin = cur.FirstOrDefault(x => string.Equals(x.FieldName.Trim(), name, StringComparison.OrdinalIgnoreCase));
                int at = twin != null ? cur.IndexOf(twin) + 1 : cur.Count;
                while (at < cur.Count && string.Equals(cur[at].FieldName.Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    at++;
                }
                List<TstField> next = cur.ToList();
                next.Insert(at, new TstField { FieldId = fid });
                ps.Add(App.PInt("ord", at + 1));
                sql.Add("IF EXISTS (SELECT 1 FROM TESTFIELD WHERE FIELD_ID = @fid) THROW 50003, N'DONE', 1;");
                sql.Add("UPDATE ITEM SET SYSDTE = SYSDTE WHERE ITEM_ID = @iid");
                sql.Add("INSERT INTO TESTFIELD (FIELD_ID, ITEM_ID, FIELD_NAME, UNIT, SPECIES_KEY, LOW_VALUE, HIGH_VALUE, DECIMALS, IS_TEXT, FIELD_ORDER, SYSDTE) VALUES (@fid, @iid, @name, @unit, @sp, @low, @high, @dec, @text, @ord, GETDATE())");
                sql.AddRange(Renumber(next, ps));
            }
            else
            {
                sql.Add("UPDATE TESTFIELD SET FIELD_NAME = @name, UNIT = @unit, SPECIES_KEY = @sp, LOW_VALUE = @low, HIGH_VALUE = @high, DECIMALS = @dec, IS_TEXT = @text, SYSDTE = GETDATE() WHERE FIELD_ID = @fid AND ITEM_ID = @iid; IF @@ROWCOUNT = 0 THROW 50002, N'GONE', 1;");
            }
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result) && add && (result.Contains("DONE") || await OneField(fid) != null))
            {
                await Redraw(response, c, itemId, false);
                response.ExecuteScript(Me + ".saved('" + Ui.Js(name + " was added.") + "');");
                return response;
            }
            if (Helper.IsError(result))
            {
                await App.Log(c.U.StaffId, add ? "add test field" : "edit test field", t.ItemName + " · " + name + " failed", false);
                response.SetElementContents("ts-e-form", result.Contains("GONE") ? "This field was deleted by someone else." : "The field could not be saved. Try again.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            string what = name + (sp != string.Empty ? " (" + SpName(sp, species) + ")" : string.Empty) + (text ? " · text" : " · " + (low.HasValue ? Num(low.Value, dec) : "–") + " to " + (high.HasValue ? Num(high.Value, dec) : "–") + (unit != string.Empty ? " " + unit : string.Empty));
            if (old != null && !old.IsText && !text && (old.LowValue != low || old.HighValue != high))
            {
                what += " (was " + (old.LowValue.HasValue ? Num(old.LowValue.Value, old.Decimals) : "–") + " to " + (old.HighValue.HasValue ? Num(old.HighValue.Value, old.Decimals) : "–") + ")";
            }
            await App.Log(c.U.StaffId, add ? "added test field" : "edited test field", t.ItemName + " · " + what, true);
            await Redraw(response, c, itemId, false);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(name + (add ? " was added." : " was saved.")) + "');");
            return response;
        }

        public async Task<ApiResponse> Move()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change test fields.");
                return response;
            }
            TstField? f = await OneField(CleanId(GetDataValue("fieldid")));
            if (f == null)
            {
                Toast(response, "This field was not found. It may have been deleted.");
                return response;
            }
            List<TstField> cur = await Fields(f.ItemId);
            int i = cur.FindIndex(x => x.FieldId == f.FieldId);
            int j = Val("dir", 6) == "up" ? i - 1 : i + 1;
            if (i < 0 || j < 0 || j >= cur.Count)
            {
                await Redraw(response, c, f.ItemId, false);
                return response;
            }
            (cur[i], cur[j]) = (cur[j], cur[i]);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", f.ItemId, 50) };
            List<string> sql = new List<string> { "UPDATE ITEM SET SYSDTE = SYSDTE WHERE ITEM_ID = @iid" };
            sql.AddRange(Renumber(cur, ps));
            string result = await Helper.PutData(sql, ps);
            if (Helper.IsError(result))
            {
                Toast(response, "The order could not be changed. Try again.");
                return response;
            }
            await Redraw(response, c, f.ItemId, false);
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
            if (!c.Can(Key, 'E'))
            {
                Toast(response, "You can't change test fields.");
                return response;
            }
            TstField? f = await OneField(CleanId(GetDataValue("fieldid")));
            if (f == null)
            {
                Toast(response, "This field was not found. It may have been deleted.");
                return response;
            }
            string fid = Ui.Js(f.FieldId);
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>Delete " + Ui.Enc(f.FieldName) + "?</h2>");
            if (f.Results > 0)
            {
                sb.Append("<p class=\"ts-warn\"><b>" + Ui.Plural(f.Results, "result was", "results were") + " entered in this field.</b> They keep their own copy and stay in the pet charts, but new results for this test won't have this field.</p>");
            }
            else
            {
                sb.Append("<p class=\"ts-mlead\">No results were entered in this field. It is removed from the result form.</p>");
            }
            sb.Append("<div class=\"ts-mfoot\"><button type=\"button\" class=\"ts-btn ts-btn-ghost\" onclick=\"" + Me + ".open('" + fid + "')\">Keep it</button><button type=\"button\" class=\"ts-btn ts-btn-danger\" onclick=\"" + Me + ".remove('" + fid + "', this)\">Delete field</button></div>");
            response.SetElementContents("ts-mbody", sb.ToString());
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
            if (!c.Can(Key, 'E'))
            {
                await App.Log(c.U.StaffId, "delete test field", "not allowed", false);
                Toast(response, "You can't change test fields.");
                return response;
            }
            TstField? f = await OneField(CleanId(GetDataValue("fieldid")));
            if (f == null)
            {
                Toast(response, "This field was not found. It may have been deleted.");
                return response;
            }
            TstTest? t = await Test(f.ItemId);
            string result = await Helper.PutData(new List<string> { "DELETE FROM TESTFIELD WHERE FIELD_ID = @fid" }, new List<SqlParameter> { App.P("fid", f.FieldId, 50) });
            if (Helper.IsError(result))
            {
                Toast(response, "The field could not be deleted. Try again.");
                return response;
            }
            await App.Log(c.U.StaffId, "deleted test field", (t?.ItemName ?? "Test") + " · " + f.FieldName + (f.SpeciesKey != string.Empty ? " (" + f.SpeciesKey + ")" : string.Empty) + (f.Results > 0 ? " · " + Ui.Plural(f.Results, "result", "results") + " kept" : string.Empty), true);
            await Redraw(response, c, f.ItemId, false);
            response.ExecuteScript(Me + ".saved('" + Ui.Js(f.FieldName + " was deleted.") + "');");
            return response;
        }

        private static List<TstField> For(List<TstField> all, string species)
        {
            return all.Where(f => f.SpeciesKey == string.Empty || f.SpeciesKey == species).GroupBy(f => f.FieldName.Trim().ToLowerInvariant()).Select(g => g.FirstOrDefault(f => f.SpeciesKey != string.Empty) ?? g.First()).OrderBy(f => f.FieldOrder).ThenBy(f => f.FieldName).ToList();
        }

        private static string Mock(List<TstField> list, string speciesName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ts-mock\"><h3>For a " + Ui.Enc(speciesName.ToLowerInvariant()) + "</h3><div class=\"ts-rform\">");
            if (list.Count == 0)
            {
                list = new List<TstField> { new TstField { FieldName = "Result", IsText = true } };
            }
            foreach (TstField f in list)
            {
                string meta = f.IsText ? "Text" : (f.Unit != string.Empty ? Ui.Enc(f.Unit) + " &middot; " : string.Empty) + "normal " + Range(f);
                sb.Append("<div class=\"ts-rrow\"><label><b>" + Ui.Enc(f.FieldName) + "</b>" + (f.SpeciesKey != string.Empty ? "<small>" + Ui.Enc(speciesName) + " range</small>" : string.Empty) + "</label>");
                if (f.IsText)
                {
                    sb.Append("<input class=\"ts-rin ts-rtext\" type=\"text\" disabled placeholder=\"Written result\" aria-label=\"" + Ui.Attr(f.FieldName) + "\">");
                }
                else
                {
                    sb.Append("<input class=\"ts-rin\" type=\"text\" disabled placeholder=\"" + (f.Decimals > 0 ? "0." + new string('0', f.Decimals) : "0") + "\" aria-label=\"" + Ui.Attr(f.FieldName) + "\"><span class=\"ts-rmeta\">" + meta + "</span>");
                }
                sb.Append("</div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Preview()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                Toast(response, "You can't see test fields.");
                return response;
            }
            TstTest? t = await Test(CleanId(GetDataValue("testid")));
            if (t == null)
            {
                Toast(response, "This test was not found.");
                return response;
            }
            List<TstField> all = await Fields(t.ItemId);
            List<TstSpecies> sp = await SpeciesList();
            StringBuilder sb = new StringBuilder();
            sb.Append("<h2>" + Ui.Enc(t.ItemName) + "</h2><p class=\"ts-mlead\">How the result form looks when results are entered. Values outside the normal range are flagged high or low.</p><div class=\"ts-mocks\">");
            sb.Append(Mock(For(all, "dog"), SpName("dog", sp)));
            sb.Append(Mock(For(all, "cat"), SpName("cat", sp)));
            sb.Append("</div><div class=\"ts-mfoot\"><button type=\"button\" class=\"ts-btn ts-btn-accent\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            response.SetElementContents("ts-mbody", sb.ToString());
            response.ExecuteScript(Me + ".unlock();" + Me + ".shown(false);");
            return response;
        }
    }
}
