using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc("My account | " + App.S(s, "name", "Animal Hospital")));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--ac-accent:" + accent : string.Empty)
                .Replace("{plhd_name}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", Auth.DemoPill("ac"));
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
            }
            return c;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            await Auth.Close(GetDataValue("tok"));
            response.ExecuteScript("AdminAccountJs.out();");
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
            response.SetElementContents("ac-menu", Auth.Menu("ac", "AdminAccountJs", "AdminAccount", c));
            response.SetElementContents("ac-who", Auth.Who("ac", c));
            response.ExecuteScript("AdminAccountJs.secs();");
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ac-ptop\"><div><h1>My account</h1><p>" + Ui.Enc(c.U.StaffName) + " &middot; signed in as <b>" + Ui.Enc(c.U.Login) + "</b></p></div></div>");
            sb.Append("<div class=\"ac-grid\"><section class=\"ac-panel\"><h2>Change password</h2>");
            if (App.Demo && c.U.IsDemo)
            {
                sb.Append("<p class=\"ac-note\">Sample accounts keep the password <b>demo</b> so every visitor can sign in. Password changes work for real accounts.</p>");
            }
            else
            {
                sb.Append("<form class=\"ac-form\" onsubmit=\"return AdminAccountJs.save()\" novalidate>");
                sb.Append("<label class=\"ac-field\"><span>Current password</span><input type=\"password\" id=\"ac-cur\" maxlength=\"100\" autocomplete=\"current-password\"><em class=\"ac-err\" id=\"ac-e-cur\"></em></label>");
                sb.Append("<label class=\"ac-field\"><span>New password</span><input type=\"password\" id=\"ac-new\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"ac-err\" id=\"ac-e-new\"></em></label>");
                sb.Append("<label class=\"ac-field\"><span>New password again</span><input type=\"password\" id=\"ac-again\" maxlength=\"100\" autocomplete=\"new-password\"></label>");
                sb.Append("<button type=\"submit\" class=\"ac-btn ac-btn-accent\">Save new password</button><p class=\"ac-hint\">At least 8 characters. Other places where you are signed in are signed out.</p></form>");
            }
            sb.Append("</section><section class=\"ac-panel\"><h2>What you can do</h2>");
            if (c.Manager)
            {
                sb.Append("<p>You are a manager: every area, plus staff, rights and settings.</p>");
            }
            else
            {
                List<IGrouping<string, MenuRow>> groups = c.Menus.Where(m => c.Can(m.MenuKey, 'V')).GroupBy(m => m.SectionKey).ToList();
                if (groups.Count == 0)
                {
                    sb.Append("<p>No areas are open to you yet.</p>");
                }
                sb.Append("<dl class=\"ac-rights\">");
                foreach (IGrouping<string, MenuRow> g in groups)
                {
                    sb.Append("<dt>" + Ui.Enc(g.First().SectionName) + "</dt><dd>" + string.Join(", ", g.OrderBy(m => m.MenuOrder).Select(m => Ui.Enc(m.MenuName) + " <small>(" + Words(c.Rights[m.MenuKey], m.ExtraName) + ")</small>")) + "</dd>");
                }
                sb.Append("</dl>");
                if (c.U.OwnOnly)
                {
                    sb.Append("<p class=\"ac-note\">You see only your own appointments and visits.</p>");
                }
            }
            sb.Append("</section></div>");
            response.SetElementContents("ac-main", sb.ToString());
            return response;
        }

        private static string Words(string rights, string extra)
        {
            List<string> w = new List<string>();
            foreach (char ch in rights)
            {
                w.Add(ch switch { 'V' => "view", 'A' => "add", 'E' => "edit", 'D' => "delete", 'X' => extra != string.Empty ? extra.ToLowerInvariant() : "extra", _ => string.Empty });
            }
            return Ui.Enc(string.Join(", ", w.Where(x => x != string.Empty)));
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (App.Demo && c.U.IsDemo)
            {
                response.ExecuteScript("AdminAccountJs.toast('Sample accounts keep the password demo.');");
                return response;
            }
            string cur = Ui.Clip(GetDataValue("cur"), 100);
            string next = Ui.Clip(GetDataValue("next"), 100);
            string again = Ui.Clip(GetDataValue("again"), 100);
            response.SetElementContents("ac-e-cur", string.Empty);
            response.SetElementContents("ac-e-new", string.Empty);
            if (Auth.Locked("acct:" + c.U.StaffId) || !Auth.Verify(cur, c.U.PassHash))
            {
                Auth.Fail("acct:" + c.U.StaffId);
                await App.Log(c.U.StaffId, "password change", "wrong current password", false);
                response.SetElementContents("ac-e-cur", "The current password is not right.");
                return response;
            }
            string weak = Auth.Weak(next, again);
            if (weak != string.Empty)
            {
                response.SetElementContents("ac-e-new", weak);
                return response;
            }
            string result = await Helper.PutData(new List<string> { "UPDATE STAFF SET PASS_HASH = @hash, MUST_CHANGE = 0, SYSDTE = GETDATE() WHERE STAFF_ID = @sid" }, new List<SqlParameter> { App.P("hash", Auth.HashPassword(next), 200), App.P("sid", c.U.StaffId, 50) });
            if (Helper.IsError(result))
            {
                response.ExecuteScript("AdminAccountJs.toast('The password could not be saved. Try again.');");
                return response;
            }
            await Auth.CloseOthers(c.U.StaffId, c.Token);
            await App.Log(c.U.StaffId, "password change", c.U.Login, true);
            response.ExecuteScript("AdminAccountJs.saved();");
            return response;
        }
    }
}
