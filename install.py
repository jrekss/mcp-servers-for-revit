import os
import shutil
import json
import sys
import subprocess

def find_revit_addin_folders():
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

def configure_mcp_clients(server_js_path):
    appdata = os.environ.get("APPDATA")
    if not appdata:
        return []
    
    configured = []
    
    # 1. Claude Desktop config path
    claude_paths = [
        os.path.join(appdata, "Subliminal", "Claude", "claude_desktop_config.json"),
        os.path.join(appdata, "Claude", "claude_desktop_config.json"),
        os.path.join(appdata, "EasyConnect", "Claude", "claude_desktop_config.json")
    ]
    
    for path in claude_paths:
        if os.path.exists(os.path.dirname(path)):
            config = {}
            if os.path.exists(path):
                try:
                    with open(path, "r", encoding="utf-8") as f:
                        config = json.load(f)
                except Exception:
                    pass
            
            if "mcpServers" not in config:
                config["mcpServers"] = {}
                
            config["mcpServers"]["mcp-server-for-revit"] = {
                "command": "node",
                "args": [server_js_path.replace("\\", "/")]
            }
            
            try:
                with open(path, "w", encoding="utf-8") as f:
                    json.dump(config, f, indent=2)
                configured.append(f"Claude Desktop ({os.path.basename(path)})")
            except Exception as e:
                print(f"Error configuring Claude Desktop: {e}")

    # 2. VS Code / Cline config path
    cline_paths = [
        os.path.join(appdata, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
        os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json")
    ]
    
    for path in cline_paths:
        if os.path.exists(os.path.dirname(path)):
            config = {}
            if os.path.exists(path):
                try:
                    with open(path, "r", encoding="utf-8") as f:
                        config = json.load(f)
                except Exception:
                    pass
            
            if "mcpServers" not in config:
                config["mcpServers"] = {}
                
            config["mcpServers"]["mcp-server-for-revit"] = {
                "command": "node",
                "args": [server_js_path.replace("\\", "/")]
            }
            
            try:
                with open(path, "w", encoding="utf-8") as f:
                    json.dump(config, f, indent=2)
                configured.append(f"Cline / VS Code ({os.path.basename(os.path.dirname(os.path.dirname(os.path.dirname(path))))})")
            except Exception as e:
                print(f"Error configuring Cline: {e}")

    # 3. Cursor / Antigravity IDE global storage.json path
    cursor_paths = [
        os.path.join(appdata, "Cursor", "User", "globalStorage", "storage.json"),
        os.path.join(appdata, "Antigravity IDE", "User", "globalStorage", "storage.json"),
        os.path.join(appdata, "ai.opencode.desktop", "User", "globalStorage", "storage.json")
    ]
    
    for path in cursor_paths:
        if os.path.exists(path):
            config = {}
            try:
                with open(path, "r", encoding="utf-8") as f:
                    config = json.load(f)
            except Exception:
                continue
            
            # Cursor and forks save MCP servers under "mcpServers" or "mcp.servers"
            # Let's write to both to ensure compatibility
            server_js_forward = server_js_path.replace("\\", "/")
            mcp_config = {
                "mcp-server-for-revit": {
                    "name": "mcp-server-for-revit",
                    "type": "command",
                    "command": f'node "{server_js_forward}"',
                    "args": "",
                    "env": {}
                }
            }
            
            # Update standard VS Code styled mcpServers dictionary if present
            if "mcpServers" in config:
                if isinstance(config["mcpServers"], dict):
                    config["mcpServers"]["mcp-server-for-revit"] = {
                        "command": "node",
                        "args": [server_js_path.replace("\\", "/")]
                    }
            else:
                config["mcpServers"] = {
                    "mcp-server-for-revit": {
                        "command": "node",
                        "args": [server_js_path.replace("\\", "/")]
                    }
                }
                
            # Update Cursor custom mcpServers setting
            if "mcp.servers" in config:
                if isinstance(config["mcp.servers"], dict):
                    config["mcp.servers"].update(mcp_config)
            else:
                config["mcp.servers"] = mcp_config
                
            try:
                with open(path, "w", encoding="utf-8") as f:
                    json.dump(config, f, indent=2)
                configured.append(f"Cursor / IDE ({os.path.basename(os.path.dirname(os.path.dirname(os.path.dirname(path))))})")
            except Exception as e:
                print(f"Error configuring Cursor: {e}")

    return configured

def main():
    print("====================================================")
    print("     Revit MCP Plugin & Server - Auto Installer")
    print("====================================================")
    
    # 1. Check Node.js
    node_ok = check_node_installed()
    if not node_ok:
        print("[WARNING] Node.js was not found in your system PATH.")
        print("          Please install Node.js (https://nodejs.org) to run the MCP server.")
        print("----------------------------------------------------")
    else:
        print("[INFO] Node.js is installed.")
        
    appdata = os.environ.get("APPDATA")
    if not appdata:
        print("[ERROR] APPDATA environment variable not found. Installation aborted.")
        sys.exit(1)
        
    target_app_dir = os.path.join(appdata, "revit_mcp_plugin")
    
    # 2. Deploy Revit add-ins
    revit_versions = find_revit_addin_folders()
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
            # Extract Revit version (e.g., "AddIn 2027 Debug R27" -> "2027")
            version = None
            for part in parts:
                if part.isdigit() and len(part) == 4:
                    version = part
                    break
            
            if version and version in revit_versions:
                src_folder_path = os.path.join(bin_dir, folder)
                dest_folder_path = os.path.join(appdata, "Autodesk", "Revit", "Addins", version)
                
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
        
        server_js_path = os.path.join(server_dest, "build", "index.js")
        print("[SUCCESS] MCP Server deployed successfully.")
    except Exception as e:
        print(f"[ERROR] Failed to deploy MCP Server: {e}")
        sys.exit(1)
        
    # 4. Auto-configure clients
    print("[INFO] Autoconfiguring AI clients...")
    configured_clients = configure_mcp_clients(server_js_path)
    if configured_clients:
        print(f"[SUCCESS] Configured clients: {', '.join(configured_clients)}")
    else:
        print("[INFO] No active AI client configs were found to auto-update.")
        print(f"       You can manually point your AI client to: {server_js_path}")
        
    print("====================================================")
    print("              INSTALLATION COMPLETE")
    print("====================================================")
    print("Next steps:")
    print("1. Restart your AI client (Cursor, Claude, VS Code).")
    print("2. Launch Revit and enable 'MCP Server' on the Ribbon.")
    print("3. Start prompting your AI to interact with Revit!")
    print("====================================================")

if __name__ == "__main__":
    main()
