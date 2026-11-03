using System.Text;
using System.Text.RegularExpressions;
using AnimalHospital.Core;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.codes
{
    public class Contact : WebPage
    {
        private const string P = "ct";

        private static readonly (string Key, string Name)[] Topics = { ("Appointments", "Appointments"), ("Boarding", "Boarding"), ("Grooming", "Grooming"), ("Prices", "Prices"), ("Records", "Records and letters"), ("New client", "Joining as a new client"), ("Other", "Something else") };

        private static readonly (string Icon, string Name, string Text)[] Ways =
        {
            ("pin", "By car", "Free parking in our own lot, with two wide bays right by the door."),
            ("users", "By bus", "The 12 bus stops a short walk from the hospital."),
            ("star", "By bike", "A covered bike rack sits next to the main entrance."),
            ("check", "Access", "Step-free entrance, wide doors and an accessible restroom.")
        };

        public override async Task OnInitialized()
        {
            HtmlDoc.ContentSecurityPolicy = true;
            SiteInfo site = await Site.Load();
            string main = Blocks.RenderPage(P, site, "contact", "Contact", b => DataPart(site, b));
            Head(site, "Contact | " + site.Settings.Name, "Opening hours, address, directions and a message form for " + site.Settings.Name + ".", Pic(site));
            HtmlDoc.HtmlBodyText = Site.Fill(HtmlDoc.HtmlBodyText, P, "ContactJs", "Contact", site).Replace("{plhd_main}", main);
        }

        public async Task<ApiResponse> Send()
        {
            ApiResponse response = new ApiResponse();
            string name = Ui.OneLine(GetDataValue("name"), 80);
            string email = Ui.Clip(GetDataValue("email"), 120).ToLowerInvariant();
            string phone = Ui.CleanPhone(GetDataValue("phone"));
            string topicValue = Ui.Clip(GetDataValue("topic"), 40);
            string topic = Topics.Any(t => t.Key == topicValue) ? topicValue : string.Empty;
            string text = Ui.Clip((GetDataValue("text") ?? string.Empty).Replace("\r", string.Empty), 1500);
            string trap = (GetDataValue("web") ?? string.Empty).Trim();
            string msgId = (GetDataValue("msgid") ?? string.Empty).Trim().ToLowerInvariant();
            bool bad = false;
            response.SetElementContents("ct-e-form", string.Empty);
            response.SetElementContents("ct-e-name", name == string.Empty ? "Please enter your name." : string.Empty);
            bad |= name == string.Empty;
            bool okMail = Ui.Email(email);
            response.SetElementContents("ct-e-email", okMail ? string.Empty : "Please enter an email address we can reply to.");
            bad |= !okMail;
            bool okPhone = phone == string.Empty || Ui.Digits(phone).Length >= 7;
            response.SetElementContents("ct-e-phone", okPhone ? string.Empty : "Please check the phone number, or leave it empty.");
            bad |= !okPhone;
            response.SetElementContents("ct-e-topic", topic == string.Empty ? "Please choose what your message is about." : string.Empty);
            bad |= topic == string.Empty;
            bool okText = text.Length >= 10;
            response.SetElementContents("ct-e-text", okText ? string.Empty : "Please write a short message (at least 10 characters).");
            bad |= !okText;
            if (bad)
            {
                response.ExecuteScript("ContactJs.invalid();");
                return response;
            }
            string first = name.Split(' ')[0];
            string topicName = Topics.First(t => t.Key == topic).Name.ToLowerInvariant();
            if (trap != string.Empty)
            {
                response.ExecuteScript("ContactJs.sent('" + Ui.Js(first) + "','" + Ui.Js(topicName) + "');");
                return response;
            }
            string ip = App.Ip();
            if (Auth.Locked("msg:" + ip) || Auth.Locked("msg:" + email))
            {
                response.SetElementContents("ct-e-form", "You have sent several messages in a short time. Please wait a few minutes or call us.");
                response.ExecuteScript("ContactJs.invalid();");
                return response;
            }
            if (!Regex.IsMatch(msgId, "^[0-9a-f]{32}$"))
            {
                msgId = App.NewId();
            }
            string result = await Helper.PutData(new List<string> { "IF NOT EXISTS (SELECT 1 FROM MESSAGE WHERE MSG_ID = @id) INSERT INTO MESSAGE (MSG_ID, SENDER_NAME, EMAIL, PHONE, TOPIC, MSG_TEXT, RECEIVED_UTC, IS_READ, IS_DONE, REPLY_NOTE, HANDLED_BY) VALUES (@id, @name, @email, @phone, @topic, @text, SYSUTCDATETIME(), 0, 0, N'', N'')" }, new List<SqlParameter>
            {
                App.P("id", msgId, 50),
                App.P("name", name, 100),
                App.P("email", email, 120),
                App.P("phone", phone, 30),
                App.P("topic", topic, 40),
                App.P("text", text, 2000)
            });
            if (Helper.IsError(result))
            {
                response.SetElementContents("ct-e-form", "Sorry, your message could not be sent just now. Please try again or call us.");
                response.ExecuteScript("ContactJs.invalid();");
                return response;
            }
            Auth.Fail("msg:" + ip);
            Auth.Fail("msg:" + email);
            await App.Log("web", "Website message", topic + " from " + name, true);
            response.ExecuteScript("ContactJs.sent('" + Ui.Js(first) + "','" + Ui.Js(topicName) + "');");
            return response;
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
            Block? b = site.BlocksOf("contact").FirstOrDefault(x => !x.Hidden && x.Type == "hero" && x.Media != string.Empty && !Blocks.IsVideo(x.Media));
            return b != null && Blocks.SafeImage(b.Media) ? b.Media : string.Empty;
        }

        private static string Form()
        {
            return "<form class=\"ct-box ct-msg\" id=\"ct-msg\" onsubmit=\"return ContactJs.send()\" novalidate><h3>" + Blocks.Icon("mail") + "Send us a message</h3><p class=\"ct-hint\">For bookings, boarding, grooming, prices or records. We reply within one working day.</p>"
                + "<label class=\"ct-field\"><span>Your name</span><input type=\"text\" id=\"ct-name\" maxlength=\"80\" autocomplete=\"name\" aria-describedby=\"ct-e-name\"><em class=\"ct-err\" id=\"ct-e-name\" role=\"alert\"></em></label>"
                + "<div class=\"ct-row2\"><label class=\"ct-field\"><span>Email</span><input type=\"email\" id=\"ct-email\" maxlength=\"120\" autocomplete=\"email\" aria-describedby=\"ct-e-email\"><em class=\"ct-err\" id=\"ct-e-email\" role=\"alert\"></em></label><label class=\"ct-field\"><span>Phone <i>(optional)</i></span><input type=\"tel\" id=\"ct-phone\" maxlength=\"30\" autocomplete=\"tel\" aria-describedby=\"ct-e-phone\"><em class=\"ct-err\" id=\"ct-e-phone\" role=\"alert\"></em></label></div>"
                + "<label class=\"ct-field\"><span>What is it about?</span><select id=\"ct-topic\" aria-describedby=\"ct-e-topic\"><option value=\"\">Choose a topic</option>" + Ui.Opts(Topics.Select(t => (t.Key, t.Name)), string.Empty) + "</select><em class=\"ct-err\" id=\"ct-e-topic\" role=\"alert\"></em></label>"
                + "<label class=\"ct-field\"><span>Message</span><textarea id=\"ct-text\" rows=\"6\" maxlength=\"1500\" aria-describedby=\"ct-e-text\"></textarea><em class=\"ct-err\" id=\"ct-e-text\" role=\"alert\"></em></label>"
                + "<div class=\"ct-hp\" aria-hidden=\"true\"><label>Leave this empty<input type=\"text\" id=\"ct-web\" tabindex=\"-1\" autocomplete=\"off\"></label></div>"
                + "<p class=\"ct-note\">" + Blocks.Icon("shield") + "<span>If your pet is unwell right now, please call instead. Messages are read during opening hours. <a href=\"Emergency\">Emergency help</a></span></p><em class=\"ct-err\" id=\"ct-e-form\" role=\"alert\"></em><button type=\"submit\" class=\"ct-btn ct-btn-accent\" id=\"ct-send\">Send message</button></form>";
        }

        private static string DataPart(SiteInfo site, Block b)
        {
            if (b.Source != "contact")
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder(Blocks.Head(P, site, b, true) + "<div class=\"ct-grid\"><div class=\"ct-left\"><div class=\"ct-box ct-call\"><h3>" + Blocks.Icon("phone") + "Call or email</h3><a class=\"ct-big\" href=\"tel:" + Ui.Attr(Site.Tel(site.Settings.Phone)) + "\">" + Ui.Enc(site.Settings.Phone) + "</a><a class=\"ct-more\" href=\"mailto:" + Ui.Attr(site.Settings.Email) + "\">" + Ui.Enc(site.Settings.Email) + "</a><p class=\"ct-addr\">" + Blocks.Icon("pin") + "<span>" + Ui.Enc(site.Settings.Address) + "</span></p></div>");
            sb.Append("<div class=\"ct-box\"><h3>" + Blocks.Icon("clock") + "Opening hours</h3><div class=\"ct-hours\">" + Site.HoursRows(P, site, "ct-hrow") + "</div><p class=\"ct-hint ct-today\">" + Site.TodayLine(P, site) + "</p></div>");
            sb.Append("<div class=\"ct-box ct-ways-box\"><h3>" + Blocks.Icon("pin") + "Getting here</h3><dl class=\"ct-ways\">");
            foreach ((string icon, string n, string t) in Ways)
            {
                sb.Append("<div><dt>" + Blocks.Icon(icon) + n + "</dt><dd>" + Ui.Enc(t) + "</dd></div>");
            }
            sb.Append("</dl></div></div>" + Form() + "</div>");
            return Blocks.Sec(P, b, sb.ToString());
        }
    }
}
