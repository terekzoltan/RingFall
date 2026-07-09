# Ringfall Brain Prompts

This folder contains source-adjacent prompt and context documentation for the Ringfall brain package.

Current scope: Wave 3 Step 2 `B3-F`, the Track C L1 pulse context/prompt draft. These files are documentation and template guidance only.

This folder does not implement:

- Python runtime prompt loading
- prompt rendering
- provider or API behavior
- model policy decisions
- context or memory retrieval
- trace or cost writing
- schema or config changes

Track D owns parser, validator, provider, and runtime integration behavior. Track C prompt documentation becomes runtime-facing only after Meta accepts the relevant draft and Track D consumes it in a later governed step.

See `l1_pulse_context_template.md` for the generic L1 `AvatarPulsePacket` context boundary and A1/Aster example stance.
