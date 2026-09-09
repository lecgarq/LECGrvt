# Pilot protocol amendment — before attempt 04

The original preregistration and yield estimates remain unchanged and hash-pinned.
Attempt 01 crashed in Parameter.StorageType during the pre-write snapshot.
Attempt 02 was accidentally launched after a failed compile; it was immediately
cancelled before fixture setup (no run folder), and no result is admissible.
Attempt 03 used the corrected, successfully built eager reader. It safely refused
a parameter with no Definition on element
c3ebb219-e63f-4383-98ab-605fca28cead-0014c9fd, before any setter. Its first case
failed infrastructure, the remaining seven were aborted, and cleanup was verified.

A complete parameter snapshot is not available through this API on this sample.
Do not access StorageType for an undefined parameter, do not label that slot as
unchanged, and do not equate an unchanged placeholder with verified hidden state.
Instead record each unavailable slot by element identity + enumeration position.
The snapshot verifies all *readable* parameter values, element identities, target
property and warning identities. This is explicitly narrower observable evidence.
The reader refuses any other snapshot error; a changed readable snapshot still
aborts the pilot. It does not silently skip exceptions.

Isolation uses the user-approved fallback already selected before attempt 01:
a separate disposable copy for every operation, closed without saving and never
reused. `rollback_verified` means the listed observable checks only; it is not a
certificate of complete model restoration. Unreadable slots and other unobserved
internal/geometry state prevent promoting this result to a copy-reuse guarantee.
Setter validation still requires a committed changed value matching its requested
candidate, observable rollback verification, and verified unsaved-copy cleanup.

No setter/model substitutions, new fixtures, family writes, broader candidate
sets, relaxed value comparators, or revised expected yields are introduced.
The full 70 remain blocked on reporting/review of this eight-case pilot.
