using System.Data;
using System.Globalization;
using System.Text;
using AnimalHospital.Core;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminReports : WebPage
    {
        private const string Key = "reports";
        private const string Me = "AdminReportsJs";
        private static readonly string[] Kinds = { "sales", "payments", "visits", "clients", "owed", "boarding" };
        private static readonly string[] Presets = { "today", "week", "month", "lastmonth", "year", "custom" };
        private static readonly string[] Groups = { "day", "week", "month" };
        private static readonly string[] Methods = { "card", "cash", "check", "other" };
        private static readonly string[] VisitTypes = { "clinic", "surgery", "grooming", "boarding" };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Reports | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--rp-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("rp"));
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

        private static string Denied()
        {
            return "<div class=\"rp-panel rp-deny\"><h1>Reports</h1><p>You don't have access to this page. Ask a manager if you need it.</p><a class=\"rp-btn rp-btn-ghost\" href=\"Admin\">Back to the dashboard</a></div>";
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript(Me + ".out();");
            return response;
        }

        private static string KindName(string k)
        {
            return k switch
            {
                "sales" => "Sales",
                "payments" => "Payments",
                "visits" => "Visits",
                "clients" => "Clients",
                "owed" => "Money owed",
                "boarding" => "Boarding",
                _ => k
            };
        }

        private static string PresetName(string p)
        {
            return p switch
            {
                "today" => "Today",
                "week" => "This week",
                "month" => "This month",
                "lastmonth" => "Last month",
                "year" => "This year",
                "custom" => "Choose dates",
                _ => p
            };
        }

        private static string TypeName(string t)
        {
            return t switch
            {
                "clinic" => "Clinic visits",
                "surgery" => "Surgery and dental",
                "grooming" => "Grooming",
                "boarding" => "Boarding",
                "" => "No visit (added by hand)",
                _ => t
            };
        }

        private static string ItemTypeName(string t)
        {
            return t switch
            {
                "practice" => "Exams and procedures",
                "medicine" => "Medicines",
                "test" => "Tests",
                "vaccine" => "Vaccines",
                "grooming" => "Grooming",
                "boarding" => "Boarding",
                "product" => "Products",
                "fee" => "Fees",
                "" => "Other",
                _ => t
            };
        }

        private sealed class Rng
        {
            public DateTime From;
            public DateTime To;
            public string Preset = "month";
            public string Group = "day";
            public string Note = string.Empty;
            public DateTime FromUtc;
            public DateTime ToUtc;
            public DateTime Today;
            public int Off0;
            public List<(DateTime At, int Off)> Cuts = new List<(DateTime At, int Off)>();
            public int Days => (To - From).Days + 1;
        }

        private static (Rng? R, string Error) Range(Dictionary<string, string> s, string preset, string from, string to, string group)
        {
            DateTime t = App.Now(s).Date;
            Rng r = new Rng { Preset = Presets.Contains(preset) ? preset : "month", Today = t };
            switch (r.Preset)
            {
                case "today":
                    r.From = t;
                    r.To = t;
                    break;
                case "week":
                    r.From = t.AddDays(-(int)t.DayOfWeek);
                    r.To = r.From.AddDays(6);
                    break;
                case "lastmonth":
                    r.From = new DateTime(t.Year, t.Month, 1).AddMonths(-1);
                    r.To = r.From.AddMonths(1).AddDays(-1);
                    break;
                case "year":
                    r.From = new DateTime(t.Year, 1, 1);
                    r.To = new DateTime(t.Year, 12, 31);
                    break;
                case "custom":
                    {
                        DateTime? a = Ui.ParseDay(from);
                        DateTime? z = Ui.ParseDay(to);
                        if (a == null || z == null)
                        {
                            return (null, "Choose both dates.");
                        }
                        if (z.Value < a.Value)
                        {
                            return (null, "The end date is before the start date.");
                        }
                        if ((z.Value - a.Value).Days > 1095)
                        {
                            return (null, "Choose a range of 3 years or less.");
                        }
                        if (a.Value.Year < 2000 || z.Value.Year > 2100)
                        {
                            return (null, "Choose dates between 2000 and 2100.");
                        }
                        r.From = a.Value;
                        r.To = z.Value;
                        break;
                    }
                default:
                    r.From = new DateTime(t.Year, t.Month, 1);
                    r.To = r.From.AddMonths(1).AddDays(-1);
                    break;
            }
            string g = Groups.Contains(group) ? group : (r.Days <= 31 ? "day" : r.Days <= 190 ? "week" : "month");
            if (g == "day" && r.Days > 92)
            {
                g = "week";
                r.Note = "Grouped by week because the range is long.";
            }
            if (g == "week" && r.Days > 730)
            {
                g = "month";
                r.Note = "Grouped by month because the range is long.";
            }
            r.Group = g;
            TimeZoneInfo z0 = App.Zone(App.S(s, "timezone"));
            r.FromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(r.From, DateTimeKind.Unspecified), z0);
            r.ToUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(r.To.AddDays(1), DateTimeKind.Unspecified), z0);
            DateTime a0 = r.FromUtc.AddDays(-1);
            DateTime b0 = r.ToUtc.AddDays(1);
            int off = (int)z0.GetUtcOffset(DateTime.SpecifyKind(a0, DateTimeKind.Utc)).TotalMinutes;
            r.Off0 = off;
            for (DateTime x = a0; x < b0; x = x.AddMinutes(30))
            {
                int o = (int)z0.GetUtcOffset(DateTime.SpecifyKind(x, DateTimeKind.Utc)).TotalMinutes;
                if (o != off)
                {
                    r.Cuts.Add((x, o));
                    off = o;
                }
            }
            return (r, string.Empty);
        }

        private static string LocalDay(Rng r, string col)
        {
            if (r.Cuts.Count == 0)
            {
                return "CAST(DATEADD(MINUTE, @o0, " + col + ") AS DATE)";
            }
            StringBuilder sb = new StringBuilder("CAST(DATEADD(MINUTE, CASE");
            for (int i = 0; i < r.Cuts.Count; i++)
            {
                sb.Append(" WHEN " + col + " < @c" + (i + 1).ToString(Ui.Inv) + " THEN @o" + i.ToString(Ui.Inv));
            }
            sb.Append(" ELSE @o" + r.Cuts.Count.ToString(Ui.Inv) + " END, " + col + ") AS DATE)");
            return sb.ToString();
        }

        private static List<SqlParameter> Ps(Rng r)
        {
            List<SqlParameter> ps = new List<SqlParameter>
            {
                App.PDate("fu", r.FromUtc), App.PDate("tu", r.ToUtc), App.PDate("f", r.From), App.PDate("t1", r.To.AddDays(1)),
                new SqlParameter("fd", SqlDbType.Date) { Value = r.From }, new SqlParameter("t1d", SqlDbType.Date) { Value = r.To.AddDays(1) },
                new SqlParameter("td", SqlDbType.Date) { Value = r.Today }, App.PInt("o0", r.Off0)
            };
            for (int i = 0; i < r.Cuts.Count; i++)
            {
                ps.Add(App.PDate("c" + (i + 1).ToString(Ui.Inv), r.Cuts[i].At));
                ps.Add(App.PInt("o" + (i + 1).ToString(Ui.Inv), r.Cuts[i].Off));
            }
            return ps;
        }

        private static async Task<List<DataRow>> Rows(string sql, Rng r)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, Ps(r));
            if (emsg != string.Empty)
            {
                throw new InvalidOperationException(emsg);
            }
            return dt.Rows.Cast<DataRow>().ToList();
        }

        private static string S(DataRow row, int i)
        {
            return row[i] == DBNull.Value ? string.Empty : Convert.ToString(row[i], CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static int I(DataRow row, int i)
        {
            return row[i] == DBNull.Value ? 0 : Convert.ToInt32(row[i], CultureInfo.InvariantCulture);
        }

        private static decimal M(DataRow row, int i)
        {
            return row[i] == DBNull.Value ? 0m : Convert.ToDecimal(row[i], CultureInfo.InvariantCulture);
        }

        private static DateTime? Dt(DataRow row, int i)
        {
            return row[i] == DBNull.Value ? null : Convert.ToDateTime(row[i], CultureInfo.InvariantCulture).Date;
        }

        private static DateTime Bucket(Rng r, DateTime d)
        {
            return r.Group switch
            {
                "week" => d.AddDays(-(int)d.DayOfWeek),
                "month" => new DateTime(d.Year, d.Month, 1),
                _ => d.Date
            };
        }

        private static List<DateTime> Buckets(Rng r)
        {
            List<DateTime> list = new List<DateTime>();
            for (DateTime d = r.From; d <= r.To; d = d.AddDays(1))
            {
                DateTime b = Bucket(r, d);
                if (list.Count == 0 || list[^1] != b)
                {
                    list.Add(b);
                }
            }
            return list;
        }

        private static string Period(Rng r, DateTime b)
        {
            bool yr = r.From.Year != r.To.Year;
            return r.Group switch
            {
                "week" => "Week of " + b.ToString(yr ? "MMM d, yyyy" : "MMM d", Ui.Us),
                "month" => b.ToString("MMMM yyyy", Ui.Us),
                _ => b.ToString(yr ? "ddd MMM d, yyyy" : "ddd MMM d", Ui.Us)
            };
        }

        private static string PeriodShort(Rng r, DateTime b)
        {
            return r.Group switch
            {
                "month" => b.ToString(r.From.Year != r.To.Year ? "MMM yy" : "MMM", Ui.Us),
                _ => b.ToString("MMM d", Ui.Us)
            };
        }

        private readonly struct Cell
        {
            public Cell(string html, string raw, bool num)
            {
                Html = html;
                Raw = raw;
                Num = num;
            }

            public string Html { get; }
            public string Raw { get; }
            public bool Num { get; }
        }

        private static Cell T(string text)
        {
            return new Cell(Ui.Enc(text), text, false);
        }

        private static Cell Ln(string html, string raw)
        {
            return new Cell(html, raw, false);
        }

        private static Cell N(int n)
        {
            return new Cell(n.ToString("#,##0", Ui.Inv), n.ToString(Ui.Inv), true);
        }

        private static Cell Q(decimal n)
        {
            return new Cell(Ui.Num(n), n.ToString("0.##", Ui.Inv), true);
        }

        private static Cell Mo(decimal v)
        {
            return new Cell(Ui.Money(v), v.ToString("0.00", Ui.Inv), true);
        }

        private static string PctText(decimal part, decimal whole)
        {
            return whole == 0 ? "-" : (part * 100m / whole).ToString("0.0", Ui.Inv) + "%";
        }

        private static Cell Pc(decimal part, decimal whole)
        {
            string t = PctText(part, whole);
            return new Cell(t == "-" ? "&ndash;" : t, t == "-" ? string.Empty : t, true);
        }

        private static string MinText(decimal mins)
        {
            int m = (int)Math.Round(mins);
            return m < 60 ? m.ToString(Ui.Inv) + " min" : (m / 60).ToString(Ui.Inv) + " h " + (m % 60).ToString("00", Ui.Inv) + " min";
        }

        private static Cell Mi(decimal sum, int count)
        {
            return count == 0 ? new Cell("&ndash;", string.Empty, true) : new Cell(MinText(sum / count), Math.Round(sum / count, 1).ToString("0.0", Ui.Inv), true);
        }

        private sealed class Rep
        {
            public StringBuilder H = new StringBuilder();
            public List<string> Csv = new List<string>();
        }

        private static void Tiles(Rep rep, List<(string Label, string Value, string Note)> tiles)
        {
            rep.H.Append("<div class=\"rp-tiles\">");
            foreach (var t in tiles)
            {
                rep.H.Append("<div class=\"rp-tile\"><small>" + Ui.Enc(t.Label) + "</small><b>" + t.Value + "</b>" + (t.Note != string.Empty ? "<span>" + Ui.Enc(t.Note) + "</span>" : string.Empty) + "</div>");
            }
            rep.H.Append("</div>");
        }

        private static string Csv(IEnumerable<string> cells)
        {
            return string.Join(",", cells.Select(Ui.Csv));
        }

        private static void Table(Rep rep, string title, string note, string[] heads, List<Cell[]> rows, Cell[]? foot, string empty = "Nothing in this range.", bool skipZero = false)
        {
            if (skipZero)
            {
                int before = rows.Count;
                rows = rows.Where(r => r.Skip(1).Any(x => x.Raw != string.Empty && x.Raw != "0" && x.Raw != "0.00")).ToList();
                if (rows.Count < before)
                {
                    note = (note != string.Empty ? note + " " : string.Empty) + (rows.Count == 0 ? string.Empty : "Periods with nothing are left out.");
                }
            }
            rep.H.Append("<section class=\"rp-sect\"><h2 class=\"rp-h2\">" + Ui.Enc(title) + "</h2>" + (note != string.Empty ? "<p class=\"rp-tnote\">" + Ui.Enc(note) + "</p>" : string.Empty));
            rep.Csv.Add(Csv(new[] { title }));
            rep.Csv.Add(Csv(heads));
            if (rows.Count == 0)
            {
                rep.H.Append("<div class=\"rp-tw\"><p class=\"rp-empty\">" + Ui.Enc(empty) + "</p></div></section>");
                rep.Csv.Add(string.Empty);
                return;
            }
            rep.H.Append("<div class=\"rp-tw\"><table class=\"rp-table\"><thead><tr>");
            for (int i = 0; i < heads.Length; i++)
            {
                bool num = rows[0].Length > i && rows[0][i].Num;
                rep.H.Append("<th" + (num ? " class=\"rp-num\"" : string.Empty) + " scope=\"col\">" + Ui.Enc(heads[i]) + "</th>");
            }
            rep.H.Append("</tr></thead><tbody>");
            foreach (Cell[] r in rows)
            {
                Row(rep, heads, r, false);
            }
            rep.H.Append("</tbody>");
            if (foot != null)
            {
                rep.H.Append("<tfoot>");
                Row(rep, heads, foot, true);
                rep.H.Append("</tfoot>");
            }
            rep.H.Append("</table></div></section>");
            rep.Csv.Add(string.Empty);
        }

        private static void Row(Rep rep, string[] heads, Cell[] r, bool foot)
        {
            rep.H.Append("<tr" + (foot ? " class=\"rp-trow\"" : string.Empty) + ">");
            for (int i = 0; i < r.Length; i++)
            {
                if (i == 0)
                {
                    rep.H.Append("<th scope=\"row\" class=\"rp-cname\"><b>" + r[i].Html + "</b></th>");
                }
                else
                {
                    rep.H.Append("<td data-l=\"" + Ui.Attr(heads[i]) + "\"" + (r[i].Num ? " class=\"rp-num\"" : string.Empty) + ">" + r[i].Html + "</td>");
                }
            }
            rep.H.Append("</tr>");
            rep.Csv.Add(Csv(r.Select(x => x.Raw)));
        }

        private static (decimal Max, int Lines) Nice(decimal max, bool money)
        {
            if (max <= 0)
            {
                return (4, 4);
            }
            double m = (double)max;
            double p = Math.Pow(10, Math.Floor(Math.Log10(m / 5)));
            double[] f = money ? new double[] { 1, 2, 2.5, 5, 10, 20, 25, 50 } : new double[] { 1, 2, 5, 10, 20, 50 };
            foreach (double x in f)
            {
                double step = Math.Max(x * p, 1);
                int n = (int)Math.Ceiling(m / step);
                if (n <= 5)
                {
                    return ((decimal)(step * Math.Max(n, 1)), Math.Max(n, 1));
                }
            }
            return ((decimal)m, 4);
        }

        private static string Axis(decimal v, bool money, bool pct)
        {
            if (pct)
            {
                return v.ToString("0.#", Ui.Inv) + "%";
            }
            string p = money ? "$" : string.Empty;
            if (v >= 1000000)
            {
                return p + (v / 1000000m).ToString("0.#", Ui.Inv) + "M";
            }
            if (v >= 10000)
            {
                return p + (v / 1000m).ToString("0", Ui.Inv) + "k";
            }
            if (v >= 1000)
            {
                return p + (v / 1000m).ToString("0.#", Ui.Inv) + "k";
            }
            return p + v.ToString("0.##", Ui.Inv);
        }

        private static void Chart(Rep rep, string title, List<(string Label, string Short, decimal Value, string Text)> bars, bool money, bool pct = false)
        {
            const int w = 760;
            const int h = 260;
            const int pl = 58;
            const int pr = 14;
            const int pt = 22;
            const int pb = 36;
            int n = Math.Max(1, bars.Count);
            var (max, lines) = Nice(bars.Count == 0 ? 0 : bars.Max(b => b.Value), money);
            double plotW = w - pl - pr;
            double plotH = h - pt - pb;
            double band = plotW / n;
            double bw = Math.Max(1.5, Math.Min(46, band * 0.7));
            int every = Math.Max(1, (int)Math.Ceiling(n / 12.0));
            string id = "rp-ch";
            decimal total = bars.Sum(b => b.Value);
            int top = bars.Count == 0 ? -1 : bars.FindIndex(b => b.Value == bars.Max(x => x.Value));
            StringBuilder sb = new StringBuilder();
            sb.Append("<section class=\"rp-sect rp-chartp\"><h2 class=\"rp-h2\" id=\"" + id + "-t\">" + Ui.Enc(title) + "</h2><div class=\"rp-chartw\">");
            sb.Append("<svg class=\"rp-chart\" viewBox=\"0 0 " + w + " " + h + "\" role=\"img\" aria-labelledby=\"" + id + "-t " + id + "-d\" preserveAspectRatio=\"xMinYMin meet\"><desc id=\"" + id + "-d\">" + Ui.Enc(Ui.Plural(bars.Count, "bar", "bars") + (bars.Count > 0 ? ", highest " + bars[top].Label + " " + bars[top].Text : string.Empty) + ". The same numbers are in the table below.") + "</desc>");
            for (int g = 0; g <= lines; g++)
            {
                decimal v = max * g / lines;
                double y = pt + plotH - plotH * g / lines;
                string ys = y.ToString("0.#", Ui.Inv);
                sb.Append("<line class=\"" + (g == 0 ? "rp-base" : "rp-grid") + "\" x1=\"" + pl + "\" x2=\"" + (w - pr) + "\" y1=\"" + ys + "\" y2=\"" + ys + "\"/>");
                sb.Append("<text class=\"rp-ax\" x=\"" + (pl - 8) + "\" y=\"" + (y + 4).ToString("0.#", Ui.Inv) + "\" text-anchor=\"end\">" + Ui.Enc(Axis(v, money, pct)) + "</text>");
            }
            for (int i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                double x = pl + band * i + (band - bw) / 2;
                double bh = max <= 0 ? 0 : plotH * (double)(b.Value / max);
                double y = pt + plotH - bh;
                double r = Math.Min(4, Math.Min(bw / 2, bh));
                string tip = b.Label + ": " + b.Text;
                sb.Append("<g class=\"rp-bar\" tabindex=\"0\" aria-label=\"" + Ui.Attr(tip) + "\"><title>" + Ui.Enc(tip) + "</title><rect class=\"rp-hit\" x=\"" + (pl + band * i).ToString("0.##", Ui.Inv) + "\" y=\"" + pt + "\" width=\"" + band.ToString("0.##", Ui.Inv) + "\" height=\"" + plotH.ToString("0.##", Ui.Inv) + "\"/>");
                if (bh > 0.4)
                {
                    string X(double v) => v.ToString("0.##", Ui.Inv);
                    double baseY = pt + plotH;
                    sb.Append("<path class=\"rp-fill\" d=\"M" + X(x) + " " + X(baseY) + "V" + X(y + r) + "Q" + X(x) + " " + X(y) + " " + X(x + r) + " " + X(y) + "H" + X(x + bw - r) + "Q" + X(x + bw) + " " + X(y) + " " + X(x + bw) + " " + X(y + r) + "V" + X(baseY) + "Z\"/>");
                }
                if (i == top && b.Value > 0)
                {
                    sb.Append("<text class=\"rp-vl\" x=\"" + (x + bw / 2).ToString("0.##", Ui.Inv) + "\" y=\"" + Math.Max(12, y - 6).ToString("0.##", Ui.Inv) + "\" text-anchor=\"middle\">" + Ui.Enc(b.Text) + "</text>");
                }
                if (i % every == 0)
                {
                    sb.Append("<text class=\"rp-ax\" x=\"" + (pl + band * i + band / 2).ToString("0.##", Ui.Inv) + "\" y=\"" + (h - pb + 18) + "\" text-anchor=\"middle\">" + Ui.Enc(b.Short) + "</text>");
                }
                sb.Append("</g>");
            }
            if (bars.Count == 0 || total == 0)
            {
                sb.Append("<text class=\"rp-ax\" x=\"" + (pl + plotW / 2).ToString("0", Ui.Inv) + "\" y=\"" + (pt + plotH / 2).ToString("0", Ui.Inv) + "\" text-anchor=\"middle\">Nothing in this range</text>");
            }
            sb.Append("</svg></div></section>");
            rep.H.Append(sb);
        }

        private static string Money(decimal v)
        {
            return Ui.Money(v).Replace("&minus;", "-");
        }

        private static async Task<string> Sales(Rep rep, Rng r)
        {
            string w = " WHERE I.STATUS <> N'void' AND I.CREATED_UTC >= @fu AND I.CREATED_UTC < @tu";
            List<DataRow> days = await Rows("SELECT D, COUNT(*), SUM(SUBTOTAL), SUM(DISC), SUM(TAX), SUM(TOTAL) FROM (SELECT " + LocalDay(r, "I.CREATED_UTC") + " AS D, I.SUBTOTAL, CASE WHEN I.DISCOUNT < 0 THEN 0 WHEN I.DISCOUNT > I.SUBTOTAL THEN I.SUBTOTAL ELSE I.DISCOUNT END AS DISC, I.TAX, I.TOTAL FROM INVOICE I" + w + ") Z GROUP BY D", r);
            List<DataRow> types = await Rows("SELECT L.ITEM_TYPE, COUNT(*), SUM(L.QTY), SUM(L.AMOUNT) FROM INVOICELINE L JOIN INVOICE I ON I.INVOICE_ID = L.INVOICE_ID" + w + " AND L.QTY > 0 GROUP BY L.ITEM_TYPE ORDER BY SUM(L.AMOUNT) DESC", r);
            List<DataRow> vt = await Rows("SELECT VT, COUNT(*), SUM(TOTAL) FROM (SELECT ISNULL(V.VISIT_TYPE, N'') AS VT, I.TOTAL FROM INVOICE I LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID AND I.VISIT_ID <> N''" + w + ") Z GROUP BY VT ORDER BY SUM(TOTAL) DESC", r);
            List<DataRow> vets = await Rows("SELECT SID, MAX(SN), COUNT(*), SUM(TOTAL) FROM (SELECT ISNULL(V.STAFF_ID, N'') AS SID, ISNULL(S.STAFF_NAME, N'') AS SN, I.TOTAL FROM INVOICE I LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID AND I.VISIT_ID <> N'' LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID" + w + ") Z GROUP BY SID ORDER BY SUM(TOTAL) DESC", r);
            List<DataRow> items = await Rows("SELECT TOP 15 K, MAX(NM), SUM(QTY), SUM(AMOUNT), MAX(TP) FROM (SELECT CASE WHEN L.ITEM_ID <> N'' THEN L.ITEM_ID ELSE N'~' + L.DESCRIPTION END AS K, ISNULL(IT.ITEM_NAME, L.DESCRIPTION) AS NM, L.QTY, L.AMOUNT, L.ITEM_TYPE AS TP FROM INVOICELINE L JOIN INVOICE I ON I.INVOICE_ID = L.INVOICE_ID LEFT JOIN ITEM IT ON IT.ITEM_ID = L.ITEM_ID AND L.ITEM_ID <> N''" + w + " AND L.QTY > 0) Z GROUP BY K ORDER BY SUM(AMOUNT) DESC", r);
            Dictionary<DateTime, (int N, decimal Sub, decimal Disc, decimal Tax, decimal Tot)> by = new Dictionary<DateTime, (int, decimal, decimal, decimal, decimal)>();
            foreach (DataRow d in days)
            {
                DateTime? day = Dt(d, 0);
                if (day == null)
                {
                    continue;
                }
                DateTime b = Bucket(r, day.Value);
                var cur = by.TryGetValue(b, out var x) ? x : (0, 0m, 0m, 0m, 0m);
                by[b] = (cur.Item1 + I(d, 1), cur.Item2 + M(d, 2), cur.Item3 + M(d, 3), cur.Item4 + M(d, 4), cur.Item5 + M(d, 5));
            }
            int n = by.Values.Sum(v => v.N);
            decimal sub = by.Values.Sum(v => v.Sub), disc = by.Values.Sum(v => v.Disc), tax = by.Values.Sum(v => v.Tax), tot = by.Values.Sum(v => v.Tot);
            Tiles(rep, new List<(string, string, string)>
            {
                ("Total sales", Ui.Money(tot), "Including tax"),
                ("Invoices", n.ToString("#,##0", Ui.Inv), "Void invoices left out"),
                ("Average invoice", n == 0 ? "&ndash;" : Ui.Money(Math.Round(tot / n, 2)), string.Empty),
                ("Before discount", Ui.Money(sub), string.Empty),
                ("Discounts", Ui.Money(disc), PctText(disc, sub) == "-" ? string.Empty : PctText(disc, sub) + " of sales"),
                ("Tax", Ui.Money(tax), string.Empty)
            });
            List<DateTime> bks = Buckets(r);
            Chart(rep, "Total sales by " + r.Group, bks.Select(b => (Period(r, b), PeriodShort(r, b), by.TryGetValue(b, out var v) ? v.Tot : 0m, Money(by.TryGetValue(b, out var v2) ? v2.Tot : 0m))).ToList(), true);
            Table(rep, "Sales by " + r.Group, "Invoices made in this range. Void invoices are left out; invoices still at checkout are included.", new[] { "Period", "Invoices", "Before discount", "Discount", "Tax", "Total" },
                bks.Select(b => { var v = by.TryGetValue(b, out var x) ? x : (0, 0m, 0m, 0m, 0m); return new[] { new Cell(Ui.Enc(Period(r, b)), Ui.Ymd(b), false), N(v.Item1), Mo(v.Item2), Mo(v.Item3), Mo(v.Item4), Mo(v.Item5) }; }).ToList(),
                new[] { T("Total"), N(n), Mo(sub), Mo(disc), Mo(tax), Mo(tot) }, "Nothing in this range.", true);
            decimal lineTot = types.Sum(x => M(x, 3));
            rep.H.Append("<div class=\"rp-two\">");
            Table(rep, "By kind of item", "Invoice lines before discount and tax.", new[] { "Kind", "Lines", "Amount", "Share" },
                types.Select(x => new[] { T(ItemTypeName(S(x, 0))), N(I(x, 1)), Mo(M(x, 3)), Pc(M(x, 3), lineTot) }).ToList(),
                new[] { T("Total"), N(types.Sum(x => I(x, 1))), Mo(lineTot), Pc(lineTot, lineTot) });
            Table(rep, "By kind of visit", string.Empty, new[] { "Visit", "Invoices", "Total", "Share" },
                vt.Select(x => new[] { T(TypeName(S(x, 0))), N(I(x, 1)), Mo(M(x, 2)), Pc(M(x, 2), tot) }).ToList(), null);
            rep.H.Append("</div><div class=\"rp-two\">");
            Table(rep, "By vet or groomer", "The person in charge of the visit.", new[] { "Staff", "Invoices", "Total", "Share" },
                vets.Select(x => new[] { T(S(x, 1) != string.Empty ? S(x, 1) : "No one assigned"), N(I(x, 2)), Mo(M(x, 3)), Pc(M(x, 3), tot) }).ToList(), null);
            Table(rep, "Top 15 items", "By amount, before discount and tax.", new[] { "Item", "Quantity", "Amount" },
                items.Select(x => new[] { T(S(x, 1)), Q(M(x, 2)), Mo(M(x, 3)) }).ToList(), null);
            rep.H.Append("</div>");
            return "sales";
        }

        private static async Task<string> Payments(Rep rep, Rng r)
        {
            string w = " WHERE P.IS_VOID = 0 AND P.PAID_UTC >= @fu AND P.PAID_UTC < @tu";
            List<DataRow> days = await Rows("SELECT D, M, COUNT(*), SUM(AMOUNT) FROM (SELECT " + LocalDay(r, "P.PAID_UTC") + " AS D, P.METHOD AS M, P.AMOUNT FROM PAYMENT P" + w + ") Z GROUP BY D, M", r);
            List<DataRow> staff = await Rows("SELECT SID, MAX(SN), COUNT(*), SUM(AMOUNT) FROM (SELECT P.STAFF_ID AS SID, ISNULL(S.STAFF_NAME, N'') AS SN, P.AMOUNT FROM PAYMENT P LEFT JOIN STAFF S ON S.STAFF_ID = P.STAFF_ID" + w + ") Z GROUP BY SID ORDER BY SUM(AMOUNT) DESC", r);
            List<DataRow> voids = await Rows("SELECT COUNT(*), ISNULL(SUM(P.AMOUNT), 0) FROM PAYMENT P WHERE P.IS_VOID = 1 AND P.PAID_UTC >= @fu AND P.PAID_UTC < @tu", r);
            Dictionary<DateTime, decimal[]> by = new Dictionary<DateTime, decimal[]>();
            Dictionary<string, (int N, decimal A)> meth = Methods.ToDictionary(m => m, m => (0, 0m));
            int count = 0;
            Dictionary<DateTime, int> cnt = new Dictionary<DateTime, int>();
            foreach (DataRow d in days)
            {
                DateTime? day = Dt(d, 0);
                if (day == null)
                {
                    continue;
                }
                DateTime b = Bucket(r, day.Value);
                string m = Methods.Contains(S(d, 1)) ? S(d, 1) : "other";
                int k = Array.IndexOf(Methods, m);
                if (!by.ContainsKey(b))
                {
                    by[b] = new decimal[4];
                    cnt[b] = 0;
                }
                by[b][k] += M(d, 3);
                cnt[b] += I(d, 2);
                meth[m] = (meth[m].N + I(d, 2), meth[m].A + M(d, 3));
                count += I(d, 2);
            }
            decimal total = meth.Values.Sum(v => v.A);
            int vn = voids.Count > 0 ? I(voids[0], 0) : 0;
            Tiles(rep, new List<(string, string, string)>
            {
                ("Payments taken", Ui.Money(total), Ui.Plural(count, "payment", "payments")),
                ("Card", Ui.Money(meth["card"].A), PctText(meth["card"].A, total) == "-" ? string.Empty : PctText(meth["card"].A, total)),
                ("Cash", Ui.Money(meth["cash"].A), PctText(meth["cash"].A, total) == "-" ? string.Empty : PctText(meth["cash"].A, total)),
                ("Check", Ui.Money(meth["check"].A), PctText(meth["check"].A, total) == "-" ? string.Empty : PctText(meth["check"].A, total)),
                ("Other", Ui.Money(meth["other"].A), PctText(meth["other"].A, total) == "-" ? string.Empty : PctText(meth["other"].A, total)),
                ("Voided", vn.ToString("#,##0", Ui.Inv), vn > 0 ? Money(voids.Count > 0 ? M(voids[0], 1) : 0m) + " not counted" : "None in this range")
            });
            List<DateTime> bks = Buckets(r);
            Chart(rep, "Payments by " + r.Group, bks.Select(b => (Period(r, b), PeriodShort(r, b), by.TryGetValue(b, out var v) ? v.Sum() : 0m, Money(by.TryGetValue(b, out var v2) ? v2.Sum() : 0m))).ToList(), true);
            Table(rep, "Payments by " + r.Group, "Payments taken in this range, by when they were taken. Voided payments are left out.", new[] { "Period", "Payments", "Card", "Cash", "Check", "Other", "Total" },
                bks.Select(b => { decimal[] v = by.TryGetValue(b, out var x) ? x : new decimal[4]; int c = cnt.TryGetValue(b, out int y) ? y : 0; return new[] { new Cell(Ui.Enc(Period(r, b)), Ui.Ymd(b), false), N(c), Mo(v[0]), Mo(v[1]), Mo(v[2]), Mo(v[3]), Mo(v.Sum()) }; }).ToList(),
                new[] { T("Total"), N(count), Mo(meth["card"].A), Mo(meth["cash"].A), Mo(meth["check"].A), Mo(meth["other"].A), Mo(total) }, "Nothing in this range.", true);
            rep.H.Append("<div class=\"rp-two\">");
            Table(rep, "By how owners paid", string.Empty, new[] { "Method", "Payments", "Amount", "Share" },
                Methods.Where(m => meth[m].N > 0).Select(m => new[] { T(Billing.MethodName(m)), N(meth[m].N), Mo(meth[m].A), Pc(meth[m].A, total) }).ToList(),
                new[] { T("Total"), N(count), Mo(total), Pc(total, total) });
            Table(rep, "Who took the payments", string.Empty, new[] { "Staff", "Payments", "Amount" },
                staff.Select(x => new[] { T(S(x, 1) != string.Empty ? S(x, 1) : "Online or not recorded"), N(I(x, 2)), Mo(M(x, 3)) }).ToList(), null);
            rep.H.Append("</div>");
            return "payments";
        }

        private const string WaitCase = "CASE WHEN V.VISIT_TYPE <> N'boarding' AND V.START_AT IS NOT NULL AND V.START_AT >= V.CHECKIN_AT AND DATEDIFF(MINUTE, V.CHECKIN_AT, V.START_AT) < 1440 THEN DATEDIFF(MINUTE, V.CHECKIN_AT, V.START_AT) END";
        private const string RoomCase = "CASE WHEN V.VISIT_TYPE <> N'boarding' AND V.START_AT IS NOT NULL AND V.READY_AT IS NOT NULL AND V.READY_AT >= V.START_AT AND DATEDIFF(MINUTE, V.START_AT, V.READY_AT) < 1440 THEN DATEDIFF(MINUTE, V.START_AT, V.READY_AT) END";

        private static async Task<string> Visits(Rep rep, Rng r)
        {
            string w = " WHERE V.STATUS <> N'cancelled' AND V.CHECKIN_AT >= @f AND V.CHECKIN_AT < @t1";
            List<DataRow> days = await Rows("SELECT D, VT, COUNT(*) FROM (SELECT CAST(V.CHECKIN_AT AS DATE) AS D, V.VISIT_TYPE AS VT FROM VISIT V" + w + ") Z GROUP BY D, VT", r);
            List<DataRow> types = await Rows("SELECT VT, COUNT(*), SUM(WM), COUNT(WM), SUM(RM), COUNT(RM) FROM (SELECT V.VISIT_TYPE AS VT, " + WaitCase + " AS WM, " + RoomCase + " AS RM FROM VISIT V" + w + ") Z GROUP BY VT", r);
            List<DataRow> staff = await Rows("SELECT SID, MAX(SN), COUNT(*), SUM(WM), COUNT(WM), SUM(RM), COUNT(RM) FROM (SELECT V.STAFF_ID AS SID, ISNULL(S.STAFF_NAME, N'') AS SN, " + WaitCase + " AS WM, " + RoomCase + " AS RM FROM VISIT V LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID" + w + ") Z GROUP BY SID ORDER BY COUNT(*) DESC", r);
            List<DataRow> appts = await Rows("SELECT VT, ST, COUNT(*) FROM (SELECT A.VISIT_TYPE AS VT, A.STATUS AS ST FROM APPOINTMENT A WHERE A.START_AT >= @f AND A.START_AT < @t1) Z GROUP BY VT, ST", r);
            Dictionary<DateTime, Dictionary<string, int>> by = new Dictionary<DateTime, Dictionary<string, int>>();
            foreach (DataRow d in days)
            {
                DateTime? day = Dt(d, 0);
                if (day == null)
                {
                    continue;
                }
                DateTime b = Bucket(r, day.Value);
                if (!by.ContainsKey(b))
                {
                    by[b] = new Dictionary<string, int>();
                }
                string t = S(d, 1);
                by[b][t] = (by[b].TryGetValue(t, out int x) ? x : 0) + I(d, 2);
            }
            int total = types.Sum(x => I(x, 1));
            decimal wsum = types.Sum(x => M(x, 2));
            int wn = types.Sum(x => I(x, 3));
            decimal rsum = types.Sum(x => M(x, 4));
            int rn = types.Sum(x => I(x, 5));
            int Ap(string st) => appts.Where(a => S(a, 1) == st).Sum(a => I(a, 2));
            int arrived = Ap("checkedin");
            int noshow = Ap("noshow");
            int cancelled = Ap("cancelled");
            int over = arrived + noshow + cancelled;
            Tiles(rep, new List<(string, string, string)>
            {
                ("Visits", total.ToString("#,##0", Ui.Inv), "Cancelled visits left out"),
                ("Average wait", wn == 0 ? "&ndash;" : Ui.Enc(MinText(wsum / wn)), "Check-in to start, boarding left out"),
                ("Average time with the vet or groomer", rn == 0 ? "&ndash;" : Ui.Enc(MinText(rsum / rn)), "Start to ready for checkout"),
                ("No-shows", PctText(noshow, over) == "-" ? "&ndash;" : PctText(noshow, over), noshow.ToString("#,##0", Ui.Inv) + " of " + over.ToString("#,##0", Ui.Inv) + " past appointments"),
                ("Cancelled", PctText(cancelled, over) == "-" ? "&ndash;" : PctText(cancelled, over), cancelled.ToString("#,##0", Ui.Inv) + " of " + over.ToString("#,##0", Ui.Inv) + " past appointments")
            });
            List<DateTime> bks = Buckets(r);
            Chart(rep, "Visits by " + r.Group, bks.Select(b => { int v = by.TryGetValue(b, out var x) ? x.Values.Sum() : 0; return (Period(r, b), PeriodShort(r, b), (decimal)v, Ui.Plural(v, "visit", "visits")); }).ToList(), false);
            List<string> vts = VisitTypes.ToList();
            foreach (string t in by.Values.SelectMany(x => x.Keys).Distinct().Where(t => !vts.Contains(t)))
            {
                vts.Add(t);
            }
            List<string> heads = new List<string> { "Period" };
            heads.AddRange(vts.Select(TypeName));
            heads.Add("Total");
            Table(rep, "Visits by " + r.Group, "By check-in date.", heads.ToArray(),
                bks.Select(b => { var x = by.TryGetValue(b, out var y) ? y : new Dictionary<string, int>(); List<Cell> cells = new List<Cell> { new Cell(Ui.Enc(Period(r, b)), Ui.Ymd(b), false) }; cells.AddRange(vts.Select(t => N(x.TryGetValue(t, out int z) ? z : 0))); cells.Add(N(x.Values.Sum())); return cells.ToArray(); }).ToList(),
                new List<Cell> { T("Total") }.Concat(vts.Select(t => N(by.Values.Sum(x => x.TryGetValue(t, out int z) ? z : 0)))).Append(N(total)).ToArray(), "Nothing in this range.", true);
            rep.H.Append("<div class=\"rp-two\">");
            Table(rep, "By kind of visit", "Wait = check-in to start. In the room = start to ready for checkout.", new[] { "Visit", "Visits", "Average wait", "Average in the room" },
                types.OrderByDescending(x => I(x, 1)).Select(x => new[] { T(TypeName(S(x, 0))), N(I(x, 1)), Mi(M(x, 2), I(x, 3)), Mi(M(x, 4), I(x, 5)) }).ToList(), null);
            Table(rep, "By vet or groomer", string.Empty, new[] { "Staff", "Visits", "Average wait", "Average in the room" },
                staff.Select(x => new[] { T(S(x, 1) != string.Empty ? S(x, 1) : "No one assigned"), N(I(x, 2)), Mi(M(x, 3), I(x, 4)), Mi(M(x, 5), I(x, 6)) }).ToList(), null);
            rep.H.Append("</div>");
            List<string> atypes = VisitTypes.Concat(appts.Select(a => S(a, 0)).Distinct().Where(t => !VisitTypes.Contains(t))).Where(t => appts.Any(a => S(a, 0) == t)).ToList();
            Table(rep, "Appointments", "Appointments that start in this range. Rates are out of past appointments (arrived, no-show or cancelled).", new[] { "Visit", "Upcoming", "Arrived", "No-show", "Cancelled", "No-show rate", "Cancel rate" },
                atypes.Select(t =>
                {
                    int Get(string st) => appts.Where(a => S(a, 0) == t && S(a, 1) == st).Sum(a => I(a, 2));
                    int up = Get("booked") + Get("confirmed");
                    int ar = Get("checkedin");
                    int ns = Get("noshow");
                    int ca = Get("cancelled");
                    return new[] { T(TypeName(t)), N(up), N(ar), N(ns), N(ca), Pc(ns, ar + ns + ca), Pc(ca, ar + ns + ca) };
                }).ToList(),
                new[] { T("Total"), N(Ap("booked") + Ap("confirmed")), N(arrived), N(noshow), N(cancelled), Pc(noshow, over), Pc(cancelled, over) });
            return "visits";
        }

        private static async Task<string> Clients(Rep rep, Rng r)
        {
            List<DataRow> nc = await Rows("SELECT D, COUNT(*) FROM (SELECT " + LocalDay(r, "C.CREATED_UTC") + " AS D FROM CLIENT C WHERE C.CREATED_UTC >= @fu AND C.CREATED_UTC < @tu) Z GROUP BY D", r);
            List<DataRow> np = await Rows("SELECT D, COUNT(*) FROM (SELECT " + LocalDay(r, "P.CREATED_UTC") + " AS D FROM PET P WHERE P.CREATED_UTC >= @fu AND P.CREATED_UTC < @tu) Z GROUP BY D", r);
            List<DataRow> sp = await Rows("SELECT ISNULL(X.SPECIES_KEY, Z.SK), MAX(ISNULL(X.PLURAL_NAME, Z.SK)), SUM(Z.NEWP), SUM(Z.LIVE), MAX(ISNULL(X.SPECIES_ORDER, 999)) FROM (SELECT P.SPECIES_KEY AS SK, CASE WHEN P.CREATED_UTC >= @fu AND P.CREATED_UTC < @tu THEN 1 ELSE 0 END AS NEWP, CASE WHEN P.ACTIVE = 1 AND P.DECEASED_DATE IS NULL THEN 1 ELSE 0 END AS LIVE FROM PET P) Z LEFT JOIN SPECIES X ON X.SPECIES_KEY = Z.SK GROUP BY ISNULL(X.SPECIES_KEY, Z.SK) ORDER BY MAX(ISNULL(X.SPECIES_ORDER, 999))", r);
            List<DataRow> seen = await Rows("SELECT COUNT(DISTINCT V.CLIENT_ID), COUNT(DISTINCT V.PET_ID), COUNT(*), COUNT(DISTINCT CASE WHEN C.CREATED_UTC >= @fu AND C.CREATED_UTC < @tu THEN V.CLIENT_ID END) FROM VISIT V LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID WHERE V.STATUS <> N'cancelled' AND V.CHECKIN_AT >= @f AND V.CHECKIN_AT < @t1", r);
            Dictionary<DateTime, (int C, int P)> by = new Dictionary<DateTime, (int, int)>();
            foreach (DataRow d in nc)
            {
                DateTime? day = Dt(d, 0);
                if (day != null)
                {
                    DateTime b = Bucket(r, day.Value);
                    var x = by.TryGetValue(b, out var y) ? y : (0, 0);
                    by[b] = (x.Item1 + I(d, 1), x.Item2);
                }
            }
            foreach (DataRow d in np)
            {
                DateTime? day = Dt(d, 0);
                if (day != null)
                {
                    DateTime b = Bucket(r, day.Value);
                    var x = by.TryGetValue(b, out var y) ? y : (0, 0);
                    by[b] = (x.Item1, x.Item2 + I(d, 1));
                }
            }
            int newC = by.Values.Sum(v => v.C);
            int newP = by.Values.Sum(v => v.P);
            int seenC = seen.Count > 0 ? I(seen[0], 0) : 0;
            int seenP = seen.Count > 0 ? I(seen[0], 1) : 0;
            int seenNew = seen.Count > 0 ? I(seen[0], 3) : 0;
            int live = sp.Sum(x => I(x, 3));
            Tiles(rep, new List<(string, string, string)>
            {
                ("New clients", newC.ToString("#,##0", Ui.Inv), "Added in this range"),
                ("New pets", newP.ToString("#,##0", Ui.Inv), "Added in this range"),
                ("Clients seen", seenC.ToString("#,##0", Ui.Inv), seenNew > 0 ? seenNew.ToString("#,##0", Ui.Inv) + " of them new" : "Owners with a visit in this range"),
                ("Pets seen", seenP.ToString("#,##0", Ui.Inv), "Pets with a visit in this range"),
                ("Pets on file", live.ToString("#,##0", Ui.Inv), "Active, not deceased, today")
            });
            List<DateTime> bks = Buckets(r);
            Chart(rep, "New clients by " + r.Group, bks.Select(b => { int v = by.TryGetValue(b, out var x) ? x.C : 0; return (Period(r, b), PeriodShort(r, b), (decimal)v, Ui.Plural(v, "new client", "new clients")); }).ToList(), false);
            rep.H.Append("<div class=\"rp-two\">");
            Table(rep, "New clients and pets by " + r.Group, string.Empty, new[] { "Period", "New clients", "New pets" },
                bks.Select(b => { var v = by.TryGetValue(b, out var x) ? x : (0, 0); return new[] { new Cell(Ui.Enc(Period(r, b)), Ui.Ymd(b), false), N(v.Item1), N(v.Item2) }; }).ToList(),
                new[] { T("Total"), N(newC), N(newP) }, "Nothing in this range.", true);
            Table(rep, "Pets by species", "New in this range, and all active pets on file today.", new[] { "Species", "New pets", "All active pets", "Share" },
                sp.Where(x => I(x, 2) + I(x, 3) > 0).Select(x => new[] { T(S(x, 1)), N(I(x, 2)), N(I(x, 3)), Pc(I(x, 3), live) }).ToList(),
                new[] { T("Total"), N(sp.Sum(x => I(x, 2))), N(live), Pc(live, live) });
            rep.H.Append("</div>");
            return "clients";
        }

        private static async Task<string> Owed(Rep rep, Rng r, Ctx c)
        {
            string w = " WHERE I.STATUS IN (N'billed', N'partial') AND I.BALANCE > 0";
            List<DataRow> bk = await Rows("SELECT B, COUNT(*), SUM(BALANCE), COUNT(DISTINCT CLIENT_ID) FROM (SELECT CASE WHEN I.DUE_DATE IS NULL OR I.DUE_DATE >= @td THEN 0 WHEN DATEDIFF(DAY, I.DUE_DATE, @td) <= 30 THEN 1 WHEN DATEDIFF(DAY, I.DUE_DATE, @td) <= 60 THEN 2 WHEN DATEDIFF(DAY, I.DUE_DATE, @td) <= 90 THEN 3 ELSE 4 END AS B, I.BALANCE, I.CLIENT_ID FROM INVOICE I" + w + ") Z GROUP BY B", r);
            List<DataRow> all = await Rows("SELECT COUNT(*), ISNULL(SUM(I.BALANCE), 0), COUNT(DISTINCT I.CLIENT_ID) FROM INVOICE I" + w, r);
            List<DataRow> top = await Rows("SELECT TOP 10 I.CLIENT_ID, MAX(ISNULL(C.FIRST_NAME + N' ' + C.LAST_NAME, N'')), COUNT(*), SUM(I.BALANCE), MIN(I.DUE_DATE) FROM INVOICE I LEFT JOIN CLIENT C ON C.CLIENT_ID = I.CLIENT_ID" + w + " GROUP BY I.CLIENT_ID ORDER BY SUM(I.BALANCE) DESC, I.CLIENT_ID", r);
            string[] names = { "Not yet due", "1–30 days late", "31–60 days late", "61–90 days late", "Over 90 days late" };
            string[] shorts = { "Not due", "1–30", "31–60", "61–90", "90+" };
            decimal[] amt = new decimal[5];
            int[] cnt = new int[5];
            foreach (DataRow d in bk)
            {
                int b = Math.Clamp(I(d, 0), 0, 4);
                amt[b] += M(d, 2);
                cnt[b] += I(d, 1);
            }
            decimal total = all.Count > 0 ? M(all[0], 1) : 0m;
            int invs = all.Count > 0 ? I(all[0], 0) : 0;
            int owners = all.Count > 0 ? I(all[0], 2) : 0;
            decimal late = amt[1] + amt[2] + amt[3] + amt[4];
            Tiles(rep, new List<(string, string, string)>
            {
                ("Owed in all", Ui.Money(total), "Billed and part-paid invoices"),
                ("Invoices", invs.ToString("#,##0", Ui.Inv), Ui.Plural(owners, "owner", "owners")),
                ("Late", Ui.Money(late), PctText(late, total) == "-" ? "Past the due date" : PctText(late, total) + " of what is owed"),
                ("Over 90 days late", Ui.Money(amt[4]), Ui.Plural(cnt[4], "invoice", "invoices"))
            });
            Chart(rep, "Money owed by how late it is", Enumerable.Range(0, 5).Select(i => (names[i], shorts[i], amt[i], Money(amt[i]))).ToList(), true);
            rep.H.Append("<div class=\"rp-two\">");
            Table(rep, "By age", "As of today, " + Ui.LongDay(r.Today) + ". Late means past the invoice's due date.", new[] { "Age", "Invoices", "Owed", "Share" },
                Enumerable.Range(0, 5).Select(i => new[] { T(names[i]), N(cnt[i]), Mo(amt[i]), Pc(amt[i], total) }).ToList(),
                new[] { T("Total"), N(invs), Mo(total), Pc(total, total) });
            bool link = c.Can("balances", 'V');
            Table(rep, "Top 10 owners", string.Empty, new[] { "Owner", "Invoices", "Owed", "Oldest due date" },
                top.Select(x =>
                {
                    string nm = S(x, 1) != string.Empty ? S(x, 1) : "No owner on file";
                    DateTime? due = Dt(x, 4);
                    Cell owner = link && S(x, 0) != string.Empty ? Ln("<a class=\"rp-link\" href=\"AdminBalances?c=" + Uri.EscapeDataString(S(x, 0)) + "\">" + Ui.Enc(nm) + "</a>", nm) : T(nm);
                    return new[] { owner, N(I(x, 2)), Mo(M(x, 3)), due.HasValue ? new Cell(Ui.Enc(Ui.Day(due.Value)) + (due.Value < r.Today ? " <span class=\"rp-late\">late</span>" : string.Empty), Ui.Ymd(due.Value), false) : T("No due date") };
                }).ToList(), null, "Nobody owes money.");
            rep.H.Append("</div>");
            return "owed";
        }

        private static async Task<string> Boarding(Rep rep, Rng r)
        {
            List<DataRow> types = await Rows("SELECT T.ROOMTYPE_ID, T.TYPE_NAME, T.ACTIVE, (SELECT COUNT(*) FROM ROOM R WHERE R.ROOMTYPE_ID = T.ROOMTYPE_ID AND R.ACTIVE = 1) FROM ROOMTYPE T WHERE T.PURPOSE = N'boarding' ORDER BY T.TYPE_ORDER, T.TYPE_NAME", r);
            List<DataRow> stays = await Rows("SELECT TID, SUM(CASE WHEN O > I THEN DATEDIFF(DAY, I, O) ELSE 0 END), SUM(CASE WHEN O > I THEN 1 ELSE 0 END), SUM(CASE WHEN DIN >= @fd AND DIN < @t1d THEN 1 ELSE 0 END), SUM(CASE WHEN DIN >= @fd AND DIN < @t1d AND DOUT > DIN THEN DATEDIFF(DAY, DIN, DOUT) ELSE 0 END) FROM (SELECT TID, DIN, DOUT, CASE WHEN DIN > @fd THEN DIN ELSE @fd END AS I, CASE WHEN DOUT < @t1d THEN DOUT ELSE @t1d END AS O FROM (SELECT A.ROOMTYPE_ID AS TID, CAST(A.START_AT AS DATE) AS DIN, CASE WHEN V.CLOSED_AT IS NOT NULL THEN CAST(V.CLOSED_AT AS DATE) ELSE CAST(A.END_AT AS DATE) END AS DOUT FROM APPOINTMENT A LEFT JOIN VISIT V ON V.VISIT_ID = A.VISIT_ID AND A.VISIT_ID <> N'' WHERE A.VISIT_TYPE = N'boarding' AND A.STATUS IN (N'booked', N'confirmed', N'checkedin') AND A.START_AT < @t1 AND A.END_AT >= @f) X) Y GROUP BY TID", r);
            int days = r.Days;
            List<(string Name, int Rooms, int Nights, int Stays, int Starts, int Len, bool Active)> list = new List<(string, int, int, int, int, int, bool)>();
            foreach (DataRow t in types)
            {
                DataRow? s = stays.FirstOrDefault(x => S(x, 0) == S(t, 0));
                list.Add((S(t, 1), I(t, 3), s == null ? 0 : I(s, 1), s == null ? 0 : I(s, 2), s == null ? 0 : I(s, 3), s == null ? 0 : I(s, 4), Convert.ToBoolean(t[2], CultureInfo.InvariantCulture)));
            }
            foreach (DataRow s in stays.Where(x => types.All(t => S(t, 0) != S(x, 0))))
            {
                list.Add(("Other or removed room type", 0, I(s, 1), I(s, 2), I(s, 3), I(s, 4), false));
            }
            list = list.Where(x => x.Active || x.Nights > 0 || x.Stays > 0).ToList();
            int nights = list.Sum(x => x.Nights);
            int avail = list.Sum(x => x.Rooms) * days;
            int nstays = list.Sum(x => x.Stays);
            int starts = list.Sum(x => x.Starts);
            int len = list.Sum(x => x.Len);
            Tiles(rep, new List<(string, string, string)>
            {
                ("Nights booked or stayed", nights.ToString("#,##0", Ui.Inv), "Past stays and bookings to come"),
                ("Occupancy", PctText(nights, avail) == "-" ? "&ndash;" : PctText(nights, avail), avail.ToString("#,##0", Ui.Inv) + " run-nights available"),
                ("Stays", nstays.ToString("#,##0", Ui.Inv), "With at least one night in this range"),
                ("Average stay", starts == 0 ? "&ndash;" : ((decimal)len / starts).ToString("0.0", Ui.Inv) + " nights", Ui.Plural(starts, "stay starts", "stays start") + " in this range")
            });
            Chart(rep, "Nights by room type", list.Select(x => (x.Name, x.Name.Length > 14 ? x.Name.Substring(0, 13) + "…" : x.Name, (decimal)x.Nights, Ui.Plural(x.Nights, "night", "nights"))).ToList(), false);
            Table(rep, "Occupancy by room type", "Nights available = active runs × " + Ui.Plural(days, "day", "days") + ". Cancelled stays and no-shows are left out; stays that ended early count until the day they went home.", new[] { "Room type", "Runs", "Nights available", "Nights booked or stayed", "Occupancy", "Stays", "Average stay" },
                list.Select(x => new[] { T(x.Name + (x.Active ? string.Empty : " (inactive)")), N(x.Rooms), N(x.Rooms * days), N(x.Nights), Pc(x.Nights, x.Rooms * days), N(x.Stays), x.Starts == 0 ? new Cell("&ndash;", string.Empty, true) : new Cell(((decimal)x.Len / x.Starts).ToString("0.0", Ui.Inv) + " nights", ((decimal)x.Len / x.Starts).ToString("0.0", Ui.Inv), true) }).ToList(),
                new[] { T("Total"), N(list.Sum(x => x.Rooms)), N(avail), N(nights), Pc(nights, avail), N(nstays), starts == 0 ? new Cell("&ndash;", string.Empty, true) : new Cell(((decimal)len / starts).ToString("0.0", Ui.Inv) + " nights", ((decimal)len / starts).ToString("0.0", Ui.Inv), true) }, "No boarding run types.");
            return "boarding";
        }

        private static async Task<string> Build(Ctx c, Dictionary<string, string> s, string kind, Rng r)
        {
            Rep rep = new Rep();
            string clock = App.Now(s).ToString("MMM d, yyyy h:mm tt", Ui.Us);
            string what = kind == "owed" ? "As of today, " + Ui.LongDay(r.Today) : (r.From == r.To ? Ui.LongDay(r.From) : Ui.Day(r.From, r.From.Year != r.To.Year) + " – " + Ui.Day(r.To)) + (r.Days > 1 ? " · by " + r.Group : string.Empty);
            rep.Csv.Add(Csv(new[] { App.S(s, "name", "Animal Hospital") + " · " + KindName(kind) + " report" }));
            rep.Csv.Add(Csv(new[] { kind == "owed" ? "As of " + Ui.Ymd(r.Today) : "From " + Ui.Ymd(r.From) + " to " + Ui.Ymd(r.To), "Made " + clock }));
            rep.Csv.Add(string.Empty);
            try
            {
                switch (kind)
                {
                    case "payments":
                        await Payments(rep, r);
                        break;
                    case "visits":
                        await Visits(rep, r);
                        break;
                    case "clients":
                        await Clients(rep, r);
                        break;
                    case "owed":
                        await Owed(rep, r, c);
                        break;
                    case "boarding":
                        await Boarding(rep, r);
                        break;
                    default:
                        await Sales(rep, r);
                        break;
                }
            }
            catch (InvalidOperationException)
            {
                return "<div class=\"rp-tw\"><p class=\"rp-empty\">The report could not be made. Try again.</p></div>";
            }
            string file = "report-" + kind + "-" + (kind == "owed" ? Ui.Ymd(r.Today) : Ui.Ymd(r.From) + "-to-" + Ui.Ymd(r.To)) + ".csv";
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rp-rhead\"><h2 class=\"rp-rtitle\">" + Ui.Enc(KindName(kind)) + "</h2><p>" + Ui.Enc(what) + "</p>" + (r.Note != string.Empty && kind != "owed" && kind != "boarding" ? "<p class=\"rp-rnote\">" + Ui.Enc(r.Note) + "</p>" : string.Empty) + "</div>");
            sb.Append(rep.H);
            sb.Append("<div id=\"rp-csv\" hidden data-name=\"" + Ui.Attr(file) + "\">" + string.Concat(rep.Csv.Select(l => "<i data-csv=\"" + Ui.Attr(l) + "\"></i>")) + "</div>");
            return sb.ToString();
        }

        private static string Controls(string kind, Rng r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"rp-ptop\"><div><h1>Reports</h1><p>Sales, payments, visits, clients, money owed and boarding for any dates. Every report can be downloaded as a spreadsheet (CSV).</p></div><div class=\"rp-acts\"><button type=\"button\" class=\"rp-btn rp-btn-ghost\" id=\"rp-csvb\" onclick=\"" + Me + ".csv()\">Download CSV</button></div></div>");
            sb.Append("<div class=\"rp-kinds\" role=\"group\" aria-label=\"Report\">");
            foreach (string k in Kinds)
            {
                sb.Append("<button type=\"button\" class=\"rp-kind" + (k == kind ? " rp-kon" : string.Empty) + "\" id=\"rp-k-" + k + "\" aria-pressed=\"" + (k == kind ? "true" : "false") + "\" onclick=\"" + Me + ".pick('" + k + "')\">" + Ui.Enc(KindName(k)) + "</button>");
            }
            sb.Append("</div><div class=\"rp-bar\" id=\"rp-bar\">");
            sb.Append("<label class=\"rp-field rp-fdates\"><span>Dates</span><select id=\"rp-preset\" onchange=\"" + Me + ".preset()\">" + Ui.Opts(Presets.Select(p => (p, PresetName(p))), r.Preset) + "</select></label>");
            sb.Append("<label class=\"rp-field rp-fdates rp-dfield\"><span>From</span><input type=\"date\" id=\"rp-from\" value=\"" + Ui.Ymd(r.From) + "\" onchange=\"" + Me + ".custom()\"></label>");
            sb.Append("<label class=\"rp-field rp-fdates rp-dfield\"><span>To</span><input type=\"date\" id=\"rp-to\" value=\"" + Ui.Ymd(r.To) + "\" onchange=\"" + Me + ".custom()\"></label>");
            sb.Append("<label class=\"rp-field rp-fgroup\"><span>Group by</span><select id=\"rp-group\" onchange=\"" + Me + ".run()\">" + Ui.Opts(Groups.Select(g => (g, g == "day" ? "Day" : g == "week" ? "Week" : "Month")), r.Group) + "</select></label>");
            sb.Append("<p class=\"rp-owedn\">Money owed is always as of today.</p></div><em class=\"rp-err\" id=\"rp-e-range\"></em>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("rp-menu", Auth.Menu("rp", Me, "AdminReports", c));
            response.SetElementContents("rp-who", Auth.Who("rp", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rp-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            var (r, _) = Range(s, "month", string.Empty, string.Empty, string.Empty);
            string html = Controls("sales", r!) + "<div id=\"rp-out\" aria-live=\"polite\">" + await Build(c, s, "sales", r!) + "</div>";
            response.SetElementContents("rp-main", html);
            response.ExecuteScript(Me + ".ready('sales', '" + Ui.Ymd(r!.From) + "', '" + Ui.Ymd(r.To) + "', '" + r.Group + "');");
            return response;
        }

        public async Task<ApiResponse> Run()
        {
            ApiResponse response = new ApiResponse();
            response.ExecuteScript(Me + ".unlock();");
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("rp-main", Denied());
                return response;
            }
            Dictionary<string, string> s = await App.Settings();
            string kind = Ui.Clip(GetDataValue("rkind"), 20);
            if (!Kinds.Contains(kind))
            {
                kind = "sales";
            }
            var (r, err) = Range(s, Ui.Clip(GetDataValue("rpreset"), 20), Ui.Clip(GetDataValue("rfrom"), 10), Ui.Clip(GetDataValue("rto"), 10), Ui.Clip(GetDataValue("rgroup"), 10));
            if (r == null)
            {
                response.SetElementContents("rp-e-range", Ui.Enc(err));
                response.ExecuteScript(Me + ".failed();");
                return response;
            }
            response.SetElementContents("rp-e-range", string.Empty);
            response.SetElementContents("rp-out", await Build(c, s, kind, r));
            response.ExecuteScript(Me + ".ready('" + kind + "', '" + Ui.Ymd(r.From) + "', '" + Ui.Ymd(r.To) + "', '" + r.Group + "');");
            return response;
        }

        public async Task<ApiResponse> CsvLog()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null || !c.Can(Key, 'V'))
            {
                return response;
            }
            await App.Log(c.U.StaffId, "download report", Ui.OneLine(GetDataValue("rfile"), 120), true);
            return response;
        }
    }
}
