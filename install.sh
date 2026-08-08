#!/usr/bin/env bash
# Registers FaceCameraToggle as a Dalamud dev plugin (idempotent).
# Run while the game is CLOSED, then start the game.
set -euo pipefail

python3 - <<'EOF'
import json, os, uuid

cfg_path = os.path.expanduser("~/.xlcore/dalamudConfig.json")
dll = r"Z:\home\iwakura\FaceCameraToggle\bin\Debug\net10.0-windows\FaceCameraToggle.dll"

with open(cfg_path, encoding="utf-8") as f:
    cfg = json.load(f)

settings = cfg.setdefault("DevPluginSettings", {})
if dll in settings:
    print("Dev plugin already registered:", dll)
else:
    settings[dll] = {
        "$type": "Dalamud.Configuration.Internal.DevPluginSettings, Dalamud",
        "StartOnBoot": True,
        "NotifyForErrors": True,
        "AutomaticReloading": False,
        "WorkingPluginId": str(uuid.uuid4()),
        "DismissedValidationProblems": {
            "$type": "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], System.Private.CoreLib",
            "$values": [],
        },
    }
    with open(cfg_path, "w", encoding="utf-8") as f:
        json.dump(cfg, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("Registered dev plugin:", dll)
EOF
