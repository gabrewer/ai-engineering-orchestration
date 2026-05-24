# PiLoop Model Selection

PiLoop workers should be able to choose their own model from the prompt file.

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

1. `--pi-model` CLI override
2. prompt frontmatter `model:`
3. Pi default model/provider configuration

This keeps worker identity and model choice close to the prompt while still allowing emergency or test-time overrides.
