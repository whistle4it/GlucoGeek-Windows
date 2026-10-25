# Security and privacy

GlucoGeek stores account names and preferences locally. Passwords are encrypted
with Windows DPAPI for the current Windows user. Readings and session tokens
remain in memory. The selected provider receives its own sign-in credentials.

## Reporting a vulnerability

Do not put passwords, session tokens, real readings, account names or unredacted
diagnostic files in a public issue. If GitHub's "Report a vulnerability" option
is available on this repository's Security tab, use that private channel.
Otherwise, open an issue asking the maintainer to arrange private contact,
without disclosing the vulnerability or any sensitive material.

## Supported releases

Use the latest Windows release. Older releases are retained for upgrade testing
and historical reference and do not receive independent security fixes.
The app uses unofficial cloud integrations; either provider may change or block
access. The app is a secondary display, never a treatment or emergency-alert system.

## Release checks

The Windows workflow scans Git history with Gitleaks, runs offline application
tests, and tests installation/upgrade/removal on a disposable Windows runner.
These checks reduce risk; they do not prove the absence of every vulnerability.
