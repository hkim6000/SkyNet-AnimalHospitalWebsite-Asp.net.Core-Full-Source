using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using AnimalHospital.codes;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.Core
{
    public static class App
    {
        private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new ConcurrentDictionary<string, TimeZoneInfo>();
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Root => Directory.GetCurrentDirectory();

        public static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        public static bool Demo => Cfg("app.app.runmode") != "1";

        public static string VaultDir
        {
            get
            {
                string name = Cfg("user.folders.vault");
                string dir = Path.Combine(Root, name == string.Empty ? "vault" : name);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static string Ip()
        {
            HttpContext? h = HttpCurrent.Context;
            if (h == null)
            {
                return string.Empty;
            }
            string fwd = Cfg("user.trust.proxy") == "1" ? h.Request.Headers["X-Forwarded-For"].ToString() : string.Empty;
            string ip = fwd.Length > 0 ? fwd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? string.Empty : h.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            return ip.Length > 60 ? ip.Substring(0, 60) : ip;
        }

        public static SqlParameter P(string name, string? value, int size = 200)
        {
            string v = value ?? string.Empty;
            return new SqlParameter(name, SqlDbType.NVarChar) { Size = size < 0 ? -1 : Math.Max(size, 1), Value = v };
        }

        public static SqlParameter PInt(string name, int value)
        {
            return new SqlParameter(name, SqlDbType.Int) { Value = value };
        }

        public static SqlParameter PBit(string name, bool value)
        {
            return new SqlParameter(name, SqlDbType.Bit) { Value = value };
        }

        public static SqlParameter PDec(string name, decimal value)
        {
            return new SqlParameter(name, SqlDbType.Decimal) { Precision = 12, Scale = 2, Value = value };
        }

        public static SqlParameter PDate(string name, DateTime? value)
        {
            return new SqlParameter(name, SqlDbType.DateTime2) { Value = value.HasValue ? value.Value : DBNull.Value };
        }

        public static async Task<Dictionary<string, string>> Settings()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var (dt, emsg) = await Helper.GetDataTable("SELECT SKEY, SVALUE FROM SETTING");
            if (emsg != string.Empty)
            {
                return d;
            }
            foreach (DataRow r in dt.Rows)
            {
                d[Convert.ToString(r["SKEY"]) ?? string.Empty] = Convert.ToString(r["SVALUE"]) ?? string.Empty;
            }
            return d;
        }

        public static string S(Dictionary<string, string> s, string key, string fallback = "")
        {
            return s.TryGetValue(key, out string? v) && v != null ? v : fallback;
        }

        public static int SInt(Dictionary<string, string> s, string key, int fallback)
        {
            return int.TryParse(S(s, key), NumberStyles.Integer, Inv, out int n) ? n : fallback;
        }

        public static TimeZoneInfo Zone(string id)
        {
            return Zones.GetOrAdd(id ?? string.Empty, k =>
            {
                try
                {
                    return k == string.Empty ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(k);
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Local;
                }
                catch (InvalidTimeZoneException)
                {
                    return TimeZoneInfo.Local;
                }
            });
        }

        public static DateTime Now(Dictionary<string, string> s)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone(S(s, "timezone"))), DateTimeKind.Unspecified);
        }

        public static DateTime Local(Dictionary<string, string> s, DateTime utc)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(S(s, "timezone"))), DateTimeKind.Unspecified);
        }

        private static string demoDay = string.Empty;
        private static string demoZone = string.Empty;
        private static readonly SemaphoreSlim DemoLock = new SemaphoreSlim(1, 1);

        public static async Task<string> DemoLoad(Dictionary<string, string> s)
        {
            string result = await Helper.PutData(new List<string> { "EXEC XSP_DEMO_LOAD @local" }, new List<SqlParameter> { PDate("local", Now(s)) });
            if (!Helper.IsError(result))
            {
                demoDay = Now(s).ToString("yyyy-MM-dd", Inv);
                Site.Forget();
            }
            return result;
        }

        public static async Task DemoDaily()
        {
            if (!Demo)
            {
                return;
            }
            if (demoDay != string.Empty && demoDay == DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone(demoZone)), DateTimeKind.Unspecified).ToString("yyyy-MM-dd", Inv))
            {
                return;
            }
            await DemoLock.WaitAsync();
            try
            {
                Dictionary<string, string> s = await Settings();
                if (s.Count == 0)
                {
                    return;
                }
                demoZone = S(s, "timezone");
                string today = Now(s).ToString("yyyy-MM-dd", Inv);
                if (demoDay == today)
                {
                    return;
                }
                if (S(s, "demo.day") != today)
                {
                    await DemoLoad(s);
                }
                demoDay = today;
            }
            finally
            {
                DemoLock.Release();
            }
        }

        public static async Task Log(string staffId, string action, string target, bool ok)
        {
            await Helper.PutData(new List<string> { "INSERT INTO ACTIVITY (ACT_UTC, STAFF_ID, ACTION, TARGET, OK, IP) VALUES (SYSUTCDATETIME(), @sid, @act, @tgt, @ok, @ip)" }, new List<SqlParameter>
            {
                P("sid", staffId, 50),
                P("act", Ui.Clip(action, 60), 60),
                P("tgt", Ui.Clip(target, 200), 200),
                PBit("ok", ok),
                P("ip", Ip(), 60)
            });
        }
    }
}
