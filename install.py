import os
import shutil
import json
import sys
import subprocess
def write_log(msg):
    import datetime
    timestamp = datetime.datetime.now().isoformat()
    log_line = f"[{timestamp}] {msg}\n"
    
    # Try writing to running user's temp dir
    try:
        temp_dir = os.environ.get("TEMP", "C:\\Temp")
        log_path = os.path.join(temp_dir, "revit_mcp_install.log")
        with open(log_path, "a", encoding="utf-8") as f:
            f.write(log_line)
    except Exception:
        pass
        
    # Try writing to jreks's temp dir specifically
    try:
        log_path_jreks = "C:\\Users\\jreks\\AppData\\Local\\Temp\\revit_mcp_install.log"
        with open(log_path_jreks, "a", encoding="utf-8") as f:
            f.write(log_line)
    except Exception:
        pass

def find_revit_addin_folders(appdata=None):
    if not appdata:
        appdata = os.environ.get("APPDATA")
    if not appdata:
        return []
    revit_dir = os.path.join(appdata, "Autodesk", "Revit", "Addins")
    if not os.path.exists(revit_dir):
        return []
    
    folders = []
    for item in os.listdir(revit_dir):
        if item.isdigit() and os.path.isdir(os.path.join(revit_dir, item)):
            folders.append(item)
    return folders

def check_node_installed():
    try:
        subprocess.run(["node", "--version"], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        return True
    except Exception:
        return False

def update_json_config(path, server_js_forward, key_style="mcpServers"):
    try:
        os.makedirs(os.path.dirname(path), exist_ok=True)
    except Exception:
        pass
    config = {}
    if os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                content = f.read()
            if content.strip():
                try:
                    config = json.loads(content)
                except Exception:
                    import re
                    # Regex to match strings (group 1), multi-line comments (group 2), or single-line comments (group 3)
                    pattern = re.compile(
                        r'("(?:\\.|[^"\\])*")|(/\*.*?\*/)|(//[^\r\n]*)',
                        re.MULTILINE | re.DOTALL
                    )
                    def replacer(match):
                        if match.group(1) is not None:
                            return match.group(1)
                        return ''
                    clean_content = pattern.sub(replacer, content)
                    # Remove trailing commas before closing braces/brackets
                    clean_content = re.sub(r',\s*([\]}])', r'\1', clean_content)
                    try:
                        config = json.loads(clean_content)
                    except Exception as e:
                        print(f"Error parsing JSON in {path}: {e}")
                        return False
        except Exception as e:
            print(f"Error reading JSON from {path}: {e}")
            return False
            
    if key_style == "mcpServers":
        if "mcpServers" not in config or not isinstance(config["mcpServers"], dict):
            config["mcpServers"] = {}
        config["mcpServers"]["mcp-server-for-revit"] = {
            "command": "node",
            "args": [server_js_forward]
        }
    elif key_style == "opencode":
        if "mcp" not in config or not isinstance(config["mcp"], dict):
            config["mcp"] = {}
        config["mcp"]["mcp-server-for-revit"] = {
            "type": "local",
            "command": ["node", server_js_forward],
            "enabled": True
        }
    elif key_style == "openclaw":
        if "mcp" not in config or not isinstance(config["mcp"], dict):
            config["mcp"] = {}
        if "servers" not in config["mcp"] or not isinstance(config["mcp"]["servers"], dict):
            config["mcp"]["servers"] = {}
        config["mcp"]["servers"]["mcp-server-for-revit"] = {
            "command": "node",
            "args": [server_js_forward]
        }
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
        if "mcpServers" in config:
            if isinstance(config["mcpServers"], dict):
                config["mcpServers"]["mcp-server-for-revit"] = {
                    "command": "node",
                    "args": [server_js_forward]
                }
        else:
            config["mcpServers"] = {
                "mcp-server-for-revit": {
                    "command": "node",
                    "args": [server_js_forward]
                }
            }
            
        if "mcp.servers" in config:
            if isinstance(config["mcp.servers"], dict):
                config["mcp.servers"].update(mcp_config)
        else:
            config["mcp.servers"] = mcp_config

    try:
        with open(path, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2)
        return True
    except Exception as e:
        print(f"Error writing config to {path}: {e}")
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
        return True
    except Exception as e:
        print(f"Error writing YAML config to {path}: {e}")
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
        return True
    except Exception as e:
        print(f"Error writing TOML config to {path}: {e}")
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
                content = f.read()
            if content.strip():
                try:
                    config = json.loads(content)
                except Exception:
                    import re
                    pattern = re.compile(
                        r'("(?:\\.|[^"\\])*")|(/\*.*?\*/)|(//[^\r\n]*)',
                        re.MULTILINE | re.DOTALL
                    )
                    def replacer(match):
                        if match.group(1) is not None:
                            return match.group(1)
                        return ''
                    clean_content = pattern.sub(replacer, content)
                    clean_content = re.sub(r',\s*([\]}])', r'\1', clean_content)
                    try:
                        config = json.loads(clean_content)
                    except Exception as e:
                        print(f"Error parsing Copilot config in {path}: {e}")
                        return False
        except Exception as e:
            print(f"Error reading Copilot config from {path}: {e}")
            return False
            
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
        return True
    except Exception as e:
        print(f"Error writing Copilot config to {path}: {e}")
        return False

def configure_mcp_clients(server_js_path, appdata, userprofile, allowed_clients=None):
    configured = []
    server_js_forward = server_js_path.replace("\\", "/")
    write_log(f"configure_mcp_clients entry: server_js_path={server_js_path}, appdata={appdata}, userprofile={userprofile}, allowed_clients={allowed_clients}")
    
    # Helper to check if we are allowed to configure this client
    def is_allowed(client_id):
        if allowed_clients is None:
            return True
        return client_id in allowed_clients

    # 1. Claude Desktop
    if is_allowed("claude"):
        path = os.path.join(appdata, "Claude", "claude_desktop_config.json")
        write_log(f"Checking Claude Desktop: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing Claude Desktop config to {path}")
            if update_json_config(path, server_js_forward, "mcpServers"):
                configured.append("Claude Desktop")

    # 2. Cline (VS Code Extension)
    if is_allowed("cline"):
        path = os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")
        write_log(f"Checking Cline: path={path}")
        if os.path.exists(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(path))))) or allowed_clients is not None:
            write_log(f"Writing Cline config to {path}")
            if update_json_config(path, server_js_forward, "mcpServers"):
                configured.append("Cline (VS Code)")

    # 3. Cursor
    if is_allowed("cursor"):
        path_new = os.path.join(userprofile, ".cursor", "mcp.json")
        path_old = os.path.join(appdata, "Cursor", "User", "globalStorage", "storage.json")
        write_log(f"Checking Cursor: path_new={path_new}, path_old={path_old}")
        cursor_done = False
        if os.path.exists(os.path.dirname(path_new)) or allowed_clients is not None:
            write_log(f"Writing Cursor new config to {path_new}")
            if update_json_config(path_new, server_js_forward, "mcpServers"):
                cursor_done = True
        if os.path.exists(os.path.dirname(path_old)):
            write_log(f"Writing Cursor old config to {path_old}")
            if update_json_config(path_old, server_js_forward, "cursor"):
                cursor_done = True
        if cursor_done:
            configured.append("Cursor")

    # 4. Windsurf
    if is_allowed("windsurf"):
        path_new = os.path.join(userprofile, ".codeium", "windsurf", "mcp_config.json")
        path_old = os.path.join(appdata, "Windsurf", "User", "globalStorage", "storage.json")
        path_old2 = os.path.join(appdata, "Code - Windsurf", "User", "globalStorage", "storage.json")
        write_log(f"Checking Windsurf: path_new={path_new}, path_old={path_old}, path_old2={path_old2}")
        windsurf_done = False
        if os.path.exists(os.path.dirname(path_new)) or allowed_clients is not None:
            write_log(f"Writing Windsurf new config to {path_new}")
            if update_json_config(path_new, server_js_forward, "mcpServers"):
                windsurf_done = True
        for p in [path_old, path_old2]:
            if os.path.exists(os.path.dirname(p)):
                write_log(f"Writing Windsurf old config to {p}")
                if update_json_config(p, server_js_forward, "cursor"):
                    windsurf_done = True
        if windsurf_done:
            configured.append("Windsurf")

    # 5. VS Code (mcp.json)
    if is_allowed("vscode"):
        path = os.path.join(appdata, "Code", "User", "mcp.json")
        write_log(f"Checking VS Code: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing VS Code config to {path}")
            if update_json_config(path, server_js_forward, "mcpServers"):
                configured.append("VS Code")

    # 6. GitHub Copilot CLI
    if is_allowed("copilot"):
        path = os.path.join(userprofile, ".copilot", "mcp-config.json")
        write_log(f"Checking GitHub Copilot: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing Copilot config to {path}")
            if update_copilot_config(path, server_js_forward):
                configured.append("GitHub Copilot CLI")

    # 7. Gemini CLI
    if is_allowed("gemini_cli"):
        path_settings = os.path.join(userprofile, ".gemini", "settings.json")
        path_mcp = os.path.join(userprofile, ".gemini", "config", "mcp_config.json")
        write_log(f"Checking Gemini CLI: path_settings={path_settings}, path_mcp={path_mcp}")
        gemini_done = False
        if os.path.exists(os.path.dirname(path_settings)) or allowed_clients is not None:
            write_log(f"Writing Gemini settings config to {path_settings}")
            if update_json_config(path_settings, server_js_forward, "mcpServers"):
                gemini_done = True
        if os.path.exists(os.path.dirname(path_mcp)) or allowed_clients is not None:
            write_log(f"Writing Gemini mcp_config to {path_mcp}")
            if update_json_config(path_mcp, server_js_forward, "mcpServers"):
                gemini_done = True
        if gemini_done:
            configured.append("Gemini CLI")

    # 8. Claude Code
    if is_allowed("claude_code"):
        path_claude = os.path.join(userprofile, ".claude.json")
        path_mcp = os.path.join(userprofile, ".mcp.json")
        write_log(f"Checking Claude Code: path_claude={path_claude}, path_mcp={path_mcp}")
        claude_code_done = False
        if os.path.exists(userprofile) or allowed_clients is not None:
            write_log(f"Writing Claude Code config to {path_claude}")
            if update_json_config(path_claude, server_js_forward, "mcpServers"):
                claude_code_done = True
            write_log(f"Writing Claude Code config to {path_mcp}")
            if update_json_config(path_mcp, server_js_forward, "mcpServers"):
                claude_code_done = True
        if claude_code_done:
            configured.append("Claude Code")

    # 9. Warp
    if is_allowed("warp"):
        path = os.path.join(userprofile, ".warp", "mcp.json")
        write_log(f"Checking Warp: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing Warp config to {path}")
            if update_json_config(path, server_js_forward, "mcpServers"):
                configured.append("Warp")

    # 10. Antigravity IDE
    if is_allowed("antigravity"):
        # Official path: ~/.gemini/config/mcp_config.json (confirmed by Google docs)
        path = os.path.join(userprofile, ".gemini", "config", "mcp_config.json")
        write_log(f"Checking Antigravity IDE: path={path}")
        write_log(f"Writing Antigravity config to {path}")
        if update_json_config(path, server_js_forward, "mcpServers"):
            configured.append("Antigravity IDE")

    # 11. OpenCode
    if is_allowed("opencode"):
        # Official path: ~/.config/opencode/opencode.json
        path = os.path.join(userprofile, ".config", "opencode", "opencode.json")
        write_log(f"Checking OpenCode: path={path}")
        write_log(f"Writing OpenCode config to {path}")
        if update_json_config(path, server_js_forward, "opencode"):
            configured.append("OpenCode")

    # 12. OpenClaw
    if is_allowed("openclaw"):
        path = os.path.join(userprofile, ".openclaw", "openclaw.json")
        write_log(f"Checking OpenClaw: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing OpenClaw config to {path}")
            if update_json_config(path, server_js_forward, "openclaw"):
                configured.append("OpenClaw")

    # 13. Hermes
    if is_allowed("hermes"):
        path = os.path.join(userprofile, ".hermes", "config.yaml")
        write_log(f"Checking Hermes: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing Hermes config to {path}")
            if update_yaml_config(path, server_js_forward):
                configured.append("Hermes")

    # 14. Codex
    if is_allowed("codex"):
        path = os.path.join(userprofile, ".codex", "config.toml")
        write_log(f"Checking Codex: path={path}")
        if os.path.exists(os.path.dirname(path)) or allowed_clients is not None:
            write_log(f"Writing Codex config to {path}")
            if update_toml_config(path, server_js_forward):
                configured.append("Codex")

    write_log(f"configure_mcp_clients exit: configured={configured}")
    return configured

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Revit MCP Plugin & Server Auto Installer")
    parser.add_argument("--revit", nargs="+", help="Revit versions to install (e.g. 2024 2027)")
    parser.add_argument("--clients", nargs="*", default=[], help="AI clients to configure (e.g. claude cursor antigravity cline windsurf vscode copilot gemini_cli claude_code warp opencode openclaw hermes codex)")
    parser.add_argument("--config-only", action="store_true", help="Only configure AI clients, skip files deployment")
    parser.add_argument("--user-appdata", help="Target user AppData path")
    parser.add_argument("--user-profile", help="Target user Profile path")
    args = parser.parse_args()
    
    allowed_revit = args.revit if args.revit else None
    allowed_clients = args.clients if args.clients else None

    print("====================================================")
    print("     Revit MCP Plugin & Server - Auto Installer")
    print("====================================================")
    
    # Resolve appdata and userprofile, prioritizing overrides passed from installer
    user_appdata = args.user_appdata if args.user_appdata else os.environ.get("APPDATA")
    user_profile = args.user_profile if args.user_profile else os.path.expanduser("~")
    
    if not user_appdata:
        print("[ERROR] APPDATA environment variable or path not found. Installation aborted.")
        sys.exit(1)
        
    target_app_dir = os.path.join(user_appdata, "revit_mcp_plugin")
    server_js_path = os.path.join(target_app_dir, "server", "build", "index.js")
    
    if args.config_only:
        print("[INFO] Running in CONFIG-ONLY mode.")
        print("[INFO] Autoconfiguring AI clients...")
        configured_clients = configure_mcp_clients(server_js_path, user_appdata, user_profile, allowed_clients=allowed_clients)
        if configured_clients:
            print(f"[SUCCESS] Configured clients: {', '.join(configured_clients)}")
        else:
            print("[INFO] No active AI client configs were found to auto-update.")
        print("====================================================")
        print("           CONFIGURATION COMPLETED")
        print("====================================================")
        return

    # 1. Check Node.js
    node_ok = check_node_installed()
    if not node_ok:
        print("[WARNING] Node.js was not found in your system PATH.")
        print("          Please install Node.js (https://nodejs.org) to run the MCP server.")
        print("----------------------------------------------------")
    else:
        print("[INFO] Node.js is installed.")
        
    # 2. Deploy Revit add-ins
    revit_versions = find_revit_addin_folders(user_appdata)
    if not revit_versions:
        print("[WARNING] No Autodesk Revit installation folders found in AppData.")
    else:
        print(f"[INFO] Found Revit versions: {', '.join(revit_versions)}")
        
    bin_dir = "plugin/bin"
    if not os.path.exists(bin_dir):
        print("[ERROR] 'plugin/bin' directory not found. Please build the plugin first.")
        sys.exit(1)
        
    # Search for compiled folders
    addin_copied = []
    for folder in os.listdir(bin_dir):
        if folder.startswith("AddIn"):
            parts = folder.split()
            version = None
            for part in parts:
                if part.isdigit() and len(part) == 4:
                    version = part
                    break
            
            if version and version in revit_versions:
                if allowed_revit is not None and version not in allowed_revit:
                    continue
                src_folder_path = os.path.join(bin_dir, folder)
                dest_folder_path = os.path.join(user_appdata, "Autodesk", "Revit", "Addins", version)
                
                # Copy files
                try:
                    for item in os.listdir(src_folder_path):
                        src_item = os.path.join(src_folder_path, item)
                        dest_item = os.path.join(dest_folder_path, item)
                        
                        if os.path.isdir(src_item):
                            if os.path.exists(dest_item):
                                shutil.rmtree(dest_item)
                            shutil.copytree(src_item, dest_item)
                        else:
                            shutil.copy2(src_item, dest_item)
                    addin_copied.append(version)
                except Exception as e:
                    print(f"[ERROR] Failed to copy addin for Revit {version}: {e}")

    if addin_copied:
        print(f"[SUCCESS] Revit Add-in installed for versions: {', '.join(addin_copied)}")
    else:
        print("[WARNING] No compiled add-ins were matched and copied.")
        
    # 3. Deploy MCP Server
    print("[INFO] Deploying MCP Server to AppData...")
    server_dest = os.path.join(target_app_dir, "server")
    if os.path.exists(server_dest):
        try:
            shutil.rmtree(server_dest)
        except Exception as e:
            print(f"[WARNING] Could not clean old server folder: {e}")
            
    os.makedirs(server_dest, exist_ok=True)
    
    server_src = "server"
    try:
        # Copy build
        shutil.copytree(os.path.join(server_src, "build"), os.path.join(server_dest, "build"), dirs_exist_ok=True)
        # Copy node_modules
        shutil.copytree(os.path.join(server_src, "node_modules"), os.path.join(server_dest, "node_modules"), dirs_exist_ok=True)
        # Copy package.json
        shutil.copy2(os.path.join(server_src, "package.json"), os.path.join(server_dest, "package.json"))
        
        print("[SUCCESS] MCP Server deployed successfully.")
    except Exception as e:
        print(f"[ERROR] Failed to deploy MCP Server: {e}")
        sys.exit(1)
        
    # 4. Auto-configure clients
    print("[INFO] Autoconfiguring AI clients...")
    configured_clients = configure_mcp_clients(server_js_path, user_appdata, user_profile, allowed_clients=allowed_clients)
    if configured_clients:
        print(f"[SUCCESS] Configured clients: {', '.join(configured_clients)}")
    else:
        print("[INFO] No active AI client configs were found to auto-update.")
        print(f"       You can manually point your AI client to: {server_js_path}")
        
    print("====================================================")
    print("              INSTALLATION COMPLETE")
    print("====================================================")
    print("Next steps:")
    print("1. Restart your AI client (Cursor, Claude, VS Code, Windsurf, OpenCode, OpenClaw, Hermes, Codex).")
    print("2. Launch Revit and enable 'MCP Server' on the Ribbon.")
    print("3. Start prompting your AI to interact with Revit!")
    print("====================================================")

if __name__ == "__main__":
    main()
