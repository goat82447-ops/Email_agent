# Email_agent

# 📬 Inbox Agent

An automated agent that reads your Gmail every morning, keeps **only the
interview / placement emails**, ignores spam & promotions, writes a short
summary of each, and delivers it three ways:

1. **Email digest** to your inbox.
2. **Web dashboard** with a sound chime and one-click "Delete in Gmail".
3. **Daily scheduler** that does all of the above automatically at a set time.

---

## 1. What it does (in plain words)

| Step | What happens |
|------|--------------|
| 📥 Read | Connects to Gmail over IMAP and fetches emails from the last 24 hours. |
| 🏷️ Classify | Sorts each email into **Interview**, **Spam/Promotion**, or **Other** using keyword rules. Only *Interview* emails are kept. |
| ✍️ Summarize | Writes a 1–2 sentence summary of each interview email (OpenAI if a key + credit is set, otherwise a keyword snippet). |
| 📧 Send | Emails you a clean HTML digest of just the interview emails. |
| 🖥️ Show | A web page lists the same emails with a summary, "Open in Gmail" link, sound chime, and a **Delete** button. |
| ⏰ Schedule | Repeats every morning at the configured time. |

---

## 2. How to run it

From the `InboxAgent` folder:

```powershell
# 1) One scan now, email the digest, then exit (best for testing)
dotnet run --project InboxAgent.csproj -- --run-once

# 2) Start the website (also runs the daily scheduler in the background)
dotnet run --project InboxAgent.csproj -- --web
#    → open http://localhost:5080

# 3) Headless daily scheduler only (no website)
dotnet run --project InboxAgent.csproj
```

| Mode | Flag | Use it for |
|------|------|-----------|
| Run once | `--run-once` | Testing, or Windows Task Scheduler. |
| Website | `--web` | See summaries in the browser + delete emails. |
| Scheduler | *(no flag)* | Runs quietly, emails you every morning. |

---

## 3. What technology it uses

| Piece | Technology |
|-------|-----------|
| Language / runtime | **C# on .NET 8** (console app that can also host a website). |
| Read email | **MailKit** `ImapClient` over SSL (`imap.gmail.com:993`), read-only for scanning. |
| Delete email | **MailKit** `ImapClient` read-write → moves the message to `[Gmail]/Trash`. |
| Send email | **MailKit** `SmtpClient` over STARTTLS (`smtp.gmail.com:587`). |
| Website | **ASP.NET Core minimal APIs** (`WebApplication`) — no separate front-end framework, HTML is built in C#. |
| Summaries (optional) | **OpenAI Chat Completions** (`gpt-4o-mini`) via `HttpClient`; falls back to a keyword snippet if unavailable. |
| Config | `appsettings.json` + `appsettings.Local.json` + environment variables. |
| Scheduling | A background service that sleeps until the next configured run time. |

---

## 4. Project structure

```
InboxAgent/
├── Program.cs                     # Entry point: picks mode, wires up services, web routes
├── appsettings.json              # Public config + keyword lists (safe to commit)
├── appsettings.Local.json        # Your real email/password/API key (NEVER commit)
├── Infrastructure/
│   └── ProjectPaths.cs           # Finds the project root reliably
├── Models/
│   ├── AgentOptions.cs           # Strongly-typed config (Inbox, Delivery, Schedule, …)
│   └── EmailItem.cs              # EmailItem, ClassifiedEmail, SummarizedEmail records
├── Services/
│   ├── GmailReader.cs            # Reads emails + DeleteAsync (move to Trash)
│   ├── KeywordEmailClassifier.cs # Interview vs spam/promo classification
│   ├── EmailSummarizer.cs        # OpenAI summary with keyword fallback
│   ├── DigestBuilder.cs          # Builds the HTML/text email digest
│   ├── EmailDigestSender.cs      # Sends the digest over SMTP
│   ├── DigestRunner.cs           # Orchestrates read → classify → summarize → send
│   ├── DigestSchedulerService.cs # Runs the digest every morning
│   └── DigestStore.cs            # Holds the latest scan for the dashboard
├── Templates/
│   └── DashboardPage.cs          # Builds the dashboard HTML (cards, sound, delete)
├── Dockerfile                    # Container image for deployment
└── render.yaml                   # Render.com deployment blueprint
```

---

## 5. Configuration

Edit **`appsettings.Local.json`** (kept out of git) — never put real secrets in
`appsettings.json`.

```jsonc
{
  "Inbox": {
    "EmailAddress": "you@gmail.com",   // the account to READ
    "AppPassword": "16charapppass"     // Gmail App Password (NOT your login password)
  },
  "Delivery": {
    "SenderEmail": "you@gmail.com",
    "SenderAppPassword": "16charapppass",
    "RecipientEmail": "you@gmail.com"  // where the digest is emailed
  },
  "Schedule": {
    "DailyRunTime": "08:00",           // 24h clock, local time
    "RunImmediatelyOnStart": true
  },
  "OpenAi": {
    "ApiKey": ""                       // optional; blank = keyword summaries
  }
}
```

### Gmail App Password (required)
Gmail blocks normal passwords for IMAP/SMTP. You must:
1. Turn on **2-Step Verification** on the Google account.
2. Create an **App Password** (16 characters) at
   <https://myaccount.google.com/apppasswords>.
3. Use that value for `AppPassword` / `SenderAppPassword`.

### OpenAI key (optional)
Without a key (or without billing credit) the app still works — it just uses a
plain keyword snippet instead of an AI summary. To enable AI summaries, add a
key **with billing** at <https://platform.openai.com/api-keys> and set
`OpenAi.ApiKey`.

> Any secret typed into a chat should be treated as leaked — rotate it.

---

## 6. The web dashboard

`--web` serves a page at `http://localhost:5080` that shows:

- **Stat cards** — Interview / Ignored / Scanned counts.
- **Email cards** — sender, subject, time, and the summary.
- **↻ Check inbox now** — re-scans on demand.
- **🔔 Sound chime** — plays when interview emails are present.
- **Open in Gmail ↗** — opens a Gmail search for that email.
- **🗑 Delete** — moves that email to **Gmail Trash** after a confirmation.

### Is Delete safe?
- It only runs after you click **OK** in a confirmation popup.
- It **moves the email to Gmail Trash**, which keeps it for **30 days**, so it is
  recoverable.
- Every other part of the dashboard is **read-only** and cannot change Gmail.
- Delete acts on the account in `Inbox.EmailAddress`.

---

## 7. Deployment (Render.com)

Files included: `Dockerfile`, `.dockerignore`, `render.yaml`.

- `render.yaml` runs `dotnet InboxAgent.dll --web` and expects these environment
  variables (set them in the Render dashboard, marked `sync:false`):
  `Inbox__EmailAddress`, `Inbox__AppPassword`, `Delivery__SenderEmail`,
  `Delivery__SenderAppPassword`, `Delivery__RecipientEmail`, `OpenAi__ApiKey`.
- Nested config uses **double underscores** (`Inbox__AppPassword`) in env vars.
- The app binds to Render's injected `PORT` on `0.0.0.0`; locally it uses
  `http://localhost:5080`.

> ⚠️ Render's **free** plan sleeps when idle, so an exact 8 AM email is not
> guaranteed. For reliable morning delivery use a paid plan or a cron trigger.

---

## 8. Config precedence (highest wins)

```
command line  >  environment variables  >  appsettings.Local.json  >  appsettings.json
```

This lets you keep safe defaults in `appsettings.json`, real secrets in
`appsettings.Local.json` locally, and override with env vars in the cloud.
