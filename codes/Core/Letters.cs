using System.Globalization;
using System.Text;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;
using SkyNet;

namespace AnimalHospital.Core
{
    public static class Letters
    {
        public static readonly string[] Kinds = { "visit", "invoice", "statement", "booking" };
        private const string Font = "font-family:Arial,Helvetica,sans-serif;";
        private const string Ink = "#1f2a27";
        private const string Muted = "#5b6b66";
        private const string Line = "#e3e8e5";
        private const string Soft = "#f4f6f5";
        private const string Th = "text-align:left;padding:7px 8px;background:#f4f6f5;font-size:12px;font-weight:bold;color:#5b6b66;";
        private const string ThR = "text-align:right;padding:7px 8px;background:#f4f6f5;font-size:12px;font-weight:bold;color:#5b6b66;";
        private const string Td = "padding:8px;border-bottom:1px solid #e3e8e5;vertical-align:top;font-size:14px;color:#1f2a27;";
        private const string TdR = "padding:8px;border-bottom:1px solid #e3e8e5;vertical-align:top;font-size:14px;color:#1f2a27;text-align:right;white-space:nowrap;";
        private const string Tbl = "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;width:100%;\">";

        public static bool MailReady()
        {
            string server = App.Cfg("app.mail.server");
            string addr = App.Cfg("app.mail.addr");
            string pw = App.Cfg("app.mail.password");
            return server != string.Empty && Ui.Email(addr) && !addr.Equals("noreply@email.com", StringComparison.OrdinalIgnoreCase) && pw != string.Empty && pw != "your-password";
        }

        private static string Accent(Dictionary<string, string> s)
        {
            string a = App.S(s, "accent");
            return Ui.SafeColor(a) ? a : "#3F7D6B";
        }

        private static string Text(string? v)
        {
            return Ui.Enc((v ?? string.Empty).Replace("\r", string.Empty)).Replace("\\n", "<br>").Replace("\n", "<br>");
        }

        private static string Num(decimal v)
        {
            return v.ToString(v == Math.Floor(v) ? "#,##0" : "#,##0.##", CultureInfo.InvariantCulture);
        }

        private static string Val(decimal v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Frame(Dictionary<string, string> s, string inner)
        {
            string ac = Accent(s);
            StringBuilder sb = new StringBuilder();
            sb.Append("<div style=\"margin:0;padding:24px 10px;background:" + Soft + ";" + Font + "color:" + Ink + ";\">");
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:640px;width:100%;margin:0 auto;background:#ffffff;border:1px solid " + Line + ";border-radius:10px;border-collapse:separate;\">");
            sb.Append("<tr><td style=\"height:6px;background:" + ac + ";border-radius:10px 10px 0 0;font-size:0;line-height:0;\">&nbsp;</td></tr>");
            sb.Append("<tr><td style=\"padding:22px 24px 26px;\">");
            sb.Append(Tbl + "<tr><td style=\"vertical-align:top;\"><div style=\"font-size:20px;font-weight:bold;color:" + ac + ";\">" + Ui.Enc(App.S(s, "name", "Animal Hospital")) + "</div>");
            List<string> bits = new List<string>();
            foreach (string k in new[] { "address", "phone", "email" })
            {
                if (App.S(s, k) != string.Empty)
                {
                    bits.Add(Ui.Enc(App.S(s, k)));
                }
            }
            sb.Append("<div style=\"font-size:13px;color:" + Muted + ";margin-top:4px;line-height:1.5;\">" + string.Join(" &middot; ", bits) + "</div></td></tr></table>");
            sb.Append(inner);
            sb.Append("</td></tr></table>");
            sb.Append("<p style=\"max-width:640px;margin:14px auto 0;font-size:12px;color:" + Muted + ";text-align:center;line-height:1.5;\">" + Ui.Enc(App.S(s, "name", "Animal Hospital")) + (App.S(s, "phone") != string.Empty ? " &middot; " + Ui.Enc(App.S(s, "phone")) : string.Empty) + "</p>");
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Title(string title, string sub)
        {
            return "<h1 style=\"margin:22px 0 4px;font-size:22px;line-height:1.3;color:" + Ink + ";" + Font + "\">" + Ui.Enc(title) + "</h1>" + (sub != string.Empty ? "<p style=\"margin:0 0 6px;font-size:14px;color:" + Muted + ";\">" + sub + "</p>" : string.Empty);
        }

        private static string H2(Dictionary<string, string> s, string text)
        {
            return "<h2 style=\"margin:24px 0 8px;padding-bottom:5px;border-bottom:1px solid " + Line + ";font-size:16px;color:" + Accent(s) + ";" + Font + "\">" + Ui.Enc(text) + "</h2>";
        }

        private static string P(string html)
        {
            return "<p style=\"margin:10px 0;font-size:15px;line-height:1.55;color:" + Ink + ";\">" + html + "</p>";
        }

        private static string Small(string html)
        {
            return "<p style=\"margin:8px 0;font-size:13px;line-height:1.5;color:" + Muted + ";\">" + html + "</p>";
        }

        private static string Day(DateTime d)
        {
            return d.ToString("MMMM d, yyyy", Ui.Us);
        }

        private static string OwnerName(string first, string last)
        {
            string n = (first + " " + last).Trim();
            return n == string.Empty ? "Pet owner" : n;
        }

        private static string BillTo(BlInvoice inv)
        {
            List<string> rows = new List<string> { "<b>" + Ui.Enc(OwnerName(inv.FirstName, inv.LastName)) + "</b>" };
            if (inv.Address1 != string.Empty)
            {
                rows.Add(Ui.Enc(inv.Address1));
            }
            string town = (inv.City + (inv.State != string.Empty ? ", " + inv.State : string.Empty) + (inv.Zip != string.Empty ? " " + inv.Zip : string.Empty)).Trim().Trim(',').Trim();
            if (town != string.Empty)
            {
                rows.Add(Ui.Enc(town));
            }
            return "<div style=\"margin:14px 0 4px;font-size:14px;line-height:1.5;color:" + Ink + ";\"><span style=\"display:block;font-size:12px;color:" + Muted + ";\">Bill to</span>" + string.Join("<br>", rows) + "</div>";
        }

        private static string TotalRow(string label, string value, bool strong = false, string note = "")
        {
            string st = strong ? "font-weight:bold;font-size:16px;" : "font-size:14px;";
            return "<tr><td style=\"padding:5px 8px;text-align:right;color:" + (strong ? Ink : Muted) + ";" + st + "\">" + Ui.Enc(label) + (note != string.Empty ? "<br><span style=\"font-size:12px;color:" + Muted + ";font-weight:normal;\">" + Ui.Enc(note) + "</span>" : string.Empty) + "</td><td style=\"padding:5px 8px;text-align:right;white-space:nowrap;width:120px;color:" + Ink + ";" + st + "\">" + value + "</td></tr>";
        }

        private static string InvoiceBlock(Dictionary<string, string> s, BlInvoice inv, List<BlLine> lines, List<BlPay> pays)
        {
            StringBuilder sb = new StringBuilder();
            DateTime made = App.Local(s, inv.CreatedUtc);
            sb.Append(Small("Invoice <b style=\"color:" + Ink + ";\">" + Ui.Enc(inv.InvoiceNo) + "</b> &middot; " + Day(made) + (inv.PetName != string.Empty ? " &middot; " + Ui.Enc(inv.PetName) : string.Empty) + " &middot; " + Ui.Enc(Billing.StatusName(inv.Status))));
            bool anyTax = lines.Any(x => x.Taxable);
            sb.Append(Tbl + "<tr><th style=\"" + Th + "\">Item</th><th style=\"" + ThR + "\">Qty</th><th style=\"" + ThR + "\">Price</th><th style=\"" + ThR + "\">Amount</th></tr>");
            if (lines.Count == 0)
            {
                sb.Append("<tr><td colspan=\"4\" style=\"" + Td + "color:" + Muted + ";\">No charges.</td></tr>");
            }
            foreach (BlLine l in lines)
            {
                sb.Append("<tr><td style=\"" + Td + "\">" + Ui.Enc(l.Description) + (l.Taxable ? " <span style=\"color:" + Muted + ";\">*</span>" : string.Empty) + "</td><td style=\"" + TdR + "\">" + Num(l.Qty) + "</td><td style=\"" + TdR + "\">" + Ui.Money(l.UnitPrice) + "</td><td style=\"" + TdR + "\">" + Ui.Money(l.Amount) + "</td></tr>");
            }
            sb.Append("</table>");
            sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"border-collapse:collapse;margin:8px 0 0 auto;\">");
            sb.Append(TotalRow("Subtotal", Ui.Money(inv.Subtotal)));
            if (Billing.DiscountApplied(inv) > 0)
            {
                sb.Append(TotalRow("Discount", Ui.Money(-Billing.DiscountApplied(inv)), false, Billing.DiscountLabel(inv.DiscountNote)));
            }
            if (inv.Tax > 0 || anyTax)
            {
                sb.Append(TotalRow("Tax", Ui.Money(inv.Tax)));
            }
            sb.Append(TotalRow("Total", Ui.Money(inv.Total), true));
            List<BlPay> good = pays.Where(x => !x.IsVoid).ToList();
            if (good.Count > 0)
            {
                sb.Append(TotalRow("Paid", Ui.Money(-inv.Paid)));
            }
            if (inv.Status != "void")
            {
                sb.Append(TotalRow(inv.Balance > 0 ? "Balance due" : "Balance", Ui.Money(inv.Balance), true));
            }
            sb.Append("</table>");
            if (anyTax)
            {
                sb.Append(Small("* Taxable item."));
            }
            if (good.Count > 0)
            {
                sb.Append("<div style=\"margin-top:14px;font-size:13px;font-weight:bold;color:" + Muted + ";\">Payments</div>" + Tbl);
                foreach (BlPay p in good)
                {
                    sb.Append("<tr><td style=\"" + Td + "\">" + Day(App.Local(s, p.PaidUtc)) + " &middot; " + Ui.Enc(Billing.MethodName(p.Method)) + (p.Reference != string.Empty ? " <span style=\"color:" + Muted + ";\">" + Ui.Enc(p.Reference) + "</span>" : string.Empty) + "</td><td style=\"" + TdR + "\">" + Ui.Money(p.Amount) + "</td></tr>");
                }
                sb.Append("</table>");
            }
            if (inv.Status == "void")
            {
                sb.Append(P("<b>This invoice was voided.</b>" + (inv.VoidReason != string.Empty ? " " + Ui.Enc(inv.VoidReason) : string.Empty) + " Nothing is owed on it."));
            }
            else if (inv.Balance > 0)
            {
                sb.Append("<div style=\"margin:16px 0 0;padding:12px 14px;background:" + Soft + ";border-radius:8px;font-size:15px;line-height:1.5;color:" + Ink + ";\"><b>Balance due: " + Ui.Money(inv.Balance) + "</b>" + (inv.DueDate.HasValue ? " by " + Day(inv.DueDate.Value) : string.Empty) + ".<br><span style=\"font-size:13px;color:" + Muted + ";\">" + HowToPay(s) + "</span></div>");
            }
            else if (inv.Status == "paid")
            {
                sb.Append(P("<b>Paid in full.</b> Thank you!"));
            }
            return sb.ToString();
        }

        private static string HowToPay(Dictionary<string, string> s)
        {
            string phone = App.S(s, "phone");
            return "Pay at the front desk by card, cash or check" + (phone != string.Empty ? ", or call " + Ui.Enc(phone) + " to pay by card" : string.Empty) + ". Please write the invoice number on a check.";
        }

        private static string Note(Dictionary<string, string> s, string extra = "")
        {
            string n = App.S(s, "invoice.note");
            StringBuilder sb = new StringBuilder();
            if (extra != string.Empty)
            {
                sb.Append(Small(Text(extra)));
            }
            if (n != string.Empty)
            {
                sb.Append("<p style=\"margin:20px 0 0;padding-top:12px;border-top:1px solid " + Line + ";font-size:13px;line-height:1.5;color:" + Muted + ";\">" + Text(n) + "</p>");
            }
            return sb.ToString();
        }

        public static async Task<(string To, string Subject, string Html, List<string> Files)> Visit(string visitId, Dictionary<string, string> s)
        {
            List<string> files = new List<string>();
            var (v, verr) = await Helper.GetOneT<LtVisit>("SELECT V.VISIT_ID AS VisitId, V.VISIT_NO AS VisitNo, V.VISIT_TYPE AS VisitType, V.REASON AS Reason, V.CHECKIN_AT AS CheckinAt, V.CLOSED_AT AS ClosedAt, V.RECHECK_DATE AS RecheckDate, V.SUMMARY AS Summary, P.PET_ID AS PetId, P.PET_NAME AS PetName, ISNULL(SP.SPECIES_NAME, P.SPECIES_KEY) AS SpeciesName, P.BREED AS Breed, V.CLIENT_ID AS ClientId, ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.EMAIL, N'') AS Email, ISNULL(S.STAFF_NAME, N'') AS StaffName FROM VISIT V JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN SPECIES SP ON SP.SPECIES_KEY = P.SPECIES_KEY LEFT JOIN CLIENT C ON C.CLIENT_ID = V.CLIENT_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID WHERE V.VISIT_ID = @vid", new List<SqlParameter> { App.P("vid", visitId, 50) });
            if (verr != string.Empty || v == null)
            {
                return (string.Empty, string.Empty, string.Empty, files);
            }
            bool stay = v.VisitType == "boarding";
            var (meds, _) = await Helper.GetListT<LtMed>("SELECT ITEM_NAME AS ItemName, QTY AS Qty, UNIT AS Unit, DOSE AS Dose, INSTRUCTIONS AS Instructions FROM ORDERLINE WHERE VISIT_ID = @vid AND STATUS <> N'cancelled' AND DISPENSE = N'home' AND ITEM_TYPE = N'medicine' ORDER BY LINE_ORDER, ITEM_NAME", new List<SqlParameter> { App.P("vid", visitId, 50) });
            var (vax, _) = await Helper.GetListT<LtVax>("SELECT VAX_NAME AS VaxName, GIVEN_DATE AS GivenDate, DUE_DATE AS DueDate FROM PETVACCINE WHERE VISIT_ID = @vid ORDER BY VAX_NAME", new List<SqlParameter> { App.P("vid", visitId, 50) });
            var (res, _) = await Helper.GetListT<LtResult>("SELECT R.LINE_ID AS LineId, O.ITEM_NAME AS TestName, R.FIELD_NAME AS FieldName, R.VALUE_NUM AS ValueNum, R.VALUE_TEXT AS ValueText, R.UNIT AS Unit, R.LOW_VALUE AS LowValue, R.HIGH_VALUE AS HighValue, R.FLAG AS Flag FROM RESULT R JOIN ORDERLINE O ON O.LINE_ID = R.LINE_ID LEFT JOIN TESTFIELD F ON F.FIELD_ID = R.FIELD_ID WHERE R.VISIT_ID = @vid AND O.STATUS <> N'cancelled' ORDER BY O.LINE_ORDER, O.LINE_ID, ISNULL(F.FIELD_ORDER, 0), R.FIELD_NAME", new List<SqlParameter> { App.P("vid", visitId, 50) });
            var (fl, _) = await Helper.GetListT<LtFile>("SELECT STORED_NAME AS StoredName, FILE_NAME AS FileName, TITLE AS Title FROM VISITFILE WHERE VISIT_ID = @vid AND SEND_OWNER = 1 ORDER BY UPLOADED_UTC", new List<SqlParameter> { App.P("vid", visitId, 50) });
            BlInvoice? inv = await Billing.ForVisit(visitId);
            string name = App.S(s, "name", "Animal Hospital");
            DateTime day = v.CheckinAt.Date;
            StringBuilder sb = new StringBuilder();
            string sub = Ui.Enc(Day(day)) + (stay && v.ClosedAt.HasValue && v.ClosedAt.Value.Date != day ? " to " + Ui.Enc(Day(v.ClosedAt.Value.Date)) : string.Empty) + (v.StaffName != string.Empty && !stay ? " &middot; with " + Ui.Enc(v.StaffName) : string.Empty);
            sb.Append(Title(v.PetName + (stay ? "'s stay summary" : "'s visit summary"), sub));
            sb.Append(P("Hi " + Ui.Enc(v.FirstName != string.Empty ? v.FirstName : "there") + ","));
            sb.Append(P(stay ? "Thank you for letting " + Ui.Enc(v.PetName) + " stay with us. Here is a summary of the stay and the invoice." : "Thank you for bringing " + Ui.Enc(v.PetName) + " in. Here is a summary of the visit, what to do at home and the invoice."));
            if (v.Reason != string.Empty && !stay)
            {
                sb.Append(Small("Reason for the visit: " + Ui.Enc(v.Reason)));
            }
            if (v.Summary.Trim() != string.Empty)
            {
                sb.Append(H2(s, "Instructions for home"));
                sb.Append(P(Text(v.Summary.Trim())));
            }
            List<LtMed> home = meds ?? new List<LtMed>();
            if (home.Count > 0)
            {
                sb.Append(H2(s, "Medicines going home"));
                sb.Append(Tbl + "<tr><th style=\"" + Th + "\">Medicine</th><th style=\"" + Th + "\">How to give it</th></tr>");
                foreach (LtMed m in home)
                {
                    string how = string.Join("<br>", new[] { m.Dose, m.Instructions }.Where(x => x.Trim() != string.Empty).Select(x => Text(x.Trim())));
                    sb.Append("<tr><td style=\"" + Td + "\"><b>" + Ui.Enc(m.ItemName) + "</b><br><span style=\"font-size:13px;color:" + Muted + ";\">" + Num(m.Qty) + " " + Ui.Enc(m.Unit) + "</span></td><td style=\"" + Td + "\">" + (how != string.Empty ? how : "<span style=\"color:" + Muted + ";\">As shown on the label.</span>") + "</td></tr>");
                }
                sb.Append("</table>");
            }
            List<LtVax> shots = vax ?? new List<LtVax>();
            if (shots.Count > 0)
            {
                sb.Append(H2(s, "Vaccines given"));
                sb.Append(Tbl + "<tr><th style=\"" + Th + "\">Vaccine</th><th style=\"" + ThR + "\">Next due</th></tr>");
                foreach (LtVax x in shots)
                {
                    sb.Append("<tr><td style=\"" + Td + "\">" + Ui.Enc(x.VaxName) + "</td><td style=\"" + TdR + "\">" + Day(x.DueDate) + "</td></tr>");
                }
                sb.Append("</table>");
            }
            List<LtResult> rs = res ?? new List<LtResult>();
            if (rs.Count > 0)
            {
                sb.Append(H2(s, "Test results"));
                foreach (IGrouping<string, LtResult> g in rs.GroupBy(x => x.LineId))
                {
                    sb.Append("<div style=\"margin:12px 0 4px;font-size:14px;font-weight:bold;color:" + Ink + ";\">" + Ui.Enc(g.First().TestName) + "</div>");
                    sb.Append(Tbl + "<tr><th style=\"" + Th + "\">Test</th><th style=\"" + ThR + "\">Result</th><th style=\"" + ThR + "\">Normal range</th></tr>");
                    foreach (LtResult r in g)
                    {
                        string value = r.ValueNum.HasValue ? Val(r.ValueNum.Value) + (r.Unit != string.Empty ? " " + Ui.Enc(r.Unit) : string.Empty) : Ui.Enc(r.ValueText);
                        string flag = r.Flag == "H" ? " <b style=\"color:#b42318;\">High</b>" : r.Flag == "L" ? " <b style=\"color:#1d5fa8;\">Low</b>" : string.Empty;
                        string range = r.LowValue.HasValue && r.HighValue.HasValue ? Val(r.LowValue.Value) + " &ndash; " + Val(r.HighValue.Value) : r.LowValue.HasValue ? "at least " + Val(r.LowValue.Value) : r.HighValue.HasValue ? "up to " + Val(r.HighValue.Value) : "&nbsp;";
                        sb.Append("<tr><td style=\"" + Td + "\">" + Ui.Enc(r.FieldName) + "</td><td style=\"" + TdR + "\">" + value + flag + "</td><td style=\"" + TdR + "color:" + Muted + ";\">" + range + "</td></tr>");
                    }
                    sb.Append("</table>");
                }
                sb.Append(Small("Your vet will talk with you about any result marked High or Low."));
            }
            if (v.RecheckDate.HasValue)
            {
                sb.Append(H2(s, "Next visit"));
                sb.Append(P("Please book a recheck for " + Ui.Enc(v.PetName) + " around <b>" + Day(v.RecheckDate.Value) + "</b>." + (App.S(s, "phone") != string.Empty ? " Call us at " + Ui.Enc(App.S(s, "phone")) + " or book online." : string.Empty)));
            }
            List<LtFile> fs = fl ?? new List<LtFile>();
            foreach (LtFile f in fs)
            {
                if (f.StoredName != string.Empty && !f.StoredName.Contains("..") && !f.StoredName.Contains('/') && !f.StoredName.Contains('\\'))
                {
                    files.Add(Path.Combine(App.VaultDir, f.StoredName));
                }
            }
            if (fs.Count > 0)
            {
                sb.Append(Small("Attached: " + string.Join(", ", fs.Select(f => Ui.Enc(f.Title != string.Empty ? f.Title : f.FileName))) + "."));
            }
            if (inv != null)
            {
                sb.Append(H2(s, "Invoice"));
                sb.Append(InvoiceBlock(s, inv, await Billing.Lines(inv.InvoiceId), await Billing.Payments(inv.InvoiceId)));
            }
            sb.Append(P("Questions about " + Ui.Enc(v.PetName) + "? Just reply to this email" + (App.S(s, "phone") != string.Empty ? " or call " + Ui.Enc(App.S(s, "phone")) : string.Empty) + "."));
            sb.Append(Note(s, inv != null ? inv.Note : string.Empty));
            string to = Ui.Email(v.Email.Trim()) ? v.Email.Trim() : string.Empty;
            return (to, Ui.Clip(v.PetName + (stay ? "'s stay summary from " : "'s visit summary from ") + name, 200), Frame(s, sb.ToString()), files);
        }

        public static async Task<(string To, string Subject, string Html, List<string> Files)> Invoice(string invoiceId, Dictionary<string, string> s)
        {
            List<string> files = new List<string>();
            BlInvoice? inv = await Billing.Get(invoiceId);
            if (inv == null)
            {
                return (string.Empty, string.Empty, string.Empty, files);
            }
            string name = App.S(s, "name", "Animal Hospital");
            StringBuilder sb = new StringBuilder();
            sb.Append(Title("Invoice " + inv.InvoiceNo, Ui.Enc(Day(App.Local(s, inv.CreatedUtc))) + (inv.PetName != string.Empty ? " &middot; " + Ui.Enc(inv.PetName) : string.Empty)));
            sb.Append(BillTo(inv));
            sb.Append(P("Hi " + Ui.Enc(inv.FirstName != string.Empty ? inv.FirstName : "there") + ", here is your invoice" + (inv.PetName != string.Empty ? " for " + Ui.Enc(inv.PetName) + (inv.VisitType == "boarding" ? "'s stay" : "'s visit") + (inv.CheckinAt.HasValue ? " on " + Day(inv.CheckinAt.Value) : string.Empty) : string.Empty) + "."));
            sb.Append(InvoiceBlock(s, inv, await Billing.Lines(inv.InvoiceId), await Billing.Payments(inv.InvoiceId)));
            sb.Append(Note(s, inv.Note));
            string to = Ui.Email(inv.Email.Trim()) ? inv.Email.Trim() : string.Empty;
            return (to, Ui.Clip("Invoice " + inv.InvoiceNo + " from " + name, 200), Frame(s, sb.ToString()), files);
        }

        public static string AgeName(int daysPast)
        {
            return daysPast <= 0 ? "Current" : daysPast <= 30 ? "1-30 days" : daysPast <= 60 ? "31-60 days" : daysPast <= 90 ? "61-90 days" : "Over 90 days";
        }

        public static DateTime DueOf(BlInvoice inv, Dictionary<string, string> s)
        {
            return inv.DueDate.HasValue ? inv.DueDate.Value.Date : App.Local(s, inv.BilledUtc ?? inv.CreatedUtc).Date.AddDays(Billing.DueDays(s));
        }

        public static async Task<(string To, string Subject, string Html, List<string> Files)> Statement(string clientId, Dictionary<string, string> s)
        {
            List<string> files = new List<string>();
            var (ct, cerr) = await Helper.GetDataTable("SELECT FIRST_NAME, LAST_NAME, EMAIL, ADDRESS1, CITY, STATE, ZIP, CLIENT_NO FROM CLIENT WHERE CLIENT_ID = @cid", new List<SqlParameter> { App.P("cid", clientId, 50) });
            if (cerr != string.Empty || ct.Rows.Count == 0)
            {
                return (string.Empty, string.Empty, string.Empty, files);
            }
            BlInvoice who = new BlInvoice
            {
                FirstName = Convert.ToString(ct.Rows[0]["FIRST_NAME"]) ?? string.Empty,
                LastName = Convert.ToString(ct.Rows[0]["LAST_NAME"]) ?? string.Empty,
                Email = Convert.ToString(ct.Rows[0]["EMAIL"]) ?? string.Empty,
                Address1 = Convert.ToString(ct.Rows[0]["ADDRESS1"]) ?? string.Empty,
                City = Convert.ToString(ct.Rows[0]["CITY"]) ?? string.Empty,
                State = Convert.ToString(ct.Rows[0]["STATE"]) ?? string.Empty,
                Zip = Convert.ToString(ct.Rows[0]["ZIP"]) ?? string.Empty
            };
            string clientNo = Convert.ToString(ct.Rows[0]["CLIENT_NO"]) ?? string.Empty;
            var (list, _) = await Helper.GetListT<BlInvoice>("SELECT " + Billing.InvCols + Billing.InvFrom + " WHERE I.CLIENT_ID = @cid AND I.STATUS IN (N'billed', N'partial') AND I.BALANCE > 0 ORDER BY I.CREATED_UTC, I.INVOICE_NO", new List<SqlParameter> { App.P("cid", clientId, 50) });
            List<BlInvoice> open = list ?? new List<BlInvoice>();
            DateTime today = App.Now(s).Date;
            string name = App.S(s, "name", "Animal Hospital");
            StringBuilder sb = new StringBuilder();
            sb.Append(Title("Statement", Ui.Enc(Day(today)) + (clientNo != string.Empty ? " &middot; Account " + Ui.Enc(clientNo) : string.Empty)));
            sb.Append(BillTo(who));
            decimal due = open.Sum(x => x.Balance);
            if (open.Count == 0)
            {
                sb.Append(P("Hi " + Ui.Enc(who.FirstName != string.Empty ? who.FirstName : "there") + ", your account is paid in full. Thank you!"));
            }
            else
            {
                sb.Append(P("Hi " + Ui.Enc(who.FirstName != string.Empty ? who.FirstName : "there") + ", this statement lists the invoices that still have a balance. The total due is <b>" + Ui.Money(due) + "</b>."));
                decimal[] ages = new decimal[5];
                sb.Append(Tbl + "<tr><th style=\"" + Th + "\">Invoice</th><th style=\"" + Th + "\">Pet</th><th style=\"" + ThR + "\">Total</th><th style=\"" + ThR + "\">Balance</th><th style=\"" + ThR + "\">Due</th></tr>");
                foreach (BlInvoice inv in open)
                {
                    DateTime dd = DueOf(inv, s);
                    int past = (today - dd).Days;
                    int b = past <= 0 ? 0 : past <= 30 ? 1 : past <= 60 ? 2 : past <= 90 ? 3 : 4;
                    ages[b] += inv.Balance;
                    string late = past > 0 ? "<br><span style=\"font-size:12px;color:#b42318;\">" + Ui.Enc(AgeName(past)) + " past due</span>" : string.Empty;
                    sb.Append("<tr><td style=\"" + Td + "\"><b>" + Ui.Enc(inv.InvoiceNo) + "</b><br><span style=\"font-size:12px;color:" + Muted + ";\">" + Day(App.Local(s, inv.CreatedUtc)) + "</span></td><td style=\"" + Td + "\">" + Ui.Enc(inv.PetName) + "</td><td style=\"" + TdR + "\">" + Ui.Money(inv.Total) + "</td><td style=\"" + TdR + "\"><b>" + Ui.Money(inv.Balance) + "</b></td><td style=\"" + TdR + "\">" + dd.ToString("MMM d, yyyy", Ui.Us) + late + "</td></tr>");
                }
                sb.Append("</table>");
                sb.Append("<div style=\"margin-top:16px;font-size:13px;font-weight:bold;color:" + Muted + ";\">How old the balance is</div>");
                sb.Append(Tbl + "<tr>");
                string[] heads = { "Current", "1-30 days", "31-60 days", "61-90 days", "Over 90 days" };
                foreach (string h in heads)
                {
                    sb.Append("<th style=\"" + ThR + "\">" + Ui.Enc(h) + "</th>");
                }
                sb.Append("</tr><tr>");
                foreach (decimal a in ages)
                {
                    sb.Append("<td style=\"" + TdR + "\">" + Ui.Money(a) + "</td>");
                }
                sb.Append("</tr></table>");
                sb.Append("<div style=\"margin:16px 0 0;padding:12px 14px;background:" + Soft + ";border-radius:8px;font-size:15px;line-height:1.5;color:" + Ink + ";\"><b>Total due: " + Ui.Money(due) + "</b><br><span style=\"font-size:13px;color:" + Muted + ";\">" + HowToPay(s) + "</span></div>");
            }
            sb.Append(Note(s));
            string to = Ui.Email(who.Email.Trim()) ? who.Email.Trim() : string.Empty;
            return (to, Ui.Clip("Your statement from " + name, 200), Frame(s, sb.ToString()), files);
        }

        private static async Task MailLog(Ctx c, string kind, string clientId, string invoiceId, string visitId, string to, string subject, string result)
        {
            await Helper.PutData(new List<string> { "INSERT INTO MAILLOG (MAIL_ID, MAIL_KIND, CLIENT_ID, INVOICE_ID, VISIT_ID, TO_ADDR, SUBJECT, SENT_UTC, RESULT, STAFF_ID) VALUES (@mid, @mk, @cid, @iid, @vid, @to, @sub, SYSUTCDATETIME(), @res, @me)" }, new List<SqlParameter>
            {
                App.P("mid", App.NewId(), 50), App.P("mk", kind, 20), App.P("cid", clientId, 50), App.P("iid", invoiceId, 50), App.P("vid", visitId, 50),
                App.P("to", Ui.Clip(to, 200), 200), App.P("sub", Ui.Clip(subject, 200), 200), App.P("res", Ui.OneLine(result, 300), 300), App.P("me", c.U.StaffId, 50)
            });
        }

        public static async Task<string> Send(Ctx c, string kind, string clientId, string invoiceId, string visitId, string to, string subject, string html, List<string> files)
        {
            string k = Kinds.Contains(kind) ? kind : "invoice";
            string addr = (to ?? string.Empty).Trim();
            string subj = Ui.OneLine(subject, 200);
            string cid = clientId ?? string.Empty;
            string iid = invoiceId ?? string.Empty;
            string vid = visitId ?? string.Empty;
            if (!Ui.Email(addr))
            {
                await MailLog(c, k, cid, iid, vid, addr, subj, "No valid email address");
                return "There is no valid email address for this owner.";
            }
            if (string.IsNullOrWhiteSpace(html))
            {
                await MailLog(c, k, cid, iid, vid, addr, subj, "Nothing to send");
                return "There is nothing to send.";
            }
            if (App.Demo)
            {
                await MailLog(c, k, cid, iid, vid, addr, subj, "Demo: not sent");
                await App.Log(c.U.StaffId, "email " + k, addr + " · demo, not sent", true);
                return string.Empty;
            }
            if (!MailReady())
            {
                await MailLog(c, k, cid, iid, vid, addr, subj, "Email is not set up");
                return "Email is not set up yet. Add the mail server, address and password in application.cfg first.";
            }
            Dictionary<string, string> s = await App.Settings();
            string err;
            try
            {
                Mail m = new Mail { ToAddr = new[] { addr }, Subject = subj, Body = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body style=\"margin:0;padding:0;\">" + html + "</body></html>", Display = App.S(s, "name", "Animal Hospital") };
                string root = Path.GetFullPath(App.VaultDir) + Path.DirectorySeparatorChar;
                foreach (string f in files ?? new List<string>())
                {
                    string full = Path.GetFullPath(f);
                    if (full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full))
                    {
                        var (ft, ferr) = await Helper.GetDataTable("SELECT TOP 1 FILE_NAME, MIME FROM VISITFILE WHERE STORED_NAME = @sn", new List<SqlParameter> { App.P("sn", Path.GetFileName(full), 100) });
                        if (ferr == string.Empty && ft.Rows.Count > 0)
                        {
                            m.AddFile(Convert.ToString(ft.Rows[0][0]) ?? Path.GetFileName(full), await File.ReadAllBytesAsync(full), Convert.ToString(ft.Rows[0][1]) ?? "application/octet-stream");
                        }
                        else
                        {
                            m.AddFile(full);
                        }
                    }
                }
                err = m.SendMail() ?? string.Empty;
            }
            catch (Exception ex)
            {
                err = ex.Message;
            }
            await MailLog(c, k, cid, iid, vid, addr, subj, err == string.Empty ? "Sent" : err);
            await App.Log(c.U.StaffId, "email " + k, addr + (err == string.Empty ? " · sent" : " · failed"), err == string.Empty);
            return err == string.Empty ? string.Empty : "The email could not be sent: " + Ui.OneLine(err, 160);
        }
    }
}
