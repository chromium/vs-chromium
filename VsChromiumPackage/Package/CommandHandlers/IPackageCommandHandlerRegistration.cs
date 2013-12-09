
namespace VsChromiumPackage.Package.CommandHandlers {
  /// <summary>
  /// Handles registration of global (i.e. package) command handlers.
  /// </summary>
  public interface IPackageCommandHandlerRegistration {
    void RegisterCommandHandlers();
  }
}