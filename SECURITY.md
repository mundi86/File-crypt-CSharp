# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 2.x     | Yes       |
| 1.x     | No (legacy, weak crypto — upgrade recommended) |

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, report them via the GitHub [Security Advisories](../../security/advisories/new) feature (private disclosure).

Include:
- A description of the vulnerability
- Steps to reproduce
- Potential impact

You will receive a response within 7 days.

---

## Cryptographic Details

### v2 Format (current)

| Property | Value |
|----------|-------|
| Algorithm | AES-256-CBC |
| Key Derivation | PBKDF2-SHA256, 100,000 iterations |
| Salt | 32 bytes (cryptographically random per file) |
| IV | 16 bytes (cryptographically random per file) |
| Padding | PKCS7 |
| File header | `PCv2` magic (4 bytes) |

### v1 Format (legacy, read-only)

| Property | Value |
|----------|-------|
| Algorithm | Rijndael-256-CBC |
| Key Derivation | PBKDF2-SHA1, 2,000 iterations |
| Status | **Weak** — decrypt and re-encrypt with v2 |

---

## Known Limitations

- **SSD / Flash Storage:** The secure-delete feature overwrites file content with zeros before deletion. Due to wear-leveling on SSDs and USB flash drives, the operating system cannot guarantee that the original data blocks are physically overwritten. Full-disk encryption (e.g., BitLocker) is recommended as a complement on such devices.

- **Swap / Hibernation:** Decrypted file contents may briefly reside in Windows page file or hibernation file. Enabling BitLocker mitigates this.

- **Password Strength:** The security of encrypted files depends entirely on password strength. Weak passwords remain vulnerable to brute-force attacks despite the high PBKDF2 iteration count.

- **Quick-Edit Temp Files:** During Quick-Edit, the file is temporarily decrypted to `%TEMP%`. privateCrypt deletes and zero-overwrites the temp file after the viewer process exits. The SSD limitation above applies here as well.
