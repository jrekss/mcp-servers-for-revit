import os
import json
import sys

def detect_agents():
    # Retrieve standard paths
    appdata = os.environ.get("APPDATA", "")
    localappdata = os.environ.get("LOCALAPPDATA", "")
    userprofile = os.path.expanduser("~")
    programfiles = os.environ.get("ProgramFiles", "C:\\Program Files")
    programfiles86 = os.environ.get("ProgramFiles(x86)", "C:\\Program Files (x86)")

    # Define standard locations for AI agents' directories and configuration files
    agents_definitions = {
        "cursor": {
            "name": "Cursor",
            "configs": [
                os.path.join(userprofile, ".cursor", "mcp.json"),
                os.path.join(appdata, "Cursor", "User", "globalStorage", "storage.json")
            ],
            "dirs": [
                os.path.join(appdata, "Cursor"),
                os.path.join(userprofile, ".cursor"),
                os.path.join(localappdata, "Programs", "cursor"),
                os.path.join(programfiles, "Cursor")
            ]
        },
        "windsurf": {
            "name": "Windsurf",
            "configs": [
                os.path.join(userprofile, ".codeium", "windsurf", "mcp_config.json"),
                os.path.join(appdata, "Windsurf", "User", "globalStorage", "storage.json"),
                os.path.join(appdata, "Code - Windsurf", "User", "globalStorage", "storage.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".codeium", "windsurf"),
                os.path.join(userprofile, ".codeium"),
                os.path.join(appdata, "Windsurf"),
                os.path.join(appdata, "Code - Windsurf"),
                os.path.join(localappdata, "Programs", "Windsurf"),
                os.path.join(programfiles, "Windsurf")
            ]
        },
        "claude": {
            "name": "Claude Desktop",
            "configs": [
                os.path.join(appdata, "Claude", "claude_desktop_config.json"),
                os.path.join(appdata, "Subliminal", "Claude", "claude_desktop_config.json"),
                os.path.join(appdata, "EasyConnect", "Claude", "claude_desktop_config.json")
            ],
            "dirs": [
                os.path.join(appdata, "Claude"),
                os.path.join(appdata, "Subliminal", "Claude"),
                os.path.join(appdata, "EasyConnect", "Claude"),
                os.path.join(localappdata, "Programs", "claude")
            ]
        },
        "cline": {
            "name": "Cline (VS Code Extension)",
            "configs": [
                os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")
            ],
            "dirs": [
                os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev"),
                os.path.join(appdata, "Code"),
                os.path.join(localappdata, "Programs", "Microsoft VS Code")
            ]
        },
        "vscode": {
            "name": "VS Code (mcp.json)",
            "configs": [
                os.path.join(appdata, "Code", "User", "mcp.json")
            ],
            "dirs": [
                os.path.join(appdata, "Code", "User"),
                os.path.join(localappdata, "Programs", "Microsoft VS Code")
            ]
        },
        "copilot": {
            "name": "GitHub Copilot CLI",
            "configs": [
                os.path.join(userprofile, ".copilot", "mcp-config.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".copilot")
            ]
        },
        "gemini_cli": {
            "name": "Gemini CLI",
            "configs": [
                os.path.join(userprofile, ".gemini", "settings.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".gemini")
            ]
        },
        "claude_code": {
            "name": "Claude Code",
            "configs": [
                os.path.join(userprofile, ".claude.json"),
                os.path.join(userprofile, ".mcp.json")
            ],
            "dirs": []
        },
        "warp": {
            "name": "Warp",
            "configs": [
                os.path.join(userprofile, ".warp", "mcp.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".warp")
            ]
        },
        "antigravity": {
            "name": "Antigravity IDE",
            "configs": [
                os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "storage.json"),
                os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")
            ],
            "dirs": [
                os.path.join(appdata, "Antigravity IDE"),
                os.path.join(localappdata, "Programs", "Antigravity IDE")
            ]
        },
        "opencode": {
            "name": "OpenCode",
            "configs": [
                os.path.join(userprofile, ".opencode.json"),
                os.path.join(localappdata, "opencode", ".opencode.json"),
                os.path.join(appdata, "opencode", "opencode.json"),
                os.path.join(appdata, "ai.opencode.desktop", "User", "globalStorage", "storage.json")
            ],
            "dirs": [
                os.path.join(appdata, "opencode"),
                os.path.join(localappdata, "opencode"),
                os.path.join(appdata, "ai.opencode.desktop"),
                os.path.join(localappdata, "Programs", "OpenCode")
            ]
        },
        "openclaw": {
            "name": "OpenClaw",
            "configs": [
                os.path.join(userprofile, ".openclaw", "openclaw.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".openclaw")
            ]
        },
        "hermes": {
            "name": "Hermes",
            "configs": [
                os.path.join(userprofile, ".hermes", "config.yaml")
            ],
            "dirs": [
                os.path.join(userprofile, ".hermes")
            ]
        },
        "codex": {
            "name": "Codex",
            "configs": [
                os.path.join(userprofile, ".codex", "config.toml")
            ],
            "dirs": [
                os.path.join(userprofile, ".codex")
            ]
        }
    }

    found_agents = []
    not_found_agents = []

    print("=========================================")
    print("      AI Client Detection Script")
    print("=========================================")
    print(f"UserProfile: {userprofile}")
    print(f"AppData: {appdata}")
    print(f"LocalAppData: {localappdata}")
    print("-----------------------------------------")

    for key, info in agents_definitions.items():
        name = info["name"]
        dirs_found = [d for d in info["dirs"] if os.path.exists(d)]
        configs_found = [c for c in info["configs"] if os.path.exists(c)]
        
        is_installed = len(dirs_found) > 0 or len(configs_found) > 0
        
        if is_installed:
            found_agents.append((key, name, dirs_found, configs_found))
        else:
            not_found_agents.append((key, name))

    print("\n[DETECTED AGENTS]")
    if found_agents:
        for key, name, dirs, configs in found_agents:
            print(f"\n+ {name} ({key})")
            if dirs:
                print("  Directories found:")
                for d in dirs:
                    print(f"    - {d}")
            if configs:
                print("  Config files found:")
                for c in configs:
                    print(f"    - {c}")
    else:
        print("  None")

    print("\n[NOT DETECTED AGENTS]")
    if not_found_agents:
        for key, name in not_found_agents:
            print(f"  - {name} ({key})")
    else:
        print("  None")
    print("=========================================\n")

if __name__ == "__main__":
    detect_agents()
