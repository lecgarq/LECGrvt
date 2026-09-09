# Pilot attempt 01 — failed infrastructure, zero setter evidence

Run: 20260909T050029-11655fc4a2704bf0864a25c26dd83c41.
TRX: changed-value-pilot-01.trx. It reports zero executed tests despite exit code 0.
The adapter's no-matching-tests message is misleading here: a provenance file and
the first disposable copy exist, and Windows recorded an unhandled process crash.
No receipt exists and no setter may be credited. Remaining seven never ran.

Windows Application event 1026, 2026-09-08 23:00:36 local: Revit.exe .NET 10.0.11;
stack begins ParamDef.getStorageType -> Parameter.get_StorageType ->
ChangedValuePilot.State -> deferred LINQ iterator -> System.Text.Json serialization.
Event 1000: exception 0xc0000005, coreclr.dll 10.0.1126.37416,
report f0988fc3-9a45-4177-b087-21489a4c7293. Journal journal.0983.txt also records
third-party add-in exceptions while opening/detaching; these do not establish the
cause of the storage-type crash.

Corrective hypothesis, not yet verified: Parameter wrappers were buffered/sorted
after their ParameterSet's enumeration, with delayed native reads during JSON
serialization. Read immutable values eagerly while retaining the ParameterSet;
check wrapper validity/definition and serialize only plain data. Do not drop hidden
parameters or silently downgrade the restoration check. Add a pre-write checkpoint
so an abrupt native exit cannot be mistaken for a never-started test.

Retry remains the same eight policies, model, candidate order, comparators,
restoration requirements and preregistered 3–6 / 18–34 estimates. It uses a new
run directory, fresh copies and a separate TRX. Ledger unchanged at 618/805.
