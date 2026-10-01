# MaskMitra

**A little privacy. Before you share.**

A portable Windows screenshot editor that suggests private details to hide, lets you annotate the image, and exports only the rendered pixels. All image processing and text recognition run on your PC.

## Start

1. Run **MaskMitra.exe**. No installer or administrator access is needed.
2. Choose **Capture**, **Open image**, or **Paste image**. **Try demo** provides a fictional example.
3. Review suggestions in the right panel. Select **Hide**, **Keep**, or **Hide matches** for matching detected values. **Hide suggested** hides undecided suggestions and preserves items you explicitly kept.
4. Use **Hide** and drag a rectangle over anything missed. Add arrows, highlights, text or numbered steps as needed.
5. Choose **Review & export**, check the final image, then **Copy clean image** or **Save PNG**.

![MaskMitra editor](../assets/preview.png)

## Features

- Area capture across the Windows virtual desktop; Escape cancels selection.
- Paste an image or drag an image file into the app. PNG, JPEG and BMP are supported inputs.
- Offline suggestions for email addresses, phone-like numbers, UPI-like IDs, and possible account/card/ID numbers.
- Custom text matching after OCR; separate phrases with `;`. Terms stay in memory for this app session.
- Matching detected occurrences can be hidden together. Names and addresses are **not** automatically recognized as sensitive.
- Solid, opaque redaction drawn after all annotations, so annotations cannot cover up the mask.
- Arrows, translucent highlights, text notes, numbered steps and undo.
- Mouse-wheel zoom, middle-button pan and Fit. Right-click a suggestion box to select its list entry.
- A final preview before copying or saving. Undecided suggestions remain visible, and the preview states their count.
- PNG export has no original text layer, undo history or copied source metadata.
- Original files are not edited. Saving over the loaded source path is refused.

## Keyboard

| Shortcut | Action |
|---|---|
| Ctrl+Shift+M | Capture an area while MaskMitra is open; unavailable if another app owns that shortcut |
| Ctrl+O | Open image |
| Ctrl+V | Paste image when not editing a text field |
| Ctrl+Z | Undo image edit when not editing a text field |
| Ctrl+E | Review and export |
| Escape | Cancel capture |

## Requirements and limits

Windows 10/11 with .NET Framework 4.8 (included on many current Windows installations). Offline scanning also needs an installed Windows OCR language; English is preferred when available, otherwise the user's supported profile language is used. If unavailable, add the OCR capability in Windows language options. The app itself does not download language data. Manual hiding and annotation work without a successful scan.

OCR is a helper, **not a guarantee**. It can miss entire lines, misread characters, or suggest harmless numbers. In local testing, Windows OCR skipped one sample line containing repeated digits (`0000 1111 2222 3333`), even with overlapping scans. Always visually inspect the whole image; use Manual Hide for missed details. Angled text beyond 3 degrees pauses suggestions to avoid inaccurate masks. Small text on very large images can be missed because OCR works on scaled copies up to 3,200 pixels per side.

Maximum input: 24 megapixels, 16,000 pixels per side, 80 MB for image files. Only the first frame of animated images is used. Export is a flattened, opaque PNG; transparency is flattened on white. The app edits one image at a time, and opening another image discards the current editing session. Up to 60 edit snapshots are retained for Undo. Original images, recognized text and edits remain in process memory until replaced or the app exits; this is not a secure memory-erasure tool.

There is no account, upload endpoint, analytics, automatic clipboard monitoring or screenshot-history folder. Windows clipboard history/sync, if enabled by the user, may retain images that the user explicitly copies. MaskMitra does not alter those system settings.

## Windows download warning

This release is **unsigned**. A downloaded copy can show Windows SmartScreen's “Windows protected your PC” warning. A verified publisher signature is not yet included. Do not disable Windows security to use the app. If you independently trust this build and its source, Windows may offer **More info → Run anyway**; managed policies may not allow it.

## Source and build

Source code and the app manifest are in `src`; the build script is in `scripts`. Build on Windows with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Test -Package
```

The build uses the Windows .NET Framework C# compiler and installed Windows Runtime metadata. No external packages or network access are required. The self-test uses fictional fixtures, writes test artifacts to a temporary directory, and does not read the user's clipboard or capture the live desktop.

## Validation

See [VALIDATION.md](VALIDATION.md) for what was tested and what remains unverified. GitHub Windows Server CI uses `-SkipOcrTests`; full OCR checks run separately on local Windows. This app is not published to the Microsoft Store.
