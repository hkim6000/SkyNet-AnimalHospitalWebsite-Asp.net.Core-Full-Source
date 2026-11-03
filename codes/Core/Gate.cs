using System.Data;
using AnimalHospital.codes;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace AnimalHospital.Core
{
    public static class Gate
    {
        private static string? vaultSeg;

        public static async Task Run(HttpContext ctx, Func<Task> next)
        {
            string path = (ctx.Request.Path.Value ?? string.Empty).Replace('\\', '/');
            string low = path.ToLowerInvariant();
            string? mime = Path.GetExtension(low) switch { ".mp4" => "video/mp4", ".webp" => "image/webp", ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".pdf" => "application/pdf", _ => null };
            if (low.StartsWith("/images/", StringComparison.Ordinal) && mime != null && (HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method)))
            {
                string root = Path.GetFullPath(Path.Combine(App.Root, "images")) + Path.DirectorySeparatorChar;
                string file = Path.GetFullPath(Path.Combine(App.Root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
                if (low.Contains("..") || !file.StartsWith(root, StringComparison.Ordinal) || !File.Exists(file))
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                ctx.Response.Headers.CacheControl = "public, max-age=3600";
                ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
                await Results.File(file, mime, enableRangeProcessing: true).ExecuteAsync(ctx);
                return;
            }
            vaultSeg ??= "/" + Path.GetFileName(App.VaultDir.TrimEnd(Path.DirectorySeparatorChar)).ToLowerInvariant();
            string vault = vaultSeg;
            if (low == vault || low.StartsWith(vault + "/", StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            if (low == "/vfile" && HttpMethods.IsGet(ctx.Request.Method))
            {
                await VisitFile(ctx);
                return;
            }
            await next();
        }

        private static async Task VisitFile(HttpContext ctx)
        {
            ctx.Response.Headers.CacheControl = "private, no-store";
            Ctx? c = await Auth.Check(ctx.Request.Cookies["ah_tok"]);
            string id = (ctx.Request.Query["id"].ToString() ?? string.Empty).Trim();
            if (c == null || (!c.Can("visits", 'V') && !c.Can("charts", 'V')) || id.Length == 0 || id.Length > 50)
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            var (dt, emsg) = await Helper.GetDataTable("SELECT F.FILE_NAME, F.STORED_NAME, F.MIME, V.STAFF_ID FROM VISITFILE F JOIN VISIT V ON V.VISIT_ID = F.VISIT_ID WHERE F.FILE_ID = @fid", new List<SqlParameter> { App.P("fid", id, 50) });
            if (emsg != string.Empty || dt.Rows.Count == 0)
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            DataRow r = dt.Rows[0];
            if (!c.Mine(Convert.ToString(r["STAFF_ID"]) ?? string.Empty))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            string stored = Convert.ToString(r["STORED_NAME"]) ?? string.Empty;
            string root = Path.GetFullPath(App.VaultDir) + Path.DirectorySeparatorChar;
            string file = Path.GetFullPath(Path.Combine(App.VaultDir, stored));
            if (stored.Length == 0 || stored.Contains("..") || stored.Contains('/') || stored.Contains('\\') || !file.StartsWith(root, StringComparison.Ordinal) || !File.Exists(file))
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            string mime = Convert.ToString(r["MIME"]) ?? "application/octet-stream";
            bool inline = mime == "application/pdf" || mime == "image/jpeg" || mime == "image/png" || mime == "image/webp";
            string name = Convert.ToString(r["FILE_NAME"]) ?? "file";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers.ContentDisposition = (inline ? "inline" : "attachment") + "; filename*=UTF-8''" + Uri.EscapeDataString(name);
            await Results.File(file, inline ? mime : "application/octet-stream").ExecuteAsync(ctx);
        }
    }
}
