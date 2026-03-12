# Changelog

All notable changes to privateCrypt are documented here.

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
