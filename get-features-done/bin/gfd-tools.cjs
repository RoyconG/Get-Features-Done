#!/usr/bin/env node
'use strict';

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// ─── Model Profiles ──────────────────────────────────────────────────────────

const MODEL_PROFILES = {
  quality:  { 'gfd-planner': 'opus', 'gfd-executor': 'opus', 'gfd-verifier': 'opus', 'gfd-researcher': 'opus', 'gfd-codebase-mapper': 'sonnet' },
  balanced: { 'gfd-planner': 'sonnet', 'gfd-executor': 'sonnet', 'gfd-verifier': 'sonnet', 'gfd-researcher': 'sonnet', 'gfd-codebase-mapper': 'haiku' },
  budget:   { 'gfd-planner': 'sonnet', 'gfd-executor': 'haiku', 'gfd-verifier': 'haiku', 'gfd-researcher': 'haiku', 'gfd-codebase-mapper': 'haiku' },
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function parseIncludeFlag(args) {
  const includes = new Set();
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--include' && args[i + 1]) {
      args[i + 1].split(',').forEach(v => includes.add(v.trim()));
    }
  }
  return includes;
}

function safeReadFile(filePath) {
  try {
    return fs.readFileSync(filePath, 'utf-8');
  } catch {
    return null;
  }
}

function loadConfig(cwd) {
  const defaults = {
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
  };

  const configPath = path.join(cwd, 'docs', 'features', 'config.json');
  try {
    const raw = fs.readFileSync(configPath, 'utf-8');
    return { ...defaults, ...JSON.parse(raw) };
  } catch {
    return defaults;
  }
}

function isGitIgnored(cwd, relPath) {
  try {
    const result = execSync(`git check-ignore -q "${relPath}" 2>/dev/null`, { cwd, stdio: ['pipe', 'pipe', 'pipe'] });
    return true;
  } catch {
    return false;
  }
}

function execGit(cwd, args) {
  try {
    const stdout = execSync(`git ${args.join(' ')}`, { cwd, encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] });
    return { exitCode: 0, stdout: stdout.trim() };
  } catch (err) {
    return { exitCode: err.status || 1, stdout: '', stderr: err.stderr || err.message };
  }
}

// ─── Frontmatter Parsing ──────────────────────────────────────────────────────

function extractFrontmatter(content) {
  if (!content) return {};
  const match = content.match(/^---\n([\s\S]*?)\n---/);
  if (!match) return {};

  const yaml = match[1];
  const result = {};
  const lines = yaml.split('\n');
  let currentKey = null;
  let currentArray = null;
  let currentObject = null;
  let objectKey = null;
  let indent = 0;

  for (const line of lines) {
    if (line.trim() === '' || line.trim().startsWith('#')) continue;

    const objectFieldMatch = line.match(/^(\s{2,4})(\w[\w-]*):\s*(.*)$/);
    if (currentObject !== null && objectFieldMatch) {
      const fieldKey = objectFieldMatch[2];
      let fieldValue = objectFieldMatch[3].trim();
      if (fieldValue === '') fieldValue = null;
      else if (fieldValue === 'true') fieldValue = true;
      else if (fieldValue === 'false') fieldValue = false;
      else if (/^\d+$/.test(fieldValue)) fieldValue = parseInt(fieldValue, 10);
      currentObject[fieldKey] = fieldValue;
      continue;
    }

    const arrayItemMatch = line.match(/^\s{2,4}-\s+(.*)$/);
    if (currentArray !== null && arrayItemMatch) {
      let val = arrayItemMatch[1].trim();
      if (val.startsWith('"') && val.endsWith('"')) val = val.slice(1, -1);
      if (val.startsWith("'") && val.endsWith("'")) val = val.slice(1, -1);
      currentArray.push(val);
      continue;
    }

    const topLevelMatch = line.match(/^([\w][\w-]*):\s*(.*)/);
    if (topLevelMatch) {
      if (currentArray !== null && currentKey) {
        result[currentKey] = currentArray;
        currentArray = null;
      }
      if (currentObject !== null && objectKey) {
        result[objectKey] = currentObject;
        currentObject = null;
        objectKey = null;
      }

      currentKey = topLevelMatch[1];
      let value = topLevelMatch[2].trim();

      if (value === '') {
        // Could be object or array - check next line
        const currentIdx = lines.indexOf(line);
        const nextLine = currentIdx < lines.length - 1 ? lines[currentIdx + 1] : '';
        if (nextLine.match(/^\s+-\s/)) {
          currentArray = [];
        } else if (nextLine.match(/^\s+\w/)) {
          currentObject = {};
          objectKey = currentKey;
          currentKey = null;
        } else {
          result[currentKey] = null;
        }
      } else if (value.startsWith('[') && value.endsWith(']')) {
        const inner = value.slice(1, -1).trim();
        if (inner === '') {
          result[currentKey] = [];
        } else {
          result[currentKey] = inner.split(',').map(v => {
            v = v.trim();
            if (v.startsWith('"') && v.endsWith('"')) v = v.slice(1, -1);
            if (v.startsWith("'") && v.endsWith("'")) v = v.slice(1, -1);
            return v;
          });
        }
        currentArray = null;
      } else if (value === 'true') {
        result[currentKey] = true;
      } else if (value === 'false') {
        result[currentKey] = false;
      } else if (/^\d+$/.test(value)) {
        result[currentKey] = parseInt(value, 10);
      } else {
        if (value.startsWith('"') && value.endsWith('"')) value = value.slice(1, -1);
        if (value.startsWith("'") && value.endsWith("'")) value = value.slice(1, -1);
        result[currentKey] = value;
      }
    }
  }

  if (currentArray !== null && currentKey) {
    result[currentKey] = currentArray;
  }
  if (currentObject !== null && objectKey) {
    result[objectKey] = currentObject;
  }

  return result;
}

function reconstructFrontmatter(obj) {
  let yaml = '';
  for (const [key, value] of Object.entries(obj)) {
    if (value === null || value === undefined) {
      yaml += `${key}:\n`;
    } else if (Array.isArray(value)) {
      if (value.length === 0) {
        yaml += `${key}: []\n`;
      } else if (value.length <= 3 && value.every(v => typeof v === 'string' && v.length < 30)) {
        yaml += `${key}: [${value.map(v => v.includes(',') || v.includes(' ') ? `"${v}"` : v).join(', ')}]\n`;
      } else {
        yaml += `${key}:\n`;
        for (const item of value) {
          yaml += `  - ${typeof item === 'string' && (item.includes(':') || item.includes(',')) ? `"${item}"` : item}\n`;
        }
      }
    } else if (typeof value === 'object') {
      yaml += `${key}:\n`;
      for (const [k, v] of Object.entries(value)) {
        yaml += `  ${k}: ${v === null ? '' : v}\n`;
      }
    } else {
      yaml += `${key}: ${value}\n`;
    }
  }
  return yaml;
}

function spliceFrontmatter(content, newFm) {
  const fmMatch = content.match(/^---\n[\s\S]*?\n---/);
  if (!fmMatch) {
    return `---\n${reconstructFrontmatter(newFm)}---\n\n${content}`;
  }
  return content.replace(fmMatch[0], `---\n${reconstructFrontmatter(newFm)}---`);
}

// ─── Output ───────────────────────────────────────────────────────────────────

const tmpDir = path.join(require('os').tmpdir(), 'gfd-tools');

function output(result, raw, rawValue) {
  const json = JSON.stringify(result);
  if (json.length > 50000) {
    fs.mkdirSync(tmpDir, { recursive: true });
    const tmpFile = path.join(tmpDir, `out-${Date.now()}.json`);
    fs.writeFileSync(tmpFile, json, 'utf-8');
    if (raw) {
      console.log(rawValue !== undefined ? String(rawValue) : tmpFile);
    } else {
      console.log(JSON.stringify({ _tmpfile: tmpFile }));
    }
    return;
  }
  if (raw) {
    console.log(rawValue !== undefined ? String(rawValue) : json);
  } else {
    console.log(json);
  }
}

function error(message) {
  console.error(JSON.stringify({ error: message }));
  process.exit(1);
}

// ─── Feature Operations ───────────────────────────────────────────────────────

function findFeatureInternal(cwd, slug) {
  if (!slug) return null;
  const featuresDir = path.join(cwd, 'docs', 'features');
  const featureDir = path.join(featuresDir, slug);
  const featureMd = path.join(featureDir, 'FEATURE.md');

  if (!fs.existsSync(featureMd)) return null;

  const content = fs.readFileSync(featureMd, 'utf-8');
  const fm = extractFrontmatter(content);

  // Count plans and summaries
  let files;
  try {
    files = fs.readdirSync(featureDir);
  } catch {
    files = [];
  }

  const plans = files.filter(f => f.match(/-PLAN\.md$/i)).sort();
  const summaries = files.filter(f => f.match(/-SUMMARY\.md$/i)).sort();
  const hasResearch = files.some(f => f === 'RESEARCH.md' || f.match(/-RESEARCH\.md$/i));
  const hasVerification = files.some(f => f === 'VERIFICATION.md' || f.match(/-VERIFICATION\.md$/i));

  // Find incomplete plans (plans without summaries)
  const planIds = plans.map(p => p.replace(/-PLAN\.md$/i, ''));
  const summaryIds = new Set(summaries.map(s => s.replace(/-SUMMARY\.md$/i, '')));
  const incompletePlans = planIds.filter(id => !summaryIds.has(id));

  return {
    found: true,
    slug,
    name: fm.name || slug,
    status: fm.status || 'new',
    owner: fm.owner || null,
    assignees: fm.assignees || [],
    priority: fm.priority || 'medium',
    depends_on: fm.depends_on || [],
    directory: path.join('docs', 'features', slug),
    feature_md: path.join('docs', 'features', slug, 'FEATURE.md'),
    plans,
    summaries,
    incomplete_plans: incompletePlans,
    has_research: hasResearch,
    has_verification: hasVerification,
    frontmatter: fm,
  };
}

function listFeaturesInternal(cwd) {
  const featuresDir = path.join(cwd, 'docs', 'features');
  const features = [];

  if (!fs.existsSync(featuresDir)) return features;

  let entries;
  try {
    entries = fs.readdirSync(featuresDir, { withFileTypes: true });
  } catch {
    return features;
  }

  for (const entry of entries) {
    if (!entry.isDirectory()) continue;
    // Skip special directories
    if (entry.name === 'codebase') continue;

    const featureInfo = findFeatureInternal(cwd, entry.name);
    if (featureInfo) {
      features.push(featureInfo);
    }
  }

  return features.sort((a, b) => {
    const priorityOrder = { high: 0, medium: 1, low: 2 };
    const statusOrder = { 'in-progress': 0, planned: 1, planning: 2, researched: 3, researching: 4, discussed: 5, discussing: 6, new: 7, done: 8 };
    const sPri = (priorityOrder[a.priority] || 1) - (priorityOrder[b.priority] || 1);
    if (sPri !== 0) return sPri;
    return (statusOrder[a.status] || 3) - (statusOrder[b.status] || 3);
  });
}

function cmdFindFeature(cwd, slug, raw) {
  if (!slug) error('feature slug required');
  const info = findFeatureInternal(cwd, slug);
  if (!info) {
    output({ found: false, slug }, raw);
    return;
  }
  output(info, raw);
}

function cmdListFeatures(cwd, options, raw) {
  const features = listFeaturesInternal(cwd);
  const filtered = options.status
    ? features.filter(f => f.status === options.status)
    : features;

  output({
    features: filtered,
    count: filtered.length,
    total: features.length,
    by_status: {
      new: features.filter(f => f.status === 'new').length,
      discussing: features.filter(f => f.status === 'discussing').length,
      discussed: features.filter(f => f.status === 'discussed').length,
      researching: features.filter(f => f.status === 'researching').length,
      researched: features.filter(f => f.status === 'researched').length,
      planning: features.filter(f => f.status === 'planning').length,
      planned: features.filter(f => f.status === 'planned').length,
      'in-progress': features.filter(f => f.status === 'in-progress').length,
      done: features.filter(f => f.status === 'done').length,
    },
  }, raw);
}

function cmdFeaturePlanIndex(cwd, slug, raw) {
  if (!slug) error('feature slug required');
  const info = findFeatureInternal(cwd, slug);
  if (!info) {
    output({ error: 'Feature not found', slug }, raw);
    return;
  }

  const featureDir = path.join(cwd, info.directory);
  const plans = info.plans;
  const indexed = [];

  for (const planFile of plans) {
    const planPath = path.join(featureDir, planFile);
    const content = safeReadFile(planPath);
    if (!content) continue;

    const fm = extractFrontmatter(content);
    const planId = planFile.replace(/-PLAN\.md$/i, '');
    const hasSummary = info.summaries.some(s => s.replace(/-SUMMARY\.md$/i, '') === planId);

    indexed.push({
      id: planId,
      file: planFile,
      name: fm.name || planId,
      type: fm.type || 'execute',
      wave: fm.wave || 1,
      depends_on: fm.depends_on || [],
      autonomous: fm.autonomous !== false,
      status: hasSummary ? 'complete' : 'pending',
    });
  }

  // Sort by wave then by plan number
  indexed.sort((a, b) => {
    const wDiff = (parseInt(a.wave) || 1) - (parseInt(b.wave) || 1);
    if (wDiff !== 0) return wDiff;
    return a.id.localeCompare(b.id);
  });

  // Group by waves
  const waves = {};
  for (const plan of indexed) {
    const w = plan.wave || 1;
    if (!waves[w]) waves[w] = [];
    waves[w].push(plan);
  }

  output({
    slug,
    plan_count: indexed.length,
    complete_count: indexed.filter(p => p.status === 'complete').length,
    plans: indexed,
    waves,
  }, raw);
}

// ─── Slug Generation ──────────────────────────────────────────────────────────

function generateSlugInternal(text) {
  if (!text) return null;
  return text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}

function cmdGenerateSlug(text, raw) {
  if (!text) error('text required');
  const slug = generateSlugInternal(text);
  output({ slug }, raw, slug);
}

// ─── Timestamps ───────────────────────────────────────────────────────────────

function cmdCurrentTimestamp(format, raw) {
  const now = new Date();
  let result;
  if (format === 'date') {
    result = now.toISOString().split('T')[0];
  } else if (format === 'iso') {
    result = now.toISOString();
  } else {
    result = now.toISOString();
  }
  output({ timestamp: result, format }, raw, result);
}

// ─── Config Operations ────────────────────────────────────────────────────────

function cmdConfigGet(cwd, key, raw) {
  const config = loadConfig(cwd);
  if (key) {
    const value = config[key];
    output({ [key]: value !== undefined ? value : null }, raw, value !== undefined ? String(value) : '');
  } else {
    output(config, raw);
  }
}

function cmdConfigSet(cwd, key, value, raw) {
  if (!key || value === undefined) error('key and value required');
  const configPath = path.join(cwd, 'docs', 'features', 'config.json');
  const config = loadConfig(cwd);

  let parsedValue;
  try { parsedValue = JSON.parse(value); } catch { parsedValue = value; }
  config[key] = parsedValue;

  fs.mkdirSync(path.dirname(configPath), { recursive: true });
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2), 'utf-8');
  output({ updated: true, key, value: parsedValue }, raw, 'true');
}

// ─── Model Resolution ─────────────────────────────────────────────────────────

function resolveModelInternal(cwd, agentName) {
  const config = loadConfig(cwd);
  const profile = config.model_profile || 'balanced';
  const profileMap = MODEL_PROFILES[profile] || MODEL_PROFILES.balanced;
  return profileMap[agentName] || 'sonnet';
}

function cmdResolveModel(cwd, agentName, raw) {
  if (!agentName) error('agent name required');
  const model = resolveModelInternal(cwd, agentName);
  output({ agent: agentName, model }, raw, model);
}

// ─── Commit ───────────────────────────────────────────────────────────────────

function cmdCommit(cwd, message, files, raw, amend) {
  if (!message && !amend) error('commit message required');

  const config = loadConfig(cwd);
  if (!config.commit_docs) {
    output({ committed: false, reason: 'commit_docs disabled' }, raw, 'skipped');
    return;
  }

  // Check gitignore for docs/features/
  if (isGitIgnored(cwd, 'docs/features/')) {
    output({ committed: false, reason: 'docs/features/ is gitignored' }, raw, 'gitignored');
    return;
  }

  // Stage files
  if (files.length > 0) {
    for (const f of files) {
      const filePath = path.isAbsolute(f) ? f : path.join(cwd, f);
      if (fs.existsSync(filePath)) {
        execGit(cwd, ['add', f]);
      }
    }
  } else {
    execGit(cwd, ['add', 'docs/features/']);
  }

  // Check if there's anything staged
  const diffResult = execGit(cwd, ['diff', '--cached', '--name-only']);
  if (!diffResult.stdout.trim()) {
    output({ committed: false, reason: 'nothing to commit' }, raw, 'nothing');
    return;
  }

  // Commit
  const commitArgs = ['commit'];
  if (amend) commitArgs.push('--amend');
  commitArgs.push('-m', message);

  const commitResult = execGit(cwd, commitArgs);
  if (commitResult.exitCode !== 0) {
    output({ committed: false, error: commitResult.stderr || 'commit failed' }, raw, 'failed');
    return;
  }

  const hashResult = execGit(cwd, ['rev-parse', '--short', 'HEAD']);
  output({ committed: true, hash: hashResult.stdout.trim(), message }, raw, hashResult.stdout.trim());
}

// ─── Frontmatter CRUD ────────────────────────────────────────────────────────

function cmdFrontmatterGet(cwd, filePath, field, raw) {
  if (!filePath) error('file path required');
  const fullPath = path.isAbsolute(filePath) ? filePath : path.join(cwd, filePath);
  const content = safeReadFile(fullPath);
  if (!content) { output({ error: 'File not found', path: filePath }, raw); return; }
  const fm = extractFrontmatter(content);
  if (field) {
    const value = fm[field];
    if (value === undefined) { output({ error: 'Field not found', field }, raw); return; }
    output({ [field]: value }, raw, JSON.stringify(value));
  } else {
    output(fm, raw);
  }
}

function cmdFrontmatterSet(cwd, filePath, field, value, raw) {
  if (!filePath || !field || value === undefined) error('file, field, and value required');
  const fullPath = path.isAbsolute(filePath) ? filePath : path.join(cwd, filePath);
  if (!fs.existsSync(fullPath)) { output({ error: 'File not found', path: filePath }, raw); return; }
  const content = fs.readFileSync(fullPath, 'utf-8');
  const fm = extractFrontmatter(content);
  let parsedValue;
  try { parsedValue = JSON.parse(value); } catch { parsedValue = value; }
  fm[field] = parsedValue;
  const newContent = spliceFrontmatter(content, fm);
  fs.writeFileSync(fullPath, newContent, 'utf-8');
  output({ updated: true, field, value: parsedValue }, raw, 'true');
}

function cmdFrontmatterMerge(cwd, filePath, data, raw) {
  if (!filePath || !data) error('file and data required');
  const fullPath = path.isAbsolute(filePath) ? filePath : path.join(cwd, filePath);
  if (!fs.existsSync(fullPath)) { output({ error: 'File not found', path: filePath }, raw); return; }
  const content = fs.readFileSync(fullPath, 'utf-8');
  const fm = extractFrontmatter(content);
  let mergeData;
  try { mergeData = JSON.parse(data); } catch { error('Invalid JSON for --data'); return; }
  Object.assign(fm, mergeData);
  const newContent = spliceFrontmatter(content, fm);
  fs.writeFileSync(fullPath, newContent, 'utf-8');
  output({ merged: true, fields: Object.keys(mergeData) }, raw, 'true');
}

// ─── Verification ─────────────────────────────────────────────────────────────

function cmdVerifySummary(cwd, summaryPath, checkCount, raw) {
  if (!summaryPath) error('summary-path required');
  const fullPath = path.isAbsolute(summaryPath) ? summaryPath : path.join(cwd, summaryPath);
  const content = safeReadFile(fullPath);
  if (!content) { output({ valid: false, error: 'File not found' }, raw, 'invalid'); return; }

  const fm = extractFrontmatter(content);
  const errors = [];

  if (!fm.feature) errors.push('Missing frontmatter: feature');
  if (!fm.plan) errors.push('Missing frontmatter: plan');
  if (!fm['one-liner']) errors.push('Missing frontmatter: one-liner');

  const taskPattern = /##\s*Task\s*\d+/gi;
  const taskMatches = content.match(taskPattern) || [];
  if (taskMatches.length < checkCount) {
    errors.push(`Expected at least ${checkCount} task sections, found ${taskMatches.length}`);
  }

  output({
    valid: errors.length === 0,
    errors,
    task_sections: taskMatches.length,
    has_frontmatter: Object.keys(fm).length > 0,
  }, raw, errors.length === 0 ? 'valid' : 'invalid');
}

function cmdVerifyCommits(cwd, hashes, raw) {
  if (!hashes || hashes.length === 0) error('At least one commit hash required');
  const valid = [];
  const invalid = [];
  for (const hash of hashes) {
    const result = execGit(cwd, ['cat-file', '-t', hash]);
    if (result.exitCode === 0 && result.stdout.trim() === 'commit') {
      valid.push(hash);
    } else {
      invalid.push(hash);
    }
  }
  output({ all_valid: invalid.length === 0, valid, invalid, total: hashes.length }, raw, invalid.length === 0 ? 'valid' : 'invalid');
}

function cmdVerifyReferences(cwd, filePath, raw) {
  if (!filePath) error('file path required');
  const fullPath = path.isAbsolute(filePath) ? filePath : path.join(cwd, filePath);
  const content = safeReadFile(fullPath);
  if (!content) { output({ error: 'File not found', path: filePath }, raw); return; }

  const found = [];
  const missing = [];

  const atRefs = content.match(/@([^\s\n,)]+\/[^\s\n,)]+)/g) || [];
  for (const ref of atRefs) {
    const cleanRef = ref.slice(1);
    const resolved = cleanRef.startsWith('~/')
      ? path.join(process.env.HOME || '', cleanRef.slice(2))
      : path.join(cwd, cleanRef);
    if (fs.existsSync(resolved)) { found.push(cleanRef); } else { missing.push(cleanRef); }
  }

  output({ valid: missing.length === 0, found: found.length, missing, total: found.length + missing.length }, raw, missing.length === 0 ? 'valid' : 'invalid');
}

function cmdVerifyPlanStructure(cwd, filePath, raw) {
  if (!filePath) error('file path required');
  const fullPath = path.isAbsolute(filePath) ? filePath : path.join(cwd, filePath);
  const content = safeReadFile(fullPath);
  if (!content) { output({ error: 'File not found', path: filePath }, raw); return; }

  const fm = extractFrontmatter(content);
  const errors = [];
  const warnings = [];

  const required = ['feature', 'plan', 'type', 'wave', 'depends_on', 'files_modified', 'autonomous'];
  for (const field of required) {
    if (fm[field] === undefined) errors.push(`Missing required frontmatter field: ${field}`);
  }

  const taskPattern = /<task[^>]*>([\s\S]*?)<\/task>/g;
  const tasks = [];
  let taskMatch;
  while ((taskMatch = taskPattern.exec(content)) !== null) {
    const taskContent = taskMatch[1];
    const nameMatch = taskContent.match(/<name>([\s\S]*?)<\/name>/);
    const taskName = nameMatch ? nameMatch[1].trim() : 'unnamed';
    const hasAction = /<action>/.test(taskContent);
    const hasVerify = /<verify>/.test(taskContent);
    const hasDone = /<done>/.test(taskContent);

    if (!nameMatch) errors.push('Task missing <name> element');
    if (!hasAction) errors.push(`Task '${taskName}' missing <action>`);
    if (!hasVerify) warnings.push(`Task '${taskName}' missing <verify>`);
    if (!hasDone) warnings.push(`Task '${taskName}' missing <done>`);

    tasks.push({ name: taskName, hasAction, hasVerify, hasDone });
  }

  if (tasks.length === 0) warnings.push('No <task> elements found');

  output({
    valid: errors.length === 0,
    errors,
    warnings,
    task_count: tasks.length,
    tasks,
  }, raw, errors.length === 0 ? 'valid' : 'invalid');
}

// ─── Summary Extract ──────────────────────────────────────────────────────────

function cmdSummaryExtract(cwd, summaryPath, fields, raw) {
  if (!summaryPath) error('summary-path required');
  const fullPath = path.join(cwd, summaryPath);
  const content = safeReadFile(fullPath);
  if (!content) { output({ error: 'File not found', path: summaryPath }, raw); return; }

  const fm = extractFrontmatter(content);
  const fullResult = {
    path: summaryPath,
    one_liner: fm['one-liner'] || null,
    key_files: fm['key-files'] || [],
    tech_added: (fm['tech-stack'] && fm['tech-stack'].added) || [],
    decisions: fm['key-decisions'] || [],
  };

  if (fields && fields.length > 0) {
    const filtered = { path: summaryPath };
    for (const field of fields) {
      if (fullResult[field] !== undefined) filtered[field] = fullResult[field];
    }
    output(filtered, raw);
    return;
  }

  output(fullResult, raw);
}

// ─── History Digest ───────────────────────────────────────────────────────────

function cmdHistoryDigest(cwd, raw) {
  const features = listFeaturesInternal(cwd);
  const allSummaries = [];
  const allDecisions = [];
  const allTechAdded = new Set();
  const allPatterns = new Set();

  for (const feature of features) {
    const featureDir = path.join(cwd, feature.directory);
    for (const summaryFile of feature.summaries) {
      const content = safeReadFile(path.join(featureDir, summaryFile));
      if (!content) continue;
      const fm = extractFrontmatter(content);

      allSummaries.push({
        feature: feature.slug,
        file: summaryFile,
        one_liner: fm['one-liner'] || null,
      });

      if (fm['key-decisions'] && Array.isArray(fm['key-decisions'])) {
        for (const d of fm['key-decisions']) {
          allDecisions.push({ feature: feature.slug, decision: d });
        }
      }
      if (fm['tech-stack'] && fm['tech-stack'].added) {
        for (const t of (Array.isArray(fm['tech-stack'].added) ? fm['tech-stack'].added : [fm['tech-stack'].added])) {
          allTechAdded.add(t);
        }
      }
    }
  }

  output({
    summaries: allSummaries,
    summary_count: allSummaries.length,
    decisions: allDecisions,
    tech_added: [...allTechAdded],
  }, raw);
}

// ─── Path Verification ────────────────────────────────────────────────────────

function pathExistsInternal(cwd, targetPath) {
  const fullPath = path.isAbsolute(targetPath) ? targetPath : path.join(cwd, targetPath);
  try { fs.statSync(fullPath); return true; } catch { return false; }
}

function cmdVerifyPathExists(cwd, targetPath, raw) {
  if (!targetPath) error('path required');
  const exists = pathExistsInternal(cwd, targetPath);
  output({ exists, path: targetPath }, raw, exists ? 'true' : 'false');
}

// ─── Template Operations ──────────────────────────────────────────────────────

function cmdTemplateSelect(cwd, templateType, raw) {
  const templatesDir = path.join(__dirname, '..', 'templates');
  const templatePath = path.join(templatesDir, `${templateType}.md`);
  if (!fs.existsSync(templatePath)) {
    const available = fs.readdirSync(templatesDir).filter(f => f.endsWith('.md')).map(f => f.replace('.md', ''));
    error(`Template not found: ${templateType}. Available: ${available.join(', ')}`);
  }
  const content = fs.readFileSync(templatePath, 'utf-8');
  output({ template: templateType, content }, raw, content);
}

function cmdTemplateFill(cwd, templateType, options, raw) {
  const templatesDir = path.join(__dirname, '..', 'templates');
  const templatePath = path.join(templatesDir, `${templateType}.md`);
  if (!fs.existsSync(templatePath)) error(`Template not found: ${templateType}`);

  let content = fs.readFileSync(templatePath, 'utf-8');
  const today = new Date().toISOString().split('T')[0];

  // Replace placeholders
  content = content.replace(/\[FEATURE\]/g, options.feature || 'unknown');
  content = content.replace(/\[PLAN\]/g, options.plan || '01');
  content = content.replace(/\[NAME\]/g, options.name || 'Unnamed');
  content = content.replace(/\[DATE\]/g, today);
  content = content.replace(/\[TYPE\]/g, options.type || 'execute');
  content = content.replace(/\[WAVE\]/g, options.wave || '1');

  if (options.fields) {
    for (const [key, value] of Object.entries(options.fields)) {
      content = content.replace(new RegExp(`\\[${key.toUpperCase()}\\]`, 'g'), String(value));
    }
  }

  output({ filled: true, template: templateType, content }, raw, content);
}

// ─── Feature Decisions & Blockers ─────────────────────────────────────────────

function cmdFeatureAddDecision(cwd, slug, summary, rationale, raw) {
  if (!slug) error('feature slug required');
  if (!summary) error('--summary required');
  const featureMdPath = path.join(cwd, 'docs', 'features', slug, 'FEATURE.md');
  if (!fs.existsSync(featureMdPath)) {
    output({ added: false, error: 'Feature not found', slug }, raw);
    return;
  }

  let content = fs.readFileSync(featureMdPath, 'utf-8');
  const entry = rationale ? `- ${summary} — ${rationale}` : `- ${summary}`;

  const decisionsMatch = content.match(/(## Decisions\s*\n)([\s\S]*?)(?=\n## |\n---|\n$)/i);
  if (decisionsMatch) {
    const sectionBody = decisionsMatch[2];
    // Remove placeholder text
    const cleaned = sectionBody.replace(/^\s*\[.*?\]\s*$/gm, '').trimEnd();
    const insertPos = content.indexOf(decisionsMatch[0]) + decisionsMatch[1].length;
    const endPos = insertPos + decisionsMatch[2].length;
    const newBody = cleaned ? cleaned + '\n' + entry + '\n' : '\n' + entry + '\n';
    content = content.slice(0, insertPos) + newBody + content.slice(endPos);
  } else {
    // Add section before --- footer if present, otherwise append
    const footerMatch = content.match(/\n---\s*\n\*Created:/);
    if (footerMatch) {
      const pos = content.indexOf(footerMatch[0]);
      content = content.slice(0, pos) + '\n## Decisions\n\n' + entry + '\n' + content.slice(pos);
    } else {
      content += '\n## Decisions\n\n' + entry + '\n';
    }
  }

  fs.writeFileSync(featureMdPath, content, 'utf-8');
  output({ added: true, slug, summary }, raw, 'true');
}

function cmdFeatureAddBlocker(cwd, slug, text, raw) {
  if (!slug) error('feature slug required');
  if (!text) error('blocker text required');
  const featureMdPath = path.join(cwd, 'docs', 'features', slug, 'FEATURE.md');
  if (!fs.existsSync(featureMdPath)) {
    output({ added: false, error: 'Feature not found', slug }, raw);
    return;
  }

  let content = fs.readFileSync(featureMdPath, 'utf-8');
  const entry = `- ${text}`;

  const blockersMatch = content.match(/(## Blockers\s*\n)([\s\S]*?)(?=\n## |\n---|\n$)/i);
  if (blockersMatch) {
    const sectionBody = blockersMatch[2];
    const cleaned = sectionBody.replace(/^\s*\[.*?\]\s*$/gm, '').trimEnd();
    const insertPos = content.indexOf(blockersMatch[0]) + blockersMatch[1].length;
    const endPos = insertPos + blockersMatch[2].length;
    const newBody = cleaned ? cleaned + '\n' + entry + '\n' : '\n' + entry + '\n';
    content = content.slice(0, insertPos) + newBody + content.slice(endPos);
  } else {
    const footerMatch = content.match(/\n---\s*\n\*Created:/);
    if (footerMatch) {
      const pos = content.indexOf(footerMatch[0]);
      content = content.slice(0, pos) + '\n## Blockers\n\n' + entry + '\n' + content.slice(pos);
    } else {
      content += '\n## Blockers\n\n' + entry + '\n';
    }
  }

  fs.writeFileSync(featureMdPath, content, 'utf-8');
  output({ added: true, slug, text }, raw, 'true');
}

// ─── Feature Status Update ────────────────────────────────────────────────────

function cmdFeatureUpdateStatus(cwd, slug, newStatus, raw) {
  if (!slug || !newStatus) error('slug and status required');

  const validStatuses = ['new', 'discussing', 'discussed', 'researching', 'researched', 'planning', 'planned', 'in-progress', 'done'];
  if (!validStatuses.includes(newStatus)) {
    error(`Invalid status: ${newStatus}. Valid: ${validStatuses.join(', ')}`);
  }

  const featureMdPath = path.join(cwd, 'docs', 'features', slug, 'FEATURE.md');
  if (!fs.existsSync(featureMdPath)) {
    output({ updated: false, error: 'Feature not found', slug }, raw);
    return;
  }

  const content = fs.readFileSync(featureMdPath, 'utf-8');
  const fm = extractFrontmatter(content);
  const oldStatus = fm.status || 'new';
  fm.status = newStatus;
  const newContent = spliceFrontmatter(content, fm);
  fs.writeFileSync(featureMdPath, newContent, 'utf-8');

  output({ updated: true, slug, old_status: oldStatus, new_status: newStatus }, raw, newStatus);
}

// ─── Progress Render ──────────────────────────────────────────────────────────

function cmdProgressRender(cwd, format, raw) {
  const features = listFeaturesInternal(cwd);
  const total = features.length;
  const done = features.filter(f => f.status === 'done').length;
  const inProgress = features.filter(f => f.status === 'in-progress').length;
  const percent = total > 0 ? Math.round((done / total) * 100) : 0;

  if (format === 'table') {
    const barWidth = 10;
    const filled = Math.round((percent / 100) * barWidth);
    const bar = '\u2588'.repeat(filled) + '\u2591'.repeat(barWidth - filled);
    let out = `# Project Progress\n\n`;
    out += `**Progress:** [${bar}] ${done}/${total} features (${percent}%)\n\n`;
    out += `| Feature | Status | Owner | Plans | Complete |\n`;
    out += `|---------|--------|-------|-------|----------|\n`;
    for (const f of features) {
      const planProgress = `${f.summaries.length}/${f.plans.length}`;
      out += `| ${f.name} | ${f.status} | ${f.owner || '-'} | ${planProgress} | ${f.status === 'done' ? 'Yes' : 'No'} |\n`;
    }
    output({ rendered: out }, raw, out);
  } else if (format === 'bar') {
    const barWidth = 20;
    const filled = Math.round((percent / 100) * barWidth);
    const bar = '\u2588'.repeat(filled) + '\u2591'.repeat(barWidth - filled);
    const text = `[${bar}] ${done}/${total} features (${percent}%)`;
    output({ bar: text, percent, completed: done, total }, raw, text);
  } else {
    output({
      features: features.map(f => ({
        slug: f.slug,
        name: f.name,
        status: f.status,
        owner: f.owner,
        plan_count: f.plans.length,
        summary_count: f.summaries.length,
      })),
      total,
      done,
      in_progress: inProgress,
      percent,
    }, raw);
  }
}

// ─── Validate Health ──────────────────────────────────────────────────────────

function cmdValidateHealth(cwd, options, raw) {
  const featuresDir = path.join(cwd, 'docs', 'features');
  const projectPath = path.join(featuresDir, 'PROJECT.md');
  const configPath = path.join(featuresDir, 'config.json');

  const errors = [];
  const warnings = [];
  const info = [];
  const repairs = [];

  const addIssue = (severity, code, message, fix, repairable = false) => {
    const issue = { code, message, fix, repairable };
    if (severity === 'error') errors.push(issue);
    else if (severity === 'warning') warnings.push(issue);
    else info.push(issue);
  };

  if (!fs.existsSync(featuresDir)) {
    addIssue('error', 'E001', 'docs/features/ directory not found', 'Run /gfd:new-project to initialize');
    output({ status: 'broken', errors, warnings, info, repairable_count: 0 }, raw);
    return;
  }

  if (!fs.existsSync(projectPath)) {
    addIssue('error', 'E002', 'PROJECT.md not found', 'Run /gfd:new-project to create');
  } else {
    const content = fs.readFileSync(projectPath, 'utf-8');
    const requiredSections = ['## What This Is', '## Core Value'];
    for (const section of requiredSections) {
      if (!content.includes(section)) {
        addIssue('warning', 'W001', `PROJECT.md missing section: ${section}`, 'Add section manually');
      }
    }
  }

  if (!fs.existsSync(configPath)) {
    addIssue('warning', 'W003', 'config.json not found', 'Run /gfd:health --repair to create with defaults', true);
    repairs.push('createConfig');
  } else {
    try {
      JSON.parse(fs.readFileSync(configPath, 'utf-8'));
    } catch (err) {
      addIssue('error', 'E005', `config.json: JSON parse error - ${err.message}`, 'Run /gfd:health --repair to reset', true);
      repairs.push('resetConfig');
    }
  }

  // Check features for FEATURE.md
  const features = listFeaturesInternal(cwd);
  for (const feature of features) {
    if (feature.incomplete_plans.length > 0) {
      addIssue('info', 'I001', `${feature.slug}: ${feature.incomplete_plans.length} plan(s) without SUMMARY.md`, 'May be in progress');
    }
  }

  // Perform repairs if requested
  const repairActions = [];
  if (options.repair && repairs.length > 0) {
    for (const repair of repairs) {
      try {
        switch (repair) {
          case 'createConfig':
          case 'resetConfig': {
            const defaults = loadConfig(cwd);
            fs.writeFileSync(configPath, JSON.stringify(defaults, null, 2), 'utf-8');
            repairActions.push({ action: repair, success: true, path: 'config.json' });
            break;
          }
        }
      } catch (err) {
        repairActions.push({ action: repair, success: false, error: err.message });
      }
    }
  }

  let status;
  if (errors.length > 0) status = 'broken';
  else if (warnings.length > 0) status = 'degraded';
  else status = 'healthy';

  const repairableCount = errors.filter(e => e.repairable).length + warnings.filter(w => w.repairable).length;

  output({
    status,
    errors,
    warnings,
    info,
    repairable_count: repairableCount,
    repairs_performed: repairActions.length > 0 ? repairActions : undefined,
  }, raw);
}

// ─── Init Commands ────────────────────────────────────────────────────────────

function cmdInitNewProject(cwd, raw) {
  const config = loadConfig(cwd);

  let hasCode = false;
  let hasPackageFile = false;
  try {
    const files = execSync('find . -maxdepth 3 \\( -name "*.ts" -o -name "*.js" -o -name "*.py" -o -name "*.go" -o -name "*.rs" -o -name "*.swift" -o -name "*.java" \\) 2>/dev/null | grep -v node_modules | grep -v .git | head -5', {
      cwd, encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'],
    });
    hasCode = files.trim().length > 0;
  } catch {}

  hasPackageFile = pathExistsInternal(cwd, 'package.json') ||
                   pathExistsInternal(cwd, 'requirements.txt') ||
                   pathExistsInternal(cwd, 'Cargo.toml') ||
                   pathExistsInternal(cwd, 'go.mod') ||
                   pathExistsInternal(cwd, 'Package.swift');

  output({
    researcher_model: resolveModelInternal(cwd, 'gfd-researcher'),
    commit_docs: config.commit_docs,
    project_exists: pathExistsInternal(cwd, 'docs/features/PROJECT.md'),
    has_codebase_map: pathExistsInternal(cwd, 'docs/features/codebase'),
    features_dir_exists: pathExistsInternal(cwd, 'docs/features'),
    has_existing_code: hasCode,
    has_package_file: hasPackageFile,
    is_brownfield: hasCode || hasPackageFile,
    needs_codebase_map: (hasCode || hasPackageFile) && !pathExistsInternal(cwd, 'docs/features/codebase'),
    has_git: pathExistsInternal(cwd, '.git'),
  }, raw);
}

function cmdInitNewFeature(cwd, slug, raw) {
  if (!slug) error('feature slug required');

  const config = loadConfig(cwd);
  const existingFeature = findFeatureInternal(cwd, slug);

  output({
    commit_docs: config.commit_docs,
    slug,
    feature_exists: !!existingFeature,
    existing_status: existingFeature?.status || null,
    features_dir_exists: pathExistsInternal(cwd, 'docs/features'),
    feature_dir: `docs/features/${slug}`,
    feature_md: `docs/features/${slug}/FEATURE.md`,
    project_exists: pathExistsInternal(cwd, 'docs/features/PROJECT.md'),
  }, raw);
}

function cmdInitPlanFeature(cwd, slug, includes, raw) {
  if (!slug) error('feature slug required');

  const config = loadConfig(cwd);
  const featureInfo = findFeatureInternal(cwd, slug);

  const result = {
    researcher_model: resolveModelInternal(cwd, 'gfd-researcher'),
    planner_model: resolveModelInternal(cwd, 'gfd-planner'),
    checker_model: resolveModelInternal(cwd, 'gfd-verifier'),

    research_enabled: config.research,
    plan_checker_enabled: config.plan_checker,
    commit_docs: config.commit_docs,

    feature_found: !!featureInfo,
    feature_dir: featureInfo?.directory || null,
    slug,
    feature_name: featureInfo?.name || null,
    feature_status: featureInfo?.status || null,

    has_research: featureInfo?.has_research || false,
    has_plans: (featureInfo?.plans?.length || 0) > 0,
    plan_count: featureInfo?.plans?.length || 0,

    features_dir_exists: pathExistsInternal(cwd, 'docs/features'),
  };

  if (includes.has('feature') && featureInfo) {
    result.feature_content = safeReadFile(path.join(cwd, featureInfo.feature_md));
  }
  if (includes.has('research') && featureInfo) {
    const featureDir = path.join(cwd, featureInfo.directory);
    try {
      const files = fs.readdirSync(featureDir);
      const researchFile = files.find(f => f === 'RESEARCH.md' || f.endsWith('-RESEARCH.md'));
      if (researchFile) result.research_content = safeReadFile(path.join(featureDir, researchFile));
    } catch {}
  }
  output(result, raw);
}

function cmdInitExecuteFeature(cwd, slug, includes, raw) {
  if (!slug) error('feature slug required');

  const config = loadConfig(cwd);
  const featureInfo = findFeatureInternal(cwd, slug);

  const result = {
    executor_model: resolveModelInternal(cwd, 'gfd-executor'),
    verifier_model: resolveModelInternal(cwd, 'gfd-verifier'),

    commit_docs: config.commit_docs,
    parallelization: config.parallelization,
    verifier_enabled: config.verifier,

    feature_found: !!featureInfo,
    feature_dir: featureInfo?.directory || null,
    slug,
    feature_name: featureInfo?.name || null,
    feature_status: featureInfo?.status || null,

    plans: featureInfo?.plans || [],
    summaries: featureInfo?.summaries || [],
    incomplete_plans: featureInfo?.incomplete_plans || [],
    plan_count: featureInfo?.plans?.length || 0,
    incomplete_count: featureInfo?.incomplete_plans?.length || 0,

    config_exists: pathExistsInternal(cwd, 'docs/features/config.json'),
  };

  if (includes.has('feature') && featureInfo) {
    result.feature_content = safeReadFile(path.join(cwd, featureInfo.feature_md));
  }
  if (includes.has('config')) {
    result.config_content = safeReadFile(path.join(cwd, 'docs', 'features', 'config.json'));
  }

  output(result, raw);
}

function cmdInitProgress(cwd, includes, raw) {
  const config = loadConfig(cwd);
  const features = listFeaturesInternal(cwd);

  const result = {
    executor_model: resolveModelInternal(cwd, 'gfd-executor'),
    planner_model: resolveModelInternal(cwd, 'gfd-planner'),
    commit_docs: config.commit_docs,

    features: features.map(f => ({
      slug: f.slug,
      name: f.name,
      status: f.status,
      owner: f.owner,
      plan_count: f.plans.length,
      summary_count: f.summaries.length,
      incomplete_plans: f.incomplete_plans.length,
    })),
    feature_count: features.length,
    done_count: features.filter(f => f.status === 'done').length,
    in_progress_count: features.filter(f => f.status === 'in-progress').length,
    new_count: features.filter(f => f.status === 'new').length,

    project_exists: pathExistsInternal(cwd, 'docs/features/PROJECT.md'),
  };

  if (includes.has('project')) {
    result.project_content = safeReadFile(path.join(cwd, 'docs', 'features', 'PROJECT.md'));
  }

  output(result, raw);
}

function cmdInitMapCodebase(cwd, raw) {
  const config = loadConfig(cwd);
  const codebaseDir = path.join(cwd, 'docs', 'features', 'codebase');
  let existingMaps = [];
  try {
    existingMaps = fs.readdirSync(codebaseDir).filter(f => f.endsWith('.md'));
  } catch {}

  output({
    mapper_model: resolveModelInternal(cwd, 'gfd-codebase-mapper'),
    commit_docs: config.commit_docs,
    search_gitignored: config.search_gitignored,
    parallelization: config.parallelization,
    codebase_dir: 'docs/features/codebase',
    existing_maps: existingMaps,
    has_maps: existingMaps.length > 0,
    features_dir_exists: pathExistsInternal(cwd, 'docs/features'),
    codebase_dir_exists: pathExistsInternal(cwd, 'docs/features/codebase'),
  }, raw);
}

// ─── CLI Router ───────────────────────────────────────────────────────────────

async function main() {
  const args = process.argv.slice(2);
  const rawIndex = args.indexOf('--raw');
  const raw = rawIndex !== -1;
  if (rawIndex !== -1) args.splice(rawIndex, 1);

  const command = args[0];
  const cwd = process.cwd();

  if (!command) {
    error('Usage: gfd-tools <command> [args] [--raw]\nCommands: feature, resolve-model, find-feature, list-features, feature-plan-index, feature-update-status, commit, verify-summary, verify, frontmatter, template, generate-slug, current-timestamp, config-get, config-set, history-digest, progress, validate, init');
  }

  switch (command) {
    case 'feature': {
      const subcommand = args[1];
      if (subcommand === 'add-decision') {
        const summaryIdx = args.indexOf('--summary');
        const rationaleIdx = args.indexOf('--rationale');
        cmdFeatureAddDecision(cwd, args[2],
          summaryIdx !== -1 ? args[summaryIdx + 1] : null,
          rationaleIdx !== -1 ? args[rationaleIdx + 1] : '',
          raw);
      } else if (subcommand === 'add-blocker') {
        cmdFeatureAddBlocker(cwd, args[2], args[3], raw);
      } else {
        error('Unknown feature subcommand. Available: add-decision, add-blocker');
      }
      break;
    }

    case 'resolve-model': {
      cmdResolveModel(cwd, args[1], raw);
      break;
    }

    case 'find-feature': {
      cmdFindFeature(cwd, args[1], raw);
      break;
    }

    case 'list-features': {
      const statusIdx = args.indexOf('--status');
      cmdListFeatures(cwd, {
        status: statusIdx !== -1 ? args[statusIdx + 1] : null,
      }, raw);
      break;
    }

    case 'feature-plan-index': {
      cmdFeaturePlanIndex(cwd, args[1], raw);
      break;
    }

    case 'feature-update-status': {
      cmdFeatureUpdateStatus(cwd, args[1], args[2], raw);
      break;
    }

    case 'commit': {
      const amend = args.includes('--amend');
      const message = args[1];
      const filesIndex = args.indexOf('--files');
      const files = filesIndex !== -1 ? args.slice(filesIndex + 1).filter(a => !a.startsWith('--')) : [];
      cmdCommit(cwd, message, files, raw, amend);
      break;
    }

    case 'verify-summary': {
      const summaryPath = args[1];
      const countIndex = args.indexOf('--check-count');
      const checkCount = countIndex !== -1 ? parseInt(args[countIndex + 1], 10) : 2;
      cmdVerifySummary(cwd, summaryPath, checkCount, raw);
      break;
    }

    case 'template': {
      const subcommand = args[1];
      if (subcommand === 'select') {
        cmdTemplateSelect(cwd, args[2], raw);
      } else if (subcommand === 'fill') {
        const templateType = args[2];
        const featureIdx = args.indexOf('--feature');
        const planIdx = args.indexOf('--plan');
        const nameIdx = args.indexOf('--name');
        const typeIdx = args.indexOf('--type');
        const waveIdx = args.indexOf('--wave');
        const fieldsIdx = args.indexOf('--fields');
        cmdTemplateFill(cwd, templateType, {
          feature: featureIdx !== -1 ? args[featureIdx + 1] : null,
          plan: planIdx !== -1 ? args[planIdx + 1] : null,
          name: nameIdx !== -1 ? args[nameIdx + 1] : null,
          type: typeIdx !== -1 ? args[typeIdx + 1] : 'execute',
          wave: waveIdx !== -1 ? args[waveIdx + 1] : '1',
          fields: fieldsIdx !== -1 ? JSON.parse(args[fieldsIdx + 1]) : {},
        }, raw);
      } else {
        error('Unknown template subcommand. Available: select, fill');
      }
      break;
    }

    case 'frontmatter': {
      const subcommand = args[1];
      const file = args[2];
      if (subcommand === 'get') {
        const fieldIdx = args.indexOf('--field');
        cmdFrontmatterGet(cwd, file, fieldIdx !== -1 ? args[fieldIdx + 1] : null, raw);
      } else if (subcommand === 'set') {
        const fieldIdx = args.indexOf('--field');
        const valueIdx = args.indexOf('--value');
        cmdFrontmatterSet(cwd, file, fieldIdx !== -1 ? args[fieldIdx + 1] : null, valueIdx !== -1 ? args[valueIdx + 1] : undefined, raw);
      } else if (subcommand === 'merge') {
        const dataIdx = args.indexOf('--data');
        cmdFrontmatterMerge(cwd, file, dataIdx !== -1 ? args[dataIdx + 1] : null, raw);
      } else {
        error('Unknown frontmatter subcommand. Available: get, set, merge');
      }
      break;
    }

    case 'verify': {
      const subcommand = args[1];
      if (subcommand === 'plan-structure') {
        cmdVerifyPlanStructure(cwd, args[2], raw);
      } else if (subcommand === 'references') {
        cmdVerifyReferences(cwd, args[2], raw);
      } else if (subcommand === 'commits') {
        cmdVerifyCommits(cwd, args.slice(2), raw);
      } else {
        error('Unknown verify subcommand. Available: plan-structure, references, commits');
      }
      break;
    }

    case 'generate-slug': {
      cmdGenerateSlug(args[1], raw);
      break;
    }

    case 'current-timestamp': {
      cmdCurrentTimestamp(args[1] || 'full', raw);
      break;
    }

    case 'config-get': {
      cmdConfigGet(cwd, args[1], raw);
      break;
    }

    case 'config-set': {
      cmdConfigSet(cwd, args[1], args[2], raw);
      break;
    }

    case 'history-digest': {
      cmdHistoryDigest(cwd, raw);
      break;
    }

    case 'verify-path-exists': {
      cmdVerifyPathExists(cwd, args[1], raw);
      break;
    }

    case 'progress': {
      const subcommand = args[1] || 'json';
      cmdProgressRender(cwd, subcommand, raw);
      break;
    }

    case 'validate': {
      const subcommand = args[1];
      if (subcommand === 'health') {
        const repairFlag = args.includes('--repair');
        cmdValidateHealth(cwd, { repair: repairFlag }, raw);
      } else {
        error('Unknown validate subcommand. Available: health');
      }
      break;
    }

    case 'summary-extract': {
      const summaryPath = args[1];
      const fieldsIndex = args.indexOf('--fields');
      const fields = fieldsIndex !== -1 ? args[fieldsIndex + 1].split(',') : null;
      cmdSummaryExtract(cwd, summaryPath, fields, raw);
      break;
    }

    case 'init': {
      const workflow = args[1];
      const includes = parseIncludeFlag(args);
      switch (workflow) {
        case 'new-project':
          cmdInitNewProject(cwd, raw);
          break;
        case 'new-feature':
          cmdInitNewFeature(cwd, args[2], raw);
          break;
        case 'plan-feature':
          cmdInitPlanFeature(cwd, args[2], includes, raw);
          break;
        case 'execute-feature':
          cmdInitExecuteFeature(cwd, args[2], includes, raw);
          break;
        case 'progress':
          cmdInitProgress(cwd, includes, raw);
          break;
        case 'map-codebase':
          cmdInitMapCodebase(cwd, raw);
          break;
        default:
          error(`Unknown init workflow: ${workflow}\nAvailable: new-project, new-feature, plan-feature, execute-feature, progress, map-codebase`);
      }
      break;
    }

    default:
      error(`Unknown command: ${command}`);
  }
}

main();
