# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Shared project documentation

To avoid duplication and drift, the canonical project overview, architecture description, critical constraints (including targeting `netstandard2.0` and `net10.0`), and testing conventions are documented in:

- `.github/copilot-instructions.md`

Claude Code should treat that file as the single source of truth for repository-wide guidance (project goals, build/test commands, architecture, and cross-cutting constraints such as culture usage and API availability).

## Claude-specific guidance

This file is intended only for guidance that is specific to Claude Code (for example, preferences about how to structure refactorings, how much commentary to include in generated code, or how to respond to particular prompts).

When adding or updating content here:

- Do **not** restate the project overview, architecture, or global constraints; instead, reference `.github/copilot-instructions.md` if needed.
- Keep instructions focused on how Claude Code should interact with this repository while respecting the shared constraints defined in the canonical documentation.
