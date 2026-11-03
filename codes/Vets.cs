using System.Data;
using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Vets : WebPage
    {
        private const string P = "pv";

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string team = Ui.Clip(QueryValue("s"), 40);
            string animal = Ui.Clip(QueryValue("a"), 20);
            string t = site.Specialties.Any(x => x.Active && x.Key == team) ? team : string.Empty;
            string a = site.Settings.Species.Contains(animal) ? animal : string.Empty;
            string word = site.Settings.ProvidersWord != string.Empty ? site.Settings.ProvidersWord : "Vets";
            Dictionary<string, string> parts = new Dictionary<string, string>();
            foreach (Block b in site.BlocksOf("vets").Where(x => !x.Hidden && x.Type == "data" && x.Source == "vetlist"))
            {
                if (!parts.ContainsKey(b.Id))
                {
                    parts[b.Id] = await VetList(site, b, t, a);
                }
            }
            string main = Blocks.RenderPage(P, site, "vets", "Our " + word.ToLowerInvariant(), b => parts.TryGetValue(b.Id, out string? h) ? h : string.Empty);
            Head(site, word + " | " + site.Settings.Name, "Meet the vets, technician and groomers at " + site.Settings.Name + (site.Settings.Town != string.Empty ? " in " + site.Settings.Town : string.Empty) + ", and the animals each one sees.", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "VetsJs", "Vets", site).Replace("{plhd_main}", main);
        }

        private void Head(SiteInfo site, string title, string desc, string image)
        {
            HtmlDoc.SetTitle(Ui.Enc(title));
            bool hasDesc = false;
            foreach (HtmlElement e in HtmlDoc.HtmlElements ?? new List<HtmlElement>())
            {
                HtmlTag? t = e.Element;
                if (t == null)
                {
                    continue;
                }
                if (t.Tag == "html")
                {
                    t.SetAttribute("lang", "en");
                }
                else if (t.Tag == "meta" && t.GetAttribute("name") == "description")
                {
                    t.SetAttribute("content", Ui.Attr(desc));
                    hasDesc = true;
                }
            }
            if (!hasDesc)
            {
                HtmlDoc.AddMetaElement("description", Ui.Attr(desc));
            }
            HttpContext? h = HttpCurrent.Context;
            string root = h != null ? h.Request.Scheme + "://" + h.Request.Host + h.Request.PathBase + "/" : string.Empty;
            List<(string, string)> og = new List<(string, string)> { ("og:type", "website"), ("og:site_name", site.Settings.Name), ("og:title", title), ("og:description", desc), ("og:locale", "en_US") };
            if (image != string.Empty)
            {
                og.Add(("og:image", root + image));
            }
            foreach ((string k, string v) in og)
            {
                HtmlDoc.AddMetaTag(new List<NameValue> { new NameValue { name = "property", value = k }, new NameValue { name = "content", value = Ui.Attr(v) } });
            }
            HtmlDoc.AddMetaElement("twitter:card", image != string.Empty ? "summary_large_image" : "summary");
        }

        private static string Pic(SiteInfo site)
        {
            Block? b = site.BlocksOf("vets").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            if (b != null && Blocks.SafeImage(b.Media))
            {
                return b.Media;
            }
            Clinician? p = site.Providers.FirstOrDefault(x => Blocks.SafeImage(x.Photo));
            return p != null ? p.Photo : string.Empty;
        }

        private static string NextText(SiteInfo site, DateTime? n)
        {
            if (n == null)
            {
                return "No online times in the next 2 weeks";
            }
            DateTime today = Site.Today(site);
            DateTime d = n.Value.Date;
            string day = d == today ? "Today" : d == today.AddDays(1) ? "Tomorrow" : Ui.Day(d, false);
            return day + " at " + Site.Hm(site, n.Value);
        }

        private static string RoleName(SiteInfo site, string role)
        {
            return role switch { "tech" => "Technician", "groomer" => "Groomer", _ => site.Settings.ProviderWord != string.Empty ? site.Settings.ProviderWord : "Vet" };
        }

        private static int RoleOrder(string role)
        {
            return role switch { "vet" => 0, "tech" => 1, "groomer" => 2, _ => 3 };
        }

        private static List<string> TeamsOf(SiteInfo site, Clinician p)
        {
            List<string> t = new List<string>();
            if (p.Specialty != string.Empty)
            {
                t.Add(p.Specialty);
            }
            foreach (string k in p.Teams.Concat(site.Services.Where(s => s.Active && s.Providers.Contains(p.Id)).Select(s => s.Specialty)))
            {
                if (!t.Contains(k))
                {
                    t.Add(k);
                }
            }
            return t;
        }

        private sealed class Free
        {
            public List<BusyRow> Busy { get; set; } = new List<BusyRow>();
            public Dictionary<string, int> Rooms { get; set; } = new Dictionary<string, int>();
        }

        private static async Task<Free> LoadFree(SiteInfo site, int days)
        {
            Free f = new Free();
            DateTime d0 = Site.Today(site);
            var (busy, berr) = await Helper.GetListT<BusyRow>("SELECT A.STAFF_ID AS StaffId, A.ROOMTYPE_ID AS RoomtypeId, ISNULL(A.PROC_AT, A.START_AT) AS FromAt, CASE WHEN A.PROC_AT IS NULL THEN A.END_AT ELSE DATEADD(MINUTE, A.PROC_MINUTES, A.PROC_AT) END AS ToAt FROM APPOINTMENT A WHERE A.START_AT >= @d0 AND A.START_AT < @d1 AND A.STATUS NOT IN (N'cancelled', N'noshow') AND A.VISIT_TYPE <> N'boarding'", new List<SqlParameter> { App.PDate("d0", d0), App.PDate("d1", d0.AddDays(days + 1)) });
            if (berr == string.Empty && busy != null)
            {
                f.Busy = busy;
            }
            var (dt, rerr) = await Helper.GetDataTable("SELECT ROOMTYPE_ID, COUNT(*) AS N FROM ROOM WHERE ACTIVE = 1 GROUP BY ROOMTYPE_ID");
            if (rerr == string.Empty && dt != null)
            {
                foreach (DataRow r in dt.Rows)
                {
                    f.Rooms[Convert.ToString(r[0]) ?? string.Empty] = Convert.ToInt32(r[1]);
                }
            }
            return f;
        }

        private static List<(TimeSpan From, TimeSpan To)> Windows(SiteInfo site, Clinician p, DateTime day)
        {
            List<(TimeSpan, TimeSpan)> list = new List<(TimeSpan, TimeSpan)>();
            (TimeSpan From, TimeSpan To)? c = Site.Clinic(site, day);
            WorkDay? w = p.Hours.FirstOrDefault(h => h.Day == (int)day.DayOfWeek);
            if (c == null || w == null || !w.On || p.TimeOff.Any(t => t.From.Date <= day.Date && t.To.Date >= day.Date))
            {
                return list;
            }
            TimeSpan? f = Ui.ParseHm(w.From);
            TimeSpan? t2 = Ui.ParseHm(w.To);
            if (f == null || t2 == null)
            {
                return list;
            }
            TimeSpan a = f.Value > c.Value.From ? f.Value : c.Value.From;
            TimeSpan z = t2.Value < c.Value.To ? t2.Value : c.Value.To;
            if (z <= a)
            {
                return list;
            }
            TimeSpan? bf = Ui.ParseHm(w.BreakFrom);
            TimeSpan? bt = Ui.ParseHm(w.BreakTo);
            if (bf != null && bt != null && bt > bf && bf < z && bt > a)
            {
                if (bf.Value > a)
                {
                    list.Add((a, bf.Value));
                }
                if (bt.Value < z)
                {
                    list.Add((bt.Value, z));
                }
            }
            else
            {
                list.Add((a, z));
            }
            return list;
        }

        private static DateTime? First(SiteInfo site, Free free, CareService sv, Clinician p, DateTime day, DateTime notBefore)
        {
            BookRow b = sv.Book;
            DateTime d0 = day.Date;
            List<BusyRow> busy = free.Busy.Where(x => x.FromAt < d0.AddDays(1).AddHours(12) && x.ToAt > d0.AddHours(-12)).ToList();
            int rooms = b.RoomtypeId == string.Empty ? int.MaxValue : free.Rooms.TryGetValue(b.RoomtypeId, out int rc) ? rc : 0;
            int step = Math.Clamp(App.SInt(site.S, "slot.minutes", 15), 5, 60);
            int len = Slots.Length(b, string.Empty);
            bool drop = Slots.Dropoff(b);
            TimeSpan dropFrom = Ui.ParseHm(b.DropFrom) ?? TimeSpan.Zero;
            TimeSpan dropTo = Ui.ParseHm(b.DropTo) ?? dropFrom;
            TimeSpan pick = Ui.ParseHm(b.PickupHm) ?? TimeSpan.Zero;
            DateTime? best = null;
            foreach ((TimeSpan wf, TimeSpan wt) in Windows(site, p, d0))
            {
                TimeSpan first = wf;
                TimeSpan last = wt;
                if (drop)
                {
                    first = first > dropTo ? first : dropTo;
                    TimeSpan cap = pick - TimeSpan.FromMinutes(60);
                    last = last < cap ? last : cap;
                }
                for (int m = (int)Math.Ceiling(first.TotalMinutes / step) * step; m + len <= (int)last.TotalMinutes; m += step)
                {
                    DateTime a = d0.AddMinutes(m);
                    DateTime z = a.AddMinutes(len);
                    if ((drop ? d0.Add(dropFrom) : a) < notBefore)
                    {
                        continue;
                    }
                    if (busy.Any(x => x.StaffId == p.Id && x.FromAt < z && x.ToAt > a))
                    {
                        continue;
                    }
                    if (rooms != int.MaxValue && busy.Count(x => x.RoomtypeId == b.RoomtypeId && x.FromAt < z && x.ToAt > a) >= rooms)
                    {
                        continue;
                    }
                    DateTime start = drop ? d0.Add(dropFrom) : a;
                    if (best == null || start < best.Value)
                    {
                        best = start;
                    }
                    break;
                }
            }
            return best;
        }

        private static DateTime? NextAny(SiteInfo site, Free free, Clinician p, int days)
        {
            if (!p.Active || !p.Accepting)
            {
                return null;
            }
            List<CareService> svs = site.Services.Where(s => Site.Online(p, s)).ToList();
            DateTime today = Site.Today(site);
            DateTime last = Site.LastDay(site);
            DateTime notBefore = Site.Earliest(site);
            for (int i = 0; i < days && svs.Count > 0; i++)
            {
                DateTime d = today.AddDays(i);
                if (d > last)
                {
                    break;
                }
                if (Site.Clinic(site, d) == null)
                {
                    continue;
                }
                DateTime? best = null;
                foreach (CareService sv in svs)
                {
                    DateTime? n = First(site, free, sv, p, d, notBefore);
                    if (n != null && (best == null || n.Value < best.Value))
                    {
                        best = n;
                    }
                }
                if (best != null)
                {
                    return best;
                }
            }
            return null;
        }

        private static string SpecTag(SiteInfo site, string key)
        {
            Specialty? sp = site.Team(key);
            return sp == null ? string.Empty : "<span class=\"pv-tag\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#3F7D6B") + "\"></i>" + Ui.Enc(sp.Name) + "</span>";
        }

        private static string Card(SiteInfo site, Free free, Clinician p, string team, string animal)
        {
            string href = "Vet?v=" + Uri.EscapeDataString(p.Id);
            List<string> teams = TeamsOf(site, p);
            bool show = (team == string.Empty || teams.Contains(team)) && (animal == string.Empty || p.Species.Count == 0 || p.Species.Contains(animal));
            string sees = p.Species.Count == 0 ? "<span class=\"pv-sp\">All animals</span>" : string.Concat(p.Species.Select(s => "<span class=\"pv-sp\">" + Ui.Enc(site.SpeciesPlural(s)) + "</span>"));
            string photo = Blocks.SafeImage(p.Photo) ? "<img class=\"pv-face\" src=\"" + Ui.Attr(p.Photo) + "\" alt=\"\" loading=\"lazy\" width=\"480\" height=\"600\">" : string.Empty;
            string foot = p.Accepting ? "<small>Next free online time</small><b>" + NextText(site, NextAny(site, free, p, 14)) + "</b>" : "<small>Not taking online bookings</small><b>Please call " + Ui.Enc(site.Settings.Phone) + "</b>";
            return "<article class=\"pv-pcard\" data-teams=\" " + Ui.Attr(string.Join(" ", teams)) + " \" data-animals=\"" + (p.Species.Count == 0 ? "*" : " " + Ui.Attr(string.Join(" ", p.Species)) + " ") + "\"" + (show ? string.Empty : " hidden") + "><a class=\"pv-pimg\" href=\"" + href + "\" tabindex=\"-1\" aria-hidden=\"true\"><span class=\"pv-noface\" style=\"--h:" + Ui.Hue(p.Id) + "\">" + Ui.Enc(Ui.Initials(p.Name)) + "</span>" + photo + "<span class=\"pv-role pv-role-" + Ui.Attr(p.Role) + "\">" + Ui.Enc(RoleName(site, p.Role)) + "</span></a><div class=\"pv-pcard-b\">" + SpecTag(site, p.Specialty) + "<h3><a href=\"" + href + "\">" + Ui.Enc(p.Name) + (p.Credentials != string.Empty ? "<small>, " + Ui.Enc(p.Credentials) + "</small>" : string.Empty) + "</a></h3><p class=\"pv-ptitle\">" + Ui.Enc(p.Title) + "</p><div class=\"pv-sees\" aria-label=\"Sees\">" + sees + "</div>" + (p.Languages.Count > 0 ? "<p class=\"pv-langs\">Speaks " + Ui.Enc(string.Join(", ", p.Languages)) + "</p>" : string.Empty) + "<a class=\"pv-next\" href=\"" + href + "\">" + foot + "</a></div></article>";
        }

        private static async Task<string> VetList(SiteInfo site, Block b, string team, string animal)
        {
            List<Specialty> specs = site.Specialties.Where(x => x.Active).OrderBy(x => x.Order).ToList();
            List<Clinician> ps = site.Providers.Where(p => p.Active).OrderBy(p => RoleOrder(p.Role)).ThenBy(p => specs.FindIndex(s => s.Key == p.Specialty)).ThenBy(p => p.Order).ToList();
            List<string> animals = site.Settings.Species.Where(k => ps.Any(p => p.Species.Count == 0 || p.Species.Contains(k))).ToList();
            specs = specs.Where(s => ps.Any(p => TeamsOf(site, p).Contains(s.Key))).ToList();
            StringBuilder sb = new StringBuilder((b.Kicker != string.Empty || b.Title != string.Empty || b.Text != string.Empty) ? Blocks.Head(P, site, b, true) : string.Empty);
            sb.Append("<div class=\"pv-filters\"><div class=\"pv-frow\"><span class=\"pv-flabel\" id=\"pv-lteam\">Team</span><div class=\"pv-chips\" role=\"group\" aria-labelledby=\"pv-lteam\" id=\"pv-teams\"><button type=\"button\" class=\"pv-chip" + (team == string.Empty ? " pv-act" : string.Empty) + "\" data-k=\"\" aria-pressed=\"" + (team == string.Empty ? "true" : "false") + "\" onclick=\"VetsJs.team('')\">Everyone</button>");
            foreach (Specialty sp in specs)
            {
                bool on = sp.Key == team;
                sb.Append("<button type=\"button\" class=\"pv-chip" + (on ? " pv-act" : string.Empty) + "\" data-k=\"" + Ui.Attr(sp.Key) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"VetsJs.team('" + Ui.Js(sp.Key) + "')\"><i style=\"background:" + (Ui.SafeColor(sp.Color) ? sp.Color : "#3F7D6B") + "\"></i>" + Ui.Enc(sp.Name) + "</button>");
            }
            sb.Append("</div></div><div class=\"pv-frow\"><span class=\"pv-flabel\" id=\"pv-lanimal\">Animal</span><div class=\"pv-chips\" role=\"group\" aria-labelledby=\"pv-lanimal\" id=\"pv-animals\"><button type=\"button\" class=\"pv-chip" + (animal == string.Empty ? " pv-act" : string.Empty) + "\" data-k=\"\" aria-pressed=\"" + (animal == string.Empty ? "true" : "false") + "\" onclick=\"VetsJs.animal('')\">Any animal</button>");
            foreach (string k in animals)
            {
                bool on = k == animal;
                sb.Append("<button type=\"button\" class=\"pv-chip" + (on ? " pv-act" : string.Empty) + "\" data-k=\"" + Ui.Attr(k) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"VetsJs.animal('" + Ui.Js(k) + "')\">" + Ui.Enc(site.SpeciesPlural(k)) + "</button>");
            }
            Free free = await LoadFree(site, 14);
            sb.Append("</div></div></div><p class=\"pv-count\" id=\"pv-count\" aria-live=\"polite\"></p><div class=\"pv-team\" id=\"pv-team\">");
            foreach (Clinician p in ps)
            {
                sb.Append(Card(site, free, p, team, animal));
            }
            sb.Append("</div><div class=\"pv-empty\" id=\"pv-empty\" hidden><b>No one matches both filters.</b><span>Try another team or animal, or call us on <a href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a> and we will help.</span><button type=\"button\" class=\"pv-btn pv-btn-ghost pv-btn-sm\" onclick=\"VetsJs.reset()\">Show everyone</button></div>");
            return Blocks.Sec(P, b, sb.ToString(), "pv-lsec");
        }
    }
}
