# Intake → Audit contract

The intake system hands the auditor one **audit package** per client per audit period, as JSON.
The C# definition is `src/ThreePLAudit.Core/Intake/AuditPackage.cs`; `samples/sample-package.json`
is a complete example.

## Submitting

| Call | Use |
|---|---|
| `POST /api/audit` (body: package) | Save and audit in one step. Returns the full run. This is the no-human path. |
| `POST /api/packages` then `POST /api/packages/{id}/audit` | Save now, audit later (or re-audit after the library changes). |
| `threepl-audit audit package.json --out run.json --csv findings.csv --disputes letters/` | Batch or scheduled runs with no server. Exit code 2 means findings exist. |

The same `packageId` can be re-submitted; it replaces the stored package. Finding ids are stable
(rule + entity), so re-runs keep review decisions.

## Conventions

- JSON is camelCase; enums are camelCase strings (`"ltl"`, `"lateShipment"`).
- Dates are `YYYY-MM-DD`; timestamps are ISO 8601 with an offset.
- Money is a decimal in USD.
- Every collection is optional. Rules skip what they have no data for, and the validator warns
  about pairings that are missing (for example invoices without a contract).

## Sections and what intake must normalize

| Section | Source documents | Notes for intake |
|---|---|---|
| `facilities` | 3PL onboarding, licenses, training logs | `state` is a USPS code. `handles` drives which compliance requirements apply: use `Food`, `Hazmat`, `Pharma`, `ControlledSubstances`, or your own values that match library requirements. `credentials[].type` and `training[].type` must match the library's `requirement.recordType`. |
| `warehouseContracts` | Signed rate card | `chargeCode` must use the **same codes** as invoice lines. Map 3PL descriptions to your codes during intake. |
| `warehouseInvoices` | 3PL invoices (PDF/EDI 810) | One line per charge. `reference` = order/receipt/project id when the 3PL gives one (powers duplicate detection). Monthly-minimum lines use code `MINIMUM` (configurable). |
| `activity` | WMS/OMS exports | Quantities per charge code per facility per period, from the **client's** systems. Billed quantities are tested against these. |
| `carrierContracts` | Carrier pricing agreements | Lane `origin`/`destination` use the same notation as bills (3-digit ZIP prefix, full ZIP, or `*`). Include the fuel table and accessorial schedule. |
| `freightBills` | Carrier invoices (EDI 210) | `receivedDate` starts the dispute clock. `dieselPrice` = the index price for the ship week (usually EIA weekly diesel). `requestedAccessorials` from the BOL. |
| `retailerProfiles` | Vendor agreements, routing guides | Dispute window and ASN deadline per retailer. These vary by retailer and are never hard-coded. |
| `deductions` | Retailer remittances, portal exports | One row per deduction, with the PO. |
| `shipments` | BOL, POD, ASN (856) and 997 logs | Empty `podExceptions` means a clean POD. `documentRefs` are carried into dispute letters. |
