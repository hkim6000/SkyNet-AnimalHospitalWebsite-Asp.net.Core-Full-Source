using System.Data;
using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Dashboard | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--db-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("db"));
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private static void Shell(ApiResponse response, Ctx c)
        {
            response.SetElementContents("db-menu", Auth.Menu("db", "AdminJs", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
            response.ExecuteScript("AdminJs.secs();");
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminJs.out();");
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
            Shell(response, c);
            response.SetElementContents("db-main", await Body(c));
            response.ExecuteScript("AdminJs.ready();");
            return response;
        }

        private static async Task<int> Count(string sql, List<SqlParameter> ps)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, ps);
            return emsg == string.Empty && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToInt32(dt.Rows[0][0]) : 0;
        }

        private static async Task<decimal> Sum(string sql, List<SqlParameter> ps)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql, ps);
            return emsg == string.Empty && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value ? Convert.ToDecimal(dt.Rows[0][0]) : 0m;
        }

        private static string Tile(string label, string value, string link, string note, bool hot = false)
        {
            string inner = "<small>" + Ui.Enc(label) + "</small><b>" + value + "</b><span>" + Ui.Enc(note) + "</span>";
            return link != string.Empty ? "<a class=\"db-tile" + (hot ? " db-hot" : string.Empty) + "\" href=\"" + Ui.Attr(link) + "\">" + inner + "</a>" : "<div class=\"db-tile" + (hot ? " db-hot" : string.Empty) + "\">" + inner + "</div>";
        }

        private static string Hello(string name)
        {
            string[] w = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0)
            {
                return name;
            }
            return w[0] == "Dr." && w.Length > 1 ? "Dr. " + w[w.Length - 1] : w[0];
        }

        private static async Task<string> Body(Ctx c)
        {
            Dictionary<string, string> s = await App.Settings();
            DateTime now = App.Now(s);
            DateTime today = now.Date;
            List<SqlParameter> Day() => new List<SqlParameter> { App.PDate("d0", today), App.PDate("d1", today.AddDays(1)), App.P("sid", c.U.StaffId, 50), App.PBit("own", c.U.OwnOnly) };
            string own = " AND (@own = 0 OR STAFF_ID = @sid)";
            StringBuilder sb = new StringBuilder();
            string hello = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
            sb.Append("<div class=\"db-ptop\"><div><h1>" + hello + ", " + Ui.Enc(Hello(c.U.StaffName)) + "</h1><p>" + Ui.Enc(c.U.Title != string.Empty ? c.U.Title : c.U.RoleName) + " &middot; " + Ui.LongDay(today) + "</p></div></div>");
            StringBuilder tiles = new StringBuilder();
            if (c.Can("today", 'V') || c.Can("appts", 'V') || c.Can("schedule", 'V'))
            {
                int appts = await Count("SELECT COUNT(*) FROM APPOINTMENT WHERE START_AT >= @d0 AND START_AT < @d1 AND VISIT_TYPE <> N'boarding' AND STATUS NOT IN (N'cancelled')" + own, Day());
                int waiting = await Count("SELECT COUNT(*) FROM VISIT WHERE STATUS = N'waiting'" + own, Day());
                int inroom = await Count("SELECT COUNT(*) FROM VISIT WHERE STATUS = N'inroom' AND VISIT_TYPE <> N'boarding'" + own, Day());
                int ready = await Count("SELECT COUNT(*) FROM VISIT WHERE STATUS = N'checkout'" + own, Day());
                string link = c.Can("today", 'V') ? "AdminToday" : string.Empty;
                tiles.Append(Tile("Appointments today", appts.ToString(Ui.Inv), c.Can("appts", 'V') ? "AdminAppts" : link, c.U.OwnOnly ? "Yours" : "All staff"));
                tiles.Append(Tile("Waiting", waiting.ToString(Ui.Inv), link, "Checked in, not started", waiting > 0));
                tiles.Append(Tile("In rooms", inroom.ToString(Ui.Inv), c.Can("roomboard", 'V') ? "AdminRoomBoard" : link, "Visits in progress"));
                tiles.Append(Tile("Ready for checkout", ready.ToString(Ui.Inv), c.Can("checkout", 'V') ? "AdminCheckout" : link, "Waiting at the desk", ready > 0));
            }
            if (c.Can("boarding", 'V'))
            {
                int house = await Count("SELECT COUNT(*) FROM VISIT WHERE VISIT_TYPE = N'boarding' AND STATUS = N'inroom'", Day());
                int arrive = await Count("SELECT COUNT(*) FROM APPOINTMENT WHERE VISIT_TYPE = N'boarding' AND START_AT >= @d0 AND START_AT < @d1 AND STATUS IN (N'booked', N'confirmed')", Day());
                tiles.Append(Tile("Boarding guests", house.ToString(Ui.Inv), "AdminBoarding", Ui.Plural(arrive, "arrival", "arrivals") + " today"));
            }
            if (c.Can("balances", 'V') || c.Can("invoices", 'V'))
            {
                decimal open = await Sum("SELECT SUM(BALANCE) FROM INVOICE WHERE STATUS IN (N'billed', N'partial') AND BALANCE > 0", Day());
                int overdue = await Count("SELECT COUNT(*) FROM INVOICE WHERE STATUS IN (N'billed', N'partial') AND BALANCE > 0 AND DUE_DATE < @d0", Day());
                tiles.Append(Tile("Open balances", Ui.Money(open), c.Can("balances", 'V') ? "AdminBalances" : "AdminInvoices", Ui.Plural(overdue, "invoice", "invoices") + " past due", overdue > 0));
            }
            if (c.Can("payments", 'V'))
            {
                decimal taken = await Sum("SELECT SUM(AMOUNT) FROM PAYMENT WHERE IS_VOID = 0 AND PAID_UTC >= @u0", new List<SqlParameter> { App.PDate("u0", TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(today, DateTimeKind.Unspecified), App.Zone(App.S(s, "timezone")))) });
                tiles.Append(Tile("Taken today", Ui.Money(taken), "AdminPayments", "Payments recorded"));
            }
            if (tiles.Length > 0)
            {
                sb.Append("<div class=\"db-tiles\">" + tiles + "</div>");
            }
            List<IGrouping<string, MenuRow>> groups = c.Menus.Where(m => m.ShowMenu && c.Can(m.MenuKey, 'V')).GroupBy(m => m.SectionKey).ToList();
            if (groups.Count > 0)
            {
                sb.Append("<h2 class=\"db-h2\">Your areas</h2><div class=\"db-areas\">");
                foreach (IGrouping<string, MenuRow> g in groups)
                {
                    sb.Append("<section class=\"db-area\"><h3>" + Auth.Icon("db", g.Key) + Ui.Enc(g.First().SectionName) + "</h3><ul>");
                    foreach (MenuRow m in g.OrderBy(x => x.MenuOrder))
                    {
                        sb.Append("<li><a href=\"" + Ui.Attr(m.PageName) + "\"><b>" + Ui.Enc(m.MenuName) + "</b><small>" + Ui.Enc(m.Hint) + "</small></a></li>");
                    }
                    sb.Append("</ul></section>");
                }
                sb.Append("</div>");
            }
            else
            {
                sb.Append("<div class=\"db-panel\"><p>No areas are open to you yet. Ask a manager to give you rights in Staff and permissions.</p></div>");
            }
            if (c.Can("activity", 'V'))
            {
                var (acts, aerr) = await Helper.GetListT<ActivityRow>("SELECT TOP 8 A.ACT_UTC AS ActUtc, ISNULL(S.STAFF_NAME, N'Website') AS StaffName, A.ACTION AS Action, A.TARGET AS Target, A.OK AS Ok FROM ACTIVITY A LEFT JOIN STAFF S ON S.STAFF_ID = A.STAFF_ID ORDER BY A.ACT_UTC DESC");
                if (aerr == string.Empty && acts != null && acts.Count > 0)
                {
                    sb.Append("<div class=\"db-panel db-acts-p\"><h2>Recent activity</h2><ul class=\"db-feed\">");
                    foreach (ActivityRow a in acts)
                    {
                        sb.Append("<li" + (a.Ok ? string.Empty : " class=\"db-bad\"") + "><b>" + Ui.Enc(a.StaffName) + "</b> " + Ui.Enc(a.Action) + (a.Target != string.Empty ? " &middot; " + Ui.Enc(a.Target) : string.Empty) + " " + Ui.Time(a.ActUtc) + "</li>");
                    }
                    sb.Append("</ul></div>");
                }
            }
            if (App.Demo && c.Manager)
            {
                sb.Append("<div class=\"db-panel db-reset\"><div><h2>Demo data</h2><p>Put back the sample clients, pets, appointments, visits and invoices, with dates moved to today. Everything other visitors changed is lost.</p></div><button type=\"button\" class=\"db-btn db-btn-ghost\" onclick=\"AdminJs.reset()\">Reset demo data</button></div>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> ResetDemo()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!App.Demo || !c.Manager)
            {
                await App.Log(c.U.StaffId, "reset demo", "not allowed", false);
                response.ExecuteScript("AdminJs.toast('Only a manager can reset the demo data, and only in demo mode.');");
                return response;
            }
            string result = await App.DemoLoad(await App.Settings());
            if (Helper.IsError(result))
            {
                response.ExecuteScript("AdminJs.toast('" + Ui.Js("The demo data could not be reset. " + result) + "');");
                return response;
            }
            await App.Log(c.U.StaffId, "reset demo", "sample data loaded", true);
            response.SetElementContents("db-main", await Body(c));
            response.ExecuteScript("AdminJs.ready();AdminJs.toast('The demo data is back to its starting point.');");
            return response;
        }
    }
}
