# Contributing

Please describe the problem or proposed improvement in an issue before a large change. Use fictional or already-redacted screenshots; do not upload personal data, account numbers or secrets.

Build and test on Windows with `./scripts/build.ps1 -Test -Package`. Full tests require Windows OCR language support. `-SkipOcrTests` is for environments without OCR and must not be reported as full OCR validation.

Keep processing offline. Never add silent uploads, automatic clipboard reads, unredacted exports or source-file modification. Preserve manual hiding when OCR fails. Include meaningful regression checks for changes to rendering, detection, coordinate mapping and export.

UI/capture/clipboard changes also need interactive tests. State what you tested, your Windows version, display scaling, and any remaining limitations.
