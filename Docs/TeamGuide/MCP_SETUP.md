# Connect Codex to Unity and Blender

Use Unity CLI with Unity Pipeline for Unity, and **MCP for Blender** by ahujasid for Blender. This guide targets Windows and the Codex desktop app. The configuration example is in [MCP_CONFIG_EXAMPLE.toml](MCP_CONFIG_EXAMPLE.toml).

## What this project uses

| Tool | Connection | Checked locally on 16 October 2026 |
| --- | --- | --- |
| Unity | Unity CLI, with the project's `com.unity.pipeline` package | CLI `1.0.0-beta.12`; Pipeline `0.8.0-exp.1`; Editor `6000.6.0f1` |
| Blender | ahujasid's MCP for Blender, formerly `blender-mcp` | Existing Codex entry starts `uvx blender-mcp`; new setup below uses `uvx mcp-for-blender` |

Unity versions come from `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json` and `unity --version`. The connector names and existing entries were checked in the current computer's Codex configuration. These checks establish installed configuration, not a fresh connection test on another computer.

The project has used direct `unity command` calls to drive the Editor. Unity CLI also exposes those commands as MCP tools through `unity mcp`. Both use Pipeline. [Unity connection documentation](https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli)

## 1. Prepare Unity

1. Install Unity Hub and the Editor version required by the project: `6000.6.0f1` for this checkout.
2. Open your copy of Skybreak in the Editor and wait for imports and compilation.
3. Open PowerShell and check the CLI:

   ```powershell
   unity --version
   ```

4. If it is missing, follow [Unity CLI installation](https://docs.unity.com/en-us/unity-cli). The bundled Unity skill supplies this Windows installer command:

   ```powershell
   $env:UNITY_CLI_CHANNEL = 'beta'
   Invoke-RestMethod https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | Invoke-Expression
   ```

   Reopen PowerShell after installation, then repeat `unity --version`.

5. Check Pipeline while the project is open:

   ```powershell
   unity pipeline list
   ```

   This project already declares Pipeline in its package manifest. If a different project has no Pipeline installation, sign in and install it there:

   ```powershell
   unity auth login
   unity pipeline install --project-path "F:/UnityProjects/Skybreak"
   ```

   Replace the path with that computer's project folder. Wait for compilation, then repeat `unity pipeline list`. [Pipeline setup](https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package)

## 2. Prepare Blender

Install Blender and [uv using Astral's installation instructions](https://docs.astral.sh/uv/getting-started/installation/). The existing model reports name Blender `5.1.2`.

Check uv and install the add-on:

```powershell
uvx --version
uvx mcp-for-blender install-addon
```

In Blender, open **Edit > Preferences > Add-ons** and enable **Interface: MCP for Blender**. Restart Blender if needed. In the 3D Viewport press **N**, open **MCP for Blender**, and select **Start MCP Server** if it is stopped. The add-on can start automatically when Blender opens. [Maintainer's setup instructions](https://github.com/ahujasid/mcp-for-blender)

## 3. Add the two servers in this app

Open **Settings > MCP servers > Add server**. Add these two STDIO entries:

| Setting | Unity | Blender |
| --- | --- | --- |
| Name | `unity` | `blender` |
| Transport | STDIO | STDIO |
| Command | `unity` | `uvx` |
| Arguments, in order | `mcp`, `--project-path`, `F:/UnityProjects/Skybreak` | `mcp-for-blender` |

Replace the Unity path for another computer. If there is a single command-line field, enter `unity mcp --project-path "F:/UnityProjects/Skybreak"` or `uvx mcp-for-blender` respectively. Save and select **Restart**. [Official OpenAI MCP setup](https://developers.openai.com/codex/mcp)

If an entry already exists, inspect or edit it instead of adding a duplicate. The user's current Blender entry uses `args = ["blender-mcp"]`; the maintainer says that name remains compatible, while new installations use `mcp-for-blender`. [Package naming and compatibility](https://github.com/ahujasid/mcp-for-blender)

### Alternative: add the TOML entries

Open `%USERPROFILE%\.codex\config.toml`, back it up, and merge the following entries. Keep unrelated settings and avoid defining the same server table twice.

```toml
[mcp_servers.unity]
command = "unity"
args = ["mcp", "--project-path", "F:/UnityProjects/Skybreak"]

[mcp_servers.blender]
command = "uvx"
args = ["mcp-for-blender"]
```

Fully restart the app after saving. A server registered through a plugin may appear separately from a manual entry; use one connection for each application. Codex supports STDIO commands in these tables. [Configuration reference](https://developers.openai.com/codex/mcp)

## 4. Check the connection

Keep Unity open with the project loaded and Blender open with its add-on running. In the chat composer, type `/mcp` to inspect connected servers. If the Codex CLI is available, `codex mcp list` lists configured servers; that list alone does not prove the applications respond.

Use these read-only prompts:

> Use Unity to report the active scene name and whether Play Mode is running. Do not change the scene.

> Use MCP for Blender to report the current scene and object count. Do not change anything.

A successful scene response verifies that application's connection. For Unity's terminal connection, this also lists available Editor commands:

```powershell
unity command --project-path "F:/UnityProjects/Skybreak" --caller plugin --skill unity-cli --format json
```

## If something does not connect

| Symptom | Action |
| --- | --- |
| App cannot find `unity` or `uvx` | Run `Get-Command unity` and `Get-Command uvx` in PowerShell. Put the reported executable path in the server's Command field, then restart the app. |
| Unity has no connected Editor | Open the correct project, wait for compilation, and check `unity pipeline list`. Fix C# compilation errors if the Editor is in Safe Mode. |
| Unity targets another project | Correct `--project-path` in the entry. |
| Blender has no response | Check its add-on is enabled and its MCP server is running. |
| Blender reports an add-on protocol mismatch | Run `uvx mcp-for-blender install-addon`, then restart Blender or disable and re-enable its add-on. |
| Old Unity connection expects port 8080 | The current computer has a separate `unityMCP` HTTP entry at `http://127.0.0.1:8080/mcp`. That endpoint's server is not established by the current package manifest. Use the Pipeline configuration above for this guide. |

MCP lets the assistant operate the open applications. It does not itself transfer Blender models into Unity: export FBX, import it, and check materials, scale and pivots. Keep authorship notes with any AI-assisted model files.

## Source and verification notes

The install commands are instructions for the recipient. This guide was prepared by inspecting local configuration, project files and CLI help, and checking the linked primary documentation. It did not reinstall connectors, edit the user's MCP settings, or run a fresh end-to-end connection test. The Unity setup steps also appear in the installed Unity plugin's `skills/unity-cli/SKILL.md` and `references/integration-advanced.md`.
