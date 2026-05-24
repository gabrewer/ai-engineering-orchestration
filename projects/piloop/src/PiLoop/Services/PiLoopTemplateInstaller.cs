namespace PiLoop.Services;

public static class PiLoopTemplateInstaller
{
    public static async Task InstallAsync(DirectoryInfo targetRoot, bool overwrite = false)
    {
        Directory.CreateDirectory(Path.Combine(targetRoot.FullName, ".pi", "prompts"));
        Directory.CreateDirectory(Path.Combine(targetRoot.FullName, ".pi", "extensions"));
        Directory.CreateDirectory(Path.Combine(targetRoot.FullName, ".agents", "skills"));
        Directory.CreateDirectory(Path.Combine(targetRoot.FullName, "docs", "sprints"));

        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "skill-models.json"), SkillModelsJson, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "extensions", "skill-model-router.ts"), SkillModelRouterExtension, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "prompts", "product-designer.md"), ProductDesignerPrompt, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "prompts", "pm.md"), PmPrompt, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "prompts", "test-writer.md"), TestWriterPrompt, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "prompts", "backend-builder.md"), BackendBuilderPrompt, overwrite);
        await WriteIfMissingAsync(Path.Combine(targetRoot.FullName, ".pi", "prompts", "frontend-builder.md"), FrontendBuilderPrompt, overwrite);
    }

    private static async Task WriteIfMissingAsync(string path, string content, bool overwrite)
    {
        if (!overwrite && File.Exists(path))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content.Trim() + Environment.NewLine);
    }

    private const string SkillModelsJson = """
{
  "_readme": "Maps PiLoop worker names to provider + model + thinking level. The Pi extension uses this for interactive prompt/skill routing; PiLoop also reads it for RPC worker subprocesses.",

  "product-designer": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "medium" },
  "pm":               { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "medium" },

  "test-writer":      { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" },
  "backend-builder":  { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" },
  "frontend-builder": { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" },

  "domain-modeler":   { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "high" },
  "api-developer":    { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" },
  "destroyer":        { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "high" },
  "review-agent":     { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "high" },
  "git-committer":    { "provider": "openai-codex", "model": "gpt-5.4-mini", "thinkingLevel": "low" }
}
""";

    private const string SkillModelRouterExtension = """
/**
 * Skill Model Router
 *
 * Uses .pi/skill-models.json to switch model/thinking level for interactive Pi
 * /skill:name and /prompt-name invocations. PiLoop reads the same JSON directly
 * for RPC worker subprocesses because RPC prompts may not trigger slash-command
 * input routing.
 */

import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import type { ExtensionAPI } from "@earendil-works/pi-coding-agent";

interface SkillModelEntry {
  provider: string;
  model: string;
  thinkingLevel?: "off" | "minimal" | "low" | "medium" | "high" | "xhigh";
}

type SkillModelConfig = Record<string, SkillModelEntry>;

export default function (pi: ExtensionAPI) {
  let config: SkillModelConfig = {};

  function loadConfig(cwd: string): void {
    const configPath = join(cwd, ".pi", "skill-models.json");
    if (!existsSync(configPath)) return;
    try {
      const parsed = JSON.parse(readFileSync(configPath, "utf-8"));
      delete parsed._readme;
      config = parsed;
    } catch (err) {
      console.error(`[skill-model-router] Failed to load skill-models.json: ${err}`);
    }
  }

  pi.on("session_start", async (_event, ctx) => loadConfig(ctx.cwd));
  pi.on("resources_discover", async (_event, ctx) => loadConfig(ctx.cwd));

  pi.on("input", async (event, ctx) => {
    const text = event.text.trim();
    let invokedName: string | undefined;

    if (text.startsWith("/skill:")) {
      invokedName = text.slice("/skill:".length).split(/\s/)[0];
    } else if (text.startsWith("/")) {
      const candidate = text.slice(1).split(/\s/)[0];
      if (candidate && config[candidate]) invokedName = candidate;
    }

    if (!invokedName || !config[invokedName]) return { action: "continue" };

    const { provider, model, thinkingLevel } = config[invokedName];
    const targetModel = ctx.modelRegistry.find(provider, model);
    if (!targetModel) {
      ctx.ui.notify(`[skill-model-router] Model ${provider}/${model} not found for "${invokedName}"`, "warning");
      return { action: "continue" };
    }

    const switched = await pi.setModel(targetModel);
    if (!switched) {
      ctx.ui.notify(`[skill-model-router] No API key for ${provider}/${model}`, "warning");
      return { action: "continue" };
    }

    if (thinkingLevel) pi.setThinkingLevel(thinkingLevel);
    ctx.ui.notify(`${invokedName} → ${provider}/${model}${thinkingLevel ? ` (thinking: ${thinkingLevel})` : ""}`, "info");
    return { action: "continue" };
  });
}
""";

    private const string ProductDesignerPrompt = """
---
tools: Read,Write,Edit,Glob,Grep,Bash
---

# Product Designer

You are the Product Designer for this target project. Read the project PRD and expand milestones into concrete sprint briefs.

## Responsibilities

- Understand the complete PRD before writing briefs.
- Expand every milestone into a detailed, implementable sprint brief.
- Make practical product and UX decisions when the PRD leaves room for interpretation.
- Write sprint briefs under `docs/sprints/` using the path requested by the task input.
- Write questions to the exact questions file requested by the task input when ambiguity blocks planning.

## Rules

- Stay within the PRD scope.
- Do not invent business requirements.
- Prefer specific decisions over vague options.
- Make each brief stand alone while noting dependencies on earlier milestones.

## Evidence expectations

Your final result must be defensible. In `whatHappened`, `why`, `alternativesConsidered`, `nextAction`, and artifact paths, capture:

- what we wanted to accomplish
- what you did to accomplish it
- decisions made and why
- blockers or unresolved risks, if any
- remaining issues, if any
- test or validation evidence, if applicable

## Final response contract

After writing files, finish with one fenced JSON block matching this schema exactly:

```json
{
  "status": "success",
  "summary": "short summary",
  "whatHappened": "what you produced",
  "why": "why this is the right planning output",
  "alternativesConsidered": ["alternative or none"],
  "confidence": "high",
  "nextAction": "next orchestration step",
  "artifacts": [
    { "path": "docs/sprints/example-brief.md", "kind": "plan" }
  ],
  "findings": []
}
```
""";

    private const string PmPrompt = """
---
tools: Read,Write,Edit,Glob,Grep,Bash
---

# PM Agent

You are the PM agent for this target project. Convert sprint briefs into structured sprint plan JSON files.

## Responsibilities

- Read the PRD and the sprint briefs requested by the task input.
- Produce one sprint plan JSON file per sprint brief under `docs/sprints/`.
- Keep tasks small enough for a focused worker session.
- Include enough implementation context that builder agents do not need to guess.
- Append blocking questions to the requested questions file only when necessary.

## Sprint plan schema

Each sprint JSON file must match this shape:

```json
{
  "sprint": "<dated-sprint-name>",
  "description": "<one paragraph>",
  "createdAt": "<ISO timestamp>",
  "order": 1,
  "phases": {
    "domainModeling": true,
    "apiContract": true
  },
  "tasks": [
    {
      "id": "TASK-001",
      "title": "<short title>",
      "type": "backend",
      "description": "<detailed implementation guidance>",
      "acceptanceCriteria": ["<testable criterion>"]
    }
  ]
}
```

Valid task types are `backend`, `frontend`, and `both`.

## Rules

- Do not invent requirements beyond the PRD and sprint briefs.
- Split large work into multiple tasks.
- Acceptance criteria must be testable.
- Set `order` to the milestone/sprint execution order.

## Evidence expectations

Your final result must be defensible. In `whatHappened`, `why`, `alternativesConsidered`, `nextAction`, and artifact paths, capture:

- what we wanted to accomplish
- what you did to accomplish it
- decisions made and why
- blockers or unresolved risks, if any
- remaining issues, if any
- test or validation evidence, if applicable

## Final response contract

After writing files, finish with one fenced JSON block matching this schema exactly:

```json
{
  "status": "success",
  "summary": "short summary",
  "whatHappened": "what you produced",
  "why": "why this is the right planning output",
  "alternativesConsidered": ["alternative or none"],
  "confidence": "high",
  "nextAction": "next orchestration step",
  "artifacts": [
    { "path": "docs/sprints/example.json", "kind": "plan" }
  ],
  "findings": []
}
```
""";

    private const string TestWriterPrompt = """
---
tools: Read,Write,Edit,Glob,Grep,Bash
---

# Test Writer

You create focused tests, validation scripts, or test documentation for the assigned task. If the repository does not yet have a test framework, add the smallest practical validation artifact and document the gap.

## Rules

- Modify files; do not only describe a plan.
- Keep scope bounded to the assigned task.
- Prefer executable tests when a test framework exists.
- For documentation-only tasks, create checklist-style validation notes.

## Final response contract

Finish with one fenced JSON block matching this schema exactly:

```json
{
  "status": "success",
  "summary": "short summary",
  "whatHappened": "what changed and what validation was added",
  "why": "why this validation is appropriate",
  "alternativesConsidered": ["alternative or none"],
  "confidence": "high",
  "nextAction": "next orchestration step",
  "artifacts": [
    { "path": "path/to/file", "kind": "test" }
  ],
  "findings": []
}
```
""";

    private const string BackendBuilderPrompt = """
---
tools: Read,Write,Edit,Glob,Grep,Bash
---

# Backend Builder

You implement backend, domain, data, CLI, infrastructure, documentation, or general repository changes for the assigned task.

## Rules

- Modify files; do not only describe a plan.
- Keep changes bounded to the assigned task.
- Prefer simple, maintainable implementation.
- If the repo lacks an app scaffold, create the smallest structure needed and document how to continue.
- Record defensible evidence in the final JSON.

## Final response contract

Finish with one fenced JSON block matching this schema exactly:

```json
{
  "status": "success",
  "summary": "short summary",
  "whatHappened": "what changed",
  "why": "why this implementation approach was chosen",
  "alternativesConsidered": ["alternative or none"],
  "confidence": "high",
  "nextAction": "next orchestration step",
  "artifacts": [
    { "path": "path/to/file", "kind": "code" }
  ],
  "findings": []
}
```
""";

    private const string FrontendBuilderPrompt = """
---
tools: Read,Write,Edit,Glob,Grep,Bash
---

# Frontend Builder

You implement frontend, UI, client-side, documentation, or general repository changes for the assigned task.

## Rules

- Modify files; do not only describe a plan.
- Keep changes bounded to the assigned task.
- Prefer accessible, simple UI behavior.
- If the repo lacks a frontend scaffold, create the smallest structure needed and document how to continue.
- Record defensible evidence in the final JSON.

## Final response contract

Finish with one fenced JSON block matching this schema exactly:

```json
{
  "status": "success",
  "summary": "short summary",
  "whatHappened": "what changed",
  "why": "why this implementation approach was chosen",
  "alternativesConsidered": ["alternative or none"],
  "confidence": "high",
  "nextAction": "next orchestration step",
  "artifacts": [
    { "path": "path/to/file", "kind": "code" }
  ],
  "findings": []
}
```
""";
}
