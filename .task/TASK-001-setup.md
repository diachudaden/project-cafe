# TASK-001: Establish GitHub Copilot workflow for Cafe CR

- Status: Completed
- Goal: Create a maintainable, task-driven GitHub Copilot workflow that matches the repo’s existing payment module and project conventions.
- Rationale: The repository already contains project-specific product and payment rules, but it lacked an explicit Copilot workflow, task backlog, and repository rules. The workflow should make future implementation predictable and reviewable.

## Specification references

- [../spec.md](../spec.md)
- [../docs/payments/SPEC.md](../docs/payments/SPEC.md)
- [../README.md](../README.md)

## Rule references

- [.rule/architecture.md](../.rule/architecture.md)
- [.rule/coding-style.md](../.rule/coding-style.md)
- [.rule/business-rules.md](../.rule/business-rules.md)
- [.rule/security.md](../.rule/security.md)
- [.rule/testing.md](../.rule/testing.md)

## Dependencies

- None

## Scope

- Add repository-wide Copilot instructions.
- Add path-specific instruction files for backend, frontend, and tests.
- Add reusable prompt files for planning, implementation, review, and verification.
- Add a minimal task backlog and rule set that maps to the current project.

## Explicit exclusions

- Do not replace existing payment or project documentation.
- Do not add unnecessary GitHub automation or speculative task files not tied to the repo.
- Do not duplicate the full business specification across many instruction files.

## Acceptance criteria

- The repo has a clear repository-level instruction file for Copilot.
- Relevant instructions and prompts are organized under [.github/](../.github).
- The task workflow is stored in [.task/](../.task) and references real project requirements.
- The rule set in [.rule/](../.rule) exists and describes real architecture and constraints.
- The workflow references actual repository structure and spec files instead of placeholders.

## Verification

- Validate the working tree and file layout.
- Check for repository-level duplication issues and missing references.
- Confirm the files are in the intended structure and reference actual project content.

## Implementation notes

This setup intentionally uses the repo’s actual docs and module structure instead of inventing a new, unsupported workflow. The repository does not currently include a live GitHub Actions or agent directory, so the maintainable solution is the supported Copilot instruction/prompt pattern plus explicit task and rule references.
