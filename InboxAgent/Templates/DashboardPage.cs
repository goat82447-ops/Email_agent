using System.Net;
using System.Text;
using InboxAgent.Services;

namespace InboxAgent.Templates;

/// <summary>Renders the InboxAgent dashboard HTML page.</summary>
internal static class DashboardPage
{
    public static string Render(DigestSnapshot? snapshot)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<meta http-equiv=\"refresh\" content=\"300\">");
        sb.Append("<title>Inbox Agent — Interview Summaries</title>");
        sb.Append("<link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.Append("<link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        AppendStyles(sb);
        sb.Append("</head><body>");

        // Header banner
        sb.Append("<header class=\"hero\"><div class=\"hero-inner\">");
        sb.Append("<div class=\"brand\"><span class=\"logo\">📬</span><div><div class=\"brand-name\">Inbox Agent</div>");
        sb.Append("<div class=\"brand-tag\">Your interview &amp; placement emails, summarized every morning</div></div></div>");
        sb.Append("</div></header>");

        sb.Append("<main class=\"wrap\">");

        // Info banner
        sb.Append("<div class=\"note\">🔒 Read-only summaries — the only action that touches Gmail is <b>Delete</b>, which moves that email to your Gmail Trash (recoverable for 30 days).</div>");

        if (snapshot is null)
        {
            sb.Append("<div class=\"toolbar\">");
            sb.Append("<form method=\"post\" action=\"/refresh\"><button class=\"btn btn-primary\" type=\"submit\" onclick=\"beep()\">↻ Check inbox now</button></form>");
            sb.Append("</div>");
            sb.Append("<div class=\"empty\"><div class=\"empty-emoji\">📭</div><h3>No scan yet</h3><p>Click <b>Check inbox now</b> to fetch and summarize your latest emails.</p></div>");
            return Close(sb, 0);
        }

        // Stats
        sb.Append("<section class=\"stats\">");
        sb.Append($"<div class=\"stat stat-a\"><span class=\"stat-num\">{snapshot.InterviewEmails.Count}</span><span class=\"stat-label\">Interview</span></div>");
        sb.Append($"<div class=\"stat stat-b\"><span class=\"stat-num\">{snapshot.IgnoredCount}</span><span class=\"stat-label\">Ignored</span></div>");
        sb.Append($"<div class=\"stat stat-c\"><span class=\"stat-num\">{snapshot.TotalScanned}</span><span class=\"stat-label\">Scanned</span></div>");
        sb.Append("</section>");

        // Toolbar
        sb.Append("<div class=\"toolbar\">");
        sb.Append("<form method=\"post\" action=\"/refresh\"><button class=\"btn btn-primary\" type=\"submit\" onclick=\"beep()\">↻ Check inbox now</button></form>");
        sb.Append("<button class=\"btn btn-ghost\" type=\"button\" onclick=\"beep()\">🔔 Test sound</button>");
        sb.Append($"<span class=\"updated\">Updated {WebUtility.HtmlEncode(snapshot.GeneratedAt.ToString("ddd, dd MMM · HH:mm"))}</span>");
        sb.Append("</div>");

        if (snapshot.InterviewEmails.Count == 0)
        {
            sb.Append("<div class=\"empty\"><div class=\"empty-emoji\">🎉</div><h3>All clear</h3><p>No interview or placement emails in the latest scan.</p></div>");
            return Close(sb, 0);
        }

        // Email cards
        sb.Append("<section class=\"cards\">");
        foreach (var item in snapshot.InterviewEmails)
        {
            var initial = string.IsNullOrWhiteSpace(item.Email.FromName)
                ? "?"
                : char.ToUpperInvariant(item.Email.FromName.TrimStart()[0]).ToString();
            var gmailUrl = "https://mail.google.com/mail/u/0/#search/"
                + WebUtility.UrlEncode(item.Email.Subject);

            sb.Append("<article class=\"card\">");
            sb.Append("<div class=\"card-head\">");
            sb.Append($"<div class=\"avatar\">{WebUtility.HtmlEncode(initial)}</div>");
            sb.Append("<div class=\"card-headtext\">");
            sb.Append($"<h3 class=\"card-title\">{WebUtility.HtmlEncode(item.Email.Subject)}</h3>");
            sb.Append($"<div class=\"card-from\">{WebUtility.HtmlEncode(item.Email.FromName)} &lt;{WebUtility.HtmlEncode(item.Email.FromAddress)}&gt;</div>");
            sb.Append("</div>");
            sb.Append($"<span class=\"pill\">{item.Email.ReceivedLocal:ddd dd MMM · HH:mm}</span>");
            sb.Append("</div>");
            sb.Append($"<p class=\"card-summary\">{WebUtility.HtmlEncode(item.Summary)}</p>");
            sb.Append("<div class=\"card-actions\">");
            sb.Append($"<a class=\"link\" href=\"{WebUtility.HtmlEncode(gmailUrl)}\" target=\"_blank\" rel=\"noopener\">Open in Gmail ↗</a>");
            if (item.Email.GmailMessageId != 0)
            {
                var subjectJs = WebUtility.HtmlEncode(item.Email.Subject.Replace("'", "\\'"));
                sb.Append("<form method=\"post\" action=\"/delete\" class=\"del-form\" ");
                sb.Append($"onsubmit=\"return confirm('Move this email to Gmail Trash?\\n\\n{subjectJs}');\">");
                sb.Append($"<input type=\"hidden\" name=\"id\" value=\"{item.Email.GmailMessageId}\">");
                sb.Append("<button class=\"btn-del\" type=\"submit\">🗑 Delete</button>");
                sb.Append("</form>");
            }
            sb.Append("</div>");
            sb.Append("</article>");
        }
        sb.Append("</section>");

        return Close(sb, snapshot.InterviewEmails.Count);
    }

    private static void AppendStyles(StringBuilder sb)
    {
        sb.Append("<style>");
        sb.Append("*{box-sizing:border-box;}");
        sb.Append("body{font-family:'Inter',Segoe UI,Arial,sans-serif;background:#f1f5f9;color:#0f172a;margin:0;}");
        sb.Append(".hero{background:linear-gradient(120deg,#4f46e5,#7c3aed);color:#fff;padding:28px 24px;}");
        sb.Append(".hero-inner{max-width:860px;margin:auto;}");
        sb.Append(".brand{display:flex;align-items:center;gap:14px;}");
        sb.Append(".logo{font-size:34px;line-height:1;}");
        sb.Append(".brand-name{font-size:22px;font-weight:800;letter-spacing:-.3px;}");
        sb.Append(".brand-tag{font-size:13px;opacity:.85;margin-top:2px;}");
        sb.Append(".wrap{max-width:860px;margin:-16px auto 40px;padding:0 24px;}");
        sb.Append(".note{background:#ecfdf5;border:1px solid #a7f3d0;color:#065f46;border-radius:12px;padding:10px 14px;font-size:13px;margin:24px 0 18px;}");
        sb.Append(".stats{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-bottom:18px;}");
        sb.Append(".stat{background:#fff;border-radius:14px;padding:16px 18px;box-shadow:0 1px 3px rgba(15,23,42,.06);border-top:3px solid #cbd5e1;display:flex;flex-direction:column;}");
        sb.Append(".stat-a{border-top-color:#4f46e5;}.stat-b{border-top-color:#f59e0b;}.stat-c{border-top-color:#0ea5e9;}");
        sb.Append(".stat-num{font-size:28px;font-weight:800;line-height:1;}");
        sb.Append(".stat-label{font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:.5px;margin-top:6px;font-weight:600;}");
        sb.Append(".toolbar{display:flex;gap:10px;align-items:center;flex-wrap:wrap;margin-bottom:18px;}");
        sb.Append(".btn{border:none;border-radius:10px;padding:10px 16px;font-size:14px;font-weight:600;font-family:inherit;cursor:pointer;text-decoration:none;display:inline-flex;align-items:center;gap:6px;transition:transform .06s ease,background .15s ease;}");
        sb.Append(".btn:active{transform:translateY(1px);}");
        sb.Append(".btn-primary{background:#4f46e5;color:#fff;}.btn-primary:hover{background:#4338ca;}");
        sb.Append(".btn-ghost{background:#fff;color:#4f46e5;border:1px solid #c7d2fe;}.btn-ghost:hover{background:#eef2ff;}");
        sb.Append(".updated{color:#64748b;font-size:13px;margin-left:auto;}");
        sb.Append(".cards{display:flex;flex-direction:column;gap:14px;}");
        sb.Append(".card{background:#fff;border-radius:16px;padding:18px 20px;box-shadow:0 1px 3px rgba(15,23,42,.07);border-left:4px solid #4f46e5;transition:box-shadow .15s ease,transform .1s ease;}");
        sb.Append(".card:hover{box-shadow:0 8px 24px rgba(79,70,229,.12);transform:translateY(-2px);}");
        sb.Append(".card-head{display:flex;align-items:flex-start;gap:12px;}");
        sb.Append(".avatar{width:38px;height:38px;border-radius:50%;background:linear-gradient(135deg,#6366f1,#8b5cf6);color:#fff;font-weight:700;display:flex;align-items:center;justify-content:center;flex:0 0 auto;}");
        sb.Append(".card-headtext{flex:1;min-width:0;}");
        sb.Append(".card-title{margin:0;font-size:16px;font-weight:700;line-height:1.35;}");
        sb.Append(".card-from{color:#64748b;font-size:12.5px;margin-top:2px;overflow:hidden;text-overflow:ellipsis;}");
        sb.Append(".pill{background:#eef2ff;color:#4338ca;border-radius:999px;padding:4px 10px;font-size:11.5px;font-weight:600;white-space:nowrap;flex:0 0 auto;}");
        sb.Append(".card-summary{margin:12px 0 0;font-size:14.5px;line-height:1.6;color:#1e293b;}");
        sb.Append(".card-actions{margin-top:12px;display:flex;align-items:center;gap:14px;}");
        sb.Append(".link{color:#4f46e5;font-size:13px;font-weight:600;text-decoration:none;}.link:hover{text-decoration:underline;}");
        sb.Append(".del-form{margin:0;margin-left:auto;}");
        sb.Append(".btn-del{background:#fff;color:#dc2626;border:1px solid #fecaca;border-radius:8px;padding:6px 12px;font-size:12.5px;font-weight:600;font-family:inherit;cursor:pointer;transition:background .15s ease;}");
        sb.Append(".btn-del:hover{background:#fef2f2;border-color:#fca5a5;}");
        sb.Append(".empty{background:#fff;border-radius:16px;padding:44px 24px;text-align:center;color:#64748b;box-shadow:0 1px 3px rgba(15,23,42,.06);}");
        sb.Append(".empty-emoji{font-size:44px;}.empty h3{margin:10px 0 4px;color:#0f172a;}");
        sb.Append(".foot{color:#94a3b8;font-size:12px;text-align:center;margin-top:28px;}");
        sb.Append("@media(max-width:560px){.stats{grid-template-columns:1fr 1fr;}.updated{margin-left:0;width:100%;}}");
        sb.Append("</style>");
    }

    private static string Close(StringBuilder sb, int interviewCount)
    {
        sb.Append("<p class=\"foot\">Generated automatically by Inbox Agent · page refreshes every 5 minutes.</p>");
        sb.Append("</main>");
        // Sound effect: a short chime that plays when interview emails are present.
        // Browsers may require one interaction before audio is allowed, so any
        // click on the page also unlocks it, and the buttons above call beep().
        sb.Append("<script>");
        sb.Append("function beep(){try{var C=window.AudioContext||window.webkitAudioContext;var c=new C();if(c.state==='suspended'){c.resume();}var o=c.createOscillator();var g=c.createGain();o.connect(g);g.connect(c.destination);o.type='sine';o.frequency.setValueAtTime(880,c.currentTime);o.frequency.setValueAtTime(1175,c.currentTime+0.15);g.gain.setValueAtTime(0.001,c.currentTime);g.gain.exponentialRampToValueAtTime(0.3,c.currentTime+0.03);g.gain.exponentialRampToValueAtTime(0.0001,c.currentTime+0.6);o.start();o.stop(c.currentTime+0.6);}catch(e){}}");
        sb.Append($"var HAS_INTERVIEWS={(interviewCount > 0 ? "true" : "false")};");
        sb.Append("window.addEventListener('load',function(){if(HAS_INTERVIEWS){beep();}});");
        sb.Append("document.addEventListener('click',function(){var C=window.AudioContext||window.webkitAudioContext;try{new C().resume();}catch(e){}},{once:true});");
        sb.Append("</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
