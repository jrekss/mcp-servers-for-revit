import os
import glob

def test_detection():
    appdata = os.environ.get("APPDATA", "")
    localappdata = os.environ.get("LOCALAPPDATA", "")
    userprofile = os.path.expanduser("~")
    programfiles = os.environ.get("ProgramFiles", "C:\\Program Files")
    programfiles86 = os.environ.get("ProgramFiles(x86)", "C:\\Program Files (x86)")

    print("=========================================")
    print("      AI Client Detection Test")
    print("=========================================")
    
    clients = {
        "cursor": {
            "configs": [
                os.path.join(appdata, "Cursor", "User", "globalStorage", "storage.json")
            ],
            "dirs": [
                os.path.join(appdata, "Cursor"),
                os.path.join(localappdata, "Programs", "cursor"),
                os.path.join(programfiles, "Cursor")
            ]
        },
        "windsurf": {
            "configs": [
                os.path.join(appdata, "Windsurf", "User", "globalStorage", "storage.json"),
                os.path.join(appdata, "Code - Windsurf", "User", "globalStorage", "storage.json")
            ],
            "dirs": [
                os.path.join(appdata, "Windsurf"),
                os.path.join(appdata, "Code - Windsurf"),
                os.path.join(localappdata, "Programs", "Windsurf"),
                os.path.join(programfiles, "Windsurf")
            ]
        },
        "claude": {
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
            "configs": [
                os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")
            ],
            "dirs": [
                os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev"),
                os.path.join(appdata, "Code"),
                os.path.join(localappdata, "Programs", "Microsoft VS Code")
            ]
        },
        "antigravity": {
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
            "configs": [
                os.path.join(userprofile, ".openclaw", "openclaw.json")
            ],
            "dirs": [
                os.path.join(userprofile, ".openclaw")
            ]
        },
        "hermes": {
            "configs": [
                os.path.join(userprofile, ".hermes", "config.yaml")
            ],
            "dirs": [
                os.path.join(userprofile, ".hermes")
            ]
        },
        "codex": {
            "configs": [
                os.path.join(userprofile, ".codex", "config.toml")
            ],
            "dirs": [
                os.path.join(userprofile, ".codex")
            ]
        }
    }

    for name, info in clients.items():
        print(f"\nClient: {name.upper()}")
        
        # Check directories
        dirs_found = []
        for d in info["dirs"]:
            if os.path.exists(d):
                dirs_found.append(d)
        if dirs_found:
            print(f"  Directories found ({len(dirs_found)}):")
            for d in dirs_found:
                print(f"    - {d}")
        else:
            print("  No directories found.")
            
        # Check configs
        configs_found = []
        for c in info["configs"]:
            if os.path.exists(c):
                configs_found.append(c)
        if configs_found:
            print(f"  Config files found ({len(configs_found)}):")
            for c in configs_found:
                print(f"    - {c}")
        else:
            print("  No config files found.")
            
    print("\n=========================================")

if __name__ == "__main__":
    test_detection()
