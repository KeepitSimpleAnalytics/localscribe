# Systematic Development Protocol

You MUST follow these protocols for ALL coding tasks. These prevent incomplete implementations and wasted rework.

## Specialized Agents Available

Use these agents for their designated purposes:

| Agent | Invoke When | Purpose |
|-------|-------------|---------|
| `@planner` | Before ANY implementation | Decompose tasks into complete checklists |
| `@researcher` | Uncertain about APIs/libraries | Verify before coding, prevent hallucinations |
| `@ui-expert` | Designing any user interface | Simplify layouts, avoid bloat and complexity |
| `@ui-completeness` | After backend changes | Ensure UI reflects all backend work |
| `@code-reviewer` | Before committing | Catch silent fallbacks and quality issues |
| `@verifier` | Before declaring "done" | Confirm all tasks completed |

## Workflow with Agents

```
1. User requests feature
   ↓
2. @planner decomposes into task list
   ↓
3. Present plan → WAIT for approval
   ↓
4. @researcher verifies any uncertain APIs
   ↓
5. @ui-expert designs clean interface (if UI involved)
   ↓
6. Implement systematically
   ↓
7. @ui-completeness audits frontend coverage
   ↓
8. @verifier confirms completion
   ↓
9. @code-reviewer before commit
```

## Protocol 1: Decomposition (MANDATORY)

Before writing ANY code, invoke @planner or output a complete task breakdown:

```
## Task Decomposition: [Feature Name]

### Backend/Logic
- [ ] ...

### Frontend/UI  
- [ ] ...

### Integration Points
- [ ] ...

### Verification
- [ ] ...

Estimated complexity: Low/Medium/High
Clarifications needed: [list or "None"]
```

**WAIT for user approval before implementing.**

## Protocol 2: Clarification

STOP and ask when you encounter:
- Multiple valid interpretations
- Missing information affecting architecture
- Trade-offs requiring user preference
- Unclear scope boundaries

**Never assume. Never proceed with ambiguity.**

## Protocol 3: No Fallbacks

PROHIBITED without explicit approval:
- Substituting different libraries than specified
- Using deprecated APIs because current ones "seem complex"
- Implementing simpler versions than requested
- Skipping features "to add later"
- Mock/placeholder implementations

When blocked, present options and wait for decision.

## Protocol 4: Research

When uncertain about APIs, libraries, or patterns:
1. Invoke @researcher or state what needs verification
2. Check current documentation
3. Report findings BEFORE implementing

## Protocol 5: Verification

After implementing, invoke @verifier or verify against the original task list:

```
## Verification

### Completed
- [x] Task 1
- [x] Task 2

### Integration Check
- [ ] UI reflects backend changes?
- [ ] All entry points updated?
- [ ] Error states handled?

### Remaining
- None / [list with reasons]
```

## Anti-Patterns to AVOID

| Anti-Pattern | Prevention |
|--------------|------------|
| Eager implementation | Always decompose first with @planner |
| Bloated/complex UI | @ui-expert designs simple interfaces |
| Silent substitution | @code-reviewer catches these |
| Partial delivery (backend done, forgot UI) | @ui-completeness audits |
| Hallucinated APIs | @researcher verifies |
| "Done!" with missing pieces | @verifier confirms |
