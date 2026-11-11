namespace Base.Authorization;

/// <summary>
/// Defines the standard permission actions. Applications add their own actions by deriving from this class,
/// for example <c>public sealed class AppActions : Actions { public const string Mark = "mark"; }</c>.
/// </summary>
public abstract class Actions
{
    /// <summary>
    /// Allows viewing the caller's own or explicitly shared resources.
    /// </summary>
    public const string View = "view";

    /// <summary>
    /// Allows viewing all resources of a module.
    /// </summary>
    public const string ViewAll = "view-all";

    /// <summary>
    /// Allows creating resources.
    /// </summary>
    public const string Create = "create";

    /// <summary>
    /// Allows editing resources.
    /// </summary>
    public const string Edit = "edit";

    /// <summary>
    /// Allows deleting resources.
    /// </summary>
    public const string Delete = "delete";
}
