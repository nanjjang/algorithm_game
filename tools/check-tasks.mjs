#!/usr/bin/env node
// docs/dev/tasks.json 검사기. node 표준 모듈만 쓴다.
//
//   node tools/check-tasks.mjs [--tasks <경로>] [--specs <폴더>] [--no-vectors] [--quiet]
//                              [--repo <폴더>] [--base <ref>] [--no-history]
//
//   --repo <폴더>   병합 기록을 읽을 저장소(기본: 이 파일의 위치에서 구한 뿌리)
//   --base <ref>    병합 기록의 기준(기본 main). 커밋 제목 <type>(<id>): … 의 id 가 그래프에 있으면 병합된 작업이다
//   --no-history    기록을 보지 않는다(전부 병합 전으로 보고 엄격하게 검사한다)
//   기록을 읽지 못하면(git 실패, <base>가 없다) 실패하지 않고 전부 병합 전으로 보며 요약에 그 사실을 적는다.
//
// 검사하는 것
//   1. 유효한 JSON이고 필수 필드가 있다. id가 겹치지 않는다.
//   2. deps가 가리키는 작업이 있고, 그래프가 위상정렬된다(순환 없음).
//   3. 같은 parallel_group 안의 작업은 서로 의존이 없고(전이 포함) owned_paths가 겹치지 않는다.
//   4. owned_paths가 겹치는 두 작업은 deps로(전이 포함) 순서가 정해져 있다.
//      3·4의 경로 겹침(DECISIONS-TECH T-65 2·4번): owned_paths의 항목 가운데 개정 장부(LEDGER)와 글자가 같은 것은
//      세지 않는다(docs/dev/ 같은 폴더 무늬는 센다). 겹침 규칙 둘은 둘 다 병합 전인 쌍에만 건다.
//      "같은 묶음은 서로 의존이 없다"는 병합 여부와 무관하다.
//   5. owner_agent가 agents에 있고 owner_model이 그 에이전트의 default_model과 같다.
//   6. milestones의 task_count, blocked_on_assets, parallel_groups가 tasks에서 계산한 값과 같다.
//   7. vectors: 선택자 문법, 명세의 모든 테스트 벡터가 정확히 한 작업에 배정되어 있다(미배정·이중 배정 0).
//   8. acceptance에 "…절 중 … 벡터" 같은 모호한 부분집합 표현이 없다(벡터의 배정은 vectors가 정본).
//
// 저장소의 뿌리는 현재 폴더가 아니라 이 파일의 위치에서 구한다.

import fs from "node:fs";
import path from "node:path";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";

// 개정 장부(T-65 1번). tools/Wizard.TaskGraph/Ledger.cs 와 같은 목록이다
const LEDGER = new Set(["docs/dev/DECISIONS-TECH.md", "docs/dev/OPEN-QUESTIONS.md", "docs/dev/tasks.json"]);
// main의 커밋 제목(PIPELINE 4.2절). tools/Wizard.TaskGraph/MergeHistory.cs 의 CommitSubject 와 같다
const COMMIT_SUBJECT = /^(feat|fix|test|data|docs|build|refactor|perf|art|chore)\(([^()\s]+)\): /;

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "..");
const args = process.argv.slice(2);
const opt = (name, fallback) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : fallback; };
const tasksPath = path.resolve(opt("--tasks", path.join(root, "docs/dev/tasks.json")));
const specDir = path.resolve(opt("--specs", path.join(root, "docs/spec")));
const checkVectors = !args.includes("--no-vectors");
const quiet = args.includes("--quiet");
const repoDir = path.resolve(opt("--repo", root));
const baseRef = opt("--base", "main");
const noHistory = args.includes("--no-history");

const errors = [];
const warnings = [];
const err = (m) => errors.push(m);
const warn = (m) => warnings.push(m);

// ---------- 1. JSON과 필드 ----------
let doc;
try { doc = JSON.parse(fs.readFileSync(tasksPath, "utf8")); }
catch (e) { console.error(`FAIL  ${tasksPath}: JSON이 아니다 — ${e.message}`); process.exit(1); }

const REQUIRED = ["id", "milestone", "title", "module", "owner_agent", "owner_model", "reviewer_model",
  "deps", "spec_refs", "contracts", "owned_paths", "acceptance", "vectors", "parallel_group", "status"];
const STATUS = new Set(["todo", "blocked-on-assets"]);   // 진행 상태는 .claude/taskstate.json에 있다(T-40)
const MODELS = new Set(["fable", "opus", "sonnet", "haiku"]);

const tasks = Array.isArray(doc.tasks) ? doc.tasks : [];
if (!tasks.length) err("tasks가 비어 있다");
const byId = new Map();
for (const t of tasks) {
  for (const k of REQUIRED) if (!(k in t)) err(`${t.id ?? "?"}: 필드 '${k}'가 없다`);
  if (byId.has(t.id)) err(`${t.id}: id가 겹친다`);
  byId.set(t.id, t);
  for (const k of ["deps", "spec_refs", "contracts", "owned_paths", "acceptance", "vectors"])
    if (k in t && !Array.isArray(t[k])) err(`${t.id}: '${k}'는 배열이어야 한다`);
  if (!STATUS.has(t.status)) err(`${t.id}: status '${t.status}' — tasks.json에는 todo 또는 blocked-on-assets만 둔다`);
  if (!MODELS.has(t.owner_model)) err(`${t.id}: owner_model '${t.owner_model}'`);
  if (!MODELS.has(t.reviewer_model)) err(`${t.id}: reviewer_model '${t.reviewer_model}'`);
  if (Array.isArray(t.acceptance) && t.acceptance.length === 0) err(`${t.id}: acceptance가 비어 있다`);
  if (Array.isArray(t.owned_paths) && t.owned_paths.length === 0) err(`${t.id}: owned_paths가 비어 있다`);
}

// ---------- 5. 에이전트와 모델 ----------
const agents = new Map((doc.agents ?? []).map((a) => [a.name, a]));
for (const t of tasks) {
  const a = agents.get(t.owner_agent);
  if (!a) { err(`${t.id}: owner_agent '${t.owner_agent}'가 agents에 없다`); continue; }
  if (a.default_model !== t.owner_model)
    err(`${t.id}: owner_model '${t.owner_model}' ≠ ${t.owner_agent}의 default_model '${a.default_model}' (모델별 변형 에이전트를 쓴다)`);
}

// ---------- 2. deps와 위상정렬 ----------
for (const t of tasks) for (const d of t.deps ?? []) {
  if (!byId.has(d)) err(`${t.id}: deps의 '${d}'가 없다`);
  if (d === t.id) err(`${t.id}: 자기 자신에 의존한다`);
}
const indeg = new Map(tasks.map((t) => [t.id, 0]));
const out = new Map(tasks.map((t) => [t.id, []]));
for (const t of tasks) for (const d of t.deps ?? []) if (byId.has(d)) { indeg.set(t.id, indeg.get(t.id) + 1); out.get(d).push(t.id); }
const order = [];
const queue = tasks.filter((t) => indeg.get(t.id) === 0).map((t) => t.id);
while (queue.length) {
  const id = queue.shift(); order.push(id);
  for (const n of out.get(id)) { indeg.set(n, indeg.get(n) - 1); if (indeg.get(n) === 0) queue.push(n); }
}
const acyclic = order.length === tasks.length;
if (!acyclic) err(`deps에 순환이 있다: ${tasks.filter((t) => indeg.get(t.id) > 0).map((t) => t.id).join(", ")}`);

// 전이 닫힘(조상 집합). 위상 순서대로 비트셋을 합친다.
const index = new Map(tasks.map((t, i) => [t.id, i]));
const words = Math.ceil(tasks.length / 32);
const anc = new Map();
if (acyclic) for (const id of order) {
  const bits = new Uint32Array(words);
  for (const d of byId.get(id).deps ?? []) {
    const a = anc.get(d); if (!a) continue;
    for (let w = 0; w < words; w++) bits[w] |= a[w];
    const i = index.get(d); bits[i >>> 5] |= 1 << (i & 31);
  }
  anc.set(id, bits);
}
const dependsOn = (a, b) => { const bits = anc.get(a); if (!bits) return false; const i = index.get(b); return (bits[i >>> 5] & (1 << (i & 31))) !== 0; };
const ordered = (a, b) => dependsOn(a, b) || dependsOn(b, a);

// ---------- 병합 기록(T-65 4·5번) ----------
// 기록을 읽지 못하면 null: 전부 병합 전으로 본다
function readSubjects() {
  const git = (...a) => execFileSync("git", ["-C", repoDir, ...a], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"], maxBuffer: 1 << 28 });
  try {
    git("rev-parse", "--verify", "--quiet", baseRef + "^{commit}");
    return git("log", "--format=%s", baseRef);
  } catch { return null; }
}
const merged = new Set();
let historySummary;
if (noHistory) historySummary = "병합 기록: 보지 않았다(--no-history). 전부 병합 전으로 본다";
else {
  const subjects = readSubjects();
  if (subjects === null) historySummary = `병합 기록: ${baseRef}을 읽지 못했다. 전부 병합 전으로 본다`;
  else {
    for (const line of subjects.split("\n")) { const m = line.match(COMMIT_SUBJECT); if (m && byId.has(m[2])) merged.add(m[2]); }
    historySummary = `병합 기록: ${baseRef}의 커밋 제목에서 병합된 작업 ${merged.size}개`;
  }
}

// ---------- 3·4. 경로 겹침 ----------
// 규칙: '/'로 끝나면 폴더 전체. '*'는 한 경로 마디 안에서만 맞는다.
function segRegex(seg) { return new RegExp("^" + seg.split("*").map((s) => s.replace(/[.+?^${}()|[\]\\]/g, "\\$&")).join("[^/]*") + "$"); }
function segOverlap(a, b) {
  const sa = a.includes("*"), sb = b.includes("*");
  if (!sa && !sb) return a === b;
  if (sa && !sb) return segRegex(a).test(b);
  if (!sa && sb) return segRegex(b).test(a);
  // 둘 다 '*': 고정된 앞머리와 꼬리가 서로 맞아야 겹칠 수 있다
  const [ap, as] = [a.slice(0, a.indexOf("*")), a.slice(a.lastIndexOf("*") + 1)];
  const [bp, bs] = [b.slice(0, b.indexOf("*")), b.slice(b.lastIndexOf("*") + 1)];
  return (ap.startsWith(bp) || bp.startsWith(ap)) && (as.endsWith(bs) || bs.endsWith(as));
}
function parsePattern(p) { const dir = p.endsWith("/"); return { dir, segs: (dir ? p.slice(0, -1) : p).split("/") }; }
function pathsOverlap(pa, pb) {
  const a = parsePattern(pa), b = parsePattern(pb);
  const n = Math.min(a.segs.length, b.segs.length);
  for (let i = 0; i < n; i++) if (!segOverlap(a.segs[i], b.segs[i])) return false;
  if (a.segs.length === b.segs.length) return true;
  const shorter = a.segs.length < b.segs.length ? a : b;
  return shorter.dir;          // 짧은 쪽이 폴더여야 긴 쪽을 품는다
}
const parsed = tasks.map((t) => (Array.isArray(t.owned_paths) ? t.owned_paths : []).filter((p) => typeof p === "string" && !LEDGER.has(p)));
let overlapPairs = 0, unmergedPairs = 0;
for (let i = 0; i < tasks.length; i++) for (let j = i + 1; j < tasks.length; j++) {
  const A = tasks[i], B = tasks[j];
  let hit = null;
  outer: for (const pa of parsed[i]) for (const pb of parsed[j]) if (pathsOverlap(pa, pb)) { hit = [pa, pb]; break outer; }
  const sameGroup = A.parallel_group === B.parallel_group;
  if (sameGroup && acyclic && ordered(A.id, B.id))
    err(`묶음 ${A.parallel_group}: ${A.id}와 ${B.id}가 서로 의존한다(같은 묶음은 의존이 없어야 한다)`);
  if (!hit) continue;
  overlapPairs++;
  if (merged.has(A.id) || merged.has(B.id)) continue;   // 순서는 main의 기록이 정했다
  unmergedPairs++;
  if (sameGroup) err(`묶음 ${A.parallel_group}: ${A.id}와 ${B.id}의 owned_paths가 겹친다 — ${hit[0]} ↔ ${hit[1]}`);
  else if (acyclic && !ordered(A.id, B.id)) err(`${A.id}와 ${B.id}의 owned_paths가 겹치는데 deps로 순서가 정해져 있지 않다 — ${hit[0]} ↔ ${hit[1]}`);
}

// ---------- 6. 마일스톤 집계 ----------
const ms = new Map((doc.milestones ?? []).map((m) => [m.id, m]));
const calc = new Map();
for (const t of tasks) {
  if (!ms.has(t.milestone)) { err(`${t.id}: milestone '${t.milestone}'가 milestones에 없다`); continue; }
  const c = calc.get(t.milestone) ?? { count: 0, blocked: 0, groups: new Set() };
  c.count++; if (t.status === "blocked-on-assets") c.blocked++; c.groups.add(t.parallel_group);
  calc.set(t.milestone, c);
}
for (const [id, m] of ms) {
  const c = calc.get(id) ?? { count: 0, blocked: 0, groups: new Set() };
  if (m.task_count !== c.count) err(`milestone ${id}: task_count ${m.task_count} ≠ 계산값 ${c.count}`);
  if (m.blocked_on_assets !== c.blocked) err(`milestone ${id}: blocked_on_assets ${m.blocked_on_assets} ≠ 계산값 ${c.blocked}`);
  const want = [...c.groups].sort(), have = [...(m.parallel_groups ?? [])].sort();
  if (want.join("|") !== have.join("|")) err(`milestone ${id}: parallel_groups가 tasks와 다르다 — 있어야 할 것 [${want.join(", ")}], 적힌 것 [${have.join(", ")}]`);
  if (m.gate && !byId.has(m.gate)) err(`milestone ${id}: gate '${m.gate}' 작업이 없다`);
  if (m.gate && byId.has(m.gate) && byId.get(m.gate).milestone !== id) err(`milestone ${id}: gate '${m.gate}'의 milestone이 다르다`);
}

// ---------- 8. 모호한 벡터 표현 ----------
const vague = /(절|TV-[A-Z0-9]+|B군|S군|A군)\s*(가운데|중)\s[^.]*벡터/;
for (const t of tasks) for (const a of t.acceptance ?? []) if (vague.test(a)) err(`${t.id}: acceptance가 벡터의 부분집합을 말로 가리킨다("${a.slice(0, 50)}…"). vectors에 선택자로 적는다`);

// ---------- 7. 벡터 배정 ----------
// 명세의 "## N. 테스트 벡터" 아래 "### N.M" 절에서 벡터 id를 모은다.
//   표의 첫 칸(| TV-001 |, | T-H04 |, | B01 |), "- **TV-11-017** …", "71. given …"(→ N71)
export function extractVectors(dir) {
  const list = [];
  for (const f of fs.readdirSync(dir).filter((x) => /^\d\d-.*\.md$/.test(x)).sort()) {
    const spec = f.slice(0, 2);
    let inVec = false, sec = "";
    for (const line of fs.readFileSync(path.join(dir, f), "utf8").split("\n")) {
      let m;
      if ((m = line.match(/^## (\d+)\. (.*)/))) { inVec = /테스트 벡터/.test(m[2]); sec = ""; continue; }
      if (!inVec) continue;
      if ((m = line.match(/^### (\d+\.\d+)\b/))) { sec = m[1]; continue; }
      if (!sec) continue;
      let id = null;
      if ((m = line.match(/^\|\s*`?([A-Z][A-Za-z0-9]*(?:-[A-Za-z0-9]+)*)`?\s*\|/)) && /\d/.test(m[1]) && m[1] !== "ID") id = m[1];
      else if ((m = line.match(/^- \*\*([A-Z][A-Za-z0-9-]*\d[A-Za-z0-9-]*)\*\*/))) id = m[1];
      else if ((m = line.match(/^(\d+)\. /))) id = "N" + m[1];
      if (id) list.push({ spec, sec, id });
    }
  }
  return list;
}

let vectorSummary = "건너뜀";
if (checkVectors) {
  if (!fs.existsSync(specDir)) err(`명세 폴더가 없다: ${specDir} (--no-vectors로 건너뛸 수 있다)`);
  else {
    const all = extractVectors(specDir);
    const key = (v) => `S${v.spec}:${v.id}`;
    const bySpec = new Map();          // spec → [{sec,id}]
    const seen = new Set();
    for (const v of all) {
      if (seen.has(key(v))) { err(`명세 ${v.spec}: 벡터 id '${v.id}'가 두 번 나온다(§${v.sec})`); continue; }
      seen.add(key(v));
      if (!bySpec.has(v.spec)) bySpec.set(v.spec, []);
      bySpec.get(v.spec).push(v);
    }
    const owner = new Map();           // "S07:TV-020" → task id
    const claim = (task, v) => {
      const k = key(v);
      if (owner.has(k) && owner.get(k) !== task) err(`벡터 ${k}(§${v.sec})가 두 작업에 배정되어 있다: ${owner.get(k)}, ${task}`);
      else if (owner.has(k)) err(`${task}: 벡터 ${k}를 두 번 가리킨다`);
      else owner.set(k, task);
    };
    const SEL = /^S(\d\d)(?:§(\d+\.\d+)|:([A-Za-z0-9-]+)(?:\.\.([A-Za-z0-9-]+))?)$/;
    for (const t of tasks) for (const sel of t.vectors ?? []) {
      const m = typeof sel === "string" ? sel.match(SEL) : null;
      if (!m) { err(`${t.id}: vectors의 '${sel}' — 꼴이 틀렸다(S07§17.3 | S13:TV-051 | S13:TV-026..TV-028)`); continue; }
      const list = bySpec.get(m[1]) ?? [];
      if (m[2]) {
        const hit = list.filter((v) => v.sec === m[2]);
        if (!hit.length) { err(`${t.id}: '${sel}' — 명세 ${m[1]}에 그런 벡터 절이 없다`); continue; }
        hit.forEach((v) => claim(t.id, v));
      } else {
        const i = list.findIndex((v) => v.id === m[3]);
        const j = m[4] ? list.findIndex((v) => v.id === m[4]) : i;
        if (i < 0 || j < 0) { err(`${t.id}: '${sel}' — 명세 ${m[1]}에 그런 벡터 id가 없다`); continue; }
        if (j < i) { err(`${t.id}: '${sel}' — 범위가 거꾸로다`); continue; }
        if (list[i].sec !== list[j].sec) { err(`${t.id}: '${sel}' — 범위가 절을 넘는다(§${list[i].sec} → §${list[j].sec})`); continue; }
        for (let k = i; k <= j; k++) claim(t.id, list[k]);
      }
    }
    const missing = all.filter((v) => !owner.has(key(v)));
    if (missing.length) {
      const bySec = new Map();
      for (const v of missing) { const k = `S${v.spec}§${v.sec}`; if (!bySec.has(k)) bySec.set(k, []); bySec.get(k).push(v.id); }
      for (const [k, ids] of bySec) err(`미배정 벡터 ${k}: ${ids.length > 12 ? ids.slice(0, 12).join(", ") + ` … (${ids.length}개)` : ids.join(", ")}`);
    }
    vectorSummary = `명세 벡터 ${all.length}개, 배정 ${owner.size}개, 미배정 ${missing.length}개`;
  }
}

// ---------- 결과 ----------
const groups = new Set(tasks.map((t) => t.parallel_group));
if (!quiet) {
  for (const w of warnings) console.log(`warn  ${w}`);
  for (const e of errors) console.log(`FAIL  ${e}`);
  console.log(`\n작업 ${tasks.length}개, 묶음 ${groups.size}개, 마일스톤 ${ms.size}개`);
  console.log(`위상정렬: ${acyclic ? "됨" : "순환 있음"} / 경로가 겹치는 작업 쌍 ${overlapPairs}개(장부 제외), 그 가운데 둘 다 병합 전인 쌍 ${unmergedPairs}개(deps로 순서가 있어야 한다)`);
  console.log(historySummary);
  console.log(`벡터: ${vectorSummary}`);
  console.log(errors.length ? `\n실패 ${errors.length}건` : "\n통과");
}
process.exit(errors.length ? 1 : 0);
