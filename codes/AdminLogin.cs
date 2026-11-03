using System.Text;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            await App.DemoDaily();
            Dictionary<string, string> s = await App.Settings();
            HtmlDoc.SetTitle(Ui.Enc(App.S(s, "name", "Animal Hospital") + " | Staff sign in"));
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            string demo = string.Empty;
            if (App.Demo)
            {
                var (list, emsg) = await Helper.GetListT<StaffRow>("SELECT S.STAFF_ID AS StaffId, S.LOGIN AS Login, S.STAFF_NAME AS StaffName, S.TITLE AS Title, S.ROLE_ID AS RoleId, ISNULL(R.ROLE_NAME, N'') AS RoleName, S.PROVIDER_TYPE AS ProviderType, S.PHOTO AS Photo, S.COLOR AS Color, S.OWN_ONLY AS OwnOnly, N'' AS PassHash, S.MUST_CHANGE AS MustChange, S.IS_DEMO AS IsDemo, S.ACTIVE AS Active FROM STAFF S LEFT JOIN ROLE R ON R.ROLE_ID = S.ROLE_ID WHERE S.IS_DEMO = 1 AND S.ACTIVE = 1 ORDER BY ISNULL(R.ROLE_ORDER, 99), S.STAFF_NAME");
                if (emsg == string.Empty && list != null && list.Count > 0)
                {
                    StringBuilder sb = new StringBuilder("<p class=\"lg-lhint\"><b>Live demo.</b> Pick a role. Each one sees different menus and buttons. The demo data is shared with other visitors.</p><div class=\"lg-ucards\">");
                    foreach (StaffRow u in list)
                    {
                        sb.Append("<button type=\"button\" class=\"lg-ucard\" onclick=\"AdminLoginJs.as('" + Ui.Js(u.Login) + "')\">" + Ui.Avatar("lg", u.StaffId, u.StaffName, u.Photo) + "<span><b>" + Ui.Enc(u.StaffName) + "</b><small>" + Ui.Enc(u.Title != string.Empty ? u.Title : u.RoleName) + "</small></span></button>");
                    }
                    sb.Append("</div><div class=\"lg-lor\">or sign in with the password&nbsp;<b>demo</b></div>");
                    demo = sb.ToString();
                }
            }
            string accent = App.S(s, "accent");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_style}", Ui.SafeColor(accent) ? "--lg-accent:" + accent : string.Empty)
                .Replace("{plhd_brand}", Ui.Enc(App.S(s, "name", "Animal Hospital")))
                .Replace("{plhd_demo}", demo)
                .Replace("{plhd_user}", string.Empty)
                .Replace("{plhd_pass}", string.Empty);
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40).ToLowerInvariant();
            string pass = Ui.Clip(GetDataValue("pass"), 200);
            if (login == string.Empty || pass == string.Empty)
            {
                response.SetElementContents("lg-e-pass", "Enter your user name and password.");
                return response;
            }
            if (Auth.Locked("login:" + login))
            {
                response.SetElementContents("lg-e-pass", "Too many attempts. Please wait a few minutes.");
                return response;
            }
            StaffRow? u = await Auth.StaffByLogin(login);
            bool ok = u != null && u.Active && (App.Demo || !u.IsDemo) && Auth.Verify(pass, u.PassHash);
            if (!ok && !App.Demo && login == App.Cfg("user.admin.user").ToLowerInvariant() && Auth.VerifySha(pass, App.Cfg("user.admin.passhash")))
            {
                string sid = u?.StaffId ?? App.NewId();
                string hash = Auth.HashPassword(pass);
                List<SqlParameter> ps = new List<SqlParameter> { App.P("sid", sid, 50), App.P("login", login, 40), App.P("hash", hash, 200) };
                string sql = u == null
                    ? "INSERT INTO STAFF (STAFF_ID, LOGIN, STAFF_NAME, TITLE, ROLE_ID, PASS_HASH, ACTIVE, IS_DEMO) VALUES (@sid, @login, @login, N'Hospital manager', N'manager', @hash, 1, 0)"
                    : "UPDATE STAFF SET ROLE_ID = N'manager', PASS_HASH = @hash, ACTIVE = 1, IS_DEMO = 0, MUST_CHANGE = 0, SYSDTE = GETDATE() WHERE STAFF_ID = @sid";
                string put = await Helper.PutData(new List<string> { sql }, ps);
                if (Helper.IsError(put))
                {
                    response.SetElementContents("lg-e-pass", "The first manager could not be saved. Check the database connection.");
                    return response;
                }
                u = await Auth.StaffByLogin(login);
                ok = u != null;
            }
            if (!ok || u == null)
            {
                Auth.Fail("login:" + login);
                await App.Log(u?.StaffId ?? string.Empty, "signin", login, false);
                response.SetElementContents("lg-e-pass", "Wrong user name or password.");
                return response;
            }
            if (u.MustChange && !App.Demo)
            {
                response.SetElementContents("lg-e-pass", string.Empty);
                response.ExecuteScript("AdminLoginJs.mustChange();");
                return response;
            }
            string tok = await Auth.Open(u.StaffId);
            await App.Log(u.StaffId, "signin", u.Login, true);
            response.ExecuteScript("AdminLoginJs.signedIn('" + tok + "',28800);");
            return response;
        }

        public async Task<ApiResponse> SetFirst()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40).ToLowerInvariant();
            string pass = Ui.Clip(GetDataValue("pass"), 200);
            string next = Ui.Clip(GetDataValue("next"), 100);
            string again = Ui.Clip(GetDataValue("again"), 100);
            response.SetElementContents("lg-e-new", string.Empty);
            if (Auth.Locked("login:" + login))
            {
                response.SetElementContents("lg-e-new", "Too many attempts. Please wait a few minutes.");
                return response;
            }
            StaffRow? u = await Auth.StaffByLogin(login);
            if (u == null || !u.Active || !u.MustChange || App.Demo || u.IsDemo || !Auth.Verify(pass, u.PassHash))
            {
                Auth.Fail("login:" + login);
                await App.Log(u?.StaffId ?? string.Empty, "first password", login, false);
                response.SetElementContents("lg-e-new", "Sign in again with the password you were given.");
                return response;
            }
            string weak = Auth.Weak(next, again);
            if (weak == string.Empty && next == pass)
            {
                weak = "Pick a password that is different from the one you were given.";
            }
            if (weak != string.Empty)
            {
                response.SetElementContents("lg-e-new", Ui.Enc(weak));
                return response;
            }
            string put = await Helper.PutData(new List<string> { "UPDATE STAFF SET PASS_HASH = @hash, MUST_CHANGE = 0, SYSDTE = GETDATE() WHERE STAFF_ID = @sid AND MUST_CHANGE = 1; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;", "DELETE FROM STAFFSESSION WHERE STAFF_ID = @sid" }, new List<SqlParameter> { App.P("hash", Auth.HashPassword(next), 200), App.P("sid", u.StaffId, 50) });
            if (Helper.IsError(put))
            {
                response.SetElementContents("lg-e-new", "The password could not be saved. Sign in again.");
                return response;
            }
            await App.Log(u.StaffId, "password change", u.Login + " · first sign-in", true);
            string tok = await Auth.Open(u.StaffId);
            await App.Log(u.StaffId, "signin", u.Login, true);
            response.ExecuteScript("AdminLoginJs.signedIn('" + tok + "',28800);");
            return response;
        }
    }
}
