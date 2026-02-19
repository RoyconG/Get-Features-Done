# Coding Conventions

**Analysis Date:** 2026-02-20

## Naming Patterns

**Files:**
- kebab-case for all files: `gfd-tools.cjs`, `gfd-planner.md`, `gfd-executor.md`
- Markdown files for agent definitions (uppercase): `PLAN.md`, `SUMMARY.md`, `FEATURE.md`
- Node.js CommonJS: `.cjs` extension used for `gfd-tools.cjs`

**Functions:**
- camelCase for all functions: `parseIncludeFlag()`, `safeReadFile()`, `loadConfig()`, `extractFrontmatter()`
- Descriptive function names indicating operation: `findFeatureInternal()`, `listFeaturesInternal()`, `execGit()`
- Async functions: named clearly without special prefix: `main()` (runs async operations)
- Handlers: named with imperative action verbs: `cmdFindFeature()`, `cmdInitNewProject()`, `cmdStateLoad()`

**Variables:**
- camelCase for variables: `content`, `result`, `filePath`, `features`, `config`
- UPPER_SNAKE_CASE for constants: `MODEL_PROFILES`, `tmpDir`, `defaults`
- Object keys: snake_case in configuration and state objects: `model_profile`, `commit_docs`, `search_gitignored`, `auto_advance`

**Types:**
- PascalCase for object shape names in comments and variable names: `Feature`, `Config`, `State`
- No formal TypeScript or interface declarations (plain JavaScript objects)
- Keys in JSON/YAML structures: snake_case: `feature_md`, `incomplete_plans`, `key-decisions`

## Code Style

**Formatting:**
- No formatting tool configured (Prettier/ESLint not used)
- Consistent manual formatting observed: 2-space indentation
- Single quotes for strings consistently used: `'use strict'`, `fs.readFileSync(filePath, 'utf-8')`
- Semicolons required on all statements
- Line length: approximately 100-120 characters typical, no hard limit enforced

**Linting:**
- No linting tool configured
- Code follows basic JS best practices: early returns, guard clauses, clear variable names
- Comments included selectively for complex logic (`// ─── Section Headers ──────────────────────`)

## Import Organization

**Order (CommonJS):**
1. Built-in Node.js modules: `const fs = require('fs')`, `const path = require('path')`
2. Child process and system utilities: `const { execSync } = require('child_process')`
3. Inline object destructuring for clarity

**Module Structure:**
- No explicit grouping with blank lines
- Imports clustered at top of file
- Everything uses CommonJS `require()` (no ES6 modules)

**Path Handling:**
- `path.join()` for cross-platform file paths
- Absolute paths preferred over relative: `path.join(cwd, 'docs', 'features')`
- `__dirname` and `require('os').tmpdir()` for system paths

## Error Handling

**Patterns:**
- Try/catch for file system and command execution operations
- Silent failures with fallback values: `safeReadFile()` returns `null` on error
- `execSync()` wrapped in try/catch with structured error return: `{ exitCode, stdout, stderr }`
- `process.exit(1)` for CLI error termination

**Error Types:**
- Simple string error messages: `error('feature slug required')`
- JSON error output for structured CLI responses: `console.error(JSON.stringify({ error: message }))`
- No custom Error classes defined (plain Error inheritance not used)

**Validation:**
- Guard clauses at function start: `if (!slug) error('feature slug required')`
- Null coalescing with defaults: `config[key] || 'default-value'`

## Logging

**Framework:**
- Plain JavaScript `console.log()` and `console.error()` only
- No logging library (winston, pino, bunyan)

**Patterns:**
- `console.log()` for standard output (JSON-structured)
- `console.error()` for errors and exit signals
- JSON output wrapper: `JSON.stringify(result)` for consistency
- No structured logging with context objects

**Usage:**
- Output only in `output()` and `error()` functions
- Avoid logging in internal helper functions
- Log at CLI boundary (command execution)

## Comments

**When to Comment:**
- Section headers using visual separators: `// ─── Feature Operations ──────────────────────────────────────────────`
- Explain non-obvious regex patterns or parsing logic
- Mark related functions: `// ─── Frontmatter Parsing ──────────────────────────────────────────────`

**JSDoc/TSDoc:**
- Not used (no formal documentation for functions)
- Code is self-documenting through clear naming

**TODO Comments:**
- Minimal TODOs found in agent markdown definitions
- Not tracking TODOs in source code strings
- Focus on working implementation, not placeholder comments

## Function Design

**Size:**
- Functions generally 10-80 lines
- Helper functions extracted for distinct operations: `extractFrontmatter()`, `reconstructFrontmatter()`, `spliceFrontmatter()`
- Complex logic split into logical blocks with section comments

**Parameters:**
- Max 3-4 parameters typical
- Options objects used for configuration: `loadConfig(cwd)` returns merged object
- Command handlers follow pattern: `cmd*(cwd, ...args, raw)`
- Destructuring used moderately in loops: `for (const [key, value] of Object.entries(obj))`

**Return Values:**
- Explicit return statements required
- Guard clauses with early return for error conditions
- Structured return objects: `{ exitCode, stdout, stderr }`, `{ valid: true, ... }`
- Null returns for "not found" states: `return null` (from `safeReadFile()`)

## Module Design

**Exports:**
- Single entry point per file (monolithic `gfd-tools.cjs`)
- No modular exports; functions defined then called in `main()` router
- Node.js entry point: `main()` at end of file

**CLI Router Pattern:**
- Switch statement routes command to handler function: `switch(command) { case 'state': ... }`
- Subcommands handled recursively in handler: `switch(subcommand) { case 'get': ... }`
- Args parsed manually from `process.argv.slice(2)`
- Raw output flag handled globally: `args.indexOf('--raw')`

**Markdown Agents:**
- Self-contained role definitions with `<role>`, `<process>`, `<templates>` sections
- Frontmatter: YAML between `---` delimiters
- Agent definitions ARE the specification (not compiled or processed)

---

*Convention analysis: 2026-02-20*
*Update when patterns change*
