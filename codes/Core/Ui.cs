using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AnimalHospital.Core
{
    public static class Ui
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
        public static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        public static string Enc(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("&", "\\x26").Replace("'", "\\'").Replace("\"", "\\x22").Replace("<", "\\x3c").Replace(">", "\\x3e").Replace("\n", "\\n").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string OneLine(string? value, int max)
        {
            return Regex.Replace(Clip(value, max), "\\s+", " ").Trim();
        }

        public static string Money(decimal v)
        {
            return (v < 0 ? "&minus;$" : "$") + Math.Abs(v).ToString("#,##0.00", Inv);
        }

        public static string Num(decimal v)
        {
            return v.ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.##", Inv);
        }

        public static string Plural(int n, string one, string many)
        {
            return n.ToString("#,##0", Inv) + " " + (n == 1 ? one : many);
        }

        public static string Day(DateTime d, bool year = true)
        {
            return d.ToString(year ? "ddd MMM d, yyyy" : "ddd MMM d", Us);
        }

        public static string LongDay(DateTime d)
        {
            return d.ToString("dddd, MMMM d, yyyy", Us);
        }

        public static string Hm(DateTime d, bool clock24 = false)
        {
            return d.ToString(clock24 ? "HH:mm" : "h:mm tt", Us);
        }

        public static string Ymd(DateTime d)
        {
            return d.ToString("yyyy-MM-dd", Inv);
        }

        public static DateTime? ParseDay(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), "yyyy-MM-dd", Inv, DateTimeStyles.None, out DateTime d) ? d.Date : null;
        }

        public static DateTime? ParseStamp(string? v)
        {
            return DateTime.TryParseExact((v ?? string.Empty).Trim(), new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm" }, Inv, DateTimeStyles.None, out DateTime d) ? d : null;
        }

        public static TimeSpan? ParseHm(string? v)
        {
            return TimeSpan.TryParseExact((v ?? string.Empty).Trim(), "hh\\:mm", Inv, out TimeSpan t) && t >= TimeSpan.Zero && t <= TimeSpan.FromHours(24) ? t : null;
        }

        public static string Iso(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Inv);
        }

        public static string Time(DateTime utc, string mode = "ago")
        {
            return "<time datetime=\"" + Iso(utc) + "\" data-t=\"" + mode + "\">" + DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("MMM d, yyyy HH:mm 'UTC'", Us) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = Regex.Replace(name ?? string.Empty, "^(Dr\\.?|Mr\\.?|Ms\\.?|Mrs\\.?)\\s+", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(Inv);
        }

        public static string Avatar(string p, string id, string name, string photo, string size = "")
        {
            string cls = p + "-av" + (size != string.Empty ? " " + p + "-av-" + size : string.Empty);
            if (SafePath(photo) && File.Exists(Path.Combine(App.Root, photo.Replace('/', Path.DirectorySeparatorChar))))
            {
                return "<img class=\"" + cls + " " + p + "-avp\" src=\"" + Attr(photo) + "\" alt=\"\" loading=\"lazy\">";
            }
            return "<span class=\"" + cls + "\" style=\"--h:" + Hue(id) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }

        public static bool SafePath(string? src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png|mp4|pdf)$") && !src!.Contains("..");
        }

        public static bool SafeColor(string? v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public static string Opts(IEnumerable<(string Value, string Text)> items, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string v, string t) in items)
            {
                sb.Append("<option value=\"" + Attr(v) + "\"" + (v == cur ? " selected" : string.Empty) + ">" + Enc(t) + "</option>");
            }
            return sb.ToString();
        }

        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + Plural(total, one, many) + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string? value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static string CleanPhone(string? v)
        {
            return Regex.Replace(Clip(v, 30), "[^0-9+()\\-. ]", string.Empty).Trim();
        }

        public static string Digits(string? v)
        {
            return Regex.Replace(v ?? string.Empty, "[^0-9]", string.Empty);
        }

        public static bool Email(string v)
        {
            return v.Length <= 120 && Regex.IsMatch(v, "^[A-Za-z0-9._%+'-]+@[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)*\\.[A-Za-z]{2,}$");
        }

        public static string Csv(string? v)
        {
            string t = (v ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            if (Regex.IsMatch(t, "^[=+\\-@\t]") && !Regex.IsMatch(t, "^-?[0-9]+(\\.[0-9]+)?$"))
            {
                t = "'" + t;
            }
            return t.Contains(',') || t.Contains('"') || t.Contains('\'') ? "\"" + t.Replace("\"", "\"\"") + "\"" : t;
        }
    }
}
