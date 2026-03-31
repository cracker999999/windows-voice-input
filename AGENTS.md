# Repository Guidelines

## Project Structure & Module Organization
- `VoiceInput/`: main .NET 8 WPF application.
- `VoiceInput/Services/`: core runtime services (audio capture, speech transcription, LLM refinement, text injection, config).
- `VoiceInput/UI/`: WPF windows and code-behind (`*.xaml`, `*.xaml.cs`).
- `VoiceInput/Models/`: data and configuration models.
- `VoiceInput/Assets/`: static resources such as `VoiceInput.ico`.
- `openspec/`: product specs, proposals, and change tasks (design/docs, not runtime code).
- `Release/`: publish output (for example `Release/VoiceInput.exe`).

## Build, Test, and Development Commands
- `dotnet restore VoiceInput\VoiceInput.csproj -r win-x64`: restore NuGet packages.
- `dotnet build VoiceInput\VoiceInput.csproj -c Release`: build the app.
- `dotnet run --project VoiceInput\VoiceInput.csproj -c Release`: run locally.
- `dotnet publish VoiceInput\VoiceInput.csproj -c Release -r win-x64 --self-contained true`: produce single-file self-contained binary.
- `.\repack.bat`: kill running app, restore, and republish to `Release/`.

## Coding Style & Naming Conventions
- Follow existing C# style: 4-space indentation, file-scoped namespaces, nullable enabled.
- Use `PascalCase` for types/methods/properties and `_camelCase` for private fields.
- Keep async methods with `Async` suffix (for example `HandleRecordingStoppedAsync`).
- Prefer extending existing services over duplicating business logic.
- Keep UI changes isolated to `UI/` unless service contracts must change.

## Testing Guidelines
- There is currently no dedicated test project in this repository.
- Before each PR, run manual regression checks:
- tray menu actions work (Language, Settings, Exit),
- hotkey press/release triggers record -> transcribe -> inject pipeline,
- settings persist to `%AppData%\VoiceInput\config.json`.
- If automated tests are added, place them under `tests/` and run with `dotnet test`.

## Commit & Pull Request Guidelines
- Recent history uses `feat:` and `fix`; prefer `<type>: <summary>` consistently.
- Keep commits scoped to one change; avoid unrelated refactors.
- PRs should include purpose, impacted modules, validation steps, and related issue/openspec links.
- Include screenshot or short recording for UI-visible changes.

## Security & Configuration Tips
- Never commit real secrets (`azureSpeechKey`, `apiKey`).
- Keep sensitive config local in `%AppData%\VoiceInput\config.json`; use placeholders in docs.
