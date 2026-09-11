# How: Builder export of packages + Harness UI polish

**Status: implemented** (Builder export + Harness chrome). Push separately when you ask.

---

## A) Builder export of packages — done

- `AuthoredUnitOpenPackageExporter` → `.daxalgostrategy` (spec + `.cs`)
- Builder **Export open package…** on Build + Validate (`CanExportOpenPackage`)
- Tests: `AuthoredUnitOpenPackageExporterTests` (export → install → register)

## B) Harness UI polish — done

- Title: `Harness · {unit} · {book}`
- Menu: **Harness · Paper…**; activity log source `Harness`
- Header: **HARNESS** + context strip (unit · book · mode · LIVE blocked · Validate cue)
- Validate→Paper status copy mentions Open Harness

Out of scope still: Marketplace upload, signing, merging Validate into Runner AXAML.
