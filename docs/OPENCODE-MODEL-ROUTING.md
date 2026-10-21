# OpenCode model routing for Overlink work

Verified on Hyperion, 2026-10-17, `opencode v2.0.16`.


### Why the bare `9router/ag/` prefix cannot work

Probed the gateway directly (`http://100.98.220.85:20128/v1/chat/completions`):

```
model=ag/      -> 503 {"error":"[antigravity/] [404]: Requested entity was not found"}
model=kr/      -> 400 {"message":"Invalid model. Please select a different model to continue.","reason":"INVALID_MODEL_ID"}
model=ag/auto  -> 503 {"error":"[antigravity/auto] [404]: Requested entity was not found"}
model=kr/auto  -> 200 TIER_OK
```

The `ag` tier resolves to an upstream provider called **`antigravity`**, and with no model name
appended it requests `antigravity/` — an empty entity, which 404s. This is an upstream gateway
limitation, not an OpenCode problem: the id fails at the API level too. `kr/` has an `auto` model
so `kr/auto` works, but `ag` has no `auto`.

**Consequence:** `ag` must be followed by a concrete model. There is no ag-tier auto-route.


## There is no native fallback in OpenCode

The config schema at `https://opencode.ai/config.json` has **no fallback, retry, or chain key**
anywhere — checked across all top-level properties and `$defs`. `properties.model` accepts exactly
one model (`providerID` + `model`). `agent.<name>.model` likewise accepts one model.

So a fallback chain cannot be configured *in* OpenCode. It has to be driven by whoever invokes
`opencode run`.

## Routing policy for Overlink work

| Order | Model | When |
|---|---|---|
| 1 | `9router/kr/claude-sonnet-4.5` | Default ag-tier route. Non-Gemini. |
| 1b | `9router/ag/claude-opus-4-6-thinking` | Alternate ag route if the above is unavailable. |
| 2 | `9router/oc/big-pickle` | Both above failed. |


`inferhub/cx/gpt-6-astra` is **not** in the Overlink chain. Master's instruction: use Astra only when
Claude quota is exhausted *and* the task is heavy. Overlink implementation does not qualify, so
Astra stays out unless explicitly requested.

## Driver protocol

The invoker must implement the fallback, because OpenCode will not:

```
for model in 9router/ag/gpt-oss-120b-medium, 9router/ag/gemini-3.8-flash, 9router/kr/auto, 9router/oc/big-pickle:
    run: opencode run -m <model> "<prompt>"
    if exit == 0 and output is non-empty:
        accept and stop
    if output matches "Model unavailable" or "Model not found":
        continue to next model
    if it is a real task failure (not a model error):
        stop and report — do not mask a task failure as a model failure
```

**Critical distinction:** a model-availability error means "retry on the next route". A task error
means the model ran and failed — retrying on another model will not fix it and hides the real
problem. Only retry on the former.

## Invocation

```bash
cd /d E:\Games\Overlink
opencode run -m <model> "<prompt>"
```

`-m` / `--model` takes `provider/model` or `provider/model#variant`.

## Config state

`C:\Users\SleepyMoru\.config\opencode\opencode.json` — unchanged by this work.
Current top-level `model` is `inferhub/cx/gpt-6-astra`; one agent, `explorer`
(`inferhub/cb/gpt-6-luna`, subagent). The 9router provider is supplied by the plugin
`@vheins/opencode-9router` (`http://100.98.220.85:20128`).

No fallback chain was written into that file because the schema has no field for one. Adding an
invented key would be silently ignored at best and a config error at worst.
