# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Never use @oninput for text fields in Blazor Server on high-latency connections

- **Context**: Blazor Server components with text input hosted on remote servers (e.g. Railway, Azure App Service)
- **Problem**: Characters are lost when typing fast because each @oninput fires a SignalR round-trip that overwrites the input value before the next keystroke arrives
- **Rule**: Never use @oninput for text fields in Blazor Server on high-latency connections; use @onchange (blur-based sync) or a JS-interop debounce instead
- **Applies to**: implement, impl-review
