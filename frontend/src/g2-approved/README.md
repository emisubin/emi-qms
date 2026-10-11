# G2 approved source integration

The two HTML files and pattern.css are byte-identical copies of the user-approved
2026-10-08 files in docs/design/mobile-input in the original checkout. They are
provenance snapshots, not standalone product entry points (their shell assets
remain owned by the actual application shell).

Run `python3 frontend/src/g2-approved/generate.py` from the repository to extract
the embedded CSS into pc.css and mobile.css. The converter changes selector
scope and cascade priority only; original declaration values and source order
are preserved. Screen selection follows the existing application breakpoint.
Do not tune generated CSS independently to imitate screenshots.

G2OperationsPage / G2OperationEditor use the approved content markup, classes,
table structure and dialogs with the existing permission-filtered API values.
DataEntryDialog adds the existing native modal focus/close lifecycle. Only
integration rules (application style reset, shell spacing, native dialog layer,
long notes and low-height accessibility) live in g2-operations.css.

The product preserves the approved API contracts even where the prototype used
mock shortcuts: shared notes on future dates, server permissions, conflict
protection, range previews and clear-vs-zero semantics. Mock data and mock save
handlers are not executed in the product.
