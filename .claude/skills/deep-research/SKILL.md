---
name: deep-web-research
description: Multi-agent internet research with gap analysis and iterative deepening. Use for research, investigation, deep dives, comprehensive overviews, state-of-the-field summaries, landscape analysis, competitive or market research, technology assessments, and literature reviews — any request needing thorough web research with citations. Use even when the topic looks simple; the gap-analysis loop produces substantially better results than a single research pass.
allowed-tools: Task, WebSearch, WebFetch, Read, Write, Glob
---

# Deep Web Research

## Core rule: you orchestrate, subagents research

**Do not run `WebSearch` yourself.** Your job is decomposition, gap analysis, and synthesis. All primary searching is delegated to subagents via the `Task` tool.

```
YOU (orchestrator)
  ├─→ Task subagent 1: subtopic A   (WebSearch + WebFetch)
  ├─→ Task subagent 2: subtopic B   (WebSearch + WebFetch)
  ├─→ Task subagent 3: subtopic C   (WebSearch + WebFetch)
  └─→ [gap analysis] → Task subagent 4+: fill critical gaps
        └─→ YOU: synthesize the final report
```

Why this matters: each subagent runs 3–5 searches on its own slice, so 3 subtopics yields 12+ searches instead of the 4–5 you would run alone. Subagents go deep; you keep breadth and coherence. That is the difference between a decent answer and a comprehensive report.

If you catch yourself about to call `WebSearch` directly: stop, and spawn a subagent instead.

---

## How to spawn subagents (Claude Code specifics)

Use the `Task` tool with `subagent_type: general-purpose`.

**To run subagents in parallel, issue all `Task` calls in a single assistant message.** Sequential messages mean sequential execution and a much slower run.

Each `Task` call needs:
- `description` — 3–5 words, e.g. `"Research market landscape"`
- `subagent_type` — `general-purpose`
- `prompt` — the full brief from the template below

Subagents cannot see the conversation. Every brief must be self-contained: restate the overall research goal, the specific subtopic, the taxonomy, and the required output format.

Subagents return their findings as text in the tool result. They do not write files — you write the final report.

---

## When to use

Use for:
- Market research and competitive analysis
- Technology landscape assessment
- Academic or scientific literature review
- Current-events synthesis
- Product comparison and recommendation
- Historical or biographical research
- Regulatory and compliance research
- Anything where "comprehensive" or "deep dive" is stated or implied

Do not use for:
- Simple factual questions answerable in one search
- Tasks that only need local repo or internal data
- Requests explicitly asking for a quick answer without depth

---

## Step 1 — Decompose and build a taxonomy

Read the request and identify:
1. **Core question** — what is fundamentally being asked
2. **Scope boundaries** — time period, geography, industry, tech stack
3. **Implicit dimensions** — what a thorough treatment would have to cover

**Split into 3–5 subtopics.** Example — *"Research the state of AI code generation tools in 2026"*:
1. Major players, market leaders, commercial offerings
2. Technical capabilities, limitations, recent breakthroughs
3. Adoption rates, enterprise usage, developer sentiment
4. Security, code-quality concerns, future trends

**Then build a taxonomy.** Classify the *kinds* of entities, claims, or concepts the topic contains — not the subtopics themselves, but the categories that cut across all of them. This gives subagents a shared analytical frame and lifts the report from descriptive to analytical.

| Research type | Taxonomy dimensions |
|---|---|
| Technology | architecture patterns, deployment models, maturity stages, vendor types |
| Market | company types (incumbent/challenger/niche), business models, customer segments |
| Policy | stakeholder types, implementation mechanisms, compliance categories |
| How-to / methodology | claim types (factual/procedural/causal/opinion), tool categories, workflow stages |
| Historical | event types, actor categories, causal chains vs interpretive claims |

Include the taxonomy in every subagent brief. Without it, subagents return a flat list of facts and the report reads like a data dump.

---

## Step 2 — Spawn subagents (mandatory)

Spawn 3–5 subagents in parallel, one per subtopic. Use this brief:

````
## Task: research subtopic [X] of [N]

**Overall research goal:** [the user's full request]

**Your subtopic:** [specific assignment]

**Topic taxonomy — organize your findings along these dimensions:**
- [dimension 1]
- [dimension 2]
- [dimension 3]

**Context:** This is one slice of a comprehensive research report. Go deep on your slice only; another agent covers the rest.

---

## Requirements

1. **Run 3–5 `WebSearch` calls.**
   - Use specific, varied queries — not rephrasings of the same thing
   - Include a year or date range where relevant (e.g. "2025 2026")
   - Mix query types: news, academic, vendor docs, reviews, analyst reports
   - Good: "GitHub Copilot enterprise adoption statistics 2026"
   - Bad: "What is AI code generation?"

2. **`WebFetch` the promising URLs.** Search snippets alone are not enough. Prioritize
   primary sources: papers, official docs, filings, detailed analyses.

3. **Extract and organize:**
   - Key facts with source URL and publication date
   - Statistics with source and as-of date
   - Direct quotes from experts or official statements (keep quotes short)
   - Contradictions or uncertainties you notice

4. **Rate source credibility:** Primary (official docs, original research) /
   Secondary (major news, industry reports) / Expert commentary /
   Community (forums, Reddit — sentiment only, never as fact).

5. **Actively research failure modes, risks, and counterarguments.**
   What goes wrong? What are documented failures and limitations? What do critics say?
   A subtopic report with only positive findings is incomplete and will be rejected.

6. **Tag every finding with verification status:**
   - **Verified** — confirmed by 2+ independent authoritative sources
   - **Single-source** — one credible source, not independently confirmed
   - **Vendor-reported** — claimed by a vendor or interested party, no independent validation
   - **Contested** — sources disagree

7. **Track temporal sensitivity.** For prices, market share, processing times, regulations,
   tool versions, and personnel: give the as-of date next to the finding itself, and flag
   anything likely to be stale soon.

---

## Output format

Return findings as text in exactly this structure:

## [Subtopic name]

### Key findings
- [Finding] [Source: URL, date, source type] [Verification: status]

### Statistics & data
| Metric | Value | As-of date | Source | Verification |
|---|---|---|---|---|

### Notable quotes
> "[short quote]" — Source, date

### Risks, limitations & counterarguments
- [Documented failure, limitation, or critical perspective]

### Time-sensitive findings
- [Finding] — as of [date]; check [source] for updates

### Contradictions & uncertainties
- [Point of disagreement or unclear information]

### Sources used
- [Title](URL) — date, source type

Do not write any files. Return your findings as your final message.
````

---

## Step 3 — Gap analysis

After all subagents return, analyze for two kinds of gaps.

**Information gaps**
1. Missing information the user would expect covered
2. Contradictions between sources that need resolving
3. Key claims supported only by outdated sources
4. Thin coverage — few sources, vague findings
5. Unsourced statistics or assertions

**Analytical gaps** — these separate a good report from a great one:
6. **No critical perspective** — failure modes, risks, counterarguments absent. A report without these is advocacy, not research.
7. **No measurement framework** — the reader cannot assess quality or success in this domain.
8. **No concrete examples** — everything abstract, no case studies or worked examples.
9. **No decision framework** — for actionable topics, the reader still cannot decide anything.

Write the analysis out explicitly before continuing:

```
## Gap analysis

### Critical gaps (must fill)
- [gap] — affects [which conclusion]

### Analytical gaps
- [ ] Critical/adversarial perspective present?
- [ ] Measurement framework present?
- [ ] Concrete examples present?
- [ ] Decision framework present? (if the topic is actionable)

### Moderate gaps (fill if cheap)
- [gap]

### Contradictions to resolve
- [topic]: Source A says X, Source B says Y — need an authoritative source

### Outdated information
- [claim] rests only on [old source] — need current data
```

---

## Step 4 — Iterate

If critical gaps exist, spawn 1–3 follow-up `Task` subagents:

```
## Task: fill research gap

**Gap:** [specific gap from the analysis]

**Context:** Initial research covered [summary] but missed [gap], which affects [conclusion].

**Questions to answer:**
1. [question]
2. [question]

**Priority sources:** [specific domains, reports, or source types]

**Requirements:** 2–4 targeted `WebSearch` calls, `WebFetch` the key sources,
return findings with full citations and verification status. Be focused — this is
narrow follow-up, not broad survey. Do not write files.
```

Re-run gap analysis. Stop when no critical gaps remain, or when returns clearly diminish.

---

## Step 5 — Synthesize and write the report

Aggregate all findings before writing. Number sources by order of first appearance.

**Write the report to a file** — chat output alone is not acceptable. Use `Write`.

- Filename: `[topic]-research-report.md`, kebab-case
- Location: a `research/` directory if one exists (check with `Glob`), otherwise the working directory. Ask if genuinely unclear.

```markdown
# [Topic]: comprehensive analysis

**Research date:** [date]
**Scope:** [brief scope description]

## Executive summary
[3–5 paragraphs of key findings, conclusions, implications. Write it last, place it first.]

## 1. [Major section]
[Content with inline numbered citations [1], [2]]

## 2. [Major section]

## Risks, limitations & critical perspectives
[Mandatory. Failure modes, documented failures, counterarguments, skeptical views.
A report without this section is advocacy, not research.]

## How success is measured
[Metrics, benchmarks, evaluation criteria in this domain. If none are established,
say so — that is itself a finding.]

## Case studies & practical examples
[2–3 concrete examples: real implementations with outcomes, worked step-by-step
examples, before/after comparisons. Include names, numbers, context, outcomes.
"Company X did Y" is not useful.]

## Decision framework
[Include when the topic is actionable. A decision tree (Mermaid), a comparison
matrix with selection criteria, a workflow with decision points, or a risk matrix.
Skip only for purely informational topics.]

## Key takeaways
- **[Takeaway]**: [brief statement]

## Limitations & uncertainties
[What could not be verified, which findings are single-source or vendor-reported,
where the field is actively debating, what may already be stale.]

## References
[1] Author/Organization. "Title." *Publication*. Date. URL

## Methodology
- Research date: [date]
- Sources consulted: [count] across [types]
- Distinct search queries: [count]
- Gap-analysis rounds: [count]
```

**Citation rules**
- Every factual claim, statistic, and specific assertion gets a citation
- Number references [1], [2], [3] by first appearance
- Your own synthesis and general knowledge need no citation
- When sources disagree, cite both and say so
- Keep direct quotes short and sparse; paraphrase by default

**Tone**: technical → precise and neutral; business → analytical and strategic; academic → rigorous, explicit about uncertainty; general interest → accessible but thorough.

---

## Quality standards

**Source hierarchy (in order of preference)**
1. Primary — official docs, original research, filings
2. Reputable secondary — major news, academic reviews, industry reports
3. Expert commentary — known experts, specialized publications
4. Community — Stack Overflow, Reddit, forums (sentiment only, never as fact)

**Red flags**
- Unsourced statistics
- Claims from anonymous blogs
- Sources older than two years on a fast-moving topic
- Single-source support for a load-bearing claim
- Unreviewed AI-generated content

**Verification** — cross-check statistics against 2+ sources; note when sources appear to copy each other; flag implausible claims.

---

## Expected depth

| Metric | Minimum | Target |
|---|---|---|
| Subtopics | 3 | 4–5 |
| Subagents spawned | 3 | 3–5 + 1–2 follow-up |
| Total web searches | 9+ | 15–25+ |
| Sources cited | 10+ | 20–30+ |
| Report length | 2,000 words | 3,000–5,000 words |

Falling below the minimums means you skipped subagents or skipped iteration.

---

## Edge cases

**Topic too broad** ("research AI") — ask the user to narrow it, or propose a focused angle and confirm before spending tokens.

**Fast-moving topic** — emphasize recency and state the research cutoff date prominently.

**Contradictory sources** — present the disagreement transparently with attribution; help the reader understand why sources differ.

**Little information available** — say so explicitly. "Limited public information exists on X" is a valid finding, not a failure.

---

## Common mistakes

1. **Researching it yourself** — you are the orchestrator. Spawn `Task` subagents.
2. **Sequential `Task` calls** — put all parallel calls in one message.
3. **Skipping gap analysis** — the gap-analysis → iteration loop is the whole value of this skill.
4. **Too few subtopics** — 3 is the floor, not the target.
5. **Vague queries** — "AI tools 2026" returns nothing useful.
6. **Not iterating** — one pass is rarely enough.
7. **Report only in chat** — write the `.md` file.
8. **All-positive reporting** — every topic has downsides; omitting them makes the report untrustworthy.
9. **Vendor claims as fact** — "99% accuracy" is marketing until independently verified. The worst version is putting vendor marketing numbers in a statistics table unqualified.
10. **Flat findings** — use the taxonomy to impose analytical structure.

**When tempted to shortcut**

| Temptation | Correct action |
|---|---|
| "This is simple, I'll just search" | No. Spawn 3+ subagents anyway. |
| "Subagents will take too long" | Parallel subagents are faster than you think. |
| "I have enough already" | Run the gap analysis formally. You will find gaps. |
| "They just want a quick answer" | They asked for research. If they want quick, they will say so. |

**Context management** — if context grows too large: ask subagents for summarized findings rather than raw notes, synthesize section by section, and keep full source lists in the references rather than in working context.

---

## Pre-flight checklist

- [ ] Decomposed into 3–5 subtopics
- [ ] Built a taxonomy of entity/claim/concept types
- [ ] Spawned 3–5 `Task` subagents in parallel (did not search myself)
- [ ] Each brief is self-contained and specifies 3–5 `WebSearch` calls
- [ ] Briefs require failure modes, risks, and counterarguments
- [ ] Briefs require per-finding verification status
- [ ] Ran gap analysis — information gaps and analytical gaps
- [ ] Spawned follow-up subagents for critical gaps
- [ ] Wrote the report to a `.md` file
- [ ] Report includes risks, measurement framework, case studies, decision framework (if actionable)
- [ ] Numbered references with full URLs
- [ ] Honest limitations section
