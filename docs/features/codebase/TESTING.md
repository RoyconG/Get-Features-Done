# Testing Patterns

**Analysis Date:** 2026-02-20

## Test Framework

**Runner:**
- Not detected - no test framework present in codebase
- Testing approach: manual validation and integration testing only

**Assertion Library:**
- Not applicable - no automated tests

**Run Commands:**
```bash
# No npm test or test scripts configured
# Manual testing via CLI commands
node get-features-done/bin/gfd-tools.cjs <command> <args>
```

## Test File Organization

**Location:**
- Not applicable - no test files present
- Testing strategy: functional/integration testing through manual CLI invocation

**Naming:**
- Not applicable

**Structure:**
```
get-features-done/
├── bin/
│   └── gfd-tools.cjs          # Single CLI executable (untested)
├── templates/
│   └── codebase/              # Document templates (no tests)
├── workflows/                 # Markdown workflows (no tests)
└── references/                # Reference docs (no tests)
```

## Test Structure

**Suite Organization:**
- Not applicable - no formal test structure

**Patterns:**
- Manual verification through command-line execution
- Testing approach: run actual commands and verify file outputs
- Example validation:
  - Create feature with `gfd-tools.cjs new-feature`
  - Verify `docs/features/<slug>/FEATURE.md` created
  - Check frontmatter parsing by running `gfd-tools.cjs frontmatter get`

## Mocking

**Framework:**
- Not applicable - no test framework

**Patterns:**
- File system mocking not used
- Network mocking not used
- Direct integration testing against actual file system

**What to Mock (if tests were added):**
- File system operations (`fs.readFileSync`, `fs.writeFileSync`)
- Child process execution (`execSync`)
- Git commands (`git log`, `git add`, `git commit`)
- Environment variables in config loading

**What NOT to Mock (if tests were added):**
- Core logic: YAML/frontmatter parsing
- Git command construction and result handling
- Configuration merging with defaults

## Fixtures and Factories

**Test Data:**
- Not applicable - no test fixtures present
- If tests were added, fixtures would be:

```javascript
// Factory functions
function createTestConfig(overrides = {}) {
  return {
    model_profile: 'balanced',
    commit_docs: true,
    search_gitignored: false,
    research: true,
    plan_checker: true,
    verifier: true,
    parallelization: true,
    auto_advance: false,
    path_prefix: 'docs/features',
    team: { members: [] },
    ...overrides
  };
}

// Sample YAML frontmatter
const sampleFrontmatter = `---
feature: test-feature
plan: 01
type: execute
---`;

// Sample markdown file
const sampleFeatureMd = `---
slug: my-feature
name: My Feature
status: backlog
---

## Description
Test feature`;
```

**Location:**
- If implemented: `tests/fixtures/` for shared test data
- Factory functions would live in test file or `tests/factories/`

## Coverage

**Requirements:**
- Not enforced - no test framework configured
- Current coverage: 0% (no automated tests)
- Recommendation: Add unit tests for critical functions before new feature development

**Configuration:**
- No coverage tool configured
- Would use: c8 or built-in test runner coverage

**View Coverage:**
```bash
# Not currently available
# If implemented: npm run test:coverage
```

## Test Types

**Unit Tests:**
- Would test: `extractFrontmatter()`, `reconstructFrontmatter()`, parsing logic, config loading
- Should mock: file system, child processes
- Location: would co-locate with source in `bin/gfd-tools.test.cjs`

**Integration Tests:**
- Would test: command execution flow, file system interactions, state management
- Would use: temporary directories for test output
- Would verify: file creation, content accuracy, git command execution

**E2E Tests:**
- Would test: full workflows (new-project → new-feature → plan-feature)
- Would create: temporary git repositories
- Would verify: complete feature lifecycle end-to-end

## Common Patterns (If Tests Were Added)

**Async Testing:**
```javascript
it('should handle async file operations', async () => {
  const content = await fs.promises.readFile(testFile, 'utf-8');
  expect(content).toContain('expected text');
});
```

**Error Testing:**
```javascript
it('should handle file not found', () => {
  const result = safeReadFile('nonexistent.txt');
  expect(result).toBeNull();
});

it('should throw on missing required arg', () => {
  expect(() => error('test error')).toThrow();
});
```

**File System Testing (if tests were added):**
```javascript
// Would need to mock fs operations
vi.mock('fs', () => ({
  readFileSync: vi.fn(),
  writeFileSync: vi.fn(),
  existsSync: vi.fn(),
  readdirSync: vi.fn()
}));

// Mock git execution
vi.mock('child_process', () => ({
  execSync: vi.fn()
}));
```

## Testing Recommendations

**Critical Areas Needing Tests:**
1. **Frontmatter parsing** (`extractFrontmatter()`, `reconstructFrontmatter()`)
   - Files: `gfd-tools.cjs` lines 79-216
   - Risk: YAML parsing errors break feature/plan creation
   - Approach: Unit tests with various YAML structures

2. **Feature discovery** (`findFeatureInternal()`, `listFeaturesInternal()`)
   - Files: `gfd-tools.cjs` lines 249-329
   - Risk: Incorrect plan/summary counting, state misalignment
   - Approach: Integration tests with temp directories

3. **Git operations** (`execGit()`)
   - Files: `gfd-tools.cjs` lines 68-75
   - Risk: Silent failures in git commands, lost commits
   - Approach: Mock execSync, test error handling

4. **CLI argument parsing**
   - Files: `gfd-tools.cjs` lines 18-26 (flags), 1459-1723 (routing)
   - Risk: Wrong command execution, arg misinterpretation
   - Approach: Integration tests with actual CLI calls

## Current Testing Approach

**Manual/Integration:**
- Commands run via CLI: `node gfd-tools.cjs [command] [args]`
- Output verified manually or through next command
- File system checked with `ls`, `cat`, `git status`

**Recommended Next Step:**
- Add Vitest configuration
- Write unit tests for parsing functions
- Add integration test suite for CLI commands
- Mock file system for fast test execution
- Target: 70%+ coverage for `gfd-tools.cjs`

---

*Testing analysis: 2026-02-20*
*Update when test patterns change*
