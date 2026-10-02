# Security Policy

## Supported Versions

| Version | Supported | Notes |
|---------|-----------|-------|
| 4.x     | Yes       | Current release — AES-256-CBC **+ HMAC-SHA256**, getrennte Schlüssel |
| 3.x     | Decrypt only | Reads 3.0 files; the folder run upgrades them to PCv4 |
| 2.x     | Decrypt only | Can read 2.0 files; new files are always written as v4 |
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

### v4 format (current)

```
[ "PCv4" 4 B ] [ salt 32 B ] [ iv 16 B ] [ ciphertext ... ] [ HMAC-SHA256 32 B ]
```

| Property | Value |
|----------|-------|
| Algorithm | AES-256-CBC, PKCS7 padding |
| Authentication | HMAC-SHA256 over header **and** ciphertext (Encrypt-then-MAC) |
| Key derivation | PBKDF2-HMAC-SHA256, 300,000 iterations |
| Master key | 64 bytes, derived **once per session** from the password, then **split in half** |
| Cipher key | `HMAC-SHA256(master[0:32], "privateCrypt/v4/enc" ‖ salt ‖ iv)` → 32 bytes |
| MAC key | `HMAC-SHA256(master[32:64], "privateCrypt/v4/mac" ‖ salt ‖ iv)` → 32 bytes |
| Salt / IV | 32 / 16 bytes, CSPRNG, fresh for every single file |
| Tag comparison | Constant time |
| Verification order | Signature is checked **before** any plaintext is produced |

**Why the master key is split in half.** The cipher key and the MAC key must never
be the same value. Encrypt-then-MAC with one shared key means a bug in either
primitive is no longer isolated — and the whole protective effect of the MAC
depends on exactly that isolation. The first 32 bytes of the master key feed only
the cipher key, the second 32 only the MAC key, each with its own label. No byte
of one key can ever appear in the other.

**Why Encrypt-then-MAC?** v2 used bare AES-CBC. CBC without a MAC is malleable: flipping a
single ciphertext bit changes exactly one plaintext bit in a predictable position, so an
attacker with write access to a `.protected` file can alter its contents without knowing the
password. Adding HMAC-SHA256 over the ciphertext closes that gap. Decryption verifies the
tag first and produces no plaintext at all if it does not match.

> Note: the error message deliberately does **not** distinguish "wrong password" from
> > "file was modified". Distinguishing them would leak information to an attacker.

### v3 format (read-only since 4.0)

Same layout as v4, same HMAC, same 300,000 PBKDF2 iterations — but in version 3.0 both
the cipher key and the MAC key were derived from **one single seed** and were therefore
bit-for-bit identical. The key separation that v3's own documentation claimed does not
exist in the files it produced. It was not a weakness that allowed reading or writing
files; it was a missing defense-in-depth. Files remain readable, and the folder run
upgrades them to v4.

The reading path (`DeriveV3Keys`) reproduces the 3.0 derivation deliberately, unchanged.
Any "improvement" there would make every file created by 3.0 unopenable.

### v2 format (read-only since 3.0)

| Property | Value |
|----------|-------|
| Algorithm | AES-256-CBC, PKCS7 |
| Key derivation | PBKDF2-SHA256, 100,000 iterations |
| Layout | `[ "PCv2" 4 B ][ salt 32 B ][ iv 16 B ][ ciphertext ]` |
| Authentication | **None** |

v2 files can be decrypted and are detected automatically. They carry no integrity
protection — if you have v2 files lying around, decrypt them once and re-encrypt; that
upgrades them to v4.

> **Folder mode does that for you.** Decrypting a folder upgrades every legacy file it
> contains to v4 in the same pass, leaving no plaintext behind. The summary states how
> many files were upgraded and from which format. Single-file decryption keeps the old
> format — re-encrypt manually if you want to upgrade one file.
>
> Until a v2 or v1 file has been upgraded, treat its contents as **unauthenticated**.
> An attacker with write access can alter it without knowing your password.

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

- **Cancellation granularity:** "Cancel" takes effect at the next 64 KiB block
  boundary. A single file larger than a few hundred megabytes therefore still takes a
  moment to respond. The partially written temporary file is removed and the target file
  is never touched, so an interrupted run is always safe to repeat.

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