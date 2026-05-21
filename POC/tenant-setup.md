# Tenant setup for Note Aerator's release pipeline

A one-time setup guide for getting a proper **Microsoft Entra tenant**
that the Microsoft Store release pipeline (see
`microsoft-store-pipeline.md`) can use.

If you're getting lost in **MSA vs tenant vs Entra vs Azure** —
start with the mental model below before doing anything.

---

## The mental model (read this first)

There are four different things that look similar but are distinct:

| Thing                        | Example for you                                | What it is                                                                                                                                                                                                                                                                                          |
| ---------------------------- | ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **MSA** (Microsoft account)  | `rjduncan19@hotmail.com`                       | A consumer identity. Owns your Outlook.com mailbox, Xbox profile, MS Store consumer purchases, and the Partner Center seat. Has its **own** MFA registration (the one on your iPhone that's flaky). Lives at <https://account.live.com>.                                                            |
| **Entra tenant** (directory) | "Default Directory" `57b4bc41-…`               | A standalone directory that holds users, groups, and app registrations. Created automatically when you signed up for a free Azure account. Has its **own** MFA enforcement, separate from MSA MFA. Lives at <https://entra.microsoft.com>.                                                          |
| **Azure subscription**       | "Free Trial" or "Pay-As-You-Go"                | A billing scope inside an Entra tenant. Holds Azure resources (VMs, storage, etc.). **We do not use this** for the release pipeline — we only use the Entra side. The tenant survives even if the subscription lapses.                                                                              |
| **Partner Center account**   | Your Note Aerator publisher account            | A separate Microsoft service for shipping to the Store. Has its own user list. Currently logged in with your MSA. We need to associate the new Entra tenant with it so service principals from the tenant can talk to Partner Center's APIs. Lives at <https://partner.microsoft.com/dashboard>.    |

The pipeline lives on this chain:

```
GitHub Actions  →  Entra service principal  →  Partner Center API  →  Microsoft Store
   (workflow)       (an "app" in the                (the only public-          (where customers
                     Entra tenant)                   facing API for             install Note
                                                     submissions)               Aerator)
```

Your MSA's job in this chain is **just to set things up once** — the
pipeline itself never uses your MSA. After setup, your MSA only comes
back into play if you want to sign in interactively to Partner Center
or the Azure portal to inspect things.

---

## Where you are right now

- ✅ You have an MSA: `rjduncan19@hotmail.com`.
- ✅ You have a Partner Center account under that MSA, with Note
  Aerator (`9N5DTC0FZP7M`) published.
- ✅ You signed up for free Azure, which created a new Entra tenant
  ("Default Directory", `57b4bc41-e1f8-458b-bd13-4be5a8316e31`). Your
  MSA is the global admin of this tenant.
- ❌ The Entra tenant is **not** yet associated with Partner Center.
- ❌ Tenant MFA is **not** yet registered for your MSA (don't worry
  about the Oct 1 2025 enforcement banner — see § MFA below).

Below is what to do next, in order.

---

## Step 1 — sign in to the new tenant for the first time

Open <https://entra.microsoft.com> in a fresh browser tab (or InPrivate
window — recommended so it doesn't bleed in cookies from other
Microsoft accounts).

1. Sign in with `rjduncan19@hotmail.com`. This uses your MSA login
   (and its existing MSA-side MFA, however that's set up today).
2. Top-right corner — confirm the tenant you're acting in. It should
   say **"Default Directory"** and show the tenant ID
   `57b4bc41-e1f8-458b-bd13-4be5a8316e31`. If you ever land in the
   wrong context, click your avatar → **Switch directory** and pick
   the right one.

This is the directory in which all subsequent app registrations,
service principals, and (eventually) MFA enrollment happen.

---

## Step 2 — sort out MFA for the tenant

You're seeing this banner in the Azure portal:

> Multifactor authentication enforcement (phase 2) will begin on or
> after October 1, 2025. MFA will be required for all users working
> in: Azure CLI, PowerShell, Azure mobile app, IaC tools, Azure
> Identity SDK, and MSAL.

### What this means

- This is **Entra tenant MFA**, not MSA MFA. They are independent.
  Your MSA already has MFA (you set it up for outlook.com); Entra
  treats that as a completely separate authentication system.
- The enforcement applies to **interactive user sign-ins** to Azure
  management endpoints. Your CI pipeline uses a service principal
  with `client_id` + `client_secret` (a non-interactive flow). **MFA
  enforcement does not apply to service principals**, so the pipeline
  won't break.
- What *will* break (after Oct 1, 2025) is *you*, the human, signing
  in to portal.azure.com / entra.microsoft.com to do admin work in
  this tenant. You'll be required to register an MFA method against
  the tenant.

### What to do

You don't have to act today, but doing it now is easier than waiting
for an emergency. Register an MFA method against the tenant:

1. With your MSA logged in to the tenant, open
   <https://mysignins.microsoft.com/security-info>.
2. Click **+ Add sign-in method**.

### Which method to pick (given your iOS push problem)

The Microsoft Authenticator "push approval" flow for *personal*
Microsoft accounts is known to be flaky on iOS — notifications
silently drop. The **same app for "work/school" accounts** (which is
what your Entra tenant counts as) usually works fine, but if you want
to skip the risk entirely, use one of the alternatives below.

| Method                                                    | Reliability | Notes                                                                                                                                              |
| --------------------------------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Authenticator app — push** (work/school account flow)   | Usually OK  | Add as a "work or school account" inside Authenticator. This is a **different** registration from your existing MSA one — they don't interfere.    |
| **Authenticator app — TOTP** (6-digit rotating code) ⭐    | Very high   | Choose "Use verification code" instead of push. Same app, just type the code. Bypasses the iOS notification issue entirely.                        |
| **Third-party TOTP app** (1Password, Bitwarden, Authy)    | Very high   | Same flow, you just don't have to use Microsoft Authenticator. Scan the QR code with whichever TOTP app you already trust.                         |
| **Phone (SMS or voice call)**                             | OK          | Decent backup. Microsoft increasingly discourages SMS for primary MFA but it still works for a personal Azure tenant.                              |
| **Passkey / FIDO2 security key**                          | Highest     | Best long-term, but requires a hardware key or platform passkey support. Overkill for a personal release pipeline; great if you already use them.  |

**Recommended for you:** add **TOTP** (either inside Microsoft
Authenticator with "Use verification code", or via 1Password /
Bitwarden), and add **phone call** as a backup. This gives you two
independent factors and avoids the broken iOS push channel entirely.

### About the "enforcement start date" widget

The portal lets you push the enforcement date back a bit using an
"elevated access" workflow. **You can ignore this.** You're going to
register MFA right now anyway, so the start date doesn't matter — you
won't notice enforcement when it arrives.

---

## Step 3 — associate the tenant with Partner Center

Even though you signed up for Partner Center with the same MSA that
owns the Entra tenant, Partner Center does not automatically know
about the tenant. You have to tell it.

1. Open <https://partner.microsoft.com/dashboard/account/v3/tenants/associated>.
2. If "Default Directory" (`57b4bc41-…`) does **not** appear, click
   **Associate Azure AD** and follow the prompt. You'll be asked to
   sign in once with a global admin of that tenant — that's your MSA.
3. After it appears in the list, Partner Center can see users and
   apps from this tenant.

You only do this once per tenant.

---

## Step 4 — register the publish app and grant it Partner Center access

This is where the release pipeline's identity is created. The full
details (with the exact field values and the Manager role
assignment) live in `microsoft-store-pipeline.md` **Step 1**.

Quick summary:

1. <https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade>
   → **+ New registration** → name it `noteaerator-store-publisher` →
   Single tenant → no redirect URI.
2. Copy the **Directory (tenant) ID** and **Application (client) ID**
   from the Overview page (the tenant ID is the same
   `57b4bc41-e1f8-458b-bd13-4be5a8316e31`; the client ID is unique
   per app).
3. **Certificates & secrets → + New client secret** → 24-month
   expiry → copy the secret **value** (only shown once).
4. <https://partner.microsoft.com/dashboard/account/v3/usermanagement#azureadapplications>
   → **Add Azure AD applications** → pick the new app → assign role
   **Manager**.
5. Stash `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, and
   `AZURE_CLIENT_SECRET` in
   <https://github.com/rjduncan19/noteaerator/settings/secrets/actions>.

Then continue with `microsoft-store-pipeline.md` from **Step 3** to
wire the GitHub Actions workflow.

---

## Clean-up: delete the tenantless "NA Note Aerator" app

While you were figuring this out, you created an app registration
("NA Note Aerator.838e73271599",
`239102d1-1438-4b5b-9c8e-47bc037bdc39`) before the tenant existed.
That one is tenantless and unusable for the Submission API. Delete it:

1. <https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade>
   (in the new tenant context) — it probably won't even appear here.
2. To find it, open
   <https://account.live.com/Consent/Manage> (the consumer "app
   permissions" page for your MSA) and revoke the entry.
3. Optionally also delete it via
   <https://account.live.com/proofs/Manage/additional> if it shows up
   as a connected app.

This is just hygiene — it doesn't affect anything functionally.

---

## FAQ

**Q: I already have MFA on my MSA via the Authenticator app (when it
works). Doesn't that count for the tenant?**
No. MSA MFA and Entra tenant MFA are entirely separate systems with
separate registrations. The same physical Authenticator app can hold
both, but they appear as two different account entries inside it.

**Q: Does the MFA enforcement break my publish pipeline?**
No. The pipeline uses a service principal (non-interactive
`client_id` + `client_secret` auth). MFA only applies to interactive
user sign-ins.

**Q: Will the free Azure tenant expire if I don't use it?**
No. The Entra tenant has no inactivity timer (unlike M365 Developer
Program tenants). It persists as long as the account exists and has a
global admin. The "free" branding only applies to the Azure
*subscription*, which we are not using.

**Q: What if I need to give someone else access later?**
Invite them as a guest user in the tenant
(<https://entra.microsoft.com/#view/Microsoft_AAD_IAM/UsersManagementMenuBlade/~/AllUsers>
→ **+ New user → Invite external user**), then grant them whatever
Entra and/or Partner Center role they need. The Partner Center seat
itself is still per-MSA and would need a separate invite from
<https://partner.microsoft.com/dashboard/account/v3/usermanagement>.

**Q: My client secret will expire in 24 months. What happens to the
pipeline?**
It'll start failing at the `msstore` login step. To rotate: create a
new secret in the Entra app, update `AZURE_CLIENT_SECRET` in GitHub
secrets, delete the old secret. Set a calendar reminder.
