---
name: security-review
description: Focused security vulnerability review. Identifies HIGH-CONFIDENCE exploitable issues (auth bypass, injection, crypto flaws, data exposure). Use on a branch, file, or directory.
disable-model-invocation: true
allowed-tools: Read, Grep, Glob, Bash, Task
argument-hint: [file, directory, or git diff target]
---

# Security Review

Perform a security-focused review on: `$ARGUMENTS`

If no argument is provided, review all changes on the current branch compared to `master` (`git diff master...HEAD`). If there are no branch changes, review all source files under `src/`.

---

## Objective

Identify **HIGH-CONFIDENCE** security vulnerabilities with real exploitation potential. This is NOT a general code review — focus ONLY on security implications.

---

## Analysis Methodology

### Phase 1 — Repository Context Research

- Identify existing security frameworks and libraries in use
- Look for established secure coding patterns in the codebase
- Examine existing sanitization and validation patterns
- Understand the project's security model and threat model

### Phase 2 — Comparative Analysis

- Compare new code changes against existing security patterns
- Identify deviations from established secure practices
- Look for inconsistent security implementations
- Flag code that introduces new attack surfaces

### Phase 3 — Vulnerability Assessment

- Examine each file for security implications
- Trace data flow from user inputs to sensitive operations
- Look for privilege boundaries being crossed unsafely
- Identify injection points and unsafe deserialization

---

## Security Categories

### 1. Input Validation Vulnerabilities

- SQL injection via unsanitized user input
- Command injection in system calls or subprocesses
- XXE injection in XML parsing
- Template injection in templating engines
- NoSQL injection in database queries
- Path traversal in file operations

### 2. Authentication & Authorization Issues

- Authentication bypass logic
- Privilege escalation paths
- Session management flaws
- JWT token vulnerabilities (algorithm confusion, missing validation, claim manipulation)
- Authorization logic bypasses

### 3. Crypto & Secrets Management

- Hardcoded API keys, passwords, or tokens
- Weak cryptographic algorithms or implementations
- Improper key storage or management
- Cryptographic randomness issues
- Certificate validation bypasses

### 4. Injection & Code Execution

- Remote code execution via deserialization
- YAML/JSON deserialization vulnerabilities
- Eval injection in dynamic code execution
- XSS vulnerabilities (reflected, stored, DOM-based)

### 5. Data Exposure

- Sensitive data logging (secrets, passwords, PII)
- API endpoint data leakage
- Debug information exposure in production

---

## Hard Exclusions — Do NOT Report

1. Denial of Service (DOS) or resource exhaustion
2. Secrets stored on disk if otherwise secured
3. Rate limiting concerns
4. Memory / CPU exhaustion
5. Lack of input validation on non-security-critical fields without proven impact
6. Lack of hardening measures — only flag concrete vulnerabilities
7. Theoretical race conditions without a concrete exploit path
8. Outdated library versions (managed separately)
9. Test-only files
10. Log spoofing
11. SSRF that only controls the path (not host/protocol)
12. Documentation files (markdown, etc.)
13. Lack of audit logs
14. Resource management issues (memory/FD leaks)
15. Regex injection or Regex DOS
16. Environment variables and CLI flags (trusted values)

---

## Confidence & Severity

**Only report findings with confidence >= 8 / 10.**

| Confidence | Meaning |
|------------|---------|
| 9–10 | Certain exploit path identified |
| 8–9 | Clear vulnerability pattern with known exploitation methods |
| < 8 | Do not report (too speculative) |

| Severity | Meaning |
|----------|---------|
| **HIGH** | Directly exploitable → RCE, data breach, or auth bypass |
| **MEDIUM** | Requires specific conditions but significant impact |

Skip LOW severity — better to miss theoretical issues than flood the report with noise.

---

## Output Format

```markdown
# Security Review Report

## Summary
[1-2 sentence overview of scope and overall security posture]

## Findings

### Vuln N: [Category]: `file:line`

- **Severity**: HIGH / MEDIUM
- **Confidence**: 8–10
- **Description**: [What the vulnerability is]
- **Exploit Scenario**: [Concrete attack path]
- **Recommendation**: [Specific fix]

## Action Items
- [ ] [Actionable fix with file path]
```

If no vulnerabilities are found above the confidence threshold:

```markdown
# Security Review Report

## Summary
No high-confidence security vulnerabilities identified.

## Notes
[Any observations or defense-in-depth suggestions, if relevant]
```
