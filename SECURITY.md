# Security Policy

## Supported Versions

| Version | Supported | Notes |
|---------|-----------|-------|
| 3.x     | Yes       | Current release — AES-256-CBC **+ HMAC-SHA256** |
| 2.x     | Decrypt only | Can read 2.0 files; new files are always written as v3 |
| 1.x     | No        | Legacy crypto (PBKDF2-SHA1, 2.000 iterations) — decrypt and re-encrypt |

## Reporting a Vulnerability

Please **do not** open a public issue for security vulnerabilities.

Instead, report them privately via the repository host's security advisory feature.

Include:
- A description of the vulnerability
- Steps to reproduce
- Potential impact

---

## Cryptographic Details

### v3 format (current)

```
[ "PCv3" 4 B ] [ salt 32 B ] [ iv 16 B ] [ ciphertext ... ] [ HMAC-SHA256 32 B ]
```

| Property | Value |
|----------|-------|
| Algorithm | AES-256-CBC, PKCS7 padding |
| Authentication | HMAC-SHA256 over header **and** ciphertext (Encrypt-then-MAC) |
| Key derivation | PBKDF2-HMAC-SHA256, 300,000 iterations |
| Master key | 64 bytes, derived **once per session** from the password |
| Per-file keys | `HMAC-SHA256(master, "enc"\|"mac" ‖ salt ‖ iv)` → 32 bytes each |
| Salt / IV | 32 / 16 bytes, CSPRNG, fresh for every single file |
| Tag comparison | Constant time |
| Verification order | Signature is checked **before** any plaintext is produced |

**Why the master key?** PBKDF2 is deliberately expensive (~1.4 s). Deriving it once per
session instead of once per file means a folder with 500 files costs the same as a single
file. The per-file subkeys are derived with HMAC, which is essentially free, and stay
domain-separated (`enc` vs `mac`) and bound to that specific file via its salt and IV.

**Why Encrypt-then-MAC?** v2 used bare AES-CBC. CBC without a MAC is malleable: flipping a
single ciphertext bit changes exactly one plaintext bit in a predictable position, so an
attacker with write access to a `.protected` file can alter its contents without knowing the
password. Adding HMAC-SHA256 over the ciphertext closes that gap. Decryption verifies the
tag first and produces no plaintext at all if it does not match.

> Note: the error message deliberately does **not** distinguish "wrong password" from
> "file was modified". Distinguishing them would leak information to an attacker.

### v2 format (read-only since 3.0)

| Property | Value |
|----------|-------|
| Algorithm | AES-256-CBC, PKCS7 |
| Key derivation | PBKDF2-SHA256, 100,000 iterations |
| Layout | `[ "PCv2" 4 B ][ salt 32 B ][ iv 16 B ][ ciphertext ]` |
| Authentication | **None** |

v2 files can be decrypted and are detected automatically. They carry no integrity
protection — if you have v2 files lying around, decrypt them once and re-encrypt; that
upgrades them to v3.

### v1 format (legacy, decrypt-only)

| Property | Value |
|----------|-------|
| Algorithm | Rijndael-256-CBC (block size 32 bytes) |
| Key derivation | PBKDF2-SHA1, 2,000 iterations |
| Key input | `ASCII(uplowme(sha256hex(password)))` |
| Layout | `[ ciphertext ][ salt 32 B ][ iv 32 B ]` |
| Authentication | **None** |

Retained so files created by the 2011 release remain readable. The 2,000 iterations are
far below any current recommendation; treat v1 files as compromised and re-encrypt them.
`PolyAES.Tests` contains fixed test vectors generated with an independent implementation
of the original algorithm, so the legacy path cannot regress unnoticed.

---

## Passwords

- Minimum **8 characters** when encrypting. Minimum 4 when decrypting, so older files stay
  accessible.
- Enforced minimums are a floor, not a recommendation. A long passphrase
  ("correct horse battery staple") matters far more than any parameter in this document.
- The password is held as `char[]` and zeroed after use, never as a `String` — but the
  .NET garbage collector may still have copied it. Nothing can fully prevent that in
  managed code.

---

## Known Limitations

- **SSD / Flash Storage:** `SecureDelete` overwrites file content with zeros before
  deleting. Due to wear-leveling on SSDs and USB flash drives, and on encrypted volumes,
  physical overwrite cannot be guaranteed. Use BitLocker as a complement.

- **Swap / Hibernation:** Plaintext may briefly reach the Windows page or hibernation file.
  BitLocker mitigates this.

- **Quick-Edit temp files:** During Quick-Edit the file is decrypted into
  `%TEMP%\privateCrypt-quickedit\`. It is zero-overwritten and deleted once the viewer
  exits. Because a viewer or the user can terminate the app abruptly, privateCrypt also
  removes leftovers older than 24 hours from that folder on the next start. The SSD
  limitation above applies.

- **Passphrase strength:** File security rests entirely on the passphrase. Iteration count
  cannot rescue a weak passphrase against an offline attack on a stolen file.

- **No key stretching beyond PBKDF2:** Argon2id or scrypt would be stronger against
  GPUs/ASICs. PBKDF2 was kept for compatibility with .NET Framework 4.8 without adding
  dependencies.

---

## Testing

`File_crypt/Tests/PolyAES.Tests` covers the crypto layer without any external test
framework (no NuGet restore required):

```
msbuild File_crypt/File_crypt.sln /p:Configuration=Release /p:Platform=x86
File_crypt\Tests\PolyAES.Tests\bin\Release\PolyAES.Tests.exe
```

It includes round-trips across block and buffer boundaries, tamper detection
(ciphertext/salt/IV/tag bit flips, block swaps, truncation, extension), v2 and v1
compatibility, and atomic-write guarantees. Run it before changing anything in
`PolyAES.cs`.