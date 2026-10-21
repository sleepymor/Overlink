## 2026-10-20 — Verify v0.18 main-menu implementation plan
- [x] Read the applicable workflow instructions and inspect the current plan
- [x] Verify scene, package, assembly, API, and build-settings assumptions against the repository
- [x] Identify plan defects that would block OpenCode or Unity execution
- [x] Perform a final consistency check and report verified findings

**Outcome:** Plan verification completed; corrections remain pending because this was a read-only verification request.

## 2026-10-20 — Revise v0.18 main-menu implementation plan
- [x] Read the repair/edit workflow and inspect the current plan state
- [x] Resolve the assembly-definition contradiction and correct Unity API guidance
- [x] Make scene registration deterministic and validated without direct ProjectSettings edits
- [x] Complete menu navigation and back-navigation requirements for the enabled scene inventory
- [x] Add serialized-reference validation and stronger acceptance checks
- [x] Re-audit the revised plan and report the final status

**Outcome:** Revised English plan independently reviewed by Mneme; prior blockers resolved and remaining documentation issues corrected.

## 2026-10-21 — Commit and release Unity CI workflow as v0.18.1
- [ ] Record the current repository state and identify the intended Unity CI workflow patch
- [ ] Verify the workflow syntax/content and run applicable repository checks
- [ ] Stage only the intended CI workflow change and reconcile the commit diff
- [ ] Commit the patch, create tag `v0.18.1`, and push the commit and tag
- [ ] Verify the remote branch and tag point to the expected commit
