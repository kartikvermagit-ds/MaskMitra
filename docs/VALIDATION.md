# MaskMitra 1.0.0 — validation

Built and tested on Windows, October 1, 2026. The executable is unsigned.

Automated checks exercise real Windows OCR on fictional screenshot fixtures, privacy masking, PNG exports and WinForms editor events. They do not read the user's clipboard, register the app's global shortcut or take a screenshot of the user's desktop.

Verified:

- Email, spaced Indian phone, UPI-like text, grouped account numbers and repeated custom phrases.
- Deduplication of overlapping scan results; possible matches remain unhidden until a user decision.
- Actual offline OCR detects two emails, a phone, a UPI-like ID and an account number in the demo.
- Every pixel in a selected mask is replaced, even when an annotation overlaps it.
- PNG round-trip dimensions and redacted pixel values; no source metadata copied into the raster export.
- Source file unchanged after export; same-path overwrite refused.
- Undo for manual edits and detection decisions; Hide matches applies to both demo emails.
- Mouse-drag redaction at the fitted display size maps back to source pixels.
- Stale OCR results cannot populate a different, newly loaded image.
- Capture rectangle calculation supports reverse-direction dragging.
- Editor and export preview rendered and visually inspected; editor also checked at its minimum size.

Known limitations:

- OCR skipped a test line containing `0000 1111 2222 3333`. Overlapping scans did not recover that particular line. Manual Hide is necessary when OCR misses a detail; the app never claims an image is completely private.
- Clipboard paste/copy with another live app, the real desktop-capture hotkey, mixed-DPI multi-monitor capture, and accessibility/screen-reader behavior have not been interactively verified.
- Screenshot and account-number detection do not include guaranteed recognition of faces, names, addresses, QR codes, bank-specific account formats or all languages.
- Windows OCR availability varies by installed language capabilities. Manual editing remains available on recognition failure.
- Code signing and Microsoft Store packaging are not included in this release.

Run the reproducible tests with `scripts/build.ps1 -Test`. Test fixtures and results are synthetic and are written only into the test output directory. GitHub's Windows Server runner uses `-SkipOcrTests` to run core checks without a Windows OCR language. This is explicitly reported as a skip and is not a substitute for the full local OCR tests.
