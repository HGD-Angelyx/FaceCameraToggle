using System;
using Dalamud.Configuration;

namespace FaceCameraToggle;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = false;
}
