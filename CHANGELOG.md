# Changelog

All notable changes to privateCrypt are documented here.

## [4.0.0] - 2026

Fixes a cryptographic defect in 3.0, closes two data-loss paths, and rebuilds the
password window.

### Security

- **New container format v4 with proper key separation.** In 3.0, `DeriveFileKeys`
  derived the cipher key and the MAC key from **one single seed**, so both were
  bit-for-bit identical — the AES key *was* the HMAC key. The documentation for
  3.0 described this as domain-separated (`enc`/`mac`); the code contained only
  one label. The cipher itself was unaffected, and no file could be read or
  written that should not have been, but the defense-in-depth that Encrypt-then-MAC
  relies on was absent. In v4 the 64-byte master key is split in half: the first
  32 bytes feed only `HMAC(master[0:32], "…/enc" ‖ salt ‖ iv)`, the second only
  `HMAC(master[32:64], "…/mac" ‖ salt ‖ iv)`. `PolyAES.Tests` asserts the two keys
  differ, and separately asserts that the v3 path still reproduces the identical
  key — a regression in either direction fails the suite.
- **v3 files remain readable.** `DeriveV3Keys` reproduces the 3.0 derivation
  deliberately and unchanged; every file written by 3.0 still opens. The folder
  run upgrades v3 files alongside v2 and v1.
- **Cancellation can no longer be defeated by a missing token.** All crypto entry
  points accept a `CancellationToken` and check it every 64 KiB. Previously a
  cancelled folder run could not interrupt the file currently in progress.

### Fixed

- **Data loss on commit.** `CommitFile` deleted the target and then moved the
  temporary file into place. Between those two calls the target did not exist — a
  crash there lost the file entirely, which for a just-decrypted file means total
  loss. It now uses `File.Replace`, which swaps atomically at filesystem level, with
  the old path as fallback on volumes that do not support it.
- **Folder runs were uncancellable in practice.** The progress bar sat at y = 118
  and the cancel button at y = 140 in a form only 95 px high — both were outside
  the client area and never appeared on screen. The "Cancel" feature was documented
  and present in the code, but not reachable. Progress and cancel now occupy a card
  that replaces the input card at the same position, so the window size does not
  change mid-run.
- **Decryption of v2 and 2011 files loaded the entire file into memory.** Both
  legacy paths used `File.ReadAllBytes` plus `TransformFinalBlock`, i.e. the file
  was held three times over. The executable is x86, so anything above roughly
  700 MB failed with `OutOfMemoryException` — contradicting the documented
  "memory use does not scale with file size". Both paths now stream from disk, and
  a test decrypts a 48 MB legacy file while watching the working set.
- **An unreadable directory aborted the whole folder run.** `CollectFiles` caught
  `UnauthorizedAccessException` and `DirectoryNotFoundException` but not
  `IOException`, which is what an interrupted network share or an over-long path
  raises. A single such directory failed the entire run.
- **Orphaned Quick-Edit leftovers were never actually deleted.** The cleanup probe
  opened each file with `FileShare.None` and then called `SecureDelete` *inside*
  that `using` block; the exclusive handle made its own delete fail, and the
  exception was swallowed. Plaintext could therefore survive indefinitely.
- **Event handler left in the Quick-Edit path** referenced a progress handler that
  targeted the folder-only layout.

### Changed

- **New interface.** Fluent-style password window: card on a tinted background,
  Segoe UI Variable where available, rounded corners matched to the Windows
  corner setting, accent-coloured primary button with hover and pressed states,
  custom-drawn password field with a focus border, and a custom progress bar —
  the stock WinForms bar cannot be coloured and leaves a bright stripe in dark
  mode. The action button now spans the full card width, so its label can no
  longer be truncated regardless of DPI scaling.
- **Format shown in the UI is honest per file.** The footer states whether
  integrity protection is active, and names the detected format.
- **Password can be revealed briefly** via a link, and re-hides when the field
  loses focus — a mistyped password no longer costs a full restart including the
  1.4 s key derivation.
- **Folder upgrades now include PCv3.** Previously only v2 and the 2011 original
  were refreshed.
- **File and folder paths are shown as a tooltip** when the label truncates a long
  name.

### Added

- **`docs/MIGRATION.md`** — how to bring 2.0, 3.0 and 2011 files onto PCv4, what
  the defect actually was, and what it did and did not affect.
- **Test suite grown from 119 to 142 checks**, including a v3 compatibility test
  built from an independent reimplementation of the 3.0 key derivation (including
  its shared-key flaw), a memory-growth test for the legacy paths, and cancellation
  tests.
- **Installer**: `uninsdeletekey` entries for the `DefaultIcon` and `shell\open`
  subkeys, which `[Registry]` did not cover.

### Verified

- 142/142 unit tests pass.
- v3, v2 and 2011 files decrypt correctly under 4.0.
- A 48 MB legacy file decrypts without the working set growing with file size.

---

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
  the master key, bound to the file's salt and IV. A folder of 500 files costs the same
  key-stretching time as one file.
  > **Corrected in 4.0:** this entry originally claimed the subkeys were
  > domain-separated (`enc`/`mac`). They were not — see the 4.0 entry above.
- **Password handled as `char[]` and zeroed after use.** The 2.0 code cleared the text box
  but kept the password in an immutable `String`, which cannot be erased.
- **Constant-time comparison** for magic bytes and the HMAC tag.
- **Clearer failure messages.** v2/v1 decryption no longer surfaces raw .NET exceptions such
  as "Padding is invalid and cannot be removed"; malformed input is reported as a corrupt
  file.

### Fixed

- **Truncated labels in the password window.** The button read "entschluess"
  instead of "entschlüsseln". The text needs 78 px in Segoe UI 9pt while the
  button only offered 75 px after `FlatStyle` padding and auto-scaling. Buttons
  are now 118 px and the form was widened from 340 to 400 px. This also fixed
  the format description and the window title with long file names.
- **File version raised to 3.0.0.0.** It still reported 2.0.0.0 while the
  registry entry and the installer already said 3.0, so 2.0 and 3.0 could not
  be told apart from the file properties alone — and the old version ended up
  being tested by mistake.
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
- **An unreadable subdirectory no longer aborts the whole folder run.**
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

- **Automatic format upgrade when decrypting a folder.** Files in an old format
  (v2 or the 2011 original) are re-encrypted immediately after decryption, this
  time in the current format with an HMAC signature. One pass leaves the whole
  folder on v3, and the summary reports how many files were upgraded and from
  which format. This matters because v2 and v1 files carry **no integrity
  protection whatsoever** — after the upgrade they do.
  The upgrade deliberately leaves **no plaintext behind**: the goal is to end up
  encrypted, not decrypted. Single-file decryption still keeps the old format,
  since migration only makes sense for a whole folder.

- **`--version` (and `/version`)** prints the running version, the active container
  format, the KDF parameters and the install path. Makes it immediately visible which
  version is actually running — useful because the Explorer context menu always points
  at `%LocalAppData%\privateCrypt`, where an older copy may sit.
- **Automated test suite** (119 tests, no external dependencies): round-trips across block
  and buffer boundaries, tamper detection, v1/v2 compatibility, atomic-write guarantees,
  `FileOps` edge cases.
- **`Tests/e2e/Invoke-PrivateCryptTests.ps1`**: drives the real executable through Windows
  UI Automation (25 checks). Its two documented pitfalls — non-ASCII in `.ps1` files under
  PowerShell 5.1, and never taking the first button from a UI Automation hit list — are
  explained in `docs/TESTING.md`, since both caused a wild goose chase during development.
- **`docs/TESTING.md`**: what each test layer covers, how to run it, and an explicit list
  of what is *not* covered.
- **Fixed v1 test vectors** in `Tests/PolyAES.Tests/legacy-vectors`, generated with an
  independent implementation of the 2011 algorithm, so the legacy path cannot regress
  silently.
- **`app.manifest`** - DPI awareness, Common Controls v6, explicit `asInvoker` execution
  level.
- **`FileOps.cs`** - file-system helpers extracted from `Form1.cs` and thereby testable.
- **AppId in the installer.** Without it, Inno derives the identity from name+version and
  creates a second entry in "Apps & Features" on every version change instead of upgrading.
- **Unified file-operation documentation** in `SECURITY.md`, including the rationale for
  the master-key design.

### Verified

- 119/119 unit tests pass.
- 25/25 end-to-end checks pass against the built executable: encryption, round-trip,
  **a bit flip in the ciphertext produces no plaintext at all**, wrong password, too-short
  password, recursive folder encryption with `.db` skip and no double encryption, and the
  overwrite protection.

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