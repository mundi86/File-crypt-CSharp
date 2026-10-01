# Changelog

All notable changes to privateCrypt are documented here.

## [3.0.0] - 2026

The release that closes the unauthenticated-ciphertext gap from 2.0 and makes the
folder operation usable on real data sets.

### Security

- **New container format v3 with HMAC-SHA256 (Encrypt-then-MAC).** v2 used bare AES-256-CBC,
  which is malleable: flipping a ciphertext bit alters plaintext at a predictable position,
  so an attacker with write access to a `.protected` file could modify its contents without
  knowing the password. v3 signs header and ciphertext with HMAC-SHA256.
- **Signature is verified before any plaintext exists.** Decryption fails closed, so a
  tampered file never yields readable output.
- **Wrong password and tampered file produce the same error message.** Distinguishing them
  would leak information to an attacker.
- **PBKDF2 raised from 100,000 to 300,000 iterations** for the master key (SHA-256).
  This is 150× the 2011 original and still under ~1.4 s.
- **Master key derived once per session, not per file.** Per-file keys come from HMAC over
  the master key, domain-separated (`enc`/`mac`) and bound to the file's salt and IV. A
  folder of 500 files costs the same key-stretching time as one file.
- **Password handled as `char[]` and zeroed after use.** The 2.0 code cleared the text box
  but kept the password in an immutable `String`, which cannot be erased.
- **Constant-time comparison** for magic bytes and the HMAC tag.
- **Clearer failure messages.** v2/v1 decryption no longer surfaces raw .NET exceptions such
  as "Padding is invalid and cannot be removed"; malformed input is reported as a corrupt
  file.

### Fixed

- **Existing files are no longer silently overwritten.** If a target already exists, the
  file is skipped and listed in the summary. Previously a folder containing both
  `report.txt` and `report.txt.protected` would decrypt over `report.txt` with no warning.
- **Case-insensitive `.protected` detection.** `"RECHNUNG.PROTECTED"` was not recognised as
  encrypted on a case-insensitive filesystem, causing double encryption.
- **Extension stripping no longer uses `Replace`.** `archiv.protected.snapshot.txt.protected`
  became `archiv.snapshot.txt`. The suffix is now removed at a fixed offset.
- **Directory junctions and symbolic links are skipped** while collecting a folder. Without
  this, `Application Data` (a junction back to the profile) could cause infinite recursion.
- **Quick-Edit temp file is always cleaned up.** If launching the viewer failed, the
  plaintext in `%TEMP%` used to stay behind. Now it is zero-overwritten and deleted, and
  leftovers from a previous crash are removed on the next start.
- **Explorer notified after uninstall.** The missing `SHChangeNotify` left the `.protected`
  icon and context menu entries visible in Explorer until it was restarted.
- **Global exception handlers** show a dialog instead of the Windows crash prompt.
- **`OpenSubKey` result null-checked** before use during install.

### Changed

- **All file operations are streaming.** Memory use no longer scales with file size. The
  2.0 code loaded the whole file into RAM three times over and the executable is x86, so a
  file above roughly 700 MB failed with `OutOfMemoryException`.
- **Writes are atomic.** Output goes to a temporary file and is renamed into place, so an
  interrupted run leaves either the old or the new file intact — never a half-written one.
- **Folder operations run off the UI thread** with a progress bar and a working **Cancel**
  button, instead of freezing the window.
- **Errors are aggregated into one summary.** Previously every failing file popped its own
  message box — a wrong password on a folder produced one dialog per file.
- **Minimum password length is 8 when encrypting** (4 when decrypting, so existing files
  stay accessible).
- **Directory mode argument validated.** An unrecognised second argument now reports the
  valid options instead of silently defaulting to encryption.
- **No-argument launch shows a short usage hint** instead of a misleading message asking
  for administrator rights (installation never required them).
- Strings are consistently German; the English/Ad-hoc messages are gone.

### Added

- **Automated test suite** (119 tests, no external dependencies): round-trips across block
  and buffer boundaries, tamper detection, v1/v2 compatibility, atomic-write guarantees,
  `FileOps` edge cases.
- **Fixed v1 test vectors** in `Tests/PolyAES.Tests/legacy-vectors`, generated with an
  independent implementation of the 2011 algorithm, so the legacy path cannot regress
  silently.
- **`app.manifest`** — DPI awareness, Common Controls v6, explicit `asInvoker` execution
  level.
- **Unified file-operation documentation** in `SECURITY.md`, including the rationale for
  the master-key design.

### Known limitations (unchanged)

- Secure delete cannot physically erase data on SSDs with wear-leveling.
- Passphrase strength, not the iteration count, is the limiting factor.
- v2 and v1 files remain readable forever and carry no integrity protection; re-encrypt
  them to upgrade.

---

## [2.0.0] - 2026

### Added
- Windows 11 context menu integration (top-level verbs via ProgID)
- Dark mode support with automatic detection via Windows registry
- Modern rounded window corners (DWM API)
- `.protected` file icon displayed in Windows Explorer
- Quick-Edit feature: temporary decryption, open file, re-encrypt on close
- Image viewer support for Quick-Edit (shimgvw.dll)
- Inno Setup 6 installer with per-user installation (no UAC/admin required)
- Secure file deletion: zero-overwrite before deletion
- Memory cleanup: cryptographic keys and passwords zeroed after use
- Backward compatibility: v1 legacy files can still be decrypted

### Changed
- Encryption upgraded from Rijndael-256/PBKDF2-SHA1 (2,000 iterations) to **AES-256-CBC / PBKDF2-SHA256 (100,000 iterations)**
- Salt size increased from 32 bytes to 32 bytes (unchanged), IV reduced to 16 bytes (AES block size)
- File magic bytes changed from none to `PCv2` header for format identification
- Context menu now uses HKCU registry (no admin rights needed)
- Install location moved to `%LocalAppData%\privateCrypt\`
- Exe renamed from `File_crypt.exe` to `privateCrypt.exe`

### Fixed
- Title bar icon now loaded explicitly from the running executable
- Shell icon cache refresh after install/uninstall via `SHChangeNotify`

### Security
- PBKDF2 iteration count raised from 2,000 to 100,000 (SHA-256)
- Passwords and derived keys cleared from memory immediately after use
- Random salt and IV generated fresh for every encryption operation

---

## [1.0.0] - 2011

- Initial release
- Rijndael-256 CBC encryption
- PBKDF2-SHA1 key derivation (2,000 iterations)
- Windows Explorer context menu via registry
- Basic file and folder encryption/decryption