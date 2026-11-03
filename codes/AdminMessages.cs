using System.Data;
using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminMessages : WebPage
    {
        private const string Key = "messages";
        private const string Me = "AdminMessagesJs";
        private const int Size = 20;
        private static readonly (string Key, string Name)[] States = { ("new", "New"), ("read", "Read"), ("done", "Done") };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("Messages | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--xg-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("xg"));
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

        private async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string message)
        {
            await App.Log(c.U.StaffId, action, "messages", false);
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

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            response.SetElementContents("xg-menu", Auth.Menu("xg", Me, "AdminMessages", c));
            response.SetElementContents("xg-who", Auth.Who("xg", c));
            response.ExecuteScript(Me + ".secs();");
            if (!c.Can(Key, 'V'))
            {
                response.SetElementContents("xg-main", "<div class=\"xg-ptop\"><div><h1>Messages</h1></div></div><div class=\"xg-panel\"><p>You don't have access to this page. Ask the hospital manager if you need it.</p></div>");
                return response;
            }
            response.SetElementContents("xg-main", await MainHtml());
            response.ExecuteScript(Me + ".loaded();");
            return response;
        }

        private static string Per(string v)
        {
            return v is "today" or "7" or "30" or "range" ? v : "all";
        }

        private static string PeriodBar(string cur, string from, string to)
        {
            (string k, string n)[] chips = { ("today", "Today"), ("7", "7 days"), ("30", "30 days"), ("all", "All") };
            StringBuilder sb = new StringBuilder("<div class=\"xg-period\"><div class=\"xg-chips\">");
            foreach ((string k, string n) in chips)
            {
                sb.Append("<button type=\"button\" class=\"xg-chip" + (k == cur ? " xg-act" : string.Empty) + "\" aria-pressed=\"" + (k == cur ? "true" : "false") + "\" onclick=\"" + Me + ".per('" + k + "')\">" + n + "</button>");
            }
            sb.Append("</div><label class=\"xg-pf\"><span>From</span><input type=\"date\" id=\"xg-from\" value=\"" + Ui.Attr(cur == "range" ? from : string.Empty) + "\" onchange=\"" + Me + ".per('range')\"></label><label class=\"xg-pf\"><span>To</span><input type=\"date\" id=\"xg-to\" value=\"" + Ui.Attr(cur == "range" ? to : string.Empty) + "\" onchange=\"" + Me + ".per('range')\"></label><input type=\"hidden\" id=\"xg-per\" value=\"" + Ui.Attr(cur) + "\"></div>");
            return sb.ToString();
        }

        private static string Pill(string st)
        {
            string name = States.FirstOrDefault(x => x.Key == st).Name ?? st;
            return "<span class=\"xg-st xg-st-" + st + "\">" + Ui.Enc(name) + "</span>";
        }

        private static async Task<List<string>> Column(string sql)
        {
            var (dt, emsg) = await Helper.GetDataTable(sql);
            return emsg == string.Empty ? dt.Rows.Cast<DataRow>().Select(r => Convert.ToString(r[0]) ?? string.Empty).Where(x => x != string.Empty).ToList() : new List<string>();
        }

        private static async Task<string> MainHtml()
        {
            List<string> names = await Column("SELECT DISTINCT SENDER_NAME FROM MESSAGE ORDER BY SENDER_NAME");
            List<string> topics = await Column("SELECT DISTINCT TOPIC FROM MESSAGE WHERE TOPIC <> N'' ORDER BY TOPIC");
            StringBuilder sb = new StringBuilder("<div class=\"xg-ptop\"><div><h1>Messages</h1><p>Messages from the contact form on the website. Reply by email or phone, write a short note for the team, then mark the message done.</p></div></div>");
            sb.Append("<div class=\"xg-bar\"><label>Search<input type=\"search\" id=\"xg-q\" list=\"xg-qlist\" placeholder=\"Name, email, phone or words\" maxlength=\"60\" oninput=\"" + Me + ".search()\"></label><datalist id=\"xg-qlist\">");
            foreach (string n in names.Take(300))
            {
                sb.Append("<option value=\"" + Ui.Attr(n) + "\"></option>");
            }
            sb.Append("</datalist><label>Status<select id=\"xg-mst\" onchange=\"" + Me + ".list(1)\"><option value=\"\">Open (not done)</option>" + Ui.Opts(States.Select(x => (x.Key, x.Name)), string.Empty) + "<option value=\"all\">Everything</option></select></label>");
            sb.Append("<label>Topic<select id=\"xg-mtopic\" onchange=\"" + Me + ".list(1)\"><option value=\"\">Any topic</option>" + Ui.Opts(topics.Select(t => (t, t)), string.Empty) + "</select></label></div>");
            sb.Append("<div class=\"xg-bar\" id=\"xg-perbar\">" + PeriodBar("all", string.Empty, string.Empty) + "</div><div class=\"xg-tbl xg-mstbl\" id=\"xg-list\"></div><div id=\"xg-pager\"></div>");
            return sb.ToString();
        }

        private static string StateOf(XgMessage m)
        {
            return m.IsDone ? "done" : m.IsRead ? "read" : "new";
        }

        private const string Cols = "M.MSG_ID AS MsgId, M.SENDER_NAME AS SenderName, M.EMAIL AS Email, M.PHONE AS Phone, M.TOPIC AS Topic, M.MSG_TEXT AS MsgText, M.RECEIVED_UTC AS ReceivedUtc, M.IS_READ AS IsRead, M.IS_DONE AS IsDone, M.REPLY_NOTE AS ReplyNote, M.HANDLED_BY AS HandledBy, ISNULL(S.STAFF_NAME, N'') AS HandledName";

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
                return await Refuse(response, c, "view messages", "You don't have access to messages.");
            }
            Dictionary<string, string> s = await App.Settings();
            string term = Val("mq", 60).Trim();
            string st = Val("mst", 10);
            string topic = Val("mtopic", 40);
            string per = Per(Val("per", 10));
            string pfrom = Val("pfrom", 10);
            string pto = Val("pto", 10);
            DateTime today = App.Now(s).Date;
            DateTime? from = per switch { "today" => today, "7" => today.AddDays(-6), "30" => today.AddDays(-29), "range" => Ui.ParseDay(pfrom), _ => null };
            DateTime? to = per switch { "today" or "7" or "30" => today, "range" => Ui.ParseDay(pto), _ => null };
            TimeZoneInfo zone = App.Zone(App.S(s, "timezone"));
            List<string> where = new List<string>();
            List<SqlParameter> ps = new List<SqlParameter>();
            where.Add(st switch
            {
                "new" => "M.IS_READ = 0 AND M.IS_DONE = 0",
                "read" => "M.IS_READ = 1 AND M.IS_DONE = 0",
                "done" => "M.IS_DONE = 1",
                "all" => "1 = 1",
                _ => "M.IS_DONE = 0"
            });
            if (topic != string.Empty)
            {
                where.Add("M.TOPIC = @topic");
                ps.Add(App.P("topic", topic, 40));
            }
            if (from != null)
            {
                where.Add("M.RECEIVED_UTC >= @f");
                ps.Add(App.PDate("f", TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Unspecified), zone)));
            }
            if (to != null)
            {
                where.Add("M.RECEIVED_UTC < @t");
                ps.Add(App.PDate("t", TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Unspecified), zone)));
            }
            if (term != string.Empty)
            {
                where.Add("(M.SENDER_NAME LIKE @q OR M.EMAIL LIKE @q OR M.PHONE LIKE @q OR M.TOPIC LIKE @q OR M.MSG_TEXT LIKE @q OR M.REPLY_NOTE LIKE @q)");
                ps.Add(App.P("q", "%" + term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%", 80));
            }
            string w = string.Join(" AND ", where);
            var (cdt, cerr) = await Helper.GetDataTable("SELECT COUNT(*) FROM MESSAGE M WHERE " + w, ps.Select(p => (SqlParameter)((ICloneable)p).Clone()).ToList());
            if (cerr != string.Empty)
            {
                return Toast(response, "The messages could not be loaded. Try again.");
            }
            int total = Convert.ToInt32(cdt.Rows[0][0]);
            int page = Ui.PageOf(Val("mpg", 6), Size, total);
            List<SqlParameter> lp = ps.Select(p => (SqlParameter)((ICloneable)p).Clone()).ToList();
            lp.Add(App.PInt("skip", (page - 1) * Size));
            lp.Add(App.PInt("take", Size));
            var (list, lerr) = await Helper.GetListT<XgMessage>("SELECT " + Cols + " FROM MESSAGE M LEFT JOIN STAFF S ON S.STAFF_ID = M.HANDLED_BY WHERE " + w + " ORDER BY M.RECEIVED_UTC DESC, M.MSG_ID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", lp);
            if (lerr != string.Empty || list == null)
            {
                return Toast(response, "The messages could not be loaded. Try again.");
            }
            StringBuilder sb = new StringBuilder("<div class=\"xg-tr xg-th\"><span>From</span><span>Topic</span><span>Message</span><span>Received</span><span>Status</span><span></span></div>");
            if (list.Count == 0)
            {
                sb.Append("<div class=\"xg-tr\"><span class=\"xg-none xg-wide\">No messages match.</span></div>");
            }
            foreach (XgMessage m in list)
            {
                string handled = m.HandledName != string.Empty ? "<small>" + Ui.Enc(m.HandledName) + "</small>" : string.Empty;
                sb.Append("<div class=\"xg-tr" + (!m.IsRead ? " xg-fresh" : string.Empty) + "\"><span class=\"xg-wide\"><b>" + Ui.Enc(m.SenderName) + "</b><small>" + Ui.Enc(m.Email != string.Empty ? m.Email : m.Phone) + "</small></span><span data-l=\"Topic\">" + Ui.Enc(m.Topic == string.Empty ? "Other" : m.Topic) + "</span><span data-l=\"Message\" class=\"xg-wide\"><span class=\"xg-ex\">" + Ui.Enc(m.MsgText) + "</span></span><span data-l=\"Received\">" + Ui.Time(m.ReceivedUtc) + "</span><span data-l=\"Status\">" + Pill(StateOf(m)) + (m.ReplyNote != string.Empty ? "<i class=\"xg-noted\" title=\"Has a team note\">Note</i>" : string.Empty) + handled + "</span><span><button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"" + Me + ".open('" + Ui.Attr(Ui.Js(m.MsgId)) + "')\">Open</button></span></div>");
            }
            response.SetElementContents("xg-list", sb.ToString());
            response.SetElementContents("xg-pager", Ui.Pager("xg", page, Size, total, "message", "messages", Me + ".list"));
            response.SetElementContents("xg-perbar", PeriodBar(per, pfrom, pto));
            response.ExecuteScript(Me + ".listed();");
            return response;
        }

        private static async Task<XgMessage?> One(string mid)
        {
            var (m, emsg) = await Helper.GetOneT<XgMessage>("SELECT " + Cols + " FROM MESSAGE M LEFT JOIN STAFF S ON S.STAFF_ID = M.HANDLED_BY WHERE M.MSG_ID = @mid", new List<SqlParameter> { App.P("mid", mid, 50) });
            return emsg == string.Empty ? m : null;
        }

        private static async Task<string> ClientLink(Ctx c, XgMessage m)
        {
            string digits = Ui.Digits(m.Phone);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("em", m.Email, 120), App.P("dg", digits.Length >= 7 ? digits : "~", 30) };
            var (row, emsg) = await Helper.GetOneT<XgClient>("SELECT TOP 1 C.CLIENT_ID AS ClientId, C.FIRST_NAME + N' ' + C.LAST_NAME AS ClientName, (SELECT COUNT(*) FROM PET P WHERE P.CLIENT_ID = C.CLIENT_ID AND P.ACTIVE = 1) AS Pets FROM CLIENT C WHERE C.ACTIVE = 1 AND ((@em <> N'' AND C.EMAIL = @em) OR REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(C.PHONE, N' ', N''), N'(', N''), N')', N''), N'-', N''), N'.', N''), N'+', N'') = @dg) ORDER BY C.CREATED_UTC", ps);
            if (emsg != string.Empty || row == null)
            {
                return "<dt>Client</dt><dd>No matching client found</dd>";
            }
            string name = Ui.Enc(row.ClientName.Trim());
            return "<dt>Possible client</dt><dd>" + (c.Can("clients", 'V') ? "<a href=\"AdminClients?c=" + Uri.EscapeDataString(row.ClientId) + "\">" + name + "</a>" : name) + " &middot; " + Ui.Plural(row.Pets, "pet", "pets") + " <small class=\"xg-hint\">possible match (not verified)</small></dd>";
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
                return await Refuse(response, c, "open message", "You don't have access to messages.");
            }
            string mid = Val("mid", 50);
            bool canE = c.Can(Key, 'E');
            bool canD = c.Can(Key, 'D');
            bool marked = false;
            XgMessage? m = await One(mid);
            if (m == null)
            {
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(0);");
                return Toast(response, "That message no longer exists.");
            }
            if (canE && !m.IsRead)
            {
                string r = await Helper.PutData(new List<string> { "UPDATE MESSAGE SET IS_READ = 1, SYSDTE = GETDATE() WHERE MSG_ID = @mid AND IS_READ = 0" }, new List<SqlParameter> { App.P("mid", mid, 50) });
                if (!Helper.IsError(r))
                {
                    m.IsRead = true;
                    marked = true;
                }
            }
            Dictionary<string, string> s = await App.Settings();
            string subject = "Your message to " + App.S(s, "name", "Animal Hospital");
            string st = StateOf(m);
            bool mailOk = Ui.Email(m.Email);
            StringBuilder sb = new StringBuilder("<h2>" + Ui.Enc(m.SenderName) + "</h2><dl class=\"xg-dl\"><dt>Status</dt><dd>" + Pill(st) + "</dd><dt>Received</dt><dd>" + Ui.Time(m.ReceivedUtc, "full") + "</dd><dt>Topic</dt><dd>" + Ui.Enc(m.Topic == string.Empty ? "Other" : m.Topic) + "</dd>" + (m.Email != string.Empty ? "<dt>Email</dt><dd>" + (mailOk ? "<a href=\"mailto:" + Ui.Attr(Uri.EscapeDataString(m.Email)) + "\">" + Ui.Enc(m.Email) + "</a>" : Ui.Enc(m.Email)) + "</dd>" : string.Empty) + (m.Phone != string.Empty ? "<dt>Phone</dt><dd><a href=\"tel:" + Ui.Attr(Ui.Digits(m.Phone)) + "\">" + Ui.Enc(m.Phone) + "</a></dd>" : string.Empty) + await ClientLink(c, m) + (m.HandledName != string.Empty ? "<dt>Handled by</dt><dd>" + Ui.Enc(m.HandledName) + "</dd>" : string.Empty) + "</dl><h3>Message</h3><div class=\"xg-lmsg\">" + Ui.Enc(m.MsgText) + "</div>");
            if (canE)
            {
                sb.Append("<h3>Answer</h3><div class=\"xg-dec\">" + (mailOk ? "<a class=\"xg-btn xg-btn-sm xg-btn-accent\" href=\"mailto:" + Ui.Attr(Uri.EscapeDataString(m.Email)) + "?subject=" + Ui.Attr(Uri.EscapeDataString(subject)) + "\">Reply by email</a>" : string.Empty) + (m.Phone != string.Empty ? "<a class=\"xg-btn xg-btn-sm xg-btn-ghost\" href=\"tel:" + Ui.Attr(Ui.Digits(m.Phone)) + "\">Call " + Ui.Enc(m.Phone) + "</a>" : string.Empty) + (st != "new" && !m.IsDone ? "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"" + Me + ".state('unread')\">Mark unread</button>" : string.Empty) + (!m.IsDone ? "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"" + Me + ".state('done')\">Mark done</button>" : "<button type=\"button\" class=\"xg-btn xg-btn-sm xg-btn-ghost\" onclick=\"" + Me + ".state('open')\">Reopen</button>") + "</div>");
            }
            sb.Append("<h3>Team note</h3>");
            if (m.ReplyNote != string.Empty)
            {
                sb.Append("<div class=\"xg-note\">" + Ui.Enc(m.ReplyNote) + (m.HandledName != string.Empty ? "<small>" + Ui.Enc(m.HandledName) + "</small>" : string.Empty) + "</div>");
            }
            else if (!canE)
            {
                sb.Append("<p class=\"xg-hint\">No note yet.</p>");
            }
            if (canE)
            {
                sb.Append("<div class=\"xg-form xg-mt\"><label class=\"xg-field\"><span>" + (m.ReplyNote != string.Empty ? "Change the note" : "Write a note") + " (only staff see it)</span><textarea id=\"xg-ereply\" rows=\"3\" maxlength=\"1000\" placeholder=\"Called back on Tuesday. Booked two cat condos for the holidays.\">" + Ui.Enc(m.ReplyNote) + "</textarea></label><em class=\"xg-err\" id=\"xg-e-ereply\" role=\"alert\"></em><div><button type=\"button\" class=\"xg-btn xg-btn-sm\" id=\"xg-notebtn\" onclick=\"" + Me + ".note()\">Save note</button></div></div>");
            }
            sb.Append("<div class=\"xg-mfoot\">" + (canD ? "<button type=\"button\" class=\"xg-btn xg-btn-danger xg-left\" onclick=\"" + Me + ".remove('" + Ui.Attr(Ui.Js(m.SenderName)) + "')\">Delete</button>" : string.Empty) + "<button type=\"button\" class=\"xg-btn xg-btn-ghost\" onclick=\"" + Me + ".closeModal()\">Close</button></div>");
            response.SetElementContents("xg-mbody", sb.ToString());
            response.ExecuteScript(Me + ".openModal();" + (marked ? Me + ".list(0);" : string.Empty));
            return response;
        }

        public async Task<ApiResponse> SetState()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "message status", "You can read messages but not change them.");
            }
            string mid = Val("mid", 50);
            string to = Val("mstate", 10) is "unread" or "done" or "open" ? Val("mstate", 10) : string.Empty;
            XgMessage? m = await One(mid);
            if (m == null || to == string.Empty)
            {
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(0);");
                return Toast(response, "That message no longer exists.");
            }
            string sql = to switch
            {
                "unread" => "UPDATE MESSAGE SET IS_READ = 0, IS_DONE = 0, SYSDTE = GETDATE() WHERE MSG_ID = @mid",
                "done" => "UPDATE MESSAGE SET IS_READ = 1, IS_DONE = 1, HANDLED_BY = @sid, SYSDTE = GETDATE() WHERE MSG_ID = @mid",
                _ => "UPDATE MESSAGE SET IS_READ = 1, IS_DONE = 0, SYSDTE = GETDATE() WHERE MSG_ID = @mid"
            };
            string r = await Helper.PutData(new List<string> { sql }, new List<SqlParameter> { App.P("mid", mid, 50), App.P("sid", c.U.StaffId, 50) });
            if (Helper.IsError(r))
            {
                return Toast(response, "That did not work. Try again.");
            }
            await App.Log(c.U.StaffId, "message " + (to == "open" ? "reopened" : to), m.SenderName + " · " + (m.Topic == string.Empty ? "Other" : m.Topic), true);
            response.ExecuteScript(Me + ".changed('" + (to == "done" ? "Marked done." : to == "unread" ? "Marked unread." : "Reopened.") + "'," + (to == "unread" ? "false" : "true") + ");");
            return response;
        }

        public async Task<ApiResponse> SaveNote()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'E'))
            {
                return await Refuse(response, c, "message note", "You can read messages but not change them.");
            }
            string mid = Val("mid", 50);
            string raw = (GetDataValue("ereply") ?? string.Empty).Replace("\r", string.Empty).Trim();
            if (raw.Length > 1000)
            {
                response.SetElementContents("xg-e-ereply", "Keep the note under 1,000 characters.");
                response.ExecuteScript(Me + ".unlock();");
                return response;
            }
            XgMessage? m = await One(mid);
            if (m == null)
            {
                response.ExecuteScript(Me + ".closeModal();" + Me + ".list(0);");
                return Toast(response, "That message no longer exists.");
            }
            string r = await Helper.PutData(new List<string> { "UPDATE MESSAGE SET REPLY_NOTE = @note, HANDLED_BY = CASE WHEN @note = N'' THEN HANDLED_BY ELSE @sid END, IS_READ = 1, SYSDTE = GETDATE() WHERE MSG_ID = @mid" }, new List<SqlParameter> { App.P("mid", mid, 50), App.P("note", raw, 1000), App.P("sid", c.U.StaffId, 50) });
            if (Helper.IsError(r))
            {
                response.ExecuteScript(Me + ".unlock();");
                return Toast(response, "The note could not be saved. Try again.");
            }
            await App.Log(c.U.StaffId, raw == string.Empty ? "message note removed" : "message note", m.SenderName, true);
            response.ExecuteScript(Me + ".changed('" + (raw == string.Empty ? "Note removed." : "Note saved.") + "',true);");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!c.Can(Key, 'D'))
            {
                return await Refuse(response, c, "delete message", "Deleting messages needs the Delete right on Messages.");
            }
            string mid = Val("mid", 50);
            XgMessage? m = await One(mid);
            if (m == null)
            {
                response.ExecuteScript(Me + ".deleted();");
                return response;
            }
            string r = await Helper.PutData(new List<string> { "DELETE FROM MESSAGE WHERE MSG_ID = @mid" }, new List<SqlParameter> { App.P("mid", mid, 50) });
            if (Helper.IsError(r))
            {
                return Toast(response, "The message could not be deleted. Try again.");
            }
            await App.Log(c.U.StaffId, "delete message", m.SenderName + " · " + Ui.Clip(m.MsgText, 60), true);
            response.ExecuteScript(Me + ".deleted();");
            return response;
        }
    }
}
