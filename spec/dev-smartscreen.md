# Dev spec: SmartScreen warning on the downloaded Syncer

Status: draft, waiting for the owner's answers. Refers to project-guide.md sections 9a (Syncer) and 9b (Guide page and downloads). Merge into the guide only after acceptance.

## 1. Problem

On a second PC (Windows 11) the browser-downloaded `InventoryTrackingSyncer.exe` (from `InventoryTrackingSyncer-1.0.0.zip` on the hosted site) shows:

> Windows protected your PC - Microsoft Defender SmartScreen prevented an unrecognized app from starting. App: InventoryTrackingSyncer.exe, Publisher: Unknown publisher. [Run anyway] [Don't run]

This is the SmartScreen reputation check, not a virus detection. "Run anyway" is available, so Smart App Control is not blocking it.

## 2. Cause

1. The browser marks the downloaded zip with Mark-of-the-Web (MotW); Windows Explorer copies it to the extracted exe. Files with MotW are checked by SmartScreen.
2. The exe is not code-signed, so the publisher is "Unknown publisher", and SmartScreen has no reputation for this file hash. A new, rarely downloaded file is "unrecognized" by definition.
3. Reputation is per file hash (unsigned) or per signing identity (signed). Every rebuild is a new hash, so an unsigned exe never keeps its reputation.

There is no setting or file attribute that marks an exe "secure" on someone else's PC. Only (a) a trusted signature plus accumulated reputation, (b) the user removing MotW, or (c) not downloading an exe through the browser avoids the dialog.

## 3. Options

| # | Option | Effect | Cost / effort |
|---|---|---|---|
| A | Code-sign with an OV certificate (e.g. from Sectigo/SSL.com/Certum resellers) | Publisher name shown. Reputation still builds up over time and downloads; early users may still see the warning. Keeping the same identity across builds keeps the reputation. | roughly 100-400 EUR per year; identity validation (individual or company); since 2023 the key must be on hardware or a cloud HSM |
| B | EV certificate | Historically instant reputation; a 2026 report says this is no longer guaranteed. | highest cost, company validation |
| C | Azure Artifact Signing (formerly Trusted Signing) | Microsoft-issued short-lived certificates, good SmartScreen standing, cheap (about 10 USD per month, unverified). Individuals are only accepted in the US and Canada; organisations in the US, Canada, EU, UK. Probably not available to a German private person. | needs a registered business and an Azure subscription |
| D | SignPath Foundation (free for open source) | Free certificate, publisher shows as the SignPath Foundation. Needs an OSI-licensed public repository, no proprietary parts, automated build (e.g. GitHub Actions), a README credit line. | repo must be public with a licence; CI setup |
| E | Submit the file to Microsoft as a false positive / for SmartScreen review (Security Intelligence submission portal) | Can clear the warning for that exact hash after review; no guaranteed time; does not survive a rebuild. | free, manual per release |
| F | Do nothing technical, give clear instructions | Guide page already says "More info -> Run anyway". Add the screenshot text and the Unblock alternative (see 4). | none |
| G | Different delivery | Framework-dependent exe (small, but needs .NET 8 Desktop Runtime), MSIX/winget/GitHub Releases. MSIX needs signing too; winget and GitHub do not remove MotW from browser downloads. The Microsoft Store removes the warning but needs a developer account (about 19 USD one time for individuals) and certification. | high |

Not helpful: changing compression or the self-extract flags (the warning is reputation, not content); a Defender false positive would show a different text.

## 4. Mitigations that need no certificate (do in any case)

1. Guide page and Syncer README: show the exact dialog text and the steps: "More info" -> "Run anyway".
2. Alternative for the user: right-click the downloaded zip (before extracting) -> Properties -> tick "Unblock" -> OK, then extract; or in PowerShell `Unblock-File .\InventoryTrackingSyncer.exe`.
3. Keep the SHA-256 in downloads.json so a user can verify the file (already there).
4. Add real version info (company, product, description, copyright) to the exe via the csproj (`Company`, `Product`, `Description`, `Copyright`); this does not remove the warning but makes the file look legitimate in properties and Defender.
5. Do not rebuild the exe needlessly: the same file keeps whatever reputation it gained.
6. After each release, submit the zip/exe to Microsoft (option E).

## 5. Recommendation

Short term: item 4 (instructions, version info) plus option E for the released file. Long term, choose by situation:
- Public repository with an open licence: option D (free, cleanest).
- A registered business exists: option C.
- Otherwise: option A if the warning is considered worth about 100-400 EUR per year; otherwise stay with F.

## 5a. Owner's answers (2026-10-10) and decision

- Repo will be public soon (open licence, MIT planned); private person, no business; no yearly cost (hobby); warning acceptable if explained, ideally gone; Syncer meant for the public.
- Warning appeared only on the exe, not the zip; later the other PC opened it without a warning (likely the one-time "Run anyway" approval or accumulated reputation for that hash; not reproducible on purpose).
- Decision: options C and A are out (business / cost). Plan = F + 4 now (done: Guide callout with the dialog text, Run anyway and Unblock steps; version metadata in the csproj), then **D (SignPath Foundation)** once the repo is public: add an MIT LICENSE, a GitHub Actions build that publishes the Syncer, apply at signpath.org, add the README credit line. Until approved, E (submission) per release is optional.
- Caveat for D: the publisher shown will be "SignPath Foundation", and reputation is still built over downloads, so a few early warnings can remain.

## 6. Open questions for the owner (answered, see 5a)

1. Is the repository (or could it be) public with an open-source licence (MIT etc.)? That decides D.
2. Do you have a registered business (Gewerbe/company) for option C or an OV certificate in a company name, or only a private identity?
3. Budget: is a yearly certificate cost acceptable, or should it stay free?
4. Is the warning acceptable if the Guide explains it well (option F + 4), or must it disappear?
5. Is the Syncer's audience small (friends) or public? For a handful of users, instructions plus Unblock are usually enough.
6. Does the other PC also show the warning for the zip download itself, or only for the exe? (Edge/Chrome may warn about the zip first.)

## 7. Implementation plan once answered

1. Add version metadata to the Syncer csproj; republish; repackage with `tools/package_release.py`.
2. Update the Guide page downloads section and the README with the dialog text, "Run anyway" steps, Unblock alternative (web tests for guide sections still apply).
3. If signing is chosen: add a signing step to the publish/package process (signtool or the service's CLI, with RFC 3161 timestamp), sign before zipping, and record the publisher in the guide.
4. Rebuild the IIS package; ask before deploying.
5. Merge this spec into project-guide.md section 9a/9b and delete this file.

## Sources

- Azure Artifact Signing quickstart: https://learn.microsoft.com/azure/artifact-signing/quickstart
- SignPath for open source: https://signpath.io/product/open-source
- SmartScreen reputation: https://learn.microsoft.com/en-us/archive/blogs/ie/smartscreen-application-reputation-building-reputation
