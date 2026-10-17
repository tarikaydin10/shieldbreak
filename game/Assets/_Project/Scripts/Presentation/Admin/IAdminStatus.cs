namespace Shieldbreak.Presentation.Admin
{
    /// <summary>
    /// Implemented by MonoBehaviours that want a status line in the admin menu (current camera view, sim tick, ...).
    /// </summary>
    public interface IAdminStatus
    {
        string AdminStatusLabel { get; }
        string AdminStatus { get; }
    }
}
