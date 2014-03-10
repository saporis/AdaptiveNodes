namespace WP_Controller
{
    public enum DeviceType
    {
        Invalid,        // Default item when no items loaded
        Unknown,        // Default when first seen and not probed
        Unconfigured,
        Switch,
        IO
    }
}
