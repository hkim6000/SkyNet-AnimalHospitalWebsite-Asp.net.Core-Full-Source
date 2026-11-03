using System.Data;
using System.Globalization;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public sealed class Opening
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string StaffId { get; set; } = string.Empty;
        public DateTime? ProcAt { get; set; }
        public int ProcMinutes { get; set; }
        public DateTime? PickupAt { get; set; }
    }

    public sealed class Window
    {
        public string StaffId { get; set; } = string.Empty;
        public TimeSpan From { get; set; }
        public TimeSpan To { get; set; }
    }

    public static class Slots
    {
        public const string BookCols = "B.BOOKTYPE_ID AS BooktypeId, B.BOOK_NAME AS BookName, B.TEAM_KEY AS TeamKey, B.VISIT_TYPE AS VisitType, B.MINUTES AS Minutes, B.MINUTES_M AS MinutesM, B.MINUTES_L AS MinutesL, B.ITEM_ID AS ItemId, B.ROOMTYPE_ID AS RoomtypeId, B.SPECIES AS Species, B.DROP_FROM AS DropFrom, B.DROP_TO AS DropTo, B.PICKUP_HM AS PickupHm, B.ONLINE AS Online, B.NEW_OK AS NewOk, B.ACTIVE AS Active";

        public static int Length(BookRow b, string size)
        {
            if (size == "medium" && b.MinutesM > 0)
            {
                return b.MinutesM;
            }
            if (size == "large" && b.MinutesL > 0)
            {
                return b.MinutesL;
            }
            return Math.Max(5, b.Minutes);
        }

        public static bool Dropoff(BookRow b)
        {
            return b.VisitType == "surgery" && Ui.ParseHm(b.DropFrom) != null && Ui.ParseHm(b.PickupHm) != null;
        }

        public static bool ForSpecies(string list, string species)
        {
            return string.IsNullOrWhiteSpace(list) || list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(species);
        }

        public static (bool Open, TimeSpan From, TimeSpan To) Hours(Dictionary<string, string> s, DateTime day)
        {
            int dn = (int)day.DayOfWeek;
            foreach (string part in App.S(s, "hours").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split('|');
                if (f.Length >= 4 && int.TryParse(f[0], NumberStyles.Integer, Ui.Inv, out int d) && d == dn)
                {
                    TimeSpan? a = Ui.ParseHm(f[2]);
                    TimeSpan? z = Ui.ParseHm(f[3]);
                    return (f[1] == "1" && a != null && z != null && z > a, a ?? TimeSpan.Zero, z ?? TimeSpan.Zero);
                }
            }
            return (false, TimeSpan.Zero, TimeSpan.Zero);
        }

        public static async Task<string> Holiday(DateTime day)
        {
            var (dt, emsg) = await Helper.GetDataTable("SELECT HOLIDAY_NAME FROM HOLIDAY WHERE HOLIDAY_DATE = @d", new List<SqlParameter> { new SqlParameter("d", SqlDbType.Date) { Value = day.Date } });
            return emsg == string.Empty && dt.Rows.Count > 0 ? Convert.ToString(dt.Rows[0][0]) ?? string.Empty : string.Empty;
        }

        public static async Task<List<Window>> Windows(Dictionary<string, string> s, DateTime day, IEnumerable<string> staffIds)
        {
            List<Window> list = new List<Window>();
            List<string> ids = staffIds.Distinct().ToList();
            var (open, hf, ht) = Hours(s, day);
            if (!open || ids.Count == 0 || await Holiday(day) != string.Empty)
            {
                return list;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.PInt("dn", (int)day.DayOfWeek), new SqlParameter("d", SqlDbType.Date) { Value = day.Date } };
            string inList = InList(ids, "s", ps);
            var (rows, emsg) = await Helper.GetListT<HourRow>("SELECT H.STAFF_ID AS StaffId, H.IS_ON AS IsOn, H.FROM_HM AS FromHm, H.TO_HM AS ToHm, H.BREAK_FROM AS BreakFrom, H.BREAK_TO AS BreakTo FROM STAFFHOURS H JOIN STAFF S ON S.STAFF_ID = H.STAFF_ID WHERE H.DAY_NO = @dn AND S.ACTIVE = 1 AND H.STAFF_ID IN (" + inList + ") AND NOT EXISTS (SELECT 1 FROM TIMEOFF T WHERE T.STAFF_ID = H.STAFF_ID AND T.FROM_DATE <= @d AND T.TO_DATE >= @d)", ps);
            if (emsg != string.Empty || rows == null)
            {
                return list;
            }
            foreach (HourRow h in rows.Where(x => x.IsOn))
            {
                TimeSpan? f = Ui.ParseHm(h.FromHm);
                TimeSpan? t = Ui.ParseHm(h.ToHm);
                if (f == null || t == null)
                {
                    continue;
                }
                TimeSpan a = f.Value > hf ? f.Value : hf;
                TimeSpan z = t.Value < ht ? t.Value : ht;
                if (z <= a)
                {
                    continue;
                }
                TimeSpan? bf = Ui.ParseHm(h.BreakFrom);
                TimeSpan? bt = Ui.ParseHm(h.BreakTo);
                if (bf != null && bt != null && bt > bf && bf < z && bt > a)
                {
                    if (bf.Value > a)
                    {
                        list.Add(new Window { StaffId = h.StaffId, From = a, To = bf.Value });
                    }
                    if (bt.Value < z)
                    {
                        list.Add(new Window { StaffId = h.StaffId, From = bt.Value, To = z });
                    }
                }
                else
                {
                    list.Add(new Window { StaffId = h.StaffId, From = a, To = z });
                }
            }
            return list;
        }

        public static string InList(List<string> ids, string prefix, List<SqlParameter> ps)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                names.Add("@" + prefix + i);
                ps.Add(App.P(prefix + i, ids[i], 50));
            }
            return names.Count == 0 ? "N''" : string.Join(", ", names);
        }

        public static async Task<List<Opening>> Find(Dictionary<string, string> s, BookRow b, string staffId, DateTime day, string size, string ignoreAppt, DateTime notBefore)
        {
            List<Opening> result = new List<Opening>();
            List<SqlParameter> sp = new List<SqlParameter> { App.P("bt", b.BooktypeId, 50) };
            var (sdt, serr) = await Helper.GetDataTable("SELECT X.STAFF_ID FROM BOOKSTAFF X JOIN STAFF S ON S.STAFF_ID = X.STAFF_ID WHERE X.BOOKTYPE_ID = @bt AND S.ACTIVE = 1", sp);
            if (serr != string.Empty)
            {
                return result;
            }
            List<string> staff = sdt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]) ?? string.Empty).Where(x => staffId == string.Empty || x == staffId).ToList();
            List<Window> wins = await Windows(s, day, staff);
            if (wins.Count == 0)
            {
                return result;
            }
            DateTime d0 = day.Date;
            List<SqlParameter> ps = new List<SqlParameter> { App.PDate("d0", d0), App.PDate("d1", d0.AddDays(1)), App.P("ign", ignoreAppt, 50) };
            var (busy, berr) = await Helper.GetListT<BusyRow>("SELECT A.STAFF_ID AS StaffId, A.ROOMTYPE_ID AS RoomtypeId, ISNULL(A.PROC_AT, A.START_AT) AS FromAt, CASE WHEN A.PROC_AT IS NULL THEN A.END_AT ELSE DATEADD(MINUTE, A.PROC_MINUTES, A.PROC_AT) END AS ToAt FROM APPOINTMENT A WHERE A.START_AT >= @d0 AND A.START_AT < @d1 AND A.STATUS NOT IN (N'cancelled', N'noshow') AND A.VISIT_TYPE <> N'boarding' AND A.APPT_ID <> @ign", ps);
            var (cdt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM ROOM WHERE ROOMTYPE_ID = @rt AND ACTIVE = 1", new List<SqlParameter> { App.P("rt", b.RoomtypeId, 50) });
            if (berr != string.Empty || busy == null || cerr != string.Empty)
            {
                return result;
            }
            int rooms = b.RoomtypeId == string.Empty ? int.MaxValue : Convert.ToInt32(cdt.Rows[0][0]);
            int step = Math.Clamp(App.SInt(s, "slot.minutes", 15), 5, 60);
            int len = Length(b, size);
            bool drop = Dropoff(b);
            TimeSpan dropFrom = Ui.ParseHm(b.DropFrom) ?? TimeSpan.Zero;
            TimeSpan dropTo = Ui.ParseHm(b.DropTo) ?? dropFrom;
            TimeSpan pick = Ui.ParseHm(b.PickupHm) ?? TimeSpan.Zero;
            foreach (Window w in wins)
            {
                TimeSpan first = w.From;
                TimeSpan last = w.To;
                if (drop)
                {
                    first = first > dropTo ? first : dropTo;
                    TimeSpan cap = pick - TimeSpan.FromMinutes(60);
                    last = last < cap ? last : cap;
                }
                int m = (int)Math.Ceiling(first.TotalMinutes / step) * step;
                for (; m + len <= (int)last.TotalMinutes; m += step)
                {
                    DateTime a = d0.AddMinutes(m);
                    DateTime z = a.AddMinutes(len);
                    if ((drop ? d0.Add(dropFrom) : a) < notBefore)
                    {
                        continue;
                    }
                    if (busy.Any(x => x.StaffId == w.StaffId && x.FromAt < z && x.ToAt > a))
                    {
                        continue;
                    }
                    if (rooms != int.MaxValue && busy.Count(x => x.RoomtypeId == b.RoomtypeId && x.FromAt < z && x.ToAt > a) >= rooms)
                    {
                        continue;
                    }
                    if (drop)
                    {
                        DateTime pk = d0.Add(pick);
                        result.Add(new Opening { Start = d0.Add(dropFrom), End = d0.Add(dropFrom).AddMinutes(15), StaffId = w.StaffId, ProcAt = a, ProcMinutes = len, PickupAt = z.AddMinutes(60) > pk ? z.AddMinutes(60) : pk });
                    }
                    else
                    {
                        result.Add(new Opening { Start = a, End = z, StaffId = w.StaffId });
                    }
                }
            }
            return result.OrderBy(x => x.ProcAt ?? x.Start).ThenBy(x => x.StaffId).ToList();
        }
    }
}
