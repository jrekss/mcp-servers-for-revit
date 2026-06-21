import os
import json
import sys

def write_log(msg):
    print(f"[LOG] {msg}")

def get_server_js_path():
    appdata = os.environ.get("APPDATA", "")
    return os.path.join(appdata, "revit_mcp_plugin", "server", "build", "index.js").replace("\\", "/")

def update_json_config(path, server_js_forward, key_style="mcpServers"):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
    except Exception as e:
        write_log(f"Failed to create directory for {path}: {e}")
        
    config = {}
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                content = f.read().strip()
                if content:
                    config = json.loads(content)
        except Exception as e:
            write_log(f"Warning: Could not read existing JSON from {path} ({e}). Overwriting.")
            
    modified = False
    
    if key_style == "mcpServers":
        if "mcpServers" not in config or not isinstance(config["mcpServers"], dict):
            config["mcpServers"] = {}
        config["mcpServers"]["mcp-server-for-revit"] = {
            "command": "node",
            "args": [server_js_forward]
        }
        modified = True
    elif key_style == "opencode":
        if "mcp" not in config or not isinstance(config["mcp"], dict):
            config["mcp"] = {}
        config["mcp"]["mcp-server-for-revit"] = {
            "type": "local",
            "command": ["node", server_js_forward],
            "enabled": True
        }
        modified = True
    elif key_style == "openclaw":
        if "mcp" not in config or not isinstance(config["mcp"], dict):
            config["mcp"] = {}
        if "servers" not in config["mcp"] or not isinstance(config["mcp"]["servers"], dict):
            config["mcp"]["servers"] = {}
        config["mcp"]["servers"]["mcp-server-for-revit"] = {
            "command": "node",
            "args": [server_js_forward]
        }
        modified = True
    elif key_style == "cursor":
        mcp_config = {
            "mcp-server-for-revit": {
                "name": "mcp-server-for-revit",
                "type": "command",
                "command": f'node "{server_js_forward}"',
                "args": "",
                "env": {}
            }
        }
        if "mcpServers" not in config or not isinstance(config["mcpServers"], dict):
            config["mcpServers"] = {}
        config["mcpServers"]["mcp-server-for-revit"] = {
            "command": "node",
            "args": [server_js_forward]
        }
        
        if "mcp.servers" not in config or not isinstance(config["mcp.servers"], dict):
            config["mcp.servers"] = {}
        config["mcp.servers"].update(mcp_config)
        modified = True
        
    if modified:
        try:
            with open(path, "w", encoding="utf-8") as f:
                json.dump(config, f, indent=2)
            write_log(f"Successfully configured JSON at {path}")
            return True
        except Exception as e:
            write_log(f"Error writing to JSON at {path}: {e}")
            return False
    return False

def update_yaml_config(path, server_js_forward):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
    except Exception:
        pass
    content = ""
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
        except Exception:
            pass
            
    lines = content.splitlines()
    mcp_servers_idx = -1
    for i, line in enumerate(lines):
        if line.strip().startswith("mcp_servers:"):
            mcp_servers_idx = i
            break
            
    new_entry = [
        'mcp_servers:',
        '  mcp-server-for-revit:',
        '    command: "node"',
        '    args:',
        f'      - "{server_js_forward}"'
    ]
    
    if mcp_servers_idx == -1:
        if lines and lines[-1].strip():
            lines.append("")
        lines.extend(new_entry)
    else:
        revit_idx = -1
        for j in range(mcp_servers_idx + 1, len(lines)):
            line = lines[j]
            if line.strip() and not line.startswith(" ") and not line.startswith("\t"):
                break
            if line.strip().startswith("mcp-server-for-revit:"):
                revit_idx = j
                break
        
        if revit_idx != -1:
            end_revit_idx = len(lines)
            indent = len(lines[revit_idx]) - len(lines[revit_idx].lstrip())
            for k in range(revit_idx + 1, len(lines)):
                line = lines[k]
                if line.strip():
                    line_indent = len(line) - len(line.lstrip())
                    if line_indent <= indent:
                        end_revit_idx = k
                        break
            lines[revit_idx:end_revit_idx] = [
                '  mcp-server-for-revit:',
                '    command: "node"',
                '    args:',
                f'      - "{server_js_forward}"'
            ]
        else:
            lines.insert(mcp_servers_idx + 1, '  mcp-server-for-revit:\n    command: "node"\n    args:\n      - "' + server_js_forward + '"')
            
    try:
        with open(path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        write_log(f"Successfully configured YAML at {path}")
        return True
    except Exception as e:
        write_log(f"Error writing to YAML at {path}: {e}")
        return False

def update_toml_config(path, server_js_forward):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
    except Exception:
        pass
    content = ""
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
        except Exception:
            pass
            
    lines = content.splitlines()
    target_header = "[mcp_servers.mcp-server-for-revit]"
    new_block = [
        target_header,
        'command = "node"',
        f'args = ["{server_js_forward}"]'
    ]
    
    header_idx = -1
    for i, line in enumerate(lines):
        if line.strip() == target_header:
            header_idx = i
            break
            
    if header_idx != -1:
        end_block_idx = len(lines)
        for j in range(header_idx + 1, len(lines)):
            line = lines[j].strip()
            if line.startswith("[") and line.endswith("]"):
                end_block_idx = j
                break
        lines[header_idx:end_block_idx] = new_block
    else:
        if lines and lines[-1].strip():
            lines.append("")
        lines.extend(new_block)
        
    try:
        with open(path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        write_log(f"Successfully configured TOML at {path}")
        return True
    except Exception as e:
        write_log(f"Error writing to TOML at {path}: {e}")
        return False

def update_copilot_config(path, server_js_forward):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
    except Exception:
        pass
    config = {}
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                config = json.load(f)
        except Exception:
            pass
            
    if "mcpServers" not in config or not isinstance(config["mcpServers"], dict):
        config["mcpServers"] = {}
    config["mcpServers"]["mcp-server-for-revit"] = {
        "type": "stdio",
        "command": "node",
        "args": [server_js_forward]
    }
    
    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2)
        write_log(f"Successfully configured Copilot config at {path}")
        return True
    except Exception as e:
        write_log(f"Error writing to Copilot config at {path}: {e}")
        return False

def detect_and_configure_all():
    userprofile = os.path.expanduser("~")
    appdata = os.environ.get("APPDATA", "")
    localappdata = os.environ.get("LOCALAPPDATA", "")
    server_js = get_server_js_path()
    
    write_log(f"Targeting server.js path: {server_js}")
    write_log(f"UserProfile: {userprofile}")
    write_log(f"AppData: {appdata}")
    
    # Client Definitions
    clients = {
        "cursor": {
            "name": "Cursor",
            "configs": [
                (os.path.join(userprofile, ".cursor", "mcp.json"), "mcpServers"),
                (os.path.join(appdata, "Cursor", "User", "globalStorage", "storage.json"), "cursor")
            ],
            "dirs": [os.path.join(appdata, "Cursor"), os.path.join(userprofile, ".cursor")]
        },
        "windsurf": {
            "name": "Windsurf",
            "configs": [
                (os.path.join(userprofile, ".codeium", "windsurf", "mcp_config.json"), "mcpServers"),
                (os.path.join(appdata, "Windsurf", "User", "globalStorage", "storage.json"), "cursor"),
                (os.path.join(appdata, "Code - Windsurf", "User", "globalStorage", "storage.json"), "cursor")
            ],
            "dirs": [os.path.join(userprofile, ".codeium", "windsurf"), os.path.join(appdata, "Windsurf"), os.path.join(appdata, "Code - Windsurf")]
        },
        "claude": {
            "name": "Claude Desktop",
            "configs": [
                (os.path.join(appdata, "Claude", "claude_desktop_config.json"), "mcpServers"),
                (os.path.join(appdata, "Subliminal", "Claude", "claude_desktop_config.json"), "mcpServers"),
                (os.path.join(appdata, "EasyConnect", "Claude", "claude_desktop_config.json"), "mcpServers")
            ],
            "dirs": [os.path.join(appdata, "Claude"), os.path.join(appdata, "Subliminal", "Claude"), os.path.join(appdata, "EasyConnect", "Claude")]
        },
        "cline": {
            "name": "Cline (VS Code Extension)",
            "configs": [
                (os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"), "mcpServers")
            ],
            "dirs": [os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev")]
        },
        "vscode": {
            "name": "VS Code (mcp.json)",
            "configs": [
                (os.path.join(appdata, "Code", "User", "mcp.json"), "mcpServers")
            ],
            "dirs": [os.path.join(appdata, "Code", "User")]
        },
        "copilot": {
            "name": "GitHub Copilot CLI",
            "configs": [
                (os.path.join(userprofile, ".copilot", "mcp-config.json"), "copilot")
            ],
            "dirs": [os.path.join(userprofile, ".copilot")]
        },
        "gemini_cli": {
            "name": "Gemini CLI / Agent",
            "configs": [
                (os.path.join(userprofile, ".gemini", "settings.json"), "mcpServers"),
                (os.path.join(userprofile, ".gemini", "config", "mcp_config.json"), "mcpServers")
            ],
            "dirs": [os.path.join(userprofile, ".gemini")]
        },
        "claude_code": {
            "name": "Claude Code",
            "configs": [
                (os.path.join(userprofile, ".claude.json"), "mcpServers"),
                (os.path.join(userprofile, ".mcp.json"), "mcpServers")
            ],
            "dirs": []
        },
        "warp": {
            "name": "Warp",
            "configs": [
                (os.path.join(userprofile, ".warp", "mcp.json"), "mcpServers")
            ],
            "dirs": [os.path.join(userprofile, ".warp")]
        },
        "antigravity": {
            "name": "Antigravity IDE",
            "configs": [
                (os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "storage.json"), "cursor"),
                (os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"), "mcpServers"),
                (os.path.join(userprofile, ".gemini", "config", "mcp_config.json"), "mcpServers")
            ],
            "dirs": [os.path.join(appdata, "Antigravity IDE")]
        },
        "opencode": {
            "name": "OpenCode",
            "configs": [
                (os.path.join(userprofile, ".opencode.json"), "opencode"),
                (os.path.join(appdata, "ai.opencode.desktop", "User", "globalStorage", "storage.json"), "cursor")
            ],
            "dirs": [os.path.join(appdata, "ai.opencode.desktop"), os.path.join(appdata, "opencode")]
        },
        "openclaw": {
            "name": "OpenClaw",
            "configs": [
                (os.path.join(userprofile, ".openclaw", "openclaw.json"), "openclaw")
            ],
            "dirs": [os.path.join(userprofile, ".openclaw")]
        },
        "hermes": {
            "name": "Hermes",
            "configs": [
                (os.path.join(userprofile, ".hermes", "config.yaml"), "yaml")
            ],
            "dirs": [os.path.join(userprofile, ".hermes")]
        },
        "codex": {
            "name": "Codex",
            "configs": [
                (os.path.join(userprofile, ".codex", "config.toml"), "toml")
            ],
            "dirs": [os.path.join(userprofile, ".codex")]
        }
    }
    
    detected_clients = []
    configured_paths = []
    
    # 1. Detection
    for key, info in clients.items():
        dirs_exist = [d for d in info["dirs"] if os.path.exists(d)]
        configs_exist = [c[0] for c in info["configs"] if os.path.exists(c[0])]
        
        is_installed = len(dirs_exist) > 0 or len(configs_exist) > 0
        if is_installed:
            detected_clients.append((key, info))
            
    print("\n--- DETECTED AI CLIENTS ---")
    for key, info in detected_clients:
        print(f"[*] {info['name']} ({key})")
        
    print("\n--- CONFIGURING MCP SERVERS ---")
    for key, info in detected_clients:
        print(f"\nConfiguring {info['name']}...")
        for config_path, style in info["configs"]:
            # If the directory for this configuration file's parent exists, we can write it.
            parent_dir = os.path.dirname(config_path)
            if os.path.exists(parent_dir):
                write_log(f"Writing config style '{style}' to: {config_path}")
                success = False
                if style == "yaml":
                    success = update_yaml_config(config_path, server_js)
                elif style == "toml":
                    success = update_toml_config(config_path, server_js)
                elif style == "copilot":
                    success = update_copilot_config(config_path, server_js)
                else:
                    success = update_json_config(config_path, server_js, style)
                if success:
                    configured_paths.append(config_path)
            else:
                write_log(f"Skipping {config_path} because parent directory {parent_dir} does not exist.")
                
    print("\n--- SUMMARY OF WRITTEN FILES ---")
    if configured_paths:
        for path in configured_paths:
            print(f"[SUCCESS] Updated: {path}")
    else:
        print("[WARNING] No configurations were written!")

if __name__ == "__main__":
    detect_and_configure_all()
