namespace TopologyForge.Rendering;

/// <summary>
/// Content of the labels shown on the arrows of the HTML map.
/// </summary>
public enum TopologyEdgeLabelMode
{
    /// <summary>
    /// Each arrow shows its direction, port and protocol (for example <c>OUT :443/TCP</c>).
    /// </summary>
    Ports = 0,

    /// <summary>
    /// No label on the arrows; the ports remain listed in the server inspector.
    /// </summary>
    None = 1
}
