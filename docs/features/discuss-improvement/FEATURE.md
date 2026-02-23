---
name: Discuss Improvement
slug: discuss-improvement
status: done
owner: Conroy
assignees: []
created: 2026-02-21
priority: high
depends_on: []
---
# Discuss Improvement

## Description

Enhances the `/gfd:discuss-feature` workflow to accept additional context (tickets, specs, feature documents) before the discussion begins. Users can provide context via a free text prompt or by passing a file path as a second argument. This context feeds directly into gray area analysis, producing more targeted discussion topics.

## Acceptance Criteria

- [x] When no file path argument is provided, a skippable free text prompt appears after the "DISCUSSING" banner asking for additional context
- [x] When a file path is provided as a second argument to `/gfd:discuss-feature`, the file is read and the free text prompt is skipped
- [x] Provided context is used to inform gray area analysis (step 5), producing more relevant discussion topics
- [x] Provided context is saved to the Notes section of FEATURE.md under a "Source Context" heading (raw for short text, summarized for long text — Claude's discretion)

## Tasks

[Populated during planning. Links to plan files.]

## Notes

### Implementation Decisions

- **Input method:** Free text prompt (skippable) OR file path as second argument; file path skips the prompt
- **Placement:** After status transition and banner display, before gray area analysis
- **Context usage:** Feeds into gray area analysis to tailor discussion topics
- **Persistence:** Saved to FEATURE.md Notes section under "Source Context" heading
- **Claude's discretion:** Whether to save raw text or summarize based on length

## Decisions

- **File read failure handling:** Falls through to interactive prompt rather than hard-failing. Provides graceful degradation when FILE_PATH is invalid.
- **SOURCE_CONTEXT threshold:** ~500 words for raw vs. summarized in FEATURE.md Notes — left to Claude's discretion per acceptance criteria.
- **Empty Source Context:** The `### Source Context` heading is entirely omitted from Notes when SOURCE_CONTEXT is empty (no blank section written).
- **Step placement:** New Step 5 placed between banner display (Step 4) and analysis (Step 6) to group context-gathering logically before analysis begins.

## Blockers

---
*Created: 2026-02-21*
*Last updated: 2026-02-21*
