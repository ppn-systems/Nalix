# Security Policy

## Supported Versions

Security patches and fixes are provided for the two most recent active branches:

| Branch | Status |
| :--- | :--- |
| `master` | ✅ Latest stable — actively maintained. |
| `dev` | ⚠️ Prerelease — under active development. |

> Older versions will **not** receive security updates. Users are strongly encouraged to upgrade to the latest stable release.

---

## Reporting a Vulnerability

If you discover a potential security vulnerability, **do not open a public issue**.

Instead, report it privately using one of the following methods:

1. **GitHub Security Advisories** — Use the **Security** tab in the [repository header](https://github.com/ppn-systems/Nalix/security) to create a private advisory.
2. **Email** — Contact the maintainers directly at **[ppn.system@gmail.com](mailto:ppn.system@gmail.com)**.

For more information about private vulnerability reporting, see [GitHub's documentation](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing-information-about-vulnerabilities/privately-reporting-a-security-vulnerability).

---

## Disclosure Process

| Step | Action | Timeline |
| :---: | :--- | :--- |
| 1 | Report acknowledged. | Within **48 hours**. |
| 2 | Issue reviewed and validated by maintainers. | — |
| 3 | Patch prepared on a private branch. | — |
| 4 | Fix released publicly; **CVE ID** assigned if applicable. | — |
| 5 | Reporter credited (unless anonymity requested). | — |

---

## Security Best Practices

When using Nalix packages in production:

- Always use the **latest version** of each `Nalix.*` NuGet package.
- Do **not** disable integrity checks or modify package binaries.
- Validate all inputs and enforce encryption when using networking or cryptographic APIs.
- Review the [official documentation](DOCUMENTATION.md) for recommended security configurations.

---

## Contact

For questions about this policy or secure usage of Nalix libraries, reach out to **[ppn.system@gmail.com](mailto:ppn.system@gmail.com)** or open a [private security advisory](https://github.com/ppn-systems/Nalix/security).
