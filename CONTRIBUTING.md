# Contributing to Veggerby.Ignition

First off — **thank you** for considering a contribution to **Veggerby.Ignition**.
This library coordinates application startup readiness via ignition signals and a deterministic coordinator. Your help improves reliability and clarity for users integrating the library into varied hosting scenarios.

Every contribution matters — whether it's a bug fix, new execution policy, performance optimization, documentation improvement, or refinement of timeout semantics.

Let’s build a focused, high‑quality readiness coordination library.

---

## 🚀 How to Contribute

There are many ways you can help:

- **Report bugs**: Found something strange or broken? Open an issue.
- **Suggest improvements**: Ideas for features, extensions, better defaults — always welcome.
- **Improve documentation**: Good docs make the project more accessible to everyone.
- **Submit code changes**: Fix a bug, add a feature, or improve internal structure.

---

## 📋 Contribution Guidelines

Please follow these basic guidelines to keep everything smooth:

1. **Open an Issue First**
   If you're planning a larger change, open an issue first to discuss it. It helps avoid duplicated work or big surprises.

2. **Small Pull Requests**
   Try to keep PRs focused and easy to review. Smaller changes get merged faster.

3. **Write Tests**
   If you're fixing a bug or adding a new feature, please include or update tests to cover it.

4. **Match the Code Style**
   Follow repository standards:
   - Allman braces for namespaces, types, methods.
   - 4 spaces indentation; no tabs; no trailing whitespace.
   - Explicit braces for all control blocks.
   - Avoid LINQ in hot coordinator paths (prefer explicit loops).
   - XML docs for all public types/members.
   - Deterministic logic: no hidden randomness or time-based branching beyond timeouts.

5. **Explain Your Changes**
   In PRs, explain the "why" as well as the "what". A few clear sentences are enough.

---

## 🛠 Local Setup

- Clone the repository.
- Build the solution (`Veggerby.Ignition.sln`) using .NET 9 or later.
- Run the tests (`Veggerby.Ignition.Tests`) to ensure everything passes before you push.

```bash
dotnet build
dotnet test
```

---

## 🧩 Project Structure (Overview)

| Folder | Purpose |
|:-------|:--------|
| `/src/Veggerby.Ignition` | Core readiness coordination library (signals, coordinator, options, DI extensions, health check) |
| `/test/Veggerby.Ignition.Tests` | Unit tests (policies, timeouts, execution modes, idempotency, adapters) |
| `/docs` | Documentation |

---

## 🛡️ Code of Conduct

Be respectful and constructive. Veggerby.Ignition welcomes contributors from all backgrounds and skill levels. No toxicity, no gatekeeping. We’re here to build reliable software together.

---

## 📢 Final Thoughts

Veggerby.Ignition is intentionally narrow in scope: coordinating startup readiness with deterministic semantics and minimal dependencies. If a proposed feature expands beyond this focus (general job orchestration, retry frameworks, UI), open an issue to discuss viability before coding.

Thank you for helping make that happen.

— The Veggerby.Ignition Maintainers
