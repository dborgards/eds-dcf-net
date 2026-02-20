# Security Policy

## Supported Versions

Only the latest stable release of **EdsDcfNet** receives security fixes.
Older versions are not actively maintained.

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |
| < latest | :x:               |

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub Issues.**

Instead, use GitHub's private vulnerability reporting feature:

1. Navigate to the [Security Advisories](https://github.com/dborgards/eds-dcf-net/security/advisories) page.
2. Click **"Report a vulnerability"**.
3. Fill in the details described below.

Alternatively, you can contact the maintainer directly at the e-mail address
listed on [his GitHub profile](https://github.com/dborgards).

### What to include in your report

- A clear description of the vulnerability and its potential impact.
- Steps to reproduce (minimal reproducing code or a malformed EDS/DCF/CPJ file
  that triggers the issue).
- The affected version(s) of the NuGet package.
- If known, a suggested fix or mitigation.

### Response expectations

- You will receive an acknowledgement within **5 business days**.
- A fix or a detailed status update will be provided within **30 days**,
  depending on complexity.
- You will be credited in the release notes unless you prefer to remain
  anonymous.

## Scope

EdsDcfNet is a **file-parsing library** with no network access, no
authentication logic, and no cryptographic operations. The relevant security
surface is therefore limited to:

| Area | Examples of in-scope issues |
|------|-----------------------------|
| Malformed input handling | Unbounded memory allocation, infinite loops, or unhandled exceptions when parsing crafted EDS/DCF/CPJ files |
| Path traversal | Any API that accepts a file path and could be abused to read or write outside the intended directory |
| Denial of service via parsing | Algorithmic complexity attacks (e.g. quadratic parsing) triggered by crafted files |
| Dependency vulnerabilities | Vulnerabilities in transitive NuGet dependencies (currently none in the main library) |

The following are **out of scope**:

- Issues in the consuming application's own code or configuration.
- Theoretical vulnerabilities without a practical proof-of-concept.
- Findings from automated scanners without a clear reproducible impact.
- Issues requiring physical access to the target system.

## Security considerations for library consumers

Because EdsDcfNet parses files that may come from untrusted sources (e.g.
device configuration files downloaded from the internet or received over a
network), consumers should apply the standard defense-in-depth measures:

- **Validate file origin** before passing paths or content to the library.
- **Limit resource usage** (memory, CPU) at the process level when parsing
  files from untrusted sources.
- **Keep the package up to date** to receive security fixes as soon as they
  are released.

## Disclosure policy

We follow a **coordinated disclosure** approach. Security issues are kept
private until a fix is available, after which a GitHub Security Advisory is
published together with the patched release.
