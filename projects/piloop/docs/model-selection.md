# PiLoop Model Selection

PiLoop supports both project-level model routing and prompt-level model selection.

## Shared skill model routing

`piloop init` installs:

- `.pi/skill-models.json`
- `.pi/extensions/skill-model-router.ts`

The extension uses `skill-models.json` for interactive Pi slash-command routing. PiLoop reads the same `skill-models.json` directly for RPC worker subprocesses, because raw RPC prompts may not trigger slash-command extension routing.

Example:

```json
{
  "team-lead": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "high" },
  "pm-agent": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "medium" },
  "destroyer": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "high" },
  "reviewer": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "high" },
  "tester": { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" },
  "product-designer": { "provider": "openai-codex", "model": "gpt-5.5", "thinkingLevel": "medium" },
  "backend-builder": { "provider": "openai-codex", "model": "gpt-5.4", "thinkingLevel": "medium" }
}
```

Lesson learned: route orchestration and quality-gate roles deliberately. `/team-lead`, destroyer, and reviewer need stronger reasoning than routine builders because they decide whether evidence is sufficient, whether to remediate, and when to escalate.

## Prompt-level model

A prompt can specify its preferred model in YAML frontmatter:

```markdown
---
model: github-copilot/claude-sonnet-4.6
tools: Read,Write,Edit,Glob,Grep,Bash
---

# Worker Prompt
```

PiLoop reads the `model` value before starting the worker's Pi RPC process and passes it to Pi for that worker only.

## CLI override

`--pi-model` is an explicit global override:

```bash
dotnet run --project projects/piloop/src/PiLoop -- plan --target-root <repo> --prd docs/PRD.md --pi-model <model>
```

If `--pi-model` is provided, it wins over prompt frontmatter for every worker in that run.

## Precedence

1. explicit CLI overrides: `--pi-provider`, `--pi-model`, `--pi-thinking`
2. `.pi/skill-models.json` entry for the worker name
3. prompt frontmatter `model:`
4. Pi default model/provider configuration

This lets PiLoop specify models for RPC worker subprocesses while the Pi extension uses the same routing file for interactive prompt and skill usage.
