# Security Policy

## Supported Versions

Patches and security fixes are considered for the two most recent active versions:

- `main` — latest stable release under maintenance.
- `dev` — prerelease branch under active development.

Older versions will not receive security updates. Users are strongly encouraged to upgrade to the latest stable release.

---

## Reporting a Vulnerability

If you discover a potential security vulnerability, **do not open a public issue**.

Instead, please report it privately through the **Security** tab in the GitHub repository header  
or contact the maintainers directly at [ppn.system@gmail.com](mailto:ppn.system@gmail.com) (if applicable).

See [this additional information from GitHub on private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability).

---

## Disclosure Process

1. The report will be acknowledged within **48 hours**.
2. The maintainers will review and validate the issue.
3. If confirmed, a patch will be prepared on a private branch.
4. The fix will be released publicly once validated and assigned a **CVE ID** (if applicable).
5. The reporter will be credited unless anonymity is requested.

---

## Security Best Practices

To ensure safety while using Nalix packages:

- Always use the **latest version** of each `Nalix.*` NuGet package.
- Do **not** disable integrity checks or modify package binaries.
- Validate inputs and enforce encryption when using networking or cryptographic APIs.
- Review official documentation for recommended security configurations.

---

## Contact

For questions about this policy or secure usage of Nalix libraries,  
please reach out to [ppn.system@gmail.com](mailto:ppn.system@gmail.com) or open a private security advisory.
