# Branches and pull requests

Simple rules for a solo project:

* **`main` always works.** It should open in Unity, pass the tests and build.
* **Do new work on a branch**, named for what it does: `feature/terrain-tools`,
  `fix/frogs-stuck-in-wall`, `docs/design-brief`.
* **Merge through a pull request** on GitHub. The automatic checks run on it; merge only
  when they are green. Use "Squash and merge" so each feature is one tidy commit on `main`.
* **Small and often** beats big and rare. A branch that lives more than a week or two gets
  hard to merge.
* Delete the branch after merging (GitHub offers a button).
* Tag milestones on `main`, for example `phase-0` when the Phase 0 gate passes.

The `main` branch is protected on GitHub so the checks must pass before merging.
