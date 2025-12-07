# Security Policy

## Supported Versions

We release patches for security vulnerabilities. Which versions are eligible for receiving such patches depends on the CVSS v3.0 Rating:

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

Please report (suspected) security vulnerabilities to **[info@unhacked.dev](mailto:info@unhacked.dev)**. You will receive a response from us within 48 hours. If the issue is confirmed, we will release a patch as soon as possible depending on complexity but historically within a few days.

Please do not report security vulnerabilities through public GitHub issues.

### What to include in your report

-   Type of issue (e.g. buffer overflow, SQL injection, cross-site scripting, etc.)
-   Full paths of source file(s) related to the manifestation of the issue
-   The location of the affected source code (tag/branch/commit or direct URL)
-   Any special configuration required to reproduce the issue
-   Step-by-step instructions to reproduce the issue
-   Proof-of-concept or exploit code (if possible)
-   Impact of the issue, including how an attacker might exploit the issue

### Preferred Languages

We prefer all communications to be in English or Portuguese.

## Security Best Practices

When using NFury, please ensure you:

1. **Keep dependencies updated**: Regularly update to the latest version of NFury and its dependencies.

2. **Use HTTPS for target URLs**: When testing APIs, prefer HTTPS endpoints to ensure data integrity.

3. **Secure your test environment**: Run load tests in isolated environments to avoid impacting production systems.

4. **Review test configurations**: Double-check target URLs before running tests to avoid accidentally testing production systems.

5. **Use authentication properly**: When testing authenticated endpoints, ensure credentials are stored securely and not committed to version control.

6. **Monitor resource usage**: Load testing can consume significant resources. Monitor your system to prevent unintended resource exhaustion.

7. **Rate limiting awareness**: Be aware of rate limits on target APIs to avoid being blocked or causing issues.

8. **Legal compliance**: Ensure you have proper authorization before load testing any API that you don't own.

## Acknowledgments

We would like to thank the following security researchers for responsibly disclosing vulnerabilities:

-   _None yet - be the first!_
