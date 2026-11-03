using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using AnimalHospital.codes;
using AnimalHospital.Models;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public static class Billing
    {
        public static readonly string[] Methods = { "card", "cash", "check", "other" };
        public static readonly string[] States = { "open", "paid", "partial", "billed", "void" };

        public const string InvCols = "I.INVOICE_ID AS InvoiceId, I.INVOICE_NO AS InvoiceNo, I.VISIT_ID AS VisitId, I.CLIENT_ID AS ClientId, I.STATUS AS Status, I.SUBTOTAL AS Subtotal, I.DISCOUNT AS Discount, I.DISCOUNT_NOTE AS DiscountNote, I.TAX AS Tax, I.TOTAL AS Total, I.PAID AS Paid, I.BALANCE AS Balance, I.DUE_DATE AS DueDate, I.BILLED_UTC AS BilledUtc, I.CLOSED_UTC AS ClosedUtc, I.VOID_REASON AS VoidReason, I.NOTE AS Note, I.CREATED_BY AS CreatedBy, I.CREATED_UTC AS CreatedUtc, " +
            "ISNULL(C.FIRST_NAME, N'') AS FirstName, ISNULL(C.LAST_NAME, N'') AS LastName, ISNULL(C.EMAIL, N'') AS Email, ISNULL(C.PHONE, N'') AS Phone, ISNULL(C.ADDRESS1, N'') AS Address1, ISNULL(C.CITY, N'') AS City, ISNULL(C.STATE, N'') AS State, ISNULL(C.ZIP, N'') AS Zip, " +
            "ISNULL(P.PET_ID, N'') AS PetId, ISNULL(P.PET_NAME, N'') AS PetName, ISNULL(P.SIZE, N'') AS PetSize, ISNULL(V.VISIT_NO, N'') AS VisitNo, ISNULL(V.VISIT_TYPE, N'') AS VisitType, ISNULL(V.STATUS, N'') AS VisitStatus, V.CHECKIN_AT AS CheckinAt, ISNULL(S.STAFF_NAME, N'') AS StaffName, ISNULL(V.STAFF_ID, N'') AS VisitStaffId";

        public const string InvFrom = " FROM INVOICE I LEFT JOIN CLIENT C ON C.CLIENT_ID = I.CLIENT_ID LEFT JOIN VISIT V ON V.VISIT_ID = I.VISIT_ID LEFT JOIN PET P ON P.PET_ID = V.PET_ID LEFT JOIN STAFF S ON S.STAFF_ID = V.STAFF_ID";

        public const string PayCols = "Y.PAY_ID AS PayId, Y.PAY_NO AS PayNo, Y.INVOICE_ID AS InvoiceId, Y.CLIENT_ID AS ClientId, Y.METHOD AS Method, Y.AMOUNT AS Amount, Y.REFERENCE AS Reference, Y.PAID_UTC AS PaidUtc, Y.STAFF_ID AS StaffId, ISNULL(T.STAFF_NAME, N'') AS StaffName, Y.IS_VOID AS IsVoid, Y.VOID_REASON AS VoidReason";

        private const string Changed = "IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";
        private const string HoldOpen = "UPDATE INVOICE SET SYSDTE = SYSDTE WHERE INVOICE_ID = @iid AND STATUS = N'open'; IF @@ROWCOUNT = 0 THROW 50002, N'STATECHANGED', 1;";

        private const string SyncInsert = "INSERT INTO INVOICELINE (ILINE_ID, INVOICE_ID, LINE_ID, ITEM_ID, ITEM_TYPE, DESCRIPTION, QTY, UNIT_PRICE, TAXABLE, AMOUNT, LINE_ORDER) SELECT LOWER(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N'')), @iid, O.LINE_ID, O.ITEM_ID, O.ITEM_TYPE, LEFT(O.ITEM_NAME, 200), O.QTY, O.UNIT_PRICE, O.TAXABLE, ROUND(O.QTY * O.UNIT_PRICE, 2), O.LINE_ORDER FROM ORDERLINE O WHERE O.VISIT_ID = @vid AND O.STATUS <> N'cancelled' AND NOT EXISTS (SELECT 1 FROM INVOICELINE L WHERE L.INVOICE_ID = @iid AND L.LINE_ID = O.LINE_ID)";

        private const string RecalcSql = "DECLARE @rst NVARCHAR(20), @rnote NVARCHAR(120), @rpct DECIMAL(9,4), @rsub DECIMAL(12,2), @rtxb DECIMAL(12,2), @rdisc DECIMAL(12,2), @reff DECIMAL(12,2), @rtax DECIMAL(12,2), @rpaid DECIMAL(12,2), @rtot DECIMAL(12,2); " +
            "SELECT @rsub = ISNULL(SUM(AMOUNT), 0), @rtxb = ISNULL(SUM(CASE WHEN TAXABLE = 1 THEN AMOUNT ELSE 0 END), 0) FROM INVOICELINE WHERE INVOICE_ID = @iid; " +
            "SELECT @rst = STATUS, @rdisc = DISCOUNT, @rnote = DISCOUNT_NOTE, @rtax = TAX FROM INVOICE WHERE INVOICE_ID = @iid; " +
            "SET @rpct = CASE WHEN @rnote LIKE N'pct:%|%' THEN TRY_CAST(SUBSTRING(@rnote, 5, CHARINDEX(N'|', @rnote) - 5) AS DECIMAL(9,4)) ELSE NULL END; " +
            "IF @rst = N'open' AND @rpct IS NOT NULL AND @rpct >= 0 AND @rpct <= 100 SET @rdisc = ROUND(@rsub * @rpct / 100, 2); " +
            "SET @rdisc = ISNULL(@rdisc, 0); IF @rdisc < 0 SET @rdisc = 0; " +
            "SET @reff = CASE WHEN @rdisc > @rsub THEN @rsub ELSE @rdisc END; " +
            "IF @rst = N'open' SET @rtax = CASE WHEN @rsub > 0 AND @rcrate > 0 THEN ROUND((@rtxb - @reff * @rtxb / @rsub) * @rcrate / 100, 2) ELSE 0 END; " +
            "SET @rtax = ISNULL(@rtax, 0); " +
            "SELECT @rpaid = ISNULL(SUM(AMOUNT), 0) FROM PAYMENT WHERE INVOICE_ID = @iid AND IS_VOID = 0; " +
            "SET @rtot = @rsub - @reff + @rtax; " +
            "UPDATE INVOICE SET SUBTOTAL = @rsub, DISCOUNT = @rdisc, TAX = @rtax, TOTAL = @rtot, PAID = @rpaid, " +
            "BALANCE = CASE WHEN STATUS = N'void' THEN 0 ELSE @rtot - @rpaid END, " +
            "STATUS = CASE WHEN STATUS IN (N'open', N'void') THEN STATUS WHEN @rtot - @rpaid <= 0 THEN N'paid' WHEN @rpaid > 0 THEN N'partial' ELSE N'billed' END, " +
            "DUE_DATE = CASE WHEN STATUS IN (N'open', N'void') OR @rtot - @rpaid <= 0 THEN DUE_DATE ELSE ISNULL(DUE_DATE, @rcdue) END, " +
            "BILLED_UTC = CASE WHEN STATUS IN (N'open', N'void') OR @rtot - @rpaid <= 0 THEN BILLED_UTC ELSE ISNULL(BILLED_UTC, SYSUTCDATETIME()) END, " +
            "CLOSED_UTC = CASE WHEN STATUS IN (N'open', N'void') THEN CLOSED_UTC WHEN @rtot - @rpaid <= 0 THEN ISNULL(CLOSED_UTC, SYSUTCDATETIME()) ELSE NULL END, " +
            "SYSDTE = GETDATE() WHERE INVOICE_ID = @iid";

        private static string SyncSql(bool strict)
        {
            return "DECLARE @sc INT = 0; " +
                "DELETE FROM INVOICELINE WHERE INVOICE_ID = @iid AND LINE_ID <> N'' AND LINE_ID NOT IN (SELECT O.LINE_ID FROM ORDERLINE O WHERE O.VISIT_ID = @vid AND O.STATUS <> N'cancelled'); SET @sc = @sc + @@ROWCOUNT; " +
                "UPDATE L SET DESCRIPTION = LEFT(O.ITEM_NAME, 200), ITEM_ID = O.ITEM_ID, ITEM_TYPE = O.ITEM_TYPE, QTY = O.QTY, UNIT_PRICE = O.UNIT_PRICE, TAXABLE = O.TAXABLE, AMOUNT = ROUND(O.QTY * O.UNIT_PRICE, 2), LINE_ORDER = O.LINE_ORDER FROM INVOICELINE L JOIN ORDERLINE O ON O.LINE_ID = L.LINE_ID WHERE L.INVOICE_ID = @iid AND L.LINE_ID <> N'' AND L.QTY > 0 AND O.VISIT_ID = @vid AND (L.DESCRIPTION <> LEFT(O.ITEM_NAME, 200) OR L.ITEM_ID <> O.ITEM_ID OR L.ITEM_TYPE <> O.ITEM_TYPE OR L.QTY <> O.QTY OR L.UNIT_PRICE <> O.UNIT_PRICE OR L.TAXABLE <> O.TAXABLE OR L.AMOUNT <> ROUND(O.QTY * O.UNIT_PRICE, 2)); SET @sc = @sc + @@ROWCOUNT; " +
                SyncInsert + "; SET @sc = @sc + @@ROWCOUNT;" +
                (strict ? " IF @sc > 0 THROW 50002, N'STATECHANGED LINESCHANGED', 1;" : string.Empty);
        }

        public static decimal TaxRate(Dictionary<string, string> s)
        {
            string v = App.S(s, "tax.rate").Trim().TrimEnd('%').Trim();
            return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal r) ? Math.Clamp(Math.Round(r, 4), 0m, 30m) : 0m;
        }

        public static int DueDays(Dictionary<string, string> s)
        {
            return Math.Clamp(App.SInt(s, "invoice.due.days", 30), 0, 365);
        }

        public static bool IsPercent(string? discountNote)
        {
            return Regex.IsMatch(discountNote ?? string.Empty, "^pct:\\d{1,3}(\\.\\d{1,2})?\\|");
        }

        public static decimal PercentOf(string? discountNote)
        {
            Match m = Regex.Match(discountNote ?? string.Empty, "^pct:(\\d{1,3}(?:\\.\\d{1,2})?)\\|");
            return m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal p) ? Math.Clamp(p, 0m, 100m) : 0m;
        }

        public static string DiscountReason(string? discountNote)
        {
            string n = discountNote ?? string.Empty;
            if (IsPercent(n) || n.StartsWith("amt|", StringComparison.Ordinal))
            {
                return n.Substring(n.IndexOf('|') + 1);
            }
            return n;
        }

        public static string DiscountLabel(string? discountNote)
        {
            string reason = DiscountReason(discountNote);
            if (!IsPercent(discountNote))
            {
                return reason;
            }
            string pct = PercentOf(discountNote).ToString("0.##", CultureInfo.InvariantCulture) + "% off";
            return reason != string.Empty ? pct + " · " + reason : pct;
        }

        public static decimal DiscountApplied(BlInvoice inv)
        {
            return Math.Max(0m, Math.Min(inv.Discount, inv.Subtotal));
        }

        public static string StatusName(string status)
        {
            return status switch
            {
                "open" => "Being prepared",
                "paid" => "Paid",
                "partial" => "Part paid",
                "billed" => "Billed",
                "void" => "Void",
                _ => status
            };
        }

        public static string MethodName(string method)
        {
            return method switch
            {
                "card" => "Card",
                "cash" => "Cash",
                "check" => "Check",
                "other" => "Other",
                _ => method
            };
        }

        public static async Task<BlInvoice?> Get(string invoiceId)
        {
            var (row, emsg) = await Helper.GetOneT<BlInvoice>("SELECT " + InvCols + InvFrom + " WHERE I.INVOICE_ID = @iid", new List<SqlParameter> { App.P("iid", invoiceId, 50) });
            return emsg == string.Empty ? row : null;
        }

        public static async Task<BlInvoice?> ForVisit(string visitId)
        {
            var (row, emsg) = await Helper.GetOneT<BlInvoice>("SELECT TOP 1 " + InvCols + InvFrom + " WHERE I.VISIT_ID = @vid AND I.VISIT_ID <> N'' AND I.STATUS <> N'void' ORDER BY I.CREATED_UTC DESC", new List<SqlParameter> { App.P("vid", visitId, 50) });
            return emsg == string.Empty ? row : null;
        }

        public static async Task<List<BlLine>> Lines(string invoiceId, bool withRemoved = false)
        {
            var (list, emsg) = await Helper.GetListT<BlLine>("SELECT L.ILINE_ID AS IlineId, L.INVOICE_ID AS InvoiceId, L.LINE_ID AS LineId, L.ITEM_ID AS ItemId, L.ITEM_TYPE AS ItemType, L.DESCRIPTION AS Description, L.QTY AS Qty, L.UNIT_PRICE AS UnitPrice, L.TAXABLE AS Taxable, L.AMOUNT AS Amount, L.LINE_ORDER AS LineOrder, ISNULL(O.UNIT, ISNULL(IT.UNIT, N'')) AS Unit, ISNULL(O.QTY, 0) AS OrderQty FROM INVOICELINE L LEFT JOIN ORDERLINE O ON O.LINE_ID = L.LINE_ID AND L.LINE_ID <> N'' LEFT JOIN ITEM IT ON IT.ITEM_ID = L.ITEM_ID WHERE L.INVOICE_ID = @iid" + (withRemoved ? string.Empty : " AND L.QTY > 0") + " ORDER BY L.LINE_ORDER, L.DESCRIPTION, L.ILINE_ID", new List<SqlParameter> { App.P("iid", invoiceId, 50) });
            return emsg == string.Empty && list != null ? list : new List<BlLine>();
        }

        public static async Task<List<BlPay>> Payments(string invoiceId)
        {
            var (list, emsg) = await Helper.GetListT<BlPay>("SELECT " + PayCols + " FROM PAYMENT Y LEFT JOIN STAFF T ON T.STAFF_ID = Y.STAFF_ID WHERE Y.INVOICE_ID = @iid ORDER BY Y.PAID_UTC, Y.PAY_NO", new List<SqlParameter> { App.P("iid", invoiceId, 50) });
            return emsg == string.Empty && list != null ? list : new List<BlPay>();
        }

        private static void RecalcPs(List<SqlParameter> ps, BlInvoice inv, Dictionary<string, string> s)
        {
            ps.Add(new SqlParameter("rcrate", SqlDbType.Decimal) { Precision = 9, Scale = 4, Value = TaxRate(s) });
            ps.Add(App.PDate("rcdue", App.Now(s).Date.AddDays(DueDays(s))));
        }

        private static string Why(string result)
        {
            if (result.Contains("LINESCHANGED", StringComparison.OrdinalIgnoreCase))
            {
                return "The charges on this visit just changed. Check the new total, then finish again.";
            }
            if (result.Contains("STATECHANGED", StringComparison.OrdinalIgnoreCase))
            {
                return "Someone else just changed this invoice. It is refreshed, check it and try again.";
            }
            if (result.Contains("OVERPAID", StringComparison.OrdinalIgnoreCase))
            {
                return "The payments are more than the total. Void a payment or change the lines first.";
            }
            if (result.Contains("UNPAID", StringComparison.OrdinalIgnoreCase))
            {
                return "There is still money to pay. Take a payment, or choose Bill the owner.";
            }
            if (result.Contains("HASPAY", StringComparison.OrdinalIgnoreCase))
            {
                return "This invoice has payments. Void the payments first.";
            }
            if (result.Contains("VISITMOVED", StringComparison.OrdinalIgnoreCase))
            {
                return "This visit is no longer waiting at checkout.";
            }
            return "This could not be saved. Try again.";
        }

        private static string Key(string? key)
        {
            string k = (key ?? string.Empty).Trim();
            return Regex.IsMatch(k, "^[a-f0-9]{32}$") ? k : string.Empty;
        }

        public static async Task<(string InvoiceId, string Error)> Open(Ctx c, string visitId, Dictionary<string, string> s)
        {
            var (vt, verr) = await Helper.GetDataTable("SELECT V.STATUS, V.CLIENT_ID, ISNULL(P.PET_NAME, N''), V.VISIT_NO FROM VISIT V LEFT JOIN PET P ON P.PET_ID = V.PET_ID WHERE V.VISIT_ID = @vid", new List<SqlParameter> { App.P("vid", visitId, 50) });
            if (verr != string.Empty || vt.Rows.Count == 0)
            {
                return (string.Empty, "This visit was not found.");
            }
            string vstatus = Convert.ToString(vt.Rows[0][0]) ?? string.Empty;
            string clientId = Convert.ToString(vt.Rows[0][1]) ?? string.Empty;
            string petName = Convert.ToString(vt.Rows[0][2]) ?? string.Empty;
            BlInvoice? inv = await ForVisit(visitId);
            if (inv != null && inv.Status != "open")
            {
                return (inv.InvoiceId, string.Empty);
            }
            if (inv != null)
            {
                if (vstatus != "checkout")
                {
                    return (inv.InvoiceId, string.Empty);
                }
                List<SqlParameter> sp = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("vid", visitId, 50) };
                RecalcPs(sp, inv, s);
                string sr = await Helper.PutData(new List<string>
                {
                    HoldOpen,
                    SyncSql(false),
                    RecalcSql
                }, sp);
                if (Helper.IsError(sr))
                {
                    BlInvoice? again = await Get(inv.InvoiceId);
                    if (again != null && again.Status != "open")
                    {
                        return (again.InvoiceId, string.Empty);
                    }
                    return (string.Empty, Why(sr));
                }
                return (inv.InvoiceId, string.Empty);
            }
            if (vstatus != "checkout")
            {
                return (string.Empty, vstatus == "closed" ? "This visit is closed and has no invoice." : vstatus == "cancelled" ? "This visit was cancelled." : "This visit is not ready for checkout yet.");
            }
            string no = await Flow.NextNo("INV", "INV");
            if (no == string.Empty)
            {
                return (string.Empty, "An invoice number could not be made. Check the database.");
            }
            string iid = App.NewId();
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", iid, 50), App.P("ino", no, 20), App.P("vid", visitId, 50), App.P("cid", clientId, 50), App.P("me", c.U.StaffId, 50) };
            RecalcPs(ps, new BlInvoice(), s);
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE VISIT SET SYSDTE = SYSDTE WHERE VISIT_ID = @vid AND STATUS = N'checkout'; IF @@ROWCOUNT = 0 THROW 50006, N'VISITMOVED', 1;",
                "IF EXISTS (SELECT 1 FROM INVOICE WHERE VISIT_ID = @vid AND STATUS <> N'void') THROW 50005, N'HASINVOICE', 1;",
                "INSERT INTO INVOICE (INVOICE_ID, INVOICE_NO, VISIT_ID, CLIENT_ID, STATUS, CREATED_BY, CREATED_UTC) VALUES (@iid, @ino, @vid, @cid, N'open', @me, SYSUTCDATETIME())",
                SyncSql(false),
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                BlInvoice? other = r.Contains("HASINVOICE", StringComparison.OrdinalIgnoreCase) ? await ForVisit(visitId) : null;
                if (other != null)
                {
                    return (other.InvoiceId, string.Empty);
                }
                return (string.Empty, Why(r));
            }
            await App.Log(c.U.StaffId, "open invoice", no + " · " + petName, true);
            return (iid, string.Empty);
        }

        public static async Task<string> Recalc(string invoiceId, Dictionary<string, string> s)
        {
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return "This invoice was not found.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", invoiceId, 50) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string> { RecalcSql }, ps);
            return Helper.IsError(r) ? Why(r) : string.Empty;
        }

        public static string CleanReference(string method, string? reference, out string error)
        {
            error = string.Empty;
            string r = Ui.OneLine(reference, 60);
            string digits = Ui.Digits(r);
            if (method == "card")
            {
                if (digits.Length >= 9)
                {
                    error = "Write only the last 4 digits of the card, never the full number.";
                    return string.Empty;
                }
                if (Regex.IsMatch(r, "^[0-9]{4}$"))
                {
                    return "**** " + r;
                }
            }
            return r;
        }

        public static async Task<(string PayId, string Error)> AddPayment(Ctx c, string invoiceId, string method, decimal amount, string reference, Dictionary<string, string> s, string key = "", bool allowOpen = false)
        {
            string m = (method ?? string.Empty).Trim().ToLowerInvariant();
            if (!Methods.Contains(m))
            {
                return (string.Empty, "Pick how the owner pays.");
            }
            decimal amt = Math.Round(amount, 2);
            if (amt <= 0)
            {
                return (string.Empty, "Enter an amount more than $0.00.");
            }
            if (amt > 100000m)
            {
                return (string.Empty, "That amount is too large.");
            }
            string refText = CleanReference(m, reference, out string rerr);
            if (rerr != string.Empty)
            {
                return (string.Empty, rerr);
            }
            string pid = Key(key);
            if (pid != string.Empty)
            {
                var (had, herr) = await Helper.GetDataTable("SELECT COUNT(*) FROM PAYMENT WHERE PAY_ID = @pid", new List<SqlParameter> { App.P("pid", pid, 50) });
                if (herr == string.Empty && Convert.ToInt32(had.Rows[0][0]) > 0)
                {
                    return (pid, string.Empty);
                }
            }
            else
            {
                pid = App.NewId();
            }
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return (string.Empty, "This invoice was not found.");
            }
            if (inv.Status == "void")
            {
                return (string.Empty, "This invoice is void. Payments can't be added.");
            }
            if (inv.Status == "open" && !allowOpen)
            {
                return (string.Empty, "This invoice is still being prepared at checkout. Take the payment there.");
            }
            if (inv.Balance <= 0)
            {
                return (string.Empty, "Nothing is owed on this invoice.");
            }
            if (amt > inv.Balance)
            {
                if (m != "cash")
                {
                    return (string.Empty, "That is more than the balance of " + Ui.Money(inv.Balance).Replace("&minus;", "-") + ". Only cash can be more, and the change is given back.");
                }
                amt = inv.Balance;
            }
            string no = await Flow.NextNo("PAY", "PAY");
            if (no == string.Empty)
            {
                return (string.Empty, "A payment number could not be made. Check the database.");
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("pid", pid, 50), App.P("pno", no, 20), App.P("cid", inv.ClientId, 50), App.P("m", m, 20), App.PDec("amt", amt), App.P("ref", refText, 60), App.P("me", c.U.StaffId, 50), App.PBit("aopen", allowOpen) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE INVOICE SET SYSDTE = SYSDTE WHERE INVOICE_ID = @iid AND STATUS <> N'void' AND (@aopen = 1 OR STATUS <> N'open') AND BALANCE >= @amt; " + Changed,
                "INSERT INTO PAYMENT (PAY_ID, PAY_NO, INVOICE_ID, CLIENT_ID, METHOD, AMOUNT, REFERENCE, PAID_UTC, STAFF_ID, IS_VOID, VOID_REASON) VALUES (@pid, @pno, @iid, @cid, @m, @amt, @ref, SYSUTCDATETIME(), @me, 0, N'')",
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                if (Key(key) != string.Empty)
                {
                    var (dup, derr) = await Helper.GetDataTable("SELECT COUNT(*) FROM PAYMENT WHERE PAY_ID = @pid", new List<SqlParameter> { App.P("pid", pid, 50) });
                    if (derr == string.Empty && Convert.ToInt32(dup.Rows[0][0]) > 0)
                    {
                        return (pid, string.Empty);
                    }
                }
                await App.Log(c.U.StaffId, "take payment", inv.InvoiceNo + " · failed", false);
                return (string.Empty, Why(r));
            }
            await App.Log(c.U.StaffId, "take payment", inv.InvoiceNo + " · " + MethodName(m) + " " + amt.ToString("0.00", CultureInfo.InvariantCulture), true);
            return (pid, string.Empty);
        }

        public static async Task<string> VoidPayment(Ctx c, string payId, string reason, Dictionary<string, string> s)
        {
            string why = Ui.OneLine(reason, 200);
            if (why == string.Empty)
            {
                return "Write why this payment is voided.";
            }
            var (pay, perr) = await Helper.GetOneT<BlPay>("SELECT " + PayCols + " FROM PAYMENT Y LEFT JOIN STAFF T ON T.STAFF_ID = Y.STAFF_ID WHERE Y.PAY_ID = @pid", new List<SqlParameter> { App.P("pid", payId, 50) });
            if (perr != string.Empty || pay == null)
            {
                return "This payment was not found.";
            }
            if (pay.IsVoid)
            {
                return "This payment is already void.";
            }
            BlInvoice? inv = await Get(pay.InvoiceId);
            if (inv == null)
            {
                return "The invoice of this payment was not found.";
            }
            if (inv.Status == "void")
            {
                return "This invoice is void.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("pid", pay.PayId, 50), App.P("why", why, 200) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE INVOICE SET SYSDTE = SYSDTE WHERE INVOICE_ID = @iid AND STATUS <> N'void'; " + Changed,
                "UPDATE PAYMENT SET IS_VOID = 1, VOID_REASON = @why, SYSDTE = GETDATE() WHERE PAY_ID = @pid AND INVOICE_ID = @iid AND IS_VOID = 0; " + Changed,
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                await App.Log(c.U.StaffId, "void payment", pay.PayNo + " · failed", false);
                return Why(r);
            }
            await App.Log(c.U.StaffId, "void payment", pay.PayNo + " · " + inv.InvoiceNo + " · " + why, true);
            return string.Empty;
        }

        public static async Task<string> Finish(Ctx c, string invoiceId, bool billLater, Dictionary<string, string> s)
        {
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return "This invoice was not found.";
            }
            if (inv.Status != "open")
            {
                return "This checkout is already finished.";
            }
            if (inv.VisitId != string.Empty && inv.VisitStatus != "checkout")
            {
                return "This visit is no longer waiting at checkout.";
            }
            DateTime now = App.Now(s);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("vid", inv.VisitId, 50), App.PBit("later", billLater), App.PDate("due", now.Date.AddDays(DueDays(s))), App.PDate("now", now) };
            RecalcPs(ps, inv, s);
            List<string> sql = new List<string> { HoldOpen };
            if (inv.VisitId != string.Empty)
            {
                sql.Add("UPDATE VISIT SET SYSDTE = SYSDTE WHERE VISIT_ID = @vid AND STATUS = N'checkout'; IF @@ROWCOUNT = 0 THROW 50006, N'VISITMOVED', 1;");
                sql.Add(SyncSql(true));
            }
            sql.AddRange(new[]
            {
                RecalcSql,
                "IF EXISTS (SELECT 1 FROM INVOICE WHERE INVOICE_ID = @iid AND BALANCE < 0) THROW 50007, N'OVERPAID', 1;",
                "IF @later = 0 AND EXISTS (SELECT 1 FROM INVOICE WHERE INVOICE_ID = @iid AND BALANCE > 0) THROW 50008, N'UNPAID', 1;",
                "UPDATE INVOICE SET STATUS = CASE WHEN BALANCE <= 0 THEN N'paid' WHEN PAID > 0 THEN N'partial' ELSE N'billed' END, DUE_DATE = CASE WHEN BALANCE > 0 THEN @due ELSE NULL END, BILLED_UTC = CASE WHEN BALANCE > 0 THEN SYSUTCDATETIME() ELSE NULL END, CLOSED_UTC = CASE WHEN BALANCE <= 0 THEN SYSUTCDATETIME() ELSE NULL END, SYSDTE = GETDATE() WHERE INVOICE_ID = @iid AND STATUS = N'open'; " + Changed
            });
            if (inv.VisitId != string.Empty)
            {
                sql.Add("UPDATE VISIT SET STATUS = N'closed', CLOSED_AT = @now, SYSDTE = GETDATE() WHERE VISIT_ID = @vid AND STATUS = N'checkout'; IF @@ROWCOUNT = 0 THROW 50006, N'VISITMOVED', 1;");
                sql.Add("UPDATE VISITROOM SET OUT_AT = @now WHERE VISIT_ID = @vid AND OUT_AT IS NULL");
            }
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                await App.Log(c.U.StaffId, "finish checkout", inv.InvoiceNo + " · failed", false);
                return Why(r);
            }
            BlInvoice? done = await Get(inv.InvoiceId);
            await App.Log(c.U.StaffId, "finish checkout", inv.InvoiceNo + " · " + inv.PetName + " · " + (done != null ? StatusName(done.Status) : string.Empty), true);
            return string.Empty;
        }

        public static async Task<string> VoidInvoice(Ctx c, string invoiceId, string reason, Dictionary<string, string> s)
        {
            string why = Ui.OneLine(reason, 200);
            if (why == string.Empty)
            {
                return "Write why this invoice is voided.";
            }
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return "This invoice was not found.";
            }
            if (inv.Status == "void")
            {
                return "This invoice is already void.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("why", why, 200) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                "UPDATE INVOICE SET SYSDTE = SYSDTE WHERE INVOICE_ID = @iid AND STATUS <> N'void'; " + Changed,
                "IF EXISTS (SELECT 1 FROM PAYMENT WHERE INVOICE_ID = @iid AND IS_VOID = 0) THROW 50009, N'HASPAY', 1;",
                "UPDATE INVOICE SET STATUS = N'void', VOID_REASON = @why, DUE_DATE = NULL, CLOSED_UTC = ISNULL(CLOSED_UTC, SYSUTCDATETIME()), SYSDTE = GETDATE() WHERE INVOICE_ID = @iid",
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                await App.Log(c.U.StaffId, "void invoice", inv.InvoiceNo + " · failed", false);
                return Why(r);
            }
            await App.Log(c.U.StaffId, "void invoice", inv.InvoiceNo + " · " + why, true);
            return string.Empty;
        }

        private static async Task<(BlInvoice? Inv, string Error)> OpenOne(string invoiceId)
        {
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return (null, "This invoice was not found.");
            }
            if (inv.Status != "open")
            {
                return (null, "This invoice is finished. Its lines can't be changed.");
            }
            return (inv, string.Empty);
        }

        public static async Task<string> SetLine(Ctx c, string invoiceId, string ilineId, decimal qty, decimal unitPrice, Dictionary<string, string> s)
        {
            decimal q = Math.Round(qty, 2);
            decimal p = Math.Round(unitPrice, 2);
            if (q <= 0 || q > 9999)
            {
                return "Quantity should be more than 0 and at most 9,999.";
            }
            if (p < 0 || p > 99999)
            {
                return "Unit price should be between $0.00 and $99,999.";
            }
            var (inv, err) = await OpenOne(invoiceId);
            if (inv == null)
            {
                return err;
            }
            BlLine? line = (await Lines(inv.InvoiceId, true)).FirstOrDefault(x => x.IlineId == ilineId);
            if (line == null)
            {
                return "This line was not found. The invoice is refreshed.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("lid", line.IlineId, 50), App.P("oid", line.LineId, 50), App.P("vid", inv.VisitId, 50), App.PDec("q", q), App.PDec("p", p) };
            RecalcPs(ps, inv, s);
            List<string> sql = new List<string>
            {
                HoldOpen,
                "UPDATE INVOICELINE SET QTY = @q, UNIT_PRICE = @p, AMOUNT = ROUND(@q * @p, 2) WHERE ILINE_ID = @lid AND INVOICE_ID = @iid; " + Changed
            };
            if (line.LineId != string.Empty)
            {
                sql.Add("UPDATE ORDERLINE SET QTY = @q, UNIT_PRICE = @p, SYSDTE = GETDATE() WHERE LINE_ID = @oid AND VISIT_ID = @vid AND STATUS <> N'cancelled'");
            }
            sql.Add(RecalcSql);
            string r = await Helper.PutData(sql, ps);
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "change invoice line", inv.InvoiceNo + " · " + line.Description, true);
            return string.Empty;
        }

        public static async Task<string> RemoveLine(Ctx c, string invoiceId, string ilineId, Dictionary<string, string> s)
        {
            var (inv, err) = await OpenOne(invoiceId);
            if (inv == null)
            {
                return err;
            }
            BlLine? line = (await Lines(inv.InvoiceId, true)).FirstOrDefault(x => x.IlineId == ilineId);
            if (line == null)
            {
                return string.Empty;
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("lid", line.IlineId, 50) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                HoldOpen,
                line.LineId == string.Empty ? "DELETE FROM INVOICELINE WHERE ILINE_ID = @lid AND INVOICE_ID = @iid" : "UPDATE INVOICELINE SET QTY = 0, AMOUNT = 0 WHERE ILINE_ID = @lid AND INVOICE_ID = @iid",
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "remove invoice line", inv.InvoiceNo + " · " + line.Description, true);
            return string.Empty;
        }

        public static async Task<string> RestoreLine(Ctx c, string invoiceId, string ilineId, Dictionary<string, string> s)
        {
            var (inv, err) = await OpenOne(invoiceId);
            if (inv == null)
            {
                return err;
            }
            BlLine? line = (await Lines(inv.InvoiceId, true)).FirstOrDefault(x => x.IlineId == ilineId && x.LineId != string.Empty);
            if (line == null)
            {
                return "This line was not found. The invoice is refreshed.";
            }
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("lid", line.IlineId, 50), App.P("vid", inv.VisitId, 50) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                HoldOpen,
                "UPDATE L SET QTY = O.QTY, UNIT_PRICE = O.UNIT_PRICE, TAXABLE = O.TAXABLE, AMOUNT = ROUND(O.QTY * O.UNIT_PRICE, 2) FROM INVOICELINE L JOIN ORDERLINE O ON O.LINE_ID = L.LINE_ID WHERE L.ILINE_ID = @lid AND L.INVOICE_ID = @iid AND O.VISIT_ID = @vid AND O.STATUS <> N'cancelled'; " + Changed,
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "restore invoice line", inv.InvoiceNo + " · " + line.Description, true);
            return string.Empty;
        }

        public static decimal PriceFor(BlItem item, string size)
        {
            if (size == "medium" && item.PriceM > 0)
            {
                return item.PriceM;
            }
            if (size == "large" && item.PriceL > 0)
            {
                return item.PriceL;
            }
            return item.Price;
        }

        public static async Task<List<BlItem>> Catalog()
        {
            var (list, emsg) = await Helper.GetListT<BlItem>("SELECT ITEM_ID AS ItemId, ITEM_NAME AS ItemName, ITEM_TYPE AS ItemType, UNIT AS Unit, PRICE AS Price, PRICE_M AS PriceM, PRICE_L AS PriceL, TAXABLE AS Taxable, SPECIES AS Species FROM ITEM WHERE ACTIVE = 1 AND NOT (ITEM_TYPE = N'boarding' AND UNIT = N'night') ORDER BY CASE ITEM_TYPE WHEN N'practice' THEN 1 WHEN N'medicine' THEN 2 WHEN N'test' THEN 3 WHEN N'vaccine' THEN 4 WHEN N'grooming' THEN 5 WHEN N'product' THEN 6 WHEN N'boarding' THEN 7 ELSE 8 END, ITEM_ORDER, ITEM_NAME");
            return emsg == string.Empty && list != null ? list : new List<BlItem>();
        }

        public static async Task<string> AddLine(Ctx c, string invoiceId, string newLineId, string itemId, decimal qty, Dictionary<string, string> s)
        {
            string lid = Key(newLineId);
            if (lid == string.Empty)
            {
                return "This form is out of date. Close it and try again.";
            }
            decimal q = Math.Round(qty, 2);
            if (q <= 0 || q > 9999)
            {
                return "Quantity should be more than 0 and at most 9,999.";
            }
            var (had, herr) = await Helper.GetDataTable("SELECT COUNT(*) FROM INVOICELINE WHERE ILINE_ID = @lid", new List<SqlParameter> { App.P("lid", lid, 50) });
            if (herr == string.Empty && Convert.ToInt32(had.Rows[0][0]) > 0)
            {
                return string.Empty;
            }
            var (inv, err) = await OpenOne(invoiceId);
            if (inv == null)
            {
                return err;
            }
            BlItem? item = (await Catalog()).FirstOrDefault(x => x.ItemId == itemId);
            if (item == null)
            {
                return "Pick an item from the list.";
            }
            decimal price = PriceFor(item, inv.PetSize);
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("lid", lid, 50), App.P("it", item.ItemId, 50), App.P("tp", item.ItemType, 20), App.P("ds", Ui.Clip(item.ItemName, 200), 200), App.PDec("q", q), App.PDec("p", price), App.PBit("tx", item.Taxable) };
            RecalcPs(ps, inv, s);
            string r = await Helper.PutData(new List<string>
            {
                HoldOpen,
                "INSERT INTO INVOICELINE (ILINE_ID, INVOICE_ID, LINE_ID, ITEM_ID, ITEM_TYPE, DESCRIPTION, QTY, UNIT_PRICE, TAXABLE, AMOUNT, LINE_ORDER) SELECT @lid, @iid, N'', @it, @tp, @ds, @q, @p, @tx, ROUND(@q * @p, 2), CASE WHEN ISNULL(MAX(LINE_ORDER), 0) < 1000 THEN 1000 ELSE MAX(LINE_ORDER) + 1 END FROM INVOICELINE WHERE INVOICE_ID = @iid",
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "add invoice line", inv.InvoiceNo + " · " + item.ItemName, true);
            return string.Empty;
        }

        public static async Task<string> SetDiscount(Ctx c, string invoiceId, bool percent, decimal value, string note, Dictionary<string, string> s)
        {
            decimal v = Math.Round(value, 2);
            string text = Ui.OneLine(note, 90);
            var (inv, err) = await OpenOne(invoiceId);
            if (inv == null)
            {
                return err;
            }
            if (v < 0)
            {
                return "The discount can't be below 0.";
            }
            if (percent && v > 100)
            {
                return "A discount can be at most 100%.";
            }
            if (!percent && v > inv.Subtotal)
            {
                return "The discount can't be more than the subtotal of " + Ui.Money(inv.Subtotal) + ".";
            }
            if (v > 0 && text == string.Empty)
            {
                return "Write a short reason for the discount.";
            }
            string stored = v == 0 ? string.Empty : Ui.Clip((percent ? "pct:" + v.ToString("0.##", CultureInfo.InvariantCulture) + "|" : "amt|") + text, 120);
            decimal amount = v == 0 ? 0 : percent ? Math.Round(inv.Subtotal * v / 100m, 2) : v;
            BlInvoice probe = new BlInvoice { DiscountNote = stored };
            List<SqlParameter> ps = new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.PDec("d", amount), App.P("dn", stored, 120) };
            RecalcPs(ps, probe, s);
            string r = await Helper.PutData(new List<string>
            {
                HoldOpen,
                "UPDATE INVOICE SET DISCOUNT = @d, DISCOUNT_NOTE = @dn WHERE INVOICE_ID = @iid",
                RecalcSql
            }, ps);
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "invoice discount", inv.InvoiceNo + " · " + (v == 0 ? "removed" : DiscountLabel(stored)), true);
            return string.Empty;
        }

        public static async Task<string> SetNote(Ctx c, string invoiceId, string note)
        {
            string text = Ui.Clip(note, 600);
            BlInvoice? inv = await Get(invoiceId);
            if (inv == null)
            {
                return "This invoice was not found.";
            }
            string r = await Helper.PutData(new List<string> { "UPDATE INVOICE SET NOTE = @nt, SYSDTE = GETDATE() WHERE INVOICE_ID = @iid; " + Changed }, new List<SqlParameter> { App.P("iid", inv.InvoiceId, 50), App.P("nt", text, 600) });
            if (Helper.IsError(r))
            {
                return Why(r);
            }
            await App.Log(c.U.StaffId, "invoice note", inv.InvoiceNo, true);
            return string.Empty;
        }
    }
}
