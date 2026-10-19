---
name: gstack-investigate
description: Investigate a reproducible Kalyeah failure with evidence, one hypothesis at a time, a minimal root-cause fix and meaningful regression verification.
---

# gstack systematic debugging — Kalyeah port

Compact adaptation of upstream `investigate/SKILL.md`, phases 1–5.
See `../sources.lock.json` and `../licenses/gstack-LICENSE`.
Use the existing tools and specialist, not gstack's host hooks or browser runtime.

1. Collect the symptom, build/device, scene, player count, logs and exact steps.
   Read the path and its callers; inspect recent changes and existing audits/tests.
2. Reproduce before proposing a fix. Distinguish sim ticks, render/physics frames,
   asynchronous delivery and actual elapsed time. Compare normal and failing cases.
3. Name one falsifiable root-cause hypothesis. Test it with a targeted assertion,
   temporary instrumentation or deterministic repro. Record what disproves it.
4. If a hypothesis fails, return to evidence. After three failures, reassess the
   layer and assumptions; don't pile speculative fixes on top of one another.
   Ask only when missing information prevents useful independent investigation.
5. For an authorized fix, change the root cause at the responsible shared layer.
   Preserve each caller's guards, authority, cancellation and recovery semantics.
6. Add meaningful regression coverage where warranted: demonstrate failure before
   the fix and success after. For flaky tests, control the clock/animation/input
   source or await the observable state; increasing sleeps is not a diagnosis.
7. Reproduce the original scenario again, run required GUT/import/build checks,
   and report symptom, cause, smallest fix, evidence and unresolved device limits.

Do not change balance, protocol, assets, thresholds or release versions as a shortcut.
Keep the investigation within its authorized scope; leave unrelated cleanup out.
A browser or headless result cannot establish Android GPU/thermal/input behavior.
